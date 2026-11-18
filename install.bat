@echo off
chcp 65001 >nul
echo [OhMyCode Status Indicator] Установка и запуск...

set "TARGET_DIR=%APPDATA%\ru.ohmylama.ohmycode\data\opencode\storage\oh-my-opencode-slim\bin"
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

copy /Y "%~dp0bin\OhMyCodeStatusIndicator.exe" "%TARGET_DIR%\OhMyCodeStatusIndicator.exe" >nul
copy /Y "%~dp0bin\OhMyCodeStatusIndicator.cs" "%TARGET_DIR%\OhMyCodeStatusIndicator.cs" >nul

echo [OhMyCode Status Indicator] Добавление в автозапуск Windows...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'OhMyCodeStatusIndicator' -Value ('\"' + $env:APPDATA + '\ru.ohmylama.ohmycode\data\opencode\storage\oh-my-opencode-slim\bin\OhMyCodeStatusIndicator.exe\"')"

echo [OhMyCode Status Indicator] Запуск виджета...
taskkill /F /IM OhMyCodeStatusIndicator.exe 2>nul
start "" "%TARGET_DIR%\OhMyCodeStatusIndicator.exe"

echo [OhMyCode Status Indicator] Готово! Виджет запущен и добавлен в автозагрузку.
pause
