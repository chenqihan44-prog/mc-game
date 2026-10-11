param([string]$OldAssembly,[ValidateSet(2,3)][int]$OldFormat=2)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$OldAssembly){$OldAssembly="tests/fixtures/published-format$OldFormat/EmberLighthouse.dll"}
$fixture=Get-Content -LiteralPath (Join-Path $root 'test-results/format2-compat-path.json') -Raw | ConvertFrom-Json
$assemblyPath=[IO.Path]::GetFullPath((Join-Path $root $OldAssembly))
$legacy=[Reflection.Assembly]::LoadFrom($assemblyPath)
$snapshotType=$legacy.GetType('EmberLighthouse.World.WorldSnapshot')
if($snapshotType.GetField('CurrentFormat').GetRawConstantValue() -ne $OldFormat){throw 'Compatibility check requires the matching actual old assembly'}
$catalogType=$legacy.GetType('EmberLighthouse.Items.Catalog')
$catalog=$catalogType.GetMethod('Load').Invoke($null,[object[]]@([string](Join-Path $root 'data')))
$storeType=$legacy.GetType('EmberLighthouse.Persistence.SaveStore')
$store=[Activator]::CreateInstance($storeType,[object[]]@([string]$fixture.storeRoot,$catalog.PSObject.BaseObject))
$pointer=Join-Path $fixture.storeRoot "$($fixture.worldId)/CURRENT"
$before=(Get-FileHash -LiteralPath $pointer -Algorithm SHA256).Hash
function Expect-Unsupported([scriptblock]$Run){
    try{$null=& $Run}catch{
        $failure=$_.Exception
        while($failure){if($failure.GetType().FullName -eq 'EmberLighthouse.Persistence.UnsupportedSaveException'){return};$failure=$failure.InnerException}
        throw
    }
    throw "Actual old assembly accepted a format$($fixture.metadataFormat) world"
}
Expect-Unsupported { $storeType.GetMethod('Load').Invoke($store,[object[]]@([string]$fixture.worldId)) }
$snapshot=[Activator]::CreateInstance($snapshotType)
$snapshotType.GetProperty('WorldId').SetValue($snapshot,[string]$fixture.worldId)
Expect-Unsupported { $storeType.GetMethod('Save').Invoke($store,[object[]]@($snapshot.PSObject.BaseObject,$null)) }
if((Get-FileHash -LiteralPath $pointer -Algorithm SHA256).Hash -ne $before){throw 'Old writer changed new CURRENT pointer'}
$record=[ordered]@{passed=$true;oldAssembly=$assemblyPath;oldDllSha256=(Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash;oldFormat=$OldFormat;newMetadataFormat=$fixture.metadataFormat;actualOldLoadRejected=$true;actualOldSaveRejected=$true;pointerUnchanged=$true;fixtureWorldId=$fixture.worldId}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root "test-results/format$OldFormat-reader-rejection.json") -Encoding utf8
$record | ConvertTo-Json
