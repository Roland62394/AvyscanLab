# =========================
# Optimised restoration — AviSynth+ 64-bit  v2.0
# MAJOR CHANGES:
#   - Pipeline order fixed (RemoveDirt FIRST)
#   - Degrain via MVTools2 (MDegrain2/3) much more effective
#   - RemoveDirt parameters fixed (limit reduced, no artefacts)
#   - Sharpen moved LAST (avoids re-amplifying noise)
# =========================

# =========================
# START CONFIGURATION
# =========================

# ------------ SOURCE ------------
src = ""          # video file (.avi/.mov/.mp4/...) OR image pattern "D:\img\scan_%06d.tif"

img_start   = 1
img_end     = 244
play_speed  = 24

use_img = true    # true = ImageSource ; false = video

# AVI source mode
prefer_avi_vfw = true
force_source = ""  # "" = auto | "AVISOURCE" | "LWLIBAV" | "LSMASH" | "FFMS2" | "DSHOW"
show_source_used = true

# ------------ OPTIONS ------------
enable_flip_h = false
enable_flip_v = false

enable_crop          = true
enable_denoise       = true   # RemoveDirt/MC — STEP 1 (film dust/dirt)
enable_degrain       = true   # MVTools2 MDegrain — STEP 2 (grain/noise)
enable_luma_levels   = true
enable_gammac        = true
enable_sharp         = true   # ALWAYS LAST

preview  = true
threads  = 4

# ------------ PARAMS ------------
# Crop (no resizing)
Crop_L = 550
Crop_T = 500
Crop_R = 1000
Crop_B = 500

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

# Luma Levels
Lum_Bright   = 0.0
Lum_Contrast = 1.05
Lum_Sat      = 1.10
Lum_Hue      = 0.0
Lum_GammaY   = 1.30

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

# Sharpen (ALWAYS LAST — after all corrections)
Sharp_Mode      = "simple"   # "simple" | "edge"
Sharp_Strength  = 8          # scale 1–20 (5=light, 8=standard, 12=medium, 16=strong, 20=very strong)
Sharp_Radius    = 1.5
Sharp_Threshold = 5          # increased: better protection against residual noise

# Preview
preview_label_size = 40
source_label_size  = 40

# ------------ L-SMASH Works ------------
lw_threads   = 0
lw_cache     = true
lw_seek_mode = 0
lw_dr        = false
lw_format    = ""

# ------------ FFMS2 ------------
ffms_cachefile = ""
ffms_threads   = -1
ffms_fpsnum    = -1
ffms_fpsden    = 1

# ------------ DirectShow ------------
dshow_convertfps = true
dshow_fps        = play_speed

# =========================
# END CONFIGURATION
# =========================


# =========================
# PLUGINS (x64)
# =========================

pluginRoot = ScriptDir() + "Plugins/"

if (!use_img) {
  if (Exist(pluginRoot + "Dual/L-Smash-Works/LSMASHSource.dll")) {
    LoadPlugin(pluginRoot + "Dual/L-Smash-Works/LSMASHSource.dll")
  }
}

function TryLoadPlugin(string path) {
  if (Exist(path)) { LoadPlugin(path) }
}

TryLoadPlugin(pluginRoot + "Masktools2/masktools2.dll")
TryLoadPlugin(pluginRoot + "RgTools/RgTools.dll")
TryLoadPlugin(pluginRoot + "RemoveDirt/RemoveDirt.dll")
TryLoadPlugin(pluginRoot + "GamMac/GamMac_x64.dll")
TryLoadPlugin(pluginRoot + "FFMS2/ffms2.dll")

# MVTools2 — REQUIRED for MDegrain (download from avisynth.nl)
TryLoadPlugin(pluginRoot + "MVTools2/mvtools2.dll")

function TryImport(string path) {
  if (Exist(path)) { Import(path) }
}

TryImport(pluginRoot + "Scripts/RemoveDirt.avsi")
TryImport(pluginRoot + "Scripts/FFMS2.avsi")

# =========================
# HELPERS
# =========================

function _MatrixAuto(clip x) {
  return (x.Width() > 1024 || x.Height() > 576) ? "Rec709" : "Rec601"
}

function _LowerExt(string path) {
  lower = LCase(path)
  return RightStr(lower, 5) == ".tiff" ? ".tiff" : RightStr(lower, 4)
}

function Even(int v) { return (v/2)*2 }

function CropSafe(clip c, int l, int t, int r, int b)
{
  l = Even(l)  t = Even(t)  r = Even(r)  b = Even(b)
  return c.Crop(l, t, -r, -b)
}

