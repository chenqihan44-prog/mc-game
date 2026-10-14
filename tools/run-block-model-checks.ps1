param([string]$Engine='', [string[]]$Checks=@('creature-motion-check','combat-check','smoke'), [switch]$Packaged, [switch]$Capture, [string]$Label='accepted')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=Join-Path $root '.tools/block-model-replacement-20261013'
New-Item -ItemType Directory -Force -Path $work | Out-Null
if(!$Engine){$Engine=Join-Path $root '.tools/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe'}
if(!$Packaged){$env:DOTNET_ROOT=(Get-Content (Join-Path $root '.tools/sdk-path.txt') -Raw).Trim();$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH}else{Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue}
$markers=@{'nature-check'='NATURE_FEEDBACK_PASS';'lighting-check'='LIGHTING_PASS';'creature-motion-check'='CREATURE_MOTION_PASS';'combat-check'='COMBAT_PASS';'smoke'='SMOKE_PASS';'expansion-check'='EXPANSION_PASS';'creature-path-check'='CREATURE_PATH_PASS';'creature-check'='CREATURE_PASS'}
$results=@()
foreach($check in $Checks){
 if(!$markers.ContainsKey($check)){throw "Unsupported check: $check"}
 $caseName="$Label-$check"
 $env:APPDATA=Join-Path $work "runtime/$caseName/appdata";$env:LOCALAPPDATA=Join-Path $work "runtime/$caseName/localappdata"
 New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
 [string[]]$arguments=@('--headless')
 if($Capture){$arguments=@('--position','-10000,-10000','--resolution','1280x720')}
 if(!$Packaged){$arguments+=@('--path',('"'+$root+'"'))}
 $arguments+=@('--',"--$check");if($Capture){$arguments+='--creature-motion-capture'}
 $stdout=Join-Path $work "$caseName.stdout.log";$stderr=Join-Path $work "$caseName.stderr.log"
 $watch=[Diagnostics.Stopwatch]::StartNew()
 $process=Start-Process -FilePath $Engine -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
 try{
  while(!$process.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt 210){throw "Check timed out: $caseName"}}
  $process.WaitForExit()
  $outText=Get-Content $stdout -Raw;$errorText=[string](Get-Content $stderr -Raw)
  $certificateDiagnostic='(?m)^ERROR: Failed to read the root certificate store\.\r?\n[ \t]+at: get_system_ca_certificates \(platform/windows/os_windows\.cpp:2570\)\r?(?:\n|$)'
  $errors=[regex]::Replace($errorText,$certificateDiagnostic,'')
  if($process.ExitCode -ne 0 -or !$outText.Contains($markers[$check]) -or $errors -match 'ERROR:|SEHException|\bleaked\b|FATAL:'){
   throw "Check failed: $caseName, exit=$($process.ExitCode)`n$errors`n$outText"
  }
  $results+=@{check=$check;passed=$true;exitCode=$process.ExitCode;seconds=$watch.Elapsed.TotalSeconds;engine=$Engine;packaged=[bool]$Packaged;capture=[bool]$Capture;stdout=$stdout;stderr=$stderr}
  Write-Output "$caseName PASS exit=$($process.ExitCode)"
 }finally{if(!$process.HasExited){Stop-Process -Id $process.Id;$process.WaitForExit(10000) | Out-Null}}
}
$results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $work "$Label-checks.json") -Encoding UTF8
