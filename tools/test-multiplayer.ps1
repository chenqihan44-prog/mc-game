param([string]$Engine='', [ValidateRange(1024,65535)][int]$Port=24567,[switch]$Packaged)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Engine){$Engine=Join-Path $root '.tools/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe'}
if(!(Test-Path -LiteralPath $Engine)){throw "Godot executable missing: $Engine"}
$run='runtime-'+[Guid]::NewGuid().ToString('N')
$directory=Join-Path $root "test-results/multiplayer-check/$run"
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$env:DOTNET_ROOT=Join-Path $root '.tools/dotnet'
if(Test-Path -LiteralPath (Join-Path $root '.tools/sdk-path.txt')){$env:DOTNET_ROOT=(Get-Content -LiteralPath (Join-Path $root '.tools/sdk-path.txt') -Raw).Trim()}
if($Packaged){Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue}else{$env:PATH="$env:DOTNET_ROOT;$env:PATH"}
$env:APPDATA=Join-Path $directory 'appdata'
$env:LOCALAPPDATA=Join-Path $directory 'localappdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$started=@()
$roleProcesses=@{}
$watch=[Diagnostics.Stopwatch]::StartNew()
function Start-ValidationRole([string]$Role){
    $arguments=@('--headless')
    if(!$Packaged){$arguments+=@('--path',('"'+$root+'"'))}
    $arguments+=@('--','--multiplayer-check','--multiplayer-role',$Role,'--multiplayer-run',$run,'--multiplayer-port',$Port,'--multiplayer-report-dir',('"'+$directory+'"'))
    Start-Process -FilePath $Engine -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory "$Role.out.log") -RedirectStandardError (Join-Path $directory "$Role.err.log") -PassThru
}
try{
    $hostProcess=Start-ValidationRole 'host';$started+=$hostProcess;$roleProcesses['host']=$hostProcess
    while(!(Test-Path -LiteralPath (Join-Path $directory 'host-ready.json'))){
        if($hostProcess.HasExited){throw "Host exited before listening: $(Get-Content -LiteralPath (Join-Path $directory 'host.err.log') -Raw)"}
        if($watch.Elapsed.TotalSeconds -gt 40){throw 'Host did not begin listening within 40 seconds'}
        Start-Sleep -Milliseconds 200
    }
    foreach($role in @('observer1','observer2','client')){$process=Start-ValidationRole $role;$started+=$process;$roleProcesses[$role]=$process}
    while(@($started | Where-Object {!$_.HasExited}).Count -gt 0){
        if($watch.Elapsed.TotalSeconds -gt 210){throw "Multiplayer runtime timeout; reports: $directory"}
        Start-Sleep -Milliseconds 250
    }
    $reports=@()
    $environmentDiagnostics=@{}
    $failures=@()
    # Only this exact two-line Windows startup diagnostic is environmental.
    # Native shutdown errors, unmanaged exceptions and resource leaks stay fatal.
    $certificateDiagnostic='(?m)^ERROR: Failed to read the root certificate store\.\r?\n[ \t]+at: get_system_ca_certificates \(platform/windows/os_windows\.cpp:2570\)\r?(?:\n|$)'
    foreach($role in @('host','client','observer1','observer2')){
        $reportPath=Join-Path $directory "$role-validation.json"
        $report=$null
        if(Test-Path -LiteralPath $reportPath){$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json}else{$failures+="$role did not write a runtime report"}
        $process=$roleProcesses[$role]
        $process.WaitForExit()
        $errors=[string](Get-Content -LiteralPath (Join-Path $directory "$role.err.log") -Raw)
        $diagnostics=[regex]::Matches($errors,$certificateDiagnostic)
        $environmentDiagnostics[$role]=[ordered]@{rootCertificateStoreReadFailures=$diagnostics.Count;messages=@($diagnostics | ForEach-Object {$_.Value.TrimEnd()});reason='Windows system certificate-store access unavailable in the sandbox'}
        $remainingErrors=[regex]::Replace($errors,$certificateDiagnostic,'')
        if(!$report -or !$report.passed -or $process.ExitCode -ne 0 -or $remainingErrors -match 'ERROR:|SEHException|\bleaked\b|FATAL:'){$failures+="$role failed (exit $($process.ExitCode), stage $($report.stage)): $($report.error); engine: $remainingErrors"}
        if($report){$reports+=$report}
    }
    $result=[ordered]@{passed=($failures.Count -eq 0);recordedUtc=[DateTime]::UtcNow;actualProcesses=4;actualEnet=$true;packaged=[bool]$Packaged;engine=$Engine;port=$Port;runDirectory=$directory;environmentDiagnostics=$environmentDiagnostics;failures=$failures;reports=$reports}
    $aggregate=$result | ConvertTo-Json -Depth 10
    $aggregate | Set-Content -LiteralPath (Join-Path $directory 'multiplayer.verification.json') -Encoding utf8
    $aggregate | Set-Content -LiteralPath (Join-Path $root 'test-results/multiplayer.verification.json') -Encoding utf8
    if($failures.Count){throw ($failures -join "`n")}
    Write-Output "MULTIPLAYER_RUNTIME_PASS: host + three clients, mutually visible models, placement, isolated pickup, remote UI callbacks, durable save and reconnect; $directory"
}finally{
    foreach($process in $started){if(!$process.HasExited){Stop-Process -Id $process.Id -Force;$process.WaitForExit(10000) | Out-Null}}
    $exitCodes=[ordered]@{}
    foreach($role in @('host','client','observer1','observer2')){if($roleProcesses.ContainsKey($role)){$process=$roleProcesses[$role];$process.Refresh();if($process.HasExited){$process.WaitForExit();$exitCodes[$role]=[ordered]@{pid=$process.Id;exitCode=$process.ExitCode}}}}
    $exitCodes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'exit-codes.json') -Encoding utf8
}
