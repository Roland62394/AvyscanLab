; ─────────────────────────────────────────────────────
; AvyScan Lab — NSIS Installer Script
; ─────────────────────────────────────────────────────
; Prerequisites:
;   1. Install NSIS 3+ from https://nsis.sourceforge.io/Download
;   2. Build/publish the app:
;        dotnet publish -c Release -r win-x64 --self-contained
;      or use publish-release.bat for obfuscated build
;   3. Place AviSynthPlus installer in Installer/ folder:
;        AviSynthPlus_3.7.5_20250420_vcredist.exe
;   4. Right-click this file → "Compile NSIS Script"
;      or run:  makensis AvyScanLab.nsi
;
; The installer will be created in Installer\Output\
; ─────────────────────────────────────────────────────

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"

; ── App metadata ──
!define APP_NAME      "AvyScan Lab"
!define APP_VERSION   "vers. 1.0"
!define APP_PUBLISHER "ScanFilm SNC"
!define APP_URL       "https://www.scanfilm.ch"
!define APP_EXE       "AvyScanLab.exe"
!define PUBLISH_DIR   "..\bin\Release\net10.0\win-x64\publish"
!define AVS_INSTALLER "AviSynthPlus_3.7.5_20250420_vcredist.exe"

; GPL plugin DLLs — installed into AviSynth+ plugins64+ folder
!define GPL_PLUGINS_DIR "GPL_Plugins"
; Permissive-license plugins (ISC/MIT) — also installed into AviSynth+ plugins64+
!define AVS_PLUGINS_DIR "AVS_Plugins"

; ── Installer settings ──
Name "${APP_NAME} ${APP_VERSION}"
OutFile "Output\AvyScanLab_Setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin
Unicode True
SetCompressor /SOLID lzma

; ── Icon ──
!define MUI_ICON   "..\Assets\Ico.ico"
!define MUI_UNICON "..\Assets\Ico.ico"

; ── Modern UI configuration ──
!define MUI_ABORTWARNING

; Installer pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
; "Launch app" checkbox on finish page
; Use a custom function to launch the app as the normal (non-elevated) user.
; Without this, the app inherits the installer's admin privileges and UIPI
; blocks drag-and-drop from Explorer (which runs at medium integrity).
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchAsCurrentUser
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ── Languages ──
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "Spanish"

; ── Localized MessageBox strings ──
; NSIS substitutes ${APP_NAME} at compile time and $1 (register) at display time.
; All MessageBox calls below use $(MSG_*) which picks the active language.

; 1. Requires 64-bit Windows (fatal, installer aborts)
LangString MSG_REQUIRES_X64 ${LANG_ENGLISH} "${APP_NAME} requires a 64-bit version of Windows."
LangString MSG_REQUIRES_X64 ${LANG_FRENCH}  "${APP_NAME} nécessite une version 64 bits de Windows."
LangString MSG_REQUIRES_X64 ${LANG_GERMAN}  "${APP_NAME} benötigt eine 64-Bit-Version von Windows."
LangString MSG_REQUIRES_X64 ${LANG_SPANISH} "${APP_NAME} requiere una versión de Windows de 64 bits."

; 2. Previous version already installed (Yes = uninstall / No = overwrite / Cancel = abort)
LangString MSG_ALREADY_INSTALLED ${LANG_ENGLISH} "${APP_NAME} is already installed.$\n$\nDo you want to uninstall the previous version before continuing?$\n$\nYes = Uninstall first, then install$\nNo  = Install over existing (update)$\nCancel = Abort"
LangString MSG_ALREADY_INSTALLED ${LANG_FRENCH}  "${APP_NAME} est déjà installé.$\n$\nVoulez-vous désinstaller la version précédente avant de continuer ?$\n$\nOui = Désinstaller d'abord, puis installer$\nNon = Installer par-dessus (mise à jour)$\nAnnuler = Abandonner"
LangString MSG_ALREADY_INSTALLED ${LANG_GERMAN}  "${APP_NAME} ist bereits installiert.$\n$\nMöchten Sie die vorherige Version zuerst deinstallieren?$\n$\nJa = Zuerst deinstallieren, dann installieren$\nNein = Über bestehende installieren (Update)$\nAbbrechen = Abbrechen"
LangString MSG_ALREADY_INSTALLED ${LANG_SPANISH} "${APP_NAME} ya está instalado.$\n$\n¿Desea desinstalar la versión anterior antes de continuar?$\n$\nSí = Desinstalar primero, luego instalar$\nNo = Instalar sobre la existente (actualizar)$\nCancelar = Cancelar"

