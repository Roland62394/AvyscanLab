# =========================
# Restoration optimisée — AviSynth+ 64-bit  v2.0
# CORRECTIONS MAJEURES:
#   - Ordre du pipeline corrigé (RemoveDirt EN PREMIER)
#   - Degrain via MVTools2 (MDegrain2/3) beaucoup plus efficace
#   - Paramètres RemoveDirt corrigés (limit réduit, sans artefacts)
#   - Sharpen déplacé EN DERNIER (évite réamplification du bruit)
# =========================

# =========================
# START CONFIGURATION
# =========================

# ------------ SOURCE ------------
src = ""          # fichier vidéo (.avi/.mov/.mp4/...) OU pattern image "D:\img\scan_%06d.tif"

img_start   = 1
img_end     = 244
play_speed  = 24

use_img = true    # true = ImageSource ; false = vidéo

# AVI source mode
prefer_avi_vfw = true
force_source = ""  # "" = auto | "AVISOURCE" | "LWLIBAV" | "LSMASH" | "FFMS2" | "DSHOW"
show_source_used = true

# ------------ OPTIONS ------------
enable_flip_h = false
enable_flip_v = false

enable_crop          = true
enable_denoise       = true   # RemoveDirt/MC — ÉTAPE 1 (poussières pellicule)
enable_degrain       = true   # MVTools2 MDegrain — ÉTAPE 2 (grain/bruit)
enable_luma_levels   = true
enable_gammac        = true
enable_sharp         = true   # TOUJOURS EN DERNIER

preview  = true
threads  = 4

# ------------ PARAMS ------------
# Crop (no resizing)
Crop_L = 550
Crop_T = 500
Crop_R = 1000
Crop_B = 500

# =============================================================
# DENOISE — RemoveDirt/MC (poussières, taches, défauts pellicule)
# ORDRE : EN PREMIER avant tout autre traitement
# =============================================================
denoise_mode     = "removedirtmc"
#   "removedirtmc"  → RemoveDirtMC  (meilleur, compensé mouvement)
#   "removedirt"    → RemoveDirt    (plus rapide, sans compensation)

# Seuil de détection temporelle (différence pixel vs trames voisines)
# VALEURS RECOMMANDÉES selon le niveau de saleté de la pellicule:
#   Pellicule propre  : denoise_strength = 6
#   Pellicule moyenne : denoise_strength = 10   ← recommandé
#   Pellicule sale    : denoise_strength = 14   (max raisonnable)
#   JAMAIS au-dessus de 18-20, risque fort d'artefacts
denoise_strength = 10

# Rayon de recherche spatial pour la réparation (dist dans RestoreMotionBlocks)
# Défaut 3 — augmenter pour effacer des taches plus grandes (ex: 3→6→10)
# Valeur trop élevée = risque de reconstruire avec une texture incorrecte
denoise_dist     = 3

# true = traitement luma seul (plus rapide) ; false = luma+chroma (meilleure restitution couleur)
denoise_grey     = false

# =============================================================
# DEGRAIN — MVTools2 MDegrain (grain argentique, bruit capteur)
# ORDRE : APRÈS RemoveDirt, AVANT les corrections couleur
# CORRECTION: remplacement de TemporalSoften par MDegrain2/3
# =============================================================
degrain_mode     = "mdegrain2"
#   "mdegrain3" → MDegrain3 (meilleur, 3 référence temporelle)
#   "mdegrain2" → MDegrain2 (bon équilibre qualité/vitesse)  ← recommandé
#   "mdegrain1" → MDegrain1 (plus rapide, moins efficace)
#   "temporal"  → TemporalSoften (fallback sans MVTools2)

# Thresholds pour MDegrain (luma / chroma)
# Plus la valeur est élevée, plus le degrain est fort
#   Grain fin     : thSAD=200, thSADC=150
#   Grain moyen   : thSAD=350, thSADC=250  ← recommandé pellicule 8/16mm
#   Grain fort    : thSAD=500, thSADC=400
degrain_thSAD    = 350  # seuil luma  (SAD par bloc)
degrain_thSADC   = 250  # seuil chroma

