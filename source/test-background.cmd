@echo off
setlocal
cd /d "%~dp0"
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist ..\release mkdir ..\release
"%FRAME%\csc.exe" /nologo /target:exe /out:..\release\lucent.background-tests.exe /r:System.Drawing.dll /r:System.Core.dll /r:System.Web.Extensions.dll /r:"%FRAME%\System.Xaml.dll" /r:"%FRAME%\WPF\WindowsBase.dll" /r:"%FRAME%\WPF\PresentationCore.dll" /r:"%FRAME%\WPF\PresentationFramework.dll" AnimatedBackground.cs DesignSystem.cs Updates.cs ..\tests\AnimatedBackgroundTests.cs
if errorlevel 1 exit /b 1
..\release\lucent.background-tests.exe
exit /b %errorlevel%
