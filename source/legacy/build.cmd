@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:exe /optimize+ /out:IconMaker.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll IconMaker.cs Synthwave.cs
if errorlevel 1 exit /b 1
IconMaker.exe Lucent.ico
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:Lucent.ico /out:Lucent.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ResolutionSwitcher.cs Synthwave.cs SwitchExtras.cs Crosshair.cs Boost.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:Lucent.ico /out:LucentSetup.exe /resource:Lucent.exe,Lucent.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll DisplaySwitchSetup.cs SetupScope.cs Synthwave.cs
if errorlevel 1 exit /b 1
del /q IconMaker.exe
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $destination=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Lucent'; New-Item -ItemType Directory -Path $destination -Force | Out-Null; Copy-Item -LiteralPath '.\Lucent.exe' -Destination (Join-Path $destination 'Lucent.exe') -Force; Copy-Item -LiteralPath '.\LucentSetup.exe' -Destination (Join-Path $destination 'LucentSetup.exe') -Force"
if errorlevel 1 exit /b 1
endlocal
