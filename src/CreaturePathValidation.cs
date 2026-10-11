using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Player;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool creaturePathCheck;
    private async void RunCreaturePathCheck()
    {
        var timer=Stopwatch.StartNew();var samples=new List<object>();Creature? actor=null;string stage="setup";float maximumFootY=0;
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Wait(Func<bool> done,double seconds=10){double end=timer.Elapsed.TotalSeconds+seconds;while(!done()){
            if(actor!=null)maximumFootY=Math.Max(maximumFootY,actor.GlobalPosition.Y);
            if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage+"，位置 "+actor?.GlobalPosition+"，最高脚部 "+maximumFootY);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }}
        async Task Ready(){await Wait(()=>renderer!.IsTerrainReady(player!.GlobalPosition),20);await Frames(3);}
        async Task Reset(string kind,Vector3 position,bool flee=false){
            foreach(var c in creatures){worldRoot!.RemoveChild(c);c.QueueFree();}creatures.Clear();world!.State.Creatures.Clear();await Frames(2);
            player!.GlobalPosition=flee?new(-2.5f,80.05f,7.5f):new(10.5f,81.05f,7.5f);player.Velocity=Vector3.Zero;await Ready();
            var def=creatureDefs.Single(d=>d.Id==kind);var state=new CreatureState{Kind=kind,Health=def.Health,Provoked=kind!="deer",Position=[position.X,position.Y,position.Z]};
            world.State.Creatures.Add(state);SpawnCreature(state);actor=creatures.Single();actor.Definition=def with{Attack="none"};maximumFootY=position.Y;await Frames(3);
        }
        void Record(bool passed,string error){
            var p=actor?.GlobalPosition??Vector3.Zero;string contacts=actor==null?"":string.Join(",",Enumerable.Range(0,actor.GetSlideCollisionCount()).Select(i=>actor.GetSlideCollision(i).GetNormal().ToString()));
            var shape=actor?.GetChildren().OfType<CollisionShape3D>().Select(c=>c.Shape).OfType<BoxShape3D>().Single();
            string directory=ProjectSettings.GlobalizePath("res://test-results/creature-path");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",controlledSetup=true,attackDisabledForNavigation=true,maximumFootY,actorPosition=new[]{p.X,p.Y,p.Z},colliderWidth=shape?.Size.X,colliderMargin=shape?.Margin,contacts,samples},Catalog.JsonOptions));
        }
        try{
            StartWorld(new(){Name="生物台阶、门与地形回归",Seed=42,Position=[10.5,81.05,7.5]});
            foreach(var c in creatures){worldRoot!.RemoveChild(c);c.QueueFree();}creatures.Clear();world!.State.Creatures.Clear();populationClock=999999;player!.Flying=true;
            await Ready();
            for(int x=0;x<=12;x++)for(int z=6;z<=8;z++){
                world.Set(new(x,79,z),catalog.Number("stone"));if(x>=4)world.Set(new(x,80,z),catalog.Number("stone"));
            }
            renderer!.RefreshEdit(new(0,79,6));await Ready();
            foreach(string kind in new[]{"husk","skitter","boar","deer","sentinel","guardian"}){
                stage="实际一格台阶："+kind;await Reset(kind,new(1.5f,80.05f,7.5f),kind=="deer");
                await Wait(()=>actor!.GlobalPosition.X>=5.1f&&actor.GlobalPosition.Y>=80.98f&&actor.IsOnFloor());
                samples.Add(new{stage,position=new[]{actor!.GlobalPosition.X,actor.GlobalPosition.Y,actor.GlobalPosition.Z},maximumFootY});GD.Print("PATH_STAGE: "+stage);
            }
            for(int x=3;x<=11;x++)for(int y=81;y<=84;y++)foreach(int z in new[]{6,8})world.Set(new(x,y,z),catalog.Number("stone"));
            var door=new Cell(7,81,7);var upper=door+new Cell(0,1,0);world.State.Inventory.Add("door",1,catalog);Check(world.Place(door,"door",false,out var error,rotation:1),error);renderer.RefreshEdit(door);await Ready();
            void Door(bool open,int rotation){foreach(var p in new[]{door,upper}){var e=world.State.Entities[p.ToString()];e.Open=open;e.Rotation=rotation;world.Set(p,world.Get(p));renderer.RefreshEdit(p);}}
            foreach(string kind in new[]{"husk","skitter","boar","deer","sentinel"})foreach(int rotation in new[]{1,3}){
                stage="关闭门阻挡："+kind+":"+rotation;Door(false,rotation);await Reset(kind,new(4.5f,81.05f,7.5f));
                if(kind=="deer"){player!.GlobalPosition=new(1.5f,81.05f,7.5f);await Ready();}await Frames(180);
                Check(actor!.GlobalPosition.X<7,"生物穿过关闭门");samples.Add(new{stage,blocked=true});
                stage="打开门后通行："+kind+":"+rotation;Door(true,rotation);await Wait(()=>actor!.GlobalPosition.X>8.3f);
                samples.Add(new{stage,position=new[]{actor!.GlobalPosition.X,actor.GlobalPosition.Y,actor.GlobalPosition.Z}});GD.Print("PATH_STAGE: "+stage);
            }
            world.State.Entities.Remove(door.ToString());world.State.Entities.Remove(upper.ToString());world.Set(door,0);world.Set(upper,0);
            for(int x=3;x<=11;x++)for(int y=81;y<=84;y++)foreach(int z in new[]{6,8})world.Set(new(x,y,z),0);renderer.RefreshEdit(door);await Ready();
            stage="移动中改地形并绕行";await Reset("husk",new(4.5f,81.05f,7.5f));await Wait(()=>actor!.GlobalPosition.X>5);
            var wall=new Cell(7,81,7);world.Set(wall,catalog.Number("stone"));world.Set(wall+new Cell(0,1,0),catalog.Number("stone"));renderer.RefreshEdit(wall);
            float detour=0;double end=timer.Elapsed.TotalSeconds+10;
            while(actor!.GlobalPosition.X<8.3f){detour=Math.Max(detour,Math.Abs(actor.GlobalPosition.Z-7.5f));if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage+"未完成");await Frames(1);}
            Check(detour>.3f,"改地形后没有实际绕行");samples.Add(new{stage,detour});
            Record(true,"");GD.Print("CREATURE_PATH_PASS: six kinds physically climb a block, five ordinary kinds respect closed and open doors in both rotations, changed terrain replans");QuitCleanly();
        }catch(Exception error){Record(false,error.Message);GD.PushError("CREATURE_PATH_FAILED: "+error);QuitCleanly(9);}
    }
}
