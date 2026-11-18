@echo off
chcp 65001 >nul
echo [OhMyCode Status Indicator] Сборка из исходников C#...

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Ошибка: csc.exe не найден по пути %CSC%
    pause
    exit /b 1
)

set "NET_DIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
set "WPF_DIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

"%CSC%" /target:winexe /out:"%~dp0bin\OhMyCodeStatusIndicator.exe" ^
  /r:"%NET_DIR%\System.dll" ^
  /r:"%NET_DIR%\System.Core.dll" ^
  /r:"%NET_DIR%\System.Drawing.dll" ^
  /r:"%NET_DIR%\System.Web.Extensions.dll" ^
  /r:"%NET_DIR%\System.Xaml.dll" ^
  /r:"%WPF_DIR%\WindowsBase.dll" ^
  /r:"%WPF_DIR%\PresentationCore.dll" ^
  /r:"%WPF_DIR%\PresentationFramework.dll" ^
  "%~dp0bin\OhMyCodeStatusIndicator.cs"

if %ERRORLEVEL% equ 0 (
    echo Сборка успешна: %~dp0bin\OhMyCodeStatusIndicator.exe
) else (
    echo Ошибка сборки!
)
pause
