namespace AvyScanLab.Services;

/// <summary>
/// Slider and text-field specifications, extracted from MainWindow to reduce file size.
/// </summary>
public static class UiFieldDefinitions
{
    public readonly record struct SliderSpec(
        string Field, double Min, double Max, double SmallChange, bool IsFloat, int Decimals = 0);

    public static readonly SliderSpec[] SliderSpecs =
    [
        new("Crop_L",            0,    500,  1,    false),
        new("Crop_T",            0,    500,  1,    false),
        new("Crop_R",            0,    500,  1,    false),
        new("Crop_B",            0,    500,  1,    false),
        new("degrain_thSAD",     0,    1000, 10,   false),
        new("degrain_thSADC",    0,    1000, 10,   false),
        new("degrain_blksize",   4,    64,   4,    false),
        new("degrain_overlap",   0,    32,   2,    false),
        new("degrain_pel",       1,    4,    1,    false),
        new("degrain_search",    0,    5,    1,    false),
        new("denoise_strength",  1,    24,   1,    false),
        new("denoise_dist",      1,    20,   1,    false),
        new("Lum_Bright",       -255,  255,  1,    false),
        new("Lum_Contrast",      0.1,  3.0,  0.05, true, 2),
        new("Lum_Sat",           0.1,  3.0,  0.05, true, 2),
        new("Lum_Hue",          -180,  180,  1,    false),
        new("Lum_GammaY",        0.1,  3.0,  0.05, true, 2),
        new("LockChan",         -3,    2,    1,    false),
        new("LockVal",           1,    255,  1,    false),
        new("Scale",             0,    2,    1,    false),
        new("Th",                0,    1,    0.01, true, 3),
        new("HiTh",              0,    1,    0.01, true, 3),
        new("X",                 0,    2000, 10,   false),
        new("Y",                 0,    2000, 10,   false),
        new("W",                 0,    4000, 10,   false),
        new("H",                 0,    4000, 10,   false),
        new("Omin",              0,    255,  1,    false),
        new("Omax",              0,    255,  1,    false),
        new("Verbosity",         0,    6,    1,    false),
        new("Sharp_Strength",    1,    20,   1,    false),
        new("Sharp_Radius",      0.5,  5.0,  0.1,  true, 1),
        new("Sharp_Threshold",   0,    100,  1,    false),
    ];

    public enum UpdateMode { Debounced, OnLostFocus, OnEnter, Immediate }

    public sealed record FieldSpec(string Name, UpdateMode Mode, bool ValidateOnChange);

    public static readonly FieldSpec[] FieldSpecs =
    [
        new("threads",          UpdateMode.OnLostFocus, true),
        new("film",             UpdateMode.Debounced,  true),
        new("img",              UpdateMode.Debounced,  true),
        new("img_start",        UpdateMode.Debounced,  true),
        new("img_end",          UpdateMode.Debounced,  true),
        new("play_speed",       UpdateMode.Debounced,  true),
        new("Crop_L",           UpdateMode.Debounced,  true),
        new("Crop_T",           UpdateMode.Debounced,  true),
        new("Crop_R",           UpdateMode.Debounced,  true),
        new("Crop_B",           UpdateMode.Debounced,  true),
        new("degrain_mode",      UpdateMode.Debounced,  true),
        new("degrain_thSAD",     UpdateMode.Debounced,  true),
        new("degrain_thSADC",    UpdateMode.Debounced,  true),
        new("degrain_blksize",   UpdateMode.Debounced,  true),
        new("degrain_overlap",   UpdateMode.Debounced,  true),
        new("degrain_pel",       UpdateMode.Debounced,  true),
        new("degrain_search",    UpdateMode.Debounced,  true),
        new("degrain_prefilter", UpdateMode.Debounced,  true),
        new("denoise_mode",     UpdateMode.Debounced,  true),
        new("denoise_strength", UpdateMode.Debounced,  true),
        new("denoise_dist",     UpdateMode.Debounced,  true),
        new("Lum_Bright",       UpdateMode.Debounced,  true),
        new("Lum_Contrast",     UpdateMode.Debounced,  true),
        new("Lum_Sat",          UpdateMode.Debounced,  true),
        new("Lum_Hue",          UpdateMode.Debounced,  true),
        new("Lum_GammaY",       UpdateMode.Debounced,  true),
        new("LockChan",         UpdateMode.Debounced,  true),
        new("LockVal",          UpdateMode.Debounced,  true),
        new("Scale",            UpdateMode.Debounced,  true),
        new("Th",               UpdateMode.Debounced,  true),
        new("HiTh",             UpdateMode.Debounced,  true),
        new("X",                UpdateMode.Debounced,  true),
        new("Y",                UpdateMode.Debounced,  true),
        new("W",                UpdateMode.Debounced,  true),
        new("H",                UpdateMode.Debounced,  true),
        new("Omin",             UpdateMode.Debounced,  true),
        new("Omax",             UpdateMode.Debounced,  true),
        new("Verbosity",        UpdateMode.Debounced,  true),
        new("Sharp_Mode",       UpdateMode.Debounced,  true),
        new("Sharp_Strength",   UpdateMode.Debounced,  true),
        new("Sharp_Radius",     UpdateMode.Debounced,  true),
        new("Sharp_Threshold",  UpdateMode.Debounced,  true),
    ];
}
