@echo off
REM Frostbite Uninstaller Wrapper - Auto-elevate to admin
setlocal enabledelayedexpansion

REM Check if running as admin
net session >nul 2>&1
if %errorLevel% == 0 (
    REM Already running as admin; launch the PowerShell script
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0zz_uninstall_frostbite.ps1"
    
    REM Pause the window so the user can review, then naturally exit
    echo.
    pause
    exit /b %errorlevel%
) else (
    REM Not admin; request elevation by restarting THIS batch file as admin
    echo Requesting administrator privileges...
    powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" || (
        echo Failed to elevate. Please run as administrator.
        pause
        exit /b 1
    )
    exit /b 0
)