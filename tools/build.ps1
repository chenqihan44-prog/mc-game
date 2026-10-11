param([switch]$Test,[switch]$Export,[ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputName='EmberLighthouse')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=Join-Path $root "builds/$OutputName"
Set-Location -LiteralPath $root
$env:DOTNET_ROOT=Join-Path $root '.tools/dotnet'
if(Test-Path -LiteralPath (Join-Path $root '.tools/sdk-path.txt')){$env:DOTNET_ROOT=(Get-Content -LiteralPath (Join-Path $root '.tools/sdk-path.txt') -Raw).Trim()}
$env:DOTNET_CLI_HOME=Join-Path $root '.tools/cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
# Avoid a persistent compiler inheriting Godot's redirected export pipe.
$env:UseSharedCompilation='false'
$env:NUGET_PACKAGES=Join-Path $root '.tools/nuget'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
$env:APPDATA=Join-Path $root '.tools/appdata'
$env:LOCALAPPDATA=Join-Path $root '.tools/localappdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$dotnet=Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$godot=Join-Path $root '.tools/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe'
& $dotnet build EmberLighthouse.csproj -c Debug
if($LASTEXITCODE -ne 0){throw 'Game build failed'}
if($Test){& $dotnet run --project tests/CoreTests.csproj;if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
    & $godot --headless --path $root --editor --import --quit
    if($LASTEXITCODE -ne 0){throw 'Godot import failed'}
    & $godot --headless --path $root -- --smoke
    if($LASTEXITCODE -ne 0){throw 'Runtime smoke test failed'}
}
if($Export){New-Item -ItemType Directory -Force -Path $release | Out-Null
    $output=& $godot --headless --path $root --export-release 'Windows Desktop' (Join-Path $release 'EmberLighthouse.exe') 2>&1
    $output | Set-Content -LiteralPath (Join-Path $root 'test-results/export-engine.log')
    if($LASTEXITCODE -ne 0 -or ($output | Select-String '^ERROR:')){throw 'Export failed; see test-results/export-engine.log'}
    if(!(Test-Path (Join-Path $release 'data_EmberLighthouse_windows_x86_64/EmberLighthouse.dll'))){throw 'Exported C# assembly missing'}
    Copy-Item -LiteralPath (Join-Path $root 'assets/LICENSES.md'),(Join-Path $root 'assets/GODOT_LICENSE.txt'),(Join-Path $root 'assets/GODOT_COPYRIGHT.txt') -Destination $release
}
