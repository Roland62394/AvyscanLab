using System.IO;
using System.Text.RegularExpressions;
using AvyScanLab.Services;
using Xunit;

namespace AvyScanLab.Tests.Services;

public partial class ScriptModuleAssemblyTests
{
    /// <summary>
    /// When all modules are active, the assembled script contains all config vars,
    /// all function definitions, and all pipeline steps.
    /// </summary>
    [Fact]
    public void AssembleModules_AllEnabled_ContainsEverything()
    {
        var repoRoot = FindRepoRoot();
        var templatePath = Path.Combine(repoRoot, "ScriptMaster.en.avs");
        if (!File.Exists(templatePath)) return;

        var template = File.ReadAllText(templatePath);
        var modules = ScriptModuleRegistry.GetBuiltInModules();

        // All filters enabled
        var config = new System.Collections.Generic.Dictionary<string, string>
        {
            ["enable_gammac"] = "true",
            ["enable_denoise"] = "true",
            ["enable_degrain"] = "true",
            ["enable_luma_levels"] = "true",
            ["enable_sharp"] = "true",
        };

        var assembled = ScriptService.AssembleModules(template, modules, config);

        // All config vars present
        Assert.Contains("enable_denoise", assembled);
        Assert.Contains("denoise_mode", assembled);
        Assert.Contains("degrain_thSAD", assembled);
        Assert.Contains("Lum_Bright", assembled);
        Assert.Contains("LockChan", assembled);
        Assert.Contains("Sharp_Mode", assembled);

        // All functions present
        Assert.Contains("DenoiseFilm", assembled);
        Assert.Contains("DegrainFilm", assembled);
        Assert.Contains("SharpenAdvanced", assembled);

        // All pipeline steps present
        Assert.Contains("DenoiseFilm(c,", assembled);
        Assert.Contains("DegrainFilm(c,", assembled);
        Assert.Contains("SharpenAdvanced(c,", assembled);

        // Core always present
        Assert.Contains("EnsureColorspaceSafe", assembled);
        Assert.Contains("src_path = src", assembled);
    }

    /// <summary>
    /// Verifies that custom filters are correctly converted to modules
    /// and inserted at the right pipeline position.
    /// </summary>
    [Fact]
    public void ConvertCustomFiltersToModules_MapsPositionCorrectly()
    {
        var filters = new[]
        {
            new AvyScanLab.Models.CustomFilter
            {
                Id = "test1", Name = "Test Filter", Enabled = true,
                Position = "AfterDenoise", Code = "c = c"
            },
            new AvyScanLab.Models.CustomFilter
            {
                Id = "test2", Name = "Disabled", Enabled = false,
                Position = "AfterGamMac", Code = "c = c"
            },
        };

        var modules = ScriptService.ConvertCustomFiltersToModules(filters);

        Assert.Single(modules); // only enabled filter
        Assert.Equal(250, modules[0].Position); // AfterDenoise = 250
        Assert.Equal("custom_test1", modules[0].Id);
        Assert.Contains("Test Filter", modules[0].PipelineCode);
    }

    /// <summary>
    /// Verifies that custom filter placeholders are resolved from config values.
    /// </summary>
    [Fact]
    public void ConvertCustomFiltersToModules_ResolvesPlaceholders()
    {
        var filters = new[]
        {
            new AvyScanLab.Models.CustomFilter
            {
                Id = "abc", Name = "Blur", Enabled = true,
                Position = "AfterSharpen", Code = "c = Blur(c, {radius})",
                Controls = [new() { Placeholder = "radius", Default = "1.0" }]
            },
        };

        var config = new System.Collections.Generic.Dictionary<string, string>
        {
            ["cf_abc_radius"] = "2.5"
        };

        var modules = ScriptService.ConvertCustomFiltersToModules(filters, config);
        Assert.Contains("Blur(c, 2.5)", modules[0].PipelineCode);
    }

