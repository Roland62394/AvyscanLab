# =========================
# Optimierte Restaurierung — AviSynth+ 64-bit  v2.0
# WESENTLICHE ÄNDERUNGEN:
#   - Pipeline-Reihenfolge korrigiert (RemoveDirt ZUERST)
#   - Degrain via MVTools2 (MDegrain2/3) deutlich effektiver
#   - RemoveDirt-Parameter korrigiert (limit reduziert, keine Artefakte)
#   - Sharpen ans ENDE verschoben (verhindert Rauschverstärkung)
# =========================

# =========================
# START CONFIGURATION
# =========================

# ------------ SOURCE ------------
src = ""          # Videodatei (.avi/.mov/.mp4/...) ODER Bildmuster "D:\img\scan_%06d.tif"

img_start   = 1
img_end     = 244
play_speed  = 24

use_img = true    # true = ImageSource ; false = Video

force_source = ""  # "" = auto (LWLIBAV -> LSMASH -> FFMS2 -> AVI) | "LWLIBAV" | "LSMASH" | "FFMS2"
show_source_used = true

# ------------ OPTIONS ------------
enable_flip_h = false
enable_flip_v = false

enable_crop          = true
enable_denoise       = true   # RemoveDirt/MC — SCHRITT 1 (Filmstaub/-flecken)
enable_degrain       = true   # MVTools2 MDegrain — SCHRITT 2 (Korn/Rauschen)
enable_luma_levels   = true
enable_gammac        = true
enable_sharp         = true   # IMMER ZULETZT

preview  = true
threads  = 4

# ------------ PARAMS ------------
# Zuschneiden (keine Skalierung)
Crop_L = 550
Crop_T = 500
Crop_R = 1000
Crop_B = 500

# =============================================================
# DENOISE — RemoveDirt/MC (Staub, Flecken, Filmfehler)
# REIHENFOLGE : ZUERST, vor jeder anderen Verarbeitung
# =============================================================
denoise_mode     = "removedirtmc"
#   "removedirtmc"  → RemoveDirtMC  (beste Qualität, bewegungskompensiert)
#   "removedirt"    → RemoveDirt    (schneller, ohne Kompensation)

# Zeitlicher Erkennungsschwellwert (Pixeldifferenz zu benachbarten Frames)
# EMPFOHLENE WERTE je nach Verschmutzungsgrad des Films:
#   Sauberer Film    : denoise_strength = 6
#   Normaler Film    : denoise_strength = 10   ← empfohlen
#   Schmutziger Film : denoise_strength = 14   (sinnvolles Maximum)
#   NIE über 18-20, hohes Artefaktrisiko
denoise_strength = 10

# Räumlicher Suchradius für die Reparatur (dist in RestoreMotionBlocks)
# Standard 3 — erhöhen zum Entfernen größerer Flecken (z. B. 3→6→10)
# Zu hoher Wert = Risiko einer Rekonstruktion mit falscher Textur
denoise_dist     = 3

# true = nur Luma (schneller) ; false = Luma+Chroma (bessere Farbwiedergabe)
denoise_grey     = false

# =============================================================
# DEGRAIN — MVTools2 MDegrain (Filmkorn, Sensorrauschen)
# REIHENFOLGE : NACH RemoveDirt, VOR Farbkorrekturen
# KORREKTUR: TemporalSoften durch MDegrain2/3 ersetzt
# =============================================================
degrain_mode     = "mdegrain2"
#   "mdegrain3" → MDegrain3 (beste Qualität, 3 zeitliche Referenzen)
#   "mdegrain2" → MDegrain2 (gutes Qualitäts-/Geschwindigkeitsverhältnis)  ← empfohlen
#   "mdegrain1" → MDegrain1 (schneller, weniger effektiv)
#   "temporal"  → TemporalSoften (Fallback ohne MVTools2)

# Schwellwerte für MDegrain (Luma / Chroma)
# Höherer Wert = stärkeres Degrain
#   Feines Korn    : thSAD=200, thSADC=150
#   Mittleres Korn : thSAD=350, thSADC=250  ← empfohlen für 8/16mm-Film
#   Starkes Korn   : thSAD=500, thSADC=400
degrain_thSAD    = 350  # Luma-Schwellwert  (SAD pro Block)
degrain_thSADC   = 250  # Chroma-Schwellwert

