[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0.75',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('Net10SelfContained', 'NetFramework48')]
    [string]$Framework = 'NetFramework48'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\portable'))
$stagingRoot = Join-Path $artifactRoot 'staging'
$layoutRoot = Join-Path $stagingRoot "LuoTianyiPet-Portable-$Version"
$releaseRoot = Join-Path $artifactRoot 'release'
$packageName = "LuoTianyiPet-Portable-$Version-win-x64.zip"
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

$applicationProject = Join-Path $repoRoot 'src\LuoTianyiPet.App\LuoTianyiPet.App.csproj'
if ($Framework -eq 'NetFramework48') {
    & $dotnet publish $applicationProject `
        -c Release `
        -f net48 `
        -r $Runtime `
        --self-contained false `
        -p:EnableNetFrameworkBuild=true `
        -p:PlatformTarget=x64 `
        -p:Prefer32Bit=false `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $layoutRoot
    if ($LASTEXITCODE -ne 0) {
        throw '.NET Framework 4.8 portable publish failed.'
    }
}
else {
    & $dotnet publish $applicationProject `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:Version=$Version `
        -p:PublishSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $layoutRoot
    if ($LASTEXITCODE -ne 0) {
        throw '.NET 10 self-contained portable publish failed.'
    }
}

Copy-Item -Path (Join-Path $repoRoot 'packaging\portable\*') `
    -Destination $layoutRoot -Recurse -Force

foreach ($requiredPath in @(
    'LuoTianyiPet.exe',
    'assets\manifests\animations.json',
    'cleanup-portable-data.ps1',
    'LUOTIANYI_PET_PORTABLE.marker'
)) {
    if (!(Test-Path -LiteralPath (Join-Path $layoutRoot $requiredPath))) {
        throw "Portable package verification failed; missing: $requiredPath"
    }
}
$commandFiles = @(Get-ChildItem -LiteralPath $layoutRoot -Filter '*.cmd' -File)
if ($commandFiles.Count -lt 2) {
    throw 'Portable package verification failed; launch or cleanup command file is missing.'
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
Add-Type -AssemblyName System.IO.Compression.FileSystem
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

Write-Host "Built clean portable package: $packagePath"
Write-Host "SHA-256: $packageHash"
Write-Host 'No certificate, package registration, installer, registry entry, or mutable user data was included.'
