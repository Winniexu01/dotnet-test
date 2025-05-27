@if not defined __echo @echo off
setlocal EnableDelayedExpansion EnableExtensions

set "__ThisScriptShort=%0"
set "__ThisScriptFull=%~f0"
set "__ThisScriptPath=%~dp0"

:: Set the default arguments for build
set __TargetArch=x64
set __BuildType=Debug
set __TargetOS=windows

:: Define a prefix for most output progress messages that come from this script. That makes
:: it easier to see where these are coming from. Note that there is a trailing space here.
set "__MsgPrefix=NATIVE BUILD: "

:: Set the various build properties here so that CMake and MSBuild can pick them up
set "__ProjectDir=%cd%"
:: remove trailing slash
if %__ProjectDir:~-1%==\ set "__ProjectDir=%__ProjectDir:~0,-1%"

set "__ArtifactsDir=%__ProjectDir%\artifacts"
set "__RootObjDir=%__ArtifactsDir%\obj"
set "__RootBinDir=%__ArtifactsDir%\bin"

set __CleanBuild=

set __TargetArchX64=0
set __TargetArchX86=0
set __TargetArchArm=0
set __TargetArchArm64=0

set __BuildTypeDebug=0
set __BuildTypeChecked=0
set __BuildTypeRelease=0
set __Ninja=1

:Arg_Loop
if "%1" == "" goto ArgsDone
set "__remainingArgs=!__remainingArgs:*%1=!"

if /i "%1" == "/?"    goto Usage
if /i "%1" == "-?"    goto Usage
if /i "%1" == "/h"    goto Usage
if /i "%1" == "-h"    goto Usage
if /i "%1" == "/help" goto Usage
if /i "%1" == "-help" goto Usage

if /i "%1" == "-x64"                 (set __TargetArchX64=1&shift&goto Arg_Loop)
if /i "%1" == "-x86"                 (set __TargetArchX86=1&shift&goto Arg_Loop)
if /i "%1" == "-arm"                 (set __TargetArchArm=1&shift&goto Arg_Loop)
if /i "%1" == "-arm64"               (set __TargetArchArm64=1&shift&goto Arg_Loop)

if /i "%1" == "-debug"               (set __BuildTypeDebug=1&shift&goto Arg_Loop)
if /i "%1" == "-checked"             (set __BuildTypeChecked=1&shift&goto Arg_Loop)
if /i "%1" == "-release"             (set __BuildTypeRelease=1&shift&goto Arg_Loop)

