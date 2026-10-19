@echo off
setlocal EnableExtensions
chcp 65001 >nul
title HMSTHModdingTool - Release Build

set "ROOT=%~dp0"
set "PROJECT=%ROOT%HMSTHModdingTool\HMSTHModdingTool.csproj"
set "CONFIG=Release"
set "PLATFORM=AnyCPU"
set "LOG_DIR=%ROOT%build-logs"
set "LOG=%LOG_DIR%\build.log"
set "MSBUILD="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%PROJECT%" (
    echo [ERROR] Project not found: "%PROJECT%"
    goto :FAIL
)

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
if errorlevel 1 (
    echo [ERROR] Cannot create build log directory.
    goto :FAIL
)

if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do (
        if not defined MSBUILD set "MSBUILD=%%I"
    )
)

if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"

if not defined MSBUILD (
    echo [ERROR] MSBuild.exe was not found.
    echo Install Visual Studio 2022 or Build Tools with MSBuild and the .NET desktop build tools.
    echo Also install the .NET Framework 4.8 Developer Pack.
    goto :FAIL
)

echo.
echo ==============================================
echo       HMSTHModdingTool - Release Build
echo ==============================================
echo Project   : %PROJECT%
echo Framework : .NET Framework 4.8
echo MSBuild   : %MSBUILD%
echo Log       : %LOG%
echo.

"%MSBUILD%" "%PROJECT%" /nologo /m /t:Rebuild /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /verbosity:minimal "/flp:logfile=%LOG%;verbosity=normal"
if errorlevel 1 goto :BUILD_FAILED

if not exist "%ROOT%HMSTHModdingTool\bin\%CONFIG%\HMSTHModdingTool.exe" (
    echo [ERROR] MSBuild returned success but the expected executable was not found.
    goto :FAIL
)

echo.
echo [OK] Build succeeded.
echo Executable: "%ROOT%HMSTHModdingTool\bin\%CONFIG%\HMSTHModdingTool.exe"
echo Log: "%LOG%"
echo.
endlocal
exit /b 0

:BUILD_FAILED
echo.
echo [ERROR] Build failed. Check the compiler output above and:
echo "%LOG%"
echo.
endlocal
exit /b 1

:FAIL
echo.
pause
endlocal
exit /b 1
