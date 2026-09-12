$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testRoot = Join-Path $repoRoot ('artifacts/maintenance/cleanup-test-' + [guid]::NewGuid().ToString('N'))
$scriptDirectory = Join-Path $testRoot 'tools/maintenance'
New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
$script = Join-Path $scriptDirectory 'clean_old_releases.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'clean_old_releases.ps1') -Destination $script
$release = Join-Path $testRoot 'artifacts/portable/release'
New-Item -ItemType Directory -Path $release -Force | Out-Null
foreach ($version in @(1, 9, 10)) {
    $name = "LuoTianyiPet-Portable-0.1.0.$version-win-x64.zip"
    $file = Join-Path $release $name
    Set-Content -LiteralPath $file -Value "fixture $version"
    ((Get-FileHash -LiteralPath $file).Hash + '  ' + $name) | Set-Content -LiteralPath ($file + '.sha256.txt')
}
$old = Join-Path $release 'LuoTianyiPet-Portable-0.1.0.1-win-x64.zip'
$latest = Join-Path $release 'LuoTianyiPet-Portable-0.1.0.10-win-x64.zip'
Set-Content -LiteralPath (Join-Path $release 'unrelated.zip') -Value 'must survive'
New-Item -ItemType Directory -Path (Join-Path $release 'UserData') | Out-Null
Set-Content -LiteralPath (Join-Path $release 'UserData/settings.json') -Value '{}'
$preview = & $script
if ($preview.FilesToDelete -ne 2 -or !(Test-Path -LiteralPath $old)) { throw 'Dry-run changed files or chose wrong numeric versions.' }
$checksum = Get-Content -LiteralPath ($latest + '.sha256.txt') -Raw
Set-Content -LiteralPath ($latest + '.sha256.txt') -Value 'invalid'
$rejected = $false
try { & $script -Apply | Out-Null } catch { $rejected = $true }
if (!$rejected -or !(Test-Path -LiteralPath $old)) { throw 'Checksum failure did not stop cleanup.' }
Set-Content -LiteralPath ($latest + '.sha256.txt') -Value $checksum.Trim()
& $script -Apply | Out-Null
if (Test-Path -LiteralPath $old) { throw 'Old package was not deleted.' }
if (Test-Path -LiteralPath ($old + '.sha256.txt')) { throw 'Orphaned checksum.' }
foreach ($name in @('LuoTianyiPet-Portable-0.1.0.9-win-x64.zip', 'LuoTianyiPet-Portable-0.1.0.10-win-x64.zip', 'unrelated.zip', 'UserData/settings.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $release $name))) { throw "Protected file removed: $name" }
}
$again = & $script -Apply
if ($again.FilesToDelete -ne 0) { throw 'Cleanup is not idempotent.' }
# A release junction must never redirect cleanup to another directory.
$junction = Join-Path $testRoot 'artifacts/msix'
New-Item -ItemType Directory -Path $junction | Out-Null
New-Item -ItemType Junction -Path (Join-Path $junction 'release') -Target $release | Out-Null
$rejected = $false
try { & $script -Apply | Out-Null } catch { $rejected = $true }
if (!$rejected) { throw 'Release junction was accepted.' }
'PASS: dry-run, numeric retention, checksum failure, package/sidecar removal, unknown-file and UserData preservation, idempotence, junction rejection.'
