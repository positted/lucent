@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:exe /out:VibranceSmoke.Test.exe /main:VibranceSmoke /r:System.Windows.Forms.dll /r:System.Drawing.dll ResolutionSwitcher.cs Appearance.cs ModePicker.cs SettingsPage.cs Synthwave.cs Brand.cs SwitchExtras.cs Crosshair.cs Boost.cs Vibrance.cs DesktopVibrance.cs CustomResolution.cs ProcessIdentity.cs NvidiaScaling.cs RefreshRate.cs VibranceSmoke.cs
if errorlevel 1 exit /b 1
VibranceSmoke.Test.exe %*
exit /b %errorlevel%