function MakeEvenY(clip c)
{
  w = Width(c)   h = Height(c)
  newW = (w % 2 == 0) ? w : (w + 1)
  newH = (h % 2 == 0) ? h : (h + 1)
  return (newW == w && newH == h) ? c : c.Spline36Resize(newW, newH)
}

function HalfSizeEven(clip c) {
  targetW = max(2, Even((Width(c)  + 1)/2))
  targetH = max(2, Even((Height(c) + 1)/2))
  return c.Spline36Resize(targetW, targetH)
}

function ApplyMirrors(clip c, bool flip_h, bool flip_v) {
  c = flip_h ? c.FlipHorizontal() : c
  c = flip_v ? c.FlipVertical()   : c
  return c
}

function EnsureColorspaceSafe(clip c, string target, string "matrix")
{
  target = UCase(target)
  matrix = Default(matrix, _MatrixAuto(c))
  current_type = PixelType(c)
  isrgb = IsRGB(c)

  if (target == "RGB32" && current_type == "RGB32") { return c }
  if (target == "RGB24" && current_type == "RGB24") { return c }
  if (target == "YV24"  && current_type == "YV24")  { return c }
  if (target == "YV12"  && current_type == "YV12")  { return c }

  if (target == "RGB32") {
    return isrgb ? ConvertToRGB32(c) : ConvertToRGB32(c, matrix=matrix)
  }
  if (target == "RGB24") {
    return isrgb ? ConvertToRGB24(c) : ConvertToRGB24(c, matrix=matrix)
  }

  bd = BitsPerComponent(c)
  c8 = (bd == 8) ? c : ConvertBits(c, 8, dither=1)

  if (target == "YV24") {
    return IsRGB(c8) ? ConvertToYV24(c8, matrix=matrix) : ConvertToYV24(c8)
  }
  if (target == "YV12") {
    c8 = MakeEvenY(c8)
    return IsRGB(c8) ? ConvertToYV12(c8, matrix=matrix) : ConvertToYV12(c8)
  }
  return c
}

function _OpenWithLabel(string label, clip v, int "overlay_size", bool "show_overlay") {
  show_overlay = Default(show_overlay, true)
  overlay_size = Default(overlay_size, 26)
  return show_overlay ? Subtitle(v, "source: " + label, size=overlay_size, align=8) : v
}

function OpenVideoSourceSmart(string path, bool "prefer_vfw", string "force", bool "show_label", int "source_overlay_size")
{
  Assert(Exist(path), "Fichier introuvable: " + path)
  ext = _LowerExt(path)
  last_err = ""
  chosen = ""
  v = NOP()
  prefer_vfw = Default(prefer_vfw, true)
  force = Default(force, "")
  show_label = Default(show_label, true)
  source_overlay_size = Default(source_overlay_size, 26)

  if (force != "") {
    fs = UCase(force)
    try {
      v = (fs == "AVISOURCE") ? AviSource(path)
        \ : (fs == "LSMASH")  ? LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)
        \ : (fs == "LWLIBAV") ? LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)
        \ : (fs == "FFMS2")   ? FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)
        \                     : DirectShowSource(path)
      chosen = fs
    } catch(err) { last_err = err  chosen = "" }
  }

  if (chosen == "" && ext == ".avi" && prefer_vfw) {
    try { v = AviSource(path)  chosen = "AVISOURCE" }
    catch(err) { last_err = err }
  }
  if (chosen == "") {
    try { v = LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LWLIBAV" }
    catch(err) { last_err = err }
  }
  if (chosen == "" && (ext == ".mp4" || ext == ".m4v" || ext == ".mov")) {
    try { v = LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LSMASH" }
    catch(err) { last_err = err }
  }
  if (chosen == "" && FunctionExists("FFVideoSource")) {
    try { v = FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)  chosen = "FFMS2" }
    catch(err) { last_err = err }
  }
  if (chosen == "") {
    try { v = DirectShowSource(path)  chosen = "DSHOW" }
    catch(err) { last_err = err }
  }

  Assert(chosen != "", "Impossible d'ouvrir le clip.\nDernière erreur:\n" + last_err)
  return _OpenWithLabel(chosen, v, overlay_size=source_overlay_size, show_overlay=show_label)
}


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

function _BuildPreview(clip src_clip, clip final_clip, int "label_size") {
  label_size = Default(label_size, 28)
  src_show = Subtitle(src_clip,   "original", size=label_size, align=2)
  res_show = Subtitle(final_clip, "final",    size=label_size, align=2)
  src_show = HalfSizeEven(src_show)
  res_show = HalfSizeEven(res_show)
  return StackHorizontal(src_show, res_show)
}


