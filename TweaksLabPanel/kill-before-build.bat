@echo off
rem Ne pas tuer pendant dotnet watch : watch arrete l'app proprement tout seul.
if "%DOTNET_WATCH%"=="1" exit /b 0
taskkill /IM TweaksLabPanel.exe /F >nul 2>&1
ping -n 2 127.0.0.1 >nul
exit /b 0
