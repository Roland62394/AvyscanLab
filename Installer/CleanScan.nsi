; ─────────────────────────────────────────────────────
; CleanScan — NSIS Installer Script
; ─────────────────────────────────────────────────────
; Prerequisites:
;   1. Install NSIS 3+ from https://nsis.sourceforge.io/Download
;   2. Build/publish the app:
;        dotnet publish -c Release -r win-x64 --self-contained
;   3. Right-click this file → "Compile NSIS Script"
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
    
    ; Plugins
    SetOutPath "$INSTDIR\Plugins"
    File /r "${PUBLISH_DIR}\Plugins\*.*"

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
    !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}      "Core application files (required)."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop}   "Create a shortcut on the Desktop."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "Create a shortcut in the Start Menu."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ─────────────────────────────────────────────────────
; INIT — check 64-bit
; ─────────────────────────────────────────────────────
Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires a 64-bit version of Windows."
        Abort
    ${EndIf}
FunctionEnd

; ─────────────────────────────────────────────────────
; UNINSTALLER
; ─────────────────────────────────────────────────────
Section "Uninstall"
    ; Remove files
    RMDir /r "$INSTDIR\Plugins"
    RMDir /r "$INSTDIR\mpv"
    RMDir /r "$INSTDIR\Users Guide"
    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\ScriptMaster.en.avs"
    Delete "$INSTDIR\ScriptUser.avs"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir  "$INSTDIR"

    ; Remove shortcuts
    Delete "$DESKTOP\${APP_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${APP_NAME}"

    ; Remove AppData (generated configs, sessions, presets)
    RMDir /r "$APPDATA\CleanScan"

    ; Remove registry keys
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
