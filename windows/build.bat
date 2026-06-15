@echo off
chcp 65001 >nul 2>&1
setlocal

set "PROJ_DIR=%~dp0LiveAgent"
set "PROJ_FILE=%PROJ_DIR%\LiveAgent.csproj"
set "OUT_DIR=%~dp0publish"

echo.
echo ========================================
echo  LiveAgent - Clean and Build
echo ========================================
echo.

:: ---- Clean ----
echo [1/3] Cleaning bin, obj, publish ...
if exist "%PROJ_DIR%bin"      rmdir /s /q "%PROJ_DIR%bin"
if exist "%PROJ_DIR%obj"      rmdir /s /q "%PROJ_DIR%obj"
if exist "%OUT_DIR%"          rmdir /s /q "%OUT_DIR%"
echo      Done.
echo.

:: ---- Restore ----
echo [2/3] Restoring NuGet packages ...
dotnet restore "%PROJ_FILE%"
if errorlevel 1 (
    echo.
    echo [ERROR] Restore failed.
    goto :fail
)
echo      Done.
echo.

:: ---- Publish single-file ----
echo [3/3] Publishing single-file (Release, win-x64, self-contained) ...
dotnet publish "%PROJ_FILE%" -c Release -o "%OUT_DIR%"
if errorlevel 1 (
    echo.
    echo [ERROR] Publish failed.
    goto :fail
)

del /q "%OUT_DIR%\*.pdb" 2>nul

echo.
echo ========================================
echo  BUILD SUCCESSFUL
echo ========================================
echo  Output : %OUT_DIR%\LiveDashboardAgent.exe
echo ========================================
echo.
goto :eof

:fail
echo.
echo Build aborted.
exit /b 1
