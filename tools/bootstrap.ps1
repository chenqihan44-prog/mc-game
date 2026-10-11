param([switch]$Templates)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$local = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $local | Out-Null
function Fetch($url, $file, $hash, $algorithm) {
    if (!(Test-Path -LiteralPath $file)) { Invoke-WebRequest $url -OutFile $file }
    if ((Get-FileHash -LiteralPath $file -Algorithm $algorithm).Hash.ToLowerInvariant() -ne $hash) {
        throw "Checksum mismatch: $file"
    }
}
$sdk = Join-Path $local 'dotnet-sdk-8.0.425.zip'
$sdkHome=Join-Path $local 'dotnet'
if(Test-Path -LiteralPath (Join-Path $local 'sdk-path.txt')){$sdkHome=(Get-Content -LiteralPath (Join-Path $local 'sdk-path.txt') -Raw).Trim()}
elseif($root -match '[^\x00-\x7F]'){$sdkHome=Join-Path ([System.IO.Path]::GetTempPath()) 'EmberLighthouseTools/dotnet-8.0.425'}
if (!(Test-Path (Join-Path $sdkHome 'dotnet.exe'))) {
    Fetch 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-win-x64.zip' $sdk 'f0b6f15bf6f1a0507205c0cb102ab99e1dee875c4682c8ed94665be1d580186a06b21455e83b3a01a0ff7f4cd887b67420f2e2fe09ed985534a4cea488ae1af9' 'SHA512'
    Expand-Archive -LiteralPath $sdk -DestinationPath $sdkHome -Force
}
Set-Content -LiteralPath (Join-Path $local 'sdk-path.txt') -Value $sdkHome
$godot = Join-Path $local 'Godot_v4.6-stable_mono_win64.zip'
if (!(Test-Path (Join-Path $local 'Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64.exe'))) {
    Fetch 'https://github.com/godotengine/godot/releases/download/4.6-stable/Godot_v4.6-stable_mono_win64.zip' $godot '0fb7695215317eb42f6ed6b348905e180eaf4b61369d5e8597a3cc1468cfda61' 'SHA256'
    Expand-Archive -LiteralPath $godot -DestinationPath $local -Force
}
New-Item -ItemType File -Force -Path (Join-Path $local 'Godot_v4.6-stable_mono_win64/_sc_') | Out-Null
if ($Templates) {
    foreach($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64','microsoft.aspnetcore.app.runtime.win-x64')){
        $file=Join-Path $local "Godot_v4.6-stable_mono_win64/GodotSharp/Tools/nupkgs/$package.8.0.31.nupkg"
        if(!(Test-Path -LiteralPath $file)){Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$package/8.0.31/$package.8.0.31.nupkg" -OutFile $file}
    }
    $templateHome = Join-Path $local 'export/templates'
    $requiredTemplates = @('windows_debug_x86_64.exe','windows_release_x86_64.exe','windows_debug_x86_64_console.exe','windows_release_x86_64_console.exe','version.txt')
    $missingTemplates = @($requiredTemplates | Where-Object { !(Test-Path -LiteralPath (Join-Path $templateHome $_)) })
    if ($missingTemplates.Count -gt 0) {
        $release = Invoke-RestMethod 'https://api.github.com/repos/godotengine/godot/releases/tags/4.6-stable'
        $asset = $release.assets | Where-Object name -eq 'Godot_v4.6-stable_mono_export_templates.tpz'
        $archive = Join-Path $local 'templates.zip'
        Fetch $asset.browser_download_url $archive ($asset.digest -replace '^sha256:', '') 'SHA256'
        Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $local 'export') -Force
    }
}
Write-Output 'Local toolchain ready: Godot 4.6 stable .NET / SDK 8.0.425'
