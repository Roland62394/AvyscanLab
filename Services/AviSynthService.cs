using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AvyScanLab.Services;

public sealed class AviSynthService : IAviSynthService
{
    public bool IsAviSynthInstalled()
    {
        if (!OperatingSystem.IsWindows()) return true;
        return IsAviSynthPlusInstalledFromRegistry()
            || IsAviSynthPlusDllPresent()
            || IsAviSynthPlusX64();
    }

    public bool IsAviSynthInstalledForVirtualDub(string? _) =>
        !OperatingSystem.IsWindows() || IsAviSynthInstalled();

    [SupportedOSPlatform("windows")]
    private static bool IsAviSynthPlusInstalledFromRegistry()
    {
        foreach (var keyPath in (string[])[@"SOFTWARE\AviSynth+", @"SOFTWARE\AviSynth"])
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            var ver = key?.GetValue("Version")?.ToString();
            if (!string.IsNullOrWhiteSpace(ver) && ver.StartsWith("3", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAviSynthPlusDllPresent()
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return !string.IsNullOrWhiteSpace(sys) && File.Exists(Path.Combine(sys, "avisynth.dll"));
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAviSynthPlusX64()
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(sys)) return false;
        var dll = Path.Combine(sys, "avisynth.dll");
        return File.Exists(dll) && FileVersionInfo.GetVersionInfo(dll).FileMajorPart >= 3;
    }
}
