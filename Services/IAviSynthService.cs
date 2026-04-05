namespace AvyScanLab.Services;

public interface IAviSynthService
{
    bool IsAviSynthInstalled();
    bool IsAviSynthInstalledForVirtualDub(string? virtualDubPath);
}
