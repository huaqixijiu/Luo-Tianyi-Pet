$pkg = Get-AppxPackage -Name LuoTianyiPet.Dev
$exe = Join-Path $PSScriptRoot 'bin\Release\net48\QqPreviewProbe.exe'
Invoke-CommandInDesktopPackage -PackageFamilyName $pkg.PackageFamilyName -AppId App -Command $exe -PreventBreakaway
