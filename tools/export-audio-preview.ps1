param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root 'tests/bin/Debug/net8.0/CoreTests.dll'))
$create=$assembly.GetType('EmberLighthouse.Audio.ProceduralAudio').GetMethod('Create')
$target=Join-Path $root 'test-results/audio-revision-preview'
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach($action in @('step','break')){
    $stream=[IO.MemoryStream]::new()
    foreach($material in @('grass','wood','stone')){
        for($i=0;$i -lt 4;$i++){
            [byte[]]$pcm=$create.Invoke($null,[object[]]@("${action}_${material}",$i))
            $gain=[Math]::Pow(10, $(if($action -eq 'step'){-8}else{-2})/20)
            for($j=0;$j -lt $pcm.Length;$j+=2){$sample=[BitConverter]::ToInt16($pcm,$j);[byte[]]$scaled=[BitConverter]::GetBytes([int16]($sample*$gain));$stream.Write($scaled,0,2)}
            $silence=[byte[]]::new([int](22050*.24)*2);$stream.Write($silence,0,$silence.Length)
        }
        $silence=[byte[]]::new(22050);$stream.Write($silence,0,$silence.Length)
    }
    [byte[]]$data=$stream.ToArray();$stream.Dispose()
    $path=Join-Path $target "$action-preview.wav"
    $writer=[IO.BinaryWriter]::new([IO.File]::Create($path))
    try{
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int]($data.Length+36));$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $writer.Write([int]16);$writer.Write([int16]1);$writer.Write([int16]1);$writer.Write([int]22050);$writer.Write([int]44100);$writer.Write([int16]2);$writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data'));$writer.Write([int]$data.Length);$writer.Write($data)
    }finally{$writer.Dispose()}
    Write-Output $path
}
Write-Output 'Order: grass, wood, stone; four variants each. Mono synthesis with cue gain, without the game spatial mix.'
