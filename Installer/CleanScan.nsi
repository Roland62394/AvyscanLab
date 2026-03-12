; ─────────────────────────────────────────────────────
; CleanScan — NSIS Installer Script
; ─────────────────────────────────────────────────────
; Prerequisites:
;   1. Install NSIS 3+ from https://nsis.sourceforge.io/Download
;   2. Build/publish the app:
;        dotnet publish -c Release -r win-x64 --self-contained
;   3. Place AviSynthPlus installer in Installer/ folder:
;        AviSynthPlus_3.7.5_20250420_vcredist.exe
;   4. Right-click this file → "Compile NSIS Script"
;      or run:  makensis CleanScan.nsi
;
; The installer will be created in Installer\Output\
; ─────────────────────────────────────────────────────

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

; ── App metadata ──
!define APP_NAME      "CleanScan"
!define APP_VERSION   "5.0 Beta"
!define APP_PUBLISHER "ScanFilm SNC"
!define APP_URL       "https://www.scanfilm.ch"
!define APP_EXE       "CleanScan.exe"
!define PUBLISH_DIR   "..\bin\Release\net10.0\win-x64\publish"
!define AVS_INSTALLER "AviSynthPlus_3.7.5_20250420_vcredist.exe"

; GPL plugin DLLs — installed into AviSynth+ plugins64+ folder
!define GPL_PLUGINS_DIR "${PUBLISH_DIR}\Plugins"

; ── Installer settings ──
Name "${APP_NAME} ${APP_VERSION}"
OutFile "Output\CleanScan_Setup.exe"
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
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
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

    ; Plugins — only MIT/ISC licensed plugins (RgTools, L-Smash-Works, Scripts)
    SetOutPath "$INSTDIR\Plugins\RgTools"
    File "${GPL_PLUGINS_DIR}\RgTools\RgTools.dll"
    File "${GPL_PLUGINS_DIR}\RgTools\LICENSE"
    SetOutPath "$INSTDIR\Plugins\Dual\L-Smash-Works"
    File /r "${GPL_PLUGINS_DIR}\Dual\L-Smash-Works\*.*"
    SetOutPath "$INSTDIR\Plugins\Scripts"
    File /r "${GPL_PLUGINS_DIR}\Scripts\*.*"

    ; mpv player library
    SetOutPath "$INSTDIR\mpv"
    File /r "${PUBLISH_DIR}\mpv\*.*"

    ; User guide
    SetOutPath "$INSTDIR\Users Guide"
    File /nonfatal /r "${PUBLISH_DIR}\Users Guide\*.*"

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

; ── GPL restoration plugins (installed into AviSynth+ folder) ──
Section "Restoration plugins (GPLv2 — required)" SecGplPlugins
    SectionIn RO  ; Cannot be deselected — required for CleanScan

    ; Read AviSynth+ plugin directory from registry
    ReadRegStr $AVS_PLUGINDIR HKLM "SOFTWARE\AviSynth" "plugindir+"
    ${If} $AVS_PLUGINDIR == ""
        ; Fallback: standard path
        StrCpy $AVS_PLUGINDIR "$PROGRAMFILES32\AviSynth+\plugins64+"
    ${EndIf}

    ; Install GPL plugin DLLs + their LICENSE files
    SetOutPath "$AVS_PLUGINDIR"

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
    !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}       "Core application files (required)."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecGplPlugins} "Film restoration plugins (GamMac, MVTools2, MaskTools2, RemoveDirt, FFMS2). Licensed under GPLv2 — source code links included."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop}    "Create a shortcut on the Desktop."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu}  "Create a shortcut in the Start Menu."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ─────────────────────────────────────────────────────
; INIT — check 64-bit + install AviSynth+
; ─────────────────────────────────────────────────────
Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires a 64-bit version of Windows."
        Abort
    ${EndIf}

    ; ── Check if AviSynth+ is already installed ──
    ReadRegStr $0 HKLM "SOFTWARE\AviSynth+" "Version"
    ${If} $0 != ""
        ; Already installed — skip
        Goto avs_done
    ${EndIf}

    ; ── AviSynth+ not found — must install ──
    MessageBox MB_OKCANCEL|MB_ICONINFORMATION \
        "${APP_NAME} requires AviSynth+ to function.$\n$\n\
        AviSynth+ (GPLv2) will now be installed.$\n\
        Click OK to proceed, or Cancel to exit." \
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
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
            "AviSynth+ installation may have failed (exit code: $1).$\n$\n\
            Click OK to continue anyway, or Cancel to exit." \
            IDOK avs_done
        Abort
    ${EndIf}

avs_done:
FunctionEnd

; ─────────────────────────────────────────────────────
; UNINSTALLER
; ─────────────────────────────────────────────────────
Section "Uninstall"
    ; Remove CleanScan files
    RMDir /r "$INSTDIR\Plugins"
    RMDir /r "$INSTDIR\mpv"
    RMDir /r "$INSTDIR\Users Guide"
    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\ScriptMaster.en.avs"
    Delete "$INSTDIR\ScriptUser.avs"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir  "$INSTDIR"

    ; Remove GPL plugins from AviSynth+ folder
    ReadRegStr $AVS_PLUGINDIR HKLM "SOFTWARE\AviSynth" "plugindir+"
    ${If} $AVS_PLUGINDIR != ""
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
    RMDir /r "$APPDATA\CleanScan"

    ; Remove registry keys
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
