@echo off
setlocal EnableDelayedExpansion
title Converze for Windows — Build Script

echo =========================================================
echo   Converze for Windows (v2.2.0) — Standalone Build
echo =========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [EROARE] .NET 8 SDK nu a fost gasit!
    echo Descarca .NET 8 SDK gratuit de la: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/4] Curatare director dist\...
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

echo [2/4] Compilare Converze.exe (Self-Contained Windows x64)...
dotnet publish Converze.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -o "dist\Converze"

if %ERRORLEVEL% neq 0 (
    echo [EROARE] Compilarea a esuat. Verifica mesajele de mai sus.
    pause
    exit /b 1
)

echo [3/4] Creare Uninstaller (Uninstall-Converze.exe) si resurse iconita...
copy /y "dist\Converze\Converze.exe" "dist\Converze\Uninstall-Converze.exe" >nul
if not exist "dist\Converze\Resources" mkdir "dist\Converze\Resources"
copy /y "Resources\AppIcon.png" "dist\Converze\Resources\AppIcon.png" >nul
copy /y "Resources\AppIcon.ico" "dist\Converze\Resources\AppIcon.ico" >nul

echo [4/4] Generare semnatura unica pentru afisarea obligatorie a Welcome Screen la prima pornire...
set INSTALL_SIG=converze-build-%RANDOM%-%DATE:/=-%-%TIME::=-%
echo !INSTALL_SIG!> "dist\Converze\install_signature.txt"

echo.
echo =========================================================
echo   BUILD FINALIZAT CU SUCCES!
echo   Executabil:  dist\Converze\Converze.exe
echo   Uninstaller: dist\Converze\Uninstall-Converze.exe
echo =========================================================
echo.
if /i "%~1"=="--no-prompt" exit /b 0
set /p RUNNOW="Vrei sa pornesti Converze acum? (D/N): "
if /i "!RUNNOW!"=="D" (
    start "" "dist\Converze\Converze.exe"
)
if /i "!RUNNOW!"=="Y" (
    start "" "dist\Converze\Converze.exe"
)
endlocal
