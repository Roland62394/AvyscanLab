using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using CleanScan.Services;
using CleanScan.ViewModels;

namespace CleanScan.Views
{
    public partial class MainWindow : Window
    {
        #region Constants

        private const string AppDataFolder         = "CleanScan";
        private const string WindowSettingsFileName = "window-settings.json";
        private const string PresetsFileName        = "presets.json";
        private const string EncodingPresetsFileName = "encoding_presets.json";
        private const string SessionFileName         = "session.json";

        /// <summary>Trial: max recording duration per clip in seconds. 0 = unlimited (full version).</summary>
        private const int TrialMaxSeconds = 30;
        private const string UseImageConfigName     = ScriptService.UseImageConfigName;

        #endregion

        #region Static data

        private static readonly string[] VideoExtensions = [".avi", ".mp4", ".mov", ".mkv", ".wmv", ".m4v", ".mpeg", ".mpg", ".webm"];
        private static readonly string[] ImageExtensions = [".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp"];

        private static readonly FilePickerFileType VideoFileType =
            new("Video") { Patterns = [.. VideoExtensions.Select(e => $"*{e}")] };
        private static readonly FilePickerFileType ImageFileType =
            new("Images") { Patterns = [.. ImageExtensions.Select(e => $"*{e}")] };

        private static readonly string[] SharpModeOptions    = ["simple", "edge"];
        private static readonly string[] SharpPresetOptions   = ["léger", "standard", "moyen", "fort", "très fort"];

        private static readonly Dictionary<string, Dictionary<string, string>> SharpPresets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["léger"]     = new(StringComparer.OrdinalIgnoreCase) {
                ["Sharp_Mode"] = "simple", ["Sharp_Strength"] = "5",  ["Sharp_Radius"] = "1.0", ["Sharp_Threshold"] = "0" },
            ["standard"]  = new(StringComparer.OrdinalIgnoreCase) {
                ["Sharp_Mode"] = "simple", ["Sharp_Strength"] = "8",  ["Sharp_Radius"] = "1.2", ["Sharp_Threshold"] = "0" },
            ["moyen"]     = new(StringComparer.OrdinalIgnoreCase) {
                ["Sharp_Mode"] = "edge",   ["Sharp_Strength"] = "12", ["Sharp_Radius"] = "1.5", ["Sharp_Threshold"] = "0" },
            ["fort"]      = new(StringComparer.OrdinalIgnoreCase) {
                ["Sharp_Mode"] = "edge",   ["Sharp_Strength"] = "16", ["Sharp_Radius"] = "1.8", ["Sharp_Threshold"] = "20" },
            ["très fort"] = new(StringComparer.OrdinalIgnoreCase) {
                ["Sharp_Mode"] = "edge",   ["Sharp_Strength"] = "20", ["Sharp_Radius"] = "2.5", ["Sharp_Threshold"] = "35" },
        };
        private static readonly string[] DenoiseModeOptions   = ["removedirtmc", "removedirt"];
        private static readonly string[] DenoisePresetOptions = ["faible", "standard", "moyen", "fort", "très fort"];

