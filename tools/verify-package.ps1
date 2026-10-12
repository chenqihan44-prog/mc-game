param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName,[switch]$Boundary,[switch]$CreaturePath,[switch]$Feedback,[switch]$Equipment,[switch]$Estate,[switch]$Startup,[switch]$StorageStartup,[switch]$PresentationOnly,[switch]$MainlineOnly,[switch]$ExplorationOnly,[switch]$Natural,[switch]$Expansion,[switch]$Lighting,[switch]$JumpPlacement)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root "builds/$OutputName"
$archive=Join-Path $root "builds/${OutputName}试玩包.zip"
$checks=@('release-smoke','interaction','creature','combat','building-headless','save-ui-headless','route')
if($Boundary){$checks+='boundary'}
if($CreaturePath){$checks+='creature-path'}
if($Feedback){$checks+=@('feedback','feedback-reload')}
if($Equipment){$checks+=@('equipment','equipment-reload')}
if($Estate){$checks+=@('estate','estate-reload')}
if($Startup){$checks+=@('startup-settings','startup-settings-blocked')}
if($StorageStartup){$checks+=@('storage-startup','storage-startup-reload','storage-startup-exit')}
if($PresentationOnly){$checks=@('release-smoke','presentation')}
if($MainlineOnly){$checks=@('release-smoke','mainline','combat','creature','equipment')}
if($ExplorationOnly){$checks=@('release-smoke','exploration','mainline','combat','creature','equipment','presentation','save-ui-headless')}
if($Natural){$checks+='surface'}
if($Lighting){$checks+='lighting'}
if($JumpPlacement){$checks+=@('jump-placement','building-headless')}
if($Expansion){$checks+='expansion'}
foreach($check in $checks){
    $record=Get-Content -LiteralPath (Join-Path $root "test-results/$OutputName-$check.verification.json") -Raw | ConvertFrom-Json
    if($record.status -ne 'passed'){throw "Unfinished or failed release check: $check"}
    foreach($artifact in $record.artifacts){if((Get-FileHash -LiteralPath $artifact.Path -Algorithm SHA256).Hash -ne $artifact.Hash){throw "Artifact changed after $check : $($artifact.Path)"}}
}
$files=@(Get-ChildItem -LiteralPath $build -File | Where-Object Extension -in '.exe','.pck','.md','.txt','.csv')
$files+=@(Get-ChildItem -LiteralPath (Join-Path $build 'data_EmberLighthouse_windows_x86_64') -File -Recurse)
if($MainlineOnly -or $ExplorationOnly){$files+=@(Get-ChildItem -LiteralPath (Join-Path $build 'previews') -File -Recurse)}
$expected=@{}
foreach($file in $files){$relative=[IO.Path]::GetRelativePath($build,$file.FullName).Replace('\','/');$expected[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
$verified=[Collections.Generic.List[object]]::new()
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{
    $entries=@($zip.Entries | Where-Object {$_.Name})
    if($entries.Count -ne $expected.Count){throw 'ZIP file count differs from the frozen release'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($entry in $entries){
        $name=$entry.FullName.Replace('\','/')
        if(!$seen.Add($name) -or !$expected.ContainsKey($name) -or $name -match '(^|/)test-results/'){throw "Unexpected or duplicate ZIP entry: $name"}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$sha.Dispose();$stream.Dispose()}
        if($hash -ne $expected[$name]){throw "ZIP content differs: $name"}
        $verified.Add([ordered]@{file=$name;sha256=$hash})
    }
}finally{$zip.Dispose()}
$documents=@('运行说明.md','试玩流程.md','耐久测试报告.md','试玩记录.csv','性能记录.csv')
if($Startup){$documents+='原生启动故障诊断.md'}
if($PresentationOnly){$documents+=@('成品体验更新.md','试玩反馈修复-2026-10-11.md')}
if($MainlineOnly -or $ExplorationOnly){$documents+='三塔归火更新.md'}
if($ExplorationOnly){$documents+='探索体验更新-2026-10-12.md'}
if($Natural){$documents+='自然地表与永久删除更新-2026-10-12.md'}
if($Lighting){$documents+='光照与掉落更新-2026-10-12.md'}
if($JumpPlacement){$documents+='跳跃放块修复-2026-10-12.md'}
if($Expansion){$documents+='物品生物与护甲更新-2026-10-12.md'}
foreach($document in $documents){
    if((Get-FileHash -LiteralPath (Join-Path $root "docs/$document")).Hash -ne $expected[$document]){throw "Packaged document is outdated: $document"}
}
$result=[ordered]@{passed=$true;recordedUtc=[DateTime]::UtcNow;archive=$archive;zipSha256=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $archive).Length;files=$verified.Count;testResultsExcluded=$true;releaseChecks=$checks;presentationOnly=[bool]$PresentationOnly;fullRegression=$false;artifacts=$verified}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root "test-results/$OutputName-package-verification.json") -Encoding utf8
Write-Output "PACKAGE_PASS: $($verified.Count) files verified, $($checks.Count) frozen release checks, current documents; SHA256 $($result.zipSha256)"