# LEISTUNG — Einfluss auf Geschwindigkeit (größter bis kleinster Einfluss):
#
#  pel — Subpixel-Genauigkeit von MSuper:
#    pel=2 → ½ Pixel, MSuper intern mit 4-facher Auflösung → LANGSAM
#    pel=1 → ganzes Pixel, ~2× schneller, kaum Verlust bei Korn ← EMPFOHLEN
#
#  blksize / overlap — Analysblockgröße:
#    blksize=8,  overlap=4  → präzise bei feinem Korn, LANGSAM (4× mehr Blöcke)
#    blksize=16, overlap=8  → gutes Geschwindigkeits-/Qualitätsverhältnis  ← EMPFOHLEN
#    blksize=32, overlap=16 → sehr schnell, weniger präzise bei feinem Korn
#    (overlap muss immer = blksize/2 sein)
#
#  search — Suchalgorithmus für Bewegungsvektoren:
#    search=3 → erschöpfend, langsamster aber präzisester
#    search=2 → hexagonal,   gutes Geschwindigkeits-/Präzisionsverhältnis  ← EMPFOHLEN
#    search=0 → einspurig,   schnellster, weniger präzise
#
degrain_blksize  = 16   # SCHNELL: 8→16 (4× weniger Blöcke zu analysieren)
degrain_overlap  = 8    # SCHNELL: 4→8  (immer blksize/2)
degrain_pel      = 1    # SCHNELL: 2→1  (~2× schneller, kaum wahrnehmbar bei Korn)
degrain_search   = 2    # NEU: hexagonal — gutes Geschwindigkeits-/Präzisionsverhältnis

# Vorfilter für MVTools-Analyse (beeinflusst den finalen Clip nicht)
degrain_prefilter = "remgrain"
#   "remgrain"  → RemoveGrain(2) — schnell und effektiv  ← EMPFOHLEN
#   "blur"      → Blur(1.0)     — Alternative
#   "none"      → kein Vorfilter (weniger präzise)

# Luma-Pegel
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

# Schärfen (IMMER ZULETZT — nach allen Korrekturen)
Sharp_Mode      = "simple"   # "simple" | "edge"
Sharp_Strength  = 8          # Skala 1–20 (5=leicht, 8=standard, 12=mittel, 16=stark, 20=sehr stark)
Sharp_Radius    = 1.5
Sharp_Threshold = 5          # erhöht: besserer Schutz vor Rauschresiduen

# Vorschau
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

# MVTools2 — ERFORDERLICH für MDegrain (Download unter avisynth.nl)
TryLoadPlugin(pluginRoot + "MVTools2/mvtools2.dll")

function TryImport(string path) {
  if (Exist(path)) { Import(path) }
}

TryImport(pluginRoot + "Scripts/RemoveDirt.avsi")
TryImport(pluginRoot + "Scripts/FFMS2.avsi")

# =========================
# HILFSFUNKTIONEN
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
  Assert(Exist(path), "Datei nicht gefunden: " + path)
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
    if (FunctionExists("LWLibavVideoSource")) {
      try { v = LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LWLIBAV" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && FunctionExists("LSMASHVideoSource")) {
      try { v = LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LSMASH" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && FunctionExists("FFVideoSource")) {
      try { v = FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)  chosen = "FFMS2" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && _LowerExt(path) == ".avi") {
      try { v = AviSource(path)  chosen = "AVI" }
      catch(err) { last_err = err }
    }
  }

  Assert(chosen != "", "Clip konnte nicht geöffnet werden.\nLetzter Fehler:\n" + last_err)
  return _OpenWithLabel(chosen, v, overlay_size=source_overlay_size, show_overlay=show_label)
}



# =========================
# FILTER 1 — DENOISE / REMOVEDIRT
# Aufgabe : Staub, Flecken und Filmkratzer entfernen
# Position: ZUERST — auf der Rohquelle, vor jeder Verarbeitung
# KORR. v2: limit korrigiert (6-14 je nach Verschmutzung), nicht mehr 24 (verursachte Artefakte)
# =========================

function DenoiseFilm(clip c, string "mode", float "strength", int "dist", bool "grey", string "matrix")
{
  mode   = Default(mode,     "removedirtmc")
  lim    = int(Default(strength, 10.0))  # akzeptiert int oder float, wird in int umgewandelt
  dist   = Default(dist,     3)
  grey   = Default(grey,     false)
  matrix = Default(matrix,   _MatrixAuto(c))

  base_pix = PixelType(c)
  bd = BitsPerComponent(c)

  # RemoveDirt/MC arbeitet in YV12 (erforderlich)
  c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
  c8  = MakeEvenY(c8)
  yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

  if (mode == "removedirtmc" && FunctionExists("RemoveDirtMC")) {
    result = RemoveDirtMC(yuv, limit=lim, _grey=grey, dist=dist)

  } else if (mode == "removedirt" && FunctionExists("RemoveDirt")) {
    result = RemoveDirt(yuv, limit=lim, grey=grey, dist=dist)

  } else {
    # Fallback: gezieltes TemporalSoften (falls RemoveDirt nicht verfügbar)
    result = TemporalSoften(yuv, 1, 4, 4, 12, 2)
  }

  # Rückkehr zum Originalformat
  result = (bd == 8) ? result : ConvertBits(result, bd)
  return (PixelType(result) == base_pix) ? result
       \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
}