    /// <summary>
    /// When all filters are disabled, neither function definitions nor pipeline steps
    /// should appear in the assembled script.
    /// </summary>
    [Fact]
    public void AssembleModules_ExcludesDisabledModules()
    {
        var repoRoot = FindRepoRoot();
        var templatePath = Path.Combine(repoRoot, "ScriptMaster.en.avs");
        if (!File.Exists(templatePath)) return;

        var template = File.ReadAllText(templatePath);
        var modules = ScriptModuleRegistry.GetBuiltInModules();

        // All filters disabled
        var config = new System.Collections.Generic.Dictionary<string, string>
        {
            ["enable_gammac"] = "false",
            ["enable_denoise"] = "false",
            ["enable_degrain"] = "false",
            ["enable_luma_levels"] = "false",
            ["enable_sharp"] = "false",
        };

        var assembled = ScriptService.AssembleModules(template, modules, config);

        // No filter functions should be present
        Assert.DoesNotContain("DenoiseFilm", assembled);
        Assert.DoesNotContain("DegrainFilm", assembled);
        Assert.DoesNotContain("SharpenAdvanced", assembled);

        // No pipeline step code should be present
        Assert.DoesNotContain("enable_gammac && FunctionExists", assembled);
        Assert.DoesNotContain("DenoiseFilm(c,", assembled);
        Assert.DoesNotContain("DegrainFilm(c,", assembled);
        Assert.DoesNotContain("Tweak(luma_yuv", assembled);
        Assert.DoesNotContain("SharpenAdvanced(c,", assembled);

        // Core helpers and source should still be present
        Assert.Contains("EnsureColorspaceSafe", assembled);
        Assert.Contains("_BuildPreview", assembled);
        Assert.Contains("src_path = src", assembled);
    }

    /// <summary>
    /// When only Denoise is enabled, only DenoiseFilm function and STEP 2 should appear.
    /// </summary>
    [Fact]
    public void AssembleModules_IncludesOnlyEnabledModules()
    {
        var repoRoot = FindRepoRoot();
        var templatePath = Path.Combine(repoRoot, "ScriptMaster.en.avs");
        if (!File.Exists(templatePath)) return;

        var template = File.ReadAllText(templatePath);
        var modules = ScriptModuleRegistry.GetBuiltInModules();

        var config = new System.Collections.Generic.Dictionary<string, string>
        {
            ["enable_gammac"] = "false",
            ["enable_denoise"] = "true",
            ["enable_degrain"] = "false",
            ["enable_luma_levels"] = "false",
            ["enable_sharp"] = "false",
        };

        var assembled = ScriptService.AssembleModules(template, modules, config);

        // Only DenoiseFilm should be present
        Assert.Contains("DenoiseFilm", assembled);
        Assert.DoesNotContain("DegrainFilm", assembled);
        Assert.DoesNotContain("SharpenAdvanced", assembled);
        Assert.DoesNotContain("enable_gammac && FunctionExists", assembled);

        // Only Denoise call in pipeline
        Assert.Contains("DenoiseFilm(c,", assembled);
        Assert.DoesNotContain("enable_gammac && FunctionExists", assembled);
        Assert.DoesNotContain("DegrainFilm(c,", assembled);
        Assert.DoesNotContain("SharpenAdvanced(c,", assembled);
    }

    private static string NormalizeLineEndings(string s) =>
        s.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>Strips __CUSTOM_INJECT_xxx__ and __CUSTOM_PLUGINS__ markers and surrounding blank lines.</summary>
    private static string StripMarkers(string s)
    {
        // Remove marker lines
        s = MarkerLineRegex().Replace(s, "");
        // Collapse runs of 3+ blank lines to 2
        s = ExcessiveBlanksRegex().Replace(s, "\n\n");
        return s;
    }

    [GeneratedRegex(@"^# __CUSTOM_(?:INJECT_\w+|PLUGINS)__[ \t]*\n?", RegexOptions.Multiline)]
    private static partial Regex MarkerLineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveBlanksRegex();

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
