@echo off
setlocal EnableDelayedExpansion
title Converze for Windows — Real Installer Generator (Program Files + Uninstaller)

echo =========================================================
echo   Converze for Windows — Creare Installer Oficial (.exe)
echo =========================================================
echo.

echo [1/4] Compilare aplicatie principala + Uninstaller...
call build_windows.bat --no-prompt
if !ERRORLEVEL! neq 0 (
    echo [EROARE] Nu s-a putut compila aplicatia principala.
    pause
    exit /b 1
)

echo [2/4] Impachetare payload aplicatie pentru Setup Wizard...
if exist "dist\payload.zip" del /f /q "dist\payload.zip"
powershell -NoProfile -Command "Compress-Archive -Path 'dist\Converze\*' -DestinationPath 'dist\payload.zip' -Force"

if not exist "dist\payload.zip" (
    echo [EROARE] Nu s-a putut genera dist\payload.zip.
    pause
    exit /b 1
)

echo [3/4] Compilare Installer Grafic (dist\Converze-Setup.exe — instaleaza in C:\Program Files\Converze)...
dotnet publish Setup\ConverzeSetup.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -o "dist\SetupOut"

if !ERRORLEVEL! neq 0 (
    echo [EROARE] Compilarea Converze-Setup.exe a esuat.
    pause
    exit /b 1
)

copy /y "dist\SetupOut\Converze-Setup.exe" "dist\Converze-Setup.exe" >nul
rmdir /s /q "dist\SetupOut"
del /f /q "dist\payload.zip"

echo [4/4] Creare arhiva distribuibila dist\Converze-Windows-Installer.zip...
powershell -NoProfile -Command "Compress-Archive -Path 'dist\Converze-Setup.exe' -DestinationPath 'dist\Converze-Windows-Installer.zip' -Force"

echo.
echo =========================================================
echo   INSTALLER OFICIAL GENERAT CU SUCCES!
echo   - Installer Grafic (.exe): dist\Converze-Setup.exe
echo     (Instaleaza in C:\Program Files\Converze + Uninstaller in Control Panel)
echo   - Arhiva ZIP cu Installer: dist\Converze-Windows-Installer.zip
echo =========================================================
echo.
set /p RUNSETUP="Vrei sa rulezi installer-ul Converze-Setup.exe acum? (D/N): "
if /i "!RUNSETUP!"=="D" (
    start "" "dist\Converze-Setup.exe"
)
if /i "!RUNSETUP!"=="Y" (
    start "" "dist\Converze-Setup.exe"
)
endlocal