; 3. App currently running — must be closed before install can proceed
LangString MSG_APP_RUNNING ${LANG_ENGLISH} "${APP_NAME} is currently running and will be closed to proceed with the installation.$\n$\nClick OK to close it and continue, or Cancel to exit."
LangString MSG_APP_RUNNING ${LANG_FRENCH}  "${APP_NAME} est actuellement en cours d'exécution et sera fermé pour poursuivre l'installation.$\n$\nCliquez sur OK pour le fermer et continuer, ou sur Annuler pour quitter."
LangString MSG_APP_RUNNING ${LANG_GERMAN}  "${APP_NAME} wird derzeit ausgeführt und wird geschlossen, um die Installation fortzusetzen.$\n$\nKlicken Sie auf OK, um es zu schließen und fortzufahren, oder auf Abbrechen, um zu beenden."
LangString MSG_APP_RUNNING ${LANG_SPANISH} "${APP_NAME} se está ejecutando actualmente y se cerrará para continuar con la instalación.$\n$\nHaga clic en Aceptar para cerrarlo y continuar, o en Cancelar para salir."

; 4. AviSynth+ dependency required
LangString MSG_AVS_REQUIRED ${LANG_ENGLISH} "${APP_NAME} requires AviSynth+ to function.$\n$\nAviSynth+ (GPLv2) will now be installed.$\nClick OK to proceed, or Cancel to exit."
LangString MSG_AVS_REQUIRED ${LANG_FRENCH}  "${APP_NAME} nécessite AviSynth+ pour fonctionner.$\n$\nAviSynth+ (GPLv2) va maintenant être installé.$\nCliquez sur OK pour continuer, ou sur Annuler pour quitter."
LangString MSG_AVS_REQUIRED ${LANG_GERMAN}  "${APP_NAME} benötigt AviSynth+ zum Ausführen.$\n$\nAviSynth+ (GPLv2) wird jetzt installiert.$\nKlicken Sie auf OK, um fortzufahren, oder auf Abbrechen, um zu beenden."
LangString MSG_AVS_REQUIRED ${LANG_SPANISH} "${APP_NAME} requiere AviSynth+ para funcionar.$\n$\nAviSynth+ (GPLv2) se instalará ahora.$\nHaga clic en Aceptar para continuar, o en Cancelar para salir."

; 5. AviSynth+ install exit code != 0 ($1 holds the exit code, resolved at runtime)
LangString MSG_AVS_INSTALL_FAILED ${LANG_ENGLISH} "AviSynth+ installation may have failed (exit code: $1).$\n$\nClick OK to continue anyway, or Cancel to exit."
LangString MSG_AVS_INSTALL_FAILED ${LANG_FRENCH}  "L'installation d'AviSynth+ a peut-être échoué (code de sortie : $1).$\n$\nCliquez sur OK pour continuer malgré tout, ou sur Annuler pour quitter."
LangString MSG_AVS_INSTALL_FAILED ${LANG_GERMAN}  "Die Installation von AviSynth+ ist möglicherweise fehlgeschlagen (Exit-Code: $1).$\n$\nKlicken Sie auf OK, um trotzdem fortzufahren, oder auf Abbrechen, um zu beenden."
LangString MSG_AVS_INSTALL_FAILED ${LANG_SPANISH} "Es posible que la instalación de AviSynth+ haya fallado (código de salida: $1).$\n$\nHaga clic en Aceptar para continuar de todos modos, o en Cancelar para salir."

