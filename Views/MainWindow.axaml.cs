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
using System.Runtime.InteropServices;
using CleanScan.Models;
using CleanScan.Services;
using CleanScan.ViewModels;

namespace CleanScan.Views
{
    public partial class MainWindow : Window, ITourHost, IFilterPresenterHost, IEncodeHost
    {
        #region Constants

        private const string AppDataFolder         = "CleanScan";
        private const string WindowSettingsFileName = "window-settings.json";
        private const string PresetsFileName        = "presets.json";
        private const string EncodingPresetsFileName = "encoding_presets.json";
        private const string GammacPresetsFileName   = "gammac_presets.json";
        private const string SessionFileName         = "session.json";
        private const string CustomFiltersFileName   = "custom_filters.json";
        private const string DefaultEncodingPresetName = "Default";

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

        [GeneratedRegex(@"^\d+$")]
        private static partial Regex NumericStemRegex();

        [GeneratedRegex(@"(?m)^(\s*preview\s*=\s*)true")]
        private static partial Regex PreviewTrueRegex();

        [GeneratedRegex(@"(?m)^(\s*preview_half\s*=\s*)true")]
        private static partial Regex PreviewHalfTrueRegex();

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
        private readonly PresetService        _gammacPresetService;
        private readonly IWindowStateService  _windowStateService;
        private readonly IDialogService       _dialogService;
        private readonly IAviService          _aviService;
        private readonly SessionService      _sessionService;
        private readonly CustomFilterService _customFilterService;
        private readonly ThemeService        _themeService = new();
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

        private ClipManager _clipManager = null!; // initialized in constructor
        private bool _applyingPreset;

        // Convenience accessors over ClipManager
        private List<ClipState> _clips => _clipManager.Clips;
        private int _activeClipIndex { get => _clipManager.ActiveIndex; set => _clipManager.ActiveIndex = value; }

        private EncodeController _encodeController = null!; // initialized in constructor
        private bool _isDroppingFiles;


        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

        #endregion

        #region Constructor & lifecycle

