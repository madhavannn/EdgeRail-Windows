@echo off
setlocal
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%compiler%" (
  echo The .NET Framework compiler was not found.
  exit /b 1
)
if not exist "%~dp0dist" mkdir "%~dp0dist"
"%compiler%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /utf8output /win32manifest:"%~dp0app.manifest" /out:"%~dp0dist\EdgeRail.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll /reference:System.Xml.dll "%~dp0*.cs"
if errorlevel 1 exit /b 1
copy /y "%~dp0EdgeRail.exe.config" "%~dp0dist\EdgeRail.exe.config" >nul
echo Built %~dp0dist\EdgeRail.exe
endlocal
