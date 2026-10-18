# SPDX-License-Identifier: Unlicense
param(
    [Parameter(Mandatory = $true)][string]$ProjectDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$IntermediateDirectory
)
$ErrorActionPreference = 'Stop'
$programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
$installer = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $installer -PathType Leaf)) { throw 'Visual Studio C++ build tools (x64) are required for Release.' }
$vsDirectory = & $installer -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($vsDirectory)) { throw 'Visual Studio C++ build tools (x64) were not found.' }
$vsCommand = Join-Path $vsDirectory 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path $vsCommand -PathType Leaf)) { throw 'VsDevCmd.bat was not found.' }
New-Item -Path $OutputDirectory, $IntermediateDirectory -ItemType Directory -Force | Out-Null
$source = Join-Path $ProjectDirectory 'NativeHost\DisplayPilotHost.cpp'
$resource = Join-Path $ProjectDirectory 'NativeHost\DisplayPilot.rc'
$iconDir = Join-Path $ProjectDirectory 'Assets'
$resOutput = Join-Path $IntermediateDirectory 'DisplayPilotHost.res'
$exeOutput = Join-Path $OutputDirectory 'DisplayPilot.exe'
$manifest = Join-Path $ProjectDirectory 'app.manifest'
$commands = @(
    ('call "{0}" -arch=amd64 -host_arch=amd64 >nul' -f $vsCommand),
    ('rc /nologo /i "{0}" /fo "{1}" "{2}"' -f $iconDir, $resOutput, $resource),
    ('cl /nologo /O2 /W4 /WX /EHsc /utf-8 /std:c++17 /MD /Fe:"{0}" "{1}" "{2}" /link /SUBSYSTEM:WINDOWS /MANIFEST:EMBED /MANIFESTINPUT:"{3}" user32.lib ole32.lib advapi32.lib' -f $exeOutput, $source, $resOutput, $manifest)
)
& cmd.exe /d /c ($commands -join ' && ')
if ($LASTEXITCODE -ne 0) { throw "Native DisplayPilot host compilation failed ($LASTEXITCODE)." }
if (-not (Test-Path $exeOutput -PathType Leaf)) { throw 'Release executable was not generated.' }