        public MainWindow()
        {
            _config             = new ConfigStore();
            _clipManager        = new ClipManager(_config);
            _sourceService      = new SourceService();
            _aviService         = new AviService();
            _scriptService      = new ScriptService(_sourceService);
            _presetService      = new PresetService(GetAppDataPath(PresetsFileName));
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _gammacPresetService  = new PresetService(GetAppDataPath(GammacPresetsFileName));
            _windowStateService = new WindowStateService(GetAppDataPath(WindowSettingsFileName));
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _customFilterService = new CustomFilterService(GetAppDataPath(CustomFiltersFileName));
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
            _clipManager        = new ClipManager(config);
            _sourceService      = sourceService;
            _scriptService      = scriptService;
            _presetService      = presetService;
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _gammacPresetService  = new PresetService(GetAppDataPath(GammacPresetsFileName));
            _windowStateService = windowStateService;
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _customFilterService = new CustomFilterService(GetAppDataPath(CustomFiltersFileName));
            _dialogService      = dialogService;
            _aviService         = aviService;

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            ConfigureMenuBar();
            InitTheme();
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
            _encodeController = new EncodeController(this);
            _encodeController.InitRecordPanel();
            InitPlayerControls();
            RebuildCustomFilterUI();
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

        private static string GetAviSynthDiagnostic()
        {
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
            Title = "CleanScan";

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
                    ShowPlayerStatus("Erreur de script AviSynth");
                    _ = ShowAvsScriptErrorAsync();
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

        private async Task ShowAvsScriptErrorAsync()
        {
            var scriptPath = _scriptService.GetPrimaryScriptPath();
            if (string.IsNullOrWhiteSpace(scriptPath)) return;

            var (avsError, fullStderr) = await EncodeController.ProbeAvsScriptError(scriptPath);
            var primary = !string.IsNullOrWhiteSpace(avsError)
                ? avsError
                : "Erreur inconnue dans le script AviSynth.\nOuvrez le script dans AvsPmod pour diagnostiquer.";
            ShowPlayerStatus(primary);
            await _dialogService.ShowErrorAsync(this, "Erreur AviSynth", primary, fullStderr);
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
            _guidedTourService.AdvanceOnClipLoaded?.Invoke();

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
                btn.Background = ThemeBrush("BgInput");
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

            // Initialize overlap max based on current blksize
            if (_sliderMap.TryGetValue("degrain_blksize", out var blk))
                ClampOverlapToBlksize(SnapToNearest(blk.Slider.Value, ValidBlkSizes));
        }

        /// <summary>Snap a value to the nearest multiple of <paramref name="step"/> from <paramref name="min"/>.</summary>
        private static double SnapToStep(double value, double min, double step)
        {
            if (step <= 0) return value;
            return min + Math.Round((value - min) / step) * step;
        }

        private static void MoveSliderToPointer(Slider slider, PointerEventArgs e)
        {
            const double thumbHalf = 7.0;
            var w = slider.Bounds.Width;
            if (w <= thumbHalf * 2) return;
            var x     = e.GetCurrentPoint(slider).Position.X;
            var ratio = Math.Clamp((x - thumbHalf) / (w - thumbHalf * 2), 0.0, 1.0);
            var raw   = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
            slider.Value = SnapToStep(raw, slider.Minimum, slider.SmallChange);
        }

        private void OnSliderValueChanged(SliderSpec spec)
        {
            if (!_layoutInitialized || _sliderSync || _suppressTextEvents) return;
            if (!_sliderMap.TryGetValue(spec.Field, out var entry)) return;
            if (this.FindControl<TextBox>(spec.Field) is not { } tb) return;

            _sliderSync = true;
            try
            {
                var snapped = SnapToStep(entry.Slider.Value, spec.Min, spec.SmallChange);
                if (spec.Field == "degrain_blksize")
                    snapped = SnapToNearest(snapped, ValidBlkSizes);
                tb.Text = spec.IsFloat
                    ? snapped.ToString("F" + spec.Decimals, CultureInfo.InvariantCulture)
                    : ((int)Math.Round(snapped)).ToString();
            }
            finally { _sliderSync = false; }
        }


        /// <summary>Valid blksize values for MVTools2 MAnalyse (powers of 2).</summary>
        private static readonly int[] ValidBlkSizes = [4, 8, 16, 32, 64];

        /// <summary>Snap to nearest value in a sorted array.</summary>
        private static int SnapToNearest(double value, int[] validValues)
        {
            var best = validValues[0];
            var bestDist = Math.Abs(value - best);
            for (var i = 1; i < validValues.Length; i++)
            {
                var dist = Math.Abs(value - validValues[i]);
                if (dist < bestDist) { best = validValues[i]; bestDist = dist; }
            }
            return best;
        }

        private void CommitSliderField(string field)
        {
            if (!_sliderMap.TryGetValue(field, out var entry)) return;
            var snapped = SnapToStep(entry.Slider.Value, entry.Spec.Min, entry.Spec.SmallChange);
            snapped = Math.Clamp(snapped, entry.Spec.Min, entry.Spec.Max);

            // blksize: snap to nearest power of 2
            if (field == "degrain_blksize")
                snapped = SnapToNearest(snapped, ValidBlkSizes);

            entry.Slider.Value = snapped;
            var text = entry.Spec.IsFloat
                ? snapped.ToString("F" + entry.Spec.Decimals, CultureInfo.InvariantCulture)
                : ((int)Math.Round(snapped)).ToString();
            _ = ApplyFieldChangeAsync(field, text, showValidationError: true, refreshScriptPreview: false);

            // When blksize changes, cap overlap to blksize/2
            if (field == "degrain_blksize")
                ClampOverlapToBlksize((int)Math.Round(snapped));
        }

        /// <summary>Ensures overlap ≤ blksize/2 and updates slider max accordingly.</summary>
        private void ClampOverlapToBlksize(int blksize)
        {
            if (!_sliderMap.TryGetValue("degrain_overlap", out var ov)) return;
            var maxOverlap = blksize / 2;
            ov.Slider.Maximum = maxOverlap;
            if (ov.Slider.Value > maxOverlap)
            {
                ov.Slider.Value = maxOverlap;
                CommitSliderField("degrain_overlap");
            }
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

        // ── UIPI drag-drop fix for elevated processes ─────────────────────
        [DllImport("user32.dll")]
        private static extern bool ChangeWindowMessageFilterEx(
            nint hwnd, uint message, uint action, nint pChangeFilterStruct);

        private const uint WM_DROPFILES_MSG      = 0x0233;
        private const uint WM_COPYGLOBALDATA_MSG  = 0x0049;
        private const uint MSGFLT_ALLOW_MSG       = 1;

        private void AllowDragDropThroughUipi()
        {
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is null) return;
            var hwnd = platformHandle.Handle;
            ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES_MSG, MSGFLT_ALLOW_MSG, 0);
            ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA_MSG, MSGFLT_ALLOW_MSG, 0);
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            AllowDragDropThroughUipi();
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

                // Rebuild batch clip list now that clips are loaded (RestoreSessionState may
                // have opened the Record panel before clips were available)
                if (_recordOpen)
                    RebuildBatchClipList();

                // Régénère toujours avec la bonne langue au démarrage (indépendamment de la validation source)
                _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
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

            // Kill any running encoding process (delegated to EncodeController — not needed,
            // but ensure encoding lock is released)
            // EncodeController's process is internal; it handles its own cleanup.

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
            CloseAllMenus();
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
            ApplyThemeLabels();


            if (this.FindControl<TextBlock>("DropHintBar") is { IsVisible: true } dropBar)
                dropBar.Text = GetUiText("DropHintBar");

            if (this.FindControl<Button>("ImportCustomFilterBtn") is { } importBtn)
                ToolTip.SetTip(importBtn, GetUiText("CfDlgImportTitle"));

            if (persist && IsVisible)
            {
                _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
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

            // MaxViewerBtn tooltip depends on state
            if (this.FindControl<Button>("MaxViewerBtn") is { } maxBtn)
                ToolTip.SetTip(maxBtn, GetUiText(_viewerMaximized ? "RestoreViewerBtn" : "MaxViewerBtn"));
        }

        private void ApplyRecordLabels()
        {
            if (this.FindControl<Button>("RecordBtn") is { } btn)
                btn.Content = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordOverlayTitle") is { } title)
                title.Text = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordDirLabel") is { } dirLbl)
                dirLbl.Text = GetUiText("RecordDirLabel");
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
            if (this.FindControl<Button>("GammacPresetSaveBtn") is { } gmSave)
                gmSave.Content = GetUiText("RecordPresetSaveBtn");
            if (this.FindControl<Button>("GammacPresetDelBtn") is { } gmDel)
                gmDel.Content = GetUiText("RecordPresetDeleteBtn");
            if (this.FindControl<Button>("RecordStartBtn") is { } startBtn)
                startBtn.Content = GetUiText("RecordStartBtn");
            if (this.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
                shutCb.Content = GetUiText("ShutdownCheckBox");
            if (this.FindControl<TextBlock>("BatchClipListLabel") is { } batchLbl)
                batchLbl.Text = GetUiText("BatchClipListLabel");
            if (this.FindControl<CheckBox>("BatchSelectAllCheck") is { } allCb)
                allCb.Content = GetUiText("BatchSelectAll");
            if (this.FindControl<TextBlock>("BatchColOriginal") is { } colOrig)
                colOrig.Text = GetUiText("BatchColOriginal");
            if (this.FindControl<TextBlock>("BatchColRenamed") is { } colRenamed)
                colRenamed.Text = GetUiText("BatchColRenamed");
            if (this.FindControl<TextBlock>("BatchColPreset") is { } colPreset)
                colPreset.Text = GetUiText("BatchColPreset");
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

        private void CloseAllMenus()
        {
            if (this.FindControl<Menu>("MainMenu") is { } mainMenu)
                mainMenu.Close();
        }

        #endregion

        #region Theme

        private void InitTheme()
        {
            // Build accent swatch buttons
            if (this.FindControl<StackPanel>("AccentSwatchPanel") is { } panel)
            {
                foreach (var accent in ThemeService.AvailableAccents)
                {
                    var color = ThemeService.AccentSwatchColors[accent];
                    var btn = new Button
                    {
                        Tag = accent,
                        Width = 22,
                        Height = 22,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(11),
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color)),
                        BorderThickness = new Thickness(2),
                        BorderBrush = Avalonia.Media.Brushes.Transparent,
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                        Content = "",
                    };
                    btn.Click += OnAccentClick;
                    // Custom template: just a colored circle
                    btn.Template = new Avalonia.Controls.Templates.FuncControlTemplate<Button>((b, _) =>
                    {
                        var border = new Border
                        {
                            CornerRadius = new CornerRadius(11),
                            [!Border.BackgroundProperty] = b[!Button.BackgroundProperty],
                            [!Border.BorderBrushProperty] = b[!Button.BorderBrushProperty],
                            [!Border.BorderThicknessProperty] = b[!Button.BorderThicknessProperty],
                        };
                        return border;
                    });
                    panel.Children.Add(btn);
                }
            }

            ApplyTheme(_themeService.Theme, _themeService.Accent);
        }

        private void OnThemeClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string theme }) return;
            _themeService.SetTheme(theme);
            ApplyTheme(theme, _themeService.Accent);
            CloseSettingsMenu();
        }

        private void OnAccentClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string accent }) return;
            _themeService.SetAccent(accent);
            ApplyTheme(_themeService.Theme, accent);
            CloseSettingsMenu();
        }

        private void ApplyTheme(string theme, string accent)
        {
            var palette = ThemeService.GetPalette(theme, accent);

            // Update application-level resources so ALL windows inherit the palette
            if (Application.Current is { } app)
            {
                foreach (var (key, hex) in palette)
                    app.Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));

                // Update Avalonia theme variant (affects Fluent popup/menu surfaces)
                var variant = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
                    ? Avalonia.Styling.ThemeVariant.Light
                    : Avalonia.Styling.ThemeVariant.Dark; // Dark and Grey both use Dark variant
                app.RequestedThemeVariant = variant;
            }

            // Also set on this window for direct ThemeBrush() lookups
            foreach (var (key, hex) in palette)
                Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));

            // Update theme button visual states
            UpdateThemeButtonStates(theme);
            UpdateAccentSwatchStates(accent);
        }

        private void UpdateThemeButtonStates(string theme)
        {
            foreach (var name in new[] { "ThemeDarkBtn", "ThemeGreyBtn", "ThemeLightBtn" })
            {
                if (this.FindControl<Button>(name) is not { Tag: string tag } btn) continue;
                var active = string.Equals(tag, theme, StringComparison.OrdinalIgnoreCase);
                btn.Foreground = active ? ThemeBrush("TextLabel") : ThemeBrush("TextPrimary");
                btn.BorderBrush = active ? ThemeBrush("AccentBlue") : ThemeBrush("BorderSubtle");
            }
        }

        private void UpdateAccentSwatchStates(string accent)
        {
            if (this.FindControl<StackPanel>("AccentSwatchPanel") is not { } panel) return;
            foreach (var child in panel.Children)
            {
                if (child is not Button btn || btn.Tag is not string tag) continue;
                btn.BorderBrush = string.Equals(tag, accent, StringComparison.OrdinalIgnoreCase)
                    ? Avalonia.Media.Brushes.White
                    : Avalonia.Media.Brushes.Transparent;
            }
        }

        private SolidColorBrush ThemeBrush(string key) =>
            Resources.TryGetValue(key, out var val) && val is SolidColorBrush b
                ? b
                : new SolidColorBrush(Colors.Magenta);

        private void ApplyThemeLabels()
        {
            if (this.FindControl<TextBlock>("ThemeLabel") is { } lbl)
                lbl.Text = GetUiText("ThemeLabel");
            if (this.FindControl<TextBlock>("AccentLabel") is { } albl)
                albl.Text = GetUiText("AccentLabel");
            if (this.FindControl<Button>("ThemeDarkBtn") is { } darkBtn)
                darkBtn.Content = GetUiText("ThemeDark");
            if (this.FindControl<Button>("ThemeGreyBtn") is { } greyBtn)
                greyBtn.Content = GetUiText("ThemeGrey");
            if (this.FindControl<Button>("ThemeLightBtn") is { } lightBtn)
                lightBtn.Content = GetUiText("ThemeLight");
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

            if (saved?.TourCompleted != true && _clips.Count == 0)
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
                _windowStateService.Save(s with { Language = ViewModel.CurrentLanguageCode, OpenPanels = panels, LastOutputDir = lastDir, AutoSaveEncodingPreset = _encodeController.AutoSaveEncodingPreset ? true : null, RecordPanelOpen = _recordOpen ? true : null, TourCompleted = prevTour });
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
                    btn.Background = ThemeBrush("AccentGreen");
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
                    if (FindPanelByName(panelName) is { } panel) panel.IsVisible = true;

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
                _encodeController.AutoSaveEncodingPreset = true;

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
            _clipManager.SaveActiveConfig();

            var clips = new List<ClipSession>();
            for (int i = 0; i < _clips.Count; i++)
            {
                var c = _clips[i];
                clips.Add(new ClipSession(
                    Path:               c.Path,
                    FilterConfig:       c.Config,
                    PresetName:         c.PresetName,
                    BatchSelected:      c.BatchSelected,
                    BatchEncodingPreset: c.BatchEncodingPreset,
                    OutputName:         c.OutputName));
            }

            var encPresetName = (this.FindControl<ComboBox>("RecordPresetCombo")?.SelectedItem as string)?.Trim();
            _sessionService.Save(new SessionState(_activeClipIndex, clips, _encodeController.CaptureCurrentEncodingValues(), encPresetName));
        }

        private void RestoreSessionClips()
        {
            var session = _sessionService.Load();
            if (session?.Clips is not { Count: > 0 } clips) return;

            // Filter out clips whose source files no longer exist
            var validClips = new List<(ClipSession Clip, int OriginalIndex)>();
            for (int i = 0; i < clips.Count; i++)
            {
                var clipPath = clips[i].Path;
                if (File.Exists(clipPath)
                    || _sourceService.IsImageSource(clipPath) && Directory.Exists(Path.GetDirectoryName(clipPath)))
                    validClips.Add((clips[i], i));
            }
            if (validClips.Count == 0) return;

            // Rebuild clip state from session
            _clips.Clear();

            foreach (var (clip, _) in validClips)
            {
                _clips.Add(new ClipState
                {
                    Path = clip.Path,
                    Config = new Dictionary<string, string>(clip.FilterConfig, StringComparer.OrdinalIgnoreCase),
                    PresetName = clip.PresetName,
                    OutputName = clip.OutputName,
                    BatchSelected = clip.BatchSelected,
                    BatchEncodingPreset = clip.BatchEncodingPreset,
                });
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
            var sourcePath = _clips[_activeClipIndex].Path;
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
                _encodeController.ApplyCurrentEncodingValues(encVals);

            // Restore encoding preset combo selection
            if (!string.IsNullOrWhiteSpace(session.EncodingPresetName)
                && this.FindControl<ComboBox>("RecordPresetCombo") is { } encCombo)
            {
                _encodeController.RefreshEncodingPresetCombo();
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
            if (_activeClipIndex < 0 || _activeClipIndex >= _clips.Count) return;
            var currentName = _clips[_activeClipIndex].PresetName;
            if (currentName is not null && currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase)) return;
            _clips[_activeClipIndex].PresetName = _clipManager.GetNextPersoName();
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
            if (_isDroppingFiles) return;
            var paths = GetDroppedFilePaths(e);
            if (paths.Count == 0)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("DropInvalidFileType"));
                return;
            }

            _isDroppingFiles = true;
            try
            {
                // Activate the first dropped file
                await ApplyDetectedSourceAndRefreshAsync(paths[0]);

                // Add remaining dropped files without activating
                for (int i = 1; i < paths.Count; i++)
                {
                    var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(paths[i]));
                    if (!_clips.Any(c => string.Equals(c.Path, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        _clips.Add(new ClipState { Path = normalized, Config = _clipManager.CaptureConfig() });
                    }
                }
                if (paths.Count > 1)
                    RebuildClipTabs();
            }
            catch (Exception ex) { DebugLog($"OnSourceDrop error: {ex.Message}"); }
            finally { _isDroppingFiles = false; }
        }

        private async void OnPlayerFilesDropped(List<string> paths)
        {
            if (_isDroppingFiles) return;
            var valid = new List<string>();
            foreach (var p in paths)
            {
                // Directory dropped → find first image inside
                if (Directory.Exists(p))
                {
                    var firstImage = FindFirstImageInDirectory(p);
                    if (firstImage is not null)
                        valid.Add(firstImage);
                    continue;
                }
                var ext = Path.GetExtension(p);
                if (!string.IsNullOrWhiteSpace(ext) &&
                    (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                     ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)))
                    valid.Add(p);
            }

            if (valid.Count == 0) return;

            _isDroppingFiles = true;
            try
            {
                // Activate the first dropped file
                await ApplyDetectedSourceAndRefreshAsync(valid[0]);

                // Add remaining files without activating
                for (int i = 1; i < valid.Count; i++)
                {
                    var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(valid[i]));
                    if (!_clips.Any(c => string.Equals(c.Path, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        _clips.Add(new ClipState { Path = normalized, Config = _clipManager.CaptureConfig() });
                    }
                }
                if (valid.Count > 1)
                    RebuildClipTabs();
            }
            catch (Exception ex) { DebugLog($"OnPlayerFilesDropped error: {ex.Message}"); }
            finally { _isDroppingFiles = false; }
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

                // Directory dropped → find first image inside
                if (Directory.Exists(path))
                {
                    var firstImage = FindFirstImageInDirectory(path);
                    if (firstImage is not null)
                        paths.Add(firstImage);
                    continue;
                }

                var ext = Path.GetExtension(path);
                if (!string.IsNullOrWhiteSpace(ext) &&
                    (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                     ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)))
                    paths.Add(path);
            }
            return paths;
        }

        /// <summary>Scan a directory for the first image file (sorted by name) matching supported extensions.</summary>
        private static string? FindFirstImageInDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return null;
            return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return !string.IsNullOrWhiteSpace(ext)
                        && ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
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
            _clipManager.AddOrActivate(path, _clipManager.CaptureConfig());
            RebuildClipTabs();
            if (_recordOpen) RebuildBatchClipList();
        }

        // CaptureClipConfig() and SaveActiveClipConfig() moved to ClipManager

        /// <summary>Restores a clip's filter config into _config and refreshes all UI controls.</summary>
        private void RestoreClipConfig(int index)
        {
            if (index < 0 || index >= _clips.Count) return;
            var clipCfg = _clips[index].Config;

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

            for (int i = 0; i < _clips.Count; i++)
            {
                var index = i;
                var path = _clips[i].Path;
                var filename = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(filename)) filename = path;
                var isActive = i == _activeClipIndex;

                var presetName = _clips[i].PresetName;
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
                        : ThemeBrush("TextPrimary"),
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
                        ? ThemeBrush("AccentBlue")
                        : ThemeBrush("BgInput"),
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
            if (index < 0 || index >= _clips.Count || index == _activeClipIndex) return;

            // Save current clip's filter config
            _clipManager.SaveActiveConfig();

            // Switch source (this sets _activeClipIndex via AddOrActivateClip)
            await ApplyDetectedSourceAndRefreshAsync(_clips[index].Path);

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
                var presetName = _activeClipIndex >= 0 && _activeClipIndex < _clips.Count
                    ? _clips[_activeClipIndex].PresetName
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
            var result = _clipManager.Remove(index);
            if (!result.Removed) return;

            if (_clips.Count == 0)
            {
                RebuildClipTabs();
                if (_recordOpen) RebuildBatchClipList();
                await ApplyDetectedSourceAndRefreshAsync(string.Empty);
                return;
            }

            if (result.WasActive)
            {
                await ApplyDetectedSourceAndRefreshAsync(_clips[_activeClipIndex].Path);
                RestoreClipConfig(_activeClipIndex);
                RestoreClipPresetCombo();
                RegenerateScript(showValidationError: false);
                if (TryValidateSourceSelection(out _))
                    await LoadScriptAsync();
            }
            else
            {
                RebuildClipTabs();
            }

            if (_recordOpen) RebuildBatchClipList();
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
                _clipManager.AddOrActivate(normalized, _clipManager.CaptureConfig());
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
                && _activeClipIndex >= 0 && _activeClipIndex < _clips.Count)
            {
                var currentName = _clips[_activeClipIndex].PresetName;
                if (currentName is null || !currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase))
                {
                    _clips[_activeClipIndex].PresetName = _clipManager.GetNextPersoName();
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

            _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
        }

        #endregion

        #region Option toggles & column visibility

