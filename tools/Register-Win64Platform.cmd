@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Register the existing Visual C++ 64-bit platform as Win64 for Visual Studio.
rem Run this file from an elevated Command Prompt.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo ERROR: Visual Studio Installer discovery tool not found.
  exit /b 1
)
set "VSROOT="
for /f "delims=" %%V in ('"%VSWHERE%" -latest -products * -version "[18.0,19.0)" -property installationPath') do if not defined VSROOT set "VSROOT=%%V"
if not defined VSROOT (
  echo ERROR: Visual Studio 2026 not found.
  exit /b 1
)
set "PLATFORMS="
for /f "delims=" %%D in ('dir /b /ad /o-n "%VSROOT%\MSBuild\Microsoft\VC" 2^>nul') do if not defined PLATFORMS if exist "%VSROOT%\MSBuild\Microsoft\VC\%%D\Platforms\Win32\Platform.default.props" set "PLATFORMS=%VSROOT%\MSBuild\Microsoft\VC\%%D\Platforms"
if not defined PLATFORMS (
  echo ERROR: Visual C++ platform directory not found.
  exit /b 1
)
set "NATIVE=%PLATFORMS%\x64"
set "REGISTERED=%PLATFORMS%\Win64"
if not exist "%NATIVE%\Platform.default.props" (
  echo ERROR: Installed native 64-bit C++ platform missing.
  exit /b 1
)
if exist "%REGISTERED%" (
  fsutil reparsepoint query "%REGISTERED%" >nul 2>&1
  if errorlevel 1 (
    echo ERROR: Win64 already exists and is not a junction. No changes made.
    exit /b 1
  )
  rem Do not replace an existing junction, even if it points elsewhere.
) else (
  mklink /J "%REGISTERED%" "%NATIVE%" || (
    echo ERROR: Unable to register Win64. Run as administrator.
    exit /b 1
  )
)
for %%F in (Platform.default.props Platform.props Platform.targets) do (
  if not exist "%REGISTERED%\%%F" (
    echo ERROR: Win64 registration missing %%F.
    exit /b 1
  )
)
echo Win64 Visual Studio platform registration is present: "%REGISTERED%"
exit /b 0