; 6. Uninstall-time license deactivation reminder (Lemon Squeezy)
LangString MSG_LICENSE_DEACTIVATE ${LANG_ENGLISH} "Before uninstalling ${APP_NAME}, please make sure you have deactivated your license.$\n$\nIn the app:  Settings  ->  License  ->  Deactivate$\n$\nThis releases an activation slot on the license server so you can reinstall on another machine later.$\n$\nClick OK to continue uninstalling (only if you have already deactivated),$\nor Cancel to abort and deactivate first."
LangString MSG_LICENSE_DEACTIVATE ${LANG_FRENCH}  "Avant de désinstaller ${APP_NAME}, assurez-vous d'avoir désactivé votre licence.$\n$\nDans l'application :  Paramètres  ->  Licence  ->  Désactiver$\n$\nCela libère un emplacement d'activation sur le serveur de licences afin que vous puissiez réinstaller sur une autre machine plus tard.$\n$\nCliquez sur OK pour poursuivre la désinstallation (uniquement si vous avez déjà désactivé),$\nou sur Annuler pour abandonner et désactiver d'abord."
LangString MSG_LICENSE_DEACTIVATE ${LANG_GERMAN}  "Bevor Sie ${APP_NAME} deinstallieren, stellen Sie bitte sicher, dass Sie Ihre Lizenz deaktiviert haben.$\n$\nIn der Anwendung:  Einstellungen  ->  Lizenz  ->  Deaktivieren$\n$\nDies gibt einen Aktivierungsplatz auf dem Lizenzserver frei, sodass Sie später auf einer anderen Maschine neu installieren können.$\n$\nKlicken Sie auf OK, um die Deinstallation fortzusetzen (nur wenn Sie bereits deaktiviert haben),$\noder auf Abbrechen, um abzubrechen und zuerst zu deaktivieren."
LangString MSG_LICENSE_DEACTIVATE ${LANG_SPANISH} "Antes de desinstalar ${APP_NAME}, asegúrese de haber desactivado su licencia.$\n$\nEn la aplicación:  Configuración  ->  Licencia  ->  Desactivar$\n$\nEsto libera una ranura de activación en el servidor de licencias para que pueda reinstalar en otra máquina más adelante.$\n$\nHaga clic en Aceptar para continuar con la desinstalación (solo si ya ha desactivado),$\no en Cancelar para abortar y desactivar primero."

; ── Variable to hold AviSynth+ plugin directory ──
Var AVS_PLUGINDIR

; ─────────────────────────────────────────────────────
; INSTALLER SECTIONS
; ─────────────────────────────────────────────────────

