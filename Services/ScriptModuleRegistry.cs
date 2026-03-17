using System.Collections.Generic;
using CleanScan.Models;

namespace CleanScan.Services;

/// <summary>
/// Registry of built-in AviSynth filter modules.
/// Each module contributes function definitions and pipeline code
/// that the assembler merges into the final script.
/// </summary>
public static class ScriptModuleRegistry
{
    public static IReadOnlyList<ScriptModule> GetBuiltInModules() =>
    [
        GamMac(),
        Denoise(),
        Degrain(),
        LumaLevels(),
        Sharpen(),
    ];

    // ── GamMac ───────────────────────────────────────────────────────────────

    private static ScriptModule GamMac() => new()
    {
        Id = "gammac",
        Name = "GamMac",
        Position = 100,
        EnableKey = "enable_gammac",
        TemporalRadius = 0,
        InjectionPointAfter = "AfterGamMac",
        Functions = "",
        PipelineCode =
            """
            # STEP 1 — GamMac FIRST (selective RGB correction — neutralises colour cast before RemoveDirt)
            if (enable_gammac && FunctionExists("GamMac")) {
              try {
                base_pix = PixelType(c)
                mx  = _MatrixAuto(c)
                rgb = EnsureColorspaceSafe(c, "RGB24", matrix=mx)
                rgb = GamMac(rgb,
                  \ LockChan=LockChan, LockVal=LockVal,
                  \ x=X, y=Y, w=W, h=H,
                  \ Scale=Scale, Th=Th, HiTh=HiTh,
                  \ Omin=Omin, Omax=Omax, Show=Show, Verbosity=Verbosity)
                c = EnsureColorspaceSafe(rgb, base_pix, matrix=mx)
              } catch(err) { pipeline_err = "GamMac: " + err }
            }
            """,
    };

    // ── Denoise ──────────────────────────────────────────────────────────────

    private static ScriptModule Denoise() => new()
    {
        Id = "denoise",
        Name = "Denoise (RemoveDirt/MC)",
        Position = 200,
        EnableKey = "enable_denoise",
        TemporalRadius = 1,
        InjectionPointAfter = "AfterDenoise",
        Functions =
            """


            # =========================
            # FILTER 1 — DENOISE / REMOVEDIRT
            # Role  : Remove dust, stains, film scratches
            # Place : FIRST — on the raw source, before any processing
            # FIX v2 : limit corrected (6-14 depending on dirt), no longer 24 which caused artefacts
            # =========================

            function DenoiseFilm(clip c, string "mode", float "strength", int "dist", bool "grey", string "matrix")
            {
              mode   = Default(mode,     "removedirtmc")
              lim    = int(Default(strength, 10.0))  # accepts int or float, converted to int for RemoveDirt
              dist   = Default(dist,     3)
              grey   = Default(grey,     false)
              matrix = Default(matrix,   _MatrixAuto(c))

              base_pix = PixelType(c)
              bd = BitsPerComponent(c)

              # RemoveDirt/MC operates in YV12 (required)
              c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
              c8  = MakeEvenY(c8)
              yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

              if (mode == "removedirtmc" && FunctionExists("RemoveDirtMC")) {
                result = RemoveDirtMC(yuv, limit=lim, _grey=grey, dist=dist)

              } else if (mode == "removedirt" && FunctionExists("RemoveDirt")) {
                result = RemoveDirt(yuv, limit=lim, grey=grey, dist=dist)

              } else {
                # Fallback: targeted TemporalSoften (if RemoveDirt unavailable)
                result = TemporalSoften(yuv, 1, 4, 4, 12, 2)
              }

              # Return to original format
              result = (bd == 8) ? result : ConvertBits(result, bd)
              return (PixelType(result) == base_pix) ? result
                   \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
            }
            """,
        PipelineCode =
            """
            # STEP 2 — Dust / film stains (RemoveDirt)
            if (enable_denoise) {
              c = DenoiseFilm(c,
                \ mode     = denoise_mode,
                \ strength = denoise_strength,
                \ dist     = denoise_dist,
                \ grey     = denoise_grey,
                \ matrix   = src_matrix)
            }
            """,
    };

