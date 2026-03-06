using System;
using System.Collections.Generic;
using System.Globalization;

namespace CleanScan.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private static readonly Dictionary<string, Dictionary<string, string>> UiTexts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["InfosMenu"] = "Infos",
                ["UserGuideMenuItem"] = "User guide",
                ["ScriptPreviewMenuItem"] = "Script preview",
                ["LanguagesMenu"] = "Languages",
                ["PresetMenuItem"] = "Preset",
                ["AboutMenuItem"] = "About",
                ["AllRightsReserved"] = "all rights reserved",
                ["AboutCompany"] = "ScanFilm SNC",
                ["AboutWebsite"] = "www.scanfilm.ch",
                ["AboutVersion"] = "trial version — beta 3.0",
                ["PresetDialogTitle"] = "Presets",
                ["PresetNameLabel"] = "Name",
                ["PresetSaveButton"] = "Save",
                ["PresetUpdateButton"] = "Update",
                ["PresetDeleteButton"] = "Delete",
                ["PresetLoadButton"] = "Load",
                ["GamMacInfoButton"] = "Info",
                ["GamMacInfoTooltip"] = "GamMac information",
                ["GamMacInfoTitle"] = "GamMac information",
                ["GamMacCloseButton"] = "Close",
                ["ErrorTitle"] = "Error",
                ["UserGuideTitle"] = "User guide",
                ["OkButton"] = "OK",
                ["DownloadButton"] = "Download",
                ["PickFilmTitle"] = "Select a file for film",
                ["PickImgTitle"] = "Select a .tif/.tiff file for img",
                ["PickSourceTitle"] = "Select a source file (video or image)",
                ["VirtualDubNotFound"] = "VirtualDub was not found. Install VirtualDub (VirtualDub2.exe) or copy VirtualDub2.exe next to CleanScan.",
                ["AviSynthRequiredTitle"] = "AviSynth+ required",
                ["AviSynthNotDetectedBody"] = "AviSynth+ was not detected on this computer. Do you want to download it?",
                ["SelectTiffError"] = "Please select a .tif or .tiff file.",
                ["IndexNameError"] = "The file name must contain only a numeric index (e.g. 00001.tif).",
                ["NoFilmSpecified"] = "No file is set in film.",
                ["NoImgSpecified"] = "No file is set in img.",
                ["FilmNotFound"] = "The file referenced in film was not found.",
                ["ImgNotFound"] = "The file referenced in img was not found.",
                ["DropInvalidFileType"] = "Unsupported file type. Please drop a video file (.avi, .mp4, .mov…) or an image file (.tif, .tiff, .jpg…).",
                ["VdbBeginning"] = "Beginning  (Ctrl+←)",
                ["VdbPrevFrame"] = "Prev frame  (←)",
                ["VdbPlay"]      = "Play  (Space)",
                ["VdbStop"]      = "Stop",
                ["VdbNextFrame"] = "Next frame  (→)",
                ["VdbEnd"]       = "End  (Ctrl+→)",
                ["SpeedBtn"]     = "Playback speed",
                ["HalfResBtn"]   = "Half-resolution preview",
                ["DropHintBar"]  = "\u25bc  Drop your file here  \u25bc",
