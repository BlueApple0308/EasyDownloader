@echo off
cd /d "%~dp0"
echo ====================================================
echo  Building EasyDownloader v2 (Drag and Drop Installer)
echo ====================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] csc.exe not found.
    pause
    exit /b 1
)

if not exist "dist" mkdir "dist"
taskkill /F /IM EasyDownloader.exe >nul 2>&1

"%CSC%" /target:winexe /optimize+ /platform:anycpu /win32manifest:src\app.manifest /win32icon:src\app.ico /res:src\app.ico,EasyDownloader.app.ico /out:dist\EasyDownloader.exe /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Web.Extensions.dll src\*.cs

if errorlevel 1 goto error
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign.ps1"
if errorlevel 1 goto error
powershell -Command "Unblock-File -Path 'dist\EasyDownloader.exe' -ErrorAction SilentlyContinue"
echo.
echo ====================================================
echo  [SUCCESS] dist\EasyDownloader.exe built!
for %%I in (dist\EasyDownloader.exe) do echo  Size: %%~zI bytes (Discord limit: 8,388,608 bytes)
echo ====================================================
goto end

:error
echo.
echo [ERROR] Compilation failed!
pause
exit /b 1

:end
