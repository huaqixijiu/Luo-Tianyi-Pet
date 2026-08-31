[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0.13',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\msix'))
$stagingRoot = Join-Path $artifactRoot 'staging'
$publishRoot = Join-Path $stagingRoot 'publish'
$layoutRoot = Join-Path $stagingRoot 'layout'
$releaseRoot = Join-Path $artifactRoot 'release'
$privateRoot = Join-Path $artifactRoot 'private'

function Assert-ArtifactPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $prefix = $artifactRoot.TrimEnd('\') + '\'
    if (!$resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the MSIX artifact directory: $resolved"
    }
}

function Reset-ArtifactDirectory([string]$Path) {
    Assert-ArtifactPath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Find-Executable([string[]]$Candidates, [string]$Name) {
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }
    throw "Required tool was not found: $Name"
}

$dotnet = Find-Executable @(
    "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
    'C:\Program Files\dotnet\dotnet.exe'
) 'dotnet'
$winapp = Find-Executable @(
    "$env:LOCALAPPDATA\Microsoft\WindowsApps\winapp.exe"
) 'winapp'
$python = Find-Executable @(
    (Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python313\python.exe')
) 'python'

Reset-ArtifactDirectory $stagingRoot
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
New-Item -ItemType Directory -Path $layoutRoot -Force | Out-Null

$packageAssets = Join-Path $repoRoot 'packaging\Assets'
& $python (Join-Path $repoRoot 'tools\packaging\generate_package_assets.py') `
    --source (Join-Path $repoRoot 'assets\animations\runtime\twelfth-anniversary-hug.atlas.png') `
    --output $packageAssets `
    --frame-width 240 `
    --frame-height 240 `
    --frame-index 6
if ($LASTEXITCODE -ne 0) { throw 'Package asset generation failed.' }

& $dotnet publish (Join-Path $repoRoot 'src\LuoTianyiPet.App\LuoTianyiPet.App.csproj') `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $layoutRoot -Recurse -Force
$layoutPackageAssets = Join-Path $layoutRoot 'assets\package'
New-Item -ItemType Directory -Path $layoutPackageAssets -Force | Out-Null
Copy-Item -Path (Join-Path $packageAssets '*') -Destination $layoutPackageAssets -Force

$manifestTemplate = Join-Path $repoRoot 'packaging\Package.appxmanifest.template'
$manifestPath = Join-Path $stagingRoot 'Package.appxmanifest'
$manifestText = Get-Content -LiteralPath $manifestTemplate -Raw
[xml]$manifestDocument = $manifestText
$manifestNamespace = [System.Xml.XmlNamespaceManager]::new($manifestDocument.NameTable)
$manifestNamespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$manifestIdentity = $manifestDocument.SelectSingleNode('/f:Package/f:Identity', $manifestNamespace)
$manifestIdentity.SetAttribute('Version', $Version)
$manifestWriterSettings = [System.Xml.XmlWriterSettings]::new()
$manifestWriterSettings.Encoding = [Text.UTF8Encoding]::new($false)
$manifestWriterSettings.Indent = $true
$manifestWriter = [System.Xml.XmlWriter]::Create($manifestPath, $manifestWriterSettings)
try {
    $manifestDocument.Save($manifestWriter)
}
finally {
    $manifestWriter.Dispose()
}

$passwordPath = Join-Path $privateRoot 'LuoTianyiPet.Dev.password.txt'
$certificatePath = Join-Path $privateRoot 'LuoTianyiPet.Dev.pfx'
$publicCertificatePath = Join-Path $privateRoot 'LuoTianyiPet.Dev.cer'
$expectedPublisher = 'CN=LuoTianyiPet Development'
if (!(Test-Path -LiteralPath $passwordPath)) {
    $randomBytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
    [System.IO.File]::WriteAllText(
        $passwordPath,
        [Convert]::ToBase64String($randomBytes),
        [Text.UTF8Encoding]::new($false))
}
$certificatePassword = [System.IO.File]::ReadAllText($passwordPath).Trim()

$certificateNeedsGeneration = !(Test-Path -LiteralPath $certificatePath)
if (!$certificateNeedsGeneration) {
    try {
        $existingCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $certificatePath,
            $certificatePassword)
        try {
            $certificateNeedsGeneration =
                $existingCertificate.Subject -cne $expectedPublisher
        }
        finally {
            $existingCertificate.Dispose()
        }
    }
    catch {
        $certificateNeedsGeneration = $true
    }
}

