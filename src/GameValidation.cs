using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool soak;
    private bool soakFinishing;
    private double soakDuration=1800;
    private readonly Stopwatch soakWatch=new();
    private readonly List<double> frameSamples=[];
    private readonly List<double> editSamples=[];
    private long lastFrame;
    private long peakWorkingSet;
    private int editCount,soakStep;
    private double nextEdit,nextSave,nextRoute;
    private string validationRoot="";
    private Vector2I soakRenderImageSize;
    private Task? soakReportTask;
    private bool soakFinalSaved,soakSaveSucceeded;
    private bool groundSoak;
    private bool gcStress;
    private int forcedCollections;
    private double nextCollection=16;
    private Vector3 lastSoakPosition;
    private double traveledMeters,minPlayerY=double.MaxValue,nextJump;
    private int jumpRequests,groundedFrames,readinessWaitFrames;
    private void InitializeValidation()
    {
        var args=OS.GetCmdlineUserArgs();soak=args.Contains("--soak");groundSoak=args.Contains("--ground-soak");gcStress=args.Contains("--gc-stress");
        foreach(var arg in args)if(arg.StartsWith("--duration="))soakDuration=Math.Clamp(double.Parse(arg[11..],System.Globalization.CultureInfo.InvariantCulture),10,14400);
        if(args.Contains("--weather-stress")){world!.State.Weather.Phase=2;world.State.Weather.Blend=1;world.State.Weather.Remaining=120;}
        if(args.Contains("--uncapped")){settings.FpsLimit=0;ApplySettings();}
        validationRoot=ProjectSettings.GlobalizePath("res://test-results");
        if(soak){GetWindow().Size=new(1920,1080);if(DisplayServer.GetName()!="headless")GetWindow().Position=new(-10000,-10000);GetViewport().Scaling3DScale=1;Directory.CreateDirectory(validationRoot);soakWatch.Start();nextEdit=15;nextSave=60;nextRoute=20;lastFrame=Stopwatch.GetTimestamp();player!.Flying=!groundSoak;lastSoakPosition=player.GlobalPosition;}
    }
    private void RunSoak()
    {
        if(soakReportTask?.IsFaulted==true){GD.PushError("耐久报告写入失败："+soakReportTask.Exception!.GetBaseException().Message);GetTree().Quit(5);return;}
        if(soakFinishing){if(soakFinalSaved&&soakReportTask?.IsCompleted==true){GD.Print(soakSaveSucceeded?"SOAK_PASS":"SOAK_FAILED");QuitCleanly(soakSaveSucceeded?0:5);}return;}
        double elapsed=soakWatch.Elapsed.TotalSeconds;long now=Stopwatch.GetTimestamp();double ms=(now-lastFrame)*1000.0/Stopwatch.Frequency;lastFrame=now;
        if(elapsed>=5&&soakRenderImageSize==Vector2I.Zero){using var image=GetViewport().GetTexture().GetImage();soakRenderImageSize=image.GetSize();image.SavePng(Path.Combine(validationRoot,"soak-viewport.png"));}
        if(elapsed>15)frameSamples.Add(ms);
        if(gcStress&&elapsed>=nextCollection){nextCollection=elapsed+1;GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();forcedCollections++;}
        var moved=player!.GlobalPosition-lastSoakPosition;traveledMeters+=new Vector2(moved.X,moved.Z).Length();lastSoakPosition=player.GlobalPosition;minPlayerY=Math.Min(minPlayerY,player.GlobalPosition.Y);if(player.IsOnFloor())groundedFrames++;
        if(player.GlobalPosition.Y<-2){GD.PushError("SOAK_FAILED: player fell below world");GetTree().Quit(5);return;}
        using(var process=Process.GetCurrentProcess())peakWorkingSet=Math.Max(peakWorkingSet,process.PeakWorkingSet64);
        if(screen!="play")SetScreen("play");
        if(renderer!.IsTerrainReady(player!.GlobalPosition)){
            if(elapsed>=nextRoute){nextRoute=elapsed+20;soakStep++;player.RotationDegrees=new(0,(soakStep%4)*90,0);}
            Input.ActionPress("forward");Input.ActionPress("sprint");
            if(groundSoak){if(elapsed>=nextJump){Input.ActionPress("jump");nextJump=elapsed+1.3;jumpRequests++;}else if(nextJump-elapsed<1.15)Input.ActionRelease("jump");}
            else if(player.GlobalPosition.Y<85)Input.ActionPress("jump");else Input.ActionRelease("jump");
            if(elapsed>=nextEdit){nextEdit=elapsed+.5;var feet=player.GlobalPosition;var p=new Cell((int)Math.Floor(feet.X)+2,(int)Math.Floor(feet.Y),(int)Math.Floor(feet.Z));
                if(Coordinates.InWorld(p)){world!.Set(p,editCount%2==0?catalog.Number("plank"):(ushort)0);renderer.RefreshEdit(p);editSamples.Add(renderer.LastEditMs);editCount++;}
            }
        }else{readinessWaitFrames++;Input.ActionRelease("forward");Input.ActionRelease("sprint");Input.ActionRelease("jump");}
        if(elapsed>=nextSave){nextSave=elapsed+60;RequestSave(success=>{if(!success)GetTree().Quit(5);else WriteSoakReport(false);});}
        if(elapsed>=soakDuration){soakFinishing=true;Input.ActionRelease("forward");Input.ActionRelease("sprint");Input.ActionRelease("jump");SetScreen("saving");RequestSave(success=>{soakSaveSucceeded=success;WriteSoakReport(success);soakFinalSaved=true;});}
    }
    private void WriteSoakReport(bool completed)
    {
        if(!soakFinishing&&soakReportTask?.IsCompleted==false)return;
        var frames=frameSamples.ToArray();var edits=editSamples.ToArray();
        var gcState=new{enabled=gcStress,forcedCollections};
        using var process=Process.GetCurrentProcess();
        peakWorkingSet=Math.Max(peakWorkingSet,Math.Max(process.PeakWorkingSet64,process.WorkingSet64));
        var state=new{completed,seconds=soakWatch.Elapsed.TotalSeconds,targetSeconds=soakDuration,scenario=groundSoak?"automated grounded sprint/jump, continuous edits, world streaming, creatures and 60s saves":"automated creative flight, continuous edits, world streaming, creatures and 60s saves",editCount,groundSoak,traveledMeters,jumpRequests,groundedFrames,readinessWaitFrames,minPlayerY,peakWorkingSetBytes=peakWorkingSet,currentWorkingSetBytes=process.WorkingSet64,loaded=renderer!.Loaded,pending=renderer.Pending,gpu=RenderingServer.GetVideoAdapterName(),viewDistance=settings.ViewDistance,viewport=GetViewport().GetVisibleRect().Size.ToString(),headless=DisplayServer.GetName()=="headless",saved=lastSaved,dateUtc=DateTime.UtcNow};
        var size=new{logical=state.viewport,textureReported=GetViewport().GetTexture().GetSize().ToString(),renderImage=soakRenderImageSize.ToString(),window=DisplayServer.WindowGetSize().ToString(),scale3D=GetViewport().Scaling3DScale,borderless=GetWindow().Borderless};
        // Only immutable data crosses to the report worker; keep sorting and disk IO off the game thread.
        var prior=soakReportTask??Task.CompletedTask;
        soakReportTask=prior.ContinueWith(previous=>{
            previous.GetAwaiter().GetResult();double sum=frames.Sum();int over100=frames.Count(v=>v>100);Array.Sort(frames);Array.Sort(edits);
            static double Percentile(double[] samples,double p)=>samples.Length==0?0:samples[Math.Min(samples.Length-1,(int)(samples.Length*p))];
            var report=new{state.completed,state.seconds,state.targetSeconds,state.scenario,frameCount=frames.Length,averageFps=sum>0?1000*frames.Length/sum:0,p95Ms=Percentile(frames,.95),p99Ms=Percentile(frames,.99),framesOver100Ms=over100,state.editCount,editP95Ms=Percentile(edits,.95),editMaxMs=edits.Length>0?edits[^1]:0,state.groundSoak,state.traveledMeters,state.jumpRequests,state.groundedFrames,state.readinessWaitFrames,state.minPlayerY,state.peakWorkingSetBytes,state.currentWorkingSetBytes,state.loaded,state.pending,state.gpu,state.viewDistance,state.viewport,state.headless,state.saved,state.dateUtc};
            void Write(string name,object value){string path=Path.Combine(validationRoot,name);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Catalog.JsonOptions));File.Move(path+".tmp",path,true);}
            Write("render-size.json",size);Write("soak.json",report);Write("gc-stress.json",gcState);
        },TaskScheduler.Default);
    }
}
