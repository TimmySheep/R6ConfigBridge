@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%CSC%" /nologo /utf8output /target:exe /out:Tests.exe Tests.cs Core.cs
if errorlevel 1 exit /b 1
Tests.exe
if errorlevel 1 exit /b 1
"%CSC%" /nologo /utf8output /target:exe /main:UiTests /out:UiTests.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll UiTests.cs App.cs Core.cs UiText.cs Discovery.cs
if errorlevel 1 exit /b 1
UiTests.exe
if errorlevel 1 exit /b 1
if "%R6_OUTPUT%"=="" set "R6_OUTPUT=R6ConfigBridge.exe"
"%CSC%" /nologo /utf8output /target:winexe /win32icon:assets\app.ico /platform:x64 /out:"%R6_OUTPUT%" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll App.cs Core.cs UiText.cs Discovery.cs
if errorlevel 1 exit /b 1
exit /b 0
