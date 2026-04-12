// CS0162: ForceLicensed is a compile-time toggle; some branches are intentionally
// unreachable depending on its value. Suppress for the whole file.
#pragma warning disable CS0162

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AvyScanLab.Services;

/// <summary>
/// Single source of truth for "trial vs licensed" state, backed by the
/// Lemon Squeezy License Keys API.
///
/// Trial gates throughout the app (single clip, no custom-filter editing, etc.)
/// check <see cref="IsLicensed"/> at runtime. License state is persisted in
/// <c>%AppData%\AvyScanLab\license.dat</c> as JSON containing the license key,
/// the Lemon Squeezy instance id (returned on first activation), and the
/// timestamp of the last successful server-side validation.
///
/// Online behaviour:
///   • <see cref="TryActivateAsync"/> calls <c>POST /v1/licenses/activate</c> and
///     records the returned <c>instance_id</c>. The product enforces a max of
///     2 activations per key (configured in the LS dashboard).
///   • <see cref="InitializeAsync"/> re-validates silently in the background on
///     every start via <c>POST /v1/licenses/validate</c>.
///   • <see cref="DeactivateAsync"/> calls <c>POST /v1/licenses/deactivate</c> to
///     free the activation slot server-side before wiping the local file.
///
/// Offline grace:
///   • As long as the local file's <c>last_validated_utc</c> is within
///     <see cref="AppConstants.OfflineGraceDays"/>, the app stays unlocked even
///     if validation fails (network down, server error, etc.).
///   • Past the grace window, a failed validation silently downgrades to trial
///     and raises <see cref="LicenseChanged"/>.
///
/// Cheat sheet:
///   • Set <see cref="ForceLicensed"/> to <c>true</c> below to unlock everything
///     at compile time (bypasses LS entirely). Useful for dev/test builds.
///   • To restore trial behaviour, set it back to <c>false</c> AND delete the
///     license file via <see cref="DeactivateAsync"/> or by removing license.dat.
/// </summary>
public static class LicenseService
{
    // ─────────────────────────────────────────────────────────────────
    //  DEV TOGGLE — flip to true to force-unlock for testing.
    //  ⚠️ Must be false for production / trial builds.
    // ─────────────────────────────────────────────────────────────────
    public const bool ForceLicensed = true;

    private const string LicenseFileName = "license.dat";

    private static string LicensePath => AppConstants.GetAppDataPath(LicenseFileName);

    // Shared HttpClient instance — cheap to reuse, avoids socket exhaustion.
    private static readonly HttpClient Http = CreateHttpClient();

    // Serializer configured to ignore unknown properties so LS can add fields
    // to their response without breaking us.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    // In-memory cached state.
    private static LicenseRecord? _record;

    /// <summary>True when the user has a valid license (or ForceLicensed is on).</summary>
    public static bool IsLicensed { get; private set; }

    /// <summary>The currently loaded license key, if any.</summary>
    public static string? LicenseKey => _record?.LicenseKey;

    /// <summary>Fired whenever <see cref="IsLicensed"/> changes (after activate/deactivate/revalidate).</summary>
    public static event Action? LicenseChanged;

    static LicenseService()
    {
        if (ForceLicensed)
        {
            IsLicensed = true;
            _record = new LicenseRecord
            {
                LicenseKey = "FORCE-LICENSED",
                InstanceId = null,
                InstanceName = Environment.MachineName,
                LastValidatedUtc = DateTime.UtcNow,
                Status = "active"
            };
            return;
        }

        // Load whatever's on disk synchronously so IsLicensed is correct the
        // moment the first UI binding is evaluated. Server re-validation
        // happens later in InitializeAsync().
        TryLoadFromDisk();
    }

    /// <summary>
    /// Structural check on a license key. Lemon Squeezy keys are UUIDs in the
    /// canonical 8-4-4-4-12 form. This is only a format check — it does not
    /// contact the server.
    /// </summary>
    public static bool Validate(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return Guid.TryParseExact(key.Trim(), "D", out _);
    }

    /// <summary>
    /// Called once at app startup (fire-and-forget). Re-validates the persisted
    /// license against Lemon Squeezy if we have one; honours the offline grace
    /// window on network failure.
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (ForceLicensed) return;
        if (_record is null || string.IsNullOrWhiteSpace(_record.LicenseKey)) return;