# PERFORMANCE — impact sur la vitesse (du plus au moins impactant) :
#
#  pel — précision sub-pixel de MSuper :
#    pel=2 → ½ pixel, MSuper interne à 4x la résolution → LENT
#    pel=1 → pixel entier, ~2x plus rapide, perte imperceptible sur grain ← RECOMMANDÉ
#
#  blksize / overlap — taille des blocs d'analyse :
#    blksize=8,  overlap=4  → précis sur petit grain, LENT (4x plus de blocs)
#    blksize=16, overlap=8  → bon équilibre vitesse/qualité               ← RECOMMANDÉ
#    blksize=32, overlap=16 → très rapide, moins précis sur petit grain
#    (overlap doit toujours = blksize/2)
#
#  search — algorithme de recherche des vecteurs de mouvement :
#    search=3 → exhaustive, le plus lent mais le plus précis
#    search=2 → hexagone,   bon compromis vitesse/précision               ← RECOMMANDÉ
#    search=0 → oneway,     le plus rapide, moins précis
#
degrain_blksize  = 16   # RAPIDE: 8→16 (4x moins de blocs à analyser)
degrain_overlap  = 8    # RAPIDE: 4→8  (toujours blksize/2)
degrain_pel      = 1    # RAPIDE: 2→1  (~2x plus rapide, imperceptible sur grain)
degrain_search   = 2    # NOUVEAU: hexagone — bon équilibre vitesse/précision

# Préfiltrage pour l'analyse MVTools (ne touche pas le clip final)
degrain_prefilter = "remgrain"
#   "remgrain"  → RemoveGrain(2) — rapide et efficace  ← RECOMMANDÉ
#   "blur"      → Blur(1.0)     — alternatif
#   "none"      → pas de préfiltrage (moins précis)

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

# Sharpen (TOUJOURS EN DERNIER — après toutes les corrections)
Sharp_Mode      = "simple"   # "simple" | "edge"
Sharp_Strength  = 8          # échelle 1–20 (5=léger, 8=standard, 12=moyen, 16=fort, 20=très fort)
Sharp_Radius    = 1.5
Sharp_Threshold = 5          # augmenté: protège mieux contre le bruit résiduel

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

# MVTools2 — REQUIS pour MDegrain (télécharger depuis avisynth.nl)
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
# FILTRE 1 — DENOISE / REMOVEDIRT
# Rôle  : Supprimer poussières, taches, rayures pellicule
# Place : EN PREMIER — sur la source brute, avant toute manipulation
# CORRECTION v2 : limit corrigé (6-14 selon saleté), plus 24 qui causait artefacts
# =========================

function DenoiseFilm(clip c, string "mode", float "strength", int "dist", bool "grey", string "matrix")
{
  mode   = Default(mode,     "removedirtmc")
  lim    = int(Default(strength, 10.0))  # accepte int ou float, converti en int pour RemoveDirt
  dist   = Default(dist,     3)
  grey   = Default(grey,     false)
  matrix = Default(matrix,   _MatrixAuto(c))

  base_pix = PixelType(c)
  bd = BitsPerComponent(c)

  # RemoveDirt/MC opère en YV12 (requis)
  c8  = (bd == 8) ? c : ConvertBits(c, 8, dither=1)
  c8  = MakeEvenY(c8)
  yuv = EnsureColorspaceSafe(c8, "YV12", matrix=matrix)

  if (mode == "removedirtmc" && FunctionExists("RemoveDirtMC")) {
    result = RemoveDirtMC(yuv, limit=lim, _grey=grey, dist=dist)

  } else if (mode == "removedirt" && FunctionExists("RemoveDirt")) {
    result = RemoveDirt(yuv, limit=lim, grey=grey, dist=dist)

  } else {
    # Fallback : TemporalSoften ciblé (si RemoveDirt non disponible)
    result = TemporalSoften(yuv, 1, 4, 4, 12, 2)
  }

  # Retour au format d'origine
  result = (bd == 8) ? result : ConvertBits(result, bd)
  return (PixelType(result) == base_pix) ? result
       \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
}


