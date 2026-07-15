@echo off
chcp 65001 >nul
title TweaksLab Panel - Raccourci bureau
setlocal

set "ROOT=%~dp0"
set "PROJ=%ROOT%TweaksLabPanel"
set "EXE=%PROJ%\bin\Debug\net8.0-windows\TweaksLabPanel.exe"
set "ICO=%PROJ%\Assets\logo.ico"

cd /d "%PROJ%"
echo.
echo  Creation du raccourci bureau...
echo.

dotnet build -v q >nul
if errorlevel 1 (
    echo  ERREUR : compilation echouee.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo  ERREUR : executable introuvable.
    echo  %EXE%
    pause
    exit /b 1
)

if not exist "%ICO%" (
    echo  ERREUR : icone introuvable.
    echo  %ICO%
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop') + '\TweaksLab Panel.lnk');" ^
  "$s.TargetPath = '%EXE%';" ^
  "$s.WorkingDirectory = '%PROJ%\bin\Debug\net8.0-windows';" ^
  "$s.IconLocation = '%ICO%,0';" ^
  "$s.Description = 'TweaksLab Panel';" ^
  "$s.Save();" ^
  "Write-Host '  Raccourci cree sur le bureau : TweaksLab Panel.lnk'"

echo.
echo  Termine.
echo.
pause
endlocal