        try
        {
            await RevalidateAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Swallow — RevalidateAsync already handled the grace-window fallback.
        }
    }

    /// <summary>
    /// Activates <paramref name="key"/> against Lemon Squeezy. On success, persists
    /// the returned instance id and unlocks the app. On failure, returns an
    /// error message suitable for display (localized English — caller is
    /// responsible for UI-side wording).
    /// </summary>
    public static async Task<ActivationResult> TryActivateAsync(string? key, CancellationToken ct = default)
    {
        if (ForceLicensed) return ActivationResult.Ok();

        var trimmed = (key ?? string.Empty).Trim();
        if (!Validate(trimmed))
            return ActivationResult.Fail("Invalid license key format.");

        LsActivateResponse? resp;
        try
        {
            var instanceName = BuildInstanceName();
            using var form = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("license_key", trimmed),
                new System.Collections.Generic.KeyValuePair<string, string>("instance_name", instanceName),
            });

            using var http = await Http.PostAsync(
                $"{AppConstants.LemonSqueezyApiBase}/v1/licenses/activate",
                form,
                ct).ConfigureAwait(false);

            resp = await http.Content
                .ReadFromJsonAsync<LsActivateResponse>(JsonOpts, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ActivationResult.Fail($"Network error: {ex.Message}");
        }

        if (resp is null)
            return ActivationResult.Fail("Empty response from license server.");

        if (!resp.Activated || resp.Error is not null)
            return ActivationResult.Fail(resp.Error ?? "Activation refused by server.");

        // Sanity check: make sure this key is for OUR product. Prevents a user
        // pasting a key bought for a different LS product in the same store.
        if (resp.Meta is { ProductId: var pid } &&
            !string.IsNullOrWhiteSpace(AppConstants.LemonSqueezyProductId) &&
            !AppConstants.LemonSqueezyProductId.StartsWith("PRODUCT_ID_") &&
            pid.ToString() != AppConstants.LemonSqueezyProductId)
        {
            return ActivationResult.Fail("This license key is not valid for AvyScan Lab.");
        }

        _record = new LicenseRecord
        {
            LicenseKey = trimmed,
            InstanceId = resp.Instance?.Id,
            InstanceName = resp.Instance?.Name ?? BuildInstanceName(),
            LastValidatedUtc = DateTime.UtcNow,
            Status = resp.LicenseKey?.Status ?? "active"
        };

        SaveToDisk(_record);
        IsLicensed = true;
        LicenseChanged?.Invoke();
        return ActivationResult.Ok();
    }

    /// <summary>
    /// Releases the activation slot server-side, wipes the local file and
    /// locks the app back to trial mode. Even if the server call fails, we
    /// still delete the local state — the user asked to deactivate and
    /// should not be stuck.
    /// </summary>
    public static async Task DeactivateAsync(CancellationToken ct = default)
    {
        if (ForceLicensed) return;

        var record = _record;
        if (record is not null &&
            !string.IsNullOrWhiteSpace(record.LicenseKey) &&
            !string.IsNullOrWhiteSpace(record.InstanceId))
        {
            try
            {
                using var form = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("license_key", record.LicenseKey!),
                    new System.Collections.Generic.KeyValuePair<string, string>("instance_id", record.InstanceId!),
                });
                using var _ = await Http.PostAsync(
                    $"{AppConstants.LemonSqueezyApiBase}/v1/licenses/deactivate",
                    form,
                    ct).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the local deactivation below always proceeds.
            }
        }

        ClearLocalState();
        LicenseChanged?.Invoke();
    }

    /// <summary>
    /// Contacts Lemon Squeezy to re-check the current license. On success,
    /// refreshes <c>last_validated_utc</c>. On failure, consults the offline
    /// grace window before deciding whether to downgrade to trial.
    /// </summary>
    public static async Task RevalidateAsync(CancellationToken ct = default)
    {
        if (ForceLicensed) return;

        var record = _record;
        if (record is null || string.IsNullOrWhiteSpace(record.LicenseKey))
        {
            IsLicensed = false;
            return;
        }

        LsValidateResponse? resp = null;
        bool networkOk = false;
        try
        {
            using var form = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("license_key", record.LicenseKey!),
                new System.Collections.Generic.KeyValuePair<string, string>("instance_id", record.InstanceId ?? string.Empty),
            });
            using var http = await Http.PostAsync(
                $"{AppConstants.LemonSqueezyApiBase}/v1/licenses/validate",
                form,
                ct).ConfigureAwait(false);

            resp = await http.Content
                .ReadFromJsonAsync<LsValidateResponse>(JsonOpts, ct)
                .ConfigureAwait(false);
            networkOk = resp is not null;
        }
        catch
        {
            networkOk = false;
        }

        if (networkOk && resp!.Valid)
        {
            // Server confirms the key is still good. Refresh timestamp and stay unlocked.
            record.LastValidatedUtc = DateTime.UtcNow;
            record.Status = resp.LicenseKey?.Status ?? record.Status;
            SaveToDisk(record);
            if (!IsLicensed)
            {
                IsLicensed = true;
                LicenseChanged?.Invoke();
            }
            return;
        }

        if (networkOk && !resp!.Valid)
        {
            // Server says NO (revoked, refunded, deactivated elsewhere). Hard downgrade.
            ClearLocalState();
            LicenseChanged?.Invoke();
            return;
        }

        // Network failure — honour the offline grace window.
        var ageDays = (DateTime.UtcNow - record.LastValidatedUtc).TotalDays;
        if (ageDays <= AppConstants.OfflineGraceDays)
        {
            // Stay unlocked; try again on next start.
            if (!IsLicensed)
            {
                IsLicensed = true;
                LicenseChanged?.Invoke();
            }
            return;
        }

        // Beyond grace — silently downgrade.
        IsLicensed = false;
        LicenseChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Internals
    // ─────────────────────────────────────────────────────────────────

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AvyScanLab/1.0");
        return client;
    }

    private static string BuildInstanceName()
    {
        // Machine name is enough to let the user recognize which slot is which
        // in the LS customer portal. Username is omitted for privacy.
        return Environment.MachineName;
    }

    private static void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(LicensePath)) return;
            var json = File.ReadAllText(LicensePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            // Tolerate the legacy plain-text format by trying JSON first and
            // falling back to "assume unlicensed" on parse failure.
            var rec = JsonSerializer.Deserialize<LicenseRecord>(json, JsonOpts);
            if (rec is null || string.IsNullOrWhiteSpace(rec.LicenseKey)) return;

            _record = rec;

            // Grant immediate local unlock if we're within the grace window;
            // RevalidateAsync() later confirms with the server.
            var ageDays = (DateTime.UtcNow - rec.LastValidatedUtc).TotalDays;
            IsLicensed = ageDays <= AppConstants.OfflineGraceDays;
        }
        catch
        {
            // Best-effort: a corrupt license file leaves the app in trial mode.
        }
    }

    private static void SaveToDisk(LicenseRecord record)
    {
        try
        {
            var dir = Path.GetDirectoryName(LicensePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(record, JsonOpts);
            File.WriteAllText(LicensePath, json);
        }
        catch
        {
            // Even if we can't persist, the in-memory state is still correct
            // for the current session.
        }
    }

    private static void ClearLocalState()
    {
        try
        {
            if (File.Exists(LicensePath)) File.Delete(LicensePath);
        }
        catch { /* best effort */ }

        _record = null;
        IsLicensed = false;
    }

}

