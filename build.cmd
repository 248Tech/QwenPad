@echo off
rem Builds QwenPad.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Usage: build.cmd [output.exe]
setlocal
set OUT=%~1
if "%OUT%"=="" set OUT=%~dp0QwenPad.exe
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:"%~dp0src\QwenPad.ico" /out:"%OUT%" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  "%~dp0src\*.cs"
