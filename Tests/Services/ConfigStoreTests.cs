using System.Collections.Generic;
using CleanScan.Services;
using Xunit;

namespace CleanScan.Tests.Services;

public class ConfigStoreTests
{
    private readonly ConfigStore _store = new();

    // ── Get ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_ReturnsEmptyString_WhenKeyAbsent()
    {
        Assert.Equal("", _store.Get("missing"));
    }

    [Fact]
    public void Get_ReturnsCustomFallback_WhenKeyAbsent()
    {
        Assert.Equal("default", _store.Get("missing", "default"));
    }

    [Fact]
    public void Get_ReturnsStoredValue_AfterSet()
    {
        _store.Set("key", "value");
        Assert.Equal("value", _store.Get("key"));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        _store.Set("Key", "value");
        Assert.Equal("value", _store.Get("KEY"));
        Assert.Equal("value", _store.Get("key"));
    }

    // ── Set ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Set_ReturnsTrue_WhenKeyIsNew()
    {
        Assert.True(_store.Set("key", "value"));
    }

    [Fact]
    public void Set_ReturnsTrue_WhenValueChanges()
    {
        _store.Set("key", "old");
        Assert.True(_store.Set("key", "new"));
    }

    [Fact]
    public void Set_ReturnsFalse_WhenValueIsUnchanged()
    {
        _store.Set("key", "value");
        Assert.False(_store.Set("key", "value"));
    }

    [Fact]
    public void Set_FiresChangedEvent_WhenValueChanges()
    {
        string? capturedKey = null;
        string? capturedValue = null;
        _store.Changed += (k, v) => { capturedKey = k; capturedValue = v; };

        _store.Set("myKey", "myValue");

        Assert.Equal("myKey", capturedKey);
        Assert.Equal("myValue", capturedValue);
    }

    [Fact]
    public void Set_DoesNotFireChangedEvent_WhenValueIsUnchanged()
    {
        _store.Set("key", "value");
        var fired = false;
        _store.Changed += (_, _) => fired = true;

        _store.Set("key", "value");

        Assert.False(fired);
    }

    [Fact]
    public void Set_AppliesNormalizeFunction()
    {
        _store.Set("key", "  hello  ", v => v.Trim());
        Assert.Equal("hello", _store.Get("key"));
    }

    [Fact]
    public void Set_TreatsNullAsEmptyString()
    {
        _store.Set("key", null!);
        Assert.Equal("", _store.Get("key"));
    }

    // ── Snapshot ─────────────────────────────────────────────────────────────

    [Fact]
    public void Snapshot_ReturnsAllValues()
    {
        _store.Set("a", "1");
        _store.Set("b", "2");

        var snapshot = _store.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("1", snapshot["a"]);
        Assert.Equal("2", snapshot["b"]);
    }

    [Fact]
    public void Snapshot_IsCaseInsensitive()
    {
        _store.Set("Key", "value");
        var snapshot = _store.Snapshot();

        Assert.Equal("value", snapshot["key"]);
        Assert.Equal("value", snapshot["KEY"]);
    }

    [Fact]
    public void Snapshot_IsIndependentOfStore()
    {
        _store.Set("key", "original");
        var snapshot = _store.Snapshot();

        _store.Set("key", "modified");

        Assert.Equal("original", snapshot["key"]);
    }

    // ── ReplaceAll ────────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_RemovesExistingKeys()
    {
        _store.Set("old", "value");
        _store.ReplaceAll(new Dictionary<string, string> { ["new"] = "data" });

        Assert.Equal("", _store.Get("old"));
    }

    [Fact]
    public void ReplaceAll_SetsNewValues()
    {
        _store.ReplaceAll(new Dictionary<string, string> { ["new"] = "data" });

        Assert.Equal("data", _store.Get("new"));
    }

    [Fact]
    public void ReplaceAll_TreatsNullValuesAsEmptyString()
    {
        _store.ReplaceAll(new Dictionary<string, string> { ["key"] = null! });

        Assert.Equal("", _store.Get("key"));
    }

    // ── Values ────────────────────────────────────────────────────────────────

    [Fact]
    public void Values_ReflectsStoredEntries()
    {
        _store.Set("x", "1");

        Assert.True(_store.Values.ContainsKey("x"));
        Assert.Equal("1", _store.Values["x"]);
    }
}
