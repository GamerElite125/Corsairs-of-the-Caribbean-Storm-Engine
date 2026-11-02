@echo off
rem Builds "Corsairs of the Caribbean.exe" into this folder with the .NET Framework compiler built into Windows.
rem Copy the exe, launcher_cotc.ini and the launcher_assets folder into your game folder to use it.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set ICON=
if exist "%~dp0cotc.ico" set ICON=/win32icon:"%~dp0cotc.ico"
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu %ICON% /out:"%~dp0Corsairs of the Caribbean.exe" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll ^
  /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" /r:"%FW%\System.Web.Extensions.dll" ^
  "%~dp0CotcLauncher.cs"
exit /b %ERRORLEVEL%
