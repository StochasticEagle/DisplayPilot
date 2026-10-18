# SPDX-License-Identifier: Unlicense
# Remove loose XBF files only when the PRI proves both resources are embedded.
param([Parameter(Mandatory = $true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$index = @('resources.pri', 'DisplayPilot.pri', 'DisplayPilot.App.pri') |
    ForEach-Object { Join-Path $Directory $_ } |
    Where-Object { Test-Path $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $index) { throw "Release resource index missing in $Directory." }

$kits = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10\bin'
$makepri = Get-Command 'makepri.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $makepri) {
    $makepri = Get-ChildItem $kits -Filter 'makepri.exe' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\makepri\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -ExpandProperty FullName -First 1
}
if (-not $makepri) { throw 'Windows SDK makepri.exe is required to verify embedded Release XAML.' }
$dump = Join-Path $env:TEMP ('DisplayPilot-pri-' + [guid]::NewGuid().ToString('N') + '.xml')
try {
    & $makepri dump /if $index /of $dump /o /dt Detailed | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dump)) { throw "Cannot inspect $index." }
    [xml]$document = Get-Content -LiteralPath $dump -Raw
    foreach ($resource in @('App.xbf', 'MainWindow.xbf')) {
        $matches = @($document.SelectNodes("//*[local-name()='NamedResource']") |
            Where-Object { $_.GetAttribute('name') -eq $resource })
        if (-not $matches -or -not ($matches | Where-Object {
            $_.SelectSingleNode("./*[local-name()='Candidate'][@type='EmbeddedData']")
        })) {
            throw "$resource is not embedded in $index; refusing to remove the external copy."
        }
    }
    Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.xbf' |
        Remove-Item -Force
}
finally {
    Remove-Item -LiteralPath $dump -Force -ErrorAction SilentlyContinue
}
