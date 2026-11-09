@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
rem Resizes the real main window on a display that is not the primary one and measures what each step costs, with parts switched off one at a time. Skips with one display.
rem Usage: test-glass-live.cmd [folder for the pictures]
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /win32manifest:lucent.manifest /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll /r:"%FRAME%\WPF\WindowsBase.dll" /r:"%FRAME%\WPF\PresentationCore.dll" /r:"%FRAME%\WPF\PresentationFramework.dll" ..\tests\ResizeLiveBench.cs
if errorlevel 1 exit /b 1
pushd ..\release
.\lucent.tests.exe %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