    // ── Degrain ──────────────────────────────────────────────────────────────

    private static ScriptModule Degrain() => new()
    {
        Id = "degrain",
        Name = "Degrain (MVTools2)",
        Position = 300,
        EnableKey = "enable_degrain",
        TemporalRadius = 3,
        InjectionPointAfter = "AfterDegrain",
        Functions =
            """


            # =========================
            # FILTER 2 — DEGRAIN (MVTools2 MDegrain)
            # Role  : Reduce film grain and general noise
            # Place : AFTER RemoveDirt, BEFORE colour corrections
            # FIX v2 : MDegrain2/3 replaces TemporalSoften (much more effective)
            # =========================

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

              # --- Fallback if MVTools2 absent ---
              if (!FunctionExists("MSuper")) {
                # Conservative TemporalSoften as fallback
                result = TemporalSoften(yuv, 2, 6, 5, 15, 2)
                result = (bd == 8) ? result : ConvertBits(result, bd)
                return (PixelType(result) == base_pix) ? result
                     \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
              }

              # --- Prefilter for motion vector analysis ---
              # Prefiltering improves MAnalyse precision WITHOUT affecting the final clip
              prefilt = (prefilter == "remgrain") ? RemoveGrain(yuv, 2)
                      \ : (prefilter == "blur")   ? Blur(yuv, 1.0)
                      \                           : yuv

              # --- MVTools2 analysis ---
              sup_orig = MSuper(yuv,    pel=pel, sharp=2)         # super for compensation
              sup_pre  = MSuper(prefilt, pel=pel, sharp=2)           # super prefiltered for analysis (levels auto)

              # Forward/backward vectors (radius 1)
              bv1 = MAnalyse(sup_pre, isb=true,  delta=1, blksize=blksize, overlap=overlap, search=search)
              fv1 = MAnalyse(sup_pre, isb=false, delta=1, blksize=blksize, overlap=overlap, search=search)

              if (mode == "mdegrain1") {
                result = MDegrain1(yuv, sup_orig, bv1, fv1, thSAD=thSAD, thSADC=thSADC)

              } else if (mode == "mdegrain2") {
                # MDegrain2: 2 temporal references on each side (recommended)
                bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
                fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
                result = MDegrain2(yuv, sup_orig, bv1, fv1, bv2, fv2, thSAD=thSAD, thSADC=thSADC)

              } else {
                # MDegrain3: 3 temporal references (best quality, slower)
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
            """,
        PipelineCode =
            """
            # STEP 3 — Film grain (MVTools2 MDegrain)
            if (enable_degrain) {
              c = DegrainFilm(c,
                \ mode      = degrain_mode,
                \ thSAD     = degrain_thSAD,
                \ thSADC    = degrain_thSADC,
                \ blksize   = degrain_blksize,
                \ overlap   = degrain_overlap,
                \ pel       = degrain_pel,
                \ search    = degrain_search,
                \ prefilter = degrain_prefilter,
                \ matrix    = src_matrix)
            }
            """,
    };

    // ── Luma Levels ──────────────────────────────────────────────────────────

    private static ScriptModule LumaLevels() => new()
    {
        Id = "luma",
        Name = "Luma / Levels",
        Position = 400,
        EnableKey = "enable_luma_levels",
        TemporalRadius = 0,
        InjectionPointAfter = "AfterLuma",
        Functions = "",
        PipelineCode =
            """
            # STEP 4 — Manual adjustments (Tweak + ColorYUV)
            if (enable_luma_levels) {
              base_pix = PixelType(c)
              mx       = _MatrixAuto(c)
              luma_yuv = EnsureColorspaceSafe(c, "YV24", matrix=mx)

              if (Lum_Bright != 0.0 || Lum_Contrast != 1.0 || Lum_Sat != 1.0 || Lum_Hue != 0.0) {
                luma_yuv = Tweak(luma_yuv, sat=Lum_Sat, hue=Lum_Hue, bright=Lum_Bright, cont=Lum_Contrast)
              }
              if (Lum_GammaY != 1.0) {
                luma_yuv = ColorYUV(luma_yuv, gamma_y=Lum_GammaY)
              }

              c = EnsureColorspaceSafe(luma_yuv, base_pix, matrix=mx)
            }
            """,
    };

