# SPDX-License-Identifier: Unlicense
# Keep WinUI compiled markup inside the application PRI in both build configurations.
param(
    [Parameter(Mandatory = $true)]
    [string]$Directory,
    [string]$BuildDirectory
)

$ErrorActionPreference = 'Stop'
$Directory = [IO.Path]::GetFullPath($Directory)
if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
    throw "DisplayPilot output directory does not exist: $Directory"
}

$priCandidates = @(
    'resources.pri',
    'DisplayPilot.pri',
    'DisplayPilot.App.pri'
)
$pri = $null
foreach ($name in $priCandidates) {
    $candidate = Join-Path $Directory $name
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $pri = $candidate
        break
    }
}
if (-not $pri -and $BuildDirectory) {
    foreach ($name in $priCandidates) {
        $candidate = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $pri = $candidate
            break
        }
    }
    if ($pri) {
        $destination = Join-Path $Directory 'resources.pri'
        if (-not [string]::Equals($pri, $destination, [StringComparison]::OrdinalIgnoreCase)) {
            Copy-Item -LiteralPath $pri -Destination $destination -Force
        }
        $pri = $destination
    }
}
if (-not $pri) {
    throw "WinUI XAML resource index is missing: $Directory"
}

$makepri = Get-Command makepri.exe -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty Source
if (-not $makepri) {
    $sdkBin = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10\bin'
    $makepri = Get-ChildItem -LiteralPath $sdkBin -Filter makepri.exe -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '\\x64$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $makepri) {
    throw 'Windows SDK makepri.exe is required to validate embedded compiled XAML.'
}

$dump = Join-Path ([IO.Path]::GetTempPath()) "DisplayPilot-$([guid]::NewGuid().ToString('N')).xml"
try {
    & $makepri dump /if $pri /of $dump /o | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dump -PathType Leaf)) {
        throw "Cannot inspect WinUI resource index: $pri"
    }

    [xml]$index = Get-Content -LiteralPath $dump -Raw
    $resources = @($index.SelectNodes("//*[local-name()='NamedResource']"))
    foreach ($name in @('App.xbf', 'MainWindow.xbf')) {
        $matching = @($resources | Where-Object { $_.GetAttribute('name') -eq $name })
        $embedded = $false
        foreach ($resource in $matching) {
            $candidates = @($resource.SelectNodes(".//*[local-name()='Candidate']"))
            if (@($candidates | Where-Object { $_.GetAttribute('type') -eq 'EmbeddedData' }).Count -gt 0) {
                $embedded = $true
                break
            }
        }
        if (-not $embedded) {
            throw "$name is not embedded as binary data in $pri. Loose XBF resources have not been deleted."
        }
    }

    $loose = @(Get-ChildItem -LiteralPath $Directory -File -Recurse |
        Where-Object { $_.Name -in @('App.xbf', 'MainWindow.xbf') })
    if ($loose.Count) {
        $loose | Remove-Item -Force
    }
}
finally {
    Remove-Item -LiteralPath $dump -Force -ErrorAction SilentlyContinue
}
