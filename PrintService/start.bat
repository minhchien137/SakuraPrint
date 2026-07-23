@echo off
setlocal

cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Node.js chua duoc cai. Tai va cai tai https://nodejs.org roi chay lai file nay.
    pause
    exit /b 1
)

if not exist "node_modules" (
    echo Dang cai dependencies lan dau...
    call npm install
)

echo.
echo ==============================================
echo   ZPL Print Service - dang khoi dong...
echo   Dung cua so nay lai de bridge tiep tuc chay.
echo ==============================================
echo.

node server.js

echo.
echo [Service da dung. Nhan phim bat ky de dong cua so.]
pause
