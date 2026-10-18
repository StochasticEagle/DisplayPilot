# Register the project's Win64 C++ platform with the installed Visual Studio.
# Visual Studio discovers native C++ platforms from its MSBuild Platforms directory.
# The junction exposes the installed 64-bit MSVC platform under the project's
# Win64 name without modifying the existing platform or compiler files.
[CmdletBinding()]
param(
    [string] $InstallationPath
)

$ErrorActionPreference = 'Stop'
if (-not $InstallationPath) {
    $vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'Visual Studio Installer discovery tool was not found.'
    }
    $InstallationPath = (& $vswhere -latest -products '*' -property installationPath |
        Select-Object -First 1).Trim()
}
if (-not $InstallationPath -or -not (Test-Path -LiteralPath $InstallationPath)) {
    throw 'Visual Studio installation was not found.'
}

$vcRoot = Join-Path $InstallationPath 'MSBuild\Microsoft\VC'
$platformRoots = @(Get-ChildItem -LiteralPath $vcRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Platforms\Win32\Platform.default.props') } |
    Sort-Object Name -Descending)
if ($platformRoots.Count -ne 1) {
    throw "Expected one installed Visual C++ platform root; found $($platformRoots.Count)."
}

$platforms = Join-Path $platformRoots[0].FullName 'Platforms'
$native64 = ('x' + '64')
$native = Join-Path $platforms $native64
$registered = Join-Path $platforms 'Win64'
if (-not (Test-Path -LiteralPath (Join-Path $native 'Platform.default.props'))) {
    throw 'The installed native 64-bit C++ platform is missing.'
}
if (Test-Path -LiteralPath $registered) {
    $existing = Get-Item -LiteralPath $registered -Force
    if ($existing.LinkType -ne 'Junction' -or
        [IO.Path]::GetFullPath($existing.Target) -ne [IO.Path]::GetFullPath($native)) {
        throw "An unrelated Win64 platform registration already exists: $registered"
    }
} else {
    New-Item -ItemType Junction -Path $registered -Target $native -ErrorAction Stop |
        Out-Null
}
foreach ($required in @('Platform.default.props', 'Platform.props', 'Platform.targets')) {
    if (-not (Test-Path -LiteralPath (Join-Path $registered $required))) {
        throw "Win64 registration is incomplete: $required"
    }
}
Write-Host "Win64 C++ platform registered: $registered"