["RecordBtn"]    = "Record",
                ["RecordDirLabel"] = "Output directory",
                ["RecordFileLabel"] = "File name",
                ["RecordEncoderLabel"] = "Encoder",
                ["RecordContainerLabel"] = "Container",
                ["RecordDirPickTitle"] = "Select output directory",
                ["RecordStartBtn"] = "▶ Start encoding",
                ["RecordStopBtn"] = "■ Stop encoding",
                ["RecordNoDirError"] = "Please select an output directory.",
                ["RecordNoFileError"] = "Please enter a file name.",
                ["RecordNoScriptError"] = "No script available for encoding.",
                ["RecordFfmpegNotFound"] = "ffmpeg was not found. Place ffmpeg.exe next to CleanScan or install it in PATH.",
                ["RecordDoneMsg"] = "Encoding completed successfully."
            },
            ["fr"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["InfosMenu"] = "Infos",
                ["UserGuideMenuItem"] = "Mode d'emploi",
                ["ScriptPreviewMenuItem"] = "Lecture du script",
                ["LanguagesMenu"] = "Langues",
                ["PresetMenuItem"] = "Preset",
                ["AboutMenuItem"] = "A propos",
                ["AllRightsReserved"] = "tous droits réservés",
                ["AboutCompany"] = "ScanFilm SNC",
                ["AboutWebsite"] = "www.scanfilm.ch",
                ["AboutVersion"] = "version d'essai — beta 3.0",
                ["PresetDialogTitle"] = "Presets",
                ["PresetNameLabel"] = "Nom",
                ["PresetSaveButton"] = "Enregistrer",
                ["PresetUpdateButton"] = "Mettre à jour",
                ["PresetDeleteButton"] = "Supprimer",
                ["PresetLoadButton"] = "Charger",
                ["GamMacInfoButton"] = "Info",
                ["GamMacInfoTooltip"] = "Informations sur GamMac",
                ["GamMacInfoTitle"] = "Informations GamMac",
                ["GamMacCloseButton"] = "Fermer",
                ["ErrorTitle"] = "Erreur",
                ["UserGuideTitle"] = "Mode d'emploi",
                ["OkButton"] = "OK",
                ["DownloadButton"] = "Télécharger",
                ["PickFilmTitle"] = "Sélectionner un fichier pour film",
                ["PickImgTitle"] = "Sélectionner un fichier .tif/.tiff pour img",
                ["PickSourceTitle"] = "Sélectionner un fichier source (vidéo ou image)",
                ["VirtualDubNotFound"] = "VirtualDub est introuvable. Installez VirtualDub (VirtualDub2.exe) ou copiez VirtualDub2.exe à côté de CleanScan.",
                ["AviSynthRequiredTitle"] = "AviSynth+ requis",
                ["AviSynthNotDetectedBody"] = "AviSynth+ n'a pas été détecté sur cet ordinateur. Voulez-vous le télécharger ?",
                ["SelectTiffError"] = "Veuillez sélectionner un fichier .tif ou .tiff.",
                ["IndexNameError"] = "Le nom du fichier doit contenir uniquement un index numérique (ex. 00001.tif).",
                ["NoFilmSpecified"] = "Aucun fichier n'est défini dans film.",
                ["NoImgSpecified"] = "Aucun fichier n'est défini dans img.",
                ["FilmNotFound"] = "Le fichier référencé dans film est introuvable.",
                ["ImgNotFound"] = "Le fichier référencé dans img est introuvable.",
                ["DropInvalidFileType"] = "Type de fichier non pris en charge. Déposez un fichier vidéo (.avi, .mp4, .mov…) ou une image (.tif, .tiff, .jpg…).",
                ["VdbBeginning"] = "Début  (Ctrl+←)",
                ["VdbPrevFrame"] = "Image précédente  (←)",
                ["VdbPlay"]      = "Lecture  (Space)",
                ["VdbStop"]      = "Arrêt",
                ["VdbNextFrame"] = "Image suivante  (→)",
                ["VdbEnd"]       = "Fin  (Ctrl+→)",
                ["SpeedBtn"]     = "Vitesse de lecture",
                ["HalfResBtn"]   = "Aperçu demi-résolution",
                ["DropHintBar"]  = "\u25bc  Glisse ton fichier ici  \u25bc",
["RecordBtn"]    = "Enregistrer",
                ["RecordDirLabel"] = "Répertoire de sortie",
                ["RecordFileLabel"] = "Nom du fichier",
                ["RecordEncoderLabel"] = "Encodeur",
                ["RecordContainerLabel"] = "Conteneur",
                ["RecordDirPickTitle"] = "Sélectionner le répertoire de sortie",
                ["RecordStartBtn"] = "▶ Lancer l'encodage",
                ["RecordStopBtn"] = "■ Arrêter l'encodage",
                ["RecordNoDirError"] = "Veuillez sélectionner un répertoire de sortie.",
                ["RecordNoFileError"] = "Veuillez saisir un nom de fichier.",
                ["RecordNoScriptError"] = "Aucun script disponible pour l'encodage.",
                ["RecordFfmpegNotFound"] = "ffmpeg est introuvable. Placez ffmpeg.exe à côté de CleanScan ou installez-le dans le PATH.",
                ["RecordDoneMsg"] = "Encodage terminé avec succès."
            },
            ["de"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["InfosMenu"] = "Infos",
                ["UserGuideMenuItem"] = "Benutzerhandbuch",
                ["ScriptPreviewMenuItem"] = "Script-Vorschau",
                ["LanguagesMenu"] = "Sprachen",
                ["PresetMenuItem"] = "Preset",
                ["AboutMenuItem"] = "Über",
                ["AllRightsReserved"] = "alle Rechte vorbehalten",
                ["AboutCompany"] = "ScanFilm SNC",
                ["AboutWebsite"] = "www.scanfilm.ch",
                ["AboutVersion"] = "Testversion — Beta 3.0",
                ["PresetDialogTitle"] = "Presets",
                ["PresetNameLabel"] = "Name",
                ["PresetSaveButton"] = "Speichern",
                ["PresetUpdateButton"] = "Aktualisieren",
                ["PresetDeleteButton"] = "Löschen",
                ["PresetLoadButton"] = "Laden",
                ["GamMacInfoButton"] = "Info",
                ["GamMacInfoTooltip"] = "GamMac-Informationen",
                ["GamMacInfoTitle"] = "GamMac-Informationen",
                ["GamMacCloseButton"] = "Schließen",
                ["ErrorTitle"] = "Fehler",
                ["UserGuideTitle"] = "Benutzerhandbuch",
                ["OkButton"] = "OK",
                ["DownloadButton"] = "Herunterladen",
                ["PickFilmTitle"] = "Datei für film auswählen",
                ["PickImgTitle"] = "Eine .tif/.tiff-Datei für img auswählen",
                ["PickSourceTitle"] = "Quelldatei auswählen (Video oder Bild)",
                ["VirtualDubNotFound"] = "VirtualDub wurde nicht gefunden. Installieren Sie VirtualDub (VirtualDub2.exe) oder kopieren Sie VirtualDub2.exe neben CleanScan.",
                ["AviSynthRequiredTitle"] = "AviSynth+ erforderlich",
                ["AviSynthNotDetectedBody"] = "AviSynth+ wurde auf diesem Computer nicht erkannt. Möchten Sie es herunterladen?",
                ["SelectTiffError"] = "Bitte wählen Sie eine .tif- oder .tiff-Datei aus.",
                ["IndexNameError"] = "Der Dateiname darf nur einen numerischen Index enthalten (z. B. 00001.tif).",
                ["NoFilmSpecified"] = "In film ist keine Datei gesetzt.",
                ["NoImgSpecified"] = "In img ist keine Datei gesetzt.",
                ["FilmNotFound"] = "Die in film referenzierte Datei wurde nicht gefunden.",
                ["ImgNotFound"] = "Die in img referenzierte Datei wurde nicht gefunden.",
                ["DropInvalidFileType"] = "Nicht unterstützter Dateityp. Bitte eine Videodatei (.avi, .mp4, .mov…) oder Bilddatei (.tif, .tiff, .jpg…) ablegen.",
                ["VdbBeginning"] = "Anfang  (Ctrl+←)",
                ["VdbPrevFrame"] = "Vorheriges Bild  (←)",
                ["VdbPlay"]      = "Abspielen  (Space)",
                ["VdbStop"]      = "Stopp",
                ["VdbNextFrame"] = "Nächstes Bild  (→)",
                ["VdbEnd"]       = "Ende  (Ctrl+→)",
                ["SpeedBtn"]     = "Wiedergabegeschwindigkeit",
                ["HalfResBtn"]   = "Vorschau halbe Auflösung",
                ["DropHintBar"]  = "\u25bc  Datei hier ablegen  \u25bc",
["RecordBtn"]    = "Aufnahme",
                ["RecordDirLabel"] = "Ausgabeverzeichnis",
                ["RecordFileLabel"] = "Dateiname",
                ["RecordEncoderLabel"] = "Encoder",
                ["RecordContainerLabel"] = "Container",
                ["RecordDirPickTitle"] = "Ausgabeverzeichnis auswählen",
                ["RecordStartBtn"] = "▶ Kodierung starten",
                ["RecordStopBtn"] = "■ Kodierung stoppen",
                ["RecordNoDirError"] = "Bitte wählen Sie ein Ausgabeverzeichnis.",
                ["RecordNoFileError"] = "Bitte geben Sie einen Dateinamen ein.",
                ["RecordNoScriptError"] = "Kein Skript für die Kodierung verfügbar.",
                ["RecordFfmpegNotFound"] = "ffmpeg wurde nicht gefunden. Platzieren Sie ffmpeg.exe neben CleanScan oder installieren Sie es im PATH.",
                ["RecordDoneMsg"] = "Kodierung erfolgreich abgeschlossen."
            },
            ["es"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["InfosMenu"] = "Infos",
                ["UserGuideMenuItem"] = "Guía del usuario",
                ["ScriptPreviewMenuItem"] = "Vista previa del script",
                ["LanguagesMenu"] = "Idiomas",
                ["PresetMenuItem"] = "Preset",
                ["AboutMenuItem"] = "Acerca de",
                ["AllRightsReserved"] = "todos los derechos reservados",
                ["AboutCompany"] = "ScanFilm SNC",
                ["AboutWebsite"] = "www.scanfilm.ch",
                ["AboutVersion"] = "versión de prueba — beta 3.0",
                ["PresetDialogTitle"] = "Presets",
                ["PresetNameLabel"] = "Nombre",
                ["PresetSaveButton"] = "Guardar",
                ["PresetUpdateButton"] = "Actualizar",
                ["PresetDeleteButton"] = "Eliminar",
                ["PresetLoadButton"] = "Cargar",
                ["GamMacInfoButton"] = "Info",
                ["GamMacInfoTooltip"] = "Información de GamMac",
                ["GamMacInfoTitle"] = "Información de GamMac",
                ["GamMacCloseButton"] = "Cerrar",
                ["ErrorTitle"] = "Error",
                ["UserGuideTitle"] = "Guía del usuario",
                ["OkButton"] = "OK",
                ["DownloadButton"] = "Descargar",
                ["PickFilmTitle"] = "Seleccionar un archivo para film",
                ["PickImgTitle"] = "Seleccionar un archivo .tif/.tiff para img",
                ["PickSourceTitle"] = "Seleccionar un archivo fuente (vídeo o imagen)",
                ["VirtualDubNotFound"] = "No se encontró VirtualDub. Instale VirtualDub (VirtualDub2.exe) o copie VirtualDub2.exe junto a CleanScan.",
                ["AviSynthRequiredTitle"] = "AviSynth+ requerido",
                ["AviSynthNotDetectedBody"] = "AviSynth+ no se detectó en este equipo. ¿Desea descargarlo?",
                ["SelectTiffError"] = "Seleccione un archivo .tif o .tiff.",
                ["IndexNameError"] = "El nombre del archivo debe contener solo un índice numérico (p. ej. 00001.tif).",
                ["NoFilmSpecified"] = "No hay ningún archivo definido en film.",
                ["NoImgSpecified"] = "No hay ningún archivo definido en img.",
                ["FilmNotFound"] = "No se encontró el archivo referenciado en film.",
                ["ImgNotFound"] = "No se encontró el archivo referenciado en img.",
                ["DropInvalidFileType"] = "Tipo de archivo no compatible. Suelte un archivo de vídeo (.avi, .mp4, .mov…) o imagen (.tif, .tiff, .jpg…).",
                ["VdbBeginning"] = "Inicio  (Ctrl+←)",
                ["VdbPrevFrame"] = "Fotograma anterior  (←)",
                ["VdbPlay"]      = "Reproducir  (Space)",
                ["VdbStop"]      = "Detener",
                ["VdbNextFrame"] = "Fotograma siguiente  (→)",
                ["VdbEnd"]       = "Fin  (Ctrl+→)",
                ["SpeedBtn"]     = "Velocidad de reproducción",
                ["HalfResBtn"]   = "Vista previa a media resolución",
                ["DropHintBar"]  = "\u25bc  Suelta tu archivo aquí  \u25bc",
["RecordBtn"]    = "Grabar",
                ["RecordDirLabel"] = "Directorio de salida",
                ["RecordFileLabel"] = "Nombre del archivo",
                ["RecordEncoderLabel"] = "Codificador",
                ["RecordContainerLabel"] = "Contenedor",
                ["RecordDirPickTitle"] = "Seleccionar directorio de salida",
                ["RecordStartBtn"] = "▶ Iniciar codificación",
                ["RecordStopBtn"] = "■ Detener codificación",
                ["RecordNoDirError"] = "Seleccione un directorio de salida.",
                ["RecordNoFileError"] = "Introduzca un nombre de archivo.",
                ["RecordNoScriptError"] = "No hay script disponible para la codificación.",
                ["RecordFfmpegNotFound"] = "No se encontró ffmpeg. Coloque ffmpeg.exe junto a CleanScan o instálelo en el PATH.",
                ["RecordDoneMsg"] = "Codificación completada con éxito."
            }
        };

        public string CurrentLanguageCode { get; private set; } = GetOsLanguageCodeOrEnglish();

        public static string GetOsLanguageCodeOrEnglish()
        {
            var osTwoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return UiTexts.ContainsKey(osTwoLetter) ? osTwoLetter : "en";
        }

        public void SetLanguage(string languageCode)
        {
            CurrentLanguageCode = UiTexts.ContainsKey(languageCode) ? languageCode : "en";
        }

        public string GetUiText(string key)
        {
            if (UiTexts.TryGetValue(CurrentLanguageCode, out var map) && map.TryGetValue(key, out var text))
            {
                return text;
            }

            if (UiTexts.TryGetValue("en", out var enMap) && enMap.TryGetValue(key, out var enText))
            {
                return enText;
            }

            return key;
        }

        public string GetUiText(string key, params object[] args)
        {
            var fmt = GetUiText(key);
            return args.Length == 0 ? fmt : string.Format(CultureInfo.CurrentCulture, fmt, args);
        }

        public string GetLocalizedText(string fr, string en)
        {
            return string.Equals(CurrentLanguageCode, "fr", StringComparison.OrdinalIgnoreCase) ? fr : en;
        }
    }
}
