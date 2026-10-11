param([switch]$Visual,[switch]$SaveUi,[switch]$SaveUiHeadless,[switch]$Building,[switch]$BuildingHeadless,[switch]$Audio,[switch]$Interaction,[switch]$Creature,[switch]$Combat,[switch]$Route,[switch]$Boundary,[switch]$CreaturePath,[switch]$Feedback,[switch]$FeedbackReload,[switch]$Equipment,[switch]$EquipmentReload,[switch]$Estate,[switch]$EstateReload,[switch]$StorageStartup,[switch]$StorageStartupReload,[switch]$StorageStartupExit,[switch]$Presentation,[switch]$Interactive,[int]$SoakSeconds=0,[switch]$Wait,[switch]$WeatherStress,[switch]$Uncapped,[switch]$GroundSoak,[switch]$GcStress,[ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName='EmberLighthouse')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
if(($Visual -or $SaveUi -or $Building -or $Audio -or $SoakSeconds -gt 0) -and !$Interactive){throw 'Graphical validation can create a desktop window. Use -Interactive only during an explicitly scheduled desktop test. Headless smoke and interaction checks do not need it.'}
$env:APPDATA=Join-Path $root '.tools/release-appdata'
$env:LOCALAPPDATA=Join-Path $root '.tools/release-localappdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA,(Join-Path $root 'test-results') | Out-Null
Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue
$engine=Join-Path $root "builds/$OutputName/EmberLighthouse.exe"
$arguments=@('--headless','--','--smoke');$name='release-smoke'
if($Visual){$arguments=@('--position','-10000,-10000','--','--visual-check');$name='visual'}
if($SaveUi){$arguments=@('--position','-10000,-10000','--','--save-ui-check');$name='save-ui'}
if($SaveUiHeadless){$arguments=@('--headless','--','--save-ui-check');$name='save-ui-headless'}
if($Building){$arguments=@('--position','-10000,-10000','--','--building-check');$name='building'}
if($BuildingHeadless){$arguments=@('--headless','--','--building-check');$name='building-headless'}
if($Audio){$arguments=@('--position','-10000,-10000','--','--audio-check');$name='audio'}
if($Interaction){$arguments=@('--headless','--','--interaction-check');$name='interaction'}
if($Creature){$arguments=@('--headless','--','--creature-check');$name='creature'}
if($Combat){$arguments=@('--headless','--','--combat-check');$name='combat'}
if($Route){$arguments=@('--headless','--','--route-check');$name='route'}
if($Boundary){$arguments=@('--headless','--','--boundary-check');$name='boundary'}
if($CreaturePath){$arguments=@('--headless','--','--creature-path-check');$name='creature-path'}
if($Feedback){$arguments=@('--headless','--','--feedback-check');$name='feedback'}
if($FeedbackReload){$arguments=@('--headless','--','--feedback-check','--feedback-reload');$name='feedback-reload'}
if($Equipment){$arguments=@('--headless','--','--equipment-check');$name='equipment'}
if($EquipmentReload){$arguments=@('--headless','--','--equipment-check','--equipment-reload');$name='equipment-reload'}
if($Estate){$arguments=@('--headless','--','--estate-check');$name='estate'}
if($EstateReload){$arguments=@('--headless','--','--estate-check','--estate-reload');$name='estate-reload'}
if($StorageStartup){$arguments=@('--headless','--','--storage-startup-check');$name='storage-startup'}
if($StorageStartupReload){$arguments=@('--headless','--','--storage-startup-check','--storage-startup-reload');$name='storage-startup-reload'}
if($StorageStartupExit){$arguments=@('--headless','--','--storage-startup-check','--storage-startup-exit');$name='storage-startup-exit'}
if($Presentation){$arguments=@('--headless','--','--presentation-check');$name='presentation'}
if($SoakSeconds -gt 0){$arguments=@('--position','-10000,-10000','--resolution','1920x1080','--','--soak',"--duration=$SoakSeconds");$name='soak'}
if($WeatherStress){$arguments+='--weather-stress'}
if($Uncapped){$arguments+='--uncapped'}
if($GroundSoak){$arguments+='--ground-soak'}
if($GcStress){$arguments+='--gc-stress'}
if($OutputName -ne 'EmberLighthouse'){$name="$OutputName-$name"}
$startedUtc=[DateTime]::UtcNow
$artifacts=@($engine,(Join-Path $root "builds/$OutputName/EmberLighthouse.pck"),(Join-Path $root "builds/$OutputName/data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll"))
$initialHashes=@(Get-FileHash -LiteralPath $artifacts -Algorithm SHA256 | Select-Object Path,Hash)
$signal=if($SoakSeconds -gt 0){'SOAK_PASS'}elseif($EstateReload){'ESTATE_RELOAD_PASS'}elseif($Estate){'ESTATE_PASS'}elseif($EquipmentReload){'EQUIPMENT_RELOAD_PASS'}elseif($Equipment){'EQUIPMENT_PASS'}elseif($FeedbackReload){'FEEDBACK_RELOAD_PASS'}elseif($Feedback){'FEEDBACK_PASS'}elseif($CreaturePath){'CREATURE_PATH_PASS'}elseif($Boundary){'BOUNDARY_PASS'}elseif($Route){'ROUTE_PASS'}elseif($Combat){'COMBAT_PASS'}elseif($Creature){'CREATURE_PASS'}elseif($Interaction){'INTERACTION_PASS'}elseif($Audio){'AUDIO_PASS'}elseif($Building -or $BuildingHeadless){'BUILDING_PASS'}elseif($SaveUi -or $SaveUiHeadless){'SAVE_UI_PASS'}else{'SMOKE_PASS'}
if($StorageStartupExit){$signal='STORAGE_STARTUP_EXIT_PASS'}elseif($StorageStartupReload){$signal='STORAGE_STARTUP_RELOAD_PASS'}elseif($StorageStartup){$signal='STORAGE_STARTUP_PASS'}
if($Presentation){$signal='PRESENTATION_PASS'}
function Write-Verification([string]$status,[string]$reason){
    [ordered]@{status=$status;reason=$reason;startedUtc=$startedUtc;recordedUtc=[DateTime]::UtcNow;pid=$p.Id;arguments=$arguments;artifacts=$initialHashes;referenceHardware=$false} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root "test-results/$name.verification.json") -Encoding utf8
}
function Finish-Validation {
    $errorText=Get-Content -LiteralPath (Join-Path $root "test-results/$name.err.log") -Raw
    $output=Get-Content -LiteralPath (Join-Path $root "test-results/$name.out.log") -Raw
    $reason=''
    if($p.ExitCode -ne 0){$reason="Process exit: $($p.ExitCode)"}
    elseif($errorText -match 'ERROR:|ObjectDB instances leaked at exit'){$reason="Engine error or resource leak: $errorText"}
    elseif($output -notmatch [regex]::Escape($signal)){$reason="Missing completion signal: $signal"}
    else{
        $finalHashes=@(Get-FileHash -LiteralPath $artifacts -Algorithm SHA256 | Select-Object Path,Hash)
        for($i=0;$i -lt $initialHashes.Count;$i++){if($initialHashes[$i].Hash -ne $finalHashes[$i].Hash){$reason='Build artifacts changed during validation';break}}
    }
    if($reason){Write-Verification 'failed' $reason;throw $reason}
    Write-Verification 'passed' 'Process exited successfully, expected signal present, no engine errors or resource leaks, artifact hashes unchanged'
    Write-Output $output
}
if(!('EmberLighthouseValidationLaunch' -as [type])){
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class EmberLighthouseValidationLaunch {
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
}
'@
}
# Child processes inherit this mode. Keep native failures in exit codes/logs,
# avoiding a Windows error dialog that can hang an unattended headless check.
# https://learn.microsoft.com/windows/win32/api/errhandlingapi/nf-errhandlingapi-seterrormode
$previousErrorMode=[EmberLighthouseValidationLaunch]::GetErrorMode()
try{
    [void][EmberLighthouseValidationLaunch]::SetErrorMode($previousErrorMode -bor 0x8003)
    $p=Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root "test-results/$name.out.log") -RedirectStandardError (Join-Path $root "test-results/$name.err.log") -PassThru
}finally{[void][EmberLighthouseValidationLaunch]::SetErrorMode($previousErrorMode)}
Write-Verification 'running' 'Awaiting process completion'
if($SoakSeconds -gt 0){$p.Id | Set-Content -LiteralPath (Join-Path $root "test-results/$name-pid.txt");Write-Output "Soak running: PID $($p.Id), $SoakSeconds seconds"
    if($Wait){$p.WaitForExit();Finish-Validation}
    return
}
$timeout=if($Route -or $Boundary){600000}elseif($CreaturePath -or $Equipment -or $Estate){180000}else{60000}
if(!$p.WaitForExit($timeout)){Write-Verification 'running-timeout' 'Observation timed out; process has not been confirmed stopped';throw 'Release validation timed out'}
Finish-Validation

