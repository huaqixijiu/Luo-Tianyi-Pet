[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$portableRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$markerPath = Join-Path $portableRoot 'PORTABLE_TEST_PACKAGE.marker'
$executablePath = Join-Path $portableRoot 'LuoTianyiPet.exe'
$dataPath = [System.IO.Path]::GetFullPath((Join-Path $portableRoot 'UserData'))
$expectedDataPrefix = $portableRoot.TrimEnd('\') + '\'

if (!(Test-Path -LiteralPath $markerPath -PathType Leaf) -or
    !(Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'This is not a complete LuoTianyiPet portable test directory. Cleanup stopped.'
}

if (!$dataPath.StartsWith($expectedDataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The test data directory is outside the portable package. Cleanup stopped.'
}

$stoppedCount = 0
Get-CimInstance Win32_Process -Filter "Name = 'LuoTianyiPet.exe'" |
    Where-Object {
        $_.ExecutablePath -and
        [System.IO.Path]::GetFullPath($_.ExecutablePath).Equals(
            $executablePath,
            [StringComparison]::OrdinalIgnoreCase)
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
        $stoppedCount++
    }

if (Test-Path -LiteralPath $dataPath) {
    Remove-Item -LiteralPath $dataPath -Recurse -Force
}

Write-Host "Cleanup completed. Stopped $stoppedCount process(es) from this package and removed UserData."
Write-Host 'You can now delete the extracted folder and the original ZIP.'
