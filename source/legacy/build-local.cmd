@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:exe /optimize+ /out:IconMaker.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll IconMaker.cs Synthwave.cs Brand.cs
if errorlevel 1 exit /b 1
IconMaker.exe Lucent.ico
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:Lucent.ico /out:Lucent.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ResolutionSwitcher.cs Appearance.cs ModePicker.cs SettingsPage.cs Synthwave.cs Brand.cs SwitchExtras.cs Crosshair.cs Boost.cs Vibrance.cs DesktopVibrance.cs CustomResolution.cs ProcessIdentity.cs NvidiaScaling.cs RefreshRate.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:Lucent.ico /out:LucentSetup.exe /resource:Lucent.exe,Lucent.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll DisplaySwitchSetup.cs SetupScope.cs Synthwave.cs Brand.cs
if errorlevel 1 exit /b 1
del /q IconMaker.exe
echo Built Lucent.exe and LucentSetup.exe in this folder.
endlocal

