[CmdletBinding()]
param(
    [switch]$TrustCertificateOnly,
    [switch]$Quiet,
    [switch]$SkipDesktopShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bundleRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$markerPath = Join-Path $bundleRoot 'LUOTIANYI_PET_SIDELOAD_BUNDLE.marker'
if (!(Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw '安装包标记缺失。请重新解压完整安装包后再试。'
}

$packages = @(Get-ChildItem -LiteralPath $bundleRoot -Filter '*.msix' -File)
if ($packages.Count -ne 1) {
    throw "安装目录必须且只能包含一个 MSIX，当前找到 $($packages.Count) 个。"
}
$packagePath = $packages[0].FullName
$certificatePath = Join-Path $bundleRoot 'LuoTianyiPet.Dev.cer'
$hashPath = Join-Path $bundleRoot 'SHA256SUMS.txt'
foreach ($requiredPath in @($certificatePath, $hashPath)) {
    if (!(Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "安装包不完整：缺少 $([System.IO.Path]::GetFileName($requiredPath))。"
    }
}

$expectedHashes = @{}
foreach ($line in [System.IO.File]::ReadAllLines($hashPath)) {
    if ($line -match '^([0-9a-fA-F]{64})\s{2}(.+)$') {
        $expectedHashes[$matches[2]] = $matches[1].ToLowerInvariant()
    }
}
foreach ($path in @($packagePath, $certificatePath)) {
    $name = [System.IO.Path]::GetFileName($path)
    if (!$expectedHashes.ContainsKey($name)) {
        throw "校验文件中没有 $name。"
    }
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $expectedHashes[$name]) {
        throw "$name 的 SHA-256 不匹配，安装已停止。"
    }
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
try {
    $signature = Get-AuthenticodeSignature -LiteralPath $packagePath
    if ($null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) {
        throw 'MSIX 签名证书与安装包附带的公钥不一致，安装已停止。'
    }
    if ($certificate.NotAfter -le [DateTime]::Now) {
        throw '安装包签名证书已经过期，请获取新版安装包。'
    }
    $certificateThumbprint = $certificate.Thumbprint
}
finally {
    $certificate.Dispose()
}

$principal = [Security.Principal.WindowsPrincipal]::new(
    [Security.Principal.WindowsIdentity]::GetCurrent())
$isAdministrator = $principal.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
$trustedCertificate = Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' |
    Where-Object Thumbprint -CEQ $certificateThumbprint |
    Select-Object -First 1
if ($null -eq $trustedCertificate -and !$isAdministrator) {
    Write-Host '即将显示 Windows UAC：只用于信任本安装包的公开测试证书。'
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $PSCommandPath),
        '-TrustCertificateOnly'
    ) -join ' '
    $process = Start-Process -FilePath 'powershell.exe' `
        -Verb RunAs `
        -ArgumentList $arguments `
        -Wait `
        -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Windows 没有完成测试证书信任，退出代码为 $($process.ExitCode)。"
    }
    $trustedCertificate = Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' |
        Where-Object Thumbprint -CEQ $certificateThumbprint |
        Select-Object -First 1
}

if ($null -eq $trustedCertificate) {
    if (!$isAdministrator) {
        throw '测试证书仍未出现在 LocalMachine\TrustedPeople，安装已停止。'
    }
    Import-Certificate `
        -FilePath $certificatePath `
        -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    Write-Host '已信任本安装包的公开测试证书。'
}
if ($TrustCertificateOnly) {
    return
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $manifestEntry = $archive.GetEntry('AppxManifest.xml')
    if ($null -eq $manifestEntry) {
        throw 'MSIX 中缺少 AppxManifest.xml。'
    }
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$namespace = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
$namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity = $manifest.SelectSingleNode('/f:Package/f:Identity', $namespace)
$application = $manifest.SelectSingleNode('/f:Package/f:Applications/f:Application', $namespace)
if ($null -eq $identity -or $null -eq $application) {
    throw 'MSIX 清单没有有效的应用身份。'
}
$identityName = $identity.GetAttribute('Name')
$packageVersion = [Version]$identity.GetAttribute('Version')
$applicationId = $application.GetAttribute('Id')

$installed = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue |
    Sort-Object Version -Descending |
    Select-Object -First 1
if ($null -ne $installed -and [Version]$installed.Version -gt $packageVersion) {
    throw "电脑中已经安装更新版本 $($installed.Version)，不会降级到 $packageVersion。"
}
if ($null -eq $installed -or [Version]$installed.Version -ne $packageVersion) {
    Add-AppxPackage -Path $packagePath -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name $identityName -ErrorAction Stop |
        Sort-Object Version -Descending |
        Select-Object -First 1
}

$appUserModelId = "$($installed.PackageFamilyName)!$applicationId"
$desktopShortcutPath = $null
if (!$SkipDesktopShortcut) {
    $desktopDirectory = [Environment]::GetFolderPath('DesktopDirectory')
    if (![string]::IsNullOrWhiteSpace($desktopDirectory) -and
        (Test-Path -LiteralPath $desktopDirectory -PathType Container)) {
        $desktopShortcutPath = Join-Path $desktopDirectory '洛天依桌宠.lnk'
        $shell = New-Object -ComObject WScript.Shell
        try {
            $shortcut = $shell.CreateShortcut($desktopShortcutPath)
            $shortcut.TargetPath = Join-Path $env:WINDIR 'explorer.exe'
            $shortcut.Arguments = "shell:AppsFolder\$appUserModelId"
            $shortcut.IconLocation = "$(Join-Path $installed.InstallLocation 'LuoTianyiPet.exe'),0"
            $shortcut.Description = '启动洛天依桌宠'
            $shortcut.Save()
            [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
        }
        finally {
            [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
        }
    }
}

$running = @(Get-Process -Name 'LuoTianyiPet' -ErrorAction SilentlyContinue).Count -gt 0
if (!$running) {
    Start-Process -FilePath 'explorer.exe' -ArgumentList "shell:AppsFolder\$appUserModelId"
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        $running = @(Get-Process -Name 'LuoTianyiPet' -ErrorAction SilentlyContinue).Count -gt 0
    } while (!$running -and [DateTime]::UtcNow -lt $deadline)
}

$shortcutMessage = if ($desktopShortcutPath) {
    "`n桌面快捷方式：$desktopShortcutPath"
}
else {
    ''
}
$runMessage = if ($running) {
    '桌宠已经启动；如果暂时没看到，请查看桌面右下角托盘。'
}
else {
    '安装已经完成，但自动启动没有被确认。请使用桌面快捷方式或开始菜单启动。'
}
$successMessage = @(
    "洛天依桌宠 $($installed.Version) 已安装。",
    "$runMessage$shortcutMessage",
    '',
    '首次使用 QQ / 微信提醒时，请在桌宠“设置 → 通知”中点击“授权访问”。'
) -join [Environment]::NewLine

Write-Host $successMessage
if (!$Quiet) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $null = $shell.Popup($successMessage, 0, '洛天依桌宠安装完成', 64)
    }
    finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
}
