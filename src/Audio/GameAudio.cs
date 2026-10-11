using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.Audio;

public partial class GameAudio : Node3D
{
    private readonly Dictionary<string,AudioStreamWav> clips=[];
    private readonly Dictionary<string,double> lastPlayed=[];
    private readonly Dictionary<string,int> counts=[];
    private readonly Dictionary<string,int> variations=[];
    private readonly AudioStreamPlayer[] effects=new AudioStreamPlayer[8];
    private readonly AudioStreamPlayer3D[] spatial=new AudioStreamPlayer3D[8];
    private AudioStreamPlayer wind=null!,music=null!;
    private AudioStreamPlayer3D fire=null!;
    private int effectIndex,spatialIndex;
    private double clock,travel,nextMusic=12,nextBirds=18,fireClock;
    private Vector3 previous;
    private bool initialized,wasSwimming,paused=true,silent;
    private Task<Dictionary<string,byte[]>> musicData=null!;
    public IReadOnlyDictionary<string,int> Counts=>counts;
    public int CachedClips=>clips.Count;
    public int VoiceLimit=>effects.Length+spatial.Length;
    public int SpatialPlaying=>spatial.Count(voice=>voice.Playing);
    public bool WorldPaused=>paused;
    public override void _Ready()
    {
        // Headless validators have no audio device; keep them from queuing dummy
        // playback that cannot be mixed or drained. Audio is tested graphically.
        silent=DisplayServer.GetName()=="headless";
        // PCM synthesis is pure data work; create native streams on the main thread.
        musicData=Task.Run(()=>new Dictionary<string,byte[]>{{"music_day",ProceduralAudio.Create("music_day")},{"music_night",ProceduralAudio.Create("music_night")}});
        foreach(string bus in new[]{"Effects","Ambience","Music"})if(AudioServer.GetBusIndex(bus)<0){int index=AudioServer.BusCount;AudioServer.AddBus();AudioServer.SetBusName(index,bus);AudioServer.SetBusSend(index,"Master");}
        for(int i=0;i<8;i++){effects[i]=new(){Bus="Effects"};AddChild(effects[i]);spatial[i]=new(){Bus="Effects",MaxDistance=28,UnitSize=8};AddChild(spatial[i]);}
        wind=new(){Bus="Ambience",VolumeDb=-15,Stream=Clip("wind")};AddChild(wind);if(!silent)wind.Play();
        fire=new(){Bus="Ambience",VolumeDb=-8,MaxDistance=16,UnitSize=6,Stream=Clip("fire")};AddChild(fire);
        music=new(){Bus="Music",VolumeDb=-8};AddChild(music);PauseWorld(true);
    }
    private AudioStreamWav Clip(string cue,int variant=0)
    {
        string key=variant==0?cue:cue+"@"+variant;
        if(clips.TryGetValue(key,out var clip))return clip;
        byte[] data=cue.StartsWith("music_")?musicData.GetAwaiter().GetResult()[cue]:ProceduralAudio.Create(cue,variant);clip=new(){Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=ProceduralAudio.Rate,Data=data};
        if(cue is "wind" or "fire"){clip.LoopMode=AudioStreamWav.LoopModeEnum.Forward;clip.LoopBegin=0;clip.LoopEnd=data.Length/2;}clips.Add(key,clip);return clip;
    }
    public void ApplyVolumes(float master,float fx,float ambient,float score)
    {
        void Set(string bus,float value){int index=AudioServer.GetBusIndex(bus);AudioServer.SetBusMute(index,value<=0);AudioServer.SetBusVolumeDb(index,Mathf.LinearToDb(Math.Max(.0001f,value)));}
        Set("Master",master);Set("Effects",fx);Set("Ambience",ambient);Set("Music",score);
    }
    public void PauseWorld(bool value)
    {
        paused=value;wind.StreamPaused=value;fire.StreamPaused=value;music.StreamPaused=value;
        if(value)foreach(var voice in spatial)voice.Stop();
    }
    public void ResetWorld(Vector3 position){previous=position;initialized=true;travel=0;wasSwimming=false;fire.Stop();music.Stop();nextMusic=clock+12;nextBirds=clock+18;lastPlayed.Clear();}
    public void Play(string cue,Vector3? position=null)
    {
        if(position.HasValue&&paused)return;
        double interval=cue.StartsWith("hit_")?.16:cue=="hurt"?.3:cue=="ui"?.04:0;
        if(clock-lastPlayed.GetValueOrDefault(cue,-999)<interval)return;lastPlayed[cue]=clock;
        counts[cue]=counts.GetValueOrDefault(cue)+1;
        if(silent)return;
        bool surface=cue.StartsWith("step_")||cue.StartsWith("break_")||cue.StartsWith("hit_")||cue.StartsWith("place_");
        int sequence=variations.GetValueOrDefault(cue);if(surface)variations[cue]=(sequence+1)%12;
        int variant=surface?sequence%4:0;
        float pitch=surface?.96f+(sequence%3)*.04f:1;
        float volume=cue.StartsWith("step_")?-8:cue.StartsWith("hit_")?-6:cue.StartsWith("place_")?-4:cue.StartsWith("break_")?-2:0;
        if(position.HasValue){var voice=spatial[spatialIndex++%spatial.Length];voice.GlobalPosition=position.Value;voice.Stream=Clip(cue,variant);voice.PitchScale=pitch;voice.VolumeDb=volume;voice.MaxDb=volume;voice.Play();}
        else{var voice=effects[effectIndex++%effects.Length];voice.Stream=Clip(cue,variant);voice.PitchScale=pitch;voice.VolumeDb=volume;voice.Play();}
    }
    public override void _Process(double delta)=>clock+=delta;
    public override void _ExitTree()
    {
        // Stop even paused playback before releasing the last stream references.
        foreach(var voice in effects){voice.Stop();voice.Stream=null;}
        foreach(var voice in spatial){voice.Stop();voice.Stream=null;}
        wind.Stop();wind.Stream=null;fire.Stop();fire.Stream=null;music.Stop();music.Stream=null;
        foreach(var clip in clips.Values)clip.Dispose();clips.Clear();
    }
    public void Tick(double delta,Explorer player,WorldState world,float daylight)
    {
        if(paused||silent)return;if(!initialized)ResetWorld(player.GlobalPosition);
        var movement=player.GlobalPosition-previous;previous=player.GlobalPosition;double distance=new Vector2(movement.X,movement.Z).Length();if(distance<3)travel+=distance;else travel=0;
        if(player.Swimming&&!wasSwimming)Play("place_water",player.GlobalPosition);wasSwimming=player.Swimming;
        if(!player.Flying&&(player.IsOnFloor()||player.Swimming)&&travel>(player.Swimming?2.4:1.85)){
            travel=0;var foot=Explorer.ToCell(player.GlobalPosition+Vector3.Down*.12f);string id=player.Swimming?"water":world.Catalog.Id(world.Get(foot));Play("step_"+ProceduralAudio.Material(id),player.GlobalPosition+Vector3.Up*.2f);
        }
        int x=(int)Math.Floor(player.GlobalPosition.X),z=(int)Math.Floor(player.GlobalPosition.Z);bool underground=player.GlobalPosition.Y<world.Terrain.Surface(x,z)-4;
        wind.VolumeDb=underground?-38:-15;
        if(clock>=nextBirds){nextBirds=clock+25+RandomField.Unit(world.State.Seed,x,0,z,23)*17;if(!underground&&daylight>.4&&world.State.Weather.Blend<.5)Play("birds",player.GlobalPosition+new Vector3(5,4,-3));}
        if(clock>=nextMusic&&!music.Playing&&musicData.IsCompletedSuccessfully){nextMusic=clock+95;music.Stream=Clip(daylight>.3?"music_day":"music_night");music.Play();counts["music"]=counts.GetValueOrDefault("music")+1;}
        fireClock-=delta;if(fireClock<=0){fireClock=1;var nearest=world.State.Entities.Where(p=>p.Value.Kind=="campfire").Select(p=>Cell.Parse(p.Key)).Select(p=>new Vector3(p.X+.5f,p.Y+.6f,p.Z+.5f)).Where(p=>p.DistanceSquaredTo(player.GlobalPosition)<16*16).OrderBy(p=>p.DistanceSquaredTo(player.GlobalPosition)).ToArray();
            if(nearest.Length==0)fire.Stop();else{fire.GlobalPosition=nearest[0];if(!fire.Playing)fire.Play();}}
    }
    public void ExportSample(string cue,string path){if(Clip(cue).SaveToWav(path)!=Error.Ok)throw new System.IO.IOException("声音样本写入失败："+path);}
}
