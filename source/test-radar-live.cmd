@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
rem Runs the real sound radar window on a display that is not the primary one, with fixed levels instead of the speakers. Skips when there is only one display.
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /win32manifest:lucent.manifest /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ..\tests\RadarLiveSmoke.cs
if errorlevel 1 exit /b 1
pushd ..\release
.\lucent.tests.exe
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%