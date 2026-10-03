@echo off
chcp 65001 >nul
title 간편 다운로더 - 관리자 서버

echo ===================================================
echo   [간편 다운로더] 관리자 웹 서버 실행 중...
echo ===================================================
echo.

cd /d "%~dp0server"
python server.py 5000

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [오류] 서버 실행 중 오류가 발생했습니다.
    pause
)
