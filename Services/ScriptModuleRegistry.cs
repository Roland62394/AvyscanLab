using System.Collections.Generic;
using AvyScanLab.Models;

namespace AvyScanLab.Services;

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
        ConfigSection =
            """
            enable_gammac        = false

            # GamMac
            LockChan  = 1
            LockVal   = 250
            Scale     = 2
            Th        = 0.12
            HiTh      = 0.25
            X         = 0
            Y         = 0
            W         = 0
            H         = 0
            Omin      = 0
            Omax      = 255
            Show      = false
            Verbosity = 4
            """,
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
        ConfigSection =
            """
            enable_denoise       = false  # RemoveDirt/MC — STEP 1 (film dust/dirt)

            # =============================================================
            # DENOISE — RemoveDirt/MC (dust, stains, film defects)
            # ORDER : FIRST, before any other processing
            # =============================================================
            denoise_mode     = "removedirtmc"
            #   "removedirtmc"  → RemoveDirtMC  (best, motion-compensated)
            #   "removedirt"    → RemoveDirt    (faster, no compensation)

            # Temporal detection threshold (pixel difference vs neighbouring frames)
            # RECOMMENDED VALUES based on film dirt level:
            #   Clean film    : denoise_strength = 6
            #   Average film  : denoise_strength = 10   ← recommended
            #   Dirty film    : denoise_strength = 14   (reasonable max)
            #   NEVER above 18-20, high risk of artefacts
            denoise_strength = 10

            # Spatial search radius for repair (dist in RestoreMotionBlocks)
            # Default 3 — increase to remove larger stains (e.g. 3→6→10)
            # Too high a value = risk of reconstruction with incorrect texture
            denoise_dist     = 3

            # true = luma only (faster) ; false = luma+chroma (better colour rendering)
            denoise_grey     = false
            """,
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
        ConfigSection =
            """
            enable_degrain       = false  # MVTools2 MDegrain — STEP 2 (grain/noise)

            # =============================================================
            # DEGRAIN — MVTools2 MDegrain (film grain, sensor noise)
            # ORDER : AFTER RemoveDirt, BEFORE colour corrections
            # FIX: replaced TemporalSoften with MDegrain2/3
            # =============================================================
            degrain_mode     = "mdegrain2"
            #   "mdegrain3" → MDegrain3 (best, 3 temporal references)
            #   "mdegrain2" → MDegrain2 (good quality/speed balance)  ← recommended
            #   "mdegrain1" → MDegrain1 (faster, less effective)
            #   "temporal"  → TemporalSoften (fallback without MVTools2)

            # Thresholds for MDegrain (luma / chroma)
            # Higher value = stronger degrain
            #   Fine grain    : thSAD=200, thSADC=150
            #   Medium grain  : thSAD=350, thSADC=250  ← recommended for 8/16mm film
            #   Heavy grain   : thSAD=500, thSADC=400
            degrain_thSAD    = 350  # luma threshold  (SAD per block)
            degrain_thSADC   = 250  # chroma threshold

            # PERFORMANCE — impact on speed (most to least impactful):
            #
            #  pel — sub-pixel precision of MSuper:
            #    pel=2 → ½ pixel, MSuper internally at 4x resolution → SLOW
            #    pel=1 → full pixel, ~2x faster, imperceptible loss on grain ← RECOMMENDED
            #
            #  blksize / overlap — analysis block size:
            #    blksize=8,  overlap=4  → precise on small grain, SLOW (4x more blocks)
            #    blksize=16, overlap=8  → good speed/quality balance               ← RECOMMENDED
            #    blksize=32, overlap=16 → very fast, less precise on small grain
            #    (overlap must always = blksize/2)
            #
            #  search — motion vector search algorithm:
            #    search=3 → exhaustive, slowest but most precise
            #    search=2 → hexagonal,  good speed/precision balance               ← RECOMMENDED
            #    search=0 → oneway,     fastest, less precise
            #
            degrain_blksize  = 16   # FAST: 8→16 (4x fewer blocks to analyse)
            degrain_overlap  = 8    # FAST: 4→8  (always blksize/2)
            degrain_pel      = 1    # FAST: 2→1  (~2x faster, imperceptible on grain)
            degrain_search   = 2    # NEW: hexagonal — good speed/precision balance

            # Prefilter for MVTools analysis (does not affect the final clip)
            degrain_prefilter = "remgrain"
            #   "remgrain"  → RemoveGrain(2) — fast and effective  ← RECOMMENDED
            #   "blur"      → Blur(1.0)     — alternative
            #   "none"      → no prefiltering (less precise)
            """,
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
        ConfigSection =
            """
            enable_luma_levels   = false

            # Luma Levels
            Lum_Bright   = 0.0
            Lum_Contrast = 1.05
            Lum_Sat      = 1.10
            Lum_Hue      = 0.0
            Lum_GammaY   = 1.30
            """,
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
        ConfigSection =
            """
            enable_sharp         = false  # ALWAYS LAST

            # Sharpen (ALWAYS LAST — after all corrections)
            Sharp_Mode      = "simple"   # "simple" | "edge"
            Sharp_Strength  = 8          # scale 1–20 (5=light, 8=standard, 12=medium, 16=strong, 20=very strong)
            Sharp_Radius    = 1.5
            Sharp_Threshold = 5          # increased: better protection against residual noise
            """,
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
