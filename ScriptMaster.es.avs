# =========================
# Restauración optimizada — AviSynth+ 64-bit  v2.0
# CAMBIOS PRINCIPALES:
#   - Orden del pipeline corregido (RemoveDirt PRIMERO)
#   - Degrain via MVTools2 (MDegrain2/3) mucho más eficaz
#   - Parámetros de RemoveDirt corregidos (limit reducido, sin artefactos)
#   - Sharpen movido al FINAL (evita reamplificar el ruido)
# =========================

# =========================
# START CONFIGURATION
# =========================

# ------------ SOURCE ------------
src = ""          # archivo de vídeo (.avi/.mov/.mp4/...) O patrón de imagen "D:\img\scan_%06d.tif"

img_start   = 1
img_end     = 244
play_speed  = 24

use_img = true    # true = ImageSource ; false = vídeo

force_source = ""  # "" = auto (LWLIBAV -> LSMASH -> FFMS2 -> DSHOW) | "LWLIBAV" | "LSMASH" | "FFMS2" | "DSHOW"
show_source_used = true

# ------------ OPTIONS ------------
enable_flip_h = false
enable_flip_v = false

enable_crop          = true
enable_denoise       = true   # RemoveDirt/MC — PASO 1 (polvo/manchas de película)
enable_degrain       = true   # MVTools2 MDegrain — PASO 2 (grano/ruido)
enable_luma_levels   = true
enable_gammac        = true
enable_sharp         = true   # SIEMPRE AL FINAL

preview  = true
threads  = 4

# ------------ PARAMS ------------
# Recorte (sin redimensionado)
Crop_L = 550
Crop_T = 500
Crop_R = 1000
Crop_B = 500

# =============================================================
# DENOISE — RemoveDirt/MC (polvo, manchas, defectos de película)
# ORDEN : PRIMERO, antes de cualquier otro procesado
# =============================================================
denoise_mode     = "removedirtmc"
#   "removedirtmc"  → RemoveDirtMC  (mejor calidad, compensación de movimiento)
#   "removedirt"    → RemoveDirt    (más rápido, sin compensación)

# Umbral de detección temporal (diferencia de píxel respecto a fotogramas vecinos)
# VALORES RECOMENDADOS según el nivel de suciedad de la película:
#   Película limpia   : denoise_strength = 6
#   Película normal   : denoise_strength = 10   ← recomendado
#   Película sucia    : denoise_strength = 14   (máximo razonable)
#   NUNCA por encima de 18-20, alto riesgo de artefactos
denoise_strength = 10

# Radio de búsqueda espacial para la reparación (dist en RestoreMotionBlocks)
# Por defecto 3 — aumentar para eliminar manchas más grandes (p. ej. 3→6→10)
# Un valor demasiado alto = riesgo de reconstruir con textura incorrecta
denoise_dist     = 3

# true = solo luma (más rápido) ; false = luma+chroma (mejor reproducción del color)
denoise_grey     = false

# =============================================================
# DEGRAIN — MVTools2 MDegrain (grano de película, ruido del sensor)
# ORDEN : DESPUÉS de RemoveDirt, ANTES de las correcciones de color
# CORRECCIÓN: TemporalSoften reemplazado por MDegrain2/3
# =============================================================
degrain_mode     = "mdegrain2"
#   "mdegrain3" → MDegrain3 (mejor calidad, 3 referencias temporales)
#   "mdegrain2" → MDegrain2 (buen equilibrio calidad/velocidad)  ← recomendado
#   "mdegrain1" → MDegrain1 (más rápido, menos eficaz)
#   "temporal"  → TemporalSoften (alternativa sin MVTools2)

# Umbrales para MDegrain (luma / chroma)
# Valor más alto = degrain más fuerte
#   Grano fino    : thSAD=200, thSADC=150
#   Grano medio   : thSAD=350, thSADC=250  ← recomendado para película 8/16mm
#   Grano grueso  : thSAD=500, thSADC=400
degrain_thSAD    = 350  # umbral de luma  (SAD por bloque)
degrain_thSADC   = 250  # umbral de chroma

