param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName='EmberLighthouse',[switch]$Startup,[switch]$Presentation,[switch]$Mainline,[switch]$Exploration,[switch]$Natural,[switch]$Expansion,[switch]$Lighting,[switch]$JumpPlacement)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root "builds/$OutputName"
if(!(Test-Path -LiteralPath (Join-Path $build 'data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll'))){throw 'Build the Windows release first'}
Copy-Item -LiteralPath (Join-Path $root 'docs/运行说明.md'),(Join-Path $root 'docs/试玩流程.md'),(Join-Path $root 'docs/试玩记录.csv'),(Join-Path $root 'docs/性能记录.csv'),(Join-Path $root 'docs/耐久测试报告.md') -Destination $build
if($Startup){Copy-Item -LiteralPath (Join-Path $root 'docs/原生启动故障诊断.md') -Destination $build}
if($Presentation){Copy-Item -LiteralPath (Join-Path $root 'docs/成品体验更新.md'),(Join-Path $root 'docs/试玩反馈修复-2026-10-11.md') -Destination $build}
if($Mainline){Copy-Item -LiteralPath (Join-Path $root 'docs/三塔归火更新.md') -Destination $build; Copy-Item -LiteralPath (Join-Path $root 'docs/previews') -Destination $build -Recurse -Force}
if($Exploration){Copy-Item -LiteralPath (Join-Path $root 'docs/探索体验更新-2026-10-12.md') -Destination $build}
if($Natural){Copy-Item -LiteralPath (Join-Path $root 'docs/自然地表与永久删除更新-2026-10-12.md') -Destination $build}
if($Expansion){Copy-Item -LiteralPath (Join-Path $root 'docs/物品生物与护甲更新-2026-10-12.md'),(Join-Path $root 'docs/游戏物品用途清单.csv') -Destination $build}
if($Lighting){Copy-Item -LiteralPath (Join-Path $root 'docs/光照与掉落更新-2026-10-12.md') -Destination $build}
if($JumpPlacement){Copy-Item -LiteralPath (Join-Path $root 'docs/跳跃放块修复-2026-10-12.md') -Destination $build}
$files=Get-ChildItem -LiteralPath $build -File | Where-Object Extension -in '.exe','.pck','.md','.txt','.csv'
$runtime=Join-Path $build 'data_EmberLighthouse_windows_x86_64'
$archive=Join-Path $root "builds/${OutputName}试玩包.zip"
$archiveInputs=@($files.FullName + $runtime)
if($Mainline){$archiveInputs+=Join-Path $build 'previews'}
Compress-Archive -LiteralPath $archiveInputs -DestinationPath $archive -Force
$checks=Get-FileHash -LiteralPath (Join-Path $build 'EmberLighthouse.exe'),(Join-Path $build 'EmberLighthouse.pck'),(Join-Path $runtime 'EmberLighthouse.dll'),$archive -Algorithm SHA256
$checks | Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root "builds/$OutputName-SHA256.json") -Encoding utf8
Write-Output $archive


