@echo off
setlocal
set "SCRIPT_ROOT=%~dp0"
set "PROJECT_ROOT="
set "OPEN_INSTALLER=0"

:parse_arguments
if "%~1"=="" goto arguments_parsed
if /I "%~1"=="--project" (
    set "PROJECT_ROOT=%~2"
    shift
    shift
    goto parse_arguments
)
if /I "%~1"=="--open-installer" (
    set "OPEN_INSTALLER=1"
    shift
    goto parse_arguments
)

echo Usage: %~nx0 --project ^<UnityOrGodotProjectRoot^> [--open-installer] 1>&2
exit /b 2

:arguments_parsed
if "%PROJECT_ROOT%"=="" (
    echo Missing required --project ^<UnityOrGodotProjectRoot^>. 1>&2
    exit /b 2
)

for %%I in ("%SCRIPT_ROOT%..\..") do set "WORKBENCH_ROOT=%%~fI"
for %%I in ("%WORKBENCH_ROOT%\..") do set "PACKAGE_ROOT=%%~fI"
set "PACKAGING_PROJECT=%WORKBENCH_ROOT%\src\YokiFrame.Packaging\YokiFrame.Packaging.csproj"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 10 SDK is required to build the YokiFrame project Runtime cache. 1>&2
    exit /b 1
)

if "%OPEN_INSTALLER%"=="1" (
    dotnet run --project "%PACKAGING_PROJECT%" -- runtime bootstrap --package-root "%PACKAGE_ROOT%" --project-root "%PROJECT_ROOT%" --configuration Release --open-installer
) else (
    dotnet run --project "%PACKAGING_PROJECT%" -- runtime bootstrap --package-root "%PACKAGE_ROOT%" --project-root "%PROJECT_ROOT%" --configuration Release
)
exit /b %errorlevel%
