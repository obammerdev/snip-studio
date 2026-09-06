@echo off
cd /d "%~dp0"
if exist "app\SnipStudio.exe" (
    start "" "app\SnipStudio.exe"
) else (
    echo Run build.ps1 -Publish first, or open the project with the .NET 10 SDK.
    pause
)
