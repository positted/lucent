@echo off
setlocal
set "FRAME=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%FRAME%\csc.exe"
if not exist "%CSC%" (echo A 64-bit .NET Framework compiler is required for Windows Magnification. & exit /b 1)
if not exist ..\release mkdir ..\release
"%CSC%" /nologo /target:exe /out:..\release\IconMaker.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll legacy\IconMaker.cs legacy\Synthwave.cs Brand.cs CrosshairModel.cs legacy\ResolutionSwitcher.cs legacy\Appearance.cs legacy\ModePicker.cs legacy\SettingsPage.cs legacy\SwitchExtras.cs legacy\Crosshair.cs legacy\Boost.cs legacy\Vibrance.cs legacy\DesktopVibrance.cs legacy\CustomResolution.cs legacy\ProcessIdentity.cs legacy\NvidiaScaling.cs legacy\RefreshRate.cs AdvancedBoost.cs LegacyNames.cs /main:IconMaker
if errorlevel 1 exit /b 1
..\release\IconMaker.exe ..\release\Lucent.ico
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /main:Lucent.Entry /out:..\release\Lucent.exe /win32icon:..\release\Lucent.ico /win32manifest:lucent.manifest /r:"%FRAME%\System.Runtime.dll" /r:"%FRAME%\System.Runtime.InteropServices.WindowsRuntime.dll" /r:"%WINDIR%\System32\WinMetadata\Windows.Media.winmd" /r:"%WINDIR%\System32\WinMetadata\Windows.Foundation.winmd" /r:"%WINDIR%\System32\WinMetadata\Windows.Storage.winmd" /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Web.Extensions.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:"%FRAME%\WPF\WindowsBase.dll" /r:"%FRAME%\WPF\PresentationCore.dll" /r:"%FRAME%\WPF\PresentationFramework.dll" /r:"%FRAME%\System.Xaml.dll" /r:lib\SharpDX.dll /r:lib\SharpDX.DXGI.dll /r:lib\SharpDX.Direct3D11.dll /r:lib\SharpDX.D3DCompiler.dll AssemblyInfo.cs App.cs ShellHome.cs ShellVideo.cs ShellEyeCare.cs ShellTray.cs Updates.cs LegacyNames.cs ZoomWheel.cs SystemStats.cs ShellStats.cs ShellDiscord.cs DiscordRpc.cs ArtistPictures.cs ShellUpdates.cs ShellResolution.cs ShellProfiles.cs GameLibrary.cs ShellCrosshair.cs ShellMemory.cs ShellVisualTools.cs ShellShortcuts.cs ShellEffects.cs ShellBackgrounds.cs DesignSystem.cs ShellChrome.cs ShellMotion.cs ShellSettings.cs AmbientBackdrop.cs ShellCapture.cs ShellBoost.cs GpuZoom.cs SoundRadar.cs ZoomColorCompensation.cs MediaSession.cs PlayerCore.cs MediaPlayerWindow.cs BigPlayerView.cs LyricsView.cs SystemVolume.cs AppVolume.cs VolumeControl.cs FlowingBackdrop.cs BigPlayerWindow.cs PlayerBar.cs BarBackdrop.cs SeekBar.cs WindowEdges.cs WindowGhost.cs CrosshairColorField.cs State.cs Effects.cs Tools.cs AudioLimiter.cs NativeVibranceBridge.cs Art.cs CrosshairModel.cs AnimatedBackground.cs Brand.cs legacy\ResolutionSwitcher.cs legacy\Appearance.cs legacy\ModePicker.cs legacy\SettingsPage.cs legacy\Synthwave.cs legacy\SwitchExtras.cs legacy\Crosshair.cs legacy\Boost.cs legacy\Vibrance.cs legacy\DesktopVibrance.cs legacy\CustomResolution.cs legacy\ProcessIdentity.cs legacy\NvidiaScaling.cs legacy\RefreshRate.cs AdvancedBoost.cs
if errorlevel 1 exit /b 1
if not exist ..\release\fonts mkdir ..\release\fonts
copy /y ..\assets\fonts\*.ttf ..\release\fonts\ >nul
copy /y ..\assets\fonts\LICENSE.txt ..\release\fonts\LICENSE.txt >nul
if errorlevel 1 exit /b 1
echo Built ..\release\Lucent.exe locally. No installation or old release changed.

copy /y lib\*.dll ..\release\ >nul

copy /y lib\SharpDX-LICENSE.txt ..\release\SharpDX-LICENSE.txt >nul
