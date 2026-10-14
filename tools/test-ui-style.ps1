param([string]$Engine = '', [switch]$Packaged, [switch]$CoopFeedbackOnly, [ValidateRange(30,600)][int]$TimeoutSeconds = 270)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Engine) { $Engine = Join-Path $root '.tools/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe' }
if (!(Test-Path -LiteralPath $Engine)) { throw "Godot executable missing: $Engine" }
$run = 'runtime-' + [Guid]::NewGuid().ToString('N')
$directory = Join-Path $root "test-results/ui-style/$run"
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$oldEnvironment = @{}
foreach ($name in @('DOTNET_ROOT','PATH','APPDATA','LOCALAPPDATA')) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$process = $null
try {
    $env:DOTNET_ROOT = Join-Path $root '.tools/dotnet'
    $sdkPath = Join-Path $root '.tools/sdk-path.txt'
    if (Test-Path -LiteralPath $sdkPath) { $env:DOTNET_ROOT = (Get-Content -LiteralPath $sdkPath -Raw).Trim() }
    if ($Packaged) { Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue }
    else { $env:PATH = "$env:DOTNET_ROOT;$env:PATH" }
    $env:APPDATA = Join-Path $directory 'appdata'
    $env:LOCALAPPDATA = Join-Path $directory 'localappdata'
    New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
    # Native rendering is required for viewport images and actual Control layout.
    $arguments = @('--rendering-method','gl_compatibility')
    if (!$Packaged) { $arguments += @('--path', ('"' + $root + '"')) }
    $arguments += @('--','--ui-style-check','--ui-style-report-dir', ('"' + $directory + '"'))
    if ($CoopFeedbackOnly) { $arguments += '--coop-feedback-only' }
    $stdout = Join-Path $directory 'ui.out.log'
    $stderr = Join-Path $directory 'ui.err.log'
    $process = Start-Process -FilePath $Engine -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (!$process.HasExited) {
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw "UI runtime timeout; artifacts: $directory" }
        Start-Sleep -Milliseconds 250
    }
    $process.WaitForExit()
    $reportPath = Join-Path $directory 'ui-style-validation.json'
    $report = $null
    $failures = @()
    if (Test-Path -LiteralPath $reportPath) { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json }
    else { $failures += 'Native GUI process did not write its validation report' }
    $errors = [string](Get-Content -LiteralPath $stderr -Raw)
    # Preserve this exact sandbox certificate diagnostic separately. Other native
    # errors, exceptions, leaked references and RIDs always fail verification.
    $certificateDiagnostic = '(?m)^ERROR: Failed to read the root certificate store\.\r?\n[ \t]+at: get_system_ca_certificates \(platform/windows/os_windows\.cpp:2570\)\r?(?:\n|$)'
    $diagnostics = [regex]::Matches($errors,$certificateDiagnostic)
    $remainingErrors = [regex]::Replace($errors,$certificateDiagnostic,'')
    if ($process.ExitCode -ne 0) { $failures += "Native GUI exit code $($process.ExitCode)" }
    if (!$report -or !$report.passed) { $failures += "GUI assertions failed at $($report.stage): $($report.error)" }
    if ($remainingErrors -match 'ERROR:|SEHException|\bleaked\b|FATAL:') { $failures += "Native runtime errors: $remainingErrors" }
    if ($report -and (!$report.nativeRendering -or $report.userProfilesTouched)) { $failures += 'Native rendering or profile isolation assertion failed' }
    $pngFiles = @(Get-ChildItem -LiteralPath $directory -Filter '*.png' -Recurse -File)
    $minimumScreenshots = if ($CoopFeedbackOnly) { 37 } else { 48 }
    if ($pngFiles.Count -lt $minimumScreenshots) { $failures += "Expected at least $minimumScreenshots rendered menu screenshots; found $($pngFiles.Count)" }
    $result = [ordered]@{passed=($failures.Count -eq 0);recordedUtc=[DateTime]::UtcNow;engine=$Engine;packaged=[bool]$Packaged;
        nativeRendering=$true;coopFeedbackOnly=[bool]$CoopFeedbackOnly;runDirectory=$directory;exitCode=$process.ExitCode;screenshotCount=$pngFiles.Count;
        environmentDiagnostics=[ordered]@{rootCertificateStoreReadFailures=$diagnostics.Count;messages=@($diagnostics | ForEach-Object {$_.Value.TrimEnd()})};
        failures=$failures;report=$report}
    $json = $result | ConvertTo-Json -Depth 12
    $json | Set-Content -LiteralPath (Join-Path $directory 'ui-style.verification.json') -Encoding utf8
    $json | Set-Content -LiteralPath (Join-Path $root 'test-results/ui-style.verification.json') -Encoding utf8
    if ($failures.Count) { throw ($failures -join "`n") }
    $scope = if ($CoopFeedbackOnly) { 'remote menu layout, real guest callbacks, articulated crouch and live player map markers' } else { 'native menus, remote menu layout, guest callbacks, shortcuts, isolated saves, articulated crouch and player map markers' }
    Write-Output "UI_STYLE_RUNTIME_PASS: $scope; $directory"
} finally {
    if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(10000) | Out-Null }
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$oldEnvironment[$name],'Process') }
}
