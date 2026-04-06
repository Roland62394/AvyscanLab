@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo  AvyScan Lab — Release Build + Obfuscation
echo ============================================================

:: 1. Clean previous build
echo.
echo [1/5] Clean...
dotnet clean -c Release -r win-x64 --nologo -v q
if errorlevel 1 goto :error

:: 2. Publish (SingleFile + ReadyToRun + Trimmed)
echo [2/5] Publish Release (SingleFile + R2R + Trimmed)...
dotnet publish -c Release -r win-x64 --self-contained --nologo
if errorlevel 1 goto :error

set PUBDIR=bin\Release\net10.0\win-x64\publish

:: 3. Obfuscate the main DLL before it gets bundled
echo [3/5] Obfuscation (Obfuscar)...

:: For SingleFile, the DLL is bundled. We need to obfuscate from the intermediate dir.
set ILDIR=obj\Release\net10.0\win-x64
if exist "%ILDIR%\AvyscanLab.dll" (
    pushd "%ILDIR%"
    obfuscar.console "%~dp0obfuscar.xml" 2>nul
    if exist "Obfuscated\AvyscanLab.dll" (
        copy /y "Obfuscated\AvyscanLab.dll" "AvyscanLab.dll" >nul
        echo    Obfuscation OK
    ) else (
        echo    WARNING: Obfuscation skipped (output not found)
    )
    popd
) else (
    echo    WARNING: DLL not found in %ILDIR%, skipping obfuscation
)

:: 4. Re-publish with obfuscated DLL
echo [4/5] Re-bundle SingleFile with obfuscated assembly...
dotnet publish -c Release -r win-x64 --self-contained --no-build --nologo 2>nul
if errorlevel 1 (
    echo    Re-publish with --no-build failed, doing full publish...
    dotnet publish -c Release -r win-x64 --self-contained --nologo
)

:: 5. Summary
echo.
echo [5/5] Done!
echo ============================================================
if exist "%PUBDIR%\AvyscanLab.exe" (
    for %%F in ("%PUBDIR%\AvyscanLab.exe") do echo  Output: %%~fF (%%~zF bytes)
) else (
    echo  Output directory: %PUBDIR%
)
echo ============================================================
echo.
pause
exit /b 0

:error
echo.
echo BUILD FAILED
pause
exit /b 1