        private static readonly Dictionary<string, Dictionary<string, string>> DenoisePresets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["faible"]    = new(StringComparer.OrdinalIgnoreCase) {
                ["denoise_mode"] = "removedirtmc", ["denoise_strength"] = "4",  ["denoise_dist"] = "2" },
            ["standard"]  = new(StringComparer.OrdinalIgnoreCase) {
                ["denoise_mode"] = "removedirtmc", ["denoise_strength"] = "10", ["denoise_dist"] = "3" },
            ["moyen"]     = new(StringComparer.OrdinalIgnoreCase) {
                ["denoise_mode"] = "removedirtmc", ["denoise_strength"] = "14", ["denoise_dist"] = "4" },
            ["fort"]      = new(StringComparer.OrdinalIgnoreCase) {
                ["denoise_mode"] = "removedirtmc", ["denoise_strength"] = "18", ["denoise_dist"] = "6" },
            ["très fort"] = new(StringComparer.OrdinalIgnoreCase) {
                ["denoise_mode"] = "removedirtmc", ["denoise_strength"] = "24", ["denoise_dist"] = "10" },
        };
        private static readonly string[] DegrainModeOptions      = ["mdegrain2", "mdegrain3", "mdegrain1", "temporal"];
        private static readonly string[] DegrainPrefilterOptions = ["remgrain", "blur", "none"];
        private static readonly string[] DegrainPresetOptions    = ["faible", "standard", "moyen", "fort", "très fort"];

        private static readonly Dictionary<string, Dictionary<string, string>> DegrainPresets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["faible"] = new(StringComparer.OrdinalIgnoreCase) {
                ["degrain_mode"] = "mdegrain1", ["degrain_thSAD"] = "150",  ["degrain_thSADC"] = "100",
                ["degrain_blksize"] = "16", ["degrain_overlap"] = "8", ["degrain_pel"] = "1", ["degrain_search"] = "2", ["degrain_prefilter"] = "remgrain" },
            ["standard"] = new(StringComparer.OrdinalIgnoreCase) {
                ["degrain_mode"] = "mdegrain2", ["degrain_thSAD"] = "350",  ["degrain_thSADC"] = "250",
                ["degrain_blksize"] = "16", ["degrain_overlap"] = "8", ["degrain_pel"] = "1", ["degrain_search"] = "2", ["degrain_prefilter"] = "remgrain" },
            ["moyen"] = new(StringComparer.OrdinalIgnoreCase) {
                ["degrain_mode"] = "mdegrain2", ["degrain_thSAD"] = "550",  ["degrain_thSADC"] = "380",
                ["degrain_blksize"] = "16", ["degrain_overlap"] = "8", ["degrain_pel"] = "1", ["degrain_search"] = "2", ["degrain_prefilter"] = "remgrain" },
            ["fort"] = new(StringComparer.OrdinalIgnoreCase) {
                ["degrain_mode"] = "mdegrain3", ["degrain_thSAD"] = "750",  ["degrain_thSADC"] = "520",
                ["degrain_blksize"] = "16", ["degrain_overlap"] = "8", ["degrain_pel"] = "2", ["degrain_search"] = "2", ["degrain_prefilter"] = "remgrain" },
            ["très fort"] = new(StringComparer.OrdinalIgnoreCase) {
                ["degrain_mode"] = "mdegrain3", ["degrain_thSAD"] = "1000", ["degrain_thSADC"] = "700",
                ["degrain_blksize"] = "8",  ["degrain_overlap"] = "4", ["degrain_pel"] = "2", ["degrain_search"] = "3", ["degrain_prefilter"] = "remgrain" },
        };

        private static readonly Dictionary<string, string> FieldToFilterPresetCombo = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Sharp_Mode"] = "sharp_preset", ["Sharp_Strength"] = "sharp_preset",
            ["Sharp_Radius"] = "sharp_preset", ["Sharp_Threshold"] = "sharp_preset",
            ["denoise_mode"] = "denoise_preset", ["denoise_strength"] = "denoise_preset", ["denoise_dist"] = "denoise_preset",
            ["degrain_mode"] = "degrain_preset", ["degrain_thSAD"] = "degrain_preset", ["degrain_thSADC"] = "degrain_preset",
            ["degrain_blksize"] = "degrain_preset", ["degrain_overlap"] = "degrain_preset", ["degrain_pel"] = "degrain_preset",
            ["degrain_search"] = "degrain_preset", ["degrain_prefilter"] = "degrain_preset",
        };

        private static readonly Dictionary<string, string> OptionButtonLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["preview"]           = "Preview",
            ["enable_flip_h"]     = "Flip h",
            ["enable_flip_v"]     = "Flip v",
            ["enable_crop"]       = "Crop",
            ["enable_degrain"]    = "Degrain",
            ["enable_denoise"]    = "Denoise",
            ["denoise_grey"]      = "grey",
            ["enable_luma_levels"] = "Luma levels",
            ["enable_gammac"]     = "Gammac",
            ["enable_sharp"]      = "Sharp",
            ["ShowPreview"]       = "Show"
        };

        private static readonly Dictionary<string, Dictionary<string, string>> ParamTooltipTexts =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["Label_Crop_L"] = new() {
                ["fr"] = "Pixels à rogner à gauche.",
                ["en"] = "Pixels to crop on the left.",
                ["de"] = "Pixel links abschneiden.",
                ["es"] = "Píxeles a recortar a la izquierda." },
            ["Label_Crop_T"] = new() {
                ["fr"] = "Pixels à rogner en haut.",
                ["en"] = "Pixels to crop at the top.",
                ["de"] = "Pixel oben abschneiden.",
                ["es"] = "Píxeles a recortar arriba." },
            ["Label_Crop_R"] = new() {
                ["fr"] = "Pixels à rogner à droite.",
                ["en"] = "Pixels to crop on the right.",
                ["de"] = "Pixel rechts abschneiden.",
                ["es"] = "Píxeles a recortar a la derecha." },
            ["Label_Crop_B"] = new() {
                ["fr"] = "Pixels à rogner en bas.",
                ["en"] = "Pixels to crop at the bottom.",
                ["de"] = "Pixel unten abschneiden.",
                ["es"] = "Píxeles a recortar abajo." },

            ["Label_degrain_mode"] = new() {
                ["fr"] = "Algorithme MDegrain. mdegrain2 = 2 références temporelles (recommandé). mdegrain3 = 3 références (meilleur/lent). mdegrain1 = 1 référence (rapide). temporal = TemporalSoften (sans MVTools2).",
                ["en"] = "MDegrain algorithm. mdegrain2 = 2 temporal references (recommended). mdegrain3 = 3 references (best/slow). mdegrain1 = 1 reference (fast). temporal = TemporalSoften (no MVTools2).",
                ["de"] = "MDegrain-Algorithmus. mdegrain2 = 2 zeitliche Referenzen (empfohlen). mdegrain3 = 3 Referenzen (beste/langsam). mdegrain1 = 1 Referenz (schnell). temporal = TemporalSoften (ohne MVTools2).",
                ["es"] = "Algoritmo MDegrain. mdegrain2 = 2 referencias temporales (recomendado). mdegrain3 = 3 referencias (mejor/lento). mdegrain1 = 1 referencia (rápido). temporal = TemporalSoften (sin MVTools2)." },
            ["Label_degrain_thSAD"] = new() {
                ["fr"] = "Seuil SAD luma — intensité du degrain sur la luminance. Grain fin : 200. Moyen : 350 (défaut). Fort : 500. Plus la valeur est haute, plus le degrain est fort.",
                ["en"] = "Luma SAD threshold — degrain strength on luminance. Fine grain: 200. Medium: 350 (default). Heavy: 500. Higher value = stronger degrain.",
                ["de"] = "Luma-SAD-Schwellwert — Kornreduktionsstärke auf Luminanz. Feinkorn: 200. Mittel: 350 (Standard). Stark: 500. Höherer Wert = stärkere Kornreduktion.",
                ["es"] = "Umbral SAD luma — intensidad del degrain en luminancia. Grano fino: 200. Medio: 350 (predeterminado). Fuerte: 500. Mayor valor = degrain más fuerte." },
            ["Label_degrain_thSADC"] = new() {
                ["fr"] = "Seuil SAD chroma — intensité du degrain sur les couleurs. Grain fin : 150. Moyen : 250 (défaut). Fort : 400. Généralement 2/3 de thSAD.",
                ["en"] = "Chroma SAD threshold — degrain strength on color channels. Fine grain: 150. Medium: 250 (default). Heavy: 400. Typically 2/3 of thSAD.",
                ["de"] = "Chroma-SAD-Schwellwert — Kornreduktionsstärke auf Farbkanälen. Feinkorn: 150. Mittel: 250 (Standard). Stark: 400. Typischerweise 2/3 von thSAD.",
                ["es"] = "Umbral SAD chroma — intensidad del degrain en canales de color. Grano fino: 150. Medio: 250 (predeterminado). Fuerte: 400. Generalmente 2/3 de thSAD." },
            ["Label_degrain_blksize"] = new() {
                ["fr"] = "Taille des blocs d'analyse MVTools (pixels). 8 = précis/lent. 16 = équilibré (défaut). 32 = rapide/moins précis. overlap doit toujours être blksize/2.",
                ["en"] = "MVTools analysis block size (pixels). 8 = precise/slow. 16 = balanced (default). 32 = fast/less precise. overlap must always be blksize/2.",
                ["de"] = "MVTools-Analyseblockgröße (Pixel). 8 = präzise/langsam. 16 = ausgewogen (Standard). 32 = schnell/weniger präzise. overlap muss immer blksize/2 sein.",
                ["es"] = "Tamaño de bloque de análisis MVTools (píxeles). 8 = preciso/lento. 16 = equilibrado (predeterminado). 32 = rápido/menos preciso. overlap debe ser siempre blksize/2." },
            ["Label_degrain_overlap"] = new() {
                ["fr"] = "Chevauchement des blocs d'analyse (pixels). Doit toujours être égal à blksize/2. Ex : blksize=16 → overlap=8. blksize=8 → overlap=4. Défaut : 8.",
                ["en"] = "Analysis block overlap (pixels). Must always equal blksize/2. E.g.: blksize=16 → overlap=8. blksize=8 → overlap=4. Default: 8.",
                ["de"] = "Analyseblock-Überlappung (Pixel). Muss immer blksize/2 entsprechen. Bsp.: blksize=16 → overlap=8. blksize=8 → overlap=4. Standard: 8.",
                ["es"] = "Superposición de bloques de análisis (píxeles). Debe ser siempre blksize/2. Ej.: blksize=16 → overlap=8. blksize=8 → overlap=4. Predeterminado: 8." },
            ["Label_degrain_pel"] = new() {
                ["fr"] = "Précision sub-pixel de MSuper. 1 = pixel entier (~2× plus rapide, recommandé pour du grain). 2 = demi-pixel (plus précis, mais MSuper 4× plus lourd). Défaut : 1.",
                ["en"] = "MSuper sub-pixel precision. 1 = full pixel (~2× faster, recommended for grain). 2 = half pixel (more precise, but MSuper 4× heavier). Default: 1.",
                ["de"] = "MSuper-Subpixel-Präzision. 1 = ganzes Pixel (~2× schneller, empfohlen für Korn). 2 = halbes Pixel (präziser, aber MSuper 4× schwerer). Standard: 1.",
                ["es"] = "Precisión sub-pixel de MSuper. 1 = pixel entero (~2× más rápido, recomendado para grano). 2 = medio pixel (más preciso, pero MSuper 4× más pesado). Predeterminado: 1." },
            ["Label_degrain_search"] = new() {
                ["fr"] = "Algorithme de recherche des vecteurs de mouvement MVTools. 0 = oneway (rapide). 2 = hexagone (équilibré, recommandé). 3 = exhaustif (précis/lent). Défaut : 2.",
                ["en"] = "MVTools motion vector search algorithm. 0 = oneway (fast). 2 = hexagonal (balanced, recommended). 3 = exhaustive (precise/slow). Default: 2.",
                ["de"] = "MVTools-Bewegungsvektor-Suchalgorithmus. 0 = oneway (schnell). 2 = hexagonal (ausgewogen, empfohlen). 3 = exhaustiv (präzise/langsam). Standard: 2.",
                ["es"] = "Algoritmo de búsqueda de vectores de movimiento MVTools. 0 = oneway (rápido). 2 = hexagonal (equilibrado, recomendado). 3 = exhaustivo (preciso/lento). Predeterminado: 2." },
            ["Label_degrain_prefilter"] = new() {
                ["fr"] = "Lissage appliqué à une copie temporaire du clip pour aider MVTools à estimer les mouvements. Le degrain s'applique toujours au clip original — ce réglage améliore la précision des vecteurs de mouvement et donc l'efficacité du degrain, sans filtrer directement l'image de sortie. remgrain = RemoveGrain(2) rapide (recommandé). blur = Blur(1.0). none = aucun.",
                ["en"] = "Smoothing applied to a temporary copy of the clip to help MVTools estimate motion. Degrain is always applied to the original clip — this setting improves motion vector accuracy and thus degrain effectiveness, without directly filtering the output image. remgrain = RemoveGrain(2) fast (recommended). blur = Blur(1.0). none = none.",
                ["de"] = "Glättung auf eine temporäre Clip-Kopie angewendet, um MVTools bei der Bewegungsschätzung zu helfen. Degrain wird immer auf den Originalclip angewendet — diese Einstellung verbessert die Bewegungsvektor-Genauigkeit und damit die Degrain-Effektivität, ohne das Ausgabebild direkt zu filtern. remgrain = RemoveGrain(2) schnell (empfohlen). blur = Blur(1.0). none = kein.",
                ["es"] = "Suavizado aplicado a una copia temporal del clip para ayudar a MVTools a estimar el movimiento. El degrain siempre se aplica al clip original — este ajuste mejora la precisión de los vectores de movimiento y por tanto la eficacia del degrain, sin filtrar directamente la imagen de salida. remgrain = RemoveGrain(2) rápido (recomendado). blur = Blur(1.0). none = ninguno." },
            ["Label_degrain_preset"] = new() {
                ["fr"] = "Réglage rapide de l'intensité du degrain. Applique automatiquement des valeurs cohérentes sur tous les paramètres. Vous pouvez ensuite affiner manuellement chaque valeur. faible=grain léger/rapide. standard=pellicule 8–16mm (défaut). moyen=grain marqué. fort=grain dense. très fort=grain très prononcé/lent.",
                ["en"] = "Quick degrain intensity preset. Automatically applies consistent values across all parameters. You can then fine-tune each value manually. faible=light grain/fast. standard=8–16mm film (default). moyen=noticeable grain. fort=dense grain. très fort=very heavy grain/slow.",
                ["de"] = "Schnell-Voreinstellung für die Degrain-Intensität. Setzt automatisch konsistente Werte für alle Parameter. Anschließend kann jeder Wert manuell feinabgestimmt werden. faible=leichtes Korn/schnell. standard=8–16mm-Film (Standard). moyen=deutliches Korn. fort=dichtes Korn. très fort=sehr starkes Korn/langsam.",
                ["es"] = "Ajuste rápido de intensidad del degrain. Aplica automáticamente valores coherentes en todos los parámetros. Puede afinar manualmente cada valor después. faible=grano leve/rápido. standard=película 8–16mm (predeterminado). moyen=grano notable. fort=grano denso. très fort=grano muy pronunciado/lento." },

            ["Label_denoise_mode"] = new() {
                ["fr"] = "Algorithme de suppression des poussières. removedirtmc = avec compensation de mouvement (recommandé) | removedirt = plus rapide.",
                ["en"] = "Dust removal algorithm. removedirtmc = motion-compensated (recommended) | removedirt = faster.",
                ["de"] = "Staubentfernungs-Algorithmus. removedirtmc = bewegungskompensiert (empfohlen) | removedirt = schneller.",
                ["es"] = "Algoritmo de eliminación de polvo. removedirtmc = con compensación de movimiento (recomendado) | removedirt = más rápido." },
            ["Label_denoise_strength"] = new() {
                ["fr"] = "Seuil de détection temporelle des poussières. Pellicule propre : 6, moyenne : 10, sale : 14. Défaut : 10. Max conseillé : 18.",
                ["en"] = "Temporal dust detection threshold. Clean film: 6, average: 10, dirty: 14. Default: 10. Max advised: 18.",
                ["de"] = "Zeitlicher Stauberkennungsschwellwert. Sauberer Film: 6, mittel: 10, verschmutzt: 14. Standard: 10. Max empfohlen: 18.",
                ["es"] = "Umbral de detección temporal de polvo. Película limpia: 6, media: 10, sucia: 14. Predeterminado: 10. Máx. aconsejado: 18." },
            ["Label_denoise_dist"] = new() {
                ["fr"] = "Rayon de recherche spatial pour la réparation. Augmenter pour les taches plus grandes (3→6→10). Défaut : 3. Min : 1.",
                ["en"] = "Spatial search radius for repair. Increase for larger spots (3→6→10). Default: 3. Min: 1.",
                ["de"] = "Räumlicher Suchradius für Reparatur. Erhöhen für größere Flecken (3→6→10). Standard: 3. Min: 1.",
                ["es"] = "Radio de búsqueda espacial para reparación. Aumentar para manchas más grandes (3→6→10). Predeterminado: 3. Mín: 1." },
            ["Label_denoise_grey"] = new() {
                ["fr"] = "true = traitement luma seul (plus rapide). false = luma + chroma (meilleure restitution couleur). Défaut : false.",
                ["en"] = "true = luma only (faster). false = luma + chroma (better color accuracy). Default: false.",
                ["de"] = "true = nur Luma (schneller). false = Luma + Chroma (bessere Farbgenauigkeit). Standard: false.",
                ["es"] = "true = solo luma (más rápido). false = luma + chroma (mejor precisión de color). Predeterminado: false." },
            ["Label_denoise_preset"] = new() {
                ["fr"] = "Réglage rapide de l'intensité de la suppression des poussières. Applique automatiquement des valeurs cohérentes sur mode, strength et dist. faible=poussière légère. standard=pellicule moyenne (défaut). moyen=pellicule sale. fort=très sale/grandes taches. très fort=cas extrêmes.",
                ["en"] = "Quick dust removal intensity preset. Automatically applies consistent values for mode, strength and dist. faible=light dust. standard=average film (default). moyen=dirty film. fort=very dirty/large spots. très fort=extreme cases.",
                ["de"] = "Schnell-Voreinstellung für die Staubentfernungsintensität. Setzt automatisch konsistente Werte für mode, strength und dist. faible=leichter Staub. standard=durchschnittlicher Film (Standard). moyen=schmutziger Film. fort=sehr schmutzig/große Flecken. très fort=extreme Fälle.",
                ["es"] = "Ajuste rápido de intensidad de eliminación de polvo. Aplica automáticamente valores coherentes para mode, strength y dist. faible=polvo leve. standard=película media (predeterminado). moyen=película sucia. fort=muy sucia/manchas grandes. très fort=casos extremos." },

            ["Label_Lum_Bright"] = new() {
                ["fr"] = "Luminosité (offset additif). Défaut : 0. Plage : -255..255. Recommandé : -30..30.",
                ["en"] = "Brightness (additive offset). Default: 0. Range: -255..255. Recommended: -30..30.",
                ["de"] = "Helligkeit (additiver Versatz). Standard: 0. Bereich: -255..255. Empfohlen: -30..30.",
                ["es"] = "Brillo (desplazamiento aditivo). Predeterminado: 0. Rango: -255..255. Recomendado: -30..30." },
            ["Label_Lum_Contrast"] = new() {
                ["fr"] = "Contraste (facteur multiplicatif). Défaut : 1.05. Min > 0. Courant : 0.5..2.0.",
                ["en"] = "Contrast (multiplicative factor). Default: 1.05. Min > 0. Typical: 0.5..2.0.",
                ["de"] = "Kontrast (multiplikativer Faktor). Standard: 1,05. Min > 0. Typisch: 0,5..2,0.",
                ["es"] = "Contraste (factor multiplicativo). Predeterminado: 1,05. Mín > 0. Típico: 0,5..2,0." },
            ["Label_Lum_Sat"] = new() {
                ["fr"] = "Saturation des couleurs. Neutre : 1.0. Défaut : 1.10. Min > 0. Courant : 0.5..2.0.",
                ["en"] = "Color saturation. Neutral: 1.0. Default: 1.10. Min > 0. Typical: 0.5..2.0.",
                ["de"] = "Farbsättigung. Neutral: 1,0. Standard: 1,10. Min > 0. Typisch: 0,5..2,0.",
                ["es"] = "Saturación de color. Neutro: 1,0. Predeterminado: 1,10. Mín > 0. Típico: 0,5..2,0." },
            ["Label_Lum_Hue"] = new() {
                ["fr"] = "Décalage de teinte en degrés. Défaut : 0.0. Courant : -180..180. Petits pas conseillés : ±2..5.",
                ["en"] = "Hue shift in degrees. Default: 0.0. Typical: -180..180. Small steps recommended: ±2..5.",
                ["de"] = "Farbtonversatz in Grad. Standard: 0,0. Typisch: -180..180. Kleine Schritte empfohlen: ±2..5.",
                ["es"] = "Desplazamiento de tono en grados. Predeterminado: 0,0. Típico: -180..180. Pasos pequeños recomendados: ±2..5." },
            ["Label_Lum_GammaY"] = new() {
                ["fr"] = "Gamma appliqué à la luminance. Défaut : 1.30. Min > 0. Courant : 0.5..3.0. >1 = éclaircit.",
                ["en"] = "Gamma applied to luminance. Default: 1.30. Min > 0. Typical: 0.5..3.0. >1 = brightens.",
                ["de"] = "Auf Luminanz angewendetes Gamma. Standard: 1,30. Min > 0. Typisch: 0,5..3,0. >1 = heller.",
                ["es"] = "Gamma aplicado a la luminancia. Predeterminado: 1,30. Mín > 0. Típico: 0,5..3,0. >1 = aclara." },

            ["Label_LockChan"] = new() {
                ["fr"] = "Canal verrouillé (inchangé) lors de la correction GamMac.\n0 = verrouiller Rouge (R)\n1 = verrouiller Vert (G) — défaut : R et B sont ajustés pour rejoindre G\n2 = verrouiller Bleu (B)\n-1 = valeur explicite LockVal utilisée comme cible\n-2 = moyenne des 3 canaux (R+G+B, \"scaled average\")\n-3 = canal médian : GamMac choisit celui dont la moyenne est entre les deux autres, puis agit comme 0, 1 ou 2",
                ["en"] = "Channel locked (unchanged) during GamMac correction.\n0 = lock Red (R)\n1 = lock Green (G) — default: R and B are adjusted to match G\n2 = lock Blue (B)\n-1 = use explicit LockVal as target\n-2 = average of 3 channels (R+G+B, \"scaled average\")\n-3 = median channel: GamMac picks the one whose average is between the other two, then acts as 0, 1 or 2",
                ["de"] = "Gesperrter (unveränderter) Kanal bei der GamMac-Korrektur.\n0 = Rot (R) sperren\n1 = Grün (G) sperren — Standard: R und B werden an G angepasst\n2 = Blau (B) sperren\n-1 = expliziten LockVal-Wert als Ziel verwenden\n-2 = Durchschnitt der 3 Kanäle (R+G+B, \"scaled average\")\n-3 = Median-Kanal: GamMac wählt den Kanal, dessen Mittelwert zwischen den anderen liegt, und verhält sich wie 0, 1 oder 2",
                ["es"] = "Canal bloqueado (sin cambios) durante la corrección GamMac.\n0 = bloquear Rojo (R)\n1 = bloquear Verde (G) — predeterminado: R y B se ajustan para coincidir con G\n2 = bloquear Azul (B)\n-1 = usar el valor explícito LockVal como objetivo\n-2 = promedio de los 3 canales (R+G+B, \"scaled average\")\n-3 = canal mediano: GamMac elige el canal cuyo promedio está entre los otros dos y actúa como 0, 1 o 2" },
            ["Label_LockVal"] = new() {
                ["fr"] = "Valeur cible du canal de référence. Défaut : 250. Plage : 0..255.",
                ["en"] = "Target value for the reference channel. Default: 250. Range: 0..255.",
                ["de"] = "Zielwert für den Referenzkanal. Standard: 250. Bereich: 0..255.",
                ["es"] = "Valor objetivo del canal de referencia. Predeterminado: 250. Rango: 0..255." },
            ["Label_Scale"] = new() {
                ["fr"] = "Facteur d'amplitude de la correction colorimétrique. Défaut : 2. Min : 0. Courant : 0..10.",
                ["en"] = "Amplitude factor of the color correction. Default: 2. Min: 0. Typical: 0..10.",
                ["de"] = "Amplitudenfaktor der Farbkorrektur. Standard: 2. Min: 0. Typisch: 0..10.",
                ["es"] = "Factor de amplitud de la corrección de color. Predeterminado: 2. Mín: 0. Típico: 0..10." },
            ["Label_Th"] = new() {
                ["fr"] = "Seuil bas de détection des zones à corriger. Défaut : 0.12. Plage : 0..1.",
                ["en"] = "Low detection threshold for areas to correct. Default: 0.12. Range: 0..1.",
                ["de"] = "Unterer Erkennungsschwellwert für zu korrigierende Bereiche. Standard: 0,12. Bereich: 0..1.",
                ["es"] = "Umbral bajo de detección de zonas a corregir. Predeterminado: 0,12. Rango: 0..1." },
            ["Label_HiTh"] = new() {
                ["fr"] = "Seuil haut de détection des zones à corriger. Défaut : 0.25. Plage : 0..1.",
                ["en"] = "High detection threshold for areas to correct. Default: 0.25. Range: 0..1.",
                ["de"] = "Oberer Erkennungsschwellwert für zu korrigierende Bereiche. Standard: 0,25. Bereich: 0..1.",
                ["es"] = "Umbral alto de detección de zonas a corregir. Predeterminado: 0,25. Rango: 0..1." },
            ["Label_X"] = new() {
                ["fr"] = "Colonne gauche de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
                ["en"] = "Left column of the analysis region (0 = whole image). Default: 0. Min: 0.",
                ["de"] = "Linke Spalte des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
                ["es"] = "Columna izquierda de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0." },
            ["Label_Y"] = new() {
                ["fr"] = "Ligne haute de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
                ["en"] = "Top row of the analysis region (0 = whole image). Default: 0. Min: 0.",
                ["de"] = "Oberste Zeile des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
                ["es"] = "Fila superior de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0." },
            ["Label_W"] = new() {
                ["fr"] = "Largeur de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
                ["en"] = "Width of the analysis region (0 = whole image). Default: 0. Min: 0.",
                ["de"] = "Breite des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
                ["es"] = "Ancho de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0." },
            ["Label_H"] = new() {
                ["fr"] = "Hauteur de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
                ["en"] = "Height of the analysis region (0 = whole image). Default: 0. Min: 0.",
                ["de"] = "Höhe des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
                ["es"] = "Altura de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0." },
            ["Label_Omin"] = new() {
                ["fr"] = "Valeur minimale de sortie (écrêtage bas). Défaut : 0. Plage : 0..255.",
                ["en"] = "Minimum output value (low clipping). Default: 0. Range: 0..255.",
                ["de"] = "Minimaler Ausgabewert (Unterabschneidung). Standard: 0. Bereich: 0..255.",
                ["es"] = "Valor mínimo de salida (recorte bajo). Predeterminado: 0. Rango: 0..255." },
            ["Label_Omax"] = new() {
                ["fr"] = "Valeur maximale de sortie (écrêtage haut). Défaut : 255. Plage : 0..255.",
                ["en"] = "Maximum output value (high clipping). Default: 255. Range: 0..255.",
                ["de"] = "Maximaler Ausgabewert (Oberabschneidung). Standard: 255. Bereich: 0..255.",
                ["es"] = "Valor máximo de salida (recorte alto). Predeterminado: 255. Rango: 0..255." },
            ["Label_ShowPreview"] = new() {
                ["fr"] = "Affiche la région d'analyse en surimpression dans la prévisualisation. Défaut : false.",
                ["en"] = "Shows the analysis region as an overlay in the preview. Default: false.",
                ["de"] = "Zeigt den Analysebereich als Überlagerung in der Vorschau. Standard: false.",
                ["es"] = "Muestra la región de análisis como superposición en la vista previa. Predeterminado: false." },
            ["Label_Verbosity"] = new() {
                ["fr"] = "Niveau de détail du log GamMac. Défaut : 4. Plage : 0..6.",
                ["en"] = "GamMac log detail level. Default: 4. Range: 0..6.",
                ["de"] = "GamMac-Protokolldetailstufe. Standard: 4. Bereich: 0..6.",
                ["es"] = "Nivel de detalle del log GamMac. Predeterminado: 4. Rango: 0..6." },

            ["Label_Sharp_Strength"] = new() {
                ["fr"] = "Intensité du renforcement (échelle 1–20). simple : recommandé 5–10. edge : peut être plus élevé (10–20) car le masque protège les zones plates. Défaut : 8.",
                ["en"] = "Sharpening strength (scale 1–20). simple: recommended 5–10. edge: can be higher (10–20) since the mask protects flat areas. Default: 8.",
                ["de"] = "Schärfungsstärke (Skala 1–20). simple: empfohlen 5–10. edge: kann höher sein (10–20), da die Maske flache Bereiche schützt. Standard: 8.",
                ["es"] = "Intensidad del enfoque (escala 1–20). simple: recomendado 5–10. edge: puede ser mayor (10–20) ya que la máscara protege las zonas planas. Predeterminado: 8." },
            ["Label_Sharp_Mode"] = new() {
                ["fr"] = "simple = Sharpen() natif global, efficace, légère amplification du grain possible sur les zones plates. edge = renforcement uniquement sur les contours d'objets via masque dual-Sobel + compression gamma (technique LimitedSharpenFaster/Didée) — le grain dans les zones plates est naturellement exclu.",
                ["en"] = "simple = global native Sharpen(), effective, slight grain amplification possible on flat areas. edge = sharpening only on object contours via dual-Sobel mask + gamma compression (LimitedSharpenFaster/Didée technique) — grain in flat areas is naturally excluded.",
                ["de"] = "simple = globales natives Sharpen(), effektiv, leichte Kornverstärkung auf flachen Flächen möglich. edge = Schärfung nur an Objektkanten via Doppel-Sobel-Maske + Gammakompression (LimitedSharpenFaster/Didée-Technik) — Korn in flachen Bereichen wird natürlich ausgeschlossen.",
                ["es"] = "simple = Sharpen() global nativo, efectivo, posible leve amplificación del grano en zonas planas. edge = enfoque solo en contornos de objetos mediante máscara Sobel dual + compresión gamma (técnica LimitedSharpenFaster/Didée) — el grano en zonas planas queda excluido naturalmente." },
            ["Label_Sharp_Radius"] = new() {
                ["fr"] = "Rayon de l'unsharp mask : contrôle l'étendue du halo de renforcement. 1.0 = netteté fine et précise (grain peu affecté). 2.0–3.0 = netteté large et prononcée (plus visible, mais peut amplifier le grain). Recommandé : 1.0–2.0. Défaut : 1.5.",
                ["en"] = "Unsharp mask radius: controls the extent of the sharpening halo. 1.0 = tight, precise sharpening (grain barely affected). 2.0–3.0 = broad, pronounced sharpening (more visible, but may amplify grain). Recommended: 1.0–2.0. Default: 1.5.",
                ["de"] = "Radius der Unsharp Mask: steuert die Breite des Schärfungshalos. 1.0 = feine, präzise Schärfung (Korn kaum beeinflusst). 2.0–3.0 = breite, ausgeprägte Schärfung (sichtbarer, kann Korn verstärken). Empfohlen: 1.0–2.0. Standard: 1,5.",
                ["es"] = "Radio de la Unsharp Mask: controla la amplitud del halo de enfoque. 1.0 = enfoque fino y preciso (grano apenas afectado). 2.0–3.0 = enfoque amplio y pronunciado (más visible, puede amplificar el grano). Recomendado: 1.0–2.0. Predeterminado: 1,5." },
            ["Label_Sharp_Threshold"] = new() {
                ["fr"] = "Mode edge uniquement : seuil de réponse minimal du masque (après compression gamma) pour déclencher le renforcement. La compression gamma seule suffit souvent — utiliser threshold=0 pour la désactiver. Recommandé si du grain résiduel persiste : 20–40. Sans effet en mode simple.",
                ["en"] = "Edge mode only: minimum mask response threshold (after gamma compression) to trigger sharpening. Gamma compression alone is often sufficient — use threshold=0 to disable. Recommended if residual grain persists: 20–40. No effect in simple mode.",
                ["de"] = "Nur Edge-Modus: minimaler Maskenschwellwert (nach Gammakompression) zum Auslösen der Schärfung. Gammakompression allein reicht oft aus — threshold=0 zum Deaktivieren. Empfohlen bei verbleibendem Korn: 20–40. Keine Wirkung im Simple-Modus.",
                ["es"] = "Solo modo edge: umbral mínimo de respuesta de la máscara (después de la compresión gamma) para activar el enfoque. La compresión gamma sola suele ser suficiente — threshold=0 para desactivarla. Recomendado si persiste grano residual: 20–40. Sin efecto en modo simple." },
        };

        private readonly record struct SliderSpec(
            string Field, double Min, double Max, double SmallChange, bool IsFloat, int Decimals = 0);

        private static readonly SliderSpec[] SliderSpecs =
        [
            new("Crop_L",            0,    500,  1,    false),
            new("Crop_T",            0,    500,  1,    false),
            new("Crop_R",            0,    500,  1,    false),
            new("Crop_B",            0,    500,  1,    false),
            new("degrain_thSAD",     0,    1000, 10,   false),
            new("degrain_thSADC",    0,    1000, 10,   false),
            new("degrain_blksize",   4,    64,   4,    false),
            new("degrain_overlap",   0,    32,   1,    false),
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

        [GeneratedRegex(@"^\d+$")]
        private static partial Regex NumericStemRegex();

        private enum UpdateMode { Debounced, OnLostFocus, OnEnter, Immediate }
        private sealed record FieldSpec(string Name, UpdateMode Mode, bool ValidateOnChange);

        private static readonly FieldSpec[] FieldSpecs =
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

        #endregion

        #region Instance state

        private readonly ConfigStore          _config;
        private readonly SourceService        _sourceService;
        private readonly IScriptService       _scriptService;
        private readonly IPresetService       _presetService;
        private readonly PresetService        _encodingPresetService;
        private readonly IWindowStateService  _windowStateService;
        private readonly IDialogService       _dialogService;
        private readonly IAviService          _aviService;
        private readonly SessionService      _sessionService;
        private readonly Debouncer            _refreshDebouncer = new(TimeSpan.FromMilliseconds(400));
        private readonly Debouncer            _windowStateDebouncer = new(TimeSpan.FromMilliseconds(120));
        private readonly SemaphoreSlim        _refreshGate      = new(1, 1);

        private MpvService? _mpvService;
        private bool        _seekDragging;
        private double      _seekDuration;
        private int         _totalFrames;
        private double      _fps;
        private double      _pendingSeekPos;

        private bool  _suppressTextEvents;
        private bool  _sliderSync;
        private bool  _loadingSourceFallback;

        private readonly Dictionary<string, (Slider Slider, SliderSpec Spec)> _sliderMap = [];
        private bool  _isClosing;
        private bool  _isInitializing;
        private bool  _layoutInitialized;
        private bool  _sourceValidationErrorVisible;
        private Grid? _mainGrid;

        private readonly List<string> _clipPaths = new();
        private readonly List<Dictionary<string, string>> _clipConfigs = new();
        private readonly List<string?> _clipPresetNames = new();
        private bool _applyingPreset;
        private int _activeClipIndex = -1;

        // Batch encoding state
        private readonly List<bool> _clipBatchSelected = new();
        private readonly List<string?> _clipBatchEncodingPreset = new();

        // Encoding preset auto-save
        private bool _autoSaveEncodingPreset;
        private bool _isLoadingEncodingPreset;
        private bool _pendingEncodingPresetPrompt;


        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

        #endregion

        #region Constructor & lifecycle

        public MainWindow()
        {
            _config             = new ConfigStore();
            _sourceService      = new SourceService();
            _aviService         = new AviService();
            _scriptService      = new ScriptService(_sourceService);
            _presetService      = new PresetService(GetAppDataPath(PresetsFileName));
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _windowStateService = new WindowStateService(GetAppDataPath(WindowSettingsFileName));
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _dialogService      = new DialogService();

            InitializeWindow();
        }

        public MainWindow(
            ConfigStore         config,
            SourceService       sourceService,
            IScriptService      scriptService,
            IPresetService      presetService,
            IWindowStateService windowStateService,
            IDialogService      dialogService,
            IAviService         aviService)
        {
            _config             = config;
            _sourceService      = sourceService;
            _scriptService      = scriptService;
            _presetService      = presetService;
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _windowStateService = windowStateService;
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _dialogService      = dialogService;
            _aviService         = aviService;

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            EnsureAviSynthAvailable();
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            ConfigureMenuBar();
            PreApplyWindowPosition();
            Opened  += OnOpened;
            Closing += OnClosing;
            PositionChanged += OnPositionChanged;
            SizeChanged     += OnWindowSizeChanged;
            BottomPanel.SizeChanged += OnBottomPanelSizeChanged;
            InitializeChoiceFields();
            UpdateOptionColumnVisibility();
            RegisterChangeHandlers();
            InitSliders();
            InitRecordPanel();
            InitPlayerControls();
            RefreshClipPresetCombo();
        }

        private void InitPlayerControls()
        {
            DebugLog("InitPlayerControls start");
            _mpvService = new MpvService();

            if (this.FindControl<MpvHost>("VideoHost") is { } host)
            {
                DebugLog("VideoHost found");
                host.HandleReady += hwnd =>
                {
                    DebugLog($"HandleReady hwnd={hwnd}");
                    _mpvService.Initialize(hwnd);
                    DebugLog($"After Initialize, IsReady={_mpvService.IsReady}");
                    if (!_mpvService.IsReady)
                    {
                        ShowPlayerStatus("mpv non disponible.\nVérifiez que libmpv-2.dll est présent dans le dossier mpv/.");
                        return;
                    }

                    // Check AviSynth at startup so the user sees the status immediately.
                    var avsCheck = GetAviSynthDiagnostic();
                    DebugLog("AviSynth startup check: " + avsCheck);
                    if (!avsCheck.Contains("chargeable OK", StringComparison.Ordinal))
                        ShowPlayerStatus("AviSynth — " + avsCheck);

                    var srcOk = TryValidateSourceSelection(out _);
                    DebugLog($"TryValidateSourceSelection={srcOk}");
                    if (!srcOk)
                    {
                        return;
                    }
                    _ = LoadScriptAsync();
                };
                host.FilesDropped += OnPlayerFilesDropped;
            }
            else
            {
                DebugLog("VideoHost NOT found — FindControl returned null");
            }

            _mpvService.PositionChanged    += pos => Dispatcher.UIThread.Post(() => OnMpvPosition(pos));
            _mpvService.DurationChanged    += dur => Dispatcher.UIThread.Post(() => OnMpvDuration(dur));
            _mpvService.PauseChanged       += p   => Dispatcher.UIThread.Post(() => OnMpvPauseChanged(p));
            _mpvService.FileLoaded         += ()  => Dispatcher.UIThread.Post(() => OnMpvFileLoaded());

            _mpvService.PlaybackRestart    += ()  => Dispatcher.UIThread.Post(OnMpvPlaybackRestart);
            _mpvService.LoadFailed         += msg => Dispatcher.UIThread.Post(() => OnMpvLoadFailed(msg));
            _mpvService.UnexpectedShutdown += ()  => Dispatcher.UIThread.Post(OnMpvUnexpectedShutdown);

            if (this.FindControl<Slider>("SeekBar") is { } seekBar)
            {
                seekBar.AddHandler(PointerPressedEvent,  (_, _) => { _seekDragging = true; },
                    RoutingStrategies.Bubble, handledEventsToo: true);
                seekBar.AddHandler(PointerReleasedEvent, (_, _) =>
                    {
                        _seekDragging = false;
                        var pos = seekBar.Value;
                        // Clamp to the last valid frame: (N-1)/fps.
                        // Frame-based is exact; duration-based fallback used before first FileLoaded.
                        if (_totalFrames > 0 && _fps > 0)
                            pos = Math.Min(pos, (_totalFrames - 1.0) / _fps);
                        else if (_seekDuration > 0)
                            pos = Math.Min(pos, _seekDuration - 0.001);
                        _mpvService?.Seek(pos);
                    },
                    RoutingStrategies.Bubble, handledEventsToo: true);
            }
        }

        private void OnMpvPosition(double pos)
        {
            if (_seekDragging || _seekDuration <= 0) return;
            if (this.FindControl<Slider>("SeekBar") is { } s) s.Value = pos;
            UpdateTimeLabel(pos, _seekDuration);
        }

        private void OnMpvDuration(double dur)
        {
            _seekDuration = dur;
            if (this.FindControl<Slider>("SeekBar") is { } s)
            {
                s.Maximum   = dur > 0 ? dur : 1;
                s.IsEnabled = dur > 0;
            }
            UpdateTimeLabel(_mpvService?.GetPosition() ?? 0, dur);
        }

        private void OnMpvPauseChanged(bool paused)
        {
            if (this.FindControl<Button>("VdbPlay") is { } btn)
                btn.Content = paused ? "▶" : "⏸";
        }

        private static readonly string _logPath =
            Path.Combine(Path.GetTempPath(), "cleanscan_debug.txt");

        private static bool _avsUsingBundled;
        private static nint _avsBundledHandle;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectoryW(string lpPathName);

        /// <summary>
        /// Called once at startup. If AviSynth+ is installed system-wide, do nothing.
        /// Otherwise, pre-load the bundled avisynth.dll into the process and configure
        /// the DLL search path so that mpv and ffmpeg find it (and DevIL.dll).
        /// </summary>
        private static void EnsureAviSynthAvailable()
        {
            // 1. Check system install (System32)
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var systemDll = Path.Combine(system32, "avisynth.dll");
            if (File.Exists(systemDll))
            {
                try
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(systemDll, out var h))
                    {
                        System.Runtime.InteropServices.NativeLibrary.Free(h);
                        return; // System AviSynth is fine, use it
                    }
                }
                catch { }
            }

            // 2. Fallback: bundled AviSynth in Plugins/AviSynth/
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
            var bundledDir = Path.Combine(exeDir, "Plugins", "AviSynth");
            var bundledDll = Path.Combine(bundledDir, "avisynth.dll");
            if (!File.Exists(bundledDll)) return;

            // Add bundled dir to DLL search path (for DevIL.dll and other dependencies)
            SetDllDirectoryW(bundledDir);

            // Also prepend to PATH (for ffmpeg subprocess)
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", bundledDir + Path.PathSeparator + currentPath);

            // Pre-load avisynth.dll and keep the handle alive for the entire process lifetime.
            // When mpv internally calls LoadLibrary("avisynth"), Windows will find it already loaded.
            if (System.Runtime.InteropServices.NativeLibrary.TryLoad(bundledDll, out _avsBundledHandle))
                _avsUsingBundled = true;
        }

        private static string GetAviSynthDiagnostic()
        {
            // If using bundled version, check it
            if (_avsUsingBundled)
            {
                var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
                var bundledDll = Path.Combine(exeDir, "Plugins", "AviSynth", "avisynth.dll");
                if (!File.Exists(bundledDll))
                    return "AviSynth.dll bundlé introuvable";
                try
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(bundledDll, out var h))
                    {
                        System.Runtime.InteropServices.NativeLibrary.Free(h);
                        return "AviSynth.dll (bundlé) chargeable OK";
                    }
                    return "AviSynth.dll (bundlé) non chargeable (mauvaise architecture ?)";
                }
                catch (Exception ex) { return $"AviSynth.dll (bundlé) erreur : {ex.Message}"; }
            }

            // System install
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var dllPath  = Path.Combine(system32, "AviSynth.dll");
            if (!File.Exists(dllPath))
                return $"AviSynth.dll absent de {system32}";
            try
            {
                if (System.Runtime.InteropServices.NativeLibrary.TryLoad(dllPath, out var h))
                {
                    System.Runtime.InteropServices.NativeLibrary.Free(h);
                    return "AviSynth.dll présent et chargeable OK";
                }
                return "AviSynth.dll présent dans System32 mais non chargeable (mauvaise architecture ?)";
            }
            catch (Exception ex) { return $"AviSynth.dll erreur : {ex.Message}"; }
        }

        private static void DebugLog(string msg)
        {
            try { File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff}  {msg}\n"); }
            catch { }
        }

        private void ShowPlayerStatus(string message)
        {
            DebugLog("ShowPlayerStatus: " + message.Replace('\n', ' '));
            Title = "CleanScan — " + message.Split('\n')[0];

            if (this.FindControl<Border>("PlayerErrorBanner") is { } banner
             && this.FindControl<TextBlock>("PlayerErrorText")  is { } text)
            {
                text.Text        = message;
                banner.IsVisible = true;
            }
        }

        private void OnMpvLoadFailed(string errorMsg)
        {
            DebugLog("OnMpvLoadFailed: " + errorMsg);

            // "unknown file format" peut signifier soit AviSynth absent, soit un script AviSynth
            // qui plante à l'exécution (ex: paramètre invalide dans un filtre comme GamMac).
            // On distingue les deux cas via la présence d'AviSynth.dll.
            if (!_loadingSourceFallback
             && (errorMsg.Contains("unknown file format", StringComparison.OrdinalIgnoreCase)
              || errorMsg.Contains("unrecognized file format", StringComparison.OrdinalIgnoreCase)))
            {
                var diag = GetAviSynthDiagnostic();
                DebugLog("AviSynth diag: " + diag);

                // Si AviSynth est installé et chargeable, le script lui-même a planté
                // (paramètre invalide, plugin manquant, etc.) — pas de fallback vidéo.
                if (diag.Contains("chargeable", StringComparison.OrdinalIgnoreCase))
                {
                    ShowPlayerStatus("Erreur de script AviSynth.\nVérifiez les paramètres des filtres actifs (LockVal, Scale, Th…).");
                    return;
                }

                // AviSynth absent ou non chargeable : fallback lecture directe.
                var raw = _config.Get("source");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var path = _sourceService.NormalizeConfiguredPath(raw);
                    if (File.Exists(path))
                    {
                        _loadingSourceFallback = true;
                        ShowPlayerStatus($"AviSynth+ non détecté ({diag}).\nLecture directe de la source (sans filtres).");
                        _mpvService?.LoadFile(path, 0);
                        return;
                    }
                }
                ShowPlayerStatus($"AviSynth+ non détecté.\n{diag}");
                return;
            }

            _loadingSourceFallback = false;
            ShowPlayerStatus($"Erreur de lecture : {errorMsg}");
        }

        private void OnMpvFileLoaded()
        {
            DebugLog("OnMpvFileLoaded — file loaded successfully");
            Title = "CleanScan";

            if (_loadingSourceFallback)
            {
                // Fallback actif : on garde le banner pour signaler le mode dégradé.
                ShowPlayerStatus("Mode dégradé : lecture directe de la source (AviSynth+ non installé).");
            }
            else if (this.FindControl<Border>("PlayerErrorBanner") is { } banner)
            {
                banner.IsVisible = false;
            }

            if (this.FindControl<TextBlock>("DropHintBar") is { } dropHint)
                dropHint.IsVisible = false;


            if (this.FindControl<Slider>("SeekBar") is { } s) s.Value = _pendingSeekPos;

            // Query exact frame count and fps to set an accurate slider maximum.
            // This prevents seeking past the last valid frame, which would crash AviSynth
            // (ImageSource has no file for out-of-range indices) and freeze video players at EOF.
            _tourAdvanceOnClipLoaded?.Invoke();

            if (_mpvService is { IsReady: true })
            {
                _totalFrames = _mpvService.GetTotalFrames();
                _fps         = _mpvService.GetFps();

                if (_totalFrames > 0 && _fps > 0 && this.FindControl<Slider>("SeekBar") is { } bar)
                {
                    bar.Maximum   = (_totalFrames - 1.0) / _fps;
                    bar.IsEnabled = true;
                }
            }
        }

        private CancellationTokenSource? _pulseAnimCts;

        private void OnMpvPlaybackRestart()
        {
            _pulseAnimCts?.Cancel();
            _pulseAnimCts = null;
            if (this.FindControl<Button>("VdbPlay") is { } btn)
            {
                btn.Opacity = 1.0;
                btn.Background = new SolidColorBrush(Color.Parse("#1A2030"));
            }
        }

        private void SetPlayButtonProcessing()
        {
            if (this.FindControl<Button>("VdbPlay") is not { } btn) return;

            btn.Background = new SolidColorBrush(Color.Parse("#FFCC00"));

            _pulseAnimCts?.Cancel();
            _pulseAnimCts = new CancellationTokenSource();
            var ct = _pulseAnimCts.Token;

            var anim = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(700),
                IterationCount = IterationCount.Infinite,
                PlaybackDirection = PlaybackDirection.Alternate,
                Easing = new SineEaseInOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(OpacityProperty, 0.45) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(OpacityProperty, 1.0)  } }
                }
            };
            anim.RunAsync(btn, ct);
        }

        private void OnMpvUnexpectedShutdown()
        {
            // mpv shut down unexpectedly (e.g. AviSynth error during seek).
            // Reinitialise the player and reload the current script.
            _seekDragging = false;
            _seekDuration = 0;
            if (this.FindControl<Slider>("SeekBar") is { } s)
            {
                s.Value     = 0;
                s.Maximum   = 1;
                s.IsEnabled = false;
            }

            _mpvService?.Reinitialize();

            if (TryValidateSourceSelection(out _))
                _ = LoadScriptAsync();
        }

        private void UpdateTimeLabel(double pos, double dur)
        {
            static string Fmt(double s) =>
                TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
            if (this.FindControl<TextBlock>("TimeLabel") is { } lbl)
                lbl.Text = $"{Fmt(pos)} / {Fmt(dur)}";

            if (this.FindControl<TextBlock>("FrameLabel") is { } fl)
            {
                var currentFrame = _fps > 0 ? (int)(pos * _fps) : 0;
                fl.Text = $"{currentFrame} / {_totalFrames}";
            }
        }

        private void InitSliders()
        {
            foreach (var spec in SliderSpecs)
            {
                if (this.FindControl<Slider>("Slide_" + spec.Field) is not { } slider) continue;
                slider.Minimum     = spec.Min;
                slider.Maximum     = spec.Max;
                slider.SmallChange = spec.SmallChange;
                slider.LargeChange = spec.SmallChange * 10;
                _sliderMap[spec.Field] = (slider, spec);

                var captured = spec;
                var pressing = false;

                slider.ValueChanged += (_, _) => OnSliderValueChanged(captured);

                slider.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
                    pressing = true;
                    e.Pointer.Capture(slider);
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerMovedEvent, (_, e) =>
                {
                    if (!pressing) return;
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (!pressing) return;
                    pressing = false;
                    e.Pointer.Capture(null);
                    CommitSliderField(captured.Field);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                // Mouse wheel on TextBox
                if (this.FindControl<TextBox>(spec.Field) is { } tb)
                {
                    var capturedSpec = spec;
                    tb.PointerWheelChanged += (_, e) =>
                    {
                        e.Handled = true;
                        if (!_sliderMap.TryGetValue(capturedSpec.Field, out var entry)) return;
                        var delta = e.Delta.Y > 0 ? capturedSpec.SmallChange : -capturedSpec.SmallChange;
                        entry.Slider.Value = Math.Clamp(entry.Slider.Value + delta, entry.Spec.Min, entry.Spec.Max);
                        CommitSliderField(capturedSpec.Field);
                    };
                }
            }

            _config.Changed += OnConfigChangedForSlider;
        }

        private static void MoveSliderToPointer(Slider slider, PointerEventArgs e)
        {
            const double thumbHalf = 7.0;
            var w = slider.Bounds.Width;
            if (w <= thumbHalf * 2) return;
            var x     = e.GetCurrentPoint(slider).Position.X;
            var ratio = Math.Clamp((x - thumbHalf) / (w - thumbHalf * 2), 0.0, 1.0);
            slider.Value = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
        }

        private void OnSliderValueChanged(SliderSpec spec)
        {
            if (!_layoutInitialized || _sliderSync || _suppressTextEvents) return;
            if (!_sliderMap.TryGetValue(spec.Field, out var entry)) return;
            if (this.FindControl<TextBox>(spec.Field) is not { } tb) return;

            _sliderSync = true;
            try
            {
                tb.Text = spec.IsFloat
                    ? entry.Slider.Value.ToString("F" + spec.Decimals, CultureInfo.InvariantCulture)
                    : ((int)Math.Round(entry.Slider.Value)).ToString();
            }
            finally { _sliderSync = false; }
        }


        private void CommitSliderField(string field)
        {
            if (!_sliderMap.TryGetValue(field, out var entry)) return;
            var text = entry.Spec.IsFloat
                ? entry.Slider.Value.ToString("F" + entry.Spec.Decimals, CultureInfo.InvariantCulture)
                : ((int)Math.Round(entry.Slider.Value)).ToString();
            _ = ApplyFieldChangeAsync(field, text, showValidationError: true, refreshScriptPreview: false);
        }

        private void OnConfigChangedForSlider(string key, string value)
        {
            if (!_sliderMap.TryGetValue(key, out var entry)) return;
            if (_sliderSync) return;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) return;
            var clamped = Math.Clamp(val, entry.Slider.Minimum, entry.Slider.Maximum);

            Dispatcher.UIThread.Post(() =>
            {
                if (Math.Abs(entry.Slider.Value - clamped) < 0.0001) return;
                _sliderSync = true;
                try { entry.Slider.Value = clamped; }
                finally { _sliderSync = false; }
            });
        }

        private void SyncAllSliders()
        {
            foreach (var (field, (slider, _)) in _sliderMap)
            {
                var raw = _config.Get(field);
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) continue;
                _sliderSync = true;
                try { slider.Value = Math.Clamp(val, slider.Minimum, slider.Maximum); }
                finally { _sliderSync = false; }
            }
        }

        // Positions the window BEFORE Show() is called, so the native window is
        // created directly at the right coordinates (no top-left flash on startup).
        private void PreApplyWindowPosition()
        {
            var saved = _windowStateService.Load();
            if (saved is null) return; // First launch: OnOpened handles it via SnapToBottomOfScreen

            WindowStartupLocation = WindowStartupLocation.Manual;
            Width    = ClampWindowWidth(saved.Width);
            Height   = Math.Clamp(saved.Height, MinHeight, MaxHeight);
            Position = new PixelPoint(saved.X, saved.Y);
            // IsSavedPositionVisible is validated later in ApplyStartupLayout (OnOpened).
            // If the position turns out to be off-screen, the window will correct itself
            // once on that session (rare: only after a screen-layout change).
        }

        private void InitializeChoiceFields()
        {
            SetComboSource("Sharp_Mode",        SharpModeOptions);
            SetComboSource("sharp_preset",      SharpPresetOptions);
            SetComboSource("degrain_preset",    DegrainPresetOptions);
            SetComboSource("degrain_mode",      DegrainModeOptions);
            SetComboSource("degrain_prefilter", DegrainPrefilterOptions);
            SetComboSource("denoise_preset",    DenoisePresetOptions);
            SetComboSource("denoise_mode",      DenoiseModeOptions);

            if (this.FindControl<ComboBox>("sharp_preset") is { } sharpPresetCombo)
                sharpPresetCombo.SelectedItem = "standard";
            if (this.FindControl<ComboBox>("degrain_preset") is { } presetCombo)
                presetCombo.SelectedItem = "standard";
            if (this.FindControl<ComboBox>("denoise_preset") is { } denoisePresetCombo)
                denoisePresetCombo.SelectedItem = "standard";
        }

        private void SetComboSource(string name, string[] options)
        {
            if (this.FindControl<ComboBox>(name) is { } combo)
            {
                combo.ItemsSource  = options;
                combo.SelectedItem = options[0];
            }
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            _isInitializing = true;
            var settings = _windowStateService.Load();
            try
            {
                ViewModel.SetLanguage(settings?.Language ?? MainWindowViewModel.GetOsLanguageCodeOrEnglish());

                ApplyLanguage(ViewModel.CurrentLanguageCode, persist: false);
                _scriptService.EnsureScriptCopiesInOutputDir();
                ApplyConfigurationValues();
                RestoreSessionState(settings);

                // Restore saved session (clips + per-clip configs)
                RestoreSessionClips();

                // Régénère toujours avec la bonne langue au démarrage (indépendamment de la validation source)
                _scriptService.Generate(_config.Snapshot(), ViewModel.CurrentLanguageCode);
            }
            finally
            {
                _isInitializing = false;
            }

            if (TryValidateSourceSelection(out _))
                await LoadScriptAsync();

            Dispatcher.UIThread.Post(() => ApplyStartupLayout(settings), DispatcherPriority.Loaded);
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _isClosing = true;
            _refreshDebouncer.Cancel();
            _config.Changed -= OnConfigChangedForSlider;

            // Kill any running encoding process
            _encodingCts?.Cancel();
            if (_encodingProcess is { HasExited: false } proc)
                try { proc.Kill(entireProcessTree: true); } catch { }

            SaveWindowSettings();
            SaveSession();
            _layoutInitialized = false;
            _mpvService?.Dispose();
        }

        #endregion

        #region Language & menu

        private void ConfigureMenuBar() => ApplyLanguage(ViewModel.CurrentLanguageCode, persist: false);

        private void OnLanguageClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: string code })
                ApplyLanguage(code, persist: true);
        }

        private void ApplyLanguage(string languageCode, bool persist)
        {
            ViewModel.SetLanguage(languageCode);

            foreach (var (controlName, textKey) in new[]
            {
                ("InfosMenu",             "InfosMenu"),
                ("UserGuideMenuItem",     "UserGuideMenuItem"),
                ("ScriptPreviewMenuItem", "ScriptPreviewMenuItem"),
                ("GuidedTourMenuItem",   "GuidedTourMenuItem"),
                ("PresetMenuItem",        "PresetMenuItem"),
                ("AboutMenuItem",         "AboutMenuItem"),
                ("FeedbackMenuItem",     "FeedbackMenuItem"),
                ("SettingsMenu",         "SettingsMenu"),
                ("ResetSettingsMenuItem", "ResetSettingsMenuItem"),
            })
            {
                if (this.FindControl<MenuItem>(controlName) is { } item)
                    item.Header = GetUiText(textKey);
            }

            if (this.FindControl<MenuItem>("LanguagesMenu") is { } langMenu)
                langMenu.Header = languageCode.ToUpper();

            if (this.FindControl<TextBlock>("ThreadsLabel") is { } threadsLbl)
                threadsLbl.Text = GetUiText("ThreadsLabel");
            if (this.FindControl<TextBlock>("SourceLoaderLabel") is { } srcLbl)
                srcLbl.Text = GetUiText("SourceLoaderLabel");

            foreach (var expandName in new[] { "CropExpandBtn", "GammacExpandBtn", "DenoiseExpandBtn", "DegrainExpandBtn", "LumaExpandBtn", "SharpExpandBtn" })
            {
                if (this.FindControl<Button>(expandName) is { } expandBtn)
                    ToolTip.SetTip(expandBtn, GetUiText("ExpandBtnTooltip"));
            }

            SetLanguageMenuChecks();
            ApplyParamTooltips(languageCode);
            ApplyTransportTooltips();
            ApplyRecordLabels();


            if (this.FindControl<TextBlock>("DropHintBar") is { IsVisible: true } dropBar)
                dropBar.Text = GetUiText("DropHintBar");

            if (persist && IsVisible)
            {
                _scriptService.Generate(_config.Snapshot(), ViewModel.CurrentLanguageCode);
                SaveWindowSettings();
            }
        }

        private void ApplyTransportTooltips()
        {
            foreach (var (controlName, textKey) in new[]
            {
                ("VdbBeginning", "VdbBeginning"),
                ("VdbPrevFrame", "VdbPrevFrame"),
                ("VdbPlay",      "VdbPlay"),
                ("VdbStop",      "VdbStop"),
                ("VdbNextFrame", "VdbNextFrame"),
                ("VdbEnd",       "VdbEnd"),
                ("SpeedBtn",     "SpeedBtn"),
                ("HalfResBtn",   "HalfResBtn"),
                ("RecordBtn",    "RecordBtn"),
            })
            {
                if (this.FindControl<Button>(controlName) is { } btn)
                    ToolTip.SetTip(btn, GetUiText(textKey));
            }
        }

        private void ApplyRecordLabels()
        {
            if (this.FindControl<Button>("RecordBtn") is { } btn)
                btn.Content = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordOverlayTitle") is { } title)
                title.Text = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordDirLabel") is { } dirLbl)
                dirLbl.Text = GetUiText("RecordDirLabel");
            if (this.FindControl<TextBlock>("RecordFileLabel") is { } fileLbl)
                fileLbl.Text = GetUiText("RecordFileLabel");
            if (this.FindControl<TextBlock>("RecordEncoderLabel") is { } encLbl)
                encLbl.Text = GetUiText("RecordEncoderLabel");
            if (this.FindControl<TextBlock>("RecordContainerLabel") is { } cntLbl)
                cntLbl.Text = GetUiText("RecordContainerLabel");
            if (this.FindControl<TextBlock>("RecordQualityModeLabel") is { } qmLbl)
                qmLbl.Text = GetUiText("RecordQualityModeLabel");
            if (this.FindControl<TextBlock>("RecordCrfLabel") is { } crfLbl)
                crfLbl.Text = GetUiText("RecordCrfLabel");
            if (this.FindControl<TextBlock>("RecordBitrateLabel") is { } brLbl)
                brLbl.Text = GetUiText("RecordBitrateLabel");
            if (this.FindControl<TextBlock>("RecordChromaLabel") is { } chLbl)
                chLbl.Text = GetUiText("RecordChromaLabel");
            if (this.FindControl<TextBlock>("RecordResizeLabel") is { } rsLbl)
                rsLbl.Text = GetUiText("RecordResizeLabel");
            if (this.FindControl<TextBlock>("RecordPresetLabel") is { } prLbl)
                prLbl.Text = GetUiText("RecordPresetLabel");
            if (this.FindControl<Button>("RecordPresetSaveBtn") is { } prSave)
                prSave.Content = GetUiText("RecordPresetSaveBtn");
            if (this.FindControl<Button>("RecordPresetDeleteBtn") is { } prDel)
                prDel.Content = GetUiText("RecordPresetDeleteBtn");
            if (this.FindControl<Button>("RecordStartBtn") is { } startBtn)
                startBtn.Content = GetUiText("RecordStartBtn");
            if (this.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
                shutCb.Content = GetUiText("ShutdownCheckBox");
        }

        private void ApplyParamTooltips(string lang)
        {
            foreach (var (name, translations) in ParamTooltipTexts)
            {
                if (this.FindControl<TextBlock>(name) is not { } label) continue;
                var tip = translations.TryGetValue(lang, out var t) ? t
                        : translations.TryGetValue("en", out var en) ? en
                        : string.Empty;
                ToolTip.SetTip(label, tip);
            }
        }

        private void SetLanguageMenuChecks()
        {
            SetLanguageMenuItemChecked("LanguageEnglishMenuItem", "en");
            SetLanguageMenuItemChecked("LanguageFrenchMenuItem",  "fr");
            SetLanguageMenuItemChecked("LanguageGermanMenuItem",  "de");
            SetLanguageMenuItemChecked("LanguageSpanishMenuItem", "es");
        }

        private void SetLanguageMenuItemChecked(string menuName, string languageCode)
        {
            if (this.FindControl<MenuItem>(menuName) is not { } menuItem) return;

            var label = languageCode switch
            {
                "en" => "English",
                "fr" => "Français",
                "de" => "Deutsch",
                "es" => "Español",
                _    => languageCode
            };

            var isSelected = string.Equals(ViewModel.CurrentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase);
            menuItem.Header = isSelected ? $"✓ {label}" : label;
        }

        private string GetUiText(string key) => ViewModel.GetUiText(key);
        private string GetLocalizedText(string fr, string en) => ViewModel.GetLocalizedText(fr, en);

        private async void OnResetSettingsClick(object? sender, RoutedEventArgs e)
        {
            CloseSettingsMenu();

            // Confirmation dialog with Yes/No
            var result = false;
            var yesButton = new Button { Content = GetUiText("OkButton"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var noButton  = new Button { Content = GetUiText("GamMacCloseButton"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            var dialog = new Window
            {
                Title = GetUiText("ResetSettingsTitle"),
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = GetUiText("ResetSettingsConfirm"), TextWrapping = TextWrapping.Wrap, MaxWidth = 400 },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Children = { yesButton, noButton }
                        }
                    }
                }
            };

            yesButton.Click += (_, _) => { result = true; dialog.Close(); };
            noButton.Click  += (_, _) => dialog.Close();
            await dialog.ShowDialog(this);

            if (!result) return;

            // Delete entire AppData\CleanScan folder so the app leaves no trace
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder);
            try { if (Directory.Exists(appDataDir)) Directory.Delete(appDataDir, recursive: true); } catch { }

            // Restart application
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
            {
                System.Diagnostics.Process.Start(exePath);
                Environment.Exit(0);
            }
        }

        private void CloseSettingsMenu()
        {
            if (this.FindControl<MenuItem>("SettingsMenu") is { } menu)
                menu.Close();
        }

        #endregion

        #region Window settings / layout

        private const int WindowBottomPadding = 8;

        private void ApplyStartupLayout(WindowSettings? saved = null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width  = GetStartupWidth(saved);
            Height = GetStartupHeight(saved);

            if (saved is { X: var sx, Y: var sy } && IsSavedPositionVisible(sx, sy))
                Position = new PixelPoint(sx, sy);
            else if (saved is null)
                CenterOnScreen();   // First launch → centre the window nicely
            else
                SnapToBottomOfScreen();

            if (saved?.BottomPanelHeight is { } bph)
                MainGrid.RowDefinitions[2].Height = new GridLength(Math.Clamp(bph, 60, 800), GridUnitType.Pixel);

            _layoutInitialized = true;

            if (saved?.TourCompleted != true && _clipPaths.Count == 0)
                Dispatcher.UIThread.Post(() => _ = ShowGuidedTourAsync(), DispatcherPriority.Background);
        }

        private double GetStartupHeight(WindowSettings? saved)
        {
            if (saved is not null)
                return Math.Clamp(saved.Height, MinHeight, MaxHeight);

            return GetCompactStartupHeight();
        }

        private double GetStartupWidth(WindowSettings? saved)
        {
            if (saved is not null)
                return ClampWindowWidth(saved.Width);

            // First launch: use ~2/3 of screen width
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is not null)
            {
                var available = screen.WorkingArea.Width / screen.Scaling;
                var target = available * 2.0 / 3.0;
                return Math.Clamp(target, MinWidth, available);
            }
            return Width;
        }

        private double ClampWindowWidth(double width)
        {
            var maxWidth = double.IsFinite(MaxWidth) ? MaxWidth : double.MaxValue;
            return Math.Clamp(width, MinWidth, maxWidth);
        }

        private double GetCompactStartupHeight()
        {
            // First launch: use ~70% of screen height for a comfortable view
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is not null)
            {
                var available = screen.WorkingArea.Height / screen.Scaling;
                var target = available * 0.70;
                return Math.Clamp(target, MinHeight, available);
            }
            return MinHeight;
        }

        private void CenterOnScreen()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null) { SnapToBottomOfScreen(); return; }

            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            // Use Width/Height properties (Bounds may not be updated yet)
            var w = (int)Math.Ceiling(Width * scaling);
            var h = (int)Math.Ceiling(Height * scaling);

            var x = wa.X + Math.Max(0, (wa.Width  - w) / 2);
            var y = wa.Y + Math.Max(0, (wa.Height - h) / 2);
            Position = new PixelPoint(x, y);
        }

        private PixelPoint GetBottomAnchoredPosition(Screen screen, int requestedX)
        {
            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            var w  = (int)Math.Ceiling(Bounds.Width * scaling);
            var h  = Math.Min((int)Math.Ceiling(Height * scaling), wa.Height);

            var x = Math.Clamp(requestedX, wa.X, Math.Max(wa.X, wa.Right - w));
            var y = Math.Clamp(wa.Bottom - h - WindowBottomPadding, wa.Y, Math.Max(wa.Y, wa.Bottom - h));

            return new PixelPoint(x, y);
        }

        private WindowSettings? _lastGoodSettings;

        private void CaptureWindowSettings()
        {
            if (WindowState != WindowState.Normal) return;
            if (_isInitializing || !_layoutInitialized) return;
            var bottomH = BottomPanel.Bounds.Height is > 0 and var bh ? (double?)bh : null;
            _lastGoodSettings = new WindowSettings(Bounds.Width, Bounds.Height, Position.X, Position.Y, ViewModel.CurrentLanguageCode, bottomH);
        }

        private void SaveWindowSettings()
        {
            CaptureWindowSettings();
            if (_lastGoodSettings is { } s)
            {
                var panels = _openParamPanels.Count > 0 ? _openParamPanels.ToArray() : null;
                var lastDir = this.FindControl<TextBox>("RecordDir")?.Text?.Trim();
                var prevTour = _windowStateService.Load()?.TourCompleted;
                _windowStateService.Save(s with { Language = ViewModel.CurrentLanguageCode, OpenPanels = panels, LastOutputDir = lastDir, AutoSaveEncodingPreset = _autoSaveEncodingPreset ? true : null, RecordPanelOpen = _recordOpen ? true : null, TourCompleted = prevTour });
            }
        }

        private async void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing || WindowState != WindowState.Normal) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        private async void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing || WindowState != WindowState.Normal) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        private async void OnBottomPanelSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        #endregion

        #region Configuration loading & UI binding

        private void ApplyConfigurationValues()
        {
            var scriptValues    = _scriptService.LoadScriptValues();
            var resourceManager = new ResourceManager("CleanScan.Resources.ConfigValues", typeof(MainWindow).Assembly);
            var newValues       = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in ScriptService.TextFieldNames)
            {
                var value = scriptValues.TryGetValue(name, out var sv) ? sv : resourceManager.GetString(name);
                if (value is null) continue;

                var isPath  = IsPathField(name);
                var uiValue = isPath ? _sourceService.NormalizeConfiguredPath(value) : value;

                if (this.FindControl<Control>(name) is TextBox tb)
                    SetTextSafely(tb, uiValue);
                else if (this.FindControl<Control>(name) is ComboBox cb)
                    uiValue = ApplyComboChoice(cb, name, uiValue);

                newValues[name] = uiValue;
            }

            var useImage = scriptValues.TryGetValue(UseImageConfigName, out var uiv)
                && bool.TryParse(uiv, out var parsedUseImage) && parsedUseImage;

            var legacySource = useImage
                ? (scriptValues.TryGetValue("img",  out var iv) ? iv : resourceManager.GetString("img"))
                : (scriptValues.TryGetValue("film", out var fv) ? fv : resourceManager.GetString("film"));

            if (!string.IsNullOrWhiteSpace(legacySource))
                SetDetectedSourceValue(legacySource, newValues);

            UpdateSourceSelection(isFilmSelected: !useImage, updateConfig: false, currentValues: newValues);

            foreach (var name in ScriptService.BoolFieldNames)
            {
                var raw = scriptValues.TryGetValue(name, out var bsv) ? bsv : resourceManager.GetString(name);
                if (raw is null || !bool.TryParse(raw, out var parsed)) continue;
                SetOptionToggleValue(name, parsed);
                newValues[name] = parsed.ToString().ToLowerInvariant();
            }

            UpdateOptionColumnVisibility();
            _config.ReplaceAll(newValues);
            SyncAllSliders();
            SyncForceSourceCombo(newValues);
        }

        private void RestoreSessionState(WindowSettings? settings)
        {
            // Restore half-res button visual from config
            var halfResValue = _config.Get("preview_half");
            if (bool.TryParse(halfResValue, out var halfRes) && halfRes)
            {
                _halfRes = true;
                if (this.FindControl<Button>("HalfResBtn") is { } btn)
                {
                    btn.Background = new SolidColorBrush(Color.Parse("#35C156"));
                    btn.Foreground = Brushes.White;
                }
            }

            // Restore expanded filter panels
            if (settings?.OpenPanels is { Length: > 0 } panels)
            {
                foreach (var panelName in panels)
                {
                    if (!IsParamPanelEnabled(panelName)) continue;
                    _openParamPanels.Add(panelName);
                    if (this.FindControl<Control>(panelName) is { } panel) panel.IsVisible = true;

                    // Update the matching expand button
                    var btnName = panelName.Replace("Params", "ExpandBtn");
                    if (this.FindControl<Button>(btnName) is { } expandBtn)
                    {
                        expandBtn.Content = "▶";
                        expandBtn.Classes.Add("active");
                    }
                }
                UpdateParamsPlaceholderVisibility();
            }

            // Restore last output directory
            if (!string.IsNullOrWhiteSpace(settings?.LastOutputDir))
            {
                if (this.FindControl<TextBox>("RecordDir") is { } dirTb)
                    dirTb.Text = settings.LastOutputDir;
            }

            // Restore "auto-save encoding preset" preference
            if (settings?.AutoSaveEncodingPreset == true)
                _autoSaveEncodingPreset = true;

            // Restore Record panel visibility
            if (settings?.RecordPanelOpen == true)
            {
                _recordOpen = true;
                if (this.FindControl<Button>("RecordBtn") is { } recBtn)
                {
                    recBtn.Background = new SolidColorBrush(Color.Parse("#C62828"));
                    recBtn.Foreground = Brushes.White;
                }
                if (this.FindControl<Border>("RecordOverlay") is { } overlay)
                    overlay.IsVisible = true;
                RebuildBatchClipList();
                UpdateDiskSpaceLabel(this.FindControl<TextBox>("RecordDir")?.Text);
            }
        }

        /// <summary>Called by App on unhandled exceptions to save session state before crash.</summary>
        public void EmergencySaveSession()
        {
            try { SaveSession(); } catch { }
        }

        private void SaveSession()
        {
            // Ensure the active clip's config is up to date
            SaveActiveClipConfig();

            var clips = new List<ClipSession>();
            for (int i = 0; i < _clipPaths.Count; i++)
            {
                clips.Add(new ClipSession(
                    Path:               _clipPaths[i],
                    FilterConfig:       i < _clipConfigs.Count ? _clipConfigs[i] : new(),
                    PresetName:         i < _clipPresetNames.Count ? _clipPresetNames[i] : null,
                    BatchSelected:      i < _clipBatchSelected.Count && _clipBatchSelected[i],
                    BatchEncodingPreset: i < _clipBatchEncodingPreset.Count ? _clipBatchEncodingPreset[i] : null));
            }

            var encPresetName = (this.FindControl<ComboBox>("RecordPresetCombo")?.SelectedItem as string)?.Trim();
            _sessionService.Save(new SessionState(_activeClipIndex, clips, CaptureEncodingValues(), encPresetName));
        }

        private void RestoreSessionClips()
        {
            var session = _sessionService.Load();
            if (session?.Clips is not { Count: > 0 } clips) return;

            // Filter out clips whose source files no longer exist
            var validClips = new List<(ClipSession Clip, int OriginalIndex)>();
            for (int i = 0; i < clips.Count; i++)
            {
                if (File.Exists(clips[i].Path))
                    validClips.Add((clips[i], i));
            }
            if (validClips.Count == 0) return;

            // Rebuild clip state from session
            _clipPaths.Clear();
            _clipConfigs.Clear();
            _clipPresetNames.Clear();
            _clipBatchSelected.Clear();
            _clipBatchEncodingPreset.Clear();

            foreach (var (clip, _) in validClips)
            {
                _clipPaths.Add(clip.Path);
                _clipConfigs.Add(new Dictionary<string, string>(clip.FilterConfig, StringComparer.OrdinalIgnoreCase));
                _clipPresetNames.Add(clip.PresetName);
                _clipBatchSelected.Add(clip.BatchSelected);
                _clipBatchEncodingPreset.Add(clip.BatchEncodingPreset);
            }

            // Determine the active clip index
            var targetIndex = session.ActiveClipIndex;
            var newIndex = validClips.FindIndex(v => v.OriginalIndex == targetIndex);
            if (newIndex < 0) newIndex = 0;
            _activeClipIndex = newIndex;

            // Restore active clip's filter config into _config and UI
            RestoreClipConfig(_activeClipIndex);

            // Set source directly (without going through ApplyDetectedSourceAndRefreshAsync
            // which calls AddOrActivateClip and would corrupt the restored clip lists)
            var sourcePath = _clipPaths[_activeClipIndex];
            var normalized = _sourceService.NormalizeConfiguredPath(sourcePath);
            _config.Set("source", normalized);
            _config.Set("film",   normalized);
            _config.Set("img",    normalized);
            if (this.FindControl<TextBox>("source") is { } srcTb)
                SetTextSafely(srcTb, normalized);

            // Detect film vs image mode
            var isImage = _sourceService.IsImageSource(normalized);
            UpdateSourceSelection(isFilmSelected: !isImage);

            // Rebuild UI
            RestoreClipPresetCombo();
            RebuildClipTabs();

            // Restore encoding parameters
            if (session.EncodingValues is { Count: > 0 } encVals)
                ApplyEncodingValues(encVals);

            // Restore encoding preset combo selection
            if (!string.IsNullOrWhiteSpace(session.EncodingPresetName)
                && this.FindControl<ComboBox>("RecordPresetCombo") is { } encCombo)
            {
                RefreshEncodingPresetCombo();
                encCombo.SelectedItem = session.EncodingPresetName;
            }
        }

        private static bool IsPathField(string name) =>
            string.Equals(name, "source", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "film", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "img",  StringComparison.OrdinalIgnoreCase);

        private static string ApplyComboChoice(ComboBox cb, string name, string rawValue)
        {
            if (string.Equals(name, "Sharp_Mode",    StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, SharpModeOptions);   return cb.SelectedItem?.ToString() ?? SharpModeOptions[0]; }
            if (string.Equals(name, "degrain_mode",      StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DegrainModeOptions);      return cb.SelectedItem?.ToString() ?? DegrainModeOptions[0]; }
            if (string.Equals(name, "degrain_prefilter", StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DegrainPrefilterOptions); return cb.SelectedItem?.ToString() ?? DegrainPrefilterOptions[0]; }
            if (string.Equals(name, "denoise_mode",  StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DenoiseModeOptions);  return cb.SelectedItem?.ToString() ?? DenoiseModeOptions[0]; }
            cb.SelectedItem = rawValue;
            return rawValue;
        }

        #endregion

        #region Change handlers registration

        private void RegisterChangeHandlers()
        {
            foreach (var spec in FieldSpecs)
            {
                if (this.FindControl<Control>(spec.Name) is TextBox textBox)
                {
                    RegisterTextBoxHandler(textBox, spec);
                    continue;
                }

                if (this.FindControl<Control>(spec.Name) is ComboBox combo)
                {
                    combo.SelectionChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, combo.SelectedItem?.ToString() ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: false);
                    };
                }
            }

            if (this.FindControl<ComboBox>("sharp_preset") is { } sharpPresetHandler)
            {
                sharpPresetHandler.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (sharpPresetHandler.SelectedItem is string preset)
                        ApplySharpPreset(preset);
                };
            }

            if (this.FindControl<ComboBox>("degrain_preset") is { } presetCombo)
            {
                presetCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (presetCombo.SelectedItem is string preset)
                        ApplyDegrainPreset(preset);
                };
            }

            if (this.FindControl<ComboBox>("denoise_preset") is { } denoisePresetCombo)
            {
                denoisePresetCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (denoisePresetCombo.SelectedItem is string preset)
                        ApplyDenoisePreset(preset);
                };
            }

            foreach (var name in ScriptService.BoolFieldNames)
            {
                if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) continue;
                btn.Tag = false;
                UpdateToggleButtonPresentation(btn, isEnabled: false);
            }

            if (this.FindControl<TextBox>("threads") is { } threadsTextBox)
            {
                threadsTextBox.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
                {
                    threadsTextBox.Focus();
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);

                threadsTextBox.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                        CloseSettingsMenu();
                };
                threadsTextBox.LostFocus += (_, _) => CloseSettingsMenu();
            }

            RegisterPathPickers();
        }

        private void RegisterTextBoxHandler(TextBox textBox, FieldSpec spec)
        {
            switch (spec.Mode)
            {
                case UpdateMode.Debounced:
                    textBox.TextChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: false);
                    };
                    break;

                case UpdateMode.OnLostFocus:
                    textBox.LostFocus += async (_, _) =>
                    {
                        if (_suppressTextEvents) return;
                        if (spec.Name.Equals("source", StringComparison.OrdinalIgnoreCase))
                        {
                            var raw = textBox.Text ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                UpdateSourceSelection(isFilmSelected: _sourceService.IsVideoSource(raw), updateConfig: true);
                                SetDetectedSourceValue(NormalizeSourceValue(raw));
                            }
                        }
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: true, refreshScriptPreview: true);
                    };
                    break;

                case UpdateMode.OnEnter:
                    textBox.KeyDown += async (_, e) =>
                    {
                        if (e.Key != Key.Enter || _suppressTextEvents) return;
                        e.Handled = true;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: true, refreshScriptPreview: true);
                        Focus();
                    };
                    break;

                case UpdateMode.Immediate:
                    textBox.TextChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: true);
                    };
                    break;
            }
        }

        private void ApplySharpPreset(string preset)
        {
            if (!SharpPresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("sharp_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        private void ApplyDegrainPreset(string preset)
        {
            if (!DegrainPresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("degrain_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        private void ApplyDenoisePreset(string preset)
        {
            if (!DenoisePresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("denoise_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        /// <summary>Renames the active clip to the next "persoN" if it isn't already one.</summary>
        private void MarkClipAsPerso()
        {
            if (_activeClipIndex < 0 || _activeClipIndex >= _clipPresetNames.Count) return;
            var currentName = _clipPresetNames[_activeClipIndex];
            if (currentName is not null && currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase)) return;
            _clipPresetNames[_activeClipIndex] = GetNextPersoName();
            RestoreClipPresetCombo();
            RebuildClipTabs();
        }

        private void RegisterPathPickers()
        {
            if (this.FindControl<Border>("ClipTabsContainer") is { } container)
            {
                container.AddHandler(DragDrop.DragOverEvent, OnSourceDragOver, RoutingStrategies.Bubble);
                container.AddHandler(DragDrop.DropEvent,     OnSourceDrop,     RoutingStrategies.Bubble);
            }
        }


        #endregion

        #region Source management

        private async Task ApplyDetectedSourceAndRefreshAsync(string rawValue)
        {
            rawValue ??= string.Empty;
            SetDetectedSourceValue(NormalizeSourceValue(rawValue));

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                _mpvService?.Unload();
                RegenerateScript(showValidationError: false);
                return;
            }

            // Stop mpv playback and reset player state for the new clip.
            _mpvService?.Stop();
            _seekDragging = false;
            _seekDuration = 0;
            _totalFrames  = 0;
            _fps          = 0;
            if (this.FindControl<Slider>("SeekBar") is { } seekBar)
            {
                seekBar.Value     = 0;
                seekBar.Maximum   = 1;
                seekBar.IsEnabled = false;
            }
            UpdateTimeLabel(0, 0);

            // Reset crop values to 0.
            _suppressTextEvents = true;
            try
            {
                foreach (var cropField in new[] { "Crop_L", "Crop_T", "Crop_R", "Crop_B" })
                {
                    _config.Set(cropField, "0");
                    if (this.FindControl<TextBox>(cropField) is { } tb)
                        tb.Text = "0";
                }
            }
            finally { _suppressTextEvents = false; }
            SyncAllSliders();

            var isFilm = _sourceService.IsVideoSource(rawValue);
            UpdateSourceSelection(isFilmSelected: isFilm, updateConfig: true);

            if (!isFilm)
            {
                var dir = Path.GetDirectoryName(_sourceService.NormalizeConfiguredPath(rawValue));
                if (!string.IsNullOrWhiteSpace(dir))
                    UpdateImageRangeFields(dir);
            }

            RegenerateScript(showValidationError: true);

            if (TryValidateSourceSelection(out var msg))
            {
                _refreshDebouncer.Cancel();
                await LoadScriptAsync(resetPosition: true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(msg))
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), msg);
        }

        private void OnSourceDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedFilePaths(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnSourceDrop(object? sender, DragEventArgs e)
        {
            var paths = GetDroppedFilePaths(e);
            if (paths.Count == 0)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("DropInvalidFileType"));
                return;
            }

            // Activate the first dropped file
            await ApplyDetectedSourceAndRefreshAsync(paths[0]);

            // Add remaining dropped files without activating
            for (int i = 1; i < paths.Count; i++)
            {
                var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(paths[i]));
                if (!_clipPaths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    _clipPaths.Add(normalized);
                    _clipConfigs.Add(CaptureClipConfig());
                    _clipPresetNames.Add(null);
                    _clipBatchSelected.Add(true);
                    _clipBatchEncodingPreset.Add(null);
                }
            }
            if (paths.Count > 1)
                RebuildClipTabs();
        }

        private async void OnPlayerFilesDropped(List<string> paths)
        {
            var valid = paths.Where(p =>
            {
                var ext = Path.GetExtension(p);
                return !string.IsNullOrWhiteSpace(ext) &&
                       (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                        ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
            }).ToList();

            if (valid.Count == 0) return;

            // Activate the first dropped file
            await ApplyDetectedSourceAndRefreshAsync(valid[0]);

            // Add remaining files without activating
            for (int i = 1; i < valid.Count; i++)
            {
                var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(valid[i]));
                if (!_clipPaths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    _clipPaths.Add(normalized);
                    _clipConfigs.Add(CaptureClipConfig());
                    _clipPresetNames.Add(null);
                    _clipBatchSelected.Add(true);
                    _clipBatchEncodingPreset.Add(null);
                }
            }
            if (valid.Count > 1)
                RebuildClipTabs();
        }

        private static List<string> GetDroppedFilePaths(DragEventArgs e)
        {
#pragma warning disable CS0618
            if (!e.Data.Contains(DataFormats.Files)) return [];
            var items = e.Data.GetFiles()?.ToList();
#pragma warning restore CS0618
            if (items is null || items.Count == 0) return [];

            var paths = new List<string>();
            foreach (var item in items)
            {
                var path = item.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path)) continue;
                var ext = Path.GetExtension(path);
                if (!string.IsNullOrWhiteSpace(ext) &&
                    (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                     ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)))
                    paths.Add(path);
            }
            return paths;
        }


        private static IReadOnlyList<FilePickerFileType> BuildSourceFileTypeFilter(string? currentValue)
        {
            var dir = GetDirectoryPath(currentValue);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return [VideoFileType, ImageFileType];

            bool hasTiff = false, hasVideo = false, hasImage = false;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (string.IsNullOrWhiteSpace(ext)) continue;

                if (ext.Equals(".tif",  StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase)) { hasTiff = true; hasImage = true; }
                else if (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) hasVideo = true;
                else if (ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) hasImage = true;

                if ((hasTiff || hasImage) && hasVideo) break;
            }

            return hasTiff || (hasImage && !hasVideo)
                ? [ImageFileType, VideoFileType]
                : [VideoFileType, ImageFileType];
        }

        private static string? GetDirectoryPath(string? currentValue)
        {
            var path = currentValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Directory.Exists(path)) return path;
            if (File.Exists(path)) return Path.GetDirectoryName(path);
            var parent = Path.GetDirectoryName(path);
            return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) ? parent : null;
        }

        private void UpdateSourceSelection(bool isFilmSelected, bool updateConfig = true,
            Dictionary<string, string>? currentValues = null)
        {
            var useImageValue = (!isFilmSelected).ToString().ToLowerInvariant();

            SetPanelVisibility("img_start_panel", !isFilmSelected);
            SetPanelVisibility("img_end_panel",   !isFilmSelected);
            SetPanelVisibility("play_speed_panel", isFilmSelected);

            if (updateConfig)
            {
                _config.Set(UseImageConfigName, useImageValue);
            }
            else
            {
                currentValues?[UseImageConfigName] = useImageValue;
            }
        }

        private void SetPanelVisibility(string name, bool visible)
        {
            if (this.FindControl<StackPanel>(name) is { } p)
                p.IsVisible = visible;
        }

        private void SetDetectedSourceValue(string rawValue, Dictionary<string, string>? currentValues = null)
        {
            var normalized = _sourceService.NormalizeConfiguredPath(rawValue);

            if (currentValues is not null)
            {
                currentValues["source"] = normalized;
                currentValues["film"]   = normalized;
                currentValues["img"]    = normalized;
            }
            else
            {
                _config.Set("source", normalized);
                _config.Set("film",   normalized);
                _config.Set("img",    normalized);
            }

            if (this.FindControl<TextBox>("source") is { } tb)
                SetTextSafely(tb, normalized);

            AddOrActivateClip(normalized);
        }

        private void AddOrActivateClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var idx = _clipPaths.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                _activeClipIndex = idx;
            else
            {
                _clipPaths.Add(path);
                // New clip inherits the current config snapshot (filters, not source/crop)
                _clipConfigs.Add(CaptureClipConfig());
                _clipPresetNames.Add(null);
                _clipBatchSelected.Add(true);
                _clipBatchEncodingPreset.Add(null);
                _activeClipIndex = _clipPaths.Count - 1;
            }
            RebuildClipTabs();
        }

        /// <summary>Captures the current filter config (excludes source/crop fields).</summary>
        private Dictionary<string, string> CaptureClipConfig()
        {
            var snap = _config.Snapshot();
            // Keep only filter-related keys (same exclusions as presets)
            foreach (var key in PresetService.ExcludedKeys)
                snap.Remove(key);
            return snap;
        }

        /// <summary>Saves the current config into the active clip's config slot.</summary>
        private void SaveActiveClipConfig()
        {
            if (_activeClipIndex >= 0 && _activeClipIndex < _clipConfigs.Count)
                _clipConfigs[_activeClipIndex] = CaptureClipConfig();
        }

        /// <summary>Restores a clip's filter config into _config and refreshes all UI controls.</summary>
        private void RestoreClipConfig(int index)
        {
            if (index < 0 || index >= _clipConfigs.Count) return;
            var clipCfg = _clipConfigs[index];

            _suppressTextEvents = true;
            _applyingPreset = true;
            try
            {
                foreach (var name in ScriptService.TextFieldNames)
                {
                    if (PresetService.ExcludedKeys.Contains(name)) continue;
                    if (!clipCfg.TryGetValue(name, out var value)) continue;

                    if (this.FindControl<Control>(name) is TextBox tb) tb.Text = value;
                    else if (this.FindControl<Control>(name) is ComboBox cb) ApplyComboChoice(cb, name, value);

                    _config.Set(name, value);
                }

                foreach (var name in ScriptService.BoolFieldNames)
                {
                    if (!clipCfg.TryGetValue(name, out var v) || !bool.TryParse(v, out var parsed)) continue;
                    SetOptionToggleValue(name, parsed);
                    _config.Set(name, parsed.ToString().ToLowerInvariant());
                }

                // Restore filter preset combo selections
                foreach (var presetKey in new[] { "sharp_preset", "degrain_preset", "denoise_preset" })
                {
                    if (this.FindControl<ComboBox>(presetKey) is { } presetCombo)
                    {
                        if (clipCfg.TryGetValue(presetKey, out var presetVal) && !string.IsNullOrEmpty(presetVal))
                            presetCombo.SelectedItem = presetVal;
                        else
                            presetCombo.SelectedIndex = -1;
                    }
                }
            }
            finally
            {
                _suppressTextEvents = false;
                _applyingPreset = false;
            }

            SyncAllSliders();
            UpdateOptionColumnVisibility();
        }

        private void RebuildClipTabs()
        {
            if (this.FindControl<WrapPanel>("ClipTabsPanel") is not { } panel) return;

            // Keep only the "+" button (last child)
            var addBtn = this.FindControl<Button>("AddClipBtn");
            panel.Children.Clear();

            for (int i = 0; i < _clipPaths.Count; i++)
            {
                var index = i;
                var path = _clipPaths[i];
                var filename = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(filename)) filename = path;
                var isActive = i == _activeClipIndex;

                var presetName = i < _clipPresetNames.Count ? _clipPresetNames[i] : null;
                var presetSuffix = presetName is not null
                    ? $"  [{presetName}]"
                    : string.Empty;

                var label = new TextBlock
                {
                    Text = filename + presetSuffix,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (isActive)
                    label.Foreground = Brushes.White;

                var closeBtn = new Button
                {
                    Content = "\u00d7",
                    FontSize = 12,
                    Background = Brushes.Transparent,
                    Foreground = isActive
                        ? new SolidColorBrush(Color.Parse("#FFFFFFA0"))
                        : new SolidColorBrush(Color.Parse("#7984A5")),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 0, 0, 0),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 0,
                    MinHeight = 0,
                };
                closeBtn.Click += (_, _) => RemoveClip(index);

                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                };
                stack.Children.Add(label);
                stack.Children.Add(closeBtn);

                var tab = new Border
                {
                    Background = isActive
                        ? new SolidColorBrush(Color.Parse("#3B82C4"))
                        : new SolidColorBrush(Color.Parse("#1A2030")),
                    BorderBrush = isActive
                        ? new SolidColorBrush(Color.Parse("#4A9AD4"))
                        : new SolidColorBrush(Color.Parse("#3A4660")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 4),
                    Margin = new Thickness(0, 0, 6, 4),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = stack,
                };
                tab.PointerPressed += (_, _) => SwitchToClip(index);
                ToolTip.SetTip(tab, path);

                panel.Children.Add(tab);
            }

            // Re-add the "+" button at the end
            if (addBtn is not null)
                panel.Children.Add(addBtn);
        }

        private async void SwitchToClip(int index)
        {
            if (index < 0 || index >= _clipPaths.Count || index == _activeClipIndex) return;

            // Save current clip's filter config
            SaveActiveClipConfig();

            // Switch source (this sets _activeClipIndex via AddOrActivateClip)
            await ApplyDetectedSourceAndRefreshAsync(_clipPaths[index]);

            // Restore the target clip's filter config
            RestoreClipConfig(_activeClipIndex);

            // Restore per-clip preset selection
            RestoreClipPresetCombo();

            RegenerateScript(showValidationError: false);

            if (TryValidateSourceSelection(out _))
                await LoadScriptAsync();
        }

        /// <summary>Restores the per-clip preset ComboBox selection without triggering the change handler.</summary>
        private void RestoreClipPresetCombo()
        {
            if (this.FindControl<ComboBox>("ClipPresetCombo") is not { } combo) return;
            _suppressClipPresetChange = true;
            try
            {
                var presetName = _activeClipIndex >= 0 && _activeClipIndex < _clipPresetNames.Count
                    ? _clipPresetNames[_activeClipIndex]
                    : null;

                var isPerso = presetName?.StartsWith("perso", StringComparison.OrdinalIgnoreCase) == true;

                var presets = _presetService.LoadPresets()
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(p => p.Name)
                    .ToList();
                combo.ItemsSource = presets;

                if (presetName is not null && !isPerso && presets.Contains(presetName, StringComparer.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = presetName;
                    combo.PlaceholderText = null;
                }
                else
                {
                    combo.SelectedIndex = -1;
                    combo.PlaceholderText = presetName; // shows "perso2" as placeholder
                }
            }
            finally { _suppressClipPresetChange = false; }
        }

        private async void RemoveClip(int index)
        {
            if (index < 0 || index >= _clipPaths.Count) return;

            bool removedActive = index == _activeClipIndex;
            _clipPaths.RemoveAt(index);
            if (index < _clipConfigs.Count) _clipConfigs.RemoveAt(index);
            if (index < _clipPresetNames.Count) _clipPresetNames.RemoveAt(index);
            if (index < _clipBatchSelected.Count) _clipBatchSelected.RemoveAt(index);
            if (index < _clipBatchEncodingPreset.Count) _clipBatchEncodingPreset.RemoveAt(index);

            if (_clipPaths.Count == 0)
            {
                _activeClipIndex = -1;
                RebuildClipTabs();
                await ApplyDetectedSourceAndRefreshAsync(string.Empty);
                return;
            }

            if (removedActive)
            {
                _activeClipIndex = Math.Min(index, _clipPaths.Count - 1);
                await ApplyDetectedSourceAndRefreshAsync(_clipPaths[_activeClipIndex]);
                RestoreClipConfig(_activeClipIndex);
                RestoreClipPresetCombo();
                RegenerateScript(showValidationError: false);
                if (TryValidateSourceSelection(out _))
                    await LoadScriptAsync();
            }
            else
            {
                if (index < _activeClipIndex)
                    _activeClipIndex--;
                RebuildClipTabs();
            }
        }

        private async void OnAddClipClick(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider is not { } sp) return;

            var currentSource = _config.Get("source");
            var suggestedLocation = await GetSuggestedStartLocationAsync(sp, currentSource);
            var filter = BuildSourceFileTypeFilter(currentSource);
            var results = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = GetUiText("PickSourceTitle"),
                AllowMultiple = true,
                SuggestedStartLocation = suggestedLocation,
                FileTypeFilter = filter
            });

            if (results.Count == 0) return;

            var newPaths = new List<string>();
            foreach (var file in results)
            {
                var filePath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(filePath)) continue;
                var displayPath = _sourceService.IsImageSource(filePath)
                    ? _sourceService.BuildImageSequenceSourcePath(filePath)
                    : filePath;
                newPaths.Add(displayPath);
            }

            if (newPaths.Count == 0) return;

            // Activate the first selected file
            await ApplyDetectedSourceAndRefreshAsync(newPaths[0]);

            // Add remaining files without activating
            for (int i = 1; i < newPaths.Count; i++)
            {
                var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(newPaths[i]));
                if (!_clipPaths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    _clipPaths.Add(normalized);
                    _clipConfigs.Add(CaptureClipConfig());
                    _clipPresetNames.Add(null);
                    _clipBatchSelected.Add(true);
                    _clipBatchEncodingPreset.Add(null);
                }
            }
            if (newPaths.Count > 1)
                RebuildClipTabs();
        }

        private void UpdateImageRangeFields(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

            int? min = null, max = null;
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (string.IsNullOrWhiteSpace(ext)
                    || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var stem = Path.GetFileNameWithoutExtension(file);
                if (!NumericStemRegex().IsMatch(stem) || !int.TryParse(stem, out var v)) continue;

                min = min.HasValue ? Math.Min(min.Value, v) : v;
                max = max.HasValue ? Math.Max(max.Value, v) : v;
            }

            if (min.HasValue && this.FindControl<TextBox>("img_start") is { } s) SetTextSafely(s, min.Value.ToString());
            if (max.HasValue && this.FindControl<TextBox>("img_end")   is { } e) SetTextSafely(e, max.Value.ToString());
        }

        private static async Task<IStorageFolder?> GetSuggestedStartLocationAsync(IStorageProvider sp, string? currentValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue)) return null;
            var path = File.Exists(currentValue) ? Path.GetDirectoryName(currentValue) : currentValue;
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
                ? await sp.TryGetFolderFromPathAsync(path)
                : null;
        }

        private string NormalizeSourceValue(string rawValue)
        {
            var normalized = _sourceService.NormalizeConfiguredPath(rawValue);
            return _sourceService.IsImageSource(normalized)
                ? _sourceService.BuildImageSequenceSourcePath(normalized)
                : normalized;
        }

        private bool IsImageSourceEnabled() =>
            bool.TryParse(_config.Get(UseImageConfigName), out var b) && b;

        #endregion

        #region Field change pipeline

        private async Task ApplyFieldChangeAsync(string key, string rawValue, bool showValidationError, bool refreshScriptPreview)
        {
            if (_isClosing || _isInitializing) return;

            rawValue ??= string.Empty;

            Func<string, string>? normalize = IsPathField(key) ? NormalizeSourceValue : null;
            var changed = _config.Set(key, rawValue, normalize);

            if (!changed && !string.Equals(key, "source", StringComparison.OrdinalIgnoreCase))
                return;

            // Rename the active clip's preset to a unique "persoN" when a filter value is manually changed
            if (changed && !_applyingPreset && !IsPathField(key)
                && !PresetService.ExcludedKeys.Contains(key)
                && _activeClipIndex >= 0 && _activeClipIndex < _clipPresetNames.Count)
            {
                var currentName = _clipPresetNames[_activeClipIndex];
                if (currentName is null || !currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase))
                {
                    _clipPresetNames[_activeClipIndex] = GetNextPersoName();
                    RestoreClipPresetCombo();
                    RebuildClipTabs();
                }

                // Deselect filter preset combo when a field belonging to that filter is manually changed
                if (FieldToFilterPresetCombo.TryGetValue(key, out var filterPresetCombo)
                    && this.FindControl<ComboBox>(filterPresetCombo) is { } filterCombo)
                {
                    _suppressTextEvents = true;
                    try { filterCombo.SelectedIndex = -1; }
                    finally { _suppressTextEvents = false; }
                    _config.Set(filterPresetCombo, string.Empty);
                }
            }

            if (key.Equals("source", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = _config.Get("source");
                _config.Set("film", normalized);
                _config.Set("img",  normalized);
            }
            else if (key.Equals("img_start", StringComparison.OrdinalIgnoreCase)
                  || key.Equals("img_end",   StringComparison.OrdinalIgnoreCase))
            {
            }

            RegenerateScript(showValidationError);

            if (!TryValidateSourceSelection(out var message))
            {
                if (showValidationError && !string.IsNullOrWhiteSpace(message))
                    await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), message);
                return;
            }

            if (!ShouldRefreshPreviewForField(key)) return;

            if (refreshScriptPreview)
            {
                _refreshDebouncer.Cancel();
                await LoadScriptAsync();
            }
            else
            {
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
            }
        }

        private void UpdateConfigurationValue(string name, string value, bool showValidationError = true) =>
            _ = ApplyFieldChangeAsync(name, value, showValidationError, refreshScriptPreview: false);

        private static bool ShouldRefreshPreviewForField(string key) =>
            ScriptService.TextFieldNames.Contains(key, StringComparer.OrdinalIgnoreCase)
            || ScriptService.BoolFieldNames.Contains(key, StringComparer.OrdinalIgnoreCase)
            || key.Equals(UseImageConfigName, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Script generation

        private void RegenerateScript(bool showValidationError = true)
        {
            if (!TryValidateSourceSelection(out var errorMessage))
            {
                if (showValidationError)
                    ShowSourceValidationError(errorMessage);
                return;
            }

            _scriptService.Generate(_config.Snapshot(), ViewModel.CurrentLanguageCode);
        }

        #endregion

        #region Option toggles & column visibility

        private readonly HashSet<string> _openParamPanels = new(StringComparer.Ordinal);

        private static readonly string[] AllParamPanels =
            ["CropParams", "DegrainParams", "DenoiseParams", "LumaParams", "GammacParams", "SharpParams"];

        private static readonly string[] AllExpandBtns =
            ["CropExpandBtn", "DegrainExpandBtn", "DenoiseExpandBtn", "LumaExpandBtn", "GammacExpandBtn", "SharpExpandBtn"];

        private static readonly Dictionary<string, string> ParamPanelToOptionToggle = new(StringComparer.Ordinal)
        {
            ["CropParams"] = "enable_crop",
            ["DegrainParams"] = "enable_degrain",
            ["DenoiseParams"] = "enable_denoise",
            ["LumaParams"] = "enable_luma_levels",
            ["GammacParams"] = "enable_gammac",
            ["SharpParams"] = "enable_sharp"
        };

        private bool IsParamPanelEnabled(string panelName) =>
            ParamPanelToOptionToggle.TryGetValue(panelName, out var optionName) && IsOptionEnabled(optionName);

        private void HideAllParamPanelsAndResetExpandButtons()
        {
            _openParamPanels.Clear();
            foreach (var name in AllParamPanels)
                if (this.FindControl<Control>(name) is { } p) p.IsVisible = false;

            foreach (var name in AllExpandBtns)
            {
                if (this.FindControl<Button>(name) is not { } b) continue;
                b.Content = "▶";
                b.Classes.Remove("active");
            }
        }

        private void UpdateParamsPlaceholderVisibility()
        {
            if (this.FindControl<TextBlock>("ParamsPlaceholder") is { } ph)
                ph.IsVisible = _openParamPanels.Count == 0;
        }

        private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string targetName) return;

            if (_openParamPanels.Remove(targetName))
            {
                // Panel is open → collapse it
                if (this.FindControl<Control>(targetName) is { } panel) panel.IsVisible = false;
                btn.Content = "▶";
                btn.Classes.Remove("active");
            }
            else
            {
                // Panel is closed → if filter is off, activate it first
                if (!IsParamPanelEnabled(targetName))
                {
                    if (ParamPanelToOptionToggle.TryGetValue(targetName, out var toggleName) &&
                        this.FindControl<Button>(toggleName) is { } toggleBtn)
                    {
                        toggleBtn.Tag = true;
                        UpdateToggleButtonPresentation(toggleBtn, true);
                        UpdateConfigurationValue(toggleName, "true", showValidationError: true);
                        UpdateOptionColumnVisibility();
                    }
                }

                _openParamPanels.Add(targetName);
                if (this.FindControl<Control>(targetName) is { } panel) panel.IsVisible = true;
                btn.Content = "▶";
                btn.Classes.Add("active");
            }

            UpdateParamsPlaceholderVisibility();
        }

        private void SnapToBottomOfScreen()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null) return;

            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            var w  = (int)Math.Ceiling(Bounds.Width * scaling);
            var h  = Math.Min((int)Math.Ceiling(Height * scaling), wa.Height);

            var x = Math.Clamp(wa.X + Math.Max(0, (wa.Width - w) / 2), wa.X, Math.Max(wa.X, wa.Right - w));
            var y = Math.Clamp(wa.Bottom - h - WindowBottomPadding, wa.Y, Math.Max(wa.Y, wa.Bottom - h));
            Position = new PixelPoint(x, y);
        }

        private bool IsSavedPositionVisible(int x, int y)
        {
            // Check if the top-left area of the title bar is on any screen.
            // Using the left edge (not the center) avoids false negatives when
            // the window is wide or positioned near the right edge of the screen.
            var titleBarLeft = new PixelPoint(x, y + 10);
            return Screens.All.Any(s => s.WorkingArea.Contains(titleBarLeft));
        }

        private void OnOptionToggleButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Name: { } name } btn) return;

            var updated = !(btn.Tag is bool v && v);
            btn.Tag = updated;
            UpdateToggleButtonPresentation(btn, updated);
            UpdateConfigurationValue(name, updated.ToString().ToLowerInvariant(), showValidationError: true);

            if (IsOptionToggle(name))
            {
                UpdateOptionColumnVisibility();
                SyncActiveParamPanelWithFilters();
            }
        }

        private void SyncActiveParamPanelWithFilters()
        {
            if (_openParamPanels.Count == 0) return;

            var toClose = _openParamPanels.Where(p => !IsParamPanelEnabled(p)).ToList();
            foreach (var panel in toClose)
            {
                _openParamPanels.Remove(panel);
                if (this.FindControl<Control>(panel) is { } c) c.IsVisible = false;
                var idx = Array.IndexOf(AllParamPanels, panel);
                if (idx >= 0 && idx < AllExpandBtns.Length &&
                    this.FindControl<Button>(AllExpandBtns[idx]) is { } btn)
                {
                    btn.Content = "▶";
                    btn.Classes.Remove("active");
                }
            }

            UpdateParamsPlaceholderVisibility();
        }

        private void UpdateOptionColumnVisibility()
        {
            _mainGrid ??= this.FindControl<Grid>("MainGrid");

            var crop    = IsOptionEnabled("enable_crop");
            var degrain = IsOptionEnabled("enable_degrain");
            var denoise = IsOptionEnabled("enable_denoise");
            var luma    = IsOptionEnabled("enable_luma_levels");
            var gammac  = IsOptionEnabled("enable_gammac");
            var sharp   = IsOptionEnabled("enable_sharp");

            SetColumnEnabled(crop,              "CropScrollViewer");
            SetColumnEnabled(crop,              "CropSplitterBefore", "CropSplitterAfter");
            SetColumnEnabled(true,              "DegrainColumn");
            SetColumnEnabled(degrain,           "DegrainScrollViewer");
            SetColumnEnabled(degrain || denoise,"DegrainDenoiseSplitter");
            SetColumnEnabled(true,              "DenoiseColumn");
            SetColumnEnabled(denoise,           "DenoiseScrollViewer");
            SetColumnEnabled(luma,              "LumaSplitterBefore", "LumaLevelsScrollViewer");
            SetColumnEnabled(luma && gammac,    "LumaGammacSplitter");
            SetColumnEnabled(gammac,            "GammacScrollViewer");
            SetColumnEnabled(gammac && sharp,   "GammacSplitterAfter");
            SetColumnEnabled(sharp,             "SharpenScrollViewer");
        }

        private bool IsOptionEnabled(string name) => GetOptionToggleValue(name);

        private bool GetOptionToggleValue(string name)
        {
            if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) return false;
            return btn.Tag is bool v && v;
        }

        private void SetOptionToggleValue(string name, bool isEnabled)
        {
            if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) return;
            btn.Tag = isEnabled;
            UpdateToggleButtonPresentation(btn, isEnabled);
        }

        private static void UpdateToggleButtonPresentation(Button btn, bool isEnabled)
        {
            var label = btn.Name is { Length: > 0 } n && OptionButtonLabels.TryGetValue(n, out var l) ? l : btn.Name ?? string.Empty;
            btn.Content     = label;
            btn.Background  = new SolidColorBrush(Color.Parse(isEnabled ? "#35C156" : "#3B4C64"));
            btn.BorderBrush = new SolidColorBrush(Color.Parse("#3B4C64"));
            btn.Foreground  = Brushes.White;
        }

        private void SetColumnEnabled(bool isEnabled, params string[] names)
        {
            foreach (var n in names)
                if (this.FindControl<Control>(n) is { } c)
                    c.IsEnabled = isEnabled;
        }

        private static bool IsOptionToggle(string name) =>
            name is "enable_crop" or "enable_degrain" or "enable_denoise"
                 or "enable_luma_levels" or "enable_gammac" or "enable_sharp";

        private static string GetBoolControlName(string name) =>
            string.Equals(name, "Show", StringComparison.OrdinalIgnoreCase) ? "ShowPreview" : name;

        #endregion

        #region Player / script preview

        // ── Barre de transport ──────────────────────────────────────────
        private void OnVdbBeginningClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.Stop();

        private void OnVdbPrevFrameClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.FrameBackStep();

        private void OnVdbPlayClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.TogglePlayPause();

        private void OnVdbNextFrameClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.FrameStep();

        private static readonly double[] PlaybackSpeeds = [0.25, 0.5, 1.0];
        private int _speedIndex = 2; // default 1x

        private void OnSpeedClick(object? sender, RoutedEventArgs e)
        {
            _speedIndex = (_speedIndex + 1) % PlaybackSpeeds.Length;
            var speed = PlaybackSpeeds[_speedIndex];
            _mpvService?.SetSpeed(speed);
            if (this.FindControl<Button>("SpeedBtn") is { } btn)
                btn.Content = speed < 1.0 ? $"{speed:G}x" : "1x";
        }

        private bool _halfRes;
        private void OnHalfResClick(object? sender, RoutedEventArgs e)
        {
            _halfRes = !_halfRes;
            if (this.FindControl<Button>("HalfResBtn") is { } btn)
            {
                btn.Background = new SolidColorBrush(Color.Parse(_halfRes ? "#35C156" : "#3B4C64"));
                btn.Foreground = Brushes.White;
            }
            UpdateConfigurationValue("preview_half", _halfRes.ToString().ToLowerInvariant());
        }

        private bool _isEncoding;
        private bool _recordOpen;
        private void InitRecordPanel()
        {
            if (this.FindControl<TextBox>("RecordDir") is { } tb)
            {
                tb.LostFocus += (_, _) => UpdateDiskSpaceLabel(tb.Text);
                tb.TextChanged += (_, _) => UpdateDiskSpaceLabel(tb.Text);
            }

            if (this.FindControl<ComboBox>("RecordEncoder") is { } enc)
                enc.SelectionChanged += OnRecordEncoderChanged;

            if (this.FindControl<ComboBox>("RecordQualityMode") is { } qm)
                qm.SelectionChanged += OnRecordQualityModeChanged;

            if (this.FindControl<ComboBox>("RecordChroma") is { } ch)
                ch.SelectionChanged += OnRecordChromaChanged;

            if (this.FindControl<ComboBox>("RecordContainer") is { } ct)
                ct.SelectionChanged += (_, _) => OnEncodingSettingChanged();

            if (this.FindControl<ComboBox>("RecordResize") is { } rs)
                rs.SelectionChanged += (_, _) => { UpdateBitrateHint(); OnEncodingSettingChanged(); };

            if (this.FindControl<TextBox>("RecordBitrate") is { } brTb)
            {
                brTb.LostFocus += (_, _) => OnBitrateValidated();
                brTb.KeyDown += (_, args) =>
                {
                    if (args.Key == Avalonia.Input.Key.Enter) OnBitrateValidated();
                };
            }

            if (this.FindControl<Slider>("RecordCrfSlider") is { } slider)
            {
                slider.PropertyChanged += (_, args) =>
                {
                    if (args.Property == Slider.ValueProperty &&
                        this.FindControl<TextBlock>("RecordCrfValue") is { } lbl)
                        lbl.Text = ((int)slider.Value).ToString();
                };

                var crfDragging = false;
                slider.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
                    crfDragging = true;
                    e.Pointer.Capture(slider);
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerMovedEvent, (_, e) =>
                {
                    if (!crfDragging) return;
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (!crfDragging) return;
                    crfDragging = false;
                    e.Pointer.Capture(null);
                    e.Handled = true;
                    OnEncodingSettingChanged();
                }, RoutingStrategies.Bubble, handledEventsToo: true);
            }

            RefreshEncodingPresetCombo();

            if (this.FindControl<ComboBox>("RecordPresetCombo") is { } presetCombo)
                presetCombo.SelectionChanged += OnRecordPresetSelectionChanged;
        }

        private void OnRecordPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            var name = combo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = _encodingPresetService.LoadPresets()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset?.Values is null) return;

            _isLoadingEncodingPreset = true;
            try { ApplyEncodingValues(preset.Values); }
            finally { _isLoadingEncodingPreset = false; }
        }

        private void OnRecordEncoderChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordEncoder") is not { } enc) return;
            var tag = (enc.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var isImageSeq = tag is "tiff" or "png";
            var isLossless = tag is "ffv1" or "utvideo" or "tiff" or "png";

            if (this.FindControl<ComboBox>("RecordContainer") is { } cnt)
                cnt.IsEnabled = !isImageSeq;

            // Hide quality/chroma controls for lossless encoders
            if (this.FindControl<Grid>("RecordQualityPanel") is { } qp)
                qp.IsVisible = !isLossless;
            if (this.FindControl<StackPanel>("RecordChromaPanel") is { } cp)
                cp.IsVisible = !isLossless;

            OnEncodingSettingChanged();
        }

        private void OnRecordQualityModeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordQualityMode") is not { } qm) return;
            var tag = (qm.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var isCrf = tag == "crf";
            if (this.FindControl<StackPanel>("RecordCrfPanel") is { } crfP)
                crfP.IsVisible = isCrf;
            if (this.FindControl<StackPanel>("RecordBitratePanel") is { } brP)
                brP.IsVisible = !isCrf;

            OnEncodingSettingChanged();
        }

        /// <summary>
        /// Chroma multiplier relative to 4:2:0.
        /// 4:2:0 = 12 bits/pixel, 4:2:2 = 16 bits/pixel (×1.33), 4:4:4 = 24 bits/pixel (×2.0).
        /// </summary>
        private static double ChromaBitrateMultiplier(string chroma) => chroma switch
        {
            "yuv422p" => 16.0 / 12.0,  // ×1.33
            "yuv444p" => 24.0 / 12.0,  // ×2.00
            _         => 1.0           // 4:2:0 baseline
        };

        /// <summary>
        /// Computes a recommended minimum bitrate (Mb/s) based on resolution and chroma.
        /// Base reference: ~15 Mb/s for 1080p 4:2:0 (good quality with x264/x265).
        /// Scales linearly with pixel count and chroma data ratio.
        /// </summary>
        private int ComputeMinBitrate()
        {
            var chroma = (this.FindControl<ComboBox>("RecordChroma")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "yuv420p";
            var resize = (this.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original";

            // Reference: 1080p = 1920×1080 = 2_073_600 pixels → 15 Mb/s for 4:2:0
            double refPixels = 2_073_600;
            double refBitrate = 15.0;

            double pixels = resize switch
            {
                "1080" => 1920.0 * 1080,
                "720"  => 1280.0 * 720,
                "576"  => 1024.0 * 576,
                "480"  => 854.0 * 480,
                _      => 1920.0 * 1080  // original: assume ~1080p as safe default
            };

            var bitrate = refBitrate * (pixels / refPixels) * ChromaBitrateMultiplier(chroma);
            return Math.Max(1, (int)Math.Ceiling(bitrate));
        }

        private bool _syncingBitrateChroma;

        private void UpdateBitrateHint()
        {
            if (_syncingBitrateChroma) return;
            if (this.FindControl<TextBox>("RecordBitrate") is not { } tb) return;
            var min = ComputeMinBitrate();
            tb.Watermark = $"{min}";
        }

        private void OnRecordChromaChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_syncingBitrateChroma) return;
            _syncingBitrateChroma = true;
            try
            {
                UpdateBitrateHint();
                // If current bitrate is below the new minimum, bump it up
                if (this.FindControl<TextBox>("RecordBitrate") is { } tb
                    && int.TryParse(tb.Text?.Trim(), out var current))
                {
                    var min = ComputeMinBitrate();
                    if (current < min) tb.Text = min.ToString();
                }
            }
            finally { _syncingBitrateChroma = false; }

            OnEncodingSettingChanged();
        }

        /// <summary>
        /// When the user validates a bitrate (Enter or LostFocus), adjust
        /// chroma to the highest level the bitrate can support.
        /// Does NOT touch the bitrate value itself.
        /// </summary>
        private void OnBitrateValidated()
        {
            if (_syncingBitrateChroma) return;
            _syncingBitrateChroma = true;
            try
            {
                if (this.FindControl<TextBox>("RecordBitrate") is not { } tb) return;
                if (this.FindControl<ComboBox>("RecordChroma") is not { } combo) return;
                if (!int.TryParse(tb.Text?.Trim(), out var bitrate) || bitrate <= 0) return;

                var resize = (this.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original";
                double pixels = resize switch
                {
                    "1080" => 1920.0 * 1080,
                    "720"  => 1280.0 * 720,
                    "576"  => 1024.0 * 576,
                    "480"  => 854.0 * 480,
                    _      => 1920.0 * 1080
                };
                double refPixels = 2_073_600;
                double refBitrate = 15.0;

                // Find the best chroma the bitrate can afford (try highest first)
                string[] chromaOptions = ["yuv444p", "yuv422p", "yuv420p"];
                string bestChroma = "yuv420p";
                foreach (var ch in chromaOptions)
                {
                    var minBr = (int)Math.Ceiling(refBitrate * (pixels / refPixels) * ChromaBitrateMultiplier(ch));
                    if (bitrate >= minBr) { bestChroma = ch; break; }
                }

                // Only change if different from current
                var currentChroma = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (currentChroma == bestChroma) return;

                foreach (var item in combo.Items)
                    if (item is ComboBoxItem ci && ci.Tag?.ToString() == bestChroma)
                    {
                        combo.SelectedItem = ci;
                        break;
                    }
            }
            finally { _syncingBitrateChroma = false; }

            OnEncodingSettingChanged();
        }

        // ── Encoding presets ──

        private static readonly string[] EncodingPresetKeys =
            ["encoder", "container", "quality_mode", "crf", "bitrate", "chroma", "resize", "output_dir"];

        private Dictionary<string, string> CaptureEncodingValues()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["encoder"]      = (this.FindControl<ComboBox>("RecordEncoder")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "x264";
            d["container"]    = (this.FindControl<ComboBox>("RecordContainer")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mkv";
            d["quality_mode"] = (this.FindControl<ComboBox>("RecordQualityMode")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "crf";
            d["crf"]          = ((int)(this.FindControl<Slider>("RecordCrfSlider")?.Value ?? 18)).ToString();
            d["bitrate"]      = this.FindControl<TextBox>("RecordBitrate")?.Text?.Trim() ?? "20";
            d["chroma"]       = (this.FindControl<ComboBox>("RecordChroma")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "yuv420p";
            d["resize"]       = (this.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original";
            d["output_dir"]   = this.FindControl<TextBox>("RecordDir")?.Text?.Trim() ?? "";
            return d;
        }

        private void ApplyEncodingValues(Dictionary<string, string> vals)
        {
            void SelectByTag(string controlName, string? tag)
            {
                if (this.FindControl<ComboBox>(controlName) is not { } combo || tag is null) return;
                foreach (var item in combo.Items)
                    if (item is ComboBoxItem ci && ci.Tag?.ToString() == tag) { combo.SelectedItem = ci; break; }
            }

            if (vals.TryGetValue("encoder", out var enc))      SelectByTag("RecordEncoder", enc);
            if (vals.TryGetValue("container", out var cnt))    SelectByTag("RecordContainer", cnt);
            if (vals.TryGetValue("quality_mode", out var qm))  SelectByTag("RecordQualityMode", qm);
            if (vals.TryGetValue("crf", out var crf) && int.TryParse(crf, out var crfVal))
            {
                if (this.FindControl<Slider>("RecordCrfSlider") is { } slider) slider.Value = crfVal;
            }
            if (vals.TryGetValue("bitrate", out var br))
            {
                if (this.FindControl<TextBox>("RecordBitrate") is { } tb) tb.Text = br;
            }
            if (vals.TryGetValue("chroma", out var ch))        SelectByTag("RecordChroma", ch);
            if (vals.TryGetValue("resize", out var rs))        SelectByTag("RecordResize", rs);
            if (vals.TryGetValue("output_dir", out var dir) && !string.IsNullOrWhiteSpace(dir))
            {
                if (this.FindControl<TextBox>("RecordDir") is { } dirTb) dirTb.Text = dir;
            }
        }

        private void RefreshEncodingPresetCombo()
        {
            if (this.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
            var list = _encodingPresetService.LoadPresets()
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Name)
                .ToList();
            combo.ItemsSource = list;
        }

        private void OnRecordPresetSaveClick(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
            // Use typed text (editable combo) or selected item
            var name = (combo.Text ?? combo.SelectedItem?.ToString())?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var presets = _encodingPresetService.LoadPresets();
            var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                existing.Values = CaptureEncodingValues();
            else
                presets.Add(new Preset(name, CaptureEncodingValues()));

            _encodingPresetService.SavePresets(presets);

            // Guard: refreshing the combo + re-selecting triggers OnRecordPresetSelectionChanged
            // which would re-apply values and fire visibility handlers (hiding controls).
            _isLoadingEncodingPreset = true;
            try
            {
                RefreshEncodingPresetCombo();
                combo.SelectedItem = name;
            }
            finally { _isLoadingEncodingPreset = false; }
        }

        private void OnRecordPresetLoadClick(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
            var name = combo.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = _encodingPresetService.LoadPresets()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset is null) return;
            _isLoadingEncodingPreset = true;
            try { ApplyEncodingValues(preset.Values); }
            finally { _isLoadingEncodingPreset = false; }
        }

        private void OnRecordPresetDeleteClick(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
            var name = combo.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;

            var presets = _encodingPresetService.LoadPresets();
            presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            _encodingPresetService.SavePresets(presets);

            _isLoadingEncodingPreset = true;
            try
            {
                RefreshEncodingPresetCombo();
                combo.SelectedItem = null;
                combo.Text = string.Empty;
            }
            finally { _isLoadingEncodingPreset = false; }
        }

        private async void OnEncodingSettingChanged()
        {
            if (_isLoadingEncodingPreset || _isInitializing || _isClosing) return;
            if (_pendingEncodingPresetPrompt) return;
            if (this.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
            var presetName = combo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(presetName)) return;

            // Auto-save without asking if user checked "don't ask again"
            if (_autoSaveEncodingPreset)
            {
                SaveEncodingPresetByName(presetName);
                return;
            }

            _pendingEncodingPresetPrompt = true;
            try
            {
                var result = await ShowEncodingPresetModifiedDialog(presetName);
                if (result == true)
                {
                    SaveEncodingPresetByName(presetName);
                }
                else if (result == false)
                {
                    // Clear preset selection to avoid confusion
                    combo.SelectedItem = null;
                }
                // null = dialog closed without choosing → do nothing
            }
            finally { _pendingEncodingPresetPrompt = false; }
        }

        private void SaveEncodingPresetByName(string name)
        {
            var presets = _encodingPresetService.LoadPresets();
            var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                existing.Values = CaptureEncodingValues();
            else
                presets.Add(new Preset(name, CaptureEncodingValues()));
            _encodingPresetService.SavePresets(presets);
        }

        /// <summary>
        /// Shows a dialog asking if the user wants to save changes to the active encoding preset.
        /// Returns true=save, false=discard, null=cancelled.
        /// </summary>
        private async Task<bool?> ShowEncodingPresetModifiedDialog(string presetName)
        {
            bool? dialogResult = null;

            var checkBox = new CheckBox
            {
                Content = GetUiText("PresetModifiedDontAsk"),
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
                FontSize = 11
            };

            var yesButton = new Button
            {
                Content = GetUiText("YesButton"),
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            var noButton = new Button
            {
                Content = GetUiText("NoButton"),
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            var dialog = new Window
            {
                Title = GetUiText("PresetModifiedTitle"),
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = string.Format(GetUiText("PresetModifiedMsg"), presetName),
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 400
                        },
                        checkBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children = { yesButton, noButton }
                        }
                    }
                }
            };

            yesButton.Click += (_, _) =>
            {
                dialogResult = true;
                if (checkBox.IsChecked == true)
                    _autoSaveEncodingPreset = true;
                dialog.Close();
            };
            noButton.Click += (_, _) =>
            {
                dialogResult = false;
                dialog.Close();
            };

            await dialog.ShowDialog(this);
            return dialogResult;
        }

        private void OnRecordClick(object? sender, RoutedEventArgs e)
        {
            _recordOpen = !_recordOpen;
            if (this.FindControl<Button>("RecordBtn") is { } btn)
            {
                btn.Background = new SolidColorBrush(Color.Parse(_recordOpen ? "#C62828" : "#3B4C64"));
                btn.Foreground = Brushes.White;
            }
            if (this.FindControl<Border>("RecordOverlay") is { } overlay)
                overlay.IsVisible = _recordOpen;
            if (_recordOpen)
            {
                RebuildBatchClipList();
                UpdateDiskSpaceLabel(this.FindControl<TextBox>("RecordDir")?.Text);
            }
        }

        private void RebuildBatchClipList()
        {
            if (this.FindControl<StackPanel>("BatchClipList") is not { } panel) return;
            panel.Children.Clear();

            // Sync batch selection list
            while (_clipBatchSelected.Count < _clipPaths.Count) _clipBatchSelected.Add(true);

            var monoFont = new FontFamily("Consolas,Cascadia Code,monospace");

            for (int i = 0; i < _clipPaths.Count; i++)
            {
                var index = i;
                var filename = Path.GetFileName(_clipPaths[i]);
                if (string.IsNullOrWhiteSpace(filename)) filename = _clipPaths[i];

                var presetName = _clipPresetNames.Count > i ? _clipPresetNames[i] : null;
                var presetSuffix = presetName is not null ? $"  [{presetName}]" : "";

                var cb = new CheckBox
                {
                    IsChecked = _clipBatchSelected[i],
                    Content = filename + presetSuffix,
                    FontSize = 11,
                    FontFamily = monoFont,
                    Foreground = new SolidColorBrush(Color.Parse("#C8D0E0")),
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                cb.Click += (_, _) =>
                {
                    if (index < _clipBatchSelected.Count)
                        _clipBatchSelected[index] = cb.IsChecked == true;
                };
                panel.Children.Add(cb);
            }

            // Update localized labels
            if (this.FindControl<TextBlock>("BatchClipListLabel") is { } lbl)
                lbl.Text = GetUiText("BatchClipListLabel");
            if (this.FindControl<CheckBox>("BatchSelectAllCheck") is { } allCb)
                allCb.Content = GetUiText("BatchSelectAll");
            if (this.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
                shutCb.Content = GetUiText("ShutdownCheckBox");
        }

        private void OnBatchSelectAllClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox allCb) return;
            var selected = allCb.IsChecked == true;
            for (int i = 0; i < _clipBatchSelected.Count; i++)
                _clipBatchSelected[i] = selected;
            RebuildBatchClipList();
            // Re-sync the select-all checkbox state
            if (this.FindControl<CheckBox>("BatchSelectAllCheck") is { } cb)
                cb.IsChecked = selected;
        }

        private async void OnRecordDirPickClick(object? sender, RoutedEventArgs e)
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = GetUiText("RecordDirPickTitle"),
                    AllowMultiple = false
                });
            if (folder.Count > 0 && this.FindControl<TextBox>("RecordDir") is { } tb)
            {
                try
                {
                    var picked = folder[0].Path.LocalPath;
                    // Normalize root drives: "E:" → "E:\"
                    if (picked.Length == 2 && picked[1] == ':')
                        picked += "\\";
                    tb.Text = picked;
                    UpdateDiskSpaceLabel(tb.Text);
                }
                catch
                {
                    // Fallback: TryGetLocalPath may work when Path.LocalPath throws on root drives
                    if (folder[0].TryGetLocalPath() is { } fallback)
                    {
                        if (fallback.Length == 2 && fallback[1] == ':')
                            fallback += "\\";
                        tb.Text = fallback;
                        UpdateDiskSpaceLabel(tb.Text);
                    }
                }
            }
        }

        private void OnRecordDirOpenClick(object? sender, RoutedEventArgs e)
        {
            var dir = this.FindControl<TextBox>("RecordDir")?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }

        private void UpdateDiskSpaceLabel(string? dirPath)
        {
            if (this.FindControl<TextBlock>("RecordDiskSpace") is not { } lbl) return;
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                lbl.Text = "";
                return;
            }
            try
            {
                var root = Path.GetPathRoot(dirPath);
                if (string.IsNullOrWhiteSpace(root)) { lbl.Text = ""; return; }
                var drive = new DriveInfo(root);
                if (!drive.IsReady) { lbl.Text = ""; return; }
                var freeMb = drive.AvailableFreeSpace / (1024.0 * 1024.0);
                lbl.Text = freeMb >= 1024
                    ? $"({freeMb / 1024.0:F1} Go)"
                    : $"({freeMb:F0} Mo)";
            }
            catch { lbl.Text = ""; }
        }

        private Process? _encodingProcess;
        private CancellationTokenSource? _encodingCts;
        private string _lastStderrLine = string.Empty;
        private readonly List<string> _stderrLines = new();

        /// <summary>Builds ffmpeg arguments from encoding values dictionary.</summary>
        private readonly HashSet<string> _usedOutputPaths = new(StringComparer.OrdinalIgnoreCase);

        private (string ffArgs, string outputPath)? BuildFfmpegArgs(
            Dictionary<string, string> encVals, string renderScriptPath, string outputDir, string sourceFileName)
        {
            var encoder   = encVals.GetValueOrDefault("encoder", "x264");
            var container = encVals.GetValueOrDefault("container", "mkv");
            var qualityMode = encVals.GetValueOrDefault("quality_mode", "crf");
            var crfValue  = encVals.GetValueOrDefault("crf", "18");
            var bitrateText = encVals.GetValueOrDefault("bitrate", "20");
            var chroma    = encVals.GetValueOrDefault("chroma", "yuv420p");
            var resize    = encVals.GetValueOrDefault("resize", "original");

            var scaleFilter = resize != "original" ? $"-vf scale=-2:{resize}" : "";
            var isImageSeq = encoder is "tiff" or "png";
            // Trial limit applied directly in compiled code (not in script)
            var durationLimit = TrialMaxSeconds > 0 ? $"-t {TrialMaxSeconds}" : "";
            // Use -f avisynth to explicitly tell ffmpeg to use the AviSynth demuxer
            var inputArgs = $"-f avisynth -i \"{renderScriptPath}\"";

            string outputPath;
            string ffArgs;

            if (isImageSeq)
            {
                var ext = encoder == "tiff" ? "tif" : "png";
                var seqDir = Path.Combine(outputDir, sourceFileName);
                try { Directory.CreateDirectory(seqDir); } catch { return null; }
                outputPath = Path.Combine(seqDir, $"%05d.{ext}");
                ffArgs = $"-progress pipe:2 {inputArgs} {durationLimit} {scaleFilter} \"{outputPath}\"";
            }
            else
            {
                // Deduplicate output filenames (e.g. two clips named "video.avi" in different folders)
                var baseName = sourceFileName;
                outputPath = Path.Combine(outputDir, $"{baseName}.{container}");
                int dup = 2;
                while (_usedOutputPaths.Contains(outputPath))
                {
                    outputPath = Path.Combine(outputDir, $"{baseName}_{dup}.{container}");
                    dup++;
                }
                _usedOutputPaths.Add(outputPath);

                var qualityArgs = "";
                if (encoder is "x264" or "x265")
                {
                    qualityArgs = qualityMode == "bitrate"
                        ? $"-b:v {bitrateText}M"
                        : $"-crf {crfValue}";
                }

                var codecArgs = encoder switch
                {
                    "x264"    => $"-c:v libx264 {qualityArgs} -preset medium -pix_fmt {chroma}",
                    "x265"    => $"-c:v libx265 {qualityArgs} -preset medium -pix_fmt {chroma}",
                    "ffv1"    => "-c:v ffv1 -level 3 -slicecrc 1",
                    "utvideo" => "-c:v utvideo",
                    "prores"  => $"-c:v prores_ks -profile:v 3 -pix_fmt {chroma}",
                    _         => $"-c:v libx264 {qualityArgs} -preset medium -pix_fmt {chroma}"
                };
                // -movflags +faststart: move moov atom to start for seekable MP4/MOV
                var movFlags = container is "mp4" or "mov" ? "-movflags +faststart" : "";
                ffArgs = $"-progress pipe:2 {inputArgs} {durationLimit} {scaleFilter} {codecArgs} {movFlags} -y \"{outputPath}\"";
            }

            return (ffArgs, outputPath);
        }

        /// <summary>Prepares config for a specific clip and regenerates the AVS script.</summary>
        private string? PrepareClipForEncoding(int clipIndex)
        {
            if (clipIndex < 0 || clipIndex >= _clipPaths.Count) return null;

            var sourcePath = _clipPaths[clipIndex];
            var normalized = _sourceService.NormalizeConfiguredPath(sourcePath);

            // Set source
            _config.Set("source", normalized);
            _config.Set("film", normalized);
            _config.Set("img", normalized);

            // Restore filter config
            if (clipIndex < _clipConfigs.Count)
            {
                var clipCfg = _clipConfigs[clipIndex];
                foreach (var kv in clipCfg)
                    _config.Set(kv.Key, kv.Value);
            }

            // Reset crop
            foreach (var cropField in new[] { "Crop_L", "Crop_T", "Crop_R", "Crop_B" })
                _config.Set(cropField, "0");
            _config.Set("enable_crop", "false");

            // Set source type
            var isFilm = _sourceService.IsVideoSource(sourcePath);
            _config.Set("use_img", (!isFilm).ToString().ToLowerInvariant());

            // Generate script
            _scriptService.Generate(_config.Snapshot(), ViewModel.CurrentLanguageCode);

            return GenerateRenderScript();
        }

        private async void OnRecordStartClick(object? sender, RoutedEventArgs e)
        {
            // If encoding is running, cancel it
            if (_encodingProcess is { HasExited: false })
            {
                _encodingCts?.Cancel();
                try { _encodingProcess.Kill(entireProcessTree: true); } catch { }
                SetRecordStartButtonState(idle: true);
                return;
            }

            var dir = this.FindControl<TextBox>("RecordDir")?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(dir))
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordNoDirError"));
                return;
            }

            // Collect selected clips
            var jobs = new List<int>();
            for (int i = 0; i < _clipPaths.Count; i++)
            {
                if (i < _clipBatchSelected.Count && _clipBatchSelected[i])
                    jobs.Add(i);
            }
            if (jobs.Count == 0)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordNoClipSelected"));
                return;
            }

            // Ensure output dir exists
            try
            {
                // Root paths like "E:\" already exist — CreateDirectory is fine
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), ex.Message);
                return;
            }

            // Find ffmpeg
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath is null)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordFfmpegNotFound"));
                return;
            }

            // Verify ffmpeg has AviSynth support
            var avsSupported = await CheckFfmpegAviSynthSupport(ffmpegPath);
            if (!avsSupported)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"),
                    "ffmpeg does not support AviSynth input.\nPlease use a ffmpeg build with AviSynth support (e.g. gyan.dev full build).");
                return;
            }

            var shutdownAfter = this.FindControl<CheckBox>("ShutdownCheckBox")?.IsChecked == true;
            var defaultEncoding = CaptureEncodingValues();

            // Save current state for restoration after batch
            SaveActiveClipConfig();
            var savedConfig = _config.Snapshot();
            var savedClipIndex = _activeClipIndex;

            SetRecordStartButtonState(idle: false);
            SetRecordProgressVisible(true);
            SetEncodingLock(true);
            _usedOutputPaths.Clear();
            _encodingCts = new CancellationTokenSource();

            int successCount = 0;
            var errors = new List<string>();

            try
            {
                for (int jobIdx = 0; jobIdx < jobs.Count; jobIdx++)
                {
                    if (_encodingCts.IsCancellationRequested) break;

                    var clipIndex = jobs[jobIdx];
                    var sourceFileName = Path.GetFileNameWithoutExtension(_clipPaths[clipIndex]);
                    var batchLabel = string.Format(GetUiText("BatchProgress"), jobIdx + 1, jobs.Count);
                    UpdateRecordProgress(0, $"{batchLabel} — {sourceFileName}");

                    // Prepare clip's AVS script
                    var renderScriptPath = PrepareClipForEncoding(clipIndex);
                    if (renderScriptPath is null || !File.Exists(renderScriptPath))
                    {
                        DebugLog($"Render script missing for clip {clipIndex}: {renderScriptPath}");
                        errors.Add($"{sourceFileName}: {GetUiText("RecordNoScriptError")}");
                        continue;
                    }
                    DebugLog($"Render script ready: {renderScriptPath} ({new FileInfo(renderScriptPath).Length} bytes)");

                    // Build ffmpeg args
                    var result = BuildFfmpegArgs(defaultEncoding, renderScriptPath, dir, sourceFileName);
                    if (result is null)
                    {
                        errors.Add($"{sourceFileName}: failed to build output path");
                        continue;
                    }

                    var (ffArgs, outputPath) = result.Value;
                    _lastStderrLine = string.Empty;
                    _stderrLines.Clear();
                    DebugLog($"ffmpeg: {ffmpegPath} {ffArgs}");

                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = ffmpegPath,
                            Arguments = ffArgs,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardError = true,
                            WorkingDirectory = Path.GetDirectoryName(renderScriptPath) ?? ""
                        };

                        // Ensure AviSynth DLLs sit next to ffmpeg.exe
                        // (LoadLibrary searches app dir, not PATH)
                        EnsureAviSynthNextToFfmpeg(ffmpegPath);

                        _encodingProcess = new Process
                        {
                            StartInfo = psi,
                            EnableRaisingEvents = true
                        };

                        _encodingProcess.Start();

                        // Use trial limit as known duration for progress bar (0 = unlimited)
                        var stderrTask = ReadFfmpegStderrAsync(
                            _encodingProcess.StandardError, TrialMaxSeconds, _encodingCts.Token,
                            batchLabel);

                        await _encodingProcess.WaitForExitAsync(_encodingCts.Token);
                        await stderrTask;

                        if (_encodingProcess.ExitCode == 0)
                        {
                            successCount++;
                        }
                        else
                        {
                            // Extract meaningful error lines from stderr
                            var errorLines = _stderrLines
                                .Where(l => !l.StartsWith("out_time", StringComparison.Ordinal)
                                         && !l.StartsWith("bitrate=", StringComparison.Ordinal)
                                         && !l.StartsWith("total_size=", StringComparison.Ordinal)
                                         && !l.StartsWith("speed=", StringComparison.Ordinal)
                                         && !l.StartsWith("progress=", StringComparison.Ordinal)
                                         && !l.StartsWith("frame=", StringComparison.Ordinal)
                                         && !l.StartsWith("fps=", StringComparison.Ordinal)
                                         && !l.StartsWith("stream_", StringComparison.Ordinal)
                                         && !l.StartsWith("dup_frames=", StringComparison.Ordinal)
                                         && !l.StartsWith("drop_frames=", StringComparison.Ordinal)
                                         && !string.IsNullOrWhiteSpace(l))
                                .TakeLast(5)
                                .ToList();
                            var msg = errorLines.Count > 0
                                ? string.Join("\n", errorLines)
                                : $"ffmpeg exit code {_encodingProcess.ExitCode}";
                            DebugLog($"ffmpeg error for {sourceFileName}:\n{string.Join("\n", _stderrLines)}");
                            errors.Add($"{sourceFileName}: {msg}");
                        }
                    }
                    finally
                    {
                        _encodingProcess?.Dispose();
                        _encodingProcess = null;
                    }
                }
            }
            catch (OperationCanceledException) { /* user cancelled */ }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), ex.Message);
            }
            finally
            {
                _encodingCts = null;
                SetRecordStartButtonState(idle: true);
                SetRecordProgressVisible(false);
                SetEncodingLock(false);

                // Restore original state
                _config.ReplaceAll(savedConfig);
                if (savedClipIndex >= 0 && savedClipIndex < _clipPaths.Count)
                {
                    _activeClipIndex = savedClipIndex;
                    RestoreClipConfig(_activeClipIndex);
                }
                RegenerateScript(showValidationError: false);
            }

            // Show result
            UpdateDiskSpaceLabel(dir);
            var doneMsg = string.Format(GetUiText("BatchDoneMsg"), successCount, jobs.Count);
            if (errors.Count > 0)
                doneMsg += "\n\n" + string.Join("\n", errors);
            await _dialogService.ShowErrorAsync(this, GetUiText("RecordBtn"), doneMsg);

            if (shutdownAfter && successCount > 0 && errors.Count == 0)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("RecordBtn"), GetUiText("BatchShutdownMsg"));
                Process.Start("shutdown", "/s /t 60");
            }
        }

        private async Task ReadFfmpegStderrAsync(System.IO.StreamReader stderr, double totalDuration, CancellationToken ct, string? batchLabel = null)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await stderr.ReadLineAsync(ct);
                    if (line is null) break; // EOF

                    _lastStderrLine = line;
                    // Keep last 30 lines for error diagnostics
                    _stderrLines.Add(line);
                    if (_stderrLines.Count > 30) _stderrLines.RemoveAt(0);

                    // Parse "-progress pipe:2" output: "out_time_us=<microseconds>"
                    if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
                    {
                        if (long.TryParse(line.AsSpan("out_time_us=".Length), out var us) && us >= 0)
                        {
                            var seconds = us / 1_000_000.0;
                            var elapsed = TimeSpan.FromSeconds(seconds);
                            string label;
                            double pct;
                            if (totalDuration > 0)
                            {
                                pct = Math.Min(100.0, seconds / totalDuration * 100.0);
                                label = $"{pct:F1}%  —  {elapsed:hh\\:mm\\:ss}";
                            }
                            else
                            {
                                pct = 0;
                                label = $"{elapsed:hh\\:mm\\:ss}";
                            }
                            if (batchLabel is not null) label = $"{batchLabel}  {label}";
                            Dispatcher.UIThread.Post(() => UpdateRecordProgress(pct, label));
                        }
                    }
                    // Fallback: parse classic "frame=...time=HH:MM:SS" lines
                    else if (line.Contains("time=", StringComparison.Ordinal))
                    {
                        var idx = line.IndexOf("time=", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var timePart = line.AsSpan(idx + 5);
                            var spaceIdx = timePart.IndexOf(' ');
                            if (spaceIdx > 0) timePart = timePart[..spaceIdx];
                            if (TimeSpan.TryParse(timePart, CultureInfo.InvariantCulture, out var ts))
                            {
                                string label;
                                double pct;
                                if (totalDuration > 0)
                                {
                                    pct = Math.Min(100.0, ts.TotalSeconds / totalDuration * 100.0);
                                    label = $"{pct:F1}%  —  {ts:hh\\:mm\\:ss}";
                                }
                                else
                                {
                                    pct = 0;
                                    label = $"{ts:hh\\:mm\\:ss}";
                                }
                                if (batchLabel is not null) label = $"{batchLabel}  {label}";
                                Dispatcher.UIThread.Post(() => UpdateRecordProgress(pct, label));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void UpdateRecordProgress(double percent, string label)
        {
            if (this.FindControl<ProgressBar>("RecordProgressBar") is { } bar)
                bar.Value = percent;
            if (this.FindControl<TextBlock>("RecordProgressText") is { } txt)
                txt.Text = label;
        }

        private void SetRecordProgressVisible(bool visible)
        {
            if (this.FindControl<StackPanel>("RecordProgressPanel") is { } panel)
                panel.IsVisible = visible;
        }

        private void SetRecordStartButtonState(bool idle)
        {
            if (this.FindControl<Button>("RecordStartBtn") is not { } btn) return;
            if (idle)
            {
                btn.Content = GetUiText("RecordStartBtn");
                btn.Background = new SolidColorBrush(Color.Parse("#35C156"));
            }
            else
            {
                btn.Content = GetUiText("RecordStopBtn");
                btn.Background = new SolidColorBrush(Color.Parse("#C62828"));
            }
        }

        private void SetEncodingLock(bool locked)
        {
            _isEncoding = locked;

            // Stop mpv playback when encoding starts
            if (locked)
                _mpvService?.Stop();

            // Disable/enable transport buttons (including Record toggle)
            foreach (var name in new[] { "VdbBeginning", "VdbPrevFrame", "VdbPlay", "VdbNextFrame", "VdbEnd", "SpeedBtn", "HalfResBtn", "RecordBtn" })
            {
                if (this.FindControl<Button>(name) is { } btn)
                {
                    btn.IsEnabled = !locked;
                    if (name == "RecordBtn")
                        btn.Opacity = locked ? 0.4 : 1.0;
                }
            }
            if (this.FindControl<Slider>("SeekBar") is { } seek)
                seek.IsEnabled = !locked;

            // Disable/enable clip preset combo in transport bar
            if (this.FindControl<ComboBox>("ClipPresetCombo") is { } clipPreset)
                clipPreset.IsEnabled = !locked;

            // Disable/enable clip tabs (file selection)
            if (this.FindControl<Border>("ClipTabsContainer") is { } clipTabs)
                clipTabs.IsEnabled = !locked;

            // Disable/enable top menu bar (Infos, Preset, Settings, Language, About)
            if (this.FindControl<Menu>("MainMenu") is { } menu)
                menu.IsEnabled = !locked;

            // Disable/enable encoding settings (clip list, params) but not Stop button
            if (this.FindControl<Grid>("RecordSettingsGrid") is { } recGrid)
                recGrid.IsEnabled = !locked;
            if (this.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
                shutCb.IsEnabled = !locked;
        }

        private string? GenerateRenderScript()
        {
            var scriptPath = _scriptService.GetPrimaryScriptPath();
            if (scriptPath is null || !File.Exists(scriptPath)) return null;

            var renderPath = Path.Combine(
                Path.GetDirectoryName(scriptPath)!,
                "ScriptRender.avs");

            var content = File.ReadAllText(scriptPath);
            // Force preview=false and preview_half=false for final render
            content = System.Text.RegularExpressions.Regex.Replace(
                content, @"(?m)^(\s*preview\s*=\s*)true", "${1}false");
            content = System.Text.RegularExpressions.Regex.Replace(
                content, @"(?m)^(\s*preview_half\s*=\s*)true", "${1}false");

            File.WriteAllText(renderPath, content);
            return renderPath;
        }

        private static string? FindFfmpeg()
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;

            // Check in Plugins/ffmpeg/ (bundled)
            var bundled = Path.Combine(exeDir, "Plugins", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(bundled)) return bundled;

            // Check next to the exe
            var local = Path.Combine(exeDir, "ffmpeg.exe");
            if (File.Exists(local)) return local;

            // Check in PATH
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            foreach (var d in pathDirs)
            {
                var candidate = Path.Combine(d.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static void EnsureAviSynthNextToFfmpeg(string ffmpegPath)
        {
            var ffmpegDir = Path.GetDirectoryName(ffmpegPath);
            if (string.IsNullOrWhiteSpace(ffmpegDir)) return;

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            var avsDir = Path.Combine(exeDir, "Plugins", "AviSynth");
            if (!Directory.Exists(avsDir)) return;

            // Copy avisynth.dll + DevIL.dll next to ffmpeg.exe if not already there
            foreach (var dll in new[] { "AviSynth.dll", "DevIL.dll" })
            {
                var src = Path.Combine(avsDir, dll);
                var dst = Path.Combine(ffmpegDir, dll);
                if (File.Exists(src) && !File.Exists(dst))
                {
                    try { File.Copy(src, dst, overwrite: false); }
                    catch { /* another process may have copied it */ }
                }
            }
        }

        private static async Task<bool> CheckFfmpegAviSynthSupport(string ffmpegPath)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-demuxers",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                proc.Start();
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                proc.Dispose();
                return output.Contains("avisynth", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private bool _syncingForceSource;

        private void SyncForceSourceCombo(Dictionary<string, string> values)
        {
            if (this.FindControl<ComboBox>("ForceSourceCombo") is not { } cb) return;
            _syncingForceSource = true;
            var current = values.TryGetValue("force_source", out var v) ? v.Trim().Trim('"') : "FFMS2";
            for (var i = 0; i < cb.ItemCount; i++)
            {
                if (cb.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), current, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    _syncingForceSource = false;
                    return;
                }
            }
            cb.SelectedIndex = 1; // default FFMS2
            _syncingForceSource = false;
        }

        private void OnForceSourceChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_syncingForceSource) return;
            if (sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item) return;
            var value = item.Tag?.ToString() ?? "";
            UpdateConfigurationValue("force_source", value);
            CloseSettingsMenu();
        }

        private void OnVdbEndClick(object? sender, RoutedEventArgs e)
        {
            if (_totalFrames > 0 && _fps > 0)
                _mpvService?.Seek((_totalFrames - 1.0) / _fps);
            else if (_seekDuration > 0)
                _mpvService?.Seek(_seekDuration - 0.001);
        }

        // ── Raccourcis clavier transport ────────────────────────────────
        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (_isEncoding) return;
            if (FocusManager?.GetFocusedElement() is TextBox) return;

            switch ((e.Key, e.KeyModifiers))
            {
                case (Key.Space, KeyModifiers.None):
                    e.Handled = true;
                    _mpvService?.TogglePlayPause();
                    break;

                case (Key.Left, KeyModifiers.None):
                    e.Handled = true;
                    _mpvService?.FrameBackStep();
                    break;

                case (Key.Left, KeyModifiers.Control):
                    e.Handled = true;
                    _mpvService?.Stop();
                    break;

                case (Key.Right, KeyModifiers.None):
                    e.Handled = true;
                    _mpvService?.FrameStep();
                    break;

                case (Key.Right, KeyModifiers.Control):
                    e.Handled = true;
                    if (_totalFrames > 0 && _fps > 0)
                        _mpvService?.Seek((_totalFrames - 1.0) / _fps);
                    else if (_seekDuration > 0)
                        _mpvService?.Seek(_seekDuration - 0.001);
                    break;
            }
        }

        // ── Chargement du script dans le player ─────────────────────────
        private async Task LoadScriptAsync(bool resetPosition = false)
        {
            if (_isClosing || _mpvService is null) return;

            await _refreshGate.WaitAsync();
            try
            {
                if (!TryValidateSourceSelection(out _))
                {
                    return;
                }

                var scriptPath = _scriptService.GetPrimaryScriptPath();
                if (string.IsNullOrWhiteSpace(scriptPath)) return;

                double pos;
                if (resetPosition)
                    pos = 0.0;
                else if (_mpvService.IsReady)
                {
                    var cur = _mpvService.GetPosition();
                    // If mpv is mid-reload it may report 0 — keep the previous pending position.
                    pos = (cur < 0.5 && _pendingSeekPos > 0.5) ? _pendingSeekPos : cur;
                }
                else
                    pos = _pendingSeekPos;
                _pendingSeekPos = pos;
                _totalFrames = 0;  // will be refreshed in OnMpvFileLoaded
                _fps         = 0;
                _loadingSourceFallback = false;
                DebugLog($"LoadFile: {scriptPath}, pos={pos:F2}, IsReady={_mpvService.IsReady}");
                try
                {
                    var content = File.ReadAllText(scriptPath);
                    DebugLog($"Script ({content.Length} chars): {content[..Math.Min(400, content.Length)].Replace('\n', '|').Replace('\r', ' ')}");
                }
                catch (Exception ex) { DebugLog($"Script read error: {ex.Message}"); }
                SetPlayButtonProcessing();
                ShowPlayerStatus("Chargement…");
                _mpvService.LoadFile(scriptPath, pos);
            }
            finally { _refreshGate.Release(); }
        }

        #endregion

        #region Validation

        private bool TryValidateSourceSelection(out string errorMessage)
        {
            var useImg = IsImageSourceEnabled();
            var raw = _config.Get("source");
            if (string.IsNullOrWhiteSpace(raw))
                raw = useImg ? _config.Get("img") : _config.Get("film");

            if (string.IsNullOrWhiteSpace(raw))
            {
                errorMessage = string.Empty;
                return false;
            }

            var path = _sourceService.NormalizeConfiguredPath(raw);

            if (useImg && !_sourceService.ImageSequenceExists(path))
            {
                errorMessage = GetLocalizedText(
                    fr: "Les images référencées dans source sont introuvables.",
                    en: "The image sequence referenced in source was not found.");
                return false;
            }

            if (!useImg && !File.Exists(path))
            {
                errorMessage = GetLocalizedText(
                    fr: "Le fichier référencé dans source est introuvable.",
                    en: "The file referenced in source was not found.");
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private void ShowSourceValidationError(string message)
        {
            if (_sourceValidationErrorVisible || string.IsNullOrWhiteSpace(message)) return;
            _sourceValidationErrorVisible = true;
            Dispatcher.UIThread.Post(async () =>
            {
                try { await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), message); }
                finally { _sourceValidationErrorVisible = false; }
            });
        }

        #endregion

        #region Presets

        private async void OnPresetClick(object? sender, RoutedEventArgs e)
        {
            await _dialogService.ShowPresetDialogAsync(this, _presetService, _config, ApplyPresetToAllClipsAsync, ViewModel);
            RefreshClipPresetCombo();
            RestoreClipPresetCombo();
            RebuildClipTabs();
        }

        /// <summary>Applies preset values to the current UI/config AND propagates to all clip configs.</summary>
        private async Task ApplyPresetToAllClipsAsync(string presetName, Dictionary<string, string> values)
        {
            await ApplyPresetValuesAsync(values);

            // Propagate to all clip configs
            var filterSnap = CaptureClipConfig();
            for (int i = 0; i < _clipConfigs.Count; i++)
                _clipConfigs[i] = new Dictionary<string, string>(filterSnap, StringComparer.OrdinalIgnoreCase);

            // Set the preset name on all clips
            for (int i = 0; i < _clipPresetNames.Count; i++)
                _clipPresetNames[i] = presetName;
        }

        /// <summary>Applies preset values to the current UI/config only (per-clip).</summary>
        private async Task ApplyPresetValuesAsync(Dictionary<string, string> values)
        {
            _applyingPreset = true;
            try
            {
            // Ignore source files and crop values from presets
            foreach (var key in PresetService.ExcludedKeys)
                values.Remove(key);

            foreach (var name in ScriptService.TextFieldNames)
            {
                if (!values.TryGetValue(name, out var value)) continue;

                if (this.FindControl<Control>(name) is TextBox tb) SetTextSafely(tb, value);
                else if (this.FindControl<Control>(name) is ComboBox cb) value = ApplyComboChoice(cb, name, value);

                _config.Set(name, value);
            }

            foreach (var name in ScriptService.BoolFieldNames)
            {
                if (!values.TryGetValue(name, out var v) || !bool.TryParse(v, out var parsed)) continue;
                SetOptionToggleValue(name, parsed);
                _config.Set(name, parsed.ToString().ToLowerInvariant());
            }

            var useImage = values.TryGetValue(UseImageConfigName, out var uiv)
                && bool.TryParse(uiv, out var parsedUseImage) && parsedUseImage;
            UpdateSourceSelection(isFilmSelected: !useImage, updateConfig: true);
            SyncAllSliders();
            RegenerateScript(showValidationError: true);
            UpdateOptionColumnVisibility();

            // Save into current clip config
            SaveActiveClipConfig();

            if (TryValidateSourceSelection(out _))
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
            }
            finally { _applyingPreset = false; }
        }

        /// <summary>Returns a unique "persoN" name not already used by another clip.</summary>
        private string GetNextPersoName()
        {
            int n = 1;
            while (_clipPresetNames.Contains($"perso{n}", StringComparer.OrdinalIgnoreCase))
                n++;
            return $"perso{n}";
        }

        /// <summary>Populates the per-clip preset ComboBox from saved presets.</summary>
        private void RefreshClipPresetCombo()
        {
            if (this.FindControl<ComboBox>("ClipPresetCombo") is not { } combo) return;
            var presets = _presetService.LoadPresets()
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Name)
                .ToList();

            var current = combo.SelectedItem as string;
            combo.ItemsSource = presets;
            if (current is not null && presets.Contains(current))
                combo.SelectedItem = current;
            else
                combo.SelectedIndex = -1;
        }

        private bool _suppressClipPresetChange;

        private async void OnClipPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressClipPresetChange) return;
            if (sender is not ComboBox combo) return;
            var name = combo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = _presetService.LoadPresets()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset?.Values is null) return;

            // Store preset name for this clip
            if (_activeClipIndex >= 0 && _activeClipIndex < _clipPresetNames.Count)
                _clipPresetNames[_activeClipIndex] = name;

            await ApplyPresetValuesAsync(new Dictionary<string, string>(preset.Values, StringComparer.OrdinalIgnoreCase));
            RebuildClipTabs();
        }

        #endregion

        #region Info dialogs

        private void OnUserGuideClick(object? sender, RoutedEventArgs e)
        {
            var lang = ViewModel.CurrentLanguageCode;
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
            var guidePath = System.IO.Path.Combine(exeDir, "Users Guide", $"CleanScan_Guide_{lang}.pdf");
            if (!System.IO.File.Exists(guidePath))
                guidePath = System.IO.Path.Combine(exeDir, "Users Guide", "CleanScan_Guide_en.pdf");
            if (System.IO.File.Exists(guidePath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = guidePath, UseShellExecute = true });
        }

        private async void OnScriptPreviewClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowScriptPreviewDialogAsync(this, _scriptService,
                () => _ = LoadScriptAsync(), ViewModel);

        private async void OnFeedbackClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowFeedbackDialogAsync(this, ViewModel);

        private async void OnAboutClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowAboutDialogAsync(
                this,
                GetUiText("AboutMenuItem"),
                GetUiText("AboutCompany"),
                GetUiText("AllRightsReserved"),
                GetUiText("AboutWebsite"),
                $"version {GetUiText("AboutVersion")}",
                GetUiText("GamMacCloseButton"),
                "avares://CleanScan/Assets/Logo.png");

        #endregion

        #region UI helpers

        private void SetTextSafely(TextBox textBox, string text)
        {
            try { _suppressTextEvents = true; textBox.Text = text; }
            finally { _suppressTextEvents = false; }
        }

        private static void SetComboBoxChoice(ComboBox cb, string? rawValue, string[] choices)
        {
            var normalized = NormalizeChoiceValue(rawValue);
            string? found = null;
            for (var i = 0; i < choices.Length; i++)
            {
                if (string.Equals(choices[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    found = choices[i];
                    break;
                }
            }
            cb.SelectedItem = found ?? (choices.Length > 0 ? choices[0] : string.Empty);
        }

        private void UpdateMinimumWidthFromConfiguration()
        {
            if (this.FindControl<Border>("ConfigurationBorder") is not { } border) return;
            Dispatcher.UIThread.Post(() =>
            {
                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var w = border.DesiredSize.Width;
                if (_mainGrid is { } g) w += g.Margin.Left + g.Margin.Right;
                MinWidth = Math.Max(MinWidth, w);
            });
        }

        private static string NormalizeChoiceValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var cleaned = value.Trim();
            var hash    = cleaned.IndexOf('#');
            if (hash >= 0) cleaned = cleaned[..hash].TrimEnd();
            return cleaned.Trim().Trim('"');
        }

        private static string GetAppDataPath(string fileName) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder, fileName);

        private void OnGuidedTourClick(object? sender, RoutedEventArgs e)
        {
            _ = ShowGuidedTourAsync();
        }

        #endregion

        #region Guided Tour

        // ── Step definitions ─────────────────────────────────────────
        // TargetName = x:Name of the control to point at (null = centered card)
        // BeforeAction = optional name used to auto-open panels before showing the step
        private static readonly (string? TargetName, string TitleKey, string BodyKey, string Emoji, string? BeforeAction)[] TourSteps =
        [
            (null,            "TourWelcomeTitle",  "TourWelcomeBody",  "\ud83c\udfac", null),
            ("AddClipBtn",    "TourAddClipTitle",  "TourAddClipBody",  "\u2795",       null),
            ("CropExpandBtn", "TourParamsTitle",   "TourParamsBody",   "\ud83d\udd27", null),
            ("Slide_Crop_L",  "TourSliderTitle",   "TourSliderBody",   "\ud83c\udf9a", "OpenCrop"),
            ("Label_Crop_L",  "TourTooltipTitle",  "TourTooltipBody",  "\ud83d\udcac", "OpenCrop"),
            ("VdbPlay",       "TourPreviewTitle",  "TourPreviewBody",  "\u25b6\ufe0f", null),
            ("RecordBtn",       "TourRecordTitle",        "TourRecordBody",        "\ud83d\udcbe", null),
            ("RecordDirPickBtn", "TourOutputDirTitle",   "TourOutputDirBody",     "\ud83d\udcc1", "OpenRecord"),
            ("RecordStartBtn", "TourStartEncodingTitle", "TourStartEncodingBody", "\ud83d\ude80", "OpenRecord"),
        ];

        private bool _tourActive;
        private Action? _tourAdvanceOnClipLoaded;

        private async Task ShowGuidedTourAsync()
        {
            if (_tourActive) return;
            _tourActive = true;
            try
            {
                // Ensure the record panel is closed so the main UI is visible
                if (_recordOpen)
                    OnRecordClick(null, new RoutedEventArgs());

                // Let the window finish layout/render before placing the floating card.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(300);

                int step = 0;
                var tourDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                // ── Build callout content (created once, updated per step) ───

            var titleTb = new TextBlock
            {
                FontSize   = 18, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#F6F6F6")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            };
            var bodyTb = new TextBlock
            {
                FontSize = 13, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                MaxWidth = 340, LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 18),
            };

            // Step dots
            var dots = new Border[TourSteps.Length];
            var dotsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6, Margin = new Thickness(0, 0, 0, 14),
            };
            for (int i = 0; i < TourSteps.Length; i++)
            {
                dots[i] = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4) };
                dotsPanel.Children.Add(dots[i]);
            }

            // Navigation buttons
            var skipBtn = new Button
            {
                MinWidth = 60, Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#8899AA")),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var prevBtn = new Button
            {
                MinWidth = 70,
                Background = new SolidColorBrush(Color.Parse("#252E42")),
                Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var nextBtn = new Button
            {
                MinWidth = 90,
                Background = new SolidColorBrush(Color.Parse("#3B82F6")),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            var buttonsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
            };
            buttonsRow.Children.Add(skipBtn);
            buttonsRow.Children.Add(prevBtn);
            buttonsRow.Children.Add(nextBtn);

            var stepLabel = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#556677")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            };

            // ── Language selector (welcome step only) ────────────────────
            var langLabel = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#8899AA")),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var langRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6, Margin = new Thickness(0, 0, 0, 12),
            };
            langRow.Children.Add(langLabel);
            foreach (var (code, label) in new[] { ("en", "English"), ("fr", "Français"), ("de", "Deutsch"), ("es", "Español") })
            {
                langRow.Children.Add(new Button
                {
                    Content = label, Tag = code, MinWidth = 70, FontSize = 12,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
                });
            }

            Action? refreshStep = null;
            foreach (var child in langRow.Children)
            {
                if (child is not Button lb) continue;
                lb.Click += (s, _) =>
                {
                    if (s is Button { Tag: string code })
                    {
                        ApplyLanguage(code, persist: true);
                        refreshStep?.Invoke();
                    }
                };
            }

            // ── Callout card (shown in a Popup so it stays above NativeControlHost) ──
            var card = new Border
            {
                Background        = new SolidColorBrush(Color.Parse("#1A2233")),
                BorderBrush       = new SolidColorBrush(Color.Parse("#3B82F6")),
                BorderThickness   = new Thickness(1),
                CornerRadius      = new CornerRadius(10),
                Padding           = new Thickness(28, 24),
                MinWidth          = 340,
                MaxWidth          = 400,
                BoxShadow         = BoxShadows.Parse("0 8 30 0 #A0000000"),
                Child = new StackPanel
                {
                    Spacing = 0,
                    Children = { titleTb, bodyTb, langRow, dotsPanel, buttonsRow, stepLabel }
                }
            };

                var mainGrid = this.FindControl<Grid>("MainGrid");
                if (mainGrid is null)
                {
                    tourDone.TrySetResult();
                    return;
                }

                var tourPopup = new Popup
                {
                    PlacementTarget = mainGrid,
                    Placement = PlacementMode.TopEdgeAlignedLeft,
                    IsLightDismissEnabled = false,
                    Child = card,
                };

                void SetPopupOffset(double x, double y)
                {
                    // TopEdgeAlignedLeft uses the target's top-left as reference.
                    tourPopup.HorizontalOffset = x;
                    tourPopup.VerticalOffset = y;
                }

                Control? highlightedTarget = null;
                IBrush? addClipBg = null;
                IBrush? addClipFg = null;
                IBrush? addClipBorder = null;

                // Auto-advance timer for steps 3–4
                CancellationTokenSource? tooltipHoverCts = null;
                EventHandler<Avalonia.Input.PointerEventArgs>? tooltipEnterHandler = null;
                EventHandler<Avalonia.Input.PointerEventArgs>? tooltipLeaveHandler = null;
                EventHandler<RangeBaseValueChangedEventArgs>? sliderValueHandler = null;
                EventHandler<RoutedEventArgs>? buttonClickHandler = null;
                Control? tooltipTarget = null;

                int GetStepVerticalNudge(int currentStep) => currentStep switch
                {
                    1 => 70,   // Add clip: lower the card
                    2 => 70,   // Parameters: lower the card
                    3 => 50,   // Slider: slightly lower the card
                    4 => -60,  // Tooltip: raise the card
                    7 => -100, // Output dir: raise the card
                    _ => 0,
                };

                int GetStepHorizontalNudge(int currentStep) => currentStep switch
                {
                    0 => 120,  // Welcome: nudge right
                    1 => 80,   // Add clip: nudge right
                    3 => 120,  // Slider: move to the right
                    7 => -120, // Output dir: move to the left
                    _ => 0,
                };

                EventHandler<PointerPressedEventArgs>? highlightedClickHandler = null;

                void ClearTooltipHover()
                {
                    tooltipHoverCts?.Cancel();
                    tooltipHoverCts = null;
                    if (tooltipTarget is not null)
                    {
                        if (tooltipEnterHandler is not null) tooltipTarget.PointerEntered -= tooltipEnterHandler;
                        if (tooltipLeaveHandler is not null) tooltipTarget.PointerExited  -= tooltipLeaveHandler;
                        if (sliderValueHandler is not null && tooltipTarget is Slider sl)
                            sl.ValueChanged -= sliderValueHandler;
                        if (buttonClickHandler is not null && tooltipTarget is Button btn)
                            btn.Click -= buttonClickHandler;
                        tooltipEnterHandler = null;
                        tooltipLeaveHandler = null;
                        sliderValueHandler = null;
                        buttonClickHandler = null;
                        tooltipTarget = null;
                    }
                }

                void ClearHighlight()
                {
                    ClearTooltipHover();

                    if (highlightedTarget is Button { Name: "AddClipBtn" } addClip)
                    {
                        if (addClipBg is not null) addClip.Background = addClipBg;
                        if (addClipFg is not null) addClip.Foreground = addClipFg;
                        if (addClipBorder is not null) addClip.BorderBrush = addClipBorder;
                    }

                    if (highlightedTarget is null) return;

                    if (highlightedClickHandler is not null)
                    {
                        highlightedTarget.RemoveHandler(Avalonia.Input.InputElement.PointerPressedEvent,
                            highlightedClickHandler);
                        highlightedClickHandler = null;
                    }

                    highlightedTarget.Classes.Remove("tour-highlight");
                    highlightedTarget = null;
                }

            // ── UpdateStep ───────────────────────────────────────────────

            void UpdateStep()
            {
                var (targetName, titleKey, bodyKey, emoji, beforeAction) = TourSteps[step];
                bool isFirst = step == 0;
                bool isLast  = step == TourSteps.Length - 1;

                // Pre-action: open/close panels as needed
                if (beforeAction == "OpenCrop")
                {
                    // Close record panel if open
                    if (_recordOpen) OnRecordClick(null, new RoutedEventArgs());

                    if (this.FindControl<Control>("CropParams") is { IsVisible: false }
                        && this.FindControl<Button>("CropExpandBtn") is { } expandBtn)
                    {
                        OnExpandButtonClick(expandBtn, new RoutedEventArgs());
                    }
                }
                else if (beforeAction == "OpenRecord")
                {
                    if (!_recordOpen) OnRecordClick(null, new RoutedEventArgs());
                }
                else if (beforeAction is null)
                {
                    // Steps without BeforeAction: close record panel if open
                    if (_recordOpen) OnRecordClick(null, new RoutedEventArgs());
                }

                // Update text
                titleTb.Text    = $"{emoji}  {GetUiText(titleKey)}";
                bodyTb.Text     = GetUiText(bodyKey);
                skipBtn.Content = GetUiText("TourSkipBtn");
                prevBtn.Content = GetUiText("TourPrevBtn");
                nextBtn.Content = isLast ? GetUiText("TourFinishBtn") : GetUiText("TourNextBtn");
                stepLabel.Text  = $"{step + 1} / {TourSteps.Length}";
                prevBtn.IsVisible = !isFirst;

                // Language row (welcome only)
                langRow.IsVisible = isFirst;
                if (isFirst)
                {
                    langLabel.Text = GetUiText("TourLanguageLabel");
                    var curLang = ViewModel.CurrentLanguageCode;
                    foreach (var child in langRow.Children)
                    {
                        if (child is not Button lb || lb.Tag is not string code) continue;
                        bool cur = string.Equals(code, curLang, StringComparison.OrdinalIgnoreCase);
                        lb.Background = new SolidColorBrush(Color.Parse(cur ? "#3B82F6" : "#252E42"));
                        lb.Foreground = new SolidColorBrush(Color.Parse(cur ? "#FFFFFF" : "#CCCCCC"));
                    }
                }

                // Dots
                for (int i = 0; i < dots.Length; i++)
                    dots[i].Background = new SolidColorBrush(
                        i == step  ? Color.Parse("#3B82F6")
                      : i < step   ? Color.Parse("#6AA3D8")
                      :              Color.Parse("#3A3A3A"));

                // ── Position the card next to the target ─────────────────
                double ww = ClientSize.Width;
                double wh = ClientSize.Height;

                // Measure card to know its size
                card.Measure(new Size(ww, wh));
                double cardW = Math.Max(card.DesiredSize.Width, 340);
                double cardH = Math.Max(card.DesiredSize.Height, 100);
                double gap = 16;

                ClearHighlight();
                if (targetName is not null && this.FindControl<Control>(targetName) is { } target)
                {
                    target.Classes.Add("tour-highlight");
                    highlightedTarget = target;

                    if (step >= 2 && step is not (2 or 3 or 4 or 5 or 6 or 7 or 8))
                    {
                        highlightedClickHandler = (_, e) =>
                        {
                            // Prevent the click from triggering the control's own handler
                            // (e.g. CropExpandBtn toggling the panel closed)
                            e.Handled = true;
                            step++;
                            if (step >= TourSteps.Length) CloseTour();
                            else UpdateStep();
                        };
                        target.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent,
                            highlightedClickHandler, Avalonia.Interactivity.RoutingStrategies.Tunnel);
                    }

                    if (target is Button { Name: "AddClipBtn" } addClip)
                    {
                        addClipBg ??= addClip.Background;
                        addClipFg ??= addClip.Foreground;
                        addClipBorder ??= addClip.BorderBrush;
                        addClip.Background = new SolidColorBrush(Color.Parse("#2A3755"));
                        addClip.Foreground = Brushes.White;
                        addClip.BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
                    }

                    var pos = target.TranslatePoint(new Point(0, 0), mainGrid);
                    double tx = pos?.X ?? 0;
                    double ty = pos?.Y ?? 0;
                    double tw = target.Bounds.Width;
                    double th = target.Bounds.Height;

                    double cLeft, cTop;

                    if (tx + tw + gap + cardW + 10 < ww)
                    {
                        // Right of the target
                        cLeft = tx + tw + gap;
                        cTop  = Math.Clamp(ty, 10, Math.Max(10, wh - cardH - 10));
                    }
                    else if (ty + th + gap + cardH + 10 < wh)
                    {
                        // Below the target
                        cLeft = Math.Clamp(tx, 10, Math.Max(10, ww - cardW - 10));
                        cTop  = ty + th + gap;
                    }
                    else if (ty - gap - cardH > 0)
                    {
                        // Above the target
                        cLeft = Math.Clamp(tx, 10, Math.Max(10, ww - cardW - 10));
                        cTop  = ty - gap - cardH;
                    }
                    else
                    {
                        // Fallback: center
                        cLeft = Math.Max(10, (ww - cardW) / 2);
                        cTop  = Math.Max(10, (wh - cardH) / 2);
                    }

                    var xNudge = GetStepHorizontalNudge(step);
                    var yNudge = GetStepVerticalNudge(step);
                    cLeft = Math.Clamp(cLeft + xNudge, 10, Math.Max(10, ww - cardW - 10));
                    cTop = Math.Clamp(cTop + yNudge, 10, Math.Max(10, wh - cardH - 10));
                    SetPopupOffset(cLeft, cTop);
                }
                else
                {
                    // No target → center in window
                    double cLeft = Math.Max(10, (ww - cardW) / 2);
                    double cTop  = Math.Max(10, (wh - cardH) / 2);
                    var xNudge = GetStepHorizontalNudge(step);
                    var yNudge = GetStepVerticalNudge(step);
                    cLeft = Math.Clamp(cLeft + xNudge, 10, Math.Max(10, ww - cardW - 10));
                    cTop = Math.Clamp(cTop + yNudge, 10, Math.Max(10, wh - cardH - 10));
                    SetPopupOffset(cLeft, cTop);
                }

                _tourAdvanceOnClipLoaded = step == 1
                    ? () =>
                    {
                        if (!_tourActive || step != 1) return;
                        step++;
                        if (step >= TourSteps.Length) CloseTour();
                        else UpdateStep();
                    }
                    : null;

                // Steps 2–7: auto-advance after interacting with the target
                if (step is >= 2 and <= 7 && targetName is not null
                    && this.FindControl<Control>(targetName) is { } ttTarget)
                {
                    var capturedStep = step;
                    tooltipTarget = ttTarget;

                    void AdvanceAfterDelay()
                    {
                        if (tooltipHoverCts is not null) return; // already running
                        tooltipHoverCts = new CancellationTokenSource();
                        var token = tooltipHoverCts.Token;
                        _ = Task.Delay(8000, token).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!_tourActive || step != capturedStep || token.IsCancellationRequested) return;
                                step++;
                                if (step >= TourSteps.Length) CloseTour();
                                else UpdateStep();
                            });
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }

                    if (step == 3 && ttTarget is Slider sl)
                    {
                        // Step 3 (slider): start 4s timer on first manipulation
                        sliderValueHandler = (_, _) => AdvanceAfterDelay();
                        sl.ValueChanged += sliderValueHandler;
                    }
                    else if (step == 5 && ttTarget is Button bt)
                    {
                        // Step 5 (play button): start 4s timer on click
                        buttonClickHandler = (_, _) => AdvanceAfterDelay();
                        bt.Click += buttonClickHandler;
                    }
                    else if (step is 2 or 6 or 7 && ttTarget is Button clickBtn)
                    {
                        // Steps 2/6/7: advance immediately on click
                        buttonClickHandler = (_, _) =>
                        {
                            if (!_tourActive) return;
                            step++;
                            if (step >= TourSteps.Length) CloseTour();
                            else UpdateStep();
                        };
                        clickBtn.Click += buttonClickHandler;
                    }
                    else if (step == 8 && ttTarget is Button lastBtn)
                    {
                        // Step 8 (start encoding): close tour immediately on click
                        buttonClickHandler = (_, _) => { if (_tourActive) CloseTour(); };
                        lastBtn.Click += buttonClickHandler;
                    }
                    else
                    {
                        // Step 4 (label): start 4s timer on hover, cancel on leave
                        tooltipEnterHandler = (_, _) => AdvanceAfterDelay();
                        tooltipLeaveHandler = (_, _) =>
                        {
                            tooltipHoverCts?.Cancel();
                            tooltipHoverCts = null;
                        };
                        ttTarget.PointerEntered += tooltipEnterHandler;
                        ttTarget.PointerExited  += tooltipLeaveHandler;
                    }
                }

                tourPopup.IsOpen = true;
            }

            // ── Navigation ───────────────────────────────────────────────

            void CloseTour()
            {
                ClearHighlight();
                _tourAdvanceOnClipLoaded = null;
                tourPopup.IsOpen = false;
                // Mark completed
                var settings = _windowStateService.Load();
                if (settings is not null)
                    _windowStateService.Save(settings with { TourCompleted = true });
                tourDone.TrySetResult();
            }

            nextBtn.Click += (_, _) =>
            {
                step++;
                if (step >= TourSteps.Length) CloseTour();
                else UpdateStep();
            };
            prevBtn.Click += (_, _) =>
            {
                if (step > 0) { step--; UpdateStep(); }
            };
            skipBtn.Click += (_, _) => CloseTour();

            refreshStep = UpdateStep;
            UpdateStep();
            await tourDone.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Guided tour failed: {ex}");
            }
            finally
            {
                _tourAdvanceOnClipLoaded = null;
                _tourActive = false;
            }
        }

        #endregion
    }
}
