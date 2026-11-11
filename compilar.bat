@echo off
rem Genera publish\SenStrap.exe (un solo archivo, no necesita .NET instalado para ejecutarse)
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo No se encuentra .NET. Instala el SDK con:
    echo     winget install Microsoft.DotNet.SDK.10
    echo y vuelve a abrir esta ventana.
    pause
    exit /b 1
)

dotnet publish SenStrap\SenStrap.csproj -c Release -o publish
if errorlevel 1 (
    echo.
    echo ERROR: no se pudo compilar. Mira los mensajes de arriba.
    pause
    exit /b 1
)

echo.
echo Listo: %~dp0publish\SenStrap.exe
pause
