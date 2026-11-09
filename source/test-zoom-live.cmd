@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
rem Runs the real zoom overlay on a display that is not the primary one, over a test picture. Skips when there is only one display.
rem It never touches the primary display or the pointer. Built with the app's manifest so display sizes are the real ones.
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /win32manifest:lucent.manifest /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ..\tests\ZoomLiveSmoke.cs
if errorlevel 1 exit /b 1
pushd ..\release
.\lucent.tests.exe
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%