if ($certificateNeedsGeneration) {
    foreach ($staleCertificate in @($certificatePath, $publicCertificatePath)) {
        Assert-ArtifactPath $staleCertificate
        if (Test-Path -LiteralPath $staleCertificate) {
            Remove-Item -LiteralPath $staleCertificate -Force
        }
    }
}

if ($certificateNeedsGeneration) {
    & $winapp cert generate `
        --manifest $manifestPath `
        --publisher $expectedPublisher `
        --output $certificatePath `
        --password $certificatePassword `
        --valid-days 365 `
        --export-cer `
        --if-exists Error `
        --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Development certificate generation failed.' }
}
if (!(Test-Path -LiteralPath $publicCertificatePath)) {
    throw "The public development certificate was not generated: $publicCertificatePath"
}

$packageName = "LuoTianyiPet_${Version}_x64.msix"
$packagePath = Join-Path $releaseRoot $packageName
Assert-ArtifactPath $packagePath
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

& $winapp pack $layoutRoot `
    --manifest $manifestPath `
    --executable 'LuoTianyiPet.exe' `
    --output $packagePath `
    --cert $certificatePath `
    --cert-password $certificatePassword `
    --skip-pri `
    --quiet
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging or signing failed.' }

$releaseCertificatePath = Join-Path $releaseRoot 'LuoTianyiPet.Dev.cer'
Copy-Item -LiteralPath $publicCertificatePath -Destination $releaseCertificatePath -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entryNames = $archive.Entries.FullName
    foreach ($requiredEntry in @(
        'AppxManifest.xml',
        'AppxBlockMap.xml',
        'AppxSignature.p7x',
        'LuoTianyiPet.exe',
        'assets/package/Square44x44Logo.png',
        'assets/package/Square150x150Logo.png',
        'assets/package/StoreLogo.png'
    )) {
        if ($entryNames -notcontains $requiredEntry) {
            throw "MSIX verification failed; missing entry: $requiredEntry"
        }
    }

    $manifestEntry = $archive.GetEntry('AppxManifest.xml')
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$packageManifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    $namespace = [System.Xml.XmlNamespaceManager]::new($packageManifest.NameTable)
    $namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespace.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3')
    $identity = $packageManifest.SelectSingleNode('/f:Package/f:Identity', $namespace)
    $capability = $packageManifest.SelectSingleNode(
        '/f:Package/f:Capabilities/uap3:Capability[@Name="userNotificationListener"]',
        $namespace)
    if ($identity.Version -ne $Version -or $identity.Publisher -ne $expectedPublisher) {
        throw 'MSIX verification failed; package identity does not match the build request.'
    }
    if ($null -eq $capability) {
        throw 'MSIX verification failed; userNotificationListener capability is missing.'
    }
}
finally {
    $archive.Dispose()
}

$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if ($signature.Status -in @('NotSigned', 'HashMismatch') -or $null -eq $signature.SignerCertificate) {
    throw "MSIX signature verification failed: $($signature.Status)"
}

$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$certificateHash = (Get-FileHash -LiteralPath $releaseCertificatePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
[System.IO.File]::WriteAllLines(
    $hashFile,
    @(
        "$packageHash  $packageName",
        "$certificateHash  LuoTianyiPet.Dev.cer"
    ),
    [Text.UTF8Encoding]::new($false))

Write-Host "Built signed MSIX: $packagePath"
Write-Host "Public test certificate: $releaseCertificatePath"
Write-Host "Signature status (expected untrusted before user installation): $($signature.Status)"
Write-Host "No certificate or application package was installed."
