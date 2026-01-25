@echo off
echo ========================================
echo Starting MQTT Broker for Marathon
echo ========================================
echo.

REM Check if Mosquitto is installed
where mosquitto >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: Mosquitto is not installed or not in PATH
    echo.
    echo Please install Mosquitto:
    echo   1. Download from: https://mosquitto.org/download/
    echo   2. Or use Chocolatey: choco install mosquitto
    echo.
    pause
    exit /b 1
)

echo Starting Mosquitto MQTT Broker...
echo Broker will run on localhost:1883
echo Press Ctrl+C to stop the broker
echo.
echo ========================================
echo.

REM Start Mosquitto with verbose logging
mosquitto -v

pause
