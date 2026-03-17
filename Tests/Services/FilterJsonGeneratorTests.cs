using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CleanScan.Models;
using Xunit;

namespace CleanScan.Tests.Services;

/// <summary>
/// Generates the 5 built-in filter JSON files in the Filters/ directory.
/// Run this test once to create/update the files. Not a real test — it's a generator.
/// </summary>
public class FilterJsonGeneratorTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    [Fact]
    public void GenerateAllFilterJsonFiles()
    {
        var repoRoot = FindRepoRoot();
        var dir = Path.Combine(repoRoot, "Filters");
        Directory.CreateDirectory(dir);

        WriteFilter(dir, "gammac.json", BuildGamMac());
        WriteFilter(dir, "denoise.json", BuildDenoise());
        WriteFilter(dir, "degrain.json", BuildDegrain());
        WriteFilter(dir, "luma.json", BuildLuma());
        WriteFilter(dir, "sharpen.json", BuildSharpen());
        WriteFilter(dir, "flip_h.json", BuildFlipH());
        WriteFilter(dir, "flip_v.json", BuildFlipV());
        WriteFilter(dir, "crop.json", BuildCrop());
    }

    private static void WriteFilter(string dir, string fileName, CustomFilter filter)
    {
        var json = JsonSerializer.Serialize(filter, JsonOpts);
        File.WriteAllText(Path.Combine(dir, fileName), json);
    }

    // ── GamMac ───────────────────────────────────────────────────────────

    private static CustomFilter BuildGamMac() => new()
    {
        Id = "gammac",
        Name = "GamMac",
        Enabled = false,
        Position = "GamMac",
        Dlls = [],
        Code = """
            if (FunctionExists("GamMac")) {
              try {
                base_pix = PixelType(c)
                mx  = _MatrixAuto(c)
                rgb = EnsureColorspaceSafe(c, "RGB24", matrix=mx)
                rgb = GamMac(rgb,
                  \ LockChan={LockChan}, LockVal={LockVal},
                  \ x={X}, y={Y}, w={W}, h={H},
                  \ Scale={Scale}, Th={Th}, HiTh={HiTh},
                  \ Omin={Omin}, Omax={Omax}, Show={Show}, Verbosity={Verbosity})
                c = EnsureColorspaceSafe(rgb, base_pix, matrix=mx)
              } catch(err) { pipeline_err = "GamMac: " + err }
            }
            """,
        Controls =
        [
            new() { Placeholder = "LockChan", Type = "slider", Default = "1", Min = -3, Max = 2, Step = 1 },
            new() { Placeholder = "LockVal", Type = "slider", Default = "250", Min = 1, Max = 255, Step = 1 },
            new() { Placeholder = "Scale", Type = "slider", Default = "2", Min = 0, Max = 2, Step = 1 },
            new() { Placeholder = "Th", Type = "slider", Default = "0.12", Min = 0, Max = 1, Step = 0.01 },
            new() { Placeholder = "HiTh", Type = "slider", Default = "0.25", Min = 0, Max = 1, Step = 0.01 },
            new() { Placeholder = "X", Type = "slider", Default = "0", Min = 0, Max = 10000, Step = 1 },
            new() { Placeholder = "Y", Type = "slider", Default = "0", Min = 0, Max = 10000, Step = 1 },
            new() { Placeholder = "W", Type = "slider", Default = "0", Min = 0, Max = 10000, Step = 1 },
            new() { Placeholder = "H", Type = "slider", Default = "0", Min = 0, Max = 10000, Step = 1 },
            new() { Placeholder = "Omin", Type = "slider", Default = "0", Min = 0, Max = 255, Step = 1 },
            new() { Placeholder = "Omax", Type = "slider", Default = "255", Min = 0, Max = 255, Step = 1 },
            new() { Placeholder = "Show", Type = "checkbox", Default = "false", OnValue = "true", OffValue = "false" },
            new() { Placeholder = "Verbosity", Type = "slider", Default = "4", Min = 0, Max = 6, Step = 1 },
        ],
    };

    // ── Denoise ──────────────────────────────────────────────────────────

    private static CustomFilter BuildDenoise() => new()
    {
        Id = "denoise",
        Name = "Denoise (RemoveDirt/MC)",
        Enabled = false,
        Position = "Denoise",
        Dlls = [],
        Code = """
            function DenoiseFilm(clip c, string "mode", float "strength", int "dist", bool "grey", string "matrix")
            {
              mode   = Default(mode,     "removedirtmc")
              lim    = int(Default(strength, 10.0))
              dist   = Default(dist,     3)
              grey   = Default(grey,     false)
              matrix = Default(matrix,   _MatrixAuto(c))

              base_pix = PixelType(c)
              bd = BitsPerComponent(c)

              c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
              c8  = MakeEvenY(c8)
              yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

              if (mode == "removedirtmc" && FunctionExists("RemoveDirtMC")) {
                result = RemoveDirtMC(yuv, limit=lim, _grey=grey, dist=dist)
              } else if (mode == "removedirt" && FunctionExists("RemoveDirt")) {
                result = RemoveDirt(yuv, limit=lim, grey=grey, dist=dist)
              } else {
                result = TemporalSoften(yuv, 1, 4, 4, 12, 2)
              }

              result = (bd == 8) ? result : ConvertBits(result, bd)
              return (PixelType(result) == base_pix) ? result
                   \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
            }

            c = DenoiseFilm(c, mode="{mode}", strength={strength}, dist={dist}, grey={grey})
            """,
        Controls =
        [
            new() { Placeholder = "mode", Type = "combo", Default = "removedirtmc", Options = ["removedirtmc", "removedirt"] },
            new() { Placeholder = "strength", Type = "slider", Default = "10", Min = 1, Max = 24, Step = 1 },
            new() { Placeholder = "dist", Type = "slider", Default = "3", Min = 1, Max = 10, Step = 1 },
            new() { Placeholder = "grey", Type = "checkbox", Default = "false", OnValue = "true", OffValue = "false" },
        ],
    };

    // ── Degrain ──────────────────────────────────────────────────────────

    private static CustomFilter BuildDegrain() => new()
    {
        Id = "degrain",
        Name = "Degrain (MVTools2)",
        Enabled = false,
        Position = "Degrain",
        Dlls = [],
        Code = """
            function DegrainFilm(clip c, string "mode", int "thSAD", int "thSADC",
            \                    int "blksize", int "overlap", int "pel", int "search",
            \                    string "prefilter", string "matrix")
            {
              mode      = Default(mode,      "mdegrain2")
              thSAD     = Default(thSAD,     350)
              thSADC    = Default(thSADC,    250)
              blksize   = Default(blksize,   16)
              overlap   = Default(overlap,   8)
              pel       = Default(pel,       1)
              search    = Default(search,    2)
              prefilter = Default(prefilter, "remgrain")
              matrix    = Default(matrix,    _MatrixAuto(c))

              base_pix = PixelType(c)
              bd = BitsPerComponent(c)

              c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
              c8  = MakeEvenY(c8)
              yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

              if (!FunctionExists("MSuper")) {
                result = TemporalSoften(yuv, 2, 6, 5, 15, 2)
                result = (bd == 8) ? result : ConvertBits(result, bd)
                return (PixelType(result) == base_pix) ? result
                     \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
              }

              prefilt = (prefilter == "remgrain") ? RemoveGrain(yuv, 2)
                      \ : (prefilter == "blur")   ? Blur(yuv, 1.0)
                      \                           : yuv

              sup_orig = MSuper(yuv,    pel=pel, sharp=2)
              sup_pre  = MSuper(prefilt, pel=pel, sharp=2)

              bv1 = MAnalyse(sup_pre, isb=true,  delta=1, blksize=blksize, overlap=overlap, search=search)
              fv1 = MAnalyse(sup_pre, isb=false, delta=1, blksize=blksize, overlap=overlap, search=search)

              if (mode == "mdegrain1") {
                result = MDegrain1(yuv, sup_orig, bv1, fv1, thSAD=thSAD, thSADC=thSADC)
              } else if (mode == "mdegrain2") {
                bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
                fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
                result = MDegrain2(yuv, sup_orig, bv1, fv1, bv2, fv2, thSAD=thSAD, thSADC=thSADC)
              } else {
                bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
                fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
                bv3 = MAnalyse(sup_pre, isb=true,  delta=3, blksize=blksize, overlap=overlap, search=search)
                fv3 = MAnalyse(sup_pre, isb=false, delta=3, blksize=blksize, overlap=overlap, search=search)
                result = MDegrain3(yuv, sup_orig, bv1, fv1, bv2, fv2, bv3, fv3, thSAD=thSAD, thSADC=thSADC)
              }

              result = (bd == 8) ? result : ConvertBits(result, bd)
              return (PixelType(result) == base_pix) ? result
                   \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
            }

            c = DegrainFilm(c, mode="{mode}", thSAD={thSAD}, thSADC={thSADC}, blksize={blksize}, overlap={overlap}, pel={pel}, search={search}, prefilter="{prefilter}")
            """,
        Controls =
        [
            new() { Placeholder = "mode", Type = "combo", Default = "mdegrain2", Options = ["mdegrain1", "mdegrain2", "mdegrain3"] },
            new() { Placeholder = "thSAD", Type = "slider", Default = "350", Min = 50, Max = 1000, Step = 10 },
            new() { Placeholder = "thSADC", Type = "slider", Default = "250", Min = 50, Max = 800, Step = 10 },
            new() { Placeholder = "blksize", Type = "combo", Default = "16", Options = ["8", "16", "32"] },
            new() { Placeholder = "overlap", Type = "combo", Default = "8", Options = ["4", "8", "16"] },
            new() { Placeholder = "pel", Type = "combo", Default = "1", Options = ["1", "2"] },
            new() { Placeholder = "search", Type = "combo", Default = "2", Options = ["0", "2", "3"] },
            new() { Placeholder = "prefilter", Type = "combo", Default = "remgrain", Options = ["remgrain", "blur", "none"] },
        ],
    };

    // ── Luma ─────────────────────────────────────────────────────────────

    private static CustomFilter BuildLuma() => new()
    {
        Id = "luma",
        Name = "Luma / Levels",
        Enabled = false,
        Position = "Luma",
        Dlls = [],
        Code = """
            base_pix = PixelType(c)
            mx       = _MatrixAuto(c)
            luma_yuv = EnsureColorspaceSafe(c, "YV24", matrix=mx)

            if ({bright} != 0.0 || {contrast} != 1.0 || {sat} != 1.0 || {hue} != 0.0) {
              luma_yuv = Tweak(luma_yuv, sat={sat}, hue={hue}, bright={bright}, cont={contrast})
            }
            if ({gamma} != 1.0) {
              luma_yuv = ColorYUV(luma_yuv, gamma_y={gamma})
            }

            c = EnsureColorspaceSafe(luma_yuv, base_pix, matrix=mx)
            """,
        Controls =
        [
            new() { Placeholder = "bright", Type = "slider", Default = "0.0", Min = -100, Max = 100, Step = 0.5 },
            new() { Placeholder = "contrast", Type = "slider", Default = "1.0", Min = 0.5, Max = 2.0, Step = 0.05 },
            new() { Placeholder = "sat", Type = "slider", Default = "1.0", Min = 0.0, Max = 3.0, Step = 0.05 },
            new() { Placeholder = "hue", Type = "slider", Default = "0.0", Min = -180, Max = 180, Step = 1 },
            new() { Placeholder = "gamma", Type = "slider", Default = "1.0", Min = 0.5, Max = 3.0, Step = 0.05 },
        ],
    };

    // ── Sharpen ──────────────────────────────────────────────────────────

    private static CustomFilter BuildSharpen() => new()
    {
        Id = "sharpen",
        Name = "Sharpen",
        Enabled = false,
        Position = "Sharpen",
        Dlls = [],
        Code = """
            function SharpenAdvanced(clip c, string "mode", float "strength", float "radius", int "threshold", string "matrix")
            {
              mode      = Default(mode, "simple")
              strength  = Default(strength, 8)
              radius    = Default(radius, 1.5)
              threshold = Default(threshold, 5)
              matrix    = Default(matrix, _MatrixAuto(c))

              base_pix = PixelType(c)
              blur_r = Min((radius - 1.0) * 0.45 + 0.15, 1.5)

              if (mode == "edge") {
                yuv = EnsureColorspaceSafe(c, "YV24", matrix=matrix)
                blurred = yuv.Blur(blur_r)
                sharp = mt_lutxy(yuv, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)
                edge_h    = yuv.mt_edge(thY1=0, thY2=255, "8 16 8 0 0 0 -8 -16 -8 4")
                edge_v    = yuv.mt_edge(thY1=0, thY2=255, "8 0 -8 16 0 -16 8 0 -8 4")
                edge_mask = mt_logic(edge_h, edge_v, "max") \
                          .mt_lut("x 128 / 0.86 ^ 255 *", U=1, V=1)
                edge_mask = edge_mask.mt_inflate().mt_inflate().Blur(1.0)
                edge_mask = (threshold > 0)
                          \ ? edge_mask.mt_lut("x " + String(threshold) + " < 0 x ?", U=1, V=1)
                          \ : edge_mask
                result = mt_merge(yuv, sharp, edge_mask, U=1, V=1)
                return EnsureColorspaceSafe(result, base_pix, matrix=matrix)
              }

              yuv_s   = EnsureColorspaceSafe(c, "YV24", matrix=matrix)
              blurred = yuv_s.Blur(blur_r)
              sharp_s = mt_lutxy(yuv_s, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)
              return EnsureColorspaceSafe(sharp_s, base_pix, matrix=matrix)
            }

            c = SharpenAdvanced(c, mode="{mode}", strength={strength}, radius={radius}, threshold={threshold})
            """,
        Controls =
        [
            new() { Placeholder = "mode", Type = "combo", Default = "simple", Options = ["simple", "edge"] },
            new() { Placeholder = "strength", Type = "slider", Default = "8", Min = 1, Max = 20, Step = 1 },
            new() { Placeholder = "radius", Type = "slider", Default = "1.5", Min = 0.5, Max = 3.0, Step = 0.1 },
            new() { Placeholder = "threshold", Type = "slider", Default = "5", Min = 0, Max = 255, Step = 1 },
        ],
    };

    // ── Flip Horizontal ─────────────────────────────────────────────────

    private static CustomFilter BuildFlipH() => new()
    {
        Id = "flip_h",
        Name = "Flip Horizontal",
        Enabled = false,
        Position = "FlipH",
        Dlls = [],
        Code = "c = c.FlipHorizontal()",
        Controls = [],
    };

    // ── Flip Vertical ───────────────────────────────────────────────────

    private static CustomFilter BuildFlipV() => new()
    {
        Id = "flip_v",
        Name = "Flip Vertical",
        Enabled = false,
        Position = "FlipV",
        Dlls = [],
        Code = "c = c.FlipVertical()",
        Controls = [],
    };

    // ── Crop ────────────────────────────────────────────────────────────

    private static CustomFilter BuildCrop() => new()
    {
        Id = "crop",
        Name = "Crop",
        Enabled = false,
        Position = "Crop",
        Dlls = [],
        Code = "c = CropSafe(c, {left}, {top}, {right}, {bottom})",
        Controls =
        [
            new() { Placeholder = "left", Type = "slider", Default = "0", Min = 0, Max = 4000, Step = 2 },
            new() { Placeholder = "top", Type = "slider", Default = "0", Min = 0, Max = 4000, Step = 2 },
            new() { Placeholder = "right", Type = "slider", Default = "0", Min = 0, Max = 4000, Step = 2 },
            new() { Placeholder = "bottom", Type = "slider", Default = "0", Min = 0, Max = 4000, Step = 2 },
        ],
    };

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
