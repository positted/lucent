@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
rem Software-rendered check of the zoom shader. It draws to a texture and reads it back; it never captures or changes a screen.
"%FRAME%\csc.exe" /nologo /target:exe /platform:x64 /out:..\release\lucent.tests.exe /r:..\release\Lucent.exe /r:lib\SharpDX.dll /r:lib\SharpDX.DXGI.dll /r:lib\SharpDX.Direct3D11.dll /r:lib\SharpDX.D3DCompiler.dll /r:System.dll /r:System.Core.dll ..\tests\ZoomShaderTests.cs
if errorlevel 1 exit /b 1
pushd ..\release
.\lucent.tests.exe
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%