Section "!${APP_NAME} (required)" SecCore
    SectionIn RO  ; Cannot be deselected

    SetOutPath "$INSTDIR"

    ; Main executable
    File "${PUBLISH_DIR}\${APP_EXE}"

    ; AviSynth script templates
    File "${PUBLISH_DIR}\ScriptMaster.en.avs"

    ; Built-in filter definitions (JSON)
    SetOutPath "$INSTDIR\Filters"
    File /nonfatal "${PUBLISH_DIR}\Filters\*.json"

    ; ffmpeg (encoding)
    SetOutPath "$INSTDIR\Plugins\ffmpeg"
    File "${PUBLISH_DIR}\Plugins\ffmpeg\ffmpeg.exe"
    File /nonfatal "${PUBLISH_DIR}\Plugins\ffmpeg\LICENSE"

    ; mpv player library
    SetOutPath "$INSTDIR\mpv"
    File /r "${PUBLISH_DIR}\mpv\*.*"

    ; User guide (txt — displayed in built-in editor)
    SetOutPath "$INSTDIR\Users Guide"
    File /nonfatal "${PUBLISH_DIR}\Users Guide\AvyScanLab_Guide_*.txt"

    ; Back to root for shortcuts
    SetOutPath "$INSTDIR"

    ; Write uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    ; Registry keys for Add/Remove Programs
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName"      "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion"   "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher"        "${APP_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout"     "${APP_URL}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString"  "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon"      "$INSTDIR\${APP_EXE}"
    WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

    ; Compute installed size for Add/Remove Programs
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "EstimatedSize" $0
SectionEnd

; ── AviSynth+ restoration plugins (installed into plugins64+ folder) ──
Section "Restoration plugins (required)" SecGplPlugins
    SectionIn RO  ; Cannot be deselected — required for AvyScan Lab

    ; Read AviSynth+ plugin directory from 64-bit registry view
    SetRegView 64
    ReadRegStr $AVS_PLUGINDIR HKLM "SOFTWARE\AviSynth" "plugindir+"
    SetRegView 32
    ${If} $AVS_PLUGINDIR == ""
        ; Fallback: standard path
        StrCpy $AVS_PLUGINDIR "$PROGRAMFILES32\AviSynth+\plugins64+"
    ${EndIf}

    ; Install plugin DLLs + their LICENSE files
    SetOutPath "$AVS_PLUGINDIR"

    ; LSMASHSource (ISC)
    File "${AVS_PLUGINS_DIR}\LSMASHSource\LSMASHSource.dll"
    File /oname=LSMASHSource_LICENSE.txt "${AVS_PLUGINS_DIR}\LSMASHSource\LICENSE"

    ; RgTools (MIT)
    File "${AVS_PLUGINS_DIR}\RgTools\RgTools.dll"
    File /oname=RgTools_LICENSE.txt "${AVS_PLUGINS_DIR}\RgTools\LICENSE"

    ; GamMac (GPLv2)
    File "${GPL_PLUGINS_DIR}\GamMac\GamMac_x64.dll"
    File /oname=GamMac_LICENSE.txt "${GPL_PLUGINS_DIR}\GamMac\LICENSE"

    ; MaskTools2 (GPLv2)
    File "${GPL_PLUGINS_DIR}\Masktools2\masktools2.dll"
    File /oname=Masktools2_LICENSE.txt "${GPL_PLUGINS_DIR}\Masktools2\LICENSE"

    ; RemoveDirt (GPLv2)
    File "${GPL_PLUGINS_DIR}\RemoveDirt\RemoveDirt.dll"
    File /oname=RemoveDirt_LICENSE.txt "${GPL_PLUGINS_DIR}\RemoveDirt\LICENSE"
    ; RemoveDirt script
    File "${GPL_PLUGINS_DIR}\Scripts\RemoveDirt.avsi"

    ; MVTools2 (GPLv2)
    File "${GPL_PLUGINS_DIR}\MVTools2\mvtools2.dll"
    File "${GPL_PLUGINS_DIR}\MVTools2\DePan.dll"
    File "${GPL_PLUGINS_DIR}\MVTools2\DePanEstimate.dll"
    File /oname=MVTools2_LICENSE.txt "${GPL_PLUGINS_DIR}\MVTools2\LICENSE"

    ; FFMS2 (MIT/GPL)
    File "${GPL_PLUGINS_DIR}\FFMS2\ffms2.dll"
    File /oname=FFMS2_LICENSE.txt "${GPL_PLUGINS_DIR}\FFMS2\LICENSE"
    ; FFMS2 script
    File "${GPL_PLUGINS_DIR}\Scripts\FFMS2.avsi"

SectionEnd

Section "Desktop shortcut" SecDesktop
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "Start Menu shortcut" SecStartMenu
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"  "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
    CreateShortCut  "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk"    "$INSTDIR\Uninstall.exe"
SectionEnd

; ── Section descriptions ──
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}       "Core application files, built-in filters and user guide (required)."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecGplPlugins} "AviSynth+ restoration plugins (LSMASHSource, RgTools, GamMac, MVTools2, MaskTools2, RemoveDirt, FFMS2). Installed into AviSynth+ plugins64+ folder."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop}    "Create a shortcut on the Desktop."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu}  "Create a shortcut in the Start Menu."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ─────────────────────────────────────────────────────
; Launch the app as the normal (non-elevated) user
; ─────────────────────────────────────────────────────
Function LaunchAsCurrentUser
    ; explorer.exe is a special Windows process that de-elevates automatically,
    ; so the launched app runs at medium integrity (normal user) — not admin.
    Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

; ─────────────────────────────────────────────────────
; INIT — check 64-bit + install AviSynth+
; ─────────────────────────────────────────────────────
Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "$(MSG_REQUIRES_X64)"
        Abort
    ${EndIf}

    ; ── Check if already installed ──
    ReadRegStr $0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString"
    ${If} $0 != ""
        MessageBox MB_YESNOCANCEL|MB_ICONQUESTION "$(MSG_ALREADY_INSTALLED)" \
            IDYES uninst_prev IDNO skip_uninst
        Abort

    uninst_prev:
        ; Run the existing uninstaller silently
        ExecWait '$0 /S' $1
        Sleep 1000

    skip_uninst:
    ${EndIf}

    ; ── Close running instance of AvyScan Lab ──
    FindWindow $0 "" "${APP_NAME}"
    ${If} $0 != 0
        MessageBox MB_OKCANCEL|MB_ICONINFORMATION "$(MSG_APP_RUNNING)" \
            IDOK close_app
        Abort

    close_app:
        ; Send WM_CLOSE to allow graceful shutdown
        SendMessage $0 ${WM_CLOSE} 0 0
        ; Wait up to 5 seconds for the process to exit
        Sleep 2000
        FindWindow $0 "" "${APP_NAME}"
        ${If} $0 != 0
            Sleep 3000
            FindWindow $0 "" "${APP_NAME}"
            ${If} $0 != 0
                ; Force kill if still running
                ExecWait 'taskkill /F /IM ${APP_EXE}'
                Sleep 1000
            ${EndIf}
        ${EndIf}
    ${EndIf}

    ; ── Check if AviSynth+ is already installed ──
    ; Method 1: registry (plugindir+ is reliably set by AviSynth+ installer)
    SetRegView 64
    ReadRegStr $0 HKLM "SOFTWARE\AviSynth" "plugindir+"
    SetRegView 32
    ${If} $0 != ""
        Goto avs_done
    ${EndIf}
    ; Method 2: default install directory exists (even x64 installs to Program Files (x86))
    ${If} ${FileExists} "$PROGRAMFILES32\AviSynth+\plugins64+\*.*"
        Goto avs_done
    ${EndIf}

    ; ── AviSynth+ not found — must install ──
    MessageBox MB_OKCANCEL|MB_ICONINFORMATION "$(MSG_AVS_REQUIRED)" \
        IDOK avs_install
    ; User clicked Cancel → abort the entire installer
    Abort