// ─────────────────────────────────────────────────────────────────────
//  Types used by LicenseService
//
//  These are deliberately top-level (not nested inside LicenseService)
//  so that Obfuscar's <SkipType> entries can reference them by simple
//  name. Nested types under a skipped outer type are still obfuscated,
//  which would garble stack traces even though JsonPropertyName-based
//  serialization would keep working.
// ─────────────────────────────────────────────────────────────────────

/// <summary>Result of <see cref="LicenseService.TryActivateAsync"/>.</summary>
public readonly struct ActivationResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }

    private ActivationResult(bool success, string? error)
    {
        Success = success;
        ErrorMessage = error;
    }

    public static ActivationResult Ok() => new(true, null);
    public static ActivationResult Fail(string message) => new(false, message);
}

/// <summary>Persisted local license state (serialized to license.dat).</summary>
internal sealed class LicenseRecord
{
    [JsonPropertyName("license_key")]        public string? LicenseKey { get; set; }
    [JsonPropertyName("instance_id")]        public string? InstanceId { get; set; }
    [JsonPropertyName("instance_name")]      public string? InstanceName { get; set; }
    [JsonPropertyName("last_validated_utc")] public DateTime LastValidatedUtc { get; set; }
    [JsonPropertyName("status")]             public string? Status { get; set; }
}

// ─── Lemon Squeezy API DTOs ──────────────────────────────────────────
// Only the fields we actually consume are mapped; LS is free to add more.

internal sealed class LsActivateResponse
{
    [JsonPropertyName("activated")]   public bool Activated { get; set; }
    [JsonPropertyName("error")]       public string? Error { get; set; }
    [JsonPropertyName("license_key")] public LsLicenseKey? LicenseKey { get; set; }
    [JsonPropertyName("instance")]    public LsInstance? Instance { get; set; }
    [JsonPropertyName("meta")]        public LsMeta? Meta { get; set; }
}

internal sealed class LsValidateResponse
{
    [JsonPropertyName("valid")]       public bool Valid { get; set; }
    [JsonPropertyName("error")]       public string? Error { get; set; }
    [JsonPropertyName("license_key")] public LsLicenseKey? LicenseKey { get; set; }
    [JsonPropertyName("meta")]        public LsMeta? Meta { get; set; }
}

internal sealed class LsLicenseKey
{
    [JsonPropertyName("id")]     public long Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("key")]    public string? Key { get; set; }
}

internal sealed class LsInstance
{
    [JsonPropertyName("id")]   public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal sealed class LsMeta
{
    [JsonPropertyName("store_id")]   public long StoreId { get; set; }
    [JsonPropertyName("product_id")] public long ProductId { get; set; }
}
