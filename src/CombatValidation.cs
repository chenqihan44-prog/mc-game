using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private async void RunCombatCheck()
    {
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        async Task Until(Func<bool> condition,string name){var wait=Stopwatch.StartNew();while(!condition()){if(wait.Elapsed.TotalSeconds>35)throw new TimeoutException(name);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        Creature Add(string kind,Vector3 position,bool provoked=false){var def=creatureDefs.Single(d=>d.Id==kind);var state=new CreatureState{Kind=kind,Health=def.Health,Position=[position.X,position.Y,position.Z],Provoked=provoked};world!.State.Creatures.Add(state);SpawnCreature(state);return creatures.Single(c=>ReferenceEquals(c.State,state));}
        void ClearEnemies(){foreach(var c in creatures){c.SetPhysicsProcess(false);c.QueueFree();}creatures.Clear();world!.State.Creatures.Clear();projectiles!.Clear();}
        void PlayerAt(float x,float z){player!.GlobalPosition=new(x,37.02f,z);player.Velocity=Vector3.Zero;world!.State.Position=[x,37.02,z];}
        void Wall(int x,int z,bool solid){for(int y=37;y<=39;y++){var p=new Cell(x,y,z);world!.Set(p,solid?catalog.Number("stone"):(ushort)0);renderer!.RefreshEdit(p);}}
        var timer=Stopwatch.StartNew();
        try{
            StartWorld(new(){Name="真实战斗机制回归",Seed=42,Creative=true,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});
            await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition),"initial terrain");await Frames(8);
            var enemy=Add("husk",new(0,37.02f,-6));enemy.SetPhysicsProcess(false);world!.State.Creative=false;
            world.State.Inventory.Slots[0]=new("bow",1);world.State.Inventory.Slots[1]=new("arrow",3);selected=0;player!.Camera.LookAt(enemy.GlobalPosition+Vector3.Up*.9f);
            double enemyHealth=enemy.State.Health;Check(Attack()&&world.State.Inventory.Count("arrow")==2&&enemy.State.Health==enemyHealth&&world.State.Inventory.Slots[0]!.Durability==383,"bow should consume/wear once and delay damage");
            await Until(()=>projectiles!.CreatureHits==1,"arrow impact");Check(enemy.State.Health==Math.Max(0,enemyHealth-catalog.Items["bow"].Power*2),"arrow damage");
            player.Camera.LookAt(new Vector3(10,38,-6));enemyHealth=enemy.State.Health;Check(Attack()&&world.State.Inventory.Count("arrow")==1,"miss still consumes one arrow");
            await Until(()=>projectiles!.Active==0,"miss expiry");Check(enemy.State.Health==enemyHealth,"miss damaged creature");
            world.State.Inventory.Remove("arrow",1);Check(Attack()&&projectiles!.Active==0&&world.State.Inventory.Count("arrow")==0&&world.State.Inventory.Slots[0]!.Durability==382,"empty bow fired or wore");
            for(int i=0;i<ProjectileFlight.Limit;i++)Check(projectiles!.Fire(new(){Position=[0,38,0],Velocity=[0,0,-1],Remaining=4,Damage=1}),"fill projectile pool");
            world.State.Inventory.Add("arrow",1,catalog);Check(Attack()&&world.State.Inventory.Count("arrow")==1&&projectiles!.Active==24&&world.State.Inventory.Slots[0]!.Durability==382,"capacity failure consumed arrow or wore bow");
            SetScreen("pause");double remaining=world.State.Projectiles[0].Remaining;await Frames(8);Check(world.State.Projectiles[0].Remaining==remaining,"pause advanced projectile");
            var pending=world.State.Copy();var save=saveQueue.Enqueue(world.State);await Until(()=>save.IsCompleted,"projectile save");save.GetAwaiter().GetResult();StartWorld(saves.Load(world.State.WorldId).Snapshot);
            Check(world!.State.Projectiles.Count==24&&projectiles!.Active==24&&world.State.Projectiles[0].Remaining==pending.Projectiles[0].Remaining,"reload reset flying arrows");
            world.State.Creative=true;projectiles!.Clear();ClearEnemies();await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition),"restored terrain");
            PlayerAt(0,0);world.State.Creative=false;var sentinel=Add("sentinel",new(-6,37.02f,0));await Until(()=>sentinel.IsPreparingAttack,"sentinel aim");PlayerAt(0,3);
            await Until(()=>projectiles.Active>0,"sentinel release");sentinel.SetPhysicsProcess(false);Check(Math.Abs(world.State.Projectiles[0].Velocity[2])<.1,"bolt homed onto displaced target");
            double health=world.State.Health;await Until(()=>projectiles.Active==0,"bolt expiry");Check(world.State.Health>=health,"sidestep did not dodge bolt");ClearEnemies();
            PlayerAt(0,0);sentinel=Add("sentinel",new(-6,37.02f,0));await Until(()=>projectiles.Active>0,"second sentinel release");sentinel.SetPhysicsProcess(false);
            int blocks=projectiles.BlockedCount;health=world.State.Health;Wall(-3,0,true);await Until(()=>projectiles.Active==0,"wall impact");Check(projectiles.BlockedCount==blocks+1&&world.State.Health>=health,"midflight wall did not stop bolt");Wall(-3,0,false);ClearEnemies();
            PlayerAt(0,0);var boar=Add("boar",new(0,37.02f,-3),true);await Until(()=>boar.IsPreparingAttack,"boar aim");PlayerAt(3,0);
            await Until(()=>boar.IsCharging,"charge release");health=world.State.Health;await Until(()=>!boar.IsCharging,"charge recovery");Check(Math.Abs(boar.GlobalPosition.X)<.2&&world.State.Health>=health,"charge tracked sidestep");ClearEnemies();
            PlayerAt(0,0);boar=Add("boar",new(0,37.02f,-3),true);await Until(()=>boar.IsCharging,"wall charge");health=world.State.Health;Wall(0,-1,true);
            await Until(()=>!boar.IsCharging,"charge wall impact");Check(boar.GlobalPosition.Z< -1.05&&world.State.Health>=health,"charge crossed wall");Wall(0,-1,false);ClearEnemies();
            PlayerAt(0,0);world.State.GuardianDefeated=false;var boss=Add("guardian",new(0,37.02f,-3));await Until(()=>boss.IsPreparingAttack,"slam aim");
            Check(boss.MarkedArea.HasValue&&Math.Abs(boss.MarkedArea.Value.X)<.1,"slam marker missing");PlayerAt(5,0);health=world.State.Health;
            await Until(()=>!boss.IsPreparingAttack,"slam release");Check(world.State.Health>=health,"leaving marked area did not dodge");ClearEnemies();
            PlayerAt(0,0);boss=Add("guardian",new(0,37.02f,-3));await Until(()=>boss.IsPreparingAttack,"pending slam");SetScreen("pause");double windup=boss.State.AttackWindup;
            await Frames(8);Check(boss.State.AttackWindup==windup,"pause advanced attack windup");save=saveQueue.Enqueue(world.State);await Until(()=>save.IsCompleted,"windup save");save.GetAwaiter().GetResult();StartWorld(saves.Load(world.State.WorldId).Snapshot);
            boss=creatures.Single(c=>c.Definition.Id=="guardian");Check(boss.State.AttackWindup==windup&&boss.State.AttackAim!=null,"reload reset pending attack");
            await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition),"pending attack terrain");health=world!.State.Health;await Until(()=>world.State.Health<health-20,"slam damage");ClearEnemies();
            world.State.Health=100;world.State.Inventory.Slots[0]=new("shield",1);selected=0;PlayerAt(0,0);player!.Camera.LookAt(new Vector3(0,38,-4));
            Input.ActionPress("use");
            Check(projectiles!.Fire(new(){Position=[0,38,-4],Velocity=[0,0,10],Damage=18,Remaining=1,Hostile=true}),"shield bolt fixture");
            await Until(()=>projectiles.Active==0,"shield impact");Check(world.State.Health>92&&world.State.Health<96&&world.State.Inventory.Slots[0]!.Durability==331,"facing shield did not reduce bolt damage or wear");
            Input.ActionRelease("use");
            ClearEnemies();PlayerAt(0,0);enemy=Add("husk",new(0,37.02f,-1.8f));enemy.SetPhysicsProcess(false);player!.Camera.LookAt(enemy.GlobalPosition+Vector3.Up);world.State.Inventory.Slots[0]=new("wood_sword",1);
            Check(Attack()&&enemy.State.Health==34&&world.State.Inventory.Slots[0]!.Durability==59,"wood sword damage or wear");world.State.Inventory.Slots[0]=new("wood_axe",1);
            Check(Attack()&&enemy.State.Health==28&&world.State.Inventory.Slots[0]!.Durability==58,"wood axe damage or double melee wear");
            ClearEnemies();PlayerAt(0,0);enemy=Add("husk",new(0,37.02f,-8));enemy.State.Health=5;await Frames(30);
            Check(enemy.GlobalPosition.Z> -7,"low-health hostile fled instead of pursuing");PlayerAt(4,0);await Until(()=>enemy.IsPreparingAttack,"moving player reacquisition");Check(enemy.GlobalPosition.X>2,"hostile failed to follow moved player");
            ClearEnemies();PlayerAt(0,0);sentinel=Add("sentinel",new(-3,37.02f,0));sentinel.State.AttackCooldown=10;await Frames(60);Check(sentinel.GlobalPosition.DistanceTo(player!.GlobalPosition)>4,"sentinel failed to retreat at close range");
            ClearEnemies();PlayerAt(0,0);enemy=Add("husk",new(0,37.02f,-1.2f));await Until(()=>enemy.IsPreparingAttack,"death during windup");
            int ghostHits=0;enemy.AttackPlayer=_=>ghostHits++;enemy.Damage(1000);enemy._PhysicsProcess(1);Check(ghostHits==0,"dead enemy completed pending attack");
            SetScreen("saving");await Frames(3);for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            string root=ProjectSettings.GlobalizePath("res://test-results/combat");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"validation.json"),JsonSerializer.Serialize(new{passed=true,headless=DisplayServer.GetName()=="headless",seconds=timer.Elapsed.TotalSeconds,bowDelayedHit=true,bowMiss=true,ammoTransactions=true,projectileLimit=24,projectilePauseReload=true,boltDodge=true,midflightWall=true,chargeDodge=true,chargeWall=true,slamMarker=true,slamDodge=true,windupPauseReload=true,slamHit=true,shieldBolt=true,lowHealthPursuit=true,movingTargetReacquired=true,sentinelRetreat=true,deadAttackSuppressed=true},Catalog.JsonOptions));
            GD.Print("COMBAT_PASS: actual bow/bolt flight, ammo/cap, pause/reload, sidestep/wall, charge, marked slam, persisted windup and directional shield");QuitCleanly();
        }catch(Exception e){GD.PushError("COMBAT_FAILED: "+e);GetTree().Quit(10);}
    }
}
