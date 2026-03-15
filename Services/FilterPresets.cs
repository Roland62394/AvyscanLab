using System;
using System.Collections.Generic;

namespace CleanScan.Services;

/// <summary>
/// Static filter preset dictionaries, extracted from MainWindow to reduce file size.
/// Each preset maps a preset name to a dictionary of parameter-name → value pairs.
/// </summary>
public static class FilterPresets
{
    public static readonly string[] SharpModeOptions    = ["simple", "edge"];
    public static readonly string[] SharpPresetOptions  = ["léger", "standard", "moyen", "fort", "très fort"];

    public static readonly Dictionary<string, Dictionary<string, string>> SharpPresets = new(StringComparer.OrdinalIgnoreCase)
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

    public static readonly string[] DenoiseModeOptions   = ["removedirtmc", "removedirt"];
    public static readonly string[] DenoisePresetOptions = ["faible", "standard", "moyen", "fort", "très fort"];

    public static readonly Dictionary<string, Dictionary<string, string>> DenoisePresets = new(StringComparer.OrdinalIgnoreCase)
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

    public static readonly string[] DegrainModeOptions      = ["mdegrain2", "mdegrain3", "mdegrain1", "temporal"];
    public static readonly string[] DegrainPrefilterOptions = ["remgrain", "blur", "none"];
    public static readonly string[] DegrainPresetOptions    = ["faible", "standard", "moyen", "fort", "très fort"];

    public static readonly Dictionary<string, Dictionary<string, string>> DegrainPresets = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>Maps individual parameter names back to their parent preset combo-box name.</summary>
    public static readonly Dictionary<string, string> FieldToFilterPresetCombo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sharp_Mode"] = "sharp_preset", ["Sharp_Strength"] = "sharp_preset",
        ["Sharp_Radius"] = "sharp_preset", ["Sharp_Threshold"] = "sharp_preset",
        ["denoise_mode"] = "denoise_preset", ["denoise_strength"] = "denoise_preset", ["denoise_dist"] = "denoise_preset",
        ["degrain_mode"] = "degrain_preset", ["degrain_thSAD"] = "degrain_preset", ["degrain_thSADC"] = "degrain_preset",
        ["degrain_blksize"] = "degrain_preset", ["degrain_overlap"] = "degrain_preset", ["degrain_pel"] = "degrain_preset",
        ["degrain_search"] = "degrain_preset", ["degrain_prefilter"] = "degrain_preset",
    };

    /// <summary>Maps toggle-button config names to their UI labels.</summary>
    public static readonly Dictionary<string, string> OptionButtonLabels = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>Localized tooltip texts for parameter labels, keyed by label control name.</summary>
    public static readonly Dictionary<string, Dictionary<string, string>> ParamTooltipTexts =
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
}
