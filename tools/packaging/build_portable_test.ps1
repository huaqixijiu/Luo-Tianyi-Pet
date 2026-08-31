[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0.15',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\portable'))
$stagingRoot = Join-Path $artifactRoot 'staging'
$layoutRoot = Join-Path $stagingRoot "LuoTianyiPet-Portable-Test-$Version"
$releaseRoot = Join-Path $artifactRoot 'release'
$packageName = "LuoTianyiPet-Portable-Test-$Version-win-x64.zip"
$packagePath = Join-Path $releaseRoot $packageName

function Assert-ArtifactPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $prefix = $artifactRoot.TrimEnd('\') + '\'
    if (!$resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the portable artifact directory: $resolved"
    }
}

function Reset-ArtifactDirectory([string]$Path) {
    Assert-ArtifactPath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

$dotnetCandidates = @(
    "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
    'C:\Program Files\dotnet\dotnet.exe'
)
$dotnet = $dotnetCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (!$dotnet) {
    throw 'The .NET SDK was not found.'
}

Reset-ArtifactDirectory $stagingRoot
New-Item -ItemType Directory -Path $layoutRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

& $dotnet publish (Join-Path $repoRoot 'src\LuoTianyiPet.App\LuoTianyiPet.App.csproj') `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $layoutRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Self-contained portable publish failed.'
}

Copy-Item -Path (Join-Path $repoRoot 'packaging\portable\*') `
    -Destination $layoutRoot -Recurse -Force

foreach ($requiredPath in @(
    'LuoTianyiPet.exe',
    'assets\manifests\animations.json',
    '启动桌宠.cmd',
    '清理测试数据.cmd',
    'cleanup-portable-data.ps1',
    '使用说明.txt',
    'PORTABLE_TEST_PACKAGE.marker'
)) {
    if (!(Test-Path -LiteralPath (Join-Path $layoutRoot $requiredPath))) {
        throw "Portable package verification failed; missing: $requiredPath"
    }
}

foreach ($forbiddenPattern in @('*.cer', '*.pfx', '*.msix')) {
    if (Get-ChildItem -LiteralPath $layoutRoot -Filter $forbiddenPattern -Recurse) {
        throw "Portable package verification failed; forbidden file included: $forbiddenPattern"
    }
}
if (Test-Path -LiteralPath (Join-Path $layoutRoot 'UserData')) {
    throw 'Portable package verification failed; mutable UserData was included.'
}

Assert-ArtifactPath $packagePath
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $layoutRoot,
    $packagePath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = Join-Path $releaseRoot "$packageName.sha256.txt"
[System.IO.File]::WriteAllText(
    $hashPath,
    "$packageHash  $packageName`r`n",
    [Text.UTF8Encoding]::new($false))

Write-Host "Built clean portable test package: $packagePath"
Write-Host "SHA-256: $packageHash"
Write-Host 'No certificate, package registration, installer, registry entry, or mutable user data was included.'
