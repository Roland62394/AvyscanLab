using System.Collections.Generic;

namespace AvyscanLab.Services;

public interface IPresetService
{
    List<Preset> LoadPresets();
    void SavePresets(List<Preset> presets);
    Dictionary<string, string> CaptureCurrentValues(ConfigStore config);
}
