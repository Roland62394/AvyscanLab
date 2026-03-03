using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, string> UserGuideTexts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "1) Select your source at the top: either a film/video file (film) or an image sequence folder (img).\n" +
                     "2) Enable only the processing blocks you need (crop, denoise, luma, gammac, sharpen...) with the option toggle buttons.\n" +
                     "3) Adjust the parameters in each block. The \"Settings info\" menu provides recommended ranges.\n" +
                     "4) Use preview and play_speed to control preview behavior in the script preview player.\n" +
                     "5) Save your configuration as a preset from the Preset menu, then reload it anytime.\n" +
                     "6) ScriptUser.avs is regenerated automatically whenever values are changed.\n" +
                     "7) To save the final video file, open ScriptUser.avs in VirtualDub with your preferred codec settings.",
            ["fr"] = "1) Sélectionnez votre source en haut : soit un fichier film/vidéo (film), soit un dossier de séquence d'images (img).\n" +
                     "2) Activez uniquement les blocs de traitement nécessaires (crop, denoise, luma, gammac, sharpen...) via les boutons bascule d'options.\n" +
                     "3) Réglez les paramètres de chaque bloc. Le menu \"Infos réglages\" donne les plages recommandées.\n" +
                     "4) Utilisez preview et play_speed pour piloter l'aperçu dans le lecteur de script.\n" +
                     "5) Enregistrez votre configuration via le menu Preset, puis rechargez-la à tout moment.\n" +
                     "6) Le fichier ScriptUser.avs est régénéré automatiquement à chaque modification.\n" +
                     "7) Pour enregistrer la vidéo finale, ouvrez ScriptUser.avs dans VirtualDub avec les options de codec souhaitées.",
            ["de"] = "1) Wählen Sie oben Ihre Quelle: entweder eine Film/Video-Datei (film) oder einen Bildsequenz-Ordner (img).\n" +
                     "2) Aktivieren Sie per Options-Toggle-Schaltflächen nur die benötigten Verarbeitungsblöcke (crop, denoise, luma, gammac, sharpen...).\n" +
                     "3) Passen Sie die Parameter je Block an. Das Menü \"Einstellungsinfos\" zeigt empfohlene Bereiche.\n" +
                     "4) Nutzen Sie preview und play_speed zur Steuerung der Vorschau im Script-Preview-Player.\n" +
                     "5) Speichern Sie Ihre Konfiguration als Preset und laden Sie sie bei Bedarf wieder.\n" +
                     "6) Die Datei ScriptUser.avs wird bei jeder Änderung automatisch neu erzeugt.\n" +
                     "7) Zum Speichern der finalen Videodatei öffnen Sie ScriptUser.avs in VirtualDub mit den gewünschten Codec-Einstellungen.",
            ["es"] = "1) Seleccione la fuente arriba: un archivo de película/vídeo (film) o una carpeta de secuencia de imágenes (img).\n" +
                     "2) Active solo los bloques de procesamiento necesarios (crop, denoise, luma, gammac, sharpen...) con los botones de alternancia de opciones.\n" +
                     "3) Ajuste los parámetros de cada bloque. El menú \"Info ajustes\" muestra rangos recomendados.\n" +
                     "4) Use preview y play_speed para controlar la vista previa en el reproductor de vista previa de script.\n" +
                     "5) Guarde su configuración como preset desde el menú Preset y cárguela cuando quiera.\n" +
                     "6) El archivo ScriptUser.avs se regenera automáticamente con cada cambio.\n" +
                     "7) Para guardar el vídeo final, abra ScriptUser.avs en VirtualDub con las opciones de códec deseadas."
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
        private readonly IWindowStateService  _windowStateService;
        private readonly IDialogService       _dialogService;
        private readonly IAviService          _aviService;
        private readonly Debouncer            _refreshDebouncer = new(TimeSpan.FromMilliseconds(400));
        private readonly Debouncer            _windowStateDebouncer = new(TimeSpan.FromMilliseconds(120));
        private readonly SemaphoreSlim        _refreshGate      = new(1, 1);

        private MpvService? _mpvService;
        private bool        _seekDragging;
        private double      _seekDuration;
        private int         _totalFrames;
        private double      _fps;

        private bool  _suppressTextEvents;
        private bool  _isClosing;
        private bool  _isInitializing;
        private bool  _sourceValidationErrorVisible;
        private Grid? _mainGrid;


        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

        #endregion

        #region Constructor & lifecycle

        public MainWindow()
        {
            _config             = new ConfigStore();
            _sourceService      = new SourceService();
            _aviService         = new AviService();
            _scriptService      = new ScriptService(_sourceService, _aviService);
            _presetService      = new PresetService(GetAppDataPath(PresetsFileName));
            _windowStateService = new WindowStateService(GetAppDataPath(WindowSettingsFileName));
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
            _windowStateService = windowStateService;
            _dialogService      = dialogService;
            _aviService         = aviService;

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            ConfigureMenuBar();
            PreApplyWindowPosition();
            Opened  += OnOpened;
            Closing += OnClosing;
            PositionChanged += OnPositionChanged;
            InitializeChoiceFields();
            UpdateOptionColumnVisibility();
            RegisterChangeHandlers();
            InitPlayerControls();
        }

        private void InitPlayerControls()
        {
            _mpvService = new MpvService();

            if (this.FindControl<MpvHost>("VideoHost") is { } host)
            {
                host.HandleReady += hwnd => _mpvService.Initialize(hwnd);
                host.FileDropped += OnPlayerFileDrop;
            }

            _mpvService.PositionChanged    += pos => Dispatcher.UIThread.Post(() => OnMpvPosition(pos));
            _mpvService.DurationChanged    += dur => Dispatcher.UIThread.Post(() => OnMpvDuration(dur));
            _mpvService.PauseChanged       += p   => Dispatcher.UIThread.Post(() => OnMpvPauseChanged(p));
            _mpvService.FileLoaded         += ()  => Dispatcher.UIThread.Post(() => OnMpvFileLoaded());
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
        }

        private void OnMpvPauseChanged(bool paused)
        {
            if (this.FindControl<Button>("VdbPlay") is { } btn)
                btn.Content = paused ? "▶" : "⏸";
        }

        private void OnMpvFileLoaded()
        {
            if (this.FindControl<Slider>("SeekBar") is { } s) s.Value = 0;

            // Query exact frame count and fps to set an accurate slider maximum.
            // This prevents seeking past the last valid frame, which would crash AviSynth
            // (ImageSource has no file for out-of-range indices) and freeze video players at EOF.
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
        }

        // Positions the window BEFORE Show() is called, so the native window is
        // created directly at the right coordinates (no top-left flash on startup).
        private void PreApplyWindowPosition()
        {
            var saved = _windowStateService.Load();
            if (saved is null) return; // First launch: OnOpened handles it via SnapToBottomOfScreen

            WindowStartupLocation = WindowStartupLocation.Manual;
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
                // Régénère toujours avec la bonne langue au démarrage (indépendamment de la validation source)
                // RegenerateScript(showValidationError: false) ne génère pas si aucune source → script resterait dans l'ancienne langue
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
            SaveWindowSettings();
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
                ("PresetMenuItem",        "PresetMenuItem"),
                ("AboutMenuItem",         "AboutMenuItem"),
            })
            {
                if (this.FindControl<MenuItem>(controlName) is { } item)
                    item.Header = GetUiText(textKey);
            }

            if (this.FindControl<MenuItem>("LanguagesMenu") is { } langMenu)
                langMenu.Header = languageCode.ToUpper();

            SetLanguageMenuChecks();
            ApplyParamTooltips(languageCode);
            ApplyTransportTooltips();

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
            })
            {
                if (this.FindControl<Button>(controlName) is { } btn)
                    ToolTip.SetTip(btn, GetUiText(textKey));
            }
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

        #endregion

        #region Window settings / layout

        private const int WindowBottomPadding = 8;

        private void ApplyStartupLayout(WindowSettings? saved = null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Height = GetStartupHeight(saved);

            if (saved is { X: var sx, Y: var sy } && IsSavedPositionVisible(sx, sy))
                Position = new PixelPoint(sx, sy);
            else
                SnapToBottomOfScreen();
        }

        private double GetStartupHeight(WindowSettings? saved)
        {
            if (saved is not null)
                return Math.Clamp(saved.Height, MinHeight, MaxHeight);

            return GetCompactStartupHeight();
        }

        private double GetCompactStartupHeight()
        {
            return MinHeight;
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

        private void SaveWindowSettings()
        {
            if (!IsVisible || WindowState != WindowState.Normal) return;
            _windowStateService.Save(new WindowSettings(Bounds.Width, Height, Position.X, Position.Y, ViewModel.CurrentLanguageCode));
        }

        private async void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (_isInitializing || _isClosing || WindowState != WindowState.Normal) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                SaveWindowSettings();
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
                        if (_suppressTextEvents) return;
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
                        if (_suppressTextEvents) return;
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
                        if (_suppressTextEvents) return;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: true);
                    };
                    break;
            }
        }

        private void ApplySharpPreset(string preset)
        {
            if (!SharpPresets.TryGetValue(preset, out var values)) return;

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
        }

        private void ApplyDegrainPreset(string preset)
        {
            if (!DegrainPresets.TryGetValue(preset, out var values)) return;

            // Mettre à jour les contrôles UI sans déclencher les handlers individuels
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

            // Mettre à jour la configuration et régénérer le script en une passe par paramètre
            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
        }

        private void ApplyDenoisePreset(string preset)
        {
            if (!DenoisePresets.TryGetValue(preset, out var values)) return;

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
        }

        private void RegisterPathPickers() =>
            RegisterSourcePicker("source", GetUiText("PickSourceTitle"));

        private void RegisterSourcePicker(string name, string title)
        {
            if (this.FindControl<TextBox>(name) is not { } textBox) return;

            textBox.AddHandler(InputElement.PointerPressedEvent, async (_, e) =>
            {
                var chosen = await PromptForSourceAsync(textBox, title);
                if (chosen is not null)
                    await ApplyDetectedSourceAndRefreshAsync(chosen);
                e.Handled = true;
            }, RoutingStrategies.Tunnel, handledEventsToo: true);

            textBox.AddHandler(DragDrop.DragOverEvent, OnSourceDragOver, RoutingStrategies.Bubble);
            textBox.AddHandler(DragDrop.DropEvent,     OnSourceDrop,     RoutingStrategies.Bubble);
        }

        #endregion

        #region Source management

        private async Task ApplyDetectedSourceAndRefreshAsync(string rawValue)
        {
            rawValue ??= string.Empty;
            SetDetectedSourceValue(NormalizeSourceValue(rawValue));

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                RegenerateScript(showValidationError: false);
                return;
            }

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
                await LoadScriptAsync();
                return;
            }

            if (!string.IsNullOrWhiteSpace(msg))
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), msg);
        }

        private void OnSourceDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedFilePath(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnSourceDrop(object? sender, DragEventArgs e)
        {
            var path = GetDroppedFilePath(e);
            if (path is null)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("DropInvalidFileType"));
                return;
            }
            await ApplyDetectedSourceAndRefreshAsync(path);
        }

        private async void OnPlayerFileDrop(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext)) return;
            if (!VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) &&
                !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return;
            await ApplyDetectedSourceAndRefreshAsync(path);
        }

        private static string? GetDroppedFilePath(DragEventArgs e)
        {
#pragma warning disable CS0618
            if (!e.Data.Contains(DataFormats.Files)) return null;
            var items = e.Data.GetFiles()?.ToList();
#pragma warning restore CS0618
            if (items is null || items.Count != 1) return null;
            var path = items[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return null;
            var ext = Path.GetExtension(path);
            return !string.IsNullOrWhiteSpace(ext) &&
                   (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                    ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                ? path : null;
        }

        private async Task<string?> PromptForSourceAsync(TextBox textBox, string title)
        {
            if (StorageProvider is not { } sp) return null;

            var suggestedLocation = await GetSuggestedStartLocationAsync(sp, textBox.Text);
            var filter = BuildSourceFileTypeFilter(textBox.Text);
            var results = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title             = title,
                AllowMultiple     = false,
                SuggestedStartLocation = suggestedLocation,
                FileTypeFilter    = filter
            });

            var filePath = results.Count > 0 ? results[0].TryGetLocalPath() : null;
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            var displayedPath = _sourceService.IsImageSource(filePath)
                ? _sourceService.BuildImageSequenceSourcePath(filePath)
                : filePath;

            textBox.Text = string.Empty;
            textBox.Text = displayedPath;
            return displayedPath;
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

        private string? _activeParamsName = null;

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
            foreach (var name in AllParamPanels)
                if (this.FindControl<Control>(name) is { } p) p.IsVisible = false;

            foreach (var name in AllExpandBtns)
            {
                if (this.FindControl<Button>(name) is not { } b) continue;
                b.Content = "▶";
                b.Classes.Remove("active");
            }
        }

        private void UpdateParamsPlaceholderVisibility(bool hasOpenPanel)
        {
            if (this.FindControl<TextBlock>("ParamsPlaceholder") is { } ph)
                ph.IsVisible = !hasOpenPanel;
        }

        private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string targetName) return;
            if (!IsParamPanelEnabled(targetName))
            {
                if (_activeParamsName == targetName)
                {
                    HideAllParamPanelsAndResetExpandButtons();
                    _activeParamsName = null;
                    UpdateParamsPlaceholderVisibility(hasOpenPanel: false);
                }
                return;
            }

            bool opening = _activeParamsName != targetName;

            // Tout masquer + reset flèches
            HideAllParamPanelsAndResetExpandButtons();

            if (opening)
            {
                if (this.FindControl<Control>(targetName) is { } panel) panel.IsVisible = true;
                btn.Content = "▶";
                btn.Classes.Add("active");
                _activeParamsName = targetName;
            }
            else
            {
                _activeParamsName = null;
            }

            UpdateParamsPlaceholderVisibility(hasOpenPanel: opening);
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
            if (_activeParamsName is null) return;
            if (IsParamPanelEnabled(_activeParamsName)) return;

            HideAllParamPanelsAndResetExpandButtons();
            _activeParamsName = null;
            UpdateParamsPlaceholderVisibility(hasOpenPanel: false);
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

        private void OnVdbStopClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.Pause();

        private void OnVdbNextFrameClick(object? sender, RoutedEventArgs e) =>
            _mpvService?.FrameStep();

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
        private async Task LoadScriptAsync()
        {
            if (_isClosing || _mpvService is null) return;

            await _refreshGate.WaitAsync();
            try
            {
                if (!TryValidateSourceSelection(out _)) return;

                var scriptPath = _scriptService.GetPrimaryScriptPath();
                if (string.IsNullOrWhiteSpace(scriptPath)) return;

                var pos = _mpvService.IsReady ? _mpvService.GetPosition() : 0.0;
                _totalFrames = 0;  // will be refreshed in OnMpvFileLoaded
                _fps         = 0;
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

        private async void OnPresetClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowPresetDialogAsync(this, _presetService, _config, ApplyPresetValuesAsync, ViewModel);

        private async Task ApplyPresetValuesAsync(Dictionary<string, string> values)
        {
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
            RegenerateScript(showValidationError: true);
            UpdateOptionColumnVisibility();

            if (TryValidateSourceSelection(out _))
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
        }

        #endregion

        #region Info dialogs

        private async void OnUserGuideClick(object? sender, RoutedEventArgs e)
        {
            var text = UserGuideTexts.TryGetValue(ViewModel.CurrentLanguageCode, out var loc)
                ? loc : UserGuideTexts["en"];
            await _dialogService.ShowTextDialogAsync(this, GetUiText("UserGuideTitle"), text);
        }

        private async void OnScriptPreviewClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowScriptPreviewDialogAsync(this, _scriptService,
                () => _ = LoadScriptAsync(), ViewModel);

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

        #endregion
    }
}
