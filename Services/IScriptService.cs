using System.Collections.Generic;
using CleanScan.Models;

namespace CleanScan.Services;

public interface IScriptService
{
    void Generate(Dictionary<string, string> configValues, string lang = "en");
    void Generate(Dictionary<string, string> configValues, IReadOnlyList<CustomFilter>? customFilters, string lang = "en");
    string? GetPrimaryScriptPath();
    IEnumerable<string> GetScriptPaths();
    string? GetMasterScriptPath();
    void EnsureUserScriptAvailable();
    void EnsureScriptCopiesInOutputDir();
    void EnsureScriptUsesAppBaseDir(string scriptPath);
    Dictionary<string, string> LoadScriptValues();
}
