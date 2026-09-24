@echo off
setlocal EnableDelayedExpansion

echo =======================================================================
echo   Law Project - Master Production Publishing Pipeline
echo   Solution: LawProject.sln (.NET 9.0)
echo =======================================================================

cd /d "%~dp0"

echo.
echo [Step 1/4] Restoring and Building LawProject.sln in Release mode...
dotnet build LawProject.sln -c Release --nologo
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed! Aborting publish.
    pause
    exit /b %errorlevel%
)

echo.
echo [Step 2/4] Running solution unit tests...
dotnet test LawProject.sln -c Release --no-build --nologo
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Unit tests failed! Publish aborted to protect production integrity.
    pause
    exit /b %errorlevel%
)

echo.
echo [Step 3/4] Publishing MVCCaseManagement (Web Portal)...
set "WEB_OUT=%~dp0Publish_Output\MVCCaseManagement"
if exist "%WEB_OUT%" (
    echo Cleaning existing output in %WEB_OUT%...
    rmdir /s /q "%WEB_OUT%"
)
dotnet publish "Law Project\MVCCaseManagement\MVCCaseManagement.csproj" -c Release -o "%WEB_OUT%" --no-build --nologo
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Publishing MVCCaseManagement failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [Step 4/4] Publishing NapixEcourtsApi (e-Courts Gateway Microservice)...
set "API_OUT=%~dp0Publish_Output\NapixEcourtsApi"
if exist "%API_OUT%" (
    echo Cleaning existing output in %API_OUT%...
    rmdir /s /q "%API_OUT%"
)
dotnet publish "NapixEcourtsApi\NapixEcourtsApi.csproj" -c Release -o "%API_OUT%" --no-build --nologo
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Publishing NapixEcourtsApi failed!
    pause
    exit /b %errorlevel%
)

:: Create necessary directories for runtime
if not exist "%WEB_OUT%\logs" mkdir "%WEB_OUT%\logs"
if not exist "%WEB_OUT%\uploads" mkdir "%WEB_OUT%\uploads"
if not exist "%WEB_OUT%\wwwroot\uploads" mkdir "%WEB_OUT%\wwwroot\uploads"
if not exist "%API_OUT%\logs" mkdir "%API_OUT%\logs"

echo.
echo =======================================================================
echo   PUBLISH SUCCESSFUL!
echo =======================================================================
echo   Web Portal:           %WEB_OUT%
echo   eCourts Microservice: %API_OUT%
echo.
echo   For deployment instructions (IIS, Windows Service, Kestrel), see:
echo   docs\PUBLISHING_GUIDE.md
echo =======================================================================
pause
