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

force_source = "FFMS2"  # "FFMS2" | "" = auto (FFMS2 -> LSMASH -> LWLIBAV -> AVI) | "LWLIBAV" | "LSMASH"
show_source_used = true

# ------------ OPTIONS ------------
preview       = true
preview_half  = false
threads  = 4

# Preview
preview_label_size = 40
source_label_size  = 40

# ------------ L-SMASH Works ------------
global lw_threads   = 0
global lw_cache     = false
global lw_seek_mode = 0
global lw_dr        = false
global lw_format    = ""

# ------------ FFMS2 ------------
global ffms_cachefile = ""
global ffms_threads   = -1
global ffms_fpsnum    = -1
global ffms_fpsden    = 1


# =========================
# END CONFIGURATION
# =========================


# =========================
# PLUGINS (x64)
# =========================

function TryLoadPlugin(string path) {
  if (Exist(path)) { LoadPlugin(path) }
}

# All AviSynth plugins (LSMASHSource, RgTools, FFMS2, GamMac, MaskTools2,
# MVTools2, RemoveDirt) are installed in AviSynth+ plugins64+ and auto-loaded.

# __CUSTOM_PLUGINS__

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

function OpenVideoSourceSmart(string path, string "force", bool "show_label", int "source_overlay_size")
{
  Assert(Exist(path), "File not found: " + path)
  last_err = ""
  chosen = ""
  v = NOP()
  force = UCase(Default(force, ""))
  show_label = Default(show_label, true)
  source_overlay_size = Default(source_overlay_size, 26)

  if (force == "LWLIBAV") {
    try { v = LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LWLIBAV" }
    catch(err) { last_err = err }
  } else if (force == "LSMASH") {
    try { v = LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LSMASH" }
    catch(err) { last_err = err }
  } else if (force == "FFMS2") {
    try { v = FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)  chosen = "FFMS2" }
    catch(err) { last_err = err }
  } else {
    if (FunctionExists("FFVideoSource")) {
      try { v = FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)  chosen = "FFMS2" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && FunctionExists("LSMASHVideoSource")) {
      try { v = LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LSMASH" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && FunctionExists("LWLibavVideoSource")) {
      try { v = LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LWLIBAV" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && _LowerExt(path) == ".avi") {
      try { v = AviSource(path)  chosen = "AVI" }
      catch(err) { last_err = err }
    }
  }

  if (chosen == "") {
    err_clip = BlankClip(width=1280, height=720, fps=25, length=1, color=$000000)
    err_clip = Subtitle(err_clip, "Unable to open clip\n" + last_err, size=28, align=5, lsp=10)
    return err_clip
  }
  return _OpenWithLabel(chosen, v, overlay_size=source_overlay_size, show_overlay=show_label)
}

function _BuildPreview(clip src_clip, clip final_clip, int "label_size", bool "already_half") {
  label_size = Default(label_size, 28)
  already_half = Default(already_half, false)
  src_show = Subtitle(src_clip,   "original", size=label_size, align=2)
  res_show = Subtitle(final_clip, "final",    size=label_size, align=2)
  if (!already_half) {
    src_show = HalfSizeEven(src_show)
    res_show = HalfSizeEven(res_show)
  }
  # Ensure same height for StackHorizontal (custom filters may crop/resize)
  sh = Height(src_show)
  rh = Height(res_show)
  if (sh != rh) {
    rw = Even(Width(res_show) * sh / rh)
    res_show = res_show.Spline36Resize(rw, sh)
  }
  return StackHorizontal(src_show, res_show)
}


# =========================
# SOURCE
# =========================

src_path = src
src = NOP()

if (use_img) {
  src = ImageSource(src_path, start=img_start, end=img_end, fps=play_speed, use_DevIL=true, pixel_type="RGB32")
} else {
  src = OpenVideoSourceSmart(src_path, force=force_source, show_label=(show_source_used && preview), source_overlay_size=source_label_size)
}

src = src.KillAudio().AssumeFPS(play_speed)

# Border padding for temporal filters (prevents out-of-range access with ImageSource).
n_img_orig = 0
n_img_pad  = 0
if (use_img) {
  n_img_orig = FrameCount(src)
  n_img_pad  = 4   # covers MDegrain3 (delta=3) + RemoveDirtMC (delta=1) with margin
  src = Trim(src, 0, 0).Loop(n_img_pad) + src + Trim(src, n_img_orig - 1, n_img_orig - 1).Loop(n_img_pad)
}


# =========================
# PIPELINE
# =========================

c = src
pipeline_err = ""

# Half-resolution preview: resize before all filters
# (filter values marked ScaleWithPreview are divided by 2 in the generated script)
if (preview_half && preview) {
  _hw = c.Width()  / 2
  _hh = c.Height() / 2
  _hw = _hw - _hw % 2
  _hh = _hh - _hh % 2
  c = c.BicubicResize(_hw, _hh)
}

# Capture source before any filter for preview comparison
src_flipped = c

# ── All filters (user-defined order) ──
# __MODULE_PIPELINE__

src_matrix = _MatrixAuto(c)

final = c

if (pipeline_err != "") {
  err_clip = BlankClip(final, length=1, color=$000000)
  final = Subtitle(err_clip, pipeline_err, size=24, align=5, lsp=10)
}

# =========================
# PREVIEW / OUTPUT
# =========================

out = preview
    \ ? _BuildPreview(
    \     EnsureColorspaceSafe(src_flipped, "YV24", matrix=src_matrix),
    \     EnsureColorspaceSafe(final,   "YV24", matrix=src_matrix),
    \     label_size=preview_label_size,
    \     already_half=preview_half)
    \ : final

# Strip border padding added for temporal filters (image source only)
out = (n_img_pad > 0) ? Trim(out, n_img_pad, n_img_pad + n_img_orig - 1) : out

# Half-resolution: source already resized before pipeline (no late resize needed)

out = out.Prefetch(threads)
out