#pragma warning disable IDE0028
        private readonly HashSet<string> _openParamPanels = new(StringComparer.Ordinal);
#pragma warning restore IDE0028

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

        private Control? FindPanelByName(string name) =>
            this.FindControl<Control>(name);

        private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string targetName) return;

            if (_openParamPanels.Remove(targetName))
            {
                // Panel is open → collapse it
                if (FindPanelByName(targetName) is { } panel) panel.IsVisible = false;
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
                if (FindPanelByName(targetName) is { } panel) panel.IsVisible = true;
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
                if (FindPanelByName(panel) is { } c) c.IsVisible = false;
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

        private void UpdateToggleButtonPresentation(Button btn, bool isEnabled)
        {
            if (btn.Name is { Length: > 0 } n && OptionButtonLabels.TryGetValue(n, out var l))
                btn.Content = l;
            // else: keep existing Content (e.g. custom filter name set by caller)

            btn.Background  = isEnabled ? ThemeBrush("AccentGreen") : ThemeBrush("BorderAccent");
            btn.BorderBrush = ThemeBrush("BorderAccent");
            btn.Foreground  = isEnabled ? Brushes.White : ThemeBrush("TextLabel");
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

        #region Custom filters

        private CustomFilterPresenter? _customFilterPresenter;

        private CustomFilterPresenter CustomFilters =>
            _customFilterPresenter ??= new CustomFilterPresenter(this, _customFilterService);

        private void RebuildCustomFilterUI() => CustomFilters.RebuildUI();
        private void OnCustomExpandClick(object? sender, RoutedEventArgs e) => CustomFilters.OnExpandClick(sender, e);
        private void OnAddCustomFilterClick(object? sender, RoutedEventArgs e) => CustomFilters.OnAddClick(sender, e);
        private void OnImportCustomFilterClick(object? sender, RoutedEventArgs e) => CustomFilters.OnImportClick(sender, e);

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

        private bool _viewerMaximized;
        private GridLength _savedBottomPanelRow = new(360);
        private bool _halfRes;
        private void OnHalfResClick(object? sender, RoutedEventArgs e)
        {
            _halfRes = !_halfRes;
            if (this.FindControl<Button>("HalfResBtn") is { } btn)
            {
                btn.Background = _halfRes ? ThemeBrush("AccentGreen") : new SolidColorBrush(Color.Parse("#3B4C64"));
                btn.Foreground = Brushes.White;
            }
            UpdateConfigurationValue("preview_half", _halfRes.ToString().ToLowerInvariant());
        }

        private void OnMaxViewerClick(object? sender, RoutedEventArgs e)
        {
            // Close tooltip immediately so it doesn't eat the next click
            if (sender is Button b)
                ToolTip.SetIsOpen(b, false);
            ToggleViewerMaximized();
            e.Handled = true;
        }

        private void ToggleViewerMaximized()
        {
            _mainGrid ??= this.FindControl<Grid>("MainGrid");
            _viewerMaximized = !_viewerMaximized;

            // Hide/show top bar (menu + source row)
            if (this.FindControl<Border>("TopBar") is { } topBar)
                topBar.IsVisible = !_viewerMaximized;

            // Hide/show bottom panel (params) + grid splitter
            if (this.FindControl<Border>("BottomPanel") is { } bottomPanel)
                bottomPanel.IsVisible = !_viewerMaximized;
            if (this.FindControl<GridSplitter>("MainSplitter") is { } splitter)
                splitter.IsVisible = !_viewerMaximized;

            // Adjust grid rows: collapse row 2 (params) when maximized
            if (_mainGrid is not null && _mainGrid.RowDefinitions.Count >= 3)
            {
                if (_viewerMaximized)
                {
                    _savedBottomPanelRow = _mainGrid.RowDefinitions[2].Height;
                    _mainGrid.RowDefinitions[1].Height = new GridLength(0);
                    _mainGrid.RowDefinitions[2].Height = new GridLength(0);
                }
                else
                {
                    _mainGrid.RowDefinitions[1].Height = new GridLength(4);
                    _mainGrid.RowDefinitions[2].Height = _savedBottomPanelRow;
                }
            }

            // Update button appearance
            if (this.FindControl<Button>("MaxViewerBtn") is { } btn)
            {
                btn.Content = _viewerMaximized ? "⛶" : "⛶";
                var tooltipKey = _viewerMaximized ? "RestoreViewerBtn" : "MaxViewerBtn";
                ToolTip.SetTip(btn, GetUiText(tooltipKey));
                btn.Background = _viewerMaximized
                    ? ThemeBrush("AccentGreen")
                    : ThemeBrush("BgInput");
                btn.Foreground = _viewerMaximized
                    ? Brushes.White
                    : ThemeBrush("TextLabel");
            }

            // Force layout update before the next input event
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }

        private bool _isEncoding;
        private bool _recordOpen;

        // ── Encoding delegations to EncodeController ──
        private void OnRecordClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordClick(sender, e);
        private void RebuildBatchClipList() => _encodeController.RebuildBatchClipList();
        private void OnBatchSelectAllClick(object? sender, RoutedEventArgs e) => _encodeController.OnBatchSelectAllClick(sender, e);
        private void OnRecordDirPickClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordDirPickClick(sender, e);
        private void OnRecordDirOpenClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordDirOpenClick(sender, e);
        private void OnRecordStartClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordStartClick(sender, e);
        private void OnRecordPresetSaveClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetSaveClick(sender, e);
        private void OnRecordPresetLoadClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetLoadClick(sender, e);
        private void OnRecordPresetDeleteClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetDeleteClick(sender, e);
        private void OnGammacPresetSaveClick(object? sender, RoutedEventArgs e) => _encodeController.OnGammacPresetSaveClick(sender, e);
        private void OnGammacPresetDeleteClick(object? sender, RoutedEventArgs e) => _encodeController.OnGammacPresetDeleteClick(sender, e);
        private void OnGammacPresetSelectionChanged(object? sender, SelectionChangedEventArgs e) => _encodeController.OnGammacPresetSelectionChanged(sender, e);
        private void UpdateDiskSpaceLabel(string? dirPath) => _encodeController.UpdateDiskSpaceLabel(dirPath);
        private void RefreshEncodingPresetCombo() => _encodeController.RefreshEncodingPresetCombo();
        private void RefreshGammacPresetCombo() => _encodeController.RefreshGammacPresetCombo();

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

                case (Key.F11, KeyModifiers.None):
                    e.Handled = true;
                    ToggleViewerMaximized();
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
            var filterSnap = _clipManager.CaptureConfig();
            for (int i = 0; i < _clips.Count; i++)
                _clips[i].Config = new Dictionary<string, string>(filterSnap, StringComparer.OrdinalIgnoreCase);

            // Set the preset name on all clips
            for (int i = 0; i < _clips.Count; i++)
                _clips[i].PresetName = presetName;
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
            _clipManager.SaveActiveConfig();

            if (TryValidateSourceSelection(out _))
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
            }
            finally { _applyingPreset = false; }
        }

        // GetNextPersoName() moved to ClipManager

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
            if (_activeClipIndex >= 0 && _activeClipIndex < _clips.Count)
                _clips[_activeClipIndex].PresetName = name;

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
                GetUiText("AboutVersion"),
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

        private readonly GuidedTourService _guidedTourService = new();

        private async Task ShowGuidedTourAsync() => await _guidedTourService.RunAsync(this);

        #endregion

        #region Interface implementations (ITourHost, IFilterPresenterHost)

        // ── ITourHost ────────────────────────────────────────────────
        T? ITourHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        SolidColorBrush ITourHost.ThemeBrush(string key) => ThemeBrush(key);
        string ITourHost.GetUiText(string key) => GetUiText(key);
        string ITourHost.CurrentLanguageCode => ViewModel.CurrentLanguageCode;
        Size ITourHost.ClientSize => ClientSize;
        bool ITourHost.IsRecordPanelOpen => _recordOpen;
        void ITourHost.ToggleRecordPanel() => OnRecordClick(null, new RoutedEventArgs());
        void ITourHost.ExpandPanel(string expandButtonName)
        {
            if (this.FindControl<Button>(expandButtonName) is { } btn)
                OnExpandButtonClick(btn, new RoutedEventArgs());
        }
        void ITourHost.ApplyLanguage(string code, bool persist) => ApplyLanguage(code, persist);
        void ITourHost.MarkTourCompleted()
        {
            var settings = _windowStateService.Load();
            if (settings is not null)
                _windowStateService.Save(settings with { TourCompleted = true });
        }

        // ── IFilterPresenterHost ─────────────────────────────────────
        T? IFilterPresenterHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        Window IFilterPresenterHost.Window => this;
        SolidColorBrush IFilterPresenterHost.ThemeBrush(string key) => ThemeBrush(key);
        string IFilterPresenterHost.GetUiText(string key) => GetUiText(key);
        MainWindowViewModel IFilterPresenterHost.ViewModel => ViewModel;
        ConfigStore IFilterPresenterHost.Config => _config;
        double IFilterPresenterHost.WindowHeight => Bounds.Height;
        ThemeService? IFilterPresenterHost.ThemeService => _themeService;
        HashSet<string> IFilterPresenterHost.OpenParamPanels => _openParamPanels;
        void IFilterPresenterHost.UpdateToggleButtonPresentation(Button btn, bool isEnabled) =>
            UpdateToggleButtonPresentation(btn, isEnabled);
        void IFilterPresenterHost.UpdateParamsPlaceholderVisibility() =>
            UpdateParamsPlaceholderVisibility();
        void IFilterPresenterHost.RegenerateScript(bool showValidationError) =>
            RegenerateScript(showValidationError);
        Task IFilterPresenterHost.LoadScriptAsync(bool resetPosition) =>
            LoadScriptAsync(resetPosition);

        // ── IEncodeHost ────────────────────────────────────────────────
        T? IEncodeHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        Window IEncodeHost.Window => this;
        SolidColorBrush IEncodeHost.ThemeBrush(string key) => ThemeBrush(key);
        string IEncodeHost.GetUiText(string key) => GetUiText(key);
        MainWindowViewModel IEncodeHost.ViewModel => ViewModel;
        ConfigStore IEncodeHost.Config => _config;
        IScriptService IEncodeHost.ScriptService => _scriptService;
        SourceService IEncodeHost.SourceService => _sourceService;
        IDialogService IEncodeHost.DialogService => _dialogService;
        PresetService IEncodeHost.EncodingPresetService => _encodingPresetService;
        PresetService IEncodeHost.GammacPresetService => _gammacPresetService;
        CustomFilterService IEncodeHost.CustomFilterService => _customFilterService;
        ClipManager IEncodeHost.ClipManager => _clipManager;
        MpvService? IEncodeHost.MpvService => _mpvService;
        ThemeService IEncodeHost.ThemeService => _themeService;
        IStorageProvider IEncodeHost.StorageProvider => StorageProvider;
        bool IEncodeHost.RecordOpen { get => _recordOpen; set => _recordOpen = value; }
        bool IEncodeHost.IsEncoding { get => _isEncoding; set => _isEncoding = value; }
        bool IEncodeHost.IsInitializing => _isInitializing;
        bool IEncodeHost.IsClosing => _isClosing;
        void IEncodeHost.RegenerateScript(bool showValidationError) => RegenerateScript(showValidationError);
        Task IEncodeHost.LoadScriptAsync(bool resetPosition) => LoadScriptAsync(resetPosition);
        void IEncodeHost.RestoreClipConfig(int index) => RestoreClipConfig(index);
        Regex IEncodeHost.PreviewTrueRegex() => PreviewTrueRegex();
        Regex IEncodeHost.PreviewHalfTrueRegex() => PreviewHalfTrueRegex();
        void IEncodeHost.MoveSliderToPointer(Slider slider, PointerEventArgs e) => MoveSliderToPointer(slider, e);

        #endregion
    }
}
