@echo off
rem Builds publish\SenStrap.exe (a single file that doesn't need .NET installed to run)
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET was not found. Install the SDK with:
    echo     winget install Microsoft.DotNet.SDK.10
    echo then open this window again.
    pause
    exit /b 1
)

dotnet publish SenStrap\SenStrap.csproj -c Release -o publish
if errorlevel 1 (
    echo.
    echo ERROR: the build failed. Check the messages above.
    pause
    exit /b 1
)

echo.
echo Done: %~dp0publish\SenStrap.exe
pause
