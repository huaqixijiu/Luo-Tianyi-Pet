[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0.44',
    [ValidateSet('Net10SelfContained', 'NetFramework48')]
    [string]$Framework = 'NetFramework48'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\sideload'))
$stagingRoot = Join-Path $artifactRoot 'staging'
$releaseRoot = Join-Path $artifactRoot 'release'
$bundleName = "LuoTianyiPet-Installer-$Version-win-x64"
$bundleRoot = Join-Path $stagingRoot $bundleName
$bundlePath = Join-Path $releaseRoot "$bundleName.zip"

function Assert-ArtifactPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $prefix = $artifactRoot.TrimEnd('\') + '\'
    if (!$resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the sideload artifact directory: $resolved"
    }
}

foreach ($path in @($stagingRoot, $releaseRoot)) {
    Assert-ArtifactPath $path
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}
New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null

& (Join-Path $PSScriptRoot 'build_msix.ps1') `
    -Version $Version `
    -Framework $Framework `
    -SigningMode Development
if ($LASTEXITCODE -ne 0) {
    throw 'MSIX build failed.'
}

$msixReleaseRoot = Join-Path $repoRoot 'artifacts\msix\release'
$packagePath = Join-Path $msixReleaseRoot "LuoTianyiPet_${Version}_x64.msix"
$certificatePath = Join-Path $msixReleaseRoot 'LuoTianyiPet.Dev.cer'
$hashPath = Join-Path $msixReleaseRoot 'SHA256SUMS.txt'
foreach ($path in @($packagePath, $certificatePath, $hashPath)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "MSIX release output is incomplete: $path"
    }
    Copy-Item -LiteralPath $path -Destination $bundleRoot -Force
}
Copy-Item -Path (Join-Path $repoRoot 'packaging\sideload\*') `
    -Destination $bundleRoot `
    -Force

Compress-Archive -LiteralPath $bundleRoot -DestinationPath $bundlePath -CompressionLevel Optimal
$bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    "$bundlePath.sha256.txt",
    "$bundleHash  $([System.IO.Path]::GetFileName($bundlePath))`r`n",
    [Text.UTF8Encoding]::new($false))

Write-Host "Built Windows 11 sideload installer bundle: $bundlePath"
Write-Host 'The bundle contains only the public CER, never the PFX private key or password.'
Write-Host 'No certificate or application package was installed.'
