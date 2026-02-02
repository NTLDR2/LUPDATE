@echo off
setlocal enabledelayedexpansion
title LUPDATE Builder

echo [INFO] Searching for .NET Compiler...

:: Try to find MSBuild (preferred)
set "MSBUILD="
for %%p in (
    "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
    "%WINDIR%\Microsoft.NET\Framework\v3.5\MSBuild.exe"
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    "%WINDIR%\Microsoft.NET\Framework64\v3.5\MSBuild.exe"
) do (
    if exist "%%~p" (
        set "MSBUILD=%%~p"
        goto :build
    )
)

echo [ERROR] Could not find MSBuild.exe. ensure .NET Framework is installed.
pause
exit /b 1

:build
echo [INFO] Found MSBuild at: !MSBUILD!
echo [INFO] Building LUPDATE.csproj...
echo.

"!MSBUILD!" LUPDATE.csproj /p:Configuration=Release /t:Build

if %errorlevel% neq 0 (
    echo [ERROR] Build Failed!
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Build Complete.
if exist "bin\Release\LUPDATE.exe" (
    echo Artifact: bin\Release\LUPDATE.exe
    copy bin\Release\LUPDATE.exe .\LUPDATE.exe >nul
    echo Copied to current directory.
)
pause
