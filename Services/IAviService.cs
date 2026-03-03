namespace CleanScan.Services;

public interface IAviService
{
    bool IsAviFourCcKnownToFailWithAviSource(string path);
    bool TryGetAviVideoFourCc(string filePath, out string fourCc);
}
