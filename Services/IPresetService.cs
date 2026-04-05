using System.Collections.Generic;

namespace AvyScanLab.Services;

public interface IPresetService
{
    List<Preset> LoadPresets();
    void SavePresets(List<Preset> presets);
    Dictionary<string, string> CaptureCurrentValues(ConfigStore config);
}
