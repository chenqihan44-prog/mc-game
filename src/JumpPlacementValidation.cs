using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;
public partial class Game
{
    private bool jumpPlacementCheck;
    private async void RunJumpPlacementCheck()
    {
        string stage="setup";var watch=Stopwatch.StartNew();var samples=new List<object>();string folder=ProjectSettings.GlobalizePath("res://test-results/jump-placement");Directory.CreateDirectory(folder);
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(stage+": "+message);}
        async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> ready){double end=watch.Elapsed.TotalSeconds+35;while(!ready()){if(watch.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frame();}}
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=watch.Elapsed.TotalSeconds,samples,actualPhysics=true,actualRightUse=true,controlledPlatform=true},EmberLighthouse.Items.Catalog.JsonOptions));
        try{
            settings.ViewDistance=2;StartWorld(new(){Name="跳跃脚下放块回归",Seed=42,Time=300,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);foreach(var c in creatures)c.QueueFree();creatures.Clear();world!.State.Creatures.Clear();
            foreach(var baseCell in new[]{new Cell(8,110,8),new Cell(15,127,15),new Cell(-1,127,-1)}){
                stage="platform "+baseCell;SetScreen("journal");for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){world.Set(baseCell+new Cell(x,-1,z),catalog.Number("stone"));for(int y=0;y<9;y++)world.Set(baseCell+new Cell(x,y,z),0);}
                player.GlobalPosition=new(baseCell.X+.5f,baseCell.Y+.02f,baseCell.Z+.5f);player.Velocity=Vector3.Zero;world.State.Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));SetScreen("play");player.SetPhysicsProcess(true);await Until(()=>player.IsOnFloor());world.State.Inventory=new();world.State.Inventory.Slots[0]=new("plank",8);selected=0;
                for(int level=0;level<3;level++){
                    stage="jump and place "+baseCell+" level "+level;var cell=baseCell+new Cell(0,level,0);Input.ActionPress("jump");await Until(()=>player.Velocity.Y>1);Input.ActionRelease("jump");await Until(()=>player.GlobalPosition.Y>cell.Y+1.03f);player.Camera.LookAt(new Vector3(cell.X+.55f,cell.Y-.1f,cell.Z+.5f));Check(player.Target()?.Before==cell,"downward placement target incorrect");float velocity=player.Velocity.Y;double height=player.GlobalPosition.Y;int before=world.State.Inventory.Count("plank");Use();
                    samples.Add(new{stage,readyImmediately=renderer!.IsTerrainReady(player.GlobalPosition),editMilliseconds=renderer.LastEditMs,velocityBefore=velocity,velocityAfter=player.Velocity.Y,height});
                    Check(world.Get(cell)==catalog.Number("plank")&&world.State.Inventory.Count("plank")==before-1,"actual right use did not place exactly one block");Check(renderer.IsTerrainReady(player.GlobalPosition),"lighting refresh invalidated otherwise ready collision");Check(player.Velocity.Y==velocity,"placement reset jump velocity");
                    int stillTicks=0;float oldY=player.GlobalPosition.Y;int ticks=0;while(!player.IsOnFloor()){
                        await Frame();ticks++;Check(renderer.IsTerrainReady(player.GlobalPosition),"neighbor lighting paused falling player");if(!player.IsOnFloor()&&Math.Abs(player.Velocity.Y)>.1f&&Math.Abs(player.GlobalPosition.Y-oldY)<.00001f)stillTicks++;oldY=player.GlobalPosition.Y;if(ticks>80)throw new TimeoutException("pillar landing");
                    }
                    Check(stillTicks==0,"airborne motion stalled");Check(Math.Abs(player.GlobalPosition.Y-(cell.Y+1))<.04,"player fell through placed block or landed too high");await Frame();
                }player.SetPhysicsProcess(false);
            }
            stage="unloaded region preserves velocity while refusing movement";var saved=player.GlobalPosition;player.GlobalPosition=new(1000.5f,150,1000.5f);player.Velocity=new(1,-5,0);Check(!renderer!.IsTerrainReady(player.GlobalPosition),"unloaded test region unexpectedly ready");var position=player.GlobalPosition;player._PhysicsProcess(1d/60);Check(player.GlobalPosition==position&&player.Velocity==new Vector3(1,-5,0),"missing collision erased velocity or allowed movement into unloaded terrain");player.GlobalPosition=saved;player.Velocity=Vector3.Zero;
            Record(true,"");GD.Print("JUMP_PLACEMENT_PASS: nine actual jump/right-use/landing cycles, positive and negative chunk corners, continuous airborne motion, exact placement cost and safe unloaded wait");QuitCleanly();
        }catch(Exception error){Input.ActionRelease("jump");Record(false,error.ToString());GD.PushError("JUMP_PLACEMENT_FAILED: "+error);QuitCleanly(14);}
    }
}

