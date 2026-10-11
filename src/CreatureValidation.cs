using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private async void RunCreatureCheck()
    {
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        async Task Until(Func<bool> condition){var timer=Stopwatch.StartNew();while(!condition()){if(timer.Elapsed.TotalSeconds>40)throw new TimeoutException("生物回归等待超时");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        var timer=Stopwatch.StartNew();
        try{
            StartWorld(new(){Name="生物生成与读档回归",Seed=42,Creative=true});
            Check(creatures.Count==world!.State.Creatures.Count,"native population differs from model");
            var boss=creatures.Single(c=>c.Definition.Id=="guardian");var bossCell=Explorer.ToCell(boss.GlobalPosition);
            Check(CreaturePopulation.CanStand(world,bossCell,3),"boss overlaps beacon");
            boss.Damage(1000);int graves=world.State.Graves.Count;boss.Damage(1000);
            Check(world.State.GuardianDefeated&&world.State.Creatures.All(c=>c.Kind!="guardian")&&world.State.Graves.Count==graves,"boss death duplicated reward");
            var saved=saveQueue.Enqueue(world.State);await Until(()=>saved.IsCompleted);saved.GetAwaiter().GetResult();
            var loaded=saves.Load(world.State.WorldId).Snapshot;StartWorld(loaded);
            Check(world!.State.GuardianDefeated&&creatures.All(c=>c.Definition.Id!="guardian")&&creatures.Count==world.State.Creatures.Count,"reload respawned boss");
            await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition));await Frames(10);
            world.State.Creative=false;
            var state=new CreatureState{Kind="husk",Health=40,Position=[player!.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z-1.2]};world.State.Creatures.Add(state);SpawnCreature(state);
            var enemy=creatures.Single(c=>ReferenceEquals(c.State,state));await Until(()=>enemy.IsPreparingAttack);
            double health=world.State.Health;var camp=Explorer.ToCell(player.GlobalPosition)+new Cell(3,0,0);
            world.State.Inventory.Add("campfire",1,catalog);Check(world.Place(camp,"campfire",false,out var error),error);renderer!.RefreshEdit(camp);
            await Frames(50);Check(!enemy.IsPreparingAttack&&world.State.Health>=health,"windup hit inside safe camp");
            world.State.Entities.Remove(camp.ToString());world.Set(camp,0);renderer.RefreshEdit(camp);
            await Until(()=>enemy.IsPreparingAttack);health=world.State.Health;world.State.Creative=true;
            await Frames(50);Check(!enemy.IsPreparingAttack&&world.State.Health>=health,"windup continued after creative switch");
            world.Advance(12);populationClock=0;TickPopulation(0);Check(creatures.Count==world.State.Creatures.Count&&creatures.Count<=CreaturePopulation.Limit,"population adapter out of sync");
            Check(creatures.All(c=>c.Definition.Id!="guardian"),"population tick resurrected boss");
            SetScreen("saving");await Frames(3);for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            string path=ProjectSettings.GlobalizePath("res://test-results/creatures");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"validation.json"),JsonSerializer.Serialize(new{passed=true,headless=DisplayServer.GetName()=="headless",seconds=timer.Elapsed.TotalSeconds,population=creatures.Count,bossClearance=true,bossDefeatReload=true,deathRewardOnce=true,safeCampCancel=true,creativeCancel=true},Catalog.JsonOptions));
            GD.Print("CREATURE_PASS: boss clearance, reward once, defeat save/reload, safe-camp and creative windup cancellation, bounded population adapter");QuitCleanly();
        }catch(Exception e){GD.PushError("CREATURE_FAILED: "+e);GetTree().Quit(9);}
    }
}
