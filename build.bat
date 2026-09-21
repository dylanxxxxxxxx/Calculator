@echo off
set CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe
set FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FX%\WPF
if not exist "%~dp0calc.ico" (
  "%CSC%" /nologo /langversion:latest /target:exe /optimize+ /lib:"%FX%" /r:System.Drawing.dll /out:"%~dp0MakeIcon.exe" "%~dp0MakeIcon.cs"
  if errorlevel 1 exit /b 1
  "%~dp0MakeIcon.exe" "%~dp0calc.ico"
  if errorlevel 1 exit /b 1
)
"%CSC%" /nologo /langversion:latest /codepage:65001 /target:winexe /optimize+ /lib:"%FX%" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\WindowsBase.dll" /r:"%FX%\System.Xaml.dll" /win32icon:"%~dp0calc.ico" /out:"D:\Users\Administrator\Desktop\轻量计算器.exe" "%~dp0CalcEngine.cs" "%~dp0App.cs"
if errorlevel 1 exit /b 1
echo built D:\Users\Administrator\Desktop\轻量计算器.exe
