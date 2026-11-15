@echo off
setlocal
set "phframework=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%phframework%\csc.exe" set "phframework=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "phbuilder=%TEMP%\PackHelper-builder-%RANDOM%.exe"
"%phframework%\csc.exe" /nologo /target:winexe /codepage:65001 /r:System.Xaml.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:"%phframework%\WPF\PresentationCore.dll" /r:"%phframework%\WPF\PresentationFramework.dll" /r:"%phframework%\WPF\WindowsBase.dll" /out:"%phbuilder%" "%~dp0tools\ReleaseBuild.cs" "%~dp0tools\ReleaseWindow.cs"
if errorlevel 1 (pause & exit /b 1)
start "" "%phbuilder%" "%~dp0."