avs_install:
    ; Extract AviSynth+ installer to temp and run it
    SetOutPath "$TEMP"
    File "${AVS_INSTALLER}"
    ExecWait '"$TEMP\${AVS_INSTALLER}" /S' $1
    Delete "$TEMP\${AVS_INSTALLER}"

    ; Verify installation succeeded
    ${If} $1 != 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "$(MSG_AVS_INSTALL_FAILED)" \
            IDOK avs_done
        Abort
    ${EndIf}

avs_done:
FunctionEnd

; ─────────────────────────────────────────────────────
; UNINSTALLER
; ─────────────────────────────────────────────────────
Section "Uninstall"
    ; ── License deactivation reminder ──
    ; Each Lemon Squeezy license has a limited number of activation slots
    ; (2 per key, configured in the LS dashboard). If the user uninstalls
    ; without first clicking "Deactivate" in the app, the slot stays busy
    ; server-side and they lose one of their 2 slots every reinstall.
    ; This is a soft reminder — we cannot call the LS API from NSIS, so
    ; we rely on the user running the in-app "Deactivate" before continuing.
    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "$(MSG_LICENSE_DEACTIVATE)" \
        IDOK license_ack
    Abort

license_ack:
    ; Remove AvyScan Lab files (includes Plugins\ffmpeg, mpv, etc.)
    RMDir /r "$INSTDIR\Plugins"
    RMDir /r "$INSTDIR\Filters"
    RMDir /r "$INSTDIR\mpv"
    RMDir /r "$INSTDIR\Users Guide"
    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\ScriptMaster.en.avs"
    Delete "$INSTDIR\ScriptUser.avs"
    Delete "$INSTDIR\ScriptRender.avs"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir  "$INSTDIR"

    ; Remove AviSynth+ plugins installed by AvyScan Lab (64-bit registry)
    SetRegView 64
    ReadRegStr $AVS_PLUGINDIR HKLM "SOFTWARE\AviSynth" "plugindir+"
    SetRegView 32
    ${If} $AVS_PLUGINDIR != ""
        Delete "$AVS_PLUGINDIR\LSMASHSource.dll"
        Delete "$AVS_PLUGINDIR\LSMASHSource_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\RgTools.dll"
        Delete "$AVS_PLUGINDIR\RgTools_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\GamMac_x64.dll"
        Delete "$AVS_PLUGINDIR\GamMac_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\masktools2.dll"
        Delete "$AVS_PLUGINDIR\Masktools2_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\RemoveDirt.dll"
        Delete "$AVS_PLUGINDIR\RemoveDirt_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\RemoveDirt.avsi"
        Delete "$AVS_PLUGINDIR\mvtools2.dll"
        Delete "$AVS_PLUGINDIR\DePan.dll"
        Delete "$AVS_PLUGINDIR\DePanEstimate.dll"
        Delete "$AVS_PLUGINDIR\MVTools2_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\ffms2.dll"
        Delete "$AVS_PLUGINDIR\FFMS2_LICENSE.txt"
        Delete "$AVS_PLUGINDIR\FFMS2.avsi"
    ${EndIf}

    ; Remove shortcuts
    Delete "$DESKTOP\${APP_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${APP_NAME}"

    ; Remove AppData (generated configs, sessions, presets)
    RMDir /r "$APPDATA\AvyScanLab"

    ; Remove registry keys
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
