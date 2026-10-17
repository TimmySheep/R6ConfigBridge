@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%CSC%" /nologo /utf8output /target:exe /out:Tests.exe Tests.cs Core.cs
if errorlevel 1 exit /b 1
Tests.exe
if errorlevel 1 exit /b 1
"%CSC%" /nologo /utf8output /target:winexe /win32icon:assets\app.ico /platform:x64 /out:R6ConfigBridge.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll App.cs Core.cs
if errorlevel 1 exit /b 1
exit /b 0
