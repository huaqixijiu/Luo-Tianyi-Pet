[CmdletBinding()]
param(
    [ValidateRange(2, 100)]
    [int]$KeepVersions = 2,
    [switch]$Apply
)

# Deliberately limited to named release files. Never recurse into worktrees,
# staging, signing material, portable UserData, or authoring sources.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts'
$rules = @(
    @{ Directory = 'msix/release'; Pattern = '^LuoTianyiPet_(\d+\.\d+\.\d+\.\d+)_x64\.msix$' },
    @{ Directory = 'portable/release'; Pattern = '^LuoTianyiPet-Portable-(?:Test-)?(\d+\.\d+\.\d+\.\d+)-win-x64\.zip$' },
    @{ Directory = 'sideload/release'; Pattern = '^LuoTianyiPet-Installer-(\d+\.\d+\.\d+\.\d+)-win-x64\.zip$' }
)

function Assert-LocalPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Path is outside this repository artifact directory.'
    }
    $current = $full
    while ($current -ne $repoRoot) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Refusing a symbolic link or junction.'
            }
        }
        $current = Split-Path -Parent $current
    }
}

$plan = @()
foreach ($rule in $rules) {
    $directory = Join-Path $artifactRoot $rule.Directory
    Assert-LocalPath $directory
    if (!(Test-Path -LiteralPath $directory)) { continue }
    $packages = @(Get-ChildItem -LiteralPath $directory -File | ForEach-Object {
        if ($_.Name -match $rule.Pattern) {
            [pscustomobject]@{ File = $_; Version = [version]$Matches[1] }
        }
    } | Sort-Object Version -Descending)
    $versions = @($packages | ForEach-Object Version | Select-Object -Unique | Select-Object -First $KeepVersions)
    foreach ($package in $packages) {
        $keep = $versions -contains $package.Version
        $files = @($package.File)
        $sidecar = $package.File.FullName + '.sha256.txt'
        if (Test-Path -LiteralPath $sidecar) { $files += Get-Item -LiteralPath $sidecar }
        foreach ($file in $files) {
            Assert-LocalPath $file.FullName
            $plan += [pscustomobject]@{
                Action = $(if ($keep) { 'Keep' } else { 'Delete' })
                Path = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
                Bytes = $file.Length
                Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
        # Validate the retained ZIP against its published checksum before pruning.
        if ($keep -and $package.File.Extension -eq '.zip') {
            if (!(Test-Path -LiteralPath $sidecar)) { throw 'Retained ZIP has no checksum.' }
            $expected = ((Get-Content -LiteralPath $sidecar -Raw).Trim() -split '\s+')[0]
            $actual = (Get-FileHash -LiteralPath $package.File.FullName -Algorithm SHA256).Hash
            if ($expected -ne $actual) { throw 'Retained ZIP checksum mismatch.' }
        }
    }
    if ($packages.Count -gt 0 -and $rule.Directory -eq 'msix/release') {
        $checksums = Join-Path $directory 'SHA256SUMS.txt'
        if (!(Test-Path -LiteralPath $checksums)) { throw 'MSIX checksums are missing.' }
        $latest = $packages[0].File
        $line = @(Get-Content -LiteralPath $checksums | Where-Object { $_.EndsWith('  ' + $latest.Name) })
        if ($line.Count -ne 1 -or ($line[0] -split '\s+')[0] -ne (Get-FileHash -LiteralPath $latest.FullName).Hash) {
            throw 'Latest MSIX checksum mismatch.'
        }
    }
}

$reportDirectory = Join-Path $artifactRoot 'maintenance'
Assert-LocalPath $reportDirectory
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report = Join-Path $reportDirectory ('release-cleanup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-ffff') + '.json')
[pscustomobject]@{ Apply = [bool]$Apply; KeepVersions = $KeepVersions; Files = $plan } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $report -Encoding utf8

foreach ($item in $plan | Where-Object Action -eq 'Delete') {
    $target = Join-Path $repoRoot $item.Path
    Assert-LocalPath $target
    if ($Apply) {
        # A changed file belongs to a newer build; stop rather than delete it.
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $item.Sha256) {
            throw 'A planned release changed during cleanup.'
        }
        Remove-Item -LiteralPath $target -Force
    }
}
$deletions = @($plan | Where-Object Action -eq 'Delete')
$bytes = 0L
foreach ($item in $deletions) { $bytes += $item.Bytes }
[pscustomobject]@{
    Applied = [bool]$Apply
    FilesToDelete = $deletions.Count
    MiB = [math]::Round($bytes / 1MB, 2)
    Report = $report
}
