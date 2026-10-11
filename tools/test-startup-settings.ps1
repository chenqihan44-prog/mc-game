param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName='EmberLighthouse-presentation',
    [switch]$BlockedUserData,
    [switch]$SafeLogOverride
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root "test-results/$OutputName-startup-settings"
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$env:APPDATA=Join-Path $directory 'appdata'
$env:LOCALAPPDATA=Join-Path $directory 'localappdata'
if($BlockedUserData){
    # A plain file cannot serve as a parent directory, even with full permissions.
    # Keep the obstruction entirely inside the test fixture; no system ACLs change.
    $env:APPDATA=Join-Path $directory 'blocked-parent'
    Set-Content -LiteralPath $env:APPDATA -Value 'controlled non-directory fixture'
    $blockedHash=(Get-FileHash -LiteralPath $env:APPDATA).Hash
}
New-Item -ItemType Directory -Force -Path $env:LOCALAPPDATA | Out-Null
if(!$BlockedUserData){New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null}
$engine=Join-Path $root "builds/$OutputName/EmberLighthouse.exe"
$arguments=@('--headless')
if($SafeLogOverride){$arguments+=@('--log-file',(Join-Path $directory 'explicit-safe-engine.log'))}
$arguments+=@('--','--startup-settings-check')
$stem=if($BlockedUserData){'blocked'}else{'normal'}
if($SafeLogOverride){$stem+='-safe-log'}
$stdout=Join-Path $directory "$stem.out.log"
$stderr=Join-Path $directory "$stem.err.log"
if(!('EmberLighthouseStartupLaunch' -as [type])){
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class EmberLighthouseStartupLaunch {
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
}
'@
}
$hashes=@(Get-FileHash -LiteralPath $engine,(Join-Path $root "builds/$OutputName/EmberLighthouse.pck"),(Join-Path $root "builds/$OutputName/data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll"))
$previous=[EmberLighthouseStartupLaunch]::GetErrorMode()
try{
    [void][EmberLighthouseStartupLaunch]::SetErrorMode($previous -bor 0x8003)
    $p=Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
}finally{[void][EmberLighthouseStartupLaunch]::SetErrorMode($previous)}
if(!$p.WaitForExit(60000)){throw "Startup observation timed out; PID $($p.Id) not confirmed stopped"}
$text=Get-Content -LiteralPath $stdout -Raw
$errors=Get-Content -LiteralPath $stderr -Raw
if($p.ExitCode -ne 0 -or $errors -match 'ObjectDB instances leaked at exit'){throw "Startup check failed: exit $($p.ExitCode); $errors"}
$prefix='STARTUP_SETTINGS: '
$offset=$text.IndexOf($prefix,[StringComparison]::Ordinal)
if($offset -lt 0){throw "Startup settings report missing: $text"}
# Catalog's JSON options indent the report across several output lines.
$report=$text.Substring($offset+$prefix.Length).Trim() | ConvertFrom-Json
if(!$report.headless -or !$report.pcFeature -or $report.effectiveFileLogging){throw "Unexpected effective startup settings: $text"}
if($BlockedUserData){
    $blockedPrefix=[IO.Path]::GetFullPath($env:APPDATA).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if(![IO.Path]::GetFullPath($report.userDataDir).StartsWith($blockedPrefix,[StringComparison]::OrdinalIgnoreCase) -or ![IO.File]::Exists($env:APPDATA) -or [IO.Directory]::Exists($env:APPDATA)){throw 'Blocked userdata fixture did not obstruct the actual native userdata path'}
    if((Get-FileHash -LiteralPath $env:APPDATA).Hash -ne $blockedHash){throw 'Blocked parent fixture changed'}
    # This is a negative filesystem test, not a clean-startup claim. Require
    # exactly these two errors, with the actual obstructed paths and callsites.
    $godotParent=$env:APPDATA.Replace('\','/')+'/Godot'
    $expected='\AERROR: Could not create directory: '''+[regex]::Escape($godotParent)+'''\.\s+at: make_dir_recursive \(core/io/dir_access\.cpp:\d+\)\s+ERROR: Error attempting to create data dir: '+[regex]::Escape($report.userDataDir)+'\.\s+at: ensure_user_data_dir \(core/os/os\.cpp:\d+\)\s*\z'
    if($errors -notmatch $expected){throw "Unexpected errors in negative startup test: $errors"}
}elseif($errors -match 'ERROR:'){throw "Engine error in normal startup: $errors"}
foreach($artifact in $hashes){if((Get-FileHash -LiteralPath $artifact.Path).Hash -ne $artifact.Hash){throw 'Frozen artifact changed'}}
$reason=if($BlockedUserData){'Negative filesystem test: exactly two expected directory errors, actual path obstructed, process exited 0 without native crash or leaks, artifact hashes unchanged'}else{'Process exited 0, effective desktop file logging disabled, no engine errors or leaks, artifact hashes unchanged'}
$result=[ordered]@{passed=$true;status='passed';reason=$reason;recordedUtc=[DateTime]::UtcNow;pid=$p.Id;exitCode=$p.ExitCode;arguments=$arguments;referenceHardware=$false;negativeTest=[bool]$BlockedUserData;expectedEngineErrors=$(if($BlockedUserData){2}else{0});engineErrors=$errors;blockedUserData=[bool]$BlockedUserData;safeLogOverride=[bool]$SafeLogOverride;settings=$report;artifacts=@($hashes | Select-Object Path,Hash)}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory "$stem.verification.json") -Encoding utf8
$receiptName=if($BlockedUserData){"$OutputName-startup-settings-blocked"}else{"$OutputName-startup-settings"}
if($SafeLogOverride){$receiptName+='-safe-log'}
Copy-Item -LiteralPath (Join-Path $directory "$stem.verification.json") -Destination (Join-Path $root "test-results/$receiptName.verification.json")
Write-Output "STARTUP_SETTINGS_PASS: effectiveFileLogging=$($report.effectiveFileLogging); blockedUserData=$BlockedUserData; safeLogOverride=$SafeLogOverride"
