using System;
using System.Collections.Generic;

namespace AvyScanLab.Services;

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
        ["denoise_grey"]      = "grey",
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
            ["es"] = "Píxeles a recortar a la izquierda.",
            ["it"] = "Pixel da ritagliare a sinistra." },
        ["Label_Crop_T"] = new() {
            ["fr"] = "Pixels à rogner en haut.",
            ["en"] = "Pixels to crop at the top.",
            ["de"] = "Pixel oben abschneiden.",
            ["es"] = "Píxeles a recortar arriba.",
            ["it"] = "Pixel da ritagliare in alto." },
        ["Label_Crop_R"] = new() {
            ["fr"] = "Pixels à rogner à droite.",
            ["en"] = "Pixels to crop on the right.",
            ["de"] = "Pixel rechts abschneiden.",
            ["es"] = "Píxeles a recortar a la derecha.",
            ["it"] = "Pixel da ritagliare a destra." },
        ["Label_Crop_B"] = new() {
            ["fr"] = "Pixels à rogner en bas.",
            ["en"] = "Pixels to crop at the bottom.",
            ["de"] = "Pixel unten abschneiden.",
            ["es"] = "Píxeles a recortar abajo.",
            ["it"] = "Pixel da ritagliare in basso." },

        ["Label_degrain_mode"] = new() {
            ["fr"] = "Algorithme MDegrain. mdegrain2 = 2 références temporelles (recommandé). mdegrain3 = 3 références (meilleur/lent). mdegrain1 = 1 référence (rapide). temporal = TemporalSoften (sans MVTools2).",
            ["en"] = "MDegrain algorithm. mdegrain2 = 2 temporal references (recommended). mdegrain3 = 3 references (best/slow). mdegrain1 = 1 reference (fast). temporal = TemporalSoften (no MVTools2).",
            ["de"] = "MDegrain-Algorithmus. mdegrain2 = 2 zeitliche Referenzen (empfohlen). mdegrain3 = 3 Referenzen (beste/langsam). mdegrain1 = 1 Referenz (schnell). temporal = TemporalSoften (ohne MVTools2).",
            ["es"] = "Algoritmo MDegrain. mdegrain2 = 2 referencias temporales (recomendado). mdegrain3 = 3 referencias (mejor/lento). mdegrain1 = 1 referencia (rápido). temporal = TemporalSoften (sin MVTools2).",
            ["it"] = "Algoritmo MDegrain. mdegrain2 = 2 riferimenti temporali (consigliato). mdegrain3 = 3 riferimenti (migliore/lento). mdegrain1 = 1 riferimento (veloce). temporal = TemporalSoften (senza MVTools2)." },
        ["Label_degrain_thSAD"] = new() {
            ["fr"] = "Seuil SAD luma — intensité du degrain sur la luminance. Grain fin : 200. Moyen : 350 (défaut). Fort : 500. Plus la valeur est haute, plus le degrain est fort.",
            ["en"] = "Luma SAD threshold — degrain strength on luminance. Fine grain: 200. Medium: 350 (default). Heavy: 500. Higher value = stronger degrain.",
            ["de"] = "Luma-SAD-Schwellwert — Kornreduktionsstärke auf Luminanz. Feinkorn: 200. Mittel: 350 (Standard). Stark: 500. Höherer Wert = stärkere Kornreduktion.",
            ["es"] = "Umbral SAD luma — intensidad del degrain en luminancia. Grano fino: 200. Medio: 350 (predeterminado). Fuerte: 500. Mayor valor = degrain más fuerte.",
            ["it"] = "Soglia SAD luma — intensità del degrain sulla luminanza. Grana fine: 200. Media: 350 (predefinito). Forte: 500. Valore più alto = degrain più forte." },
        ["Label_degrain_thSADC"] = new() {
            ["fr"] = "Seuil SAD chroma — intensité du degrain sur les couleurs. Grain fin : 150. Moyen : 250 (défaut). Fort : 400. Généralement 2/3 de thSAD.",
            ["en"] = "Chroma SAD threshold — degrain strength on color channels. Fine grain: 150. Medium: 250 (default). Heavy: 400. Typically 2/3 of thSAD.",
            ["de"] = "Chroma-SAD-Schwellwert — Kornreduktionsstärke auf Farbkanälen. Feinkorn: 150. Mittel: 250 (Standard). Stark: 400. Typischerweise 2/3 von thSAD.",
            ["es"] = "Umbral SAD chroma — intensidad del degrain en canales de color. Grano fino: 150. Medio: 250 (predeterminado). Fuerte: 400. Generalmente 2/3 de thSAD.",
            ["it"] = "Soglia SAD chroma — intensità del degrain sui canali colore. Grana fine: 150. Media: 250 (predefinito). Forte: 400. Tipicamente 2/3 di thSAD." },
        ["Label_degrain_blksize"] = new() {
            ["fr"] = "Taille des blocs d'analyse MVTools (pixels). 8 = précis/lent. 16 = équilibré (défaut). 32 = rapide/moins précis. overlap doit toujours être blksize/2.",
            ["en"] = "MVTools analysis block size (pixels). 8 = precise/slow. 16 = balanced (default). 32 = fast/less precise. overlap must always be blksize/2.",
            ["de"] = "MVTools-Analyseblockgröße (Pixel). 8 = präzise/langsam. 16 = ausgewogen (Standard). 32 = schnell/weniger präzise. overlap muss immer blksize/2 sein.",
            ["es"] = "Tamaño de bloque de análisis MVTools (píxeles). 8 = preciso/lento. 16 = equilibrado (predeterminado). 32 = rápido/menos preciso. overlap debe ser siempre blksize/2.",
            ["it"] = "Dimensione blocchi di analisi MVTools (pixel). 8 = preciso/lento. 16 = equilibrato (predefinito). 32 = veloce/meno preciso. overlap deve sempre essere blksize/2." },
        ["Label_degrain_overlap"] = new() {
            ["fr"] = "Chevauchement des blocs d'analyse (pixels). Doit toujours être égal à blksize/2. Ex : blksize=16 → overlap=8. blksize=8 → overlap=4. Défaut : 8.",
            ["en"] = "Analysis block overlap (pixels). Must always equal blksize/2. E.g.: blksize=16 → overlap=8. blksize=8 → overlap=4. Default: 8.",
            ["de"] = "Analyseblock-Überlappung (Pixel). Muss immer blksize/2 entsprechen. Bsp.: blksize=16 → overlap=8. blksize=8 → overlap=4. Standard: 8.",
            ["es"] = "Superposición de bloques de análisis (píxeles). Debe ser siempre blksize/2. Ej.: blksize=16 → overlap=8. blksize=8 → overlap=4. Predeterminado: 8.",
            ["it"] = "Sovrapposizione blocchi di analisi (pixel). Deve sempre essere blksize/2. Es.: blksize=16 → overlap=8. blksize=8 → overlap=4. Predefinito: 8." },
        ["Label_degrain_pel"] = new() {
            ["fr"] = "Précision sub-pixel de MSuper. 1 = pixel entier (~2× plus rapide, recommandé pour du grain). 2 = demi-pixel (plus précis, mais MSuper 4× plus lourd). Défaut : 1.",
            ["en"] = "MSuper sub-pixel precision. 1 = full pixel (~2× faster, recommended for grain). 2 = half pixel (more precise, but MSuper 4× heavier). Default: 1.",
            ["de"] = "MSuper-Subpixel-Präzision. 1 = ganzes Pixel (~2× schneller, empfohlen für Korn). 2 = halbes Pixel (präziser, aber MSuper 4× schwerer). Standard: 1.",
            ["es"] = "Precisión sub-pixel de MSuper. 1 = pixel entero (~2× más rápido, recomendado para grano). 2 = medio pixel (más preciso, pero MSuper 4× más pesado). Predeterminado: 1.",
            ["it"] = "Precisione sub-pixel di MSuper. 1 = pixel intero (~2× più veloce, consigliato per la grana). 2 = mezzo pixel (più preciso, ma MSuper 4× più pesante). Predefinito: 1." },
        ["Label_degrain_search"] = new() {
            ["fr"] = "Algorithme de recherche des vecteurs de mouvement MVTools. 0 = oneway (rapide). 2 = hexagone (équilibré, recommandé). 3 = exhaustif (précis/lent). Défaut : 2.",
            ["en"] = "MVTools motion vector search algorithm. 0 = oneway (fast). 2 = hexagonal (balanced, recommended). 3 = exhaustive (precise/slow). Default: 2.",
            ["de"] = "MVTools-Bewegungsvektor-Suchalgorithmus. 0 = oneway (schnell). 2 = hexagonal (ausgewogen, empfohlen). 3 = exhaustiv (präzise/langsam). Standard: 2.",
            ["es"] = "Algoritmo de búsqueda de vectores de movimiento MVTools. 0 = oneway (rápido). 2 = hexagonal (equilibrado, recomendado). 3 = exhaustivo (preciso/lento). Predeterminado: 2.",
            ["it"] = "Algoritmo di ricerca vettori di movimento MVTools. 0 = oneway (veloce). 2 = esagonale (equilibrato, consigliato). 3 = esaustivo (preciso/lento). Predefinito: 2." },
        ["Label_degrain_prefilter"] = new() {
            ["fr"] = "Lissage appliqué à une copie temporaire du clip pour aider MVTools à estimer les mouvements. Le degrain s'applique toujours au clip original — ce réglage améliore la précision des vecteurs de mouvement et donc l'efficacité du degrain, sans filtrer directement l'image de sortie. remgrain = RemoveGrain(2) rapide (recommandé). blur = Blur(1.0). none = aucun.",
            ["en"] = "Smoothing applied to a temporary copy of the clip to help MVTools estimate motion. Degrain is always applied to the original clip — this setting improves motion vector accuracy and thus degrain effectiveness, without directly filtering the output image. remgrain = RemoveGrain(2) fast (recommended). blur = Blur(1.0). none = none.",
            ["de"] = "Glättung auf eine temporäre Clip-Kopie angewendet, um MVTools bei der Bewegungsschätzung zu helfen. Degrain wird immer auf den Originalclip angewendet — diese Einstellung verbessert die Bewegungsvektor-Genauigkeit und damit die Degrain-Effektivität, ohne das Ausgabebild direkt zu filtern. remgrain = RemoveGrain(2) schnell (empfohlen). blur = Blur(1.0). none = kein.",
            ["es"] = "Suavizado aplicado a una copia temporal del clip para ayudar a MVTools a estimar el movimiento. El degrain siempre se aplica al clip original — este ajuste mejora la precisión de los vectores de movimiento y por tanto la eficacia del degrain, sin filtrar directamente la imagen de salida. remgrain = RemoveGrain(2) rápido (recomendado). blur = Blur(1.0). none = ninguno.",
            ["it"] = "Smoothing applicato a una copia temporanea del clip per aiutare MVTools a stimare il movimento. Il degrain viene sempre applicato al clip originale — questa impostazione migliora la precisione dei vettori di movimento e quindi l'efficacia del degrain, senza filtrare direttamente l'immagine di output. remgrain = RemoveGrain(2) veloce (consigliato). blur = Blur(1.0). none = nessuno." },
        ["Label_degrain_preset"] = new() {
            ["fr"] = "Réglage rapide de l'intensité du degrain. Applique automatiquement des valeurs cohérentes sur tous les paramètres. Vous pouvez ensuite affiner manuellement chaque valeur. faible=grain léger/rapide. standard=pellicule 8–16mm (défaut). moyen=grain marqué. fort=grain dense. très fort=grain très prononcé/lent.",
            ["en"] = "Quick degrain intensity preset. Automatically applies consistent values across all parameters. You can then fine-tune each value manually. faible=light grain/fast. standard=8–16mm film (default). moyen=noticeable grain. fort=dense grain. très fort=very heavy grain/slow.",
            ["de"] = "Schnell-Voreinstellung für die Degrain-Intensität. Setzt automatisch konsistente Werte für alle Parameter. Anschließend kann jeder Wert manuell feinabgestimmt werden. faible=leichtes Korn/schnell. standard=8–16mm-Film (Standard). moyen=deutliches Korn. fort=dichtes Korn. très fort=sehr starkes Korn/langsam.",
            ["es"] = "Ajuste rápido de intensidad del degrain. Aplica automáticamente valores coherentes en todos los parámetros. Puede afinar manualmente cada valor después. faible=grano leve/rápido. standard=película 8–16mm (predeterminado). moyen=grano notable. fort=grano denso. très fort=grano muy pronunciado/lento.",
            ["it"] = "Preset rapido per l'intensità del degrain. Applica automaticamente valori coerenti su tutti i parametri. Puoi poi regolare manualmente ogni valore. faible=grana leggera/veloce. standard=pellicola 8–16mm (predefinito). moyen=grana evidente. fort=grana densa. très fort=grana molto pronunciata/lenta." },

        ["Label_denoise_mode"] = new() {
            ["fr"] = "Algorithme de suppression des poussières. removedirtmc = avec compensation de mouvement (recommandé) | removedirt = plus rapide.",
            ["en"] = "Dust removal algorithm. removedirtmc = motion-compensated (recommended) | removedirt = faster.",
            ["de"] = "Staubentfernungs-Algorithmus. removedirtmc = bewegungskompensiert (empfohlen) | removedirt = schneller.",
            ["es"] = "Algoritmo de eliminación de polvo. removedirtmc = con compensación de movimiento (recomendado) | removedirt = más rápido.",
            ["it"] = "Algoritmo di rimozione polvere. removedirtmc = con compensazione del movimento (consigliato) | removedirt = più veloce." },
        ["Label_denoise_strength"] = new() {
            ["fr"] = "Seuil de détection temporelle des poussières. Pellicule propre : 6, moyenne : 10, sale : 14. Défaut : 10. Max conseillé : 18.",
            ["en"] = "Temporal dust detection threshold. Clean film: 6, average: 10, dirty: 14. Default: 10. Max advised: 18.",
            ["de"] = "Zeitlicher Stauberkennungsschwellwert. Sauberer Film: 6, mittel: 10, verschmutzt: 14. Standard: 10. Max empfohlen: 18.",
            ["es"] = "Umbral de detección temporal de polvo. Película limpia: 6, media: 10, sucia: 14. Predeterminado: 10. Máx. aconsejado: 18.",
            ["it"] = "Soglia di rilevamento temporale della polvere. Pellicola pulita: 6, media: 10, sporca: 14. Predefinito: 10. Max consigliato: 18." },
        ["Label_denoise_dist"] = new() {
            ["fr"] = "Rayon de recherche spatial pour la réparation. Augmenter pour les taches plus grandes (3→6→10). Défaut : 3. Min : 1.",
            ["en"] = "Spatial search radius for repair. Increase for larger spots (3→6→10). Default: 3. Min: 1.",
            ["de"] = "Räumlicher Suchradius für Reparatur. Erhöhen für größere Flecken (3→6→10). Standard: 3. Min: 1.",
            ["es"] = "Radio de búsqueda espacial para reparación. Aumentar para manchas más grandes (3→6→10). Predeterminado: 3. Mín: 1.",
            ["it"] = "Raggio di ricerca spaziale per la riparazione. Aumentare per macchie più grandi (3→6→10). Predefinito: 3. Min: 1." },
        ["Label_denoise_grey"] = new() {
            ["fr"] = "true = traitement luma seul (plus rapide). false = luma + chroma (meilleure restitution couleur). Défaut : false.",
            ["en"] = "true = luma only (faster). false = luma + chroma (better color accuracy). Default: false.",
            ["de"] = "true = nur Luma (schneller). false = Luma + Chroma (bessere Farbgenauigkeit). Standard: false.",
            ["es"] = "true = solo luma (más rápido). false = luma + chroma (mejor precisión de color). Predeterminado: false.",
            ["it"] = "true = solo luma (più veloce). false = luma + chroma (migliore accuratezza dei colori). Predefinito: false." },
        ["Label_denoise_preset"] = new() {
            ["fr"] = "Réglage rapide de l'intensité de la suppression des poussières. Applique automatiquement des valeurs cohérentes sur mode, strength et dist. faible=poussière légère. standard=pellicule moyenne (défaut). moyen=pellicule sale. fort=très sale/grandes taches. très fort=cas extrêmes.",
            ["en"] = "Quick dust removal intensity preset. Automatically applies consistent values for mode, strength and dist. faible=light dust. standard=average film (default). moyen=dirty film. fort=very dirty/large spots. très fort=extreme cases.",
            ["de"] = "Schnell-Voreinstellung für die Staubentfernungsintensität. Setzt automatisch konsistente Werte für mode, strength und dist. faible=leichter Staub. standard=durchschnittlicher Film (Standard). moyen=schmutziger Film. fort=sehr schmutzig/große Flecken. très fort=extreme Fälle.",
            ["es"] = "Ajuste rápido de intensidad de eliminación de polvo. Aplica automáticamente valores coherentes para mode, strength y dist. faible=polvo leve. standard=película media (predeterminado). moyen=película sucia. fort=muy sucia/manchas grandes. très fort=casos extremos.",
            ["it"] = "Preset rapido per l'intensità della rimozione polvere. Applica automaticamente valori coerenti su mode, strength e dist. faible=polvere leggera. standard=pellicola media (predefinito). moyen=pellicola sporca. fort=molto sporca/macchie grandi. très fort=casi estremi." },

        ["Label_Lum_Bright"] = new() {
            ["fr"] = "Luminosité (offset additif). Défaut : 0. Plage : -255..255. Recommandé : -30..30.",
            ["en"] = "Brightness (additive offset). Default: 0. Range: -255..255. Recommended: -30..30.",
            ["de"] = "Helligkeit (additiver Versatz). Standard: 0. Bereich: -255..255. Empfohlen: -30..30.",
            ["es"] = "Brillo (desplazamiento aditivo). Predeterminado: 0. Rango: -255..255. Recomendado: -30..30.",
            ["it"] = "Luminosità (offset additivo). Predefinito: 0. Intervallo: -255..255. Consigliato: -30..30." },
        ["Label_Lum_Contrast"] = new() {
            ["fr"] = "Contraste (facteur multiplicatif). Défaut : 1.05. Min > 0. Courant : 0.5..2.0.",
            ["en"] = "Contrast (multiplicative factor). Default: 1.05. Min > 0. Typical: 0.5..2.0.",
            ["de"] = "Kontrast (multiplikativer Faktor). Standard: 1,05. Min > 0. Typisch: 0,5..2,0.",
            ["es"] = "Contraste (factor multiplicativo). Predeterminado: 1,05. Mín > 0. Típico: 0,5..2,0.",
            ["it"] = "Contrasto (fattore moltiplicativo). Predefinito: 1,05. Min > 0. Tipico: 0,5..2,0." },
        ["Label_Lum_Sat"] = new() {
            ["fr"] = "Saturation des couleurs. Neutre : 1.0. Défaut : 1.10. Min > 0. Courant : 0.5..2.0.",
            ["en"] = "Color saturation. Neutral: 1.0. Default: 1.10. Min > 0. Typical: 0.5..2.0.",
            ["de"] = "Farbsättigung. Neutral: 1,0. Standard: 1,10. Min > 0. Typisch: 0,5..2,0.",
            ["es"] = "Saturación de color. Neutro: 1,0. Predeterminado: 1,10. Mín > 0. Típico: 0,5..2,0.",
            ["it"] = "Saturazione dei colori. Neutro: 1,0. Predefinito: 1,10. Min > 0. Tipico: 0,5..2,0." },
        ["Label_Lum_Hue"] = new() {
            ["fr"] = "Décalage de teinte en degrés. Défaut : 0.0. Courant : -180..180. Petits pas conseillés : ±2..5.",
            ["en"] = "Hue shift in degrees. Default: 0.0. Typical: -180..180. Small steps recommended: ±2..5.",
            ["de"] = "Farbtonversatz in Grad. Standard: 0,0. Typisch: -180..180. Kleine Schritte empfohlen: ±2..5.",
            ["es"] = "Desplazamiento de tono en grados. Predeterminado: 0,0. Típico: -180..180. Pasos pequeños recomendados: ±2..5.",
            ["it"] = "Spostamento della tinta in gradi. Predefinito: 0,0. Tipico: -180..180. Piccoli passi consigliati: ±2..5." },
        ["Label_Lum_GammaY"] = new() {
            ["fr"] = "Gamma appliqué à la luminance. Défaut : 1.30. Min > 0. Courant : 0.5..3.0. >1 = éclaircit.",
            ["en"] = "Gamma applied to luminance. Default: 1.30. Min > 0. Typical: 0.5..3.0. >1 = brightens.",
            ["de"] = "Auf Luminanz angewendetes Gamma. Standard: 1,30. Min > 0. Typisch: 0,5..3,0. >1 = heller.",
            ["es"] = "Gamma aplicado a la luminancia. Predeterminado: 1,30. Mín > 0. Típico: 0,5..3,0. >1 = aclara.",
            ["it"] = "Gamma applicato alla luminanza. Predefinito: 1,30. Min > 0. Tipico: 0,5..3,0. >1 = schiarisce." },

        ["Label_LockChan"] = new() {
            ["fr"] = "Canal verrouillé (inchangé) lors de la correction GamMac.\n0 = verrouiller Rouge (R)\n1 = verrouiller Vert (G) — défaut : R et B sont ajustés pour rejoindre G\n2 = verrouiller Bleu (B)\n-1 = valeur explicite LockVal utilisée comme cible\n-2 = moyenne des 3 canaux (R+G+B, \"scaled average\")\n-3 = canal médian : GamMac choisit celui dont la moyenne est entre les deux autres, puis agit comme 0, 1 ou 2",
            ["en"] = "Channel locked (unchanged) during GamMac correction.\n0 = lock Red (R)\n1 = lock Green (G) — default: R and B are adjusted to match G\n2 = lock Blue (B)\n-1 = use explicit LockVal as target\n-2 = average of 3 channels (R+G+B, \"scaled average\")\n-3 = median channel: GamMac picks the one whose average is between the other two, then acts as 0, 1 or 2",
            ["de"] = "Gesperrter (unveränderter) Kanal bei der GamMac-Korrektur.\n0 = Rot (R) sperren\n1 = Grün (G) sperren — Standard: R und B werden an G angepasst\n2 = Blau (B) sperren\n-1 = expliziten LockVal-Wert als Ziel verwenden\n-2 = Durchschnitt der 3 Kanäle (R+G+B, \"scaled average\")\n-3 = Median-Kanal: GamMac wählt den Kanal, dessen Mittelwert zwischen den anderen liegt, und verhält sich wie 0, 1 oder 2",
            ["es"] = "Canal bloqueado (sin cambios) durante la corrección GamMac.\n0 = bloquear Rojo (R)\n1 = bloquear Verde (G) — predeterminado: R y B se ajustan para coincidir con G\n2 = bloquear Azul (B)\n-1 = usar el valor explícito LockVal como objetivo\n-2 = promedio de los 3 canales (R+G+B, \"scaled average\")\n-3 = canal mediano: GamMac elige el canal cuyo promedio está entre los otros dos y actúa como 0, 1 o 2",
            ["it"] = "Canale bloccato (invariato) durante la correzione GamMac.\n0 = blocca Rosso (R)\n1 = blocca Verde (G) — predefinito: R e B vengono regolati per allinearsi a G\n2 = blocca Blu (B)\n-1 = usa il valore esplicito LockVal come obiettivo\n-2 = media dei 3 canali (R+G+B, \"scaled average\")\n-3 = canale mediano: GamMac sceglie quello la cui media è tra gli altri due, poi agisce come 0, 1 o 2" },
        ["Label_LockVal"] = new() {
            ["fr"] = "Valeur cible du canal de référence. Défaut : 250. Plage : 0..255.",
            ["en"] = "Target value for the reference channel. Default: 250. Range: 0..255.",
            ["de"] = "Zielwert für den Referenzkanal. Standard: 250. Bereich: 0..255.",
            ["es"] = "Valor objetivo del canal de referencia. Predeterminado: 250. Rango: 0..255.",
            ["it"] = "Valore obiettivo del canale di riferimento. Predefinito: 250. Intervallo: 0..255." },
        ["Label_Scale"] = new() {
            ["fr"] = "Facteur d'amplitude de la correction colorimétrique. Défaut : 2. Min : 0. Courant : 0..10.",
            ["en"] = "Amplitude factor of the color correction. Default: 2. Min: 0. Typical: 0..10.",
            ["de"] = "Amplitudenfaktor der Farbkorrektur. Standard: 2. Min: 0. Typisch: 0..10.",
            ["es"] = "Factor de amplitud de la corrección de color. Predeterminado: 2. Mín: 0. Típico: 0..10.",
            ["it"] = "Fattore di ampiezza della correzione del colore. Predefinito: 2. Min: 0. Tipico: 0..10." },
        ["Label_Th"] = new() {
            ["fr"] = "Seuil bas de détection des zones à corriger. Défaut : 0.12. Plage : 0..1.",
            ["en"] = "Low detection threshold for areas to correct. Default: 0.12. Range: 0..1.",
            ["de"] = "Unterer Erkennungsschwellwert für zu korrigierende Bereiche. Standard: 0,12. Bereich: 0..1.",
            ["es"] = "Umbral bajo de detección de zonas a corregir. Predeterminado: 0,12. Rango: 0..1.",
            ["it"] = "Soglia bassa di rilevamento delle zone da correggere. Predefinito: 0,12. Intervallo: 0..1." },
        ["Label_HiTh"] = new() {
            ["fr"] = "Seuil haut de détection des zones à corriger. Défaut : 0.25. Plage : 0..1.",
            ["en"] = "High detection threshold for areas to correct. Default: 0.25. Range: 0..1.",
            ["de"] = "Oberer Erkennungsschwellwert für zu korrigierende Bereiche. Standard: 0,25. Bereich: 0..1.",
            ["es"] = "Umbral alto de detección de zonas a corregir. Predeterminado: 0,25. Rango: 0..1.",
            ["it"] = "Soglia alta di rilevamento delle zone da correggere. Predefinito: 0,25. Intervallo: 0..1." },
        ["Label_X"] = new() {
            ["fr"] = "Colonne gauche de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
            ["en"] = "Left column of the analysis region (0 = whole image). Default: 0. Min: 0.",
            ["de"] = "Linke Spalte des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
            ["es"] = "Columna izquierda de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0.",
            ["it"] = "Colonna sinistra della regione di analisi (0 = tutta l'immagine). Predefinito: 0. Min: 0." },
        ["Label_Y"] = new() {
            ["fr"] = "Ligne haute de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
            ["en"] = "Top row of the analysis region (0 = whole image). Default: 0. Min: 0.",
            ["de"] = "Oberste Zeile des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
            ["es"] = "Fila superior de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0.",
            ["it"] = "Riga superiore della regione di analisi (0 = tutta l'immagine). Predefinito: 0. Min: 0." },
        ["Label_W"] = new() {
            ["fr"] = "Largeur de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
            ["en"] = "Width of the analysis region (0 = whole image). Default: 0. Min: 0.",
            ["de"] = "Breite des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
            ["es"] = "Ancho de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0.",
            ["it"] = "Larghezza della regione di analisi (0 = tutta l'immagine). Predefinito: 0. Min: 0." },
        ["Label_H"] = new() {
            ["fr"] = "Hauteur de la région d'analyse (0 = toute l'image). Défaut : 0. Min : 0.",
            ["en"] = "Height of the analysis region (0 = whole image). Default: 0. Min: 0.",
            ["de"] = "Höhe des Analysebereichs (0 = gesamtes Bild). Standard: 0. Min: 0.",
            ["es"] = "Altura de la región de análisis (0 = imagen completa). Predeterminado: 0. Mín: 0.",
            ["it"] = "Altezza della regione di analisi (0 = tutta l'immagine). Predefinito: 0. Min: 0." },
        ["Label_Omin"] = new() {
            ["fr"] = "Valeur minimale de sortie (écrêtage bas). Défaut : 0. Plage : 0..255.",
            ["en"] = "Minimum output value (low clipping). Default: 0. Range: 0..255.",
            ["de"] = "Minimaler Ausgabewert (Unterabschneidung). Standard: 0. Bereich: 0..255.",
            ["es"] = "Valor mínimo de salida (recorte bajo). Predeterminado: 0. Rango: 0..255.",
            ["it"] = "Valore minimo di output (clipping basso). Predefinito: 0. Intervallo: 0..255." },
        ["Label_Omax"] = new() {
            ["fr"] = "Valeur maximale de sortie (écrêtage haut). Défaut : 255. Plage : 0..255.",
            ["en"] = "Maximum output value (high clipping). Default: 255. Range: 0..255.",
            ["de"] = "Maximaler Ausgabewert (Oberabschneidung). Standard: 255. Bereich: 0..255.",
            ["es"] = "Valor máximo de salida (recorte alto). Predeterminado: 255. Rango: 0..255.",
            ["it"] = "Valore massimo di output (clipping alto). Predefinito: 255. Intervallo: 0..255." },
        ["Label_ShowPreview"] = new() {
            ["fr"] = "Affiche la région d'analyse en surimpression dans la prévisualisation. Défaut : false.",
            ["en"] = "Shows the analysis region as an overlay in the preview. Default: false.",
            ["de"] = "Zeigt den Analysebereich als Überlagerung in der Vorschau. Standard: false.",
            ["es"] = "Muestra la región de análisis como superposición en la vista previa. Predeterminado: false.",
            ["it"] = "Mostra la regione di analisi come sovrapposizione nell'anteprima. Predefinito: false." },
        ["Label_Verbosity"] = new() {
            ["fr"] = "Niveau de détail du log GamMac. Défaut : 4. Plage : 0..6.",
            ["en"] = "GamMac log detail level. Default: 4. Range: 0..6.",
            ["de"] = "GamMac-Protokolldetailstufe. Standard: 4. Bereich: 0..6.",
            ["es"] = "Nivel de detalle del log GamMac. Predeterminado: 4. Rango: 0..6.",
            ["it"] = "Livello di dettaglio del log GamMac. Predefinito: 4. Intervallo: 0..6." },

        ["Label_Sharp_Strength"] = new() {
            ["fr"] = "Intensité du renforcement (échelle 1–20). simple : recommandé 5–10. edge : peut être plus élevé (10–20) car le masque protège les zones plates. Défaut : 8.",
            ["en"] = "Sharpening strength (scale 1–20). simple: recommended 5–10. edge: can be higher (10–20) since the mask protects flat areas. Default: 8.",
            ["de"] = "Schärfungsstärke (Skala 1–20). simple: empfohlen 5–10. edge: kann höher sein (10–20), da die Maske flache Bereiche schützt. Standard: 8.",
            ["es"] = "Intensidad del enfoque (escala 1–20). simple: recomendado 5–10. edge: puede ser mayor (10–20) ya que la máscara protege las zonas planas. Predeterminado: 8.",
            ["it"] = "Intensità della nitidezza (scala 1–20). simple: consigliato 5–10. edge: può essere più alto (10–20) poiché la maschera protegge le zone piatte. Predefinito: 8." },
        ["Label_Sharp_Mode"] = new() {
            ["fr"] = "simple = Sharpen() natif global, efficace, légère amplification du grain possible sur les zones plates. edge = renforcement uniquement sur les contours d'objets via masque dual-Sobel + compression gamma (technique LimitedSharpenFaster/Didée) — le grain dans les zones plates est naturellement exclu.",
            ["en"] = "simple = global native Sharpen(), effective, slight grain amplification possible on flat areas. edge = sharpening only on object contours via dual-Sobel mask + gamma compression (LimitedSharpenFaster/Didée technique) — grain in flat areas is naturally excluded.",
            ["de"] = "simple = globales natives Sharpen(), effektiv, leichte Kornverstärkung auf flachen Flächen möglich. edge = Schärfung nur an Objektkanten via Doppel-Sobel-Maske + Gammakompression (LimitedSharpenFaster/Didée-Technik) — Korn in flachen Bereichen wird natürlich ausgeschlossen.",
            ["es"] = "simple = Sharpen() global nativo, efectivo, posible leve amplificación del grano en zonas planas. edge = enfoque solo en contornos de objetos mediante máscara Sobel dual + compresión gamma (técnica LimitedSharpenFaster/Didée) — el grano en zonas planas queda excluido naturalmente.",
            ["it"] = "simple = Sharpen() nativo globale, efficace, possibile leggera amplificazione della grana sulle zone piatte. edge = nitidezza solo sui contorni degli oggetti tramite maschera dual-Sobel + compressione gamma (tecnica LimitedSharpenFaster/Didée) — la grana nelle zone piatte viene naturalmente esclusa." },
        ["Label_Sharp_Radius"] = new() {
            ["fr"] = "Rayon de l'unsharp mask : contrôle l'étendue du halo de renforcement. 1.0 = netteté fine et précise (grain peu affecté). 2.0–3.0 = netteté large et prononcée (plus visible, mais peut amplifier le grain). Recommandé : 1.0–2.0. Défaut : 1.5.",
            ["en"] = "Unsharp mask radius: controls the extent of the sharpening halo. 1.0 = tight, precise sharpening (grain barely affected). 2.0–3.0 = broad, pronounced sharpening (more visible, but may amplify grain). Recommended: 1.0–2.0. Default: 1.5.",
            ["de"] = "Radius der Unsharp Mask: steuert die Breite des Schärfungshalos. 1.0 = feine, präzise Schärfung (Korn kaum beeinflusst). 2.0–3.0 = breite, ausgeprägte Schärfung (sichtbarer, kann Korn verstärken). Empfohlen: 1.0–2.0. Standard: 1,5.",
            ["es"] = "Radio de la Unsharp Mask: controla la amplitud del halo de enfoque. 1.0 = enfoque fino y preciso (grano apenas afectado). 2.0–3.0 = enfoque amplio y pronunciado (más visible, puede amplificar el grano). Recomendado: 1.0–2.0. Predeterminado: 1,5.",
            ["it"] = "Raggio della unsharp mask: controlla l'estensione dell'alone di nitidezza. 1.0 = nitidezza fine e precisa (grana appena influenzata). 2.0–3.0 = nitidezza ampia e pronunciata (più visibile, può amplificare la grana). Consigliato: 1.0–2.0. Predefinito: 1,5." },
        ["Label_Sharp_Threshold"] = new() {
            ["fr"] = "Mode edge uniquement : seuil de réponse minimal du masque (après compression gamma) pour déclencher le renforcement. La compression gamma seule suffit souvent — utiliser threshold=0 pour la désactiver. Recommandé si du grain résiduel persiste : 20–40. Sans effet en mode simple.",
            ["en"] = "Edge mode only: minimum mask response threshold (after gamma compression) to trigger sharpening. Gamma compression alone is often sufficient — use threshold=0 to disable. Recommended if residual grain persists: 20–40. No effect in simple mode.",
            ["de"] = "Nur Edge-Modus: minimaler Maskenschwellwert (nach Gammakompression) zum Auslösen der Schärfung. Gammakompression allein reicht oft aus — threshold=0 zum Deaktivieren. Empfohlen bei verbleibendem Korn: 20–40. Keine Wirkung im Simple-Modus.",
            ["es"] = "Solo modo edge: umbral mínimo de respuesta de la máscara (después de la compresión gamma) para activar el enfoque. La compresión gamma sola suele ser suficiente — threshold=0 para desactivarla. Recomendado si persiste grano residual: 20–40. Sin efecto en modo simple.",
            ["it"] = "Solo modalità edge: soglia minima di risposta della maschera (dopo compressione gamma) per attivare la nitidezza. La sola compressione gamma è spesso sufficiente — usare threshold=0 per disattivarla. Consigliato se persiste grana residua: 20–40. Nessun effetto in modalità simple." },

        // ── Auto White Balance (awb32) ──
        ["Label_Awb_Strength"] = new() {
            ["fr"] = "Intensité du mélange. 0 = aucune correction, 1 = balance des blancs automatique complète.",
            ["en"] = "Blend strength. 0 = no correction, 1 = full auto white balance.",
            ["de"] = "Mischstärke. 0 = keine Korrektur, 1 = vollständiger automatischer Weißabgleich.",
            ["es"] = "Intensidad de mezcla. 0 = sin corrección, 1 = balance de blancos automático completo.",
            ["it"] = "Intensità della miscela. 0 = nessuna correzione, 1 = bilanciamento del bianco automatico completo." },
        ["Label_Awb_Autogain"] = new() {
            ["fr"] = "Ajuste aussi automatiquement les niveaux de luminosité (autogain).",
            ["en"] = "Also auto-adjust brightness levels (autogain).",
            ["de"] = "Passt außerdem die Helligkeitsstufen automatisch an (Autogain).",
            ["es"] = "Ajusta también automáticamente los niveles de brillo (autogain).",
            ["it"] = "Regola automaticamente anche i livelli di luminosità (autogain)." },

        // ── 8mm Stabilisation (stab8mm1) ──
        ["Label_Stab_MaxH"] = new() {
            ["fr"] = "Amplitude horizontale maximale de stabilisation (% de la largeur). Plus grande valeur = corrige des tremblements plus amples.",
            ["en"] = "Max horizontal stabilization range (% of width). Higher = corrects larger shaking.",
            ["de"] = "Maximaler horizontaler Stabilisierungsbereich (% der Breite). Höher = korrigiert größere Verwacklungen.",
            ["es"] = "Rango máximo de estabilización horizontal (% del ancho). Mayor = corrige sacudidas más grandes.",
            ["it"] = "Ampiezza orizzontale massima di stabilizzazione (% della larghezza). Valore più alto = corregge scosse più ampie." },
        ["Label_Stab_MaxV"] = new() {
            ["fr"] = "Amplitude verticale maximale de stabilisation (% de la hauteur). Plus grande valeur = corrige des tremblements plus amples.",
            ["en"] = "Max vertical stabilization range (% of height). Higher = corrects larger shaking.",
            ["de"] = "Maximaler vertikaler Stabilisierungsbereich (% der Höhe). Höher = korrigiert größere Verwacklungen.",
            ["es"] = "Rango máximo de estabilización vertical (% del alto). Mayor = corrige sacudidas más grandes.",
            ["it"] = "Ampiezza verticale massima di stabilizzazione (% dell'altezza). Valore più alto = corregge scosse più ampie." },
        ["Label_Stab_Error"] = new() {
            ["fr"] = "Seuil d'erreur pour l'estimation de mouvement. Plus bas = correspondance plus stricte, moins de fausses corrections.",
            ["en"] = "Motion estimation error threshold. Lower = stricter matching, less false corrections.",
            ["de"] = "Fehlerschwellwert für die Bewegungsschätzung. Niedriger = striktere Übereinstimmung, weniger Fehlkorrekturen.",
            ["es"] = "Umbral de error para la estimación de movimiento. Menor = coincidencia más estricta, menos correcciones falsas.",
            ["it"] = "Soglia di errore per la stima del movimento. Più basso = corrispondenza più rigorosa, meno correzioni errate." },
        ["Label_Stab_Mirror"] = new() {
            ["fr"] = "Méthode de remplissage des bords. 0 = bordures noires, 1 = bords miroir (masque le recadrage de stabilisation).",
            ["en"] = "Border fill method. 0 = black borders, 1 = mirror edges (hides stabilization crop).",
            ["de"] = "Randfüllmethode. 0 = schwarze Ränder, 1 = Spiegelränder (verbirgt das Stabilisierungs-Crop).",
            ["es"] = "Método de relleno de bordes. 0 = bordes negros, 1 = bordes en espejo (oculta el recorte de estabilización).",
            ["it"] = "Metodo di riempimento dei bordi. 0 = bordi neri, 1 = bordi specchiati (nasconde il ritaglio di stabilizzazione)." },
        ["Label_Stab_Cutoff"] = new() {
            ["fr"] = "Fréquence de coupure passe-haut. Plus bas = supprime la dérive lente, plus haut = seulement les tremblements rapides.",
            ["en"] = "High-pass cutoff frequency. Lower = removes slow drift, higher = only fast jitter.",
            ["de"] = "Hochpass-Grenzfrequenz. Niedriger = entfernt langsame Drift, höher = nur schnelles Zittern.",
            ["es"] = "Frecuencia de corte paso alto. Menor = elimina deriva lenta, mayor = solo vibraciones rápidas.",
            ["it"] = "Frequenza di taglio passa-alto. Più bassa = rimuove le derive lente, più alta = solo le vibrazioni rapide." },
        ["Label_Stab_Damping"] = new() {
            ["fr"] = "Amortissement de la stabilisation. Plus haut = plus doux mais réponse plus lente aux vrais mouvements de caméra.",
            ["en"] = "Damping of stabilization. Higher = smoother but slower response to real camera moves.",
            ["de"] = "Dämpfung der Stabilisierung. Höher = weicher, aber langsamere Reaktion auf echte Kamerabewegungen.",
            ["es"] = "Amortiguación de la estabilización. Mayor = más suave pero respuesta más lenta a movimientos reales de cámara.",
            ["it"] = "Smorzamento della stabilizzazione. Più alto = più fluido ma risposta più lenta ai movimenti reali della camera." },
        ["Label_Stab_EstL"] = new() {
            ["fr"] = "Zone d'estimation : pixels à ignorer depuis la gauche (exclut les perforations de la pellicule).",
            ["en"] = "Estimation zone: pixels to ignore from left (exclude film sprocket holes).",
            ["de"] = "Schätzzone: Pixel von links ignorieren (Filmperforationen ausschließen).",
            ["es"] = "Zona de estimación: píxeles a ignorar desde la izquierda (excluir perforaciones de película).",
            ["it"] = "Zona di stima: pixel da ignorare da sinistra (escludere i fori di trascinamento della pellicola)." },
        ["Label_Stab_EstT"] = new() {
            ["fr"] = "Zone d'estimation : pixels à ignorer depuis le haut.",
            ["en"] = "Estimation zone: pixels to ignore from top.",
            ["de"] = "Schätzzone: Pixel von oben ignorieren.",
            ["es"] = "Zona de estimación: píxeles a ignorar desde arriba.",
            ["it"] = "Zona di stima: pixel da ignorare dall'alto." },
        ["Label_Stab_EstR"] = new() {
            ["fr"] = "Zone d'estimation : pixels à ignorer depuis la droite.",
            ["en"] = "Estimation zone: pixels to ignore from right.",
            ["de"] = "Schätzzone: Pixel von rechts ignorieren.",
            ["es"] = "Zona de estimación: píxeles a ignorar desde la derecha.",
            ["it"] = "Zona di stima: pixel da ignorare da destra." },
        ["Label_Stab_EstB"] = new() {
            ["fr"] = "Zone d'estimation : pixels à ignorer depuis le bas.",
            ["en"] = "Estimation zone: pixels to ignore from bottom.",
            ["de"] = "Schätzzone: Pixel von unten ignorieren.",
            ["es"] = "Zona de estimación: píxeles a ignorar desde abajo.",
            ["it"] = "Zona di stima: pixel da ignorare dal basso." },
        ["Label_Stab_CropL"] = new() {
            ["fr"] = "Recadrage final gauche : supprime les bordures noires après stabilisation.",
            ["en"] = "Final crop left: remove black borders after stabilization.",
            ["de"] = "Endgültiger Beschnitt links: entfernt schwarze Ränder nach der Stabilisierung.",
            ["es"] = "Recorte final izquierdo: elimina bordes negros tras la estabilización.",
            ["it"] = "Ritaglio finale sinistro: rimuove i bordi neri dopo la stabilizzazione." },
        ["Label_Stab_CropT"] = new() {
            ["fr"] = "Recadrage final haut : supprime les bordures noires après stabilisation.",
            ["en"] = "Final crop top: remove black borders after stabilization.",
            ["de"] = "Endgültiger Beschnitt oben: entfernt schwarze Ränder nach der Stabilisierung.",
            ["es"] = "Recorte final superior: elimina bordes negros tras la estabilización.",
            ["it"] = "Ritaglio finale superiore: rimuove i bordi neri dopo la stabilizzazione." },
        ["Label_Stab_CropR"] = new() {
            ["fr"] = "Recadrage final droite : supprime les bordures noires après stabilisation.",
            ["en"] = "Final crop right: remove black borders after stabilization.",
            ["de"] = "Endgültiger Beschnitt rechts: entfernt schwarze Ränder nach der Stabilisierung.",
            ["es"] = "Recorte final derecho: elimina bordes negros tras la estabilización.",
            ["it"] = "Ritaglio finale destro: rimuove i bordi neri dopo la stabilizzazione." },
        ["Label_Stab_CropB"] = new() {
            ["fr"] = "Recadrage final bas : supprime les bordures noires après stabilisation.",
            ["en"] = "Final crop bottom: remove black borders after stabilization.",
            ["de"] = "Endgültiger Beschnitt unten: entfernt schwarze Ränder nach der Stabilisierung.",
            ["es"] = "Recorte final inferior: elimina bordes negros tras la estabilización.",
            ["it"] = "Ritaglio finale inferiore: rimuove i bordi neri dopo la stabilizzazione." },

        // ── White Balance (wb32) ──
        ["Label_Wb_RGain"] = new() {
            ["fr"] = "Gain du canal rouge. >1 réchauffe l'image, <1 réduit la dominante rouge.",
            ["en"] = "Red gain. >1 warms the image, <1 reduces red cast.",
            ["de"] = "Rot-Verstärkung. >1 wärmt das Bild, <1 reduziert den Rotstich.",
            ["es"] = "Ganancia de rojo. >1 calienta la imagen, <1 reduce el tinte rojo.",
            ["it"] = "Guadagno del rosso. >1 riscalda l'immagine, <1 riduce la dominante rossa." },
        ["Label_Wb_RGamma"] = new() {
            ["fr"] = "Gamma rouge. Ajuste les demi-tons rouges sans écrêter les hautes lumières.",
            ["en"] = "Red gamma. Adjusts red midtones without clipping highlights.",
            ["de"] = "Rot-Gamma. Passt die roten Mitteltöne an, ohne Lichter abzuschneiden.",
            ["es"] = "Gamma de rojo. Ajusta los tonos medios rojos sin recortar las luces.",
            ["it"] = "Gamma del rosso. Regola i mezzitoni rossi senza tagliare le alte luci." },
        ["Label_Wb_GGain"] = new() {
            ["fr"] = "Gain du canal vert. Généralement proche de 1.0 (canal de référence).",
            ["en"] = "Green gain. Usually close to 1.0 (reference channel).",
            ["de"] = "Grün-Verstärkung. Normalerweise nahe 1.0 (Referenzkanal).",
            ["es"] = "Ganancia de verde. Normalmente cerca de 1.0 (canal de referencia).",
            ["it"] = "Guadagno del verde. Solitamente vicino a 1.0 (canale di riferimento)." },
        ["Label_Wb_GGamma"] = new() {
            ["fr"] = "Gamma vert. Ajuste les demi-tons verts indépendamment.",
            ["en"] = "Green gamma. Adjusts green midtones independently.",
            ["de"] = "Grün-Gamma. Passt die grünen Mitteltöne unabhängig an.",
            ["es"] = "Gamma de verde. Ajusta los tonos medios verdes de forma independiente.",
            ["it"] = "Gamma del verde. Regola i mezzitoni verdi in modo indipendente." },
        ["Label_Wb_BGain"] = new() {
            ["fr"] = "Gain du canal bleu. >1 refroidit l'image, <1 réduit la dominante bleue.",
            ["en"] = "Blue gain. >1 cools the image, <1 reduces blue cast.",
            ["de"] = "Blau-Verstärkung. >1 kühlt das Bild, <1 reduziert den Blaustich.",
            ["es"] = "Ganancia de azul. >1 enfría la imagen, <1 reduce el tinte azul.",
            ["it"] = "Guadagno del blu. >1 raffredda l'immagine, <1 riduce la dominante blu." },
        ["Label_Wb_BGamma"] = new() {
            ["fr"] = "Gamma bleu. Ajuste les demi-tons bleus indépendamment.",
            ["en"] = "Blue gamma. Adjusts blue midtones independently.",
            ["de"] = "Blau-Gamma. Passt die blauen Mitteltöne unabhängig an.",
            ["es"] = "Gamma de azul. Ajusta los tonos medios azules de forma independiente.",
            ["it"] = "Gamma del blu. Regola i mezzitoni blu in modo indipendente." },

        // ── RGB Levels (rgb_levels32) ──
        ["Label_Rgb_Brightness"] = new() {
            ["fr"] = "Décalage de luminosité global (0 = aucun changement).",
            ["en"] = "Global brightness offset (0 = no change).",
            ["de"] = "Globaler Helligkeitsversatz (0 = keine Änderung).",
            ["es"] = "Desplazamiento de brillo global (0 = sin cambios).",
            ["it"] = "Offset globale di luminosità (0 = nessuna modifica)." },
        ["Label_Rgb_Contrast"] = new() {
            ["fr"] = "Multiplicateur de contraste global (1.0 = neutre).",
            ["en"] = "Global contrast multiplier (1.0 = neutral).",
            ["de"] = "Globaler Kontrastmultiplikator (1.0 = neutral).",
            ["es"] = "Multiplicador de contraste global (1.0 = neutro).",
            ["it"] = "Moltiplicatore di contrasto globale (1,0 = neutro)." },
        ["Label_Rgb_Gamma"] = new() {
            ["fr"] = "Gamma global. <1 = demi-tons plus sombres, >1 = plus clairs.",
            ["en"] = "Global gamma. <1 = darker midtones, >1 = brighter.",
            ["de"] = "Globales Gamma. <1 = dunklere Mitteltöne, >1 = heller.",
            ["es"] = "Gamma global. <1 = tonos medios más oscuros, >1 = más brillantes.",
            ["it"] = "Gamma globale. <1 = mezzitoni più scuri, >1 = più chiari." },
        ["Label_Rgb_RGain"] = new() {
            ["fr"] = "Gain du canal rouge (multiplicateur). Augmenter pour réchauffer, diminuer pour refroidir.",
            ["en"] = "Red channel gain (multiplier). Increase to warm, decrease to cool.",
            ["de"] = "Rot-Kanal-Verstärkung (Multiplikator). Erhöhen zum Erwärmen, verringern zum Kühlen.",
            ["es"] = "Ganancia del canal rojo (multiplicador). Aumentar para calentar, disminuir para enfriar.",
            ["it"] = "Guadagno del canale rosso (moltiplicatore). Aumentare per riscaldare, diminuire per raffreddare." },
        ["Label_Rgb_RGamma"] = new() {
            ["fr"] = "Gamma du canal rouge. Ajuste les demi-tons rouges indépendamment.",
            ["en"] = "Red channel gamma. Adjusts red midtones independently.",
            ["de"] = "Rot-Kanal-Gamma. Passt die roten Mitteltöne unabhängig an.",
            ["es"] = "Gamma del canal rojo. Ajusta los tonos medios rojos de forma independiente.",
            ["it"] = "Gamma del canale rosso. Regola i mezzitoni rossi in modo indipendente." },
        ["Label_Rgb_ROffset"] = new() {
            ["fr"] = "Décalage (lift) du canal rouge. Déplace le point noir rouge.",
            ["en"] = "Red channel offset (lift). Shifts red black point.",
            ["de"] = "Rot-Kanal-Versatz (Lift). Verschiebt den roten Schwarzpunkt.",
            ["es"] = "Desplazamiento del canal rojo (lift). Cambia el punto negro del rojo.",
            ["it"] = "Offset (lift) del canale rosso. Sposta il punto nero del rosso." },
        ["Label_Rgb_GGain"] = new() {
            ["fr"] = "Gain du canal vert (multiplicateur).",
            ["en"] = "Green channel gain (multiplier).",
            ["de"] = "Grün-Kanal-Verstärkung (Multiplikator).",
            ["es"] = "Ganancia del canal verde (multiplicador).",
            ["it"] = "Guadagno del canale verde (moltiplicatore)." },
        ["Label_Rgb_GGamma"] = new() {
            ["fr"] = "Gamma du canal vert. Ajuste les demi-tons verts indépendamment.",
            ["en"] = "Green channel gamma. Adjusts green midtones independently.",
            ["de"] = "Grün-Kanal-Gamma. Passt die grünen Mitteltöne unabhängig an.",
            ["es"] = "Gamma del canal verde. Ajusta los tonos medios verdes de forma independiente.",
            ["it"] = "Gamma del canale verde. Regola i mezzitoni verdi in modo indipendente." },
        ["Label_Rgb_GOffset"] = new() {
            ["fr"] = "Décalage (lift) du canal vert. Déplace le point noir vert.",
            ["en"] = "Green channel offset (lift). Shifts green black point.",
            ["de"] = "Grün-Kanal-Versatz (Lift). Verschiebt den grünen Schwarzpunkt.",
            ["es"] = "Desplazamiento del canal verde (lift). Cambia el punto negro del verde.",
            ["it"] = "Offset (lift) del canale verde. Sposta il punto nero del verde." },
        ["Label_Rgb_BGain"] = new() {
            ["fr"] = "Gain du canal bleu (multiplicateur). Augmenter pour des tons plus froids.",
            ["en"] = "Blue channel gain (multiplier). Increase for cooler tones.",
            ["de"] = "Blau-Kanal-Verstärkung (Multiplikator). Erhöhen für kühlere Töne.",
            ["es"] = "Ganancia del canal azul (multiplicador). Aumentar para tonos más fríos.",
            ["it"] = "Guadagno del canale blu (moltiplicatore). Aumentare per toni più freddi." },
        ["Label_Rgb_BGamma"] = new() {
            ["fr"] = "Gamma du canal bleu. Ajuste les demi-tons bleus indépendamment.",
            ["en"] = "Blue channel gamma. Adjusts blue midtones independently.",
            ["de"] = "Blau-Kanal-Gamma. Passt die blauen Mitteltöne unabhängig an.",
            ["es"] = "Gamma del canal azul. Ajusta los tonos medios azules de forma independiente.",
            ["it"] = "Gamma del canale blu. Regola i mezzitoni blu in modo indipendente." },
        ["Label_Rgb_BOffset"] = new() {
            ["fr"] = "Décalage (lift) du canal bleu. Déplace le point noir bleu.",
            ["en"] = "Blue channel offset (lift). Shifts blue black point.",
            ["de"] = "Blau-Kanal-Versatz (Lift). Verschiebt den blauen Schwarzpunkt.",
            ["es"] = "Desplazamiento del canal azul (lift). Cambia el punto negro del azul.",
            ["it"] = "Offset (lift) del canale blu. Sposta il punto nero del blu." },
    };

    /// <summary>Maps (filterId.placeholder) to the ParamTooltipTexts key for localized tooltips.</summary>
    public static readonly Dictionary<string, string> ParamTooltipKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Crop
        ["crop.left"]   = "Label_Crop_L",
        ["crop.top"]    = "Label_Crop_T",
        ["crop.right"]  = "Label_Crop_R",
        ["crop.bottom"] = "Label_Crop_B",
        // Degrain
        ["degrain.mode"]      = "Label_degrain_mode",
        ["degrain.thSAD"]     = "Label_degrain_thSAD",
        ["degrain.thSADC"]    = "Label_degrain_thSADC",
        ["degrain.blksize"]   = "Label_degrain_blksize",
        ["degrain.overlap"]   = "Label_degrain_overlap",
        ["degrain.pel"]       = "Label_degrain_pel",
        ["degrain.search"]    = "Label_degrain_search",
        ["degrain.prefilter"] = "Label_degrain_prefilter",
        ["degrain.preset"]    = "Label_degrain_preset",
        // Denoise
        ["denoise.mode"]     = "Label_denoise_mode",
        ["denoise.strength"] = "Label_denoise_strength",
        ["denoise.dist"]     = "Label_denoise_dist",
        ["denoise.grey"]     = "Label_denoise_grey",
        ["denoise.preset"]   = "Label_denoise_preset",
        // Luma
        ["luma.bright"]   = "Label_Lum_Bright",
        ["luma.contrast"] = "Label_Lum_Contrast",
        ["luma.sat"]      = "Label_Lum_Sat",
        ["luma.hue"]      = "Label_Lum_Hue",
        ["luma.gamma"]    = "Label_Lum_GammaY",
        // GamMac
        ["gammac.LockChan"]  = "Label_LockChan",
        ["gammac.LockVal"]   = "Label_LockVal",
        ["gammac.Scale"]     = "Label_Scale",
        ["gammac.Th"]        = "Label_Th",
        ["gammac.HiTh"]      = "Label_HiTh",
        ["gammac.X"]         = "Label_X",
        ["gammac.Y"]         = "Label_Y",
        ["gammac.W"]         = "Label_W",
        ["gammac.H"]         = "Label_H",
        ["gammac.Omin"]      = "Label_Omin",
        ["gammac.Omax"]      = "Label_Omax",
        ["gammac.Show"]      = "Label_ShowPreview",
        ["gammac.Verbosity"] = "Label_Verbosity",
        // Sharpen
        ["sharpen.mode"]      = "Label_Sharp_Mode",
        ["sharpen.strength"]  = "Label_Sharp_Strength",
        ["sharpen.radius"]    = "Label_Sharp_Radius",
        ["sharpen.threshold"] = "Label_Sharp_Threshold",
        // Auto White Balance
        ["awb32.strength"] = "Label_Awb_Strength",
        ["awb32.autogain"] = "Label_Awb_Autogain",
        // 8mm Stabilisation
        ["stab8mm1.maxstabH"]    = "Label_Stab_MaxH",
        ["stab8mm1.maxstabV"]    = "Label_Stab_MaxV",
        ["stab8mm1.error"]       = "Label_Stab_Error",
        ["stab8mm1.mirror"]      = "Label_Stab_Mirror",
        ["stab8mm1.cutoff"]      = "Label_Stab_Cutoff",
        ["stab8mm1.damping"]     = "Label_Stab_Damping",
        ["stab8mm1.est_left"]    = "Label_Stab_EstL",
        ["stab8mm1.est_top"]     = "Label_Stab_EstT",
        ["stab8mm1.est_right"]   = "Label_Stab_EstR",
        ["stab8mm1.est_bottom"]  = "Label_Stab_EstB",
        ["stab8mm1.crop_left"]   = "Label_Stab_CropL",
        ["stab8mm1.crop_top"]    = "Label_Stab_CropT",
        ["stab8mm1.crop_right"]  = "Label_Stab_CropR",
        ["stab8mm1.crop_bottom"] = "Label_Stab_CropB",
        // White Balance (32-bit)
        ["wb32.r_gain"]  = "Label_Wb_RGain",
        ["wb32.r_gamma"] = "Label_Wb_RGamma",
        ["wb32.g_gain"]  = "Label_Wb_GGain",
        ["wb32.g_gamma"] = "Label_Wb_GGamma",
        ["wb32.b_gain"]  = "Label_Wb_BGain",
        ["wb32.b_gamma"] = "Label_Wb_BGamma",
        // RGB Levels (32-bit)
        ["rgb_levels32.brightness"] = "Label_Rgb_Brightness",
        ["rgb_levels32.contrast"]   = "Label_Rgb_Contrast",
        ["rgb_levels32.gamma"]      = "Label_Rgb_Gamma",
        ["rgb_levels32.r_gain"]     = "Label_Rgb_RGain",
        ["rgb_levels32.r_gamma"]    = "Label_Rgb_RGamma",
        ["rgb_levels32.r_offset"]   = "Label_Rgb_ROffset",
        ["rgb_levels32.g_gain"]     = "Label_Rgb_GGain",
        ["rgb_levels32.g_gamma"]    = "Label_Rgb_GGamma",
        ["rgb_levels32.g_offset"]   = "Label_Rgb_GOffset",
        ["rgb_levels32.b_gain"]     = "Label_Rgb_BGain",
        ["rgb_levels32.b_gamma"]    = "Label_Rgb_BGamma",
        ["rgb_levels32.b_offset"]   = "Label_Rgb_BOffset",
    };
}
