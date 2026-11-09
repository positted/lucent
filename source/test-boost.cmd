@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
"%FRAME%\csc.exe" /nologo /platform:x64 /out:..\release\lucent.boostfixtureA.exe ..\tests\RamFixture.cs
if errorlevel 1 exit /b 1
copy /y ..\release\lucent.boostfixtureA.exe ..\release\lucent.boostfixtureB.exe >nul
"%FRAME%\csc.exe" /nologo /target:winexe /platform:x64 /out:..\release\lucent.boostwindow.exe /r:System.Windows.Forms.dll ..\tests\WindowFixture.cs
if errorlevel 1 exit /b 1
"%FRAME%\csc.exe" /nologo /platform:x64 /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ..\tests\BoostSafetyTests.cs
if errorlevel 1 exit /b 1
pushd ..\release
.\lucent.tests.exe
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
