param(
    [string]$NewAssembly='builds/EmberLighthouse-presentation/data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll',
    [string]$SourceRoot='tests/fixtures/published-format3/worlds',
    [ValidatePattern('^[a-fA-F0-9]{32}$')][string]$WorldId='860b32146dd04be2b2e1aba37057c630'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Join-Path (Join-Path $root $SourceRoot) $WorldId
$trialRoot=Join-Path $root "test-results/published-format3-migration/$([Guid]::NewGuid().ToString('N'))/worlds"
New-Item -ItemType Directory -Force -Path $trialRoot | Out-Null
$sourceHashes=@(Get-ChildItem -LiteralPath $source -File -Recurse | Get-FileHash -Algorithm SHA256)
Copy-Item -LiteralPath $source -Destination (Join-Path $trialRoot $WorldId) -Recurse
$path=Join-Path $trialRoot $WorldId
$pointer=Get-Content -LiteralPath (Join-Path $path 'CURRENT') -Raw | ConvertFrom-Json
$rawMetadata=Join-Path $path "$($pointer.Generation).json"
$oldState=Get-Content -LiteralPath $rawMetadata -Raw | ConvertFrom-Json
if($oldState.FormatVersion -ne 3 -or $oldState.PSObject.Properties.Name -contains 'Equipment'){throw 'Expected a genuinely published format3 world without equipment fields'}
$assemblyPath=[IO.Path]::GetFullPath((Join-Path $root $NewAssembly))
$assembly=[Reflection.Assembly]::LoadFrom($assemblyPath)
$catalogType=$assembly.GetType('EmberLighthouse.Items.Catalog')
$catalog=$catalogType.GetMethod('Load').Invoke($null,[object[]]@([string](Join-Path $root 'data')))
$storeType=$assembly.GetType('EmberLighthouse.Persistence.SaveStore')
$store=$storeType.GetConstructor([Type[]]@([string],$catalogType)).Invoke([object[]]@([string]$trialRoot,$catalog.PSObject.BaseObject))
$rawHash=(Get-FileHash -LiteralPath $rawMetadata).Hash
$pointerHash=(Get-FileHash -LiteralPath (Join-Path $path 'CURRENT')).Hash
$loaded=$storeType.GetMethod('Load').Invoke($store,[object[]]@($WorldId))
if(!$loaded.Migrated -or $loaded.Snapshot.FormatVersion -ne 4 -or $loaded.Snapshot.Equipment.Slots.Length -ne 5 -or @($loaded.Snapshot.Equipment.Slots | Where-Object {$null -ne $_}).Count -ne 0){throw 'Published legacy world did not migrate to empty equipment slots'}
if((Get-FileHash -LiteralPath $rawMetadata).Hash -ne $rawHash -or (Get-FileHash -LiteralPath (Join-Path $path 'CURRENT')).Hash -ne $pointerHash){throw 'Loading rewrote the original generation'}
$null=$storeType.GetMethod('Save').Invoke($store,[object[]]@($loaded.Snapshot.PSObject.BaseObject,$null))
$backupRoot=Join-Path (Split-Path $trialRoot -Parent) 'backups'
$backups=@(Get-ChildItem -LiteralPath $backupRoot -Directory)
if($backups.Count -ne 1){throw 'First format4 save did not create exactly one legacy backup'}
$backup=$backups[0].FullName
$legacyFiles=@("$($pointer.Generation).json","$($pointer.Generation).commit")
$legacyFiles+=@($pointer.Chunks.PSObject.Properties | ForEach-Object {"chunks/$($_.Value.Hash).chunk"} | Select-Object -Unique)
foreach($relative in $legacyFiles){$original=Join-Path $path $relative;$backupFile=Join-Path $backup $relative;if(!(Test-Path -LiteralPath $backupFile) -or (Get-FileHash -LiteralPath $backupFile).Hash -ne (Get-FileHash -LiteralPath $original).Hash){throw 'Raw legacy backup missing or different'}}
$saved=$storeType.GetMethod('Load').Invoke($store,[object[]]@($WorldId))
if($saved.Snapshot.FormatVersion -ne 4 -or $saved.Snapshot.Inventory.Slots.Length -ne 36 -or $saved.Snapshot.Changes.Count -ne $loaded.Snapshot.Changes.Count){throw 'Published world changed gameplay during save'}
foreach($key in $loaded.Snapshot.Changes.Keys){if($saved.Snapshot.Changes[$key] -ne $loaded.Snapshot.Changes[$key]){throw 'Published terrain edit changed'}}
for($i=0;$i -lt 36;$i++){$a=$saved.Snapshot.Inventory.Slots[$i];$b=$loaded.Snapshot.Inventory.Slots[$i];if(($null -eq $a) -ne ($null -eq $b)){throw 'Published item disappeared'};if($null -ne $a -and ($a.Id -ne $b.Id -or $a.Count -ne $b.Count -or $a.Durability -ne $b.Durability)){throw 'Published inventory changed'}}
foreach($hash in $sourceHashes){if((Get-FileHash -LiteralPath $hash.Path).Hash -ne $hash.Hash){throw 'Published source world was modified'}}
$record=[ordered]@{passed=$true;assembly=$assemblyPath;dllSha256=(Get-FileHash -LiteralPath $assemblyPath).Hash;sourceWorld=$source;copiedWorld=$path;sourceMetadataFormat=3;newMetadataFormat=4;legacyEquipmentAbsent=$true;emptyNewSlots=5;loadReadOnly=$true;firstSaveRawBackup=$true;originalSourceUnchanged=$true;modifiedCells=$saved.Snapshot.Changes.Count;backup=$backup}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'test-results/published-format3-migration.json') -Encoding utf8
$record | ConvertTo-Json