# =========================
# SOURCE
# =========================

src_path = src
src = NOP()

if (use_img) {
  src = ImageSource(src_path, start=img_start, end=img_end, fps=play_speed, use_DevIL=true, pixel_type="RGB24")
} else {
  src = OpenVideoSourceSmart(src_path, prefer_vfw=prefer_avi_vfw, force=force_source, show_label=(show_source_used && preview), source_overlay_size=source_label_size)
}

src = ApplyMirrors(src, enable_flip_h, enable_flip_v)

if (enable_crop) {
  src = CropSafe(src, Crop_L, Crop_T, Crop_R, Crop_B)
}

src = src.KillAudio().AssumeFPS(play_speed)
src_matrix = _MatrixAuto(src)

# Border padding for temporal filters (prevents out-of-range access with ImageSource).
# MVTools2 MDegrain2/3 requests frames beyond clip boundaries for motion references.
# With ImageSource, those frames have no corresponding file → crash.
# We prepend/append copies of the first/last frame, then strip the padding at output.
n_img_orig = 0
n_img_pad  = 0
if (use_img) {
  n_img_orig = FrameCount(src)
  n_img_pad  = 4   # covers MDegrain3 (delta=3) + RemoveDirtMC (delta=1) with margin
  src = Trim(src, 0, 0).Loop(n_img_pad) + src + Trim(src, n_img_orig - 1, n_img_orig - 1).Loop(n_img_pad)
}


# =========================
# PIPELINE — FIXED ORDER v2
#
#  v1  :  Degrain → Denoise → Color → Sharpen
#  v2  :  Denoise → Degrain → GamMac → LumaLevels → Sharpen
#  v3  :  GamMac → Denoise → Degrain → LumaLevels → Sharpen  ← CURRENT
#
#  Why this order?
#  1. GamMac FIRST : neutralises colour cast (e.g. strong pink tint)
#     before RemoveDirt. RemoveDirt operates in YV12; a strong colour cast
#     biases chroma components and masks stains → less reliable detection.
#     After GamMac, stains have better contrast on a neutral background.
#  2. RemoveDirt AFTER GamMac : detects dust better on a balanced image.
#  3. MDegrain AFTER RemoveDirt : MVTools2 vectors more accurate without dust.
#  4. LumaLevels : fine adjustments on clean image.
#  5. Sharpen LAST : does not re-amplify residual noise/grain.
# =========================

c = src

# STEP 1 — GamMac FIRST (selective RGB correction — neutralises colour cast before RemoveDirt)
if (enable_gammac && FunctionExists("GamMac")) {
  base_pix = PixelType(c)
  mx  = _MatrixAuto(c)
  rgb = EnsureColorspaceSafe(c, "RGB24", matrix=mx)
  rgb = GamMac(rgb,
    \ LockChan=LockChan, LockVal=LockVal,
    \ x=X, y=Y, w=W, h=H,
    \ Scale=Scale, Th=Th, HiTh=HiTh,
    \ Omin=Omin, Omax=Omax, Show=Show, Verbosity=Verbosity)
  c = EnsureColorspaceSafe(rgb, base_pix, matrix=mx)
}

# STEP 2 — Dust / film stains (RemoveDirt)
if (enable_denoise) {
  c = DenoiseFilm(c,
    \ mode     = denoise_mode,
    \ strength = denoise_strength,
    \ dist     = denoise_dist,
    \ grey     = denoise_grey,
    \ matrix   = src_matrix)
}

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

# STEP 5 — Sharpen (ALWAYS LAST)
if (enable_sharp) {
  c = SharpenAdvanced(c,
    \ mode      = Sharp_Mode,
    \ strength  = Sharp_Strength,
    \ radius    = Sharp_Radius,
    \ threshold = Sharp_Threshold,
    \ matrix    = src_matrix)
}

final = c


# =========================
# PREVIEW / OUTPUT
# =========================

out = preview
    \ ? _BuildPreview(
    \     EnsureColorspaceSafe(src,   "YV24", matrix=src_matrix),
    \     EnsureColorspaceSafe(final, "YV24", matrix=src_matrix),
    \     label_size=preview_label_size)
    \ : final

# Strip border padding added for temporal filters (image source only)
out = (n_img_pad > 0) ? Trim(out, n_img_pad, n_img_pad + n_img_orig - 1) : out

effective_threads = (enable_gammac) ? 1 : threads
out = out.Prefetch(effective_threads)
out