# =========================
# FILTER 2 — DEGRAIN (MVTools2 MDegrain)
# Aufgabe : Filmkorn und allgemeines Rauschen reduzieren
# Position: NACH RemoveDirt, VOR Farbkorrekturen
# KORR. v2: MDegrain2/3 ersetzt TemporalSoften (deutlich effektiver)
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

  # --- Fallback falls MVTools2 fehlt ---
  if (!FunctionExists("MSuper")) {
    # Konservatives TemporalSoften als Fallback
    result = TemporalSoften(yuv, 2, 6, 5, 15, 2)
    result = (bd == 8) ? result : ConvertBits(result, bd)
    return (PixelType(result) == base_pix) ? result
         \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # --- Vorfilter für die Bewegungsvektoranalyse ---
  # Vorfilterung verbessert die MAnalyse-Präzision OHNE den finalen Clip zu beeinflussen
  prefilt = (prefilter == "remgrain") ? RemoveGrain(yuv, 2)
          \ : (prefilter == "blur")   ? Blur(yuv, 1.0)
          \                           : yuv

  # --- MVTools2-Analyse ---
  sup_orig = MSuper(yuv,    pel=pel, sharp=2)         # Super für Kompensation
  sup_pre  = MSuper(prefilt, pel=pel, sharp=2)           # vorgefilterte Super für Analyse (levels auto)

  # Vorwärts-/Rückwärtsvektoren (Radius 1)
  bv1 = MAnalyse(sup_pre, isb=true,  delta=1, blksize=blksize, overlap=overlap, search=search)
  fv1 = MAnalyse(sup_pre, isb=false, delta=1, blksize=blksize, overlap=overlap, search=search)

  if (mode == "mdegrain1") {
    result = MDegrain1(yuv, sup_orig, bv1, fv1, thSAD=thSAD, thSADC=thSADC)

  } else if (mode == "mdegrain2") {
    # MDegrain2: 2 zeitliche Referenzen auf jeder Seite (empfohlen)
    bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
    fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
    result = MDegrain2(yuv, sup_orig, bv1, fv1, bv2, fv2, thSAD=thSAD, thSADC=thSADC)

  } else {
    # MDegrain3: 3 zeitliche Referenzen (beste Qualität, langsamer)
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
# FILTER 3 — SCHÄRFEN (unverändert, immer zuletzt)
# =========================

function SharpenAdvanced(clip c, string "mode", float "strength", float "radius", int "threshold", string "matrix")
{
  mode      = Default(mode, "unsharp")
  strength  = Default(strength, 8)
  radius    = Default(radius, 1.5)
  threshold = Default(threshold, 5)
  matrix    = Default(matrix, _MatrixAuto(c))

  base_pix = PixelType(c)

  # Unschärfe-Radius für Unsharp Mask: radius steuert die Breite des Schärfungshalos.
  # radius=1.0 → feine, präzise Schärfung ; radius=3.0 → breite, ausgeprägte Schärfung.
  blur_r = Min((radius - 1.0) * 0.45 + 0.15, 1.5)

  if (mode == "edge") {
    yuv = EnsureColorspaceSafe(c, "YV24", matrix=matrix)

    # Schärfung via Unsharp Mask mit variablem Radius
    blurred = yuv.Blur(blur_r)
    sharp = mt_lutxy(yuv, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)

    # Dualer Sobel-Kantenmask (horizontal + vertikal) mit Gammakompression.
    # Die ^0.86-Kurve (aus LimitedSharpenFaster/Didée) unterdrückt natürlich schwache
    # Antworten (Korn, Feinstruktur) und bewahrt echte Objektkanten.
    edge_h    = yuv.mt_edge(thY1=0, thY2=255, "8 16 8 0 0 0 -8 -16 -8 4")
    edge_v    = yuv.mt_edge(thY1=0, thY2=255, "8 0 -8 16 0 -16 8 0 -8 4")
    edge_mask = mt_logic(edge_h, edge_v, "max") \
              .mt_lut("x 128 / 0.86 ^ 255 *", U=1, V=1)

    # Maske aufblasen (volle Kantenbreite), dann weich machen (graduelle Übergänge)
    edge_mask = edge_mask.mt_inflate().mt_inflate().Blur(1.0)

    # Optionaler Schwellwert: verbleibende schwache Gamma-Antworten ausschließen. Empfohlen: 20–40.
    edge_mask = (threshold > 0)
              \ ? edge_mask.mt_lut("x " + String(threshold) + " < 0 x ?", U=1, V=1)
              \ : edge_mask

    # Zusammenführen: Original auf flachen Flächen, geschärft an Kanten — Chroma unverändert
    result = mt_merge(yuv, sharp, edge_mask, U=1, V=1)
    return EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # "simple": globale Unsharp Mask mit variablem Radius — effektiv, kann Korn leicht verstärken
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
# QUELLE
# =========================

src_path = src
src = NOP()

if (use_img) {
  src = ImageSource(src_path, start=img_start, end=img_end, fps=play_speed, use_DevIL=true, pixel_type="RGB24")
} else {
  src = OpenVideoSourceSmart(src_path, force=force_source, show_label=(show_source_used && preview), source_overlay_size=source_label_size)
}

src = ApplyMirrors(src, enable_flip_h, enable_flip_v)

if (enable_crop) {
  src = CropSafe(src, Crop_L, Crop_T, Crop_R, Crop_B)
}

src = src.KillAudio().AssumeFPS(play_speed)
src_matrix = _MatrixAuto(src)

# Rand-Padding für Zeitfilter (verhindert Zugriffe außerhalb des Bereichs bei ImageSource).
# MVTools2 MDegrain2/3 fordert Frames außerhalb des Clips für Bewegungsreferenzen an.
# Bei ImageSource existieren diese Frames nicht als Datei → Absturz.
# Wir fügen vorne/hinten Kopien des ersten/letzten Frames ein und entfernen das Padding am Ausgang.
n_img_orig = 0
n_img_pad  = 0
if (use_img) {
  n_img_orig = FrameCount(src)
  n_img_pad  = 4   # deckt MDegrain3 (delta=3) + RemoveDirtMC (delta=1) mit Puffer ab
  src = Trim(src, 0, 0).Loop(n_img_pad) + src + Trim(src, n_img_orig - 1, n_img_orig - 1).Loop(n_img_pad)
}


# =========================
# PIPELINE — KORRIGIERTE REIHENFOLGE v2
#
#  v1  :  Degrain → Denoise → Farbe → Schärfen
#  v2  :  Denoise → Degrain → GamMac → LumaLevels → Schärfen
#  v3  :  GamMac → Denoise → Degrain → LumaLevels → Schärfen  ← AKTUELL
#
#  Warum diese Reihenfolge?
#  1. GamMac ZUERST: neutralisiert Farbstich (z. B. starker Rosaton)
#     vor RemoveDirt. RemoveDirt arbeitet in YV12; ein starker Farbstich
#     verzerrt Chroma-Komponenten und maskiert Flecken → weniger zuverlässige Erkennung.
#     Nach GamMac haben Flecken besseren Kontrast auf neutralem Hintergrund.
#  2. RemoveDirt NACH GamMac: erkennt Staub besser auf ausgewogenem Bild.
#  3. MDegrain NACH RemoveDirt: MVTools2-Vektoren ohne Staub präziser.
#  4. LumaLevels: feine Anpassungen auf sauberem Bild.
#  5. Schärfen ZULETZT: verstärkt Rauschen/Korn nicht erneut.
# =========================

c = src

# SCHRITT 1 — GamMac ZUERST (selektive RGB-Korrektur — neutralisiert Farbstich vor RemoveDirt)
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

# SCHRITT 2 — Staub / Filmflecken (RemoveDirt)
if (enable_denoise) {
  c = DenoiseFilm(c,
    \ mode     = denoise_mode,
    \ strength = denoise_strength,
    \ dist     = denoise_dist,
    \ grey     = denoise_grey,
    \ matrix   = src_matrix)
}

# SCHRITT 3 — Filmkorn (MVTools2 MDegrain)
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

# SCHRITT 4 — Manuelle Anpassungen (Tweak + ColorYUV)
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

# SCHRITT 5 — Schärfen (IMMER ZULETZT)
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
# VORSCHAU / AUSGABE
# =========================

out = preview
    \ ? _BuildPreview(
    \     EnsureColorspaceSafe(src,   "YV24", matrix=src_matrix),
    \     EnsureColorspaceSafe(final, "YV24", matrix=src_matrix),
    \     label_size=preview_label_size)
    \ : final

# Rand-Padding für Zeitfilter entfernen (nur bei Bildsequenz-Quelle)
out = (n_img_pad > 0) ? Trim(out, n_img_pad, n_img_pad + n_img_orig - 1) : out

effective_threads = (enable_gammac) ? 1 : threads
out = out.Prefetch(effective_threads)
out