    // ── Sharpen ──────────────────────────────────────────────────────────────

    private static ScriptModule Sharpen() => new()
    {
        Id = "sharpen",
        Name = "Sharpen",
        Position = 500,
        EnableKey = "enable_sharp",
        TemporalRadius = 0,
        InjectionPointAfter = "AfterSharpen",
        Functions =
            """


            # =========================
            # FILTER 3 — SHARPEN (unchanged, always last)
            # =========================

            function SharpenAdvanced(clip c, string "mode", float "strength", float "radius", int "threshold", string "matrix")
            {
              mode      = Default(mode, "unsharp")
              strength  = Default(strength, 8)
              radius    = Default(radius, 1.5)
              threshold = Default(threshold, 5)
              matrix    = Default(matrix, _MatrixAuto(c))

              base_pix = PixelType(c)

              # Blur radius for unsharp mask: radius controls the extent of the sharpening halo.
              # radius=1.0 → tight, precise sharpening ; radius=3.0 → broad, pronounced sharpening.
              blur_r = Min((radius - 1.0) * 0.45 + 0.15, 1.5)

              if (mode == "edge") {
                yuv = EnsureColorspaceSafe(c, "YV24", matrix=matrix)

                # Sharpening via unsharp mask with variable radius
                blurred = yuv.Blur(blur_r)
                sharp = mt_lutxy(yuv, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)

                # Dual-Sobel edge mask (horizontal + vertical) with gamma compression.
                # The ^0.86 curve (from LimitedSharpenFaster/Didée) naturally crushes low-response
                # areas (grain, fine texture) while preserving real object contours.
                edge_h    = yuv.mt_edge(thY1=0, thY2=255, "8 16 8 0 0 0 -8 -16 -8 4")
                edge_v    = yuv.mt_edge(thY1=0, thY2=255, "8 0 -8 16 0 -16 8 0 -8 4")
                edge_mask = mt_logic(edge_h, edge_v, "max") \
                          .mt_lut("x 128 / 0.86 ^ 255 *", U=1, V=1)

                # Inflate mask to cover full edge width, then soften for gradual transitions
                edge_mask = edge_mask.mt_inflate().mt_inflate().Blur(1.0)

                # Optional threshold: exclude residual weak gamma responses. Recommended: 20–40.
                # Set threshold=0 to disable (gamma compression alone is usually sufficient).
                edge_mask = (threshold > 0)
                          \ ? edge_mask.mt_lut("x " + String(threshold) + " < 0 x ?", U=1, V=1)
                          \ : edge_mask

                # Merge: original on flat areas, sharpened on edges — chroma unchanged
                result = mt_merge(yuv, sharp, edge_mask, U=1, V=1)
                return EnsureColorspaceSafe(result, base_pix, matrix=matrix)
              }

              # "simple": global unsharp mask with variable radius — effective but may slightly amplify grain
              yuv_s   = EnsureColorspaceSafe(c, "YV24", matrix=matrix)
              blurred = yuv_s.Blur(blur_r)
              sharp_s = mt_lutxy(yuv_s, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)
              return EnsureColorspaceSafe(sharp_s, base_pix, matrix=matrix)
            }
            """,
        PipelineCode =
            """
            # STEP 5 — Sharpen (ALWAYS LAST)
            if (enable_sharp) {
              c = SharpenAdvanced(c,
                \ mode      = Sharp_Mode,
                \ strength  = Sharp_Strength,
                \ radius    = Sharp_Radius,
                \ threshold = Sharp_Threshold,
                \ matrix    = src_matrix)
            }
            """,
    };
}