# RENDIMIENTO — impacto en la velocidad (mayor a menor impacto):
#
#  pel — precisión subpíxel de MSuper:
#    pel=2 → ½ píxel, MSuper internamente a 4× la resolución → LENTO
#    pel=1 → píxel entero, ~2× más rápido, pérdida imperceptible en el grano ← RECOMENDADO
#
#  blksize / overlap — tamaño de bloque de análisis:
#    blksize=8,  overlap=4  → preciso en grano fino, LENTO (4× más bloques)
#    blksize=16, overlap=8  → buen equilibrio velocidad/calidad               ← RECOMENDADO
#    blksize=32, overlap=16 → muy rápido, menos preciso en grano fino
#    (overlap debe ser siempre = blksize/2)
#
#  search — algoritmo de búsqueda de vectores de movimiento:
#    search=3 → exhaustivo, el más lento pero el más preciso
#    search=2 → hexagonal,  buen equilibrio velocidad/precisión               ← RECOMENDADO
#    search=0 → unidireccional, el más rápido, menos preciso
#
degrain_blksize  = 16   # RÁPIDO: 8→16 (4× menos bloques a analizar)
degrain_overlap  = 8    # RÁPIDO: 4→8  (siempre blksize/2)
degrain_pel      = 1    # RÁPIDO: 2→1  (~2× más rápido, imperceptible en el grano)
degrain_search   = 2    # NUEVO: hexagonal — buen equilibrio velocidad/precisión

# Prefiltrado para el análisis de MVTools (no afecta al clip final)
degrain_prefilter = "remgrain"
#   "remgrain"  → RemoveGrain(2) — rápido y eficaz  ← RECOMENDADO
#   "blur"      → Blur(1.0)     — alternativa
#   "none"      → sin prefiltrado (menos preciso)

# Niveles de luma
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

# Enfocar (SIEMPRE AL FINAL — después de todas las correcciones)
Sharp_Mode      = "simple"   # "simple" | "edge"
Sharp_Strength  = 8          # escala 1–20 (5=ligero, 8=estándar, 12=medio, 16=fuerte, 20=muy fuerte)
Sharp_Radius    = 1.5
Sharp_Threshold = 5          # aumentado: mejor protección contra el ruido residual

# Vista previa
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

# MVTools2 — NECESARIO para MDegrain (descargar en avisynth.nl)
TryLoadPlugin(pluginRoot + "MVTools2/mvtools2.dll")

function TryImport(string path) {
  if (Exist(path)) { Import(path) }
}

TryImport(pluginRoot + "Scripts/RemoveDirt.avsi")
TryImport(pluginRoot + "Scripts/FFMS2.avsi")

