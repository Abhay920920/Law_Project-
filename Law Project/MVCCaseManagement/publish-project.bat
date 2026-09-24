@echo off
setlocal enabledelayedexpansion

echo =================================================================
echo   Publishing MVCCaseManagement (Production Ready)
echo   Target: net9.0 ^| Configuration: Release
echo   Profile: Properties\PublishProfiles\FolderProfile.pubxml
echo =================================================================

:: Ensure we are in the project directory
cd /d "%~dp0"

echo [1/3] Running automated unit tests...
dotnet test "..\MVCCaseManagement.Tests\MVCCaseManagement.Tests.csproj" -c Release --nologo
if %errorlevel% neq 0 (
    echo.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    echo   TESTS FAILED. Aborting publish to protect production.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/3] Publishing web application to Release package...
dotnet publish MVCCaseManagement.csproj /p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml -c Release --nologo

if %errorlevel% neq 0 (
    echo.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    echo   PUBLISH FAILED. Check the errors above.
    echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    pause
    exit /b %errorlevel%
)

echo.
echo [3/3] Setting up deployment directories...
if not exist "..\Publish_Output\logs" mkdir "..\Publish_Output\logs"
if not exist "..\Publish_Output\uploads" mkdir "..\Publish_Output\uploads"
if not exist "..\Publish_Output\wwwroot\uploads" mkdir "..\Publish_Output\wwwroot\uploads"

echo.
echo =================================================================
echo   SUCCESS! Production build ready.
echo   Files located in: %~dp0..\Publish_Output
echo.
echo   Next Steps:
echo   1. Copy Publish_Output to your IIS server physical path.
echo   2. Grant IIS_IUSRS read/write permissions to uploads\ and logs\
echo   3. Set AppPool to 'No Managed Code' + 'AlwaysRunning'.
echo =================================================================
pause