# =========================
# FILTRE 2 — DEGRAIN (MVTools2 MDegrain)
# Rôle  : Réduire le grain argentique et le bruit général
# Place : APRÈS RemoveDirt, AVANT corrections couleur
# CORRECTION v2 : MDegrain2/3 remplace TemporalSoften (bien plus efficace)
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

  # --- Fallback si MVTools2 absent ---
  if (!FunctionExists("MSuper")) {
    # TemporalSoften conservateur comme fallback
    result = TemporalSoften(yuv, 2, 6, 5, 15, 2)
    result = (bd == 8) ? result : ConvertBits(result, bd)
    return (PixelType(result) == base_pix) ? result
         \                                  : EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # --- Préfiltrage pour l'analyse des vecteurs de mouvement ---
  # Le préfiltrage améliore la précision de MAnalyse SANS affecter le clip final
  prefilt = (prefilter == "remgrain") ? RemoveGrain(yuv, 2)
          \ : (prefilter == "blur")   ? Blur(yuv, 1.0)
          \                           : yuv

  # --- Analyse MVTools2 ---
  sup_orig = MSuper(yuv,    pel=pel, sharp=2)         # super pour compensation
  sup_pre  = MSuper(prefilt, pel=pel, sharp=2)           # super préfiltré pour analyse (levels auto)

  # Vecteurs avant/arrière (radius 1)
  bv1 = MAnalyse(sup_pre, isb=true,  delta=1, blksize=blksize, overlap=overlap, search=search)
  fv1 = MAnalyse(sup_pre, isb=false, delta=1, blksize=blksize, overlap=overlap, search=search)

  if (mode == "mdegrain1") {
    result = MDegrain1(yuv, sup_orig, bv1, fv1, thSAD=thSAD, thSADC=thSADC)

  } else if (mode == "mdegrain2") {
    # MDegrain2 : 2 références temporelles de chaque côté (recommandé)
    bv2 = MAnalyse(sup_pre, isb=true,  delta=2, blksize=blksize, overlap=overlap, search=search)
    fv2 = MAnalyse(sup_pre, isb=false, delta=2, blksize=blksize, overlap=overlap, search=search)
    result = MDegrain2(yuv, sup_orig, bv1, fv1, bv2, fv2, thSAD=thSAD, thSADC=thSADC)

  } else {
    # MDegrain3 : 3 références temporelles (meilleure qualité, plus lent)
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
# FILTRE 3 — SHARPEN (inchangé, toujours en dernier)
# =========================

function SharpenAdvanced(clip c, string "mode", float "strength", float "radius", int "threshold", string "matrix")
{
  mode      = Default(mode, "simple")
  strength  = Default(strength, 8)
  radius    = Default(radius, 1.5)
  threshold = Default(threshold, 5)
  matrix    = Default(matrix, _MatrixAuto(c))

  base_pix = PixelType(c)

  # Rayon de flou pour l'unsharp mask : radius contrôle l'étendue du halo de renforcement.
  # radius=1.0 → netteté fine et précise ; radius=3.0 → netteté large et prononcée.
  blur_r = Min((radius - 1.0) * 0.45 + 0.15, 1.5)

  if (mode == "edge") {
    yuv = EnsureColorspaceSafe(c, "YV24", matrix=matrix)

    # Renforcement via unsharp mask avec rayon variable
    blurred = yuv.Blur(blur_r)
    sharp = mt_lutxy(yuv, blurred, "x " + String(strength) + " x y - * + 0 max 255 min", U=1, V=1)

    # Masque dual-Sobel horizontal + vertical, puis compression gamma.
    # La courbe ^0.86 (inspirée de LimitedSharpenFaster/Didée) écrase naturellement
    # les faibles réponses (grain, texture fine) et préserve les vrais contours d'objets,
    # sans nécessiter de seuil arbitraire.
    edge_h    = yuv.mt_edge(thY1=0, thY2=255, "8 16 8 0 0 0 -8 -16 -8 4")
    edge_v    = yuv.mt_edge(thY1=0, thY2=255, "8 0 -8 16 0 -16 8 0 -8 4")
    edge_mask = mt_logic(edge_h, edge_v, "max") \
              .mt_lut("x 128 / 0.86 ^ 255 *", U=1, V=1)

    # Élargissement du masque pour couvrir la largeur complète des contours,
    # puis adoucissement pour des transitions graduelles (évite les bords nets du masque)
    edge_mask = edge_mask.mt_inflate().mt_inflate().Blur(1.0)

    # Seuil optionnel : exclure les réponses gamma résiduelles. Recommandé : 20–40.
    # Inutile si la compression gamma suffit (threshold=0 pour désactiver).
    edge_mask = (threshold > 0)
              \ ? edge_mask.mt_lut("x " + String(threshold) + " < 0 x ?", U=1, V=1)
              \ : edge_mask

    # Fusion : original sur zones plates, renforcé sur contours — chroma inchangée
    result = mt_merge(yuv, sharp, edge_mask, U=1, V=1)
    return EnsureColorspaceSafe(result, base_pix, matrix=matrix)
  }

  # "simple" : unsharp mask global avec rayon variable — efficace, légère amplification du grain possible
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

# Padding des bords pour les filtres temporels (évite les accès hors plage pour ImageSource)
# MVTools2 MDegrain2 (delta=2) et MDegrain3 (delta=3) demandent des frames avant/après la plage.
# Pour ImageSource, ces frames n'ont pas de fichier correspondant → crash.
# On préfixe/suffixe le clip avec des copies de la première/dernière frame, puis on retire le padding en sortie.
n_img_orig = 0
n_img_pad  = 0
if (use_img) {
  n_img_orig = FrameCount(src)
  n_img_pad  = 4   # couvre MDegrain3 (delta=3) + RemoveDirtMC (delta=1) avec marge
  src = Trim(src, 0, 0).Loop(n_img_pad) + src + Trim(src, n_img_orig - 1, n_img_orig - 1).Loop(n_img_pad)
}


# =========================
# PIPELINE — ORDRE CORRIGÉ v2
#
#  v1  :  Degrain → Denoise → Color → Sharpen
#  v2  :  Denoise → Degrain → GamMac → LumaLevels → Sharpen
#  v3  :  GamMac → Denoise → Degrain → LumaLevels → Sharpen  ← ACTUEL
#
#  Pourquoi cet ordre ?
#  1. GamMac EN PREMIER : neutralise la dominante couleur (ex: forte teinte rose)
#     avant RemoveDirt. RemoveDirt opère en YV12 ; une dominante forte biaise
#     les composantes chroma et masque les taches → détection moins fiable.
#     Après GamMac, les taches ont un meilleur contraste sur un fond neutre.
#  2. RemoveDirt APRÈS GamMac : détecte mieux les poussières sur image équilibrée.
#  3. MDegrain APRÈS RemoveDirt : vecteurs MVTools2 plus précis sans poussières.
#  4. LumaLevels : ajustements fins sur image propre.
#  5. Sharpen EN DERNIER : ne réamplifie pas le bruit/grain résiduel.
# =========================

c = src

# ÉTAPE 1 — GamMac EN PREMIER (correction RGB sélective — neutralise la dominante couleur avant RemoveDirt)
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

# ÉTAPE 2 — Poussières / taches pellicule (RemoveDirt)
if (enable_denoise) {
  c = DenoiseFilm(c,
    \ mode     = denoise_mode,
    \ strength = denoise_strength,
    \ dist     = denoise_dist,
    \ grey     = denoise_grey,
    \ matrix   = src_matrix)
}

# ÉTAPE 3 — Grain argentique (MVTools2 MDegrain)
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

# ÉTAPE 4 — Ajustements manuels (Tweak + ColorYUV)
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

# ÉTAPE 5 — Sharpen (TOUJOURS EN DERNIER)
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

# Retrait du padding ajouté pour les filtres temporels (image source uniquement)
out = (n_img_pad > 0) ? Trim(out, n_img_pad, n_img_pad + n_img_orig - 1) : out

out = out.Prefetch(threads)
out