if /i "%1" == "-clean"               (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "-ninja"               (shift&goto Arg_Loop)
if /i "%1" == "-msbuild"             (set __Ninja=0&shift&goto Arg_Loop)

echo Invalid command-line argument: %1
goto Usage

:ArgsDone

set /A __TotalSpecifiedBuildArch=__TargetArchX64 + __TargetArchX86 + __TargetArchArm + __TargetArchArm64
if %__TotalSpecifiedBuildArch% GTR 1 (
    echo Error: more than one build architecture specified.
    goto Usage
)

if %__TargetArchX64%==1      set __TargetArch=x64
if %__TargetArchX86%==1      set __TargetArch=x86
if %__TargetArchArm%==1      set __TargetArch=arm
if %__TargetArchArm64%==1    set __TargetArch=arm64

set /A __TotalSpecifiedBuildType=__BuildTypeDebug + __BuildTypeChecked + __BuildTypeRelease
if %__TotalSpecifiedBuildType% GTR 1 (
    echo Error: more than one build type specified.
    goto Usage
)

if %__BuildTypeDebug%==1    set __BuildType=Debug
if %__BuildTypeChecked%==1  set __BuildType=Checked
if %__BuildTypeRelease%==1  set __BuildType=Release

set __CMakeTarget=install
set __ConfigTriplet=%__TargetOS%.%__TargetArch%.%__BuildType%
set "__LogsDir=%__ArtifactsDir%\log\%__BuildType%"
set "__BinDir=%__RootBinDir%\%__ConfigTriplet%"
set "__IntermediatesDir=%__RootObjDir%\%__ConfigTriplet%"
if "%__Ninja%"=="0" (set "__IntermediatesDir=%__IntermediatesDir%\ide")

echo %__MsgPrefix%Checking prerequisites

call "%__ProjectDir%\eng\native\init-vs-env.cmd" !__TargetArch!
if NOT '%ERRORLEVEL%' == '0' goto ExitWithError

REM Eval the output from set-cmake-path.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%__ProjectDir%\eng\native\set-cmake-path.ps1"""') do %%a
echo %__MsgPrefix%Using CMake from !CMakePath!

if not defined NumberOfCores (
    REM Determine number of physical processor cores available on machine
    set TotalNumberOfCores=0
    for /f "tokens=*" %%I in (
        'wmic cpu get NumberOfCores /value ^| find "=" 2^>NUL'
    ) do set %%I & set /a TotalNumberOfCores=TotalNumberOfCores+NumberOfCores
    set NumberOfCores=!TotalNumberOfCores!
)
echo %__MsgPrefix%Number of processor cores %NumberOfCores%

echo %__MsgPrefix%Commencing build of native components for %__ConfigTriplet%

:: Generate path to be set for CMAKE_INSTALL_PREFIX to contain forward slash
set "__CMakeBinDir=%__BinDir%"
set "__CMakeBinDir=%__CMakeBinDir:\=/%"

if not exist "%__BinDir%"           md "%__BinDir%"
if not exist "%__IntermediatesDir%" md "%__IntermediatesDir%"
if not exist "%__LogsDir%"          md "%__LogsDir%"

REM =========================================================================================
REM ===
REM === Start the build steps
REM ===
REM =========================================================================================

echo %__MsgPrefix%Generating build files. Using ninja: %__Ninja%

if %__Ninja% EQU 1 (
    set __ExtraCmakeArgs="-DCMAKE_BUILD_TYPE=!__BuildType!"
)
set __ExtraCmakeArgs=!__ExtraCmakeArgs! "-DCLR_CMAKE_TARGET_ARCH=%__TargetArch%"

echo Calling "%__ProjectDir%\eng\native\gen-buildsys.cmd" "%__ProjectDir%" "%__IntermediatesDir%" %__VSVersion% %__TargetArch% %__TargetOS% !__ExtraCmakeArgs!
call "%__ProjectDir%\eng\native\gen-buildsys.cmd" "%__ProjectDir%" "%__IntermediatesDir%" %__VSVersion% %__TargetArch% %__TargetOS% !__ExtraCmakeArgs!
if not !errorlevel! == 0 (
    echo %__ErrMsgPrefix%%__MsgPrefix%Error: failed to generate native component build project!
    goto ExitWithError
)

if not exist "%__IntermediatesDir%\CMakeCache.txt" (
    echo %__MsgPrefix%Error: failed to generate native component build project!
    exit /b 1
)

REM =========================================================================================
REM ===
REM === Build tests
REM ===
REM =========================================================================================

set __CmakeBuildToolArgs=
if %__Ninja% EQU 1 (
    set __CmakeBuildToolArgs=
) else (
    REM We pass the /m flag directly to MSBuild so that we can get both MSBuild and CL parallelism, which is fastest for our builds.
    set "__CmakeBuildToolArgs=/nologo /m /bl:%__LogsDir%\NativeBuild_%__ConfigTriplet%.binlog"
)

set "__BinLog=%__LogsDir%\NativeBuild_%__ConfigTriplet%.binlog"

set __cmakeCommand="%CMakePath%" --build %__IntermediatesDir% --target %__CMakeTarget% --config %__BuildType% -- !__CmakeBuildToolArgs!
echo %__MsgPrefix%Invoking cmake build: %__cmakeCommand%

%__cmakeCommand%
if not !errorlevel! == 0 (
    set __exitCode=!errorlevel!
    echo %__ErrMsgPrefix%%__MsgPrefix%Error: native component build failed. Refer to the build log.
    goto ExitWithError
)

echo %__MsgPrefix% successfully built. Binaries available at !__BinDir!
exit /b 0

REM =========================================================================================
REM ===
REM === Helper routines
REM ===
REM =========================================================================================

:Usage
echo.
echo Build native part of the debugger tests.
echo.
echo Usage:
echo     %__ThisScriptShort% [option1] [option2] ...
echo.
echo All arguments are optional. The options are:
echo.
echo./? -? /h -h /help -help: view this message.
echo Build architecture: one of x64, x86, arm, arm64 ^(default: x64^).
echo Build type: one of Debug, Checked, Release ^(default: Debug^).
echo clean: force a clean build ^(default is to perform an incremental build^).
echo sequential: force a non-parallel build ^(default is to build in parallel
echo     using all processors^).
exit /b 1

:ExitWithError
exit /b 1
