using System.Collections.Generic;

namespace CleanScan.Services;

public interface IScriptService
{
    void Generate(Dictionary<string, string> configValues, string lang = "fr");
    string? GetPrimaryScriptPath();
    IEnumerable<string> GetScriptPaths();
    string? GetMasterScriptPath();
    void EnsureUserScriptAvailable();
    void EnsureScriptCopiesInOutputDir();
    void EnsureScriptUsesAppBaseDir(string scriptPath);
    Dictionary<string, string> LoadScriptValues();
}
