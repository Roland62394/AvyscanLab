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
            ["fr"] =
                "===========================================\n" +
                "       CLEANSCAN  —  GUIDE UTILISATEUR\n" +
                "       Version d'essai — enregistrement\n" +
                "            limité à 60 s. par clip\n" +
                "===========================================\n\n" +

                "CleanScan est un outil de restauration de films argentiques\n" +
                "numérisés. Il génère un script AviSynth optimisé et offre une prévisualisation\n" +
                "en temps réel via un lecteur vidéo intégré (libmpv).\n\n" +

                "-------------------------------------------\n" +
                "  1. PRÉREQUIS\n" +
                "-------------------------------------------\n\n" +
                "  - AviSynth+ doit être installé sur le système (64 bits).\n" +
                "    Si absent, CleanScan le détecte au démarrage et propose\n" +
                "    un lien de téléchargement direct.\n" +
                "  - FFmpeg est fourni avec l'application (Plugins/ffmpeg/)\n" +
                "    pour l'encodage des fichiers de sortie.\n" +
                "  - Le lecteur mpv est fourni avec l'application (mpv/).\n\n" +

                "-------------------------------------------\n" +
                "  2. SÉLECTION DE LA SOURCE\n" +
                "-------------------------------------------\n\n" +
                "La barre supérieure contient les champs de source :\n\n" +
                "  - Fichier vidéo : glissez-déposez un fichier vidéo\n" +
                "    (.avi, .mp4, .mov, .mkv, .wmv, .m4v, .mpeg, .mpg, .webm)\n" +
                "    sur la zone de lecture, ou cliquez sur le champ \"source\".\n\n" +
                "  - Séquence d'images : activez le mode image (bouton bascule),\n" +
                "    puis renseignez le chemin du dossier dans le champ \"img\".\n" +
                "    Formats supportés : .tif, .tiff, .jpg, .jpeg, .png, .bmp.\n" +
                "    Spécifiez la première et la dernière image avec\n" +
                "    les champs \"start\" et \"end\".\n\n" +
                "  - FPS : le champ \"fps\" définit la cadence de lecture\n" +
                "    pour la prévisualisation.\n\n" +

                "-------------------------------------------\n" +
                "  3. LECTEUR VIDÉO ET NAVIGATION\n" +
                "-------------------------------------------\n\n" +
                "Le lecteur intégré affiche le résultat du script AviSynth\n" +
                "en temps réel. Toute modification de paramètre régénère\n" +
                "automatiquement le script et rafraîchit l'aperçu.\n\n" +
                "  Raccourcis clavier :\n" +
                "    Espace         Lecture / Pause\n" +
                "    Flèche gauche  Image précédente\n" +
                "    Flèche droite  Image suivante\n" +
                "    Ctrl+Gauche    Aller au début\n" +
                "    Ctrl+Droite    Aller à la fin\n\n" +
                "  Barre de transport :\n" +
                "    |<   Début\n" +
                "    <<   Image précédente\n" +
                "    >    Lecture / Pause\n" +
                "    >>   Image suivante\n" +
                "    >|   Fin\n" +
                "    1x   Vitesse de lecture (clic pour alterner)\n" +
                "    1/2  Prévisualisation demi-résolution (plus rapide)\n\n" +
                "  La barre de progression (seek) affiche la position\n" +
                "  courante et la durée totale. Cliquez ou glissez\n" +
                "  pour naviguer dans le film.\n\n" +

                "-------------------------------------------\n" +
                "  4. PANNEAU DE FILTRES\n" +
                "-------------------------------------------\n\n" +
                "Le panneau inférieur est divisé en trois colonnes :\n" +
                "  - Colonne gauche : boutons d'activation des filtres\n" +
                "  - Séparateur redimensionnable\n" +
                "  - Colonne droite : paramètres détaillés du filtre sélectionné\n\n" +
                "Chaque filtre possède un bouton ON/OFF et un bouton\n" +
                "d'expansion (>) qui affiche son panneau de paramètres.\n" +
                "Les boutons actifs apparaissent en vert.\n\n" +

                "OPTIONS GÉNÉRALES :\n" +
                "  - preview      : active la prévisualisation AviSynth\n" +
                "  - enable_flip_h : retournement horizontal\n" +
                "  - enable_flip_v : retournement vertical\n\n" +

                "--- CROP (Recadrage) ---\n\n" +
                "Supprime des pixels sur les bords de l'image.\n" +
                "  - Crop_L / Crop_T / Crop_R / Crop_B : pixels à retirer\n" +
                "    sur chaque bord (gauche, haut, droite, bas).\n" +
                "  - Plage : 0 à 500 pixels.\n\n" +

                "--- DEGRAIN (Réduction du grain) ---\n\n" +
                "Réduction temporelle du grain de pellicule via MVTools2.\n\n" +
                "  Presets rapides : faible, standard, moyen, fort, très fort.\n" +
                "  Sélectionnez un preset pour remplir automatiquement\n" +
                "  tous les paramètres, puis affinez manuellement.\n\n" +
                "  Paramètres :\n" +
                "  - degrain_mode : algorithme (mdegrain1/2/3 ou temporal)\n" +
                "      mdegrain2 = 2 réf. temporelles (recommandé)\n" +
                "      mdegrain3 = 3 réf. (meilleur, plus lent)\n" +
                "      mdegrain1 = 1 réf. (rapide)\n" +
                "      temporal  = TemporalSoften (sans MVTools2)\n" +
                "  - degrain_thSAD  : seuil luma (0-1000, défaut 350)\n" +
                "  - degrain_thSADC : seuil chroma (0-1000, défaut 250)\n" +
                "  - degrain_blksize : taille de bloc (8/16/32, défaut 16)\n" +
                "  - degrain_overlap : chevauchement (= blksize/2)\n" +
                "  - degrain_pel : précision sub-pixel (1 ou 2)\n" +
                "  - degrain_search : algo de recherche (0=rapide, 2=hexagone, 3=exhaustif)\n" +
                "  - degrain_prefilter : pré-lissage (remgrain, blur, none)\n\n" +

                "--- DENOISE (Suppression des poussières) ---\n\n" +
                "Détection et suppression temporelle des poussières et rayures.\n\n" +
                "  Presets rapides : faible, standard, moyen, fort, très fort.\n\n" +
                "  Paramètres :\n" +
                "  - denoise_mode : removedirtmc (avec compensation\n" +
                "    de mouvement, recommandé) ou removedirt (plus rapide)\n" +
                "  - denoise_strength : seuil de détection (1-24, défaut 10)\n" +
                "  - denoise_dist : rayon spatial de réparation (1-20, défaut 3)\n" +
                "  - denoise_grey : luma seul (plus rapide) ou luma+chroma\n\n" +

                "--- LUMA LEVELS (Niveaux de luminosité) ---\n\n" +
                "Ajustement de la luminosité, du contraste et des couleurs.\n\n" +
                "  - Lum_Bright   : luminosité (-255 à 255, défaut 0)\n" +
                "  - Lum_Contrast : contraste (0.1 à 3.0, défaut 1.05)\n" +
                "  - Lum_Sat      : saturation (0.1 à 3.0, défaut 1.10)\n" +
                "  - Lum_Hue      : décalage teinte (-180 à 180, défaut 0)\n" +
                "  - Lum_GammaY   : gamma luminance (0.1 à 3.0, défaut 1.30)\n" +
                "                   >1 éclaircit, <1 assombrit\n\n" +

                "--- GAMMAC (Correction colorimétrique avancée) ---\n\n" +
                "Correction automatique des dérives de couleur entre canaux RGB.\n\n" +
                "  - LockChan : canal de référence (défaut 1 = Vert)\n" +
                "      0=Rouge, 1=Vert, 2=Bleu\n" +
                "      -1=valeur cible (LockVal), -2=moyenne, -3=médian\n" +
                "  - LockVal  : valeur cible (1-255, défaut 250)\n" +
                "  - Scale    : amplitude de correction (0-2, défaut 2)\n" +
                "  - Th / HiTh : seuils de détection (0-1)\n" +
                "  - X, Y, W, H : région d'analyse (0=image entière)\n" +
                "  - Omin / Omax : écrêtage de sortie (0-255)\n" +
                "  - Show : affiche la région d'analyse en surimpression\n" +
                "  - Verbosity : niveau de log (0-6)\n\n" +

                "--- SHARPEN (Netteté) ---\n\n" +
                "Renforcement de la netteté de l'image.\n\n" +
                "  Presets rapides : léger, standard, moyen, fort, très fort.\n\n" +
                "  - Sharp_Mode : méthode de renforcement\n" +
                "      simple = Sharpen() global (efficace, peut amplifier le grain)\n" +
                "      edge = contours uniquement via masque Sobel + gamma\n" +
                "             (technique LimitedSharpenFaster/Didée)\n" +
                "  - Sharp_Strength : intensité (1-20, défaut 8)\n" +
                "  - Sharp_Radius   : rayon unsharp mask (0.5-5.0, défaut 1.5)\n" +
                "  - Sharp_Threshold : seuil en mode edge (0-100, défaut 0)\n\n" +

                "-------------------------------------------\n" +
                "  5. PRÉRÉGLAGES (PRESETS)\n" +
                "-------------------------------------------\n\n" +
                "Le menu Preset permet de sauvegarder et restaurer\n" +
                "vos configurations de filtres.\n\n" +
                "  - Sauvegarder : entrez un nom et cliquez Sauvegarder.\n" +
                "  - Mettre à jour : sélectionnez un preset et cliquez\n" +
                "    Mettre à jour pour écraser ses valeurs.\n" +
                "  - Charger : sélectionnez un preset et cliquez Charger.\n" +
                "  - Supprimer : sélectionnez un preset et cliquez Supprimer.\n\n" +
                "  Note : les champs source, film, img, img_start, img_end\n" +
                "  et crop ne sont pas inclus dans les presets.\n" +
                "  Les presets sont stockés dans :\n" +
                "  %AppData%\\CleanScan\\presets.json\n\n" +

                "-------------------------------------------\n" +
                "  6. ENCODAGE (ENREGISTREMENT)\n" +
                "-------------------------------------------\n\n" +
                "Cliquez sur le bouton REC dans la barre de transport\n" +
                "pour ouvrir le panneau d'encodage.\n\n" +
                "  1. Choisissez un dossier de destination.\n" +
                "  2. Entrez un nom de fichier de sortie.\n" +
                "  3. Sélectionnez un encodeur :\n" +
                "       x264         — H.264 lossy (CRF 18, medium)\n" +
                "       x265         — H.265/HEVC lossy (CRF 20, medium)\n" +
                "       FFV1         — lossless (archivage professionnel)\n" +
                "       UT Video     — lossless (rapide, édition)\n" +
                "       ProRes       — ProRes 422 HQ (post-production)\n" +
                "       TIFF seq.    — séquence TIFF (images individuelles)\n" +
                "       PNG seq.     — séquence PNG (images individuelles)\n" +
                "  4. Sélectionnez un conteneur (MKV, MP4, AVI, MOV)\n" +
                "     pour les codecs vidéo (ignoré pour les séquences).\n" +
                "  5. Cliquez sur \"Lancer l'encodage\".\n\n" +
                "  Une barre de progression affiche le pourcentage\n" +
                "  et le temps écoulé. Cliquez sur \"Arrêter l'encodage\"\n" +
                "  pour annuler à tout moment.\n\n" +
                "  Note : l'encodage utilise un script de rendu dédié\n" +
                "  (ScriptRender.avs) où preview est désactivé,\n" +
                "  garantissant la qualité finale maximale.\n\n" +

                "-------------------------------------------\n" +
                "  7. RÉGLAGES\n" +
                "-------------------------------------------\n\n" +
                "Menu Réglages :\n\n" +
                "  - Threads : nombre de threads AviSynth.\n" +
                "    Augmentez pour exploiter tous les coeurs CPU.\n\n" +
                "  - Chargeur source : sélection du plugin de décodage.\n" +
                "      Auto     — détection automatique (défaut)\n" +
                "      FFMS2    — FFmpegSource2 (bon pour MP4/MKV)\n" +
                "      L-SMASH  — LSmashSource (précis pour MP4/MOV)\n" +
                "      LWLibav  — LWLibavVideoSource (polyvalent)\n\n" +

                "Menu Langue :\n" +
                "  Français, English, Deutsch, Español.\n" +
                "  Le script AviSynth est régénéré dans la langue choisie.\n\n" +

                "-------------------------------------------\n" +
                "  8. APERÇU DU SCRIPT\n" +
                "-------------------------------------------\n\n" +
                "Menu Infos > Aperçu du script ouvre une fenêtre\n" +
                "affichant le contenu complet de ScriptUser.avs.\n" +
                "Le bouton Recharger relit le fichier depuis le disque\n" +
                "et rafraîchit la prévisualisation.\n\n" +

                "-------------------------------------------\n" +
                "  9. WORKFLOW RECOMMANDÉ\n" +
                "-------------------------------------------\n\n" +
                "  1. Ouvrez votre source (glisser-déposer ou champ source).\n" +
                "  2. Naviguez jusqu'à une image représentative.\n" +
                "  3. Activez les filtres un par un, en commençant par :\n" +
                "       a) Crop (si bordures noires ou cadre à ajuster)\n" +
                "       b) Degrain (réduction du grain de pellicule)\n" +
                "       c) Denoise (suppression des poussières)\n" +
                "       d) Luma Levels (luminosité / contraste)\n" +
                "       e) GamMac (correction colorimétrique)\n" +
                "       f) Sharpen (netteté finale)\n" +
                "  4. Utilisez les presets de chaque filtre pour un\n" +
                "     réglage rapide, puis affinez manuellement.\n" +
                "  5. Vérifiez le résultat en parcourant plusieurs\n" +
                "     passages du film.\n" +
                "  6. Sauvegardez votre configuration en preset.\n" +
                "  7. Lancez l'encodage final.\n\n" +

                "-------------------------------------------\n" +
                "  10. ASTUCES\n" +
                "-------------------------------------------\n\n" +
                "  - La molette de souris sur les champs numériques\n" +
                "    incrémente/décrémente la valeur par pas.\n" +
                "  - Le mode demi-résolution (bouton 1/2) accélère\n" +
                "    considérablement la prévisualisation.\n" +
                "  - Tous les paramètres sont sauvegardés automatiquement\n" +
                "    dans ScriptUser.avs à chaque modification.\n" +
                "  - La position et la taille de la fenêtre sont\n" +
                "    mémorisées entre les sessions.\n" +
                "  - Les infobulles sur chaque paramètre (survol)\n" +
                "    affichent les plages recommandées et des conseils.\n\n" +

                "===========================================\n" +
                "           www.scanfilm.ch\n" +
                "===========================================",

            ["en"] =
                "===========================================\n" +
                "        CLEANSCAN  —  USER GUIDE\n" +
                "       Trial version — recording\n" +
                "          limited to 60 s. per clip\n" +
                "===========================================\n\n" +

                "CleanScan is a restoration tool for digitized\n" +
                "analog film. It generates an optimized AviSynth script and\n" +
                "provides real-time preview via an embedded video player (libmpv).\n\n" +

                "-------------------------------------------\n" +
                "  1. PREREQUISITES\n" +
                "-------------------------------------------\n\n" +
                "  - AviSynth+ must be installed on the system (64-bit).\n" +
                "    If missing, CleanScan detects it at startup and\n" +
                "    offers a direct download link.\n" +
                "  - FFmpeg is bundled with the application (Plugins/ffmpeg/)\n" +
                "    for output file encoding.\n" +
                "  - The mpv player is bundled with the application (mpv/).\n\n" +

                "-------------------------------------------\n" +
                "  2. SOURCE SELECTION\n" +
                "-------------------------------------------\n\n" +
                "The top bar contains the source fields:\n\n" +
                "  - Video file: drag and drop a video file\n" +
                "    (.avi, .mp4, .mov, .mkv, .wmv, .m4v, .mpeg, .mpg, .webm)\n" +
                "    onto the player area, or click the \"source\" field.\n\n" +
                "  - Image sequence: enable image mode (toggle button),\n" +
                "    then enter the folder path in the \"img\" field.\n" +
                "    Supported formats: .tif, .tiff, .jpg, .jpeg, .png, .bmp.\n" +
                "    Specify first and last image with \"start\" and \"end\" fields.\n\n" +
                "  - FPS: the \"fps\" field sets the playback frame rate\n" +
                "    for preview.\n\n" +

                "-------------------------------------------\n" +
                "  3. VIDEO PLAYER & NAVIGATION\n" +
                "-------------------------------------------\n\n" +
                "The embedded player displays the AviSynth script result\n" +
                "in real time. Any parameter change automatically regenerates\n" +
                "the script and refreshes the preview.\n\n" +
                "  Keyboard shortcuts:\n" +
                "    Space          Play / Pause\n" +
                "    Left arrow     Previous frame\n" +
                "    Right arrow    Next frame\n" +
                "    Ctrl+Left      Go to beginning\n" +
                "    Ctrl+Right     Go to end\n\n" +
                "  Transport bar:\n" +
                "    |<   Beginning\n" +
                "    <<   Previous frame\n" +
                "    >    Play / Pause\n" +
                "    >>   Next frame\n" +
                "    >|   End\n" +
                "    1x   Playback speed (click to cycle)\n" +
                "    1/2  Half-resolution preview (faster)\n\n" +
                "  The seek bar shows current position and total duration.\n" +
                "  Click or drag to navigate through the film.\n\n" +

                "-------------------------------------------\n" +
                "  4. FILTER PANEL\n" +
                "-------------------------------------------\n\n" +
                "The bottom panel is divided into three columns:\n" +
                "  - Left column: filter activation buttons\n" +
                "  - Resizable splitter\n" +
                "  - Right column: detailed parameters for selected filter\n\n" +
                "Each filter has an ON/OFF button and an expand button (>)\n" +
                "that shows its parameter panel. Active buttons appear green.\n\n" +

                "GENERAL OPTIONS:\n" +
                "  - preview       : enable AviSynth preview\n" +
                "  - enable_flip_h : horizontal flip\n" +
                "  - enable_flip_v : vertical flip\n\n" +

                "--- CROP ---\n\n" +
                "Remove pixels from image edges.\n" +
                "  - Crop_L / Crop_T / Crop_R / Crop_B: pixels to remove\n" +
                "    on each side (left, top, right, bottom).\n" +
                "  - Range: 0 to 500 pixels.\n\n" +

                "--- DEGRAIN (Grain reduction) ---\n\n" +
                "Temporal film grain reduction via MVTools2.\n\n" +
                "  Quick presets: faible, standard, moyen, fort, tres fort.\n" +
                "  Select a preset to auto-fill all parameters,\n" +
                "  then fine-tune manually.\n\n" +
                "  Parameters:\n" +
                "  - degrain_mode: algorithm (mdegrain1/2/3 or temporal)\n" +
                "  - degrain_thSAD: luma threshold (0-1000, default 350)\n" +
                "  - degrain_thSADC: chroma threshold (0-1000, default 250)\n" +
                "  - degrain_blksize: block size (8/16/32, default 16)\n" +
                "  - degrain_overlap: overlap (= blksize/2)\n" +
                "  - degrain_pel: sub-pixel precision (1 or 2)\n" +
                "  - degrain_search: search algorithm (0=fast, 2=hex, 3=exhaustive)\n" +
                "  - degrain_prefilter: pre-smoothing (remgrain, blur, none)\n\n" +

                "--- DENOISE (Dust removal) ---\n\n" +
                "Temporal dust and scratch detection and removal.\n\n" +
                "  Quick presets: faible, standard, moyen, fort, tres fort.\n\n" +
                "  Parameters:\n" +
                "  - denoise_mode: removedirtmc (motion-compensated,\n" +
                "    recommended) or removedirt (faster)\n" +
                "  - denoise_strength: detection threshold (1-24, default 10)\n" +
                "  - denoise_dist: spatial repair radius (1-20, default 3)\n" +
                "  - denoise_grey: luma only (faster) or luma+chroma\n\n" +

                "--- LUMA LEVELS ---\n\n" +
                "Brightness, contrast and color adjustment.\n\n" +
                "  - Lum_Bright   : brightness (-255 to 255, default 0)\n" +
                "  - Lum_Contrast : contrast (0.1 to 3.0, default 1.05)\n" +
                "  - Lum_Sat      : saturation (0.1 to 3.0, default 1.10)\n" +
                "  - Lum_Hue      : hue shift (-180 to 180, default 0)\n" +
                "  - Lum_GammaY   : luminance gamma (0.1 to 3.0, default 1.30)\n" +
                "                   >1 brightens, <1 darkens\n\n" +

                "--- GAMMAC (Advanced color correction) ---\n\n" +
                "Automatic correction of color channel drift in RGB.\n\n" +
                "  - LockChan: reference channel (default 1 = Green)\n" +
                "      0=Red, 1=Green, 2=Blue\n" +
                "      -1=target value (LockVal), -2=average, -3=median\n" +
                "  - LockVal: target value (1-255, default 250)\n" +
                "  - Scale: correction amplitude (0-2, default 2)\n" +
                "  - Th / HiTh: detection thresholds (0-1)\n" +
                "  - X, Y, W, H: analysis region (0=full image)\n" +
                "  - Omin / Omax: output clipping (0-255)\n" +
                "  - Show: overlay analysis region on preview\n" +
                "  - Verbosity: log level (0-6)\n\n" +

                "--- SHARPEN ---\n\n" +
                "Image sharpening enhancement.\n\n" +
                "  Quick presets: leger, standard, moyen, fort, tres fort.\n\n" +
                "  - Sharp_Mode: sharpening method\n" +
                "      simple = global Sharpen() (effective, may amplify grain)\n" +
                "      edge = contours only via Sobel mask + gamma\n" +
                "             (LimitedSharpenFaster/Didee technique)\n" +
                "  - Sharp_Strength: intensity (1-20, default 8)\n" +
                "  - Sharp_Radius: unsharp mask radius (0.5-5.0, default 1.5)\n" +
                "  - Sharp_Threshold: edge mode threshold (0-100, default 0)\n\n" +

                "-------------------------------------------\n" +
                "  5. PRESETS\n" +
                "-------------------------------------------\n\n" +
                "The Preset menu lets you save and restore\n" +
                "your filter configurations.\n\n" +
                "  - Save: enter a name and click Save.\n" +
                "  - Update: select a preset and click Update\n" +
                "    to overwrite its values.\n" +
                "  - Load: select a preset and click Load.\n" +
                "  - Delete: select a preset and click Delete.\n\n" +
                "  Note: source, film, img, img_start, img_end\n" +
                "  and crop fields are not included in presets.\n" +
                "  Presets are stored in:\n" +
                "  %AppData%\\CleanScan\\presets.json\n\n" +

                "-------------------------------------------\n" +
                "  6. ENCODING\n" +
                "-------------------------------------------\n\n" +
                "Click the REC button in the transport bar\n" +
                "to open the encoding panel.\n\n" +
                "  1. Choose a destination folder.\n" +
                "  2. Enter an output file name.\n" +
                "  3. Select an encoder:\n" +
                "       x264         — H.264 lossy (CRF 18, medium)\n" +
                "       x265         — H.265/HEVC lossy (CRF 20, medium)\n" +
                "       FFV1         — lossless (professional archiving)\n" +
                "       UT Video     — lossless (fast, editing)\n" +
                "       ProRes       — ProRes 422 HQ (post-production)\n" +
                "       TIFF seq.    — TIFF sequence (individual frames)\n" +
                "       PNG seq.     — PNG sequence (individual frames)\n" +
                "  4. Select a container (MKV, MP4, AVI, MOV)\n" +
                "     for video codecs (ignored for sequences).\n" +
                "  5. Click \"Start encoding\".\n\n" +
                "  A progress bar shows percentage and elapsed time.\n" +
                "  Click \"Stop encoding\" to cancel at any time.\n\n" +
                "  Note: encoding uses a dedicated render script\n" +
                "  (ScriptRender.avs) where preview is disabled,\n" +
                "  ensuring maximum final quality.\n\n" +

                "-------------------------------------------\n" +
                "  7. SETTINGS\n" +
                "-------------------------------------------\n\n" +
                "Settings menu:\n\n" +
                "  - Threads: number of AviSynth threads.\n" +
                "    Increase to use all CPU cores.\n\n" +
                "  - Source loader: decoding plugin selection.\n" +
                "      Auto     — automatic detection (default)\n" +
                "      FFMS2    — FFmpegSource2 (good for MP4/MKV)\n" +
                "      L-SMASH  — LSmashSource (precise for MP4/MOV)\n" +
                "      LWLibav  — LWLibavVideoSource (versatile)\n\n" +

                "Language menu:\n" +
                "  English, Francais, Deutsch, Espanol.\n" +
                "  The AviSynth script is regenerated in the chosen language.\n\n" +

                "-------------------------------------------\n" +
                "  8. SCRIPT PREVIEW\n" +
                "-------------------------------------------\n\n" +
                "Infos menu > Script preview opens a window\n" +
                "showing the full contents of ScriptUser.avs.\n" +
                "The Reload button re-reads the file from disk\n" +
                "and refreshes the preview.\n\n" +

                "-------------------------------------------\n" +
                "  9. RECOMMENDED WORKFLOW\n" +
                "-------------------------------------------\n\n" +
                "  1. Open your source (drag-and-drop or source field).\n" +
                "  2. Navigate to a representative frame.\n" +
                "  3. Enable filters one by one, starting with:\n" +
                "       a) Crop (if black borders or framing adjustment needed)\n" +
                "       b) Degrain (film grain reduction)\n" +
                "       c) Denoise (dust removal)\n" +
                "       d) Luma Levels (brightness / contrast)\n" +
                "       e) GamMac (color correction)\n" +
                "       f) Sharpen (final sharpening)\n" +
                "  4. Use each filter's presets for quick setup,\n" +
                "     then fine-tune manually.\n" +
                "  5. Check the result across several film passages.\n" +
                "  6. Save your configuration as a preset.\n" +
                "  7. Launch the final encoding.\n\n" +

                "-------------------------------------------\n" +
                "  10. TIPS\n" +
                "-------------------------------------------\n\n" +
                "  - Mouse wheel on numeric fields increments/decrements\n" +
                "    the value by step.\n" +
                "  - Half-resolution mode (1/2 button) significantly\n" +
                "    speeds up preview.\n" +
                "  - All parameters are automatically saved to\n" +
                "    ScriptUser.avs on each change.\n" +
                "  - Window position and size are remembered\n" +
                "    between sessions.\n" +
                "  - Tooltips on each parameter (hover) show\n" +
                "    recommended ranges and tips.\n\n" +

                "===========================================\n" +
                "           www.scanfilm.ch\n" +
                "===========================================",

            ["de"] =
                "===========================================\n" +
                "     CLEANSCAN  —  BENUTZERHANDBUCH\n" +
                "      Testversion — Aufnahme auf\n" +
                "        60 Sek. pro Clip begrenzt\n" +
                "===========================================\n\n" +

                "CleanScan ist ein Restaurierungstool fuer\n" +
                "digitalisierte analoge Filme. Es erzeugt ein optimiertes\n" +
                "AviSynth-Skript und bietet Echtzeitvorschau ueber einen\n" +
                "integrierten Videoplayer (libmpv).\n\n" +

                "-------------------------------------------\n" +
                "  1. VORAUSSETZUNGEN\n" +
                "-------------------------------------------\n\n" +
                "  - AviSynth+ muss auf dem System installiert sein (64-Bit).\n" +
                "    Falls fehlend, erkennt CleanScan dies beim Start\n" +
                "    und bietet einen direkten Download-Link.\n" +
                "  - FFmpeg ist in der Anwendung enthalten (Plugins/ffmpeg/)\n" +
                "    fuer die Ausgabekodierung.\n" +
                "  - Der mpv-Player ist in der Anwendung enthalten (mpv/).\n\n" +

                "-------------------------------------------\n" +
                "  2. QUELLENAUSWAHL\n" +
                "-------------------------------------------\n\n" +
                "Die obere Leiste enthaelt die Quellenfelder:\n\n" +
                "  - Videodatei: Ziehen Sie eine Videodatei\n" +
                "    (.avi, .mp4, .mov, .mkv, .wmv, .m4v, .mpeg, .mpg, .webm)\n" +
                "    auf den Playerbereich oder klicken Sie auf \"source\".\n\n" +
                "  - Bildsequenz: Aktivieren Sie den Bildmodus (Umschalter),\n" +
                "    geben Sie den Ordnerpfad im Feld \"img\" ein.\n" +
                "    Unterstuetzte Formate: .tif, .tiff, .jpg, .jpeg, .png, .bmp.\n" +
                "    Geben Sie erstes und letztes Bild mit \"start\"/\"end\" an.\n\n" +
                "  - FPS: Das Feld \"fps\" legt die Wiedergabegeschwindigkeit\n" +
                "    fuer die Vorschau fest.\n\n" +

                "-------------------------------------------\n" +
                "  3. VIDEOPLAYER & NAVIGATION\n" +
                "-------------------------------------------\n\n" +
                "Der integrierte Player zeigt das AviSynth-Skriptergebnis\n" +
                "in Echtzeit. Jede Parameteraenderung regeneriert\n" +
                "automatisch das Skript und aktualisiert die Vorschau.\n\n" +
                "  Tastenkuerzel:\n" +
                "    Leertaste      Wiedergabe / Pause\n" +
                "    Pfeil links    Vorheriges Bild\n" +
                "    Pfeil rechts   Naechstes Bild\n" +
                "    Strg+Links     Zum Anfang\n" +
                "    Strg+Rechts    Zum Ende\n\n" +
                "  Transportleiste:\n" +
                "    |<   Anfang\n" +
                "    <<   Vorheriges Bild\n" +
                "    >    Wiedergabe / Pause\n" +
                "    >>   Naechstes Bild\n" +
                "    >|   Ende\n" +
                "    1x   Wiedergabegeschwindigkeit (Klick zum Wechseln)\n" +
                "    1/2  Halbe Aufloesung (schneller)\n\n" +
                "  Die Suchleiste zeigt aktuelle Position und Gesamtdauer.\n" +
                "  Klicken oder ziehen Sie, um im Film zu navigieren.\n\n" +

                "-------------------------------------------\n" +
                "  4. FILTERPANEL\n" +
                "-------------------------------------------\n\n" +
                "Das untere Panel ist in drei Spalten unterteilt:\n" +
                "  - Linke Spalte: Filter-Aktivierungsschaltflaechen\n" +
                "  - Veraenderbarer Teiler\n" +
                "  - Rechte Spalte: Detailparameter des gewaehlten Filters\n\n" +
                "Jeder Filter hat einen EIN/AUS-Knopf und einen\n" +
                "Erweiterungsknopf (>). Aktive Knoepfe erscheinen gruen.\n\n" +

                "ALLGEMEINE OPTIONEN:\n" +
                "  - preview       : AviSynth-Vorschau aktivieren\n" +
                "  - enable_flip_h : Horizontale Spiegelung\n" +
                "  - enable_flip_v : Vertikale Spiegelung\n\n" +

                "--- CROP (Zuschnitt) ---\n" +
                "  Crop_L / Crop_T / Crop_R / Crop_B: Pixel (0-500)\n\n" +

                "--- DEGRAIN (Kornreduktion) ---\n" +
                "  Zeitliche Filmkornreduktion ueber MVTools2.\n" +
                "  Schnellvoreinstellungen und manuelle Feinabstimmung.\n" +
                "  Parameter: degrain_mode, thSAD, thSADC, blksize,\n" +
                "  overlap, pel, search, prefilter.\n\n" +

                "--- DENOISE (Staubentfernung) ---\n" +
                "  Zeitliche Staub- und Kratzer-Erkennung.\n" +
                "  Parameter: denoise_mode, strength, dist, grey.\n\n" +

                "--- LUMA LEVELS ---\n" +
                "  Helligkeit, Kontrast, Saettigung, Farbton, Gamma.\n\n" +

                "--- GAMMAC (Farbkorrektur) ---\n" +
                "  Automatische RGB-Kanalkorrektur.\n" +
                "  Parameter: LockChan, LockVal, Scale, Th, HiTh,\n" +
                "  X, Y, W, H, Omin, Omax, Show, Verbosity.\n\n" +

                "--- SHARPEN (Schaerfung) ---\n" +
                "  Bildschaerfung. Modi: simple (global) oder edge (Konturen).\n" +
                "  Parameter: Sharp_Mode, Strength, Radius, Threshold.\n\n" +

                "-------------------------------------------\n" +
                "  5. VOREINSTELLUNGEN (PRESETS)\n" +
                "-------------------------------------------\n\n" +
                "  Speichern, Aktualisieren, Laden, Loeschen.\n" +
                "  Gespeichert in: %AppData%\\CleanScan\\presets.json\n\n" +

                "-------------------------------------------\n" +
                "  6. KODIERUNG\n" +
                "-------------------------------------------\n\n" +
                "Klicken Sie auf REC in der Transportleiste.\n" +
                "  Encoder: x264, x265, FFV1, UT Video, ProRes,\n" +
                "           TIFF-Seq., PNG-Seq.\n" +
                "  Container: MKV, MP4, AVI, MOV.\n" +
                "  Fortschrittsanzeige mit Prozent und Zeit.\n\n" +

                "-------------------------------------------\n" +
                "  7. EINSTELLUNGEN\n" +
                "-------------------------------------------\n\n" +
                "  - Threads: Anzahl der AviSynth-Threads.\n" +
                "  - Quell-Lader: Auto, FFMS2, L-SMASH, LWLibav.\n" +
                "  - Sprache: English, Francais, Deutsch, Espanol.\n\n" +

                "-------------------------------------------\n" +
                "  8. EMPFOHLENER WORKFLOW\n" +
                "-------------------------------------------\n\n" +
                "  1. Quelle oeffnen (Drag-and-Drop oder Quellenfeld).\n" +
                "  2. Zu einem repraesentativen Bild navigieren.\n" +
                "  3. Filter einzeln aktivieren: Crop > Degrain >\n" +
                "     Denoise > Luma > GamMac > Sharpen.\n" +
                "  4. Voreinstellungen verwenden, dann feinabstimmen.\n" +
                "  5. Ergebnis an mehreren Filmstellen pruefen.\n" +
                "  6. Konfiguration als Preset speichern.\n" +
                "  7. Endkodierung starten.\n\n" +

                "===========================================\n" +
                "           www.scanfilm.ch\n" +
                "===========================================",

            ["es"] =
                "===========================================\n" +
                "      CLEANSCAN  —  GUIA DEL USUARIO\n" +
                "     Version de prueba — grabacion\n" +
                "        limitada a 60 s. por clip\n" +
                "===========================================\n\n" +

                "CleanScan es una herramienta de restauracion\n" +
                "de peliculas analogicas digitalizadas. Genera un script\n" +
                "AviSynth optimizado y ofrece vista previa en tiempo real\n" +
                "mediante un reproductor de video integrado (libmpv).\n\n" +

                "-------------------------------------------\n" +
                "  1. REQUISITOS PREVIOS\n" +
                "-------------------------------------------\n\n" +
                "  - AviSynth+ debe estar instalado (64 bits).\n" +
                "    Si falta, CleanScan lo detecta al inicio y\n" +
                "    ofrece un enlace de descarga directa.\n" +
                "  - FFmpeg incluido en la aplicacion (Plugins/ffmpeg/)\n" +
                "    para la codificacion de archivos de salida.\n" +
                "  - El reproductor mpv incluido en la aplicacion (mpv/).\n\n" +

                "-------------------------------------------\n" +
                "  2. SELECCION DE FUENTE\n" +
                "-------------------------------------------\n\n" +
                "La barra superior contiene los campos de fuente:\n\n" +
                "  - Archivo de video: arrastre y suelte un archivo\n" +
                "    (.avi, .mp4, .mov, .mkv, .wmv, .m4v, .mpeg, .mpg, .webm)\n" +
                "    sobre el area del reproductor o haga clic en \"source\".\n\n" +
                "  - Secuencia de imagenes: active el modo imagen,\n" +
                "    introduzca la ruta en el campo \"img\".\n" +
                "    Formatos: .tif, .tiff, .jpg, .jpeg, .png, .bmp.\n" +
                "    Especifique primera y ultima imagen con \"start\"/\"end\".\n\n" +
                "  - FPS: el campo \"fps\" define la velocidad de\n" +
                "    reproduccion para la vista previa.\n\n" +

                "-------------------------------------------\n" +
                "  3. REPRODUCTOR Y NAVEGACION\n" +
                "-------------------------------------------\n\n" +
                "El reproductor integrado muestra el resultado del script\n" +
                "AviSynth en tiempo real. Cada cambio de parametro regenera\n" +
                "automaticamente el script y actualiza la vista previa.\n\n" +
                "  Atajos de teclado:\n" +
                "    Espacio         Reproducir / Pausa\n" +
                "    Flecha izq.     Fotograma anterior\n" +
                "    Flecha der.     Fotograma siguiente\n" +
                "    Ctrl+Izq.       Ir al inicio\n" +
                "    Ctrl+Der.       Ir al final\n\n" +
                "  Barra de transporte:\n" +
                "    |<   Inicio\n" +
                "    <<   Fotograma anterior\n" +
                "    >    Reproducir / Pausa\n" +
                "    >>   Fotograma siguiente\n" +
                "    >|   Final\n" +
                "    1x   Velocidad (clic para alternar)\n" +
                "    1/2  Vista previa a mitad de resolucion (mas rapida)\n\n" +
                "  La barra de busqueda muestra posicion y duracion total.\n" +
                "  Haga clic o arrastre para navegar por la pelicula.\n\n" +

                "-------------------------------------------\n" +
                "  4. PANEL DE FILTROS\n" +
                "-------------------------------------------\n\n" +
                "El panel inferior se divide en tres columnas:\n" +
                "  - Columna izquierda: botones de activacion\n" +
                "  - Separador redimensionable\n" +
                "  - Columna derecha: parametros detallados\n\n" +
                "Cada filtro tiene un boton ON/OFF y un boton\n" +
                "de expansion (>). Los botones activos aparecen en verde.\n\n" +

                "OPCIONES GENERALES:\n" +
                "  - preview       : activar vista previa AviSynth\n" +
                "  - enable_flip_h : volteo horizontal\n" +
                "  - enable_flip_v : volteo vertical\n\n" +

                "--- CROP (Recorte) ---\n" +
                "  Crop_L / Crop_T / Crop_R / Crop_B: pixeles (0-500)\n\n" +

                "--- DEGRAIN (Reduccion de grano) ---\n" +
                "  Reduccion temporal del grano via MVTools2.\n" +
                "  Presets rapidos y ajuste manual fino.\n" +
                "  Parametros: degrain_mode, thSAD, thSADC, blksize,\n" +
                "  overlap, pel, search, prefilter.\n\n" +

                "--- DENOISE (Eliminacion de polvo) ---\n" +
                "  Deteccion temporal de polvo y rayaduras.\n" +
                "  Parametros: denoise_mode, strength, dist, grey.\n\n" +

                "--- LUMA LEVELS ---\n" +
                "  Brillo, contraste, saturacion, tono, gamma.\n\n" +

                "--- GAMMAC (Correccion de color) ---\n" +
                "  Correccion automatica de canales RGB.\n" +
                "  Parametros: LockChan, LockVal, Scale, Th, HiTh,\n" +
                "  X, Y, W, H, Omin, Omax, Show, Verbosity.\n\n" +

                "--- SHARPEN (Enfoque) ---\n" +
                "  Mejora de nitidez. Modos: simple (global) o edge (contornos).\n" +
                "  Parametros: Sharp_Mode, Strength, Radius, Threshold.\n\n" +

                "-------------------------------------------\n" +
                "  5. PRESETS (PREAJUSTES)\n" +
                "-------------------------------------------\n\n" +
                "  Guardar, Actualizar, Cargar, Eliminar.\n" +
                "  Almacenados en: %AppData%\\CleanScan\\presets.json\n\n" +

                "-------------------------------------------\n" +
                "  6. CODIFICACION\n" +
                "-------------------------------------------\n\n" +
                "Haga clic en REC en la barra de transporte.\n" +
                "  Codificadores: x264, x265, FFV1, UT Video, ProRes,\n" +
                "                 secuencia TIFF, secuencia PNG.\n" +
                "  Contenedores: MKV, MP4, AVI, MOV.\n" +
                "  Barra de progreso con porcentaje y tiempo.\n\n" +

                "-------------------------------------------\n" +
                "  7. AJUSTES\n" +
                "-------------------------------------------\n\n" +
                "  - Threads: numero de hilos AviSynth.\n" +
                "  - Cargador de fuente: Auto, FFMS2, L-SMASH, LWLibav.\n" +
                "  - Idioma: English, Francais, Deutsch, Espanol.\n\n" +

                "-------------------------------------------\n" +
                "  8. FLUJO DE TRABAJO RECOMENDADO\n" +
                "-------------------------------------------\n\n" +
                "  1. Abrir fuente (arrastrar o campo source).\n" +
                "  2. Navegar a un fotograma representativo.\n" +
                "  3. Activar filtros uno por uno: Crop > Degrain >\n" +
                "     Denoise > Luma > GamMac > Sharpen.\n" +
                "  4. Usar presets, luego ajustar manualmente.\n" +
                "  5. Verificar resultado en varias escenas.\n" +
                "  6. Guardar configuracion como preset.\n" +
                "  7. Iniciar codificacion final.\n\n" +

                "===========================================\n" +
                "           www.scanfilm.ch\n" +
                "==========================================="
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
            SizeChanged     += OnWindowSizeChanged;
            BottomPanel.SizeChanged += OnBottomPanelSizeChanged;
            InitializeChoiceFields();
            UpdateOptionColumnVisibility();
            RegisterChangeHandlers();
            InitSliders();
            InitRecordPanel();
            InitPlayerControls();
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
                        if (avsCheck.Contains("chargeable OK", StringComparison.Ordinal))
                            ShowPlayerStatus("Aucune source configurée.\nGlissez un fichier vidéo sur le player ou renseignez le champ SOURCE.");
                        return;
                    }
                    _ = LoadScriptAsync();
                };
                host.FileDropped += OnPlayerFileDrop;
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
                    return $"AviSynth.dll présent et chargeable OK";
                }
                return $"AviSynth.dll présent dans System32 mais non chargeable (mauvaise architecture ?)";
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

            if (this.FindControl<Slider>("SeekBar") is { } s) s.Value = _pendingSeekPos;

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
            _config.Changed -= OnConfigChangedForSlider;
            SaveWindowSettings();
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
            ApplyRecordLabels();

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
            if (this.FindControl<Button>("RecordStartBtn") is { } startBtn)
                startBtn.Content = GetUiText("RecordStartBtn");
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
            else
                SnapToBottomOfScreen();

            if (saved?.BottomPanelHeight is { } bph)
                MainGrid.RowDefinitions[2].Height = new GridLength(Math.Clamp(bph, 60, 800), GridUnitType.Pixel);

            _layoutInitialized = true;
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

            return Width;
        }

        private double ClampWindowWidth(double width)
        {
            var maxWidth = double.IsFinite(MaxWidth) ? MaxWidth : double.MaxValue;
            return Math.Clamp(width, MinWidth, maxWidth);
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
                _windowStateService.Save(s with { Language = ViewModel.CurrentLanguageCode, OpenPanels = panels });
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
                if (this.FindControl<Control>(targetName) is { } panel) panel.IsVisible = false;
                btn.Content = "▶";
                btn.Classes.Remove("active");
            }
            else
            {
                // N'ouvrir que si le filtre est actif
                if (!IsParamPanelEnabled(targetName)) return;

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

        private bool _recordOpen;
        private void InitRecordPanel()
        {
            if (this.FindControl<TextBox>("RecordDir") is { } tb)
                tb.LostFocus += (_, _) => UpdateDiskSpaceLabel(tb.Text);

            if (this.FindControl<ComboBox>("RecordEncoder") is { } enc)
                enc.SelectionChanged += OnRecordEncoderChanged;
        }

        private void OnRecordEncoderChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this.FindControl<ComboBox>("RecordEncoder") is not { } enc) return;
            var tag = (enc.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var isImageSeq = tag is "tiff" or "png";
            if (this.FindControl<ComboBox>("RecordContainer") is { } cnt)
                cnt.IsEnabled = !isImageSeq;
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
                tb.Text = folder[0].Path.LocalPath;
                UpdateDiskSpaceLabel(tb.Text);
            }
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
                if (root is null) { lbl.Text = ""; return; }
                var drive = new DriveInfo(root);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0);
                lbl.Text = freeGb >= 1024
                    ? $"({freeGb / 1024.0:F1} Go)"
                    : $"({freeGb:F0} Mo)";
            }
            catch { lbl.Text = ""; }
        }

        private Process? _encodingProcess;
        private CancellationTokenSource? _encodingCts;
        private string _lastStderrLine = string.Empty;

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
            var fileName = this.FindControl<TextBox>("RecordFileName")?.Text?.Trim();
            var encoder = (this.FindControl<ComboBox>("RecordEncoder")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "x264";
            var container = (this.FindControl<ComboBox>("RecordContainer")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mkv";

            if (string.IsNullOrWhiteSpace(dir))
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordNoDirError"));
                return;
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordNoFileError"));
                return;
            }

            // Ensure output dir exists
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), ex.Message);
                return;
            }

            // Generate a render script (preview=false, preview_half=false)
            var renderScriptPath = GenerateRenderScript();
            if (renderScriptPath is null)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordNoScriptError"));
                return;
            }

            // Find ffmpeg
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath is null)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("RecordFfmpegNotFound"));
                return;
            }

            // Build output path and ffmpeg arguments
            var isImageSeq = encoder is "tiff" or "png";
            string outputPath;
            string ffArgs;

            if (isImageSeq)
            {
                var ext = encoder == "tiff" ? "tif" : "png";
                var seqDir = Path.Combine(dir, fileName);
                try { Directory.CreateDirectory(seqDir); }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), ex.Message);
                    return;
                }
                outputPath = Path.Combine(seqDir, $"%05d.{ext}");
                ffArgs = $"-progress pipe:2 -t 60 -i \"{renderScriptPath}\" \"{outputPath}\"";
            }
            else
            {
                outputPath = Path.Combine(dir, $"{fileName}.{container}");
                var codecArgs = encoder switch
                {
                    "x264" => "-c:v libx264 -crf 18 -preset medium",
                    "x265" => "-c:v libx265 -crf 20 -preset medium",
                    "ffv1" => "-c:v ffv1 -level 3 -slicecrc 1",
                    "utvideo" => "-c:v utvideo",
                    "prores" => "-c:v prores_ks -profile:v 3",
                    _ => "-c:v libx264 -crf 18 -preset medium"
                };
                ffArgs = $"-progress pipe:2 -t 60 -i \"{renderScriptPath}\" {codecArgs} -y \"{outputPath}\"";
            }

            SetRecordStartButtonState(idle: false);
            SetRecordProgressVisible(true);
            _encodingCts = new CancellationTokenSource();
            _lastStderrLine = string.Empty;

            // Get total duration from mpv for progress calculation
            var totalDuration = _mpvService.Duration;

            try
            {
                _encodingProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffArgs,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                _encodingProcess.Start();

                // Read stderr asynchronously to avoid deadlock and parse progress
                var stderrTask = ReadFfmpegStderrAsync(_encodingProcess.StandardError, totalDuration, _encodingCts.Token);

                // Wait for exit asynchronously
                await _encodingProcess.WaitForExitAsync(_encodingCts.Token);
                await stderrTask;

                if (_encodingProcess.ExitCode == 0)
                {
                    UpdateDiskSpaceLabel(dir);
                    UpdateRecordProgress(100, "100%");
                    await _dialogService.ShowErrorAsync(this, GetUiText("RecordBtn"), GetUiText("RecordDoneMsg"));
                    await _dialogService.ShowErrorAsync(this,
                        "Version d'essai",
                        "Cette version d'essai limite la durée de votre clip à 1 min.");
                }
                else
                {
                    var msg = string.IsNullOrWhiteSpace(_lastStderrLine)
                        ? $"ffmpeg exit code {_encodingProcess.ExitCode}"
                        : _lastStderrLine;
                    await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), msg);
                }
            }
            catch (OperationCanceledException) { /* user cancelled */ }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), ex.Message);
            }
            finally
            {
                _encodingProcess?.Dispose();
                _encodingProcess = null;
                _encodingCts = null;
                SetRecordStartButtonState(idle: true);
                SetRecordProgressVisible(false);
            }
        }

        private async Task ReadFfmpegStderrAsync(System.IO.StreamReader stderr, double totalDuration, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await stderr.ReadLineAsync(ct);
                    if (line is null) break; // EOF

                    _lastStderrLine = line;

                    // Parse "-progress pipe:2" output: "out_time_us=<microseconds>"
                    if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && totalDuration > 0)
                    {
                        if (long.TryParse(line.AsSpan("out_time_us=".Length), out var us) && us >= 0)
                        {
                            var seconds = us / 1_000_000.0;
                            var pct = Math.Min(100.0, seconds / totalDuration * 100.0);
                            var elapsed = TimeSpan.FromSeconds(seconds);
                            var label = $"{pct:F1}%  —  {elapsed:hh\\:mm\\:ss}";
                            Dispatcher.UIThread.Post(() => UpdateRecordProgress(pct, label));
                        }
                    }
                    // Fallback: parse classic "frame=...time=HH:MM:SS" lines
                    else if (line.Contains("time=", StringComparison.Ordinal) && totalDuration > 0)
                    {
                        var idx = line.IndexOf("time=", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var timePart = line.AsSpan(idx + 5);
                            var spaceIdx = timePart.IndexOf(' ');
                            if (spaceIdx > 0) timePart = timePart[..spaceIdx];
                            if (TimeSpan.TryParse(timePart, CultureInfo.InvariantCulture, out var ts))
                            {
                                var pct = Math.Min(100.0, ts.TotalSeconds / totalDuration * 100.0);
                                var label = $"{pct:F1}%  —  {ts:hh\\:mm\\:ss}";
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
                    ShowPlayerStatus("Aucune source valide.\nRenseignez le champ SOURCE avec un fichier existant.");
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

        private async void OnPresetClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowPresetDialogAsync(this, _presetService, _config, ApplyPresetValuesAsync, ViewModel);

        private async Task ApplyPresetValuesAsync(Dictionary<string, string> values)
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
