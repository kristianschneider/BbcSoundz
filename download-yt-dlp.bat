@echo off
echo Downloading yt-dlp.exe...
echo.

REM Check if curl is available
curl --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: curl is not available. Please download yt-dlp.exe manually from:
    echo https://github.com/yt-dlp/yt-dlp/releases/latest
    echo.
    echo Download the yt-dlp.exe file and place it in the same directory as BbcSoundz.exe
    pause
    exit /b 1
)

REM Download yt-dlp.exe
curl -L -o yt-dlp.exe https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe

if %errorlevel% eq 0 (
    echo.
    echo yt-dlp.exe downloaded successfully!
    echo You can now use the download functionality in BBC Soundz.
) else (
    echo.
    echo Error downloading yt-dlp.exe
    echo Please download it manually from:
    echo https://github.com/yt-dlp/yt-dlp/releases/latest
)

echo.
pause
