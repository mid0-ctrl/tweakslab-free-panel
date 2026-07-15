@echo off
chcp 65001 >nul
title TweaksLab Panel
setlocal EnableExtensions

set "ROOT=%~dp0"
set "PROJ=%ROOT%TweaksLabPanel"
set "WORKDIR=%PROJ%\bin\Debug\net8.0-windows"
set "EXE=%WORKDIR%\TweaksLabPanel.exe"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"

cd /d "%PROJ%" 2>nul
if errorlevel 1 (
    echo.
    echo  ERREUR : dossier projet introuvable.
    echo  %PROJ%
    goto :fin
)

echo.
echo  ========================================
echo   TweaksLab Panel
echo  ========================================
echo.

if exist "%DOTNET%" (
    set "DOTNET_CMD=%DOTNET%"
) else (
    set "DOTNET_CMD=dotnet"
)

tasklist /FI "IMAGENAME eq TweaksLabPanel.exe" 2>nul | find /I "TweaksLabPanel.exe" >nul
if not errorlevel 1 (
    echo  Fermeture de l'ancienne instance...
    taskkill /IM TweaksLabPanel.exe /F >nul 2>&1
    ping -n 2 127.0.0.1 >nul
)

echo  Compilation...
"%DOTNET_CMD%" build -c Debug -v minimal
if errorlevel 1 (
    echo.
    echo  ERREUR de compilation.
    goto :fin
)

if not exist "%EXE%" (
    echo.
    echo  ERREUR : executable introuvable.
    echo  %EXE%
    goto :fin
)

echo.
echo  Lancement...
start "TweaksLab Panel" /D "%WORKDIR%" "%EXE%"

ping -n 4 127.0.0.1 >nul
tasklist /FI "IMAGENAME eq TweaksLabPanel.exe" 2>nul | find /I "TweaksLabPanel.exe" >nul
if errorlevel 1 (
    echo.
    echo  ERREUR : le panel ne demarre pas.
    echo  Log : %LOCALAPPDATA%\TweaksLabPanel\logs\startup.log
    goto :fin
)

echo  OK — panel ouvert. Regarde la barre des taches.

:fin
echo.
pause
endlocal
