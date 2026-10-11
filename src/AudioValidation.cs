using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberLighthouse.Core;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool audioCheck,audioCheckStarted;
    private readonly Stopwatch audioCheckWatch=new();
    private void StartAudioCheck(){StartWorld(new(){Name="声音与近场灯光检查",Seed=42,Creative=true});audioCheckWatch.Start();}
    private void RunAudioCheck()
    {
        if(audioCheckStarted)return;
        if(audioCheckWatch.Elapsed.TotalSeconds>45){GD.PushError("AUDIO_FAILED: terrain timeout");GetTree().Quit(6);return;}
        if(audioCheckWatch.Elapsed.TotalSeconds<5||renderer==null||player==null||!renderer.IsTerrainReady(player.GlobalPosition))return;
        audioCheckStarted=true;CheckAudioInEngine();
    }
    private async void CheckAudioInEngine()
    {
        static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        try{
            string path=ProjectSettings.GlobalizePath("res://test-results/audio");Directory.CreateDirectory(path);
            foreach(string cue in new[]{"step_wood","step_stone","step_grass","step_sand","step_snow","step_water","break_wood","pickup","craft","hurt","shield","warn_guardian","warn_skitter","birds","music_day","music_night"})audio.ExportSample(cue,Path.Combine(path,cue+".wav"));
            audio.ApplyVolumes(1,0,.6f,.4f);
            Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Effects")),"effects mute");
            Check(!AudioServer.IsBusMute(AudioServer.GetBusIndex("Ambience"))&&!AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")),"independent categories");
            audio.ApplyVolumes(0,1,1,1);Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Master")),"master mute");
            audio.ApplyVolumes(1,1,.7f,.3f);
            for(int i=0;i<100;i++)audio.Play("attack",player!.GlobalPosition+Vector3.Right);
            Check(audio.VoiceLimit==16&&audio.SpatialPlaying<=8,"bounded spatial voices");
            audio.PauseWorld(true);audio.Play("warn_guardian",player!.GlobalPosition);
            Check(audio.WorldPaused&&audio.SpatialPlaying==0&&!audio.Counts.ContainsKey("warn_guardian"),"pause suppresses creature warnings");
            audio.Play("ui");Check(audio.Counts.ContainsKey("ui"),"UI remains audible while paused");audio.PauseWorld(false);
            var ground=new Cell(2,36,-3);var crop=ground+new Cell(0,1,0);world!.Set(ground,catalog.Number("farmland"));world.Set(crop,0);renderer!.RefreshEdit(ground);
            Plant(ground,"wheat_seed");Check(audio.Counts.ContainsKey("place_grass"),"planting cue");world.Advance(270);Harvest(crop,world.State.Entities[crop.ToString()]);Check(audio.Counts.ContainsKey("pickup"),"harvest cue");
            var lamps=Enumerable.Range(0,10).Select(i=>new Cell(i-5,38,-5)).ToArray();
            foreach(var p in lamps){world.Set(p,catalog.Number("lantern"));renderer.RefreshEdit(p);}
            world.State.Time=900;player.Rotation=Vector3.Zero;
            for(int i=0;i<30;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(nearLights!.ActiveCount==6,"near lights select six from ten sources");
            using(var image=GetViewport().GetTexture().GetImage())image.SavePng(Path.Combine(path,"lighting.png"));
            foreach(var p in lamps){world.Set(p,0);renderer.RefreshEdit(p);}
            for(int i=0;i<30;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(nearLights.ActiveCount==0,"removed lights release pool slots");
            File.WriteAllText(Path.Combine(path,"validation.json"),JsonSerializer.Serialize(new{passed=true,seconds=audioCheckWatch.Elapsed.TotalSeconds,samples=16,voiceLimit=audio.VoiceLimit,counts=audio.Counts,lightLimit=6,pausing=true,categoryMuting=true},EmberLighthouse.Items.Catalog.JsonOptions));
            GD.Print("AUDIO_PASS: PCM samples, bus muting, bounded voices, pause, planting, harvest, six-light pool and removal");QuitCleanly();
        }catch(Exception e){GD.PushError("AUDIO_FAILED: "+e);GetTree().Quit(6);}
    }
}