# =========================
# FUNCIONES AUXILIARES
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
  Assert(Exist(path), "Archivo no encontrado: " + path)
  ext = _LowerExt(path)
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
  } else if (force == "DSHOW") {
    try { v = DirectShowSource(path, convertfps=dshow_convertfps, fps=dshow_fps)  chosen = "DSHOW" }
    catch(err) { last_err = err }
  } else {
    try { v = LWLibavVideoSource(path, threads=lw_threads, cache=lw_cache, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LWLIBAV" }
    catch(err) { last_err = err }

    if (chosen == "" && (ext == ".mp4" || ext == ".m4v" || ext == ".mov")) {
      try { v = LSMASHVideoSource(path, threads=lw_threads, seek_mode=lw_seek_mode, dr=lw_dr, format=lw_format)  chosen = "LSMASH" }
      catch(err) { last_err = err }
    }

    if (chosen == "" && FunctionExists("FFVideoSource")) {
      try { v = FFVideoSource(path, cachefile=ffms_cachefile, threads=ffms_threads, fpsnum=ffms_fpsnum, fpsden=ffms_fpsden)  chosen = "FFMS2" }
      catch(err) { last_err = err }
    }

    if (chosen == "") {
      try { v = DirectShowSource(path, convertfps=dshow_convertfps, fps=dshow_fps)  chosen = "DSHOW" }
      catch(err) { last_err = err }
    }
  }

  Assert(chosen != "", "No se puede abrir el clip.\nÚltimo error:\n" + last_err)
  return _OpenWithLabel(chosen, v, overlay_size=source_overlay_size, show_overlay=show_label)
}



# =========================
# FILTRO 1 — DENOISE / REMOVEDIRT
# Función : Eliminar polvo, manchas y rayaduras de película
# Posición: PRIMERO — sobre la fuente bruta, antes de cualquier procesado
# CORR. v2: limit corregido (6-14 según suciedad), ya no 24 que causaba artefactos
# =========================

function DenoiseFilm(clip c, string "mode", float "strength", int "dist", bool "grey", string "matrix")
{
  mode   = Default(mode,     "removedirtmc")
  lim    = int(Default(strength, 10.0))  # acepta int o float, convertido a int para RemoveDirt
  dist   = Default(dist,     3)
  grey   = Default(grey,     false)
  matrix = Default(matrix,   _MatrixAuto(c))

  base_pix = PixelType(c)
  bd = BitsPerComponent(c)

  # RemoveDirt/MC opera en YV12 (obligatorio)
  c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
  c8  = MakeEvenY(c8)
  yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

  if (mode == "removedirtmc" && FunctionExists("RemoveDirtMC")) {
    result = RemoveDirtMC(yuv, limit=lim, _grey=grey, dist=dist)

  } else if (mode == "removedirt" && FunctionExists("RemoveDirt")) {
    result = RemoveDirt(yuv, limit=lim, grey=grey, dist=dist)

  } else {
    # Alternativa: TemporalSoften selectivo (si RemoveDirt no está disponible)
    result = TemporalSoften(yuv, 1, 4, 4, 12, 2)
  }

  # Volver al formato original
  result = (bd == 8) ? result : ConvertBits(result, bd)
  return (PixelType(result) == base_pix) ? result
       \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
}


# =========================
# FILTRO 2 — DEGRAIN (MVTools2 MDegrain)
# Función : Reducir el grano de película y el ruido general
# Posición: DESPUÉS de RemoveDirt, ANTES de las correcciones de color
# CORR. v2: MDegrain2/3 reemplaza a TemporalSoften (mucho más eficaz)
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

  # --- Alternativa si MVTools2 no está disponible ---
  if (!FunctionExists("MSuper")) {
    # TemporalSoften conservador como alternativa
    result = TemporalSoften(yuv, 2, 6, 5, 15, 2)
    result = (bd == 8) ? result : ConvertBits(result, bd)
    return (PixelType(result) == base_pix) ? result
         \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # --- Prefiltrado para el análisis de vectores de movimiento ---
  # El prefiltrado mejora la precisión de MAnalyse SIN afectar al clip final
  prefilt = (prefilter == "remgrain") ? RemoveGrain(yuv, 2)
          \ : (prefilter == "blur")   ? Blur(yuv, 1.0)
          \                           : yuv

  # --- Análisis MVTools2 ---
  sup_orig = MSuper(yuv,    pel=pel, sharp=2)         # super para compensación
  sup_pre  = MSuper(prefilt, pel=pel, sharp=2)           # super prefiltrado para análisis (levels auto)

  # Vectores adelante/atrás (radio 1)
  bv1 = MAnalyse(sup_pre, isb=true,  delta=1, blksize=blksize, overlap=overlap, search=search)
  fv1 = MAnalyse(sup_pre, isb=false, delta=1, blksize=blksize, overlap=overlap, search=search)

  if (mode == "mdegrain1") {
    result = MDegrain1(yuv, sup_orig, bv1, fv1, thSAD=thSAD, thSADC=thSADC)

  } else if (mode == "mdegrain2") {
    # MDegrain2: 2 referencias temporales en cada lado (recomendado)
    bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
    fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
    result = MDegrain2(yuv, sup_orig, bv1, fv1, bv2, fv2, thSAD=thSAD, thSADC=thSADC)

  } else {
    # MDegrain3: 3 referencias temporales (mejor calidad, más lento)
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
# FILTRO 3 — ENFOCAR (sin cambios, siempre al final)
# =========================

function SharpenAdvanced(clip c, string "mode", float "strength", float "radius", int "threshold", string "matrix")
{
  mode      = Default(mode, "unsharp")
  strength  = Default(strength, 8)
  radius    = Default(radius, 1.5)
  threshold = Default(threshold, 5)
  matrix    = Default(matrix, _MatrixAuto(c))

  base_pix = PixelType(c)

  # Radio de desenfoque para Unsharp Mask: radius controla la amplitud del halo de enfoque.
  # radius=1.0 → enfoque fino y preciso ; radius=3.0 → enfoque amplio y pronunciado.
  blur_r = Min((radius - 1.0) * 0.45 + 0.15, 1.5)

  if (mode == "edge") {
    yuv = EnsureColorspaceSafe(c, "YV24", matrix=matrix)

    # Enfoque via Unsharp Mask con radio variable
    blurred = yuv.Blur(blur_r)
    sharp = mt_lutxy(yuv, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)

    # Máscara Sobel dual (horizontal + vertical) con compresión gamma.
    # La curva ^0.86 (de LimitedSharpenFaster/Didée) suprime naturalmente las respuestas
    # débiles (grano, textura fina) y preserva los contornos reales de objetos.
    edge_h    = yuv.mt_edge(thY1=0, thY2=255, "8 16 8 0 0 0 -8 -16 -8 4")
    edge_v    = yuv.mt_edge(thY1=0, thY2=255, "8 0 -8 16 0 -16 8 0 -8 4")
    edge_mask = mt_logic(edge_h, edge_v, "max") \
              .mt_lut("x 128 / 0.86 ^ 255 *", U=1, V=1)

    # Inflar la máscara (cubre el ancho completo del borde), luego suavizar transiciones
    edge_mask = edge_mask.mt_inflate().mt_inflate().Blur(1.0)

    # Umbral opcional: excluir respuestas gamma residuales débiles. Recomendado: 20–40.
    edge_mask = (threshold > 0)
              \ ? edge_mask.mt_lut("x " + String(threshold) + " < 0 x ?", U=1, V=1)
              \ : edge_mask

    # Fusionar: original en zonas planas, enfocado en bordes — croma sin cambios
    result = mt_merge(yuv, sharp, edge_mask, U=1, V=1)
    return EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # "simple": Unsharp Mask global con radio variable — efectivo pero puede amplificar ligeramente el grano
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
# FUENTE
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

# Relleno de bordes para filtros temporales (evita accesos fuera de rango con ImageSource).
# MVTools2 MDegrain2/3 solicita frames más allá de los límites del clip para referencias de movimiento.
# Con ImageSource, esos frames no tienen archivo correspondiente → crash.
# Añadimos copias del primer/último frame al inicio/fin y eliminamos el relleno en la salida.
n_img_orig = 0
n_img_pad  = 0
if (use_img) {
  n_img_orig = FrameCount(src)
  n_img_pad  = 4   # cubre MDegrain3 (delta=3) + RemoveDirtMC (delta=1) con margen
  src = Trim(src, 0, 0).Loop(n_img_pad) + src + Trim(src, n_img_orig - 1, n_img_orig - 1).Loop(n_img_pad)
}


# =========================
# PIPELINE — ORDEN CORREGIDO v2
#
#  v1  :  Degrain → Denoise → Color → Enfocar
#  v2  :  Denoise → Degrain → GamMac → LumaLevels → Enfocar
#  v3  :  GamMac → Denoise → Degrain → LumaLevels → Enfocar  ← ACTUAL
#
#  ¿Por qué este orden?
#  1. GamMac PRIMERO: neutraliza la dominante de color (p. ej. fuerte tono rosado)
#     antes de RemoveDirt. RemoveDirt opera en YV12; una dominante fuerte
#     sesga los componentes de chroma y enmascara las manchas → detección menos fiable.
#     Tras GamMac, las manchas tienen mejor contraste sobre un fondo neutro.
#  2. RemoveDirt DESPUÉS de GamMac: detecta mejor el polvo en una imagen equilibrada.
#  3. MDegrain DESPUÉS de RemoveDirt: vectores de MVTools2 más precisos sin polvo.
#  4. LumaLevels: ajustes finos sobre una imagen limpia.
#  5. Enfocar AL FINAL: no reamplifica el ruido/grano residual.
# =========================

c = src

# PASO 1 — GamMac PRIMERO (corrección RGB selectiva — neutraliza la dominante de color antes de RemoveDirt)
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

# PASO 2 — Polvo / manchas de película (RemoveDirt)
if (enable_denoise) {
  c = DenoiseFilm(c,
    \ mode     = denoise_mode,
    \ strength = denoise_strength,
    \ dist     = denoise_dist,
    \ grey     = denoise_grey,
    \ matrix   = src_matrix)
}

# PASO 3 — Grano de película (MVTools2 MDegrain)
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

# PASO 4 — Ajustes manuales (Tweak + ColorYUV)
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

# PASO 5 — Enfocar (SIEMPRE AL FINAL)
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
# VISTA PREVIA / SALIDA
# =========================

out = preview
    \ ? _BuildPreview(
    \     EnsureColorspaceSafe(src,   "YV24", matrix=src_matrix),
    \     EnsureColorspaceSafe(final, "YV24", matrix=src_matrix),
    \     label_size=preview_label_size)
    \ : final

# Eliminar relleno de bordes para filtros temporales (solo en secuencia de imágenes)
out = (n_img_pad > 0) ? Trim(out, n_img_pad, n_img_pad + n_img_orig - 1) : out

effective_threads = (enable_gammac) ? 1 : threads
out = out.Prefetch(effective_threads)
out
