using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool boundaryCheck;
    private async void RunBoundaryCheck()
    {
        var timer=Stopwatch.StartNew();var samples=new List<object>();
        bool prematureSkyReady=false;
        static void Check(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Ready(){double end=timer.Elapsed.TotalSeconds+20;while(!renderer!.IsTerrainReady(player!.GlobalPosition)){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException("边界地形等待超时");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}await Frames(3);}
        void Record(bool passed,string error){
            string path=ProjectSettings.GlobalizePath("res://test-results/boundary");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"validation.json"),JsonSerializer.Serialize(new{passed,error,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",prematureSkyReady,controlledSetup=true,syntheticReadinessGate=true,samples},Catalog.JsonOptions));
        }
        try{
            StartWorld(new(){Name="边界与未加载地形回归",Seed=42,Creative=true,Position=[2047.5,232,0]});
            foreach(var c in creatures)c.QueueFree();creatures.Clear();world!.State.Creatures.Clear();populationClock=999999;
            prematureSkyReady=renderer!.IsTerrainReady(player!.GlobalPosition);
            player.Flying=true;
            foreach(float height in new[]{232f,80f})foreach(var direction in new[]{Vector3.Right,Vector3.Left,Vector3.Back,Vector3.Forward}){
                player.GlobalPosition=new(direction.X*2047.5f,height,direction.Z*2047.5f);player.Velocity=Vector3.Zero;
                await Ready();player.LookAt(player.GlobalPosition+direction);Input.ActionPress("forward");await Frames(40);Input.ActionRelease("forward");
                var p=player.GlobalPosition;
                samples.Add(new{stage="border",height,direction=new[]{direction.X,direction.Z},position=new[]{p.X,p.Y,p.Z}});
                Check(Math.Abs(p.X)<=2047.705f&&Math.Abs(p.Z)<=2047.705f,"角色越过水平世界边界："+p);
                Check(Math.Abs(p.X*direction.X+p.Z*direction.Z)>2047.65f,"边界检查没有实际到达边界");
            }
            Check(!prematureSkyReady,"未提交任何区块却报告高空地形就绪");
            player.GlobalPosition=new(0,80,0);player.Velocity=Vector3.Zero;await Ready();
            Func<Vector3,bool> ready=player.TerrainReady;
            player.TerrainReady=p=>ready(p)&&p.X<.2f;
            player.LookAt(player.GlobalPosition+Vector3.Right);Input.ActionPress("forward");Input.ActionPress("jump");await Frames(40);Input.ActionRelease("forward");Input.ActionRelease("jump");
            samples.Add(new{stage="unready-horizontal-and-vertical",position=new[]{player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z}});
            Check(player.GlobalPosition.X<.2f&&player.GlobalPosition.Y<80.2f,"进入未就绪目的地时仍在移动或上升");
            player.TerrainReady=ready;player.GlobalPosition=new(0,80,0);player.Velocity=Vector3.Zero;player.Flying=false;await Ready();
            player.TerrainReady=p=>ready(p)&&p.Y>=79.9f;await Frames(45);
            samples.Add(new{stage="unready-fall",position=new[]{player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z}});
            Check(player.GlobalPosition.Y>=79.9f&&player.Velocity==Vector3.Zero,"下落穿过未就绪地形");
            player.TerrainReady=ready;player.Flying=true;
            player.GlobalPosition=new(2050,232,0);player.Velocity=Vector3.Zero;await Frames(2);
            Check(player.GlobalPosition.X<=2047.705f&&world.State.Position[0]<=2047.705,"旧越界位置没有恢复到有效世界边界");
            player.GlobalPosition=new(0,80,0);player.Velocity=Vector3.Zero;await Ready();
            var edited=new Cell(15,79,0);var adjacent=new Cell(16,79,0);
            int staleBefore=renderer.StaleResultsDiscarded;
            world.Set(edited,catalog.Number("stone"));double staleDeadline=timer.Elapsed.TotalSeconds+10;
            while(!renderer.HasPending(Coordinates.Chunk(edited))){if(timer.Elapsed.TotalSeconds>staleDeadline)throw new TimeoutException("未观察到实际旧区块任务");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            world.Set(edited,catalog.Number("plank"));renderer.RefreshEdit(edited);
            world.Set(adjacent,catalog.Number("cobble"));renderer.RefreshEdit(adjacent);
            while(renderer.StaleResultsDiscarded<=staleBefore){if(timer.Elapsed.TotalSeconds>staleDeadline)throw new TimeoutException("过期网格任务未被丢弃");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            samples.Add(new{stage="stale-task-rejected",discarded=renderer.StaleResultsDiscarded-staleBefore});
            player.GlobalPosition=new(256,80,0);await Ready();
            player.GlobalPosition=new(16,80,0);await Ready();
            Check(world.Get(edited)==catalog.Number("plank")&&world.Get(adjacent)==catalog.Number("cobble"),"跨区块卸载后编辑丢失");
            var query=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(new(15.5f,81,.5f),new(15.5f,78,.5f)));
            Check(query.Count>0&&((Vector3)query["position"]).Y>79.9f,"卸载重载后的编辑碰撞丢失或被旧任务覆盖");
            var saved=saveQueue.Enqueue(world.State);double deadline=timer.Elapsed.TotalSeconds+20;
            while(!saved.IsCompleted){if(timer.Elapsed.TotalSeconds>deadline)throw new TimeoutException("边界回归保存超时");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}saved.GetAwaiter().GetResult();
            var loaded=saves.Load(world.State.WorldId).Snapshot;
            Check(loaded.Changes.GetValueOrDefault(edited.ToString())=="plank"&&loaded.Changes.GetValueOrDefault(adjacent.ToString())=="cobble","跨区块编辑没有保存");
            samples.Add(new{stage="unload-reload-collision-save",cell=edited.ToString()});
            Record(true,"");GD.Print("BOUNDARY_PASS: four directions at terrain/sky height, empty sky readiness, unready flight/fall freeze, old outside position recovery, unload/reload collision and saved edit");QuitCleanly();
        }catch(Exception error){Record(false,error.Message);GD.PushError("BOUNDARY_FAILED: "+error);QuitCleanly(9);}
        finally{foreach(string action in new[]{"forward","jump","crouch","sprint"})Input.ActionRelease(action);}
    }
}
