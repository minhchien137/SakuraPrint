@echo off
setlocal EnableDelayedExpansion

:: ============================================================================
::  ZPL Print Service - Cai dat TOAN BO chi bang 1 file .bat nay
::  - Tu xin quyen Administrator neu chua co
::  - Tu cai Node.js neu may chua co
::  - Tu cai npm dependencies
::  - Tu tai NSSM va dang ky Windows Service (tu khoi dong cung Windows)
::  - Tu start service luon
::
::  File nay chi dung ky tu ASCII thuan tuy (khong dau, khong ky tu ve khung)
::  de chay dung tren moi bang ma he thong (locale) cua Windows, ke ca
::  Windows tieng Trung (GBK / codepage 936).
:: ============================================================================

:: -- 0. Tu nang quyen Administrator neu can --------------------------------
set "SCRIPT_PATH=%~f0"
set "SCRIPT_DIR=%~dp0"

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Dang xin quyen Administrator...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath $env:SCRIPT_PATH -WorkingDirectory $env:SCRIPT_DIR -Verb RunAs"
    if errorlevel 1 (
        echo.
        echo [LOI] Khong the xin quyen Administrator. Vui long chuot phai vao file
        echo INSTALL_ALL.bat va chon "Run as administrator" roi thu lai.
        pause
    )
    exit /b
)

cd /d "%~dp0"

echo.
echo ==============================================================
echo   ZPL PRINT SERVICE - CAI DAT TOAN BO
echo ==============================================================
echo.

:: -- 1. Kiem tra / cai Node.js ----------------------------------------------
where node >nul 2>nul
if errorlevel 1 (
    echo [1/3] Node.js chua co - dang cai dat tu dong...

    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-node.ps1"
    if errorlevel 1 (
        echo.
        echo [LOI] Cai Node.js tu dong khong thanh cong.
        echo Vui long tu tai va cai tai https://nodejs.org roi chay lai file nay.
        pause
        exit /b 1
    )

    call :RefreshPath

    where node >nul 2>nul
    if errorlevel 1 (
        echo.
        echo [LOI] Node.js co ve da cai xong nhung Command Prompt nay chua nhan ra.
        echo Vui long dong cua so nay, mo lai Command Prompt MOI roi chay lai file nay.
        pause
        exit /b 1
    )
    echo        Node.js da cai xong:
    node -v
) else (
    echo [1/3] Node.js da co san:
    node -v
)

echo.

:: -- 2. Cai npm dependencies -------------------------------------------------
echo [2/3] Dang cai dependencies (express, cors...)...
call npm install
if errorlevel 1 (
    echo [LOI] npm install that bai. Kiem tra ket noi mang roi chay lai.
    pause
    exit /b 1
)

echo.

:: -- 3. Cai + start Windows Service (NSSM) ----------------------------------
echo [3/3] Dang cai dat Windows Service (tu khoi dong cung Windows)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-service.ps1"
if errorlevel 1 (
    echo [LOI] Cai Windows Service that bai. Xem log loi ben tren.
    pause
    exit /b 1
)

echo.
echo ==============================================================
echo   HOAN TAT!
echo   Service dang chay nen, tu khoi dong lai cung Windows.
echo   Kiem tra: http://localhost:8021/health
echo ==============================================================
echo.
pause
exit /b 0

:: ============================================================================
::  RefreshPath - nap lai bien PATH cua phien Command Prompt hien tai tu
::  registry (Machine + User), vi winget/MSI khong tu cap nhat PATH cho
::  cua so console dang mo san. Dung reg.exe (co san tren moi Windows,
::  khong phu thuoc PowerShell) de tranh loi encoding.
:: ============================================================================
:RefreshPath
set "MACHINE_PATH="
set "USER_PATH="

if exist "%ProgramFiles%\nodejs\node.exe" set "PATH=%PATH%;%ProgramFiles%\nodejs\"

for /f "tokens=2,*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "MACHINE_PATH=%%B"
call set "MACHINE_PATH=%MACHINE_PATH%"
if defined MACHINE_PATH set "PATH=%PATH%;%MACHINE_PATH%"

for /f "tokens=2,*" %%A in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USER_PATH=%%B"
call set "USER_PATH=%USER_PATH%"
if defined USER_PATH set "PATH=%PATH%;%USER_PATH%"

exit /b 0
