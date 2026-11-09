@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /out:..\release\lucent.ramfixture.exe ..\tests\RamFixture.cs
if errorlevel 1 exit /b 1
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:"%FRAME%\WPF\WindowsBase.dll" /r:"%FRAME%\WPF\PresentationCore.dll" /r:"%FRAME%\WPF\PresentationFramework.dll" ..\tests\LogicTests.cs
if errorlevel 1 exit /b 1
..\release\lucent.tests.exe
exit /b %errorlevel%
