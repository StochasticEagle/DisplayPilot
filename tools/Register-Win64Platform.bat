@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Register Win64 for Visual Studio 2026 without changing existing platform files.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" goto :missing_vswhere
set "VSROOT="
rem CALL handles the quoted executable path (including Program Files (x86)).
for /f "usebackq delims=" %%V in (`call "%VSWHERE%" -latest -products * -version "[18.0,19.0)" -property installationPath`) do if not defined VSROOT set "VSROOT=%%V"
if not defined VSROOT goto :missing_vs
rem VS 2026 uses v180 C++ targets. The older v160 tree can coexist,
rem but registering only there leaves Win64 without v145 toolset files.
set "PLATFORMS=%VSROOT%\MSBuild\Microsoft\VC\v180\Platforms"
if not exist "%PLATFORMS%\x64\Platform.default.props" goto :missing_platforms
set "NATIVE=%PLATFORMS%\x64"
set "REGISTERED=%PLATFORMS%\Win64"
if not exist "%NATIVE%\Platform.default.props" goto :missing_native
if exist "%REGISTERED%\Platform.default.props" goto :verify
if exist "%REGISTERED%" goto :existing
mklink /J "%REGISTERED%" "%NATIVE%"
if errorlevel 1 goto :create_failed
:verify
for %%F in (Platform.default.props Platform.props Platform.targets) do if not exist "%REGISTERED%\%%F" goto :incomplete
echo Win64 platform registered: "%REGISTERED%"
exit /b 0

:missing_vswhere
echo ERROR: Visual Studio Installer discovery tool not found: "%VSWHERE%"
exit /b 1
:missing_vs
echo ERROR: Visual Studio 2026 not found.
exit /b 1
:missing_platforms
echo ERROR: Visual C++ platform directory not found under "%VSROOT%".
exit /b 1
:missing_native
echo ERROR: Native 64-bit platform missing: "%NATIVE%".
exit /b 1
:existing
echo ERROR: Win64 already exists but is not a usable platform: "%REGISTERED%".
exit /b 1
:create_failed
echo ERROR: Unable to create Win64 registration. Run as administrator.
exit /b 1
:incomplete
echo ERROR: Win64 registration is incomplete: "%REGISTERED%".
exit /b 1
