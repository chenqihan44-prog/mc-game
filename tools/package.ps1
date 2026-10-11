param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName='EmberLighthouse',[switch]$Startup,[switch]$Presentation)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root "builds/$OutputName"
if(!(Test-Path -LiteralPath (Join-Path $build 'data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll'))){throw 'Build the Windows release first'}
Copy-Item -LiteralPath (Join-Path $root 'docs/运行说明.md'),(Join-Path $root 'docs/试玩流程.md'),(Join-Path $root 'docs/试玩记录.csv'),(Join-Path $root 'docs/性能记录.csv'),(Join-Path $root 'docs/耐久测试报告.md') -Destination $build
if($Startup){Copy-Item -LiteralPath (Join-Path $root 'docs/原生启动故障诊断.md') -Destination $build}
if($Presentation){Copy-Item -LiteralPath (Join-Path $root 'docs/成品体验更新.md') -Destination $build}
$files=Get-ChildItem -LiteralPath $build -File | Where-Object Extension -in '.exe','.pck','.md','.txt','.csv'
$runtime=Join-Path $build 'data_EmberLighthouse_windows_x86_64'
$archive=Join-Path $root "builds/${OutputName}试玩包.zip"
Compress-Archive -LiteralPath @($files.FullName + $runtime) -DestinationPath $archive -Force
$checks=Get-FileHash -LiteralPath (Join-Path $build 'EmberLighthouse.exe'),(Join-Path $build 'EmberLighthouse.pck'),(Join-Path $runtime 'EmberLighthouse.dll'),$archive -Algorithm SHA256
$checks | Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root "builds/$OutputName-SHA256.json") -Encoding utf8
Write-Output $archive
