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
using EmberLighthouse.Rendering;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool mainlineCheck,mainlinePreview;
    private async void RunMainlineCheck()
    {
        var watch=Stopwatch.StartNew();string stage="setup";var milestones=new List<string>();string folder=ProjectSettings.GlobalizePath("res://test-results/mainline");Directory.CreateDirectory(folder);
        static void Check(bool result,string error){if(!result)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Children(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var n in Children(child))yield return n;}}
        void Click(string title)=>Children(overlay).OfType<Button>().Single(b=>b.Text==title).EmitSignal(Button.SignalName.Pressed);
        async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> done){double end=watch.Elapsed.TotalSeconds+25;while(!done()){if(watch.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frame();}}
        async Task Go(Cell cell){SetScreen("journal");player!.GlobalPosition=new(cell.X+.5f,cell.Y+.02f,cell.Z+.5f);world!.State.Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));}
        void Mark(){milestones.Add(stage);GD.Print("MAINLINE_STAGE: "+stage);}
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=watch.Elapsed.TotalSeconds,controlledSetup=true,teleportedBetweenSites=true,grantedFixtureMaterials=true,manualSurvivalDurationMeasured=false,milestones},Catalog.JsonOptions));
        try{
            settings.ViewDistance=3;StartWorld(new(){Name="三塔归火原生交互验证",Seed=42,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));SetScreen("journal");
            world!.State.Inventory.Add("iron_ingot",48,catalog);world.State.Inventory.Add("copper_coil",8,catalog);
            var layout=world.Terrain.Adventure!;
            async Task Defeat(string site,string kind,string proof){var s=layout.Site(site);await Go(s.Id=="snow"?s.At(0,17,-12):s.At(0,1,-12));var target=s.At(0,s.Id=="snow"?17:1,-16);await Until(()=>renderer!.IsTerrainReady(new(target.X+.5f,target.Y,target.Z+.5f)));world.State.Population.NextSpawnTime=0;TickPopulation(1);await Until(()=>creatures.Any(c=>c.Definition.Id==kind));foreach(var c in creatures)c.SetPhysicsProcess(false);var enemy=creatures.Single(c=>c.Definition.Id==kind);Check(enemy.State.Health==enemy.Definition.Health,"elite did not use full health");enemy.Damage(100000);Check(world.State.Mainline.Proofs.Contains(proof),"actual death callback failed proof "+proof);await Frame();}
            Cell Tower(int i){var t=layout.Towers[i];return new(t.X,world.Terrain.Surface(t.X,t.Z)+1,t.Z);}
            async Task Repair(int i){await Go(Tower(i)+new Cell(0,0,4));ShowMainlineBeacon(Tower(i));Click("修复并点亮");Check(world.State.Beacons.Contains(i),"tower UI repair failed "+i);}
            stage="mine proof and first tower UI";ShowMainlineBeacon(Tower(0));Click("修复并点亮");Check(world.State.Beacons.Count==0,"early UI bypassed mine");await Defeat("mine","mine_warden","mine");await Repair(0);ShowMainlineJournal();Check(Children(overlay).OfType<Label>().Any(l=>l.Text.Contains("双域远征")),"journal missed regional objectives");Mark();
            stage="two regional elites, proof gates, second tower UI";await Defeat("snow","observatory_warden","snow");ShowMainlineBeacon(Tower(1));Click("修复并点亮");Check(!world.State.Beacons.Contains(1),"one prism bypassed gate");await Defeat("forge","furnace_warden","forge");await Repair(1);Mark();
            stage="physical calibration bench, alloy crafting, lining, repair";await Go(new(0,39,0));var bench=new Cell(2,39,0);world.Set(bench,0);world.State.Inventory.Add("calibration_bench",1,catalog);Check(world.Place(bench,"calibration_bench",false,out var error),error);renderer!.RefreshEdit(bench);currentContainer=bench;currentStation="calibration_bench";world.State.Inventory.Add("crystal",12,catalog);world.State.Inventory.Add("coal",12,catalog);world.State.Inventory.Add("stick",4,catalog);
            for(int i=0;i<3;i++)Craft(catalog.Recipes["ember_alloy"]);Check(world.State.Inventory.Count("ember_alloy")==3,"alloy station craft failed");Craft(catalog.Recipes["ember_sword"]);Check(world.State.Inventory.Count("ember_sword")==1,"new weapon craft failed");
            world.State.Equipment.Slots[1]=new("iron_chestplate",1,20);world.State.Inventory.Add("heat_lining",1,catalog);world.State.Inventory.Add("repair_kit",1,catalog);ShowWorkshopMaintenance(()=>{});Click("安装耐热衬片");Check(world.State.Equipment.Slots[1]!.HeatLining&&world.State.Inventory.Count("heat_lining")==0,"lining UI failed");Children(overlay).OfType<Button>().Single(b=>b.Text=="修理（修理包 ×1）"&&b.GetParent().GetChildren().OfType<Label>().Any(l=>l.Text.StartsWith(catalog.Items["iron_chestplate"].Name))).EmitSignal(Button.SignalName.Pressed);Check(world.State.Equipment.Slots[1]!.Durability==116,"repair UI failed");currentContainer=null;Mark();
            stage="deep guardian proof, sigil recipe, third tower";await Defeat("cave","guardian","guardian");await Go(new(0,39,0));currentContainer=bench;currentStation="calibration_bench";Craft(catalog.Recipes["beacon_sigil"]);Check(world.State.Inventory.Count("beacon_sigil")==1,"sigil craft failed");currentContainer=null;await Repair(2);Mark();
            stage="physical bridge, reflector puzzle, power-room guardian";var vault=layout.Site("vault");await Go(vault.At(-24,5,-16));var socket=vault.At(-22,5,-16);InteractMainline(socket,"power_socket");Click("连接电桥");Check(!world.State.Mainline.Nodes.Contains("bridge"),"unbuilt bridge accepted");for(int x=-22;x<=-17;x++){var p=vault.At(x,4,-16);world.Set(p,catalog.Number("plank"));renderer!.RefreshEdit(p);}Click("连接电桥");Check(world.State.Mainline.Nodes.Contains("bridge"),"physical bridge not recognized");
            var reflector=vault.At(20,1,-16);await Go(reflector+new Cell(-2,0,0));world.State.Mainline.ReflectorRotation=(layout.ReflectorAnswer+1)%4;InteractMainline(reflector,"reflector");Click("校准供能");Check(!world.State.Mainline.Nodes.Contains("reflector"),"wrong reflector direction accepted");for(int i=0;i<3;i++)Click("旋转 90°");Click("校准供能");Check(world.State.Mainline.Nodes.Contains("reflector"),"rotated reflector failed");
            await Go(vault.At(13,1,16));world.State.Population.NextSpawnTime=0;TickPopulation(1);await Until(()=>creatures.Any(c=>c.State.Home=="vault_guard"));foreach(var c in creatures)c.SetPhysicsProcess(false);creatures.Single(c=>c.State.Home=="vault_guard").Damage(10000);Check(world.State.Mainline.Nodes.Count==3,"guard node not recorded");Mark();
            stage="giant collision, shield, four anchors and projectile callback";var console=vault.At(0,1,6);await Go(console+new Cell(0,0,3));InteractMainline(console,"core_console");Click("启动核心 · 挑战熔心巨像");var giant=creatures.Single(c=>c.Definition.Id=="colossus");giant.SetPhysicsProcess(false);var shape=(BoxShape3D)giant.GetChildren().OfType<CollisionShape3D>().Single().Shape;Check(shape.Size.Y>10&&shape.Size.X>=4.5f,"giant physics remained small");double hp=giant.State.Health;giant.Damage(100);Check(giant.State.Health==hp,"shield phase damaged giant");
            for(int i=0;i<2;i++)Check(InteractMainline(layout.Anchor(i),"energy_anchor"),"anchor Q failed");
            for(int i=2;i<4;i++){var a=layout.Anchor(i);await Go(vault.At(0,1,8));var shot=new ProjectileState{Position=[a.X+.5,a.Y+.5,a.Z+3],Velocity=[0,0,-10],Remaining=1};Check(projectiles!.Fire(shot),"anchor arrow not fired");SetScreen("play");await Until(()=>world.State.Mainline.Anchors.Contains(i));SetScreen("journal");}
            Check(world.State.Mainline.Anchors.Count==4,"anchor phase incomplete");giant.State.AttackStyle="bolt";giant.Damage(40);Check(giant.State.Health==hp-10,"core armor reduction failed");giant.State.AttackStyle="slam";giant.State.AttackCooldown=3;giant.Damage(40);Check(giant.State.Health==hp-50,"exposed core window failed");
            stage="giant attack remains latched across phase boundary and low-health windup";world.State.Health=100;double received=0;var originalAttack=giant.AttackPlayer;giant.AttackPlayer=damage=>{received=damage;originalAttack(damage);};world.State.Mainline.BattleTime=6.05;giant.State.AttackStyle="slam";giant.State.AttackWindup=.03;giant.State.AttackAim=[player!.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];giant.State.AttackCooldown=0;SetScreen("play");giant.SetPhysicsProcess(true);await Until(()=>giant.State.AttackWindup<0);giant.SetPhysicsProcess(false);SetScreen("journal");Check(received==65&&!giant.IsCharging,"telegraphed slam changed into another phase at release");
            giant.State.Health=500;giant.State.AttackStyle="";giant.State.AttackCooldown=0;giant.State.AttackWindup=-1;world.State.Mainline.BattleTime=12.05;SetScreen("play");giant.SetPhysicsProcess(true);await Until(()=>giant.IsPreparingAttack);giant.SetPhysicsProcess(false);SetScreen("journal");Check(giant.State.AttackWindup<=1.12&&giant.State.AttackStyle=="bolt","rage phase did not shorten windup");giant.State.Health=hp-50;giant.State.AttackStyle="slam";giant.State.AttackWindup=-1;giant.State.AttackAim=null;giant.State.AttackCooldown=3;giant.AttackPlayer=originalAttack;Mark();
            stage="active battle save and actual world reconstruction";RequestSave();await Until(()=>saveCallbacks.Count==0);var loaded=saves.Load(world.State.WorldId).Snapshot;Check(loaded.Mainline.FinaleActive&&loaded.Mainline.Anchors.Count==4&&loaded.Creatures.Single(c=>c.Kind=="colossus").Health==hp-50,"active battle save lost state");StartWorld(loaded);player!.SetPhysicsProcess(false);SetScreen("journal");await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));foreach(var c in creatures)c.SetPhysicsProcess(false);Check(world!.State.Mainline.Nodes.Count==3&&world.State.Equipment.Slots[1]!.HeatLining,"world reconstruction lost permanent progress");Mark();
            stage="finale death moves bag to entrance and keeps milestones";SetScreen("play");Hurt(10000);Check(screen=="death"&&!world.State.Mainline.FinaleActive&&world.State.Mainline.Anchors.Count==0,"death did not reset battle");Check(world.State.Beacons.Count==3&&world.State.Mainline.Nodes.Count==3&&world.State.Graves.Last().Position[1]==41,"death lost milestones or bag trapped underground");Click("从营地复活");player.SetPhysicsProcess(false);await Go(console+new Cell(0,0,3));InteractMainline(console,"core_console");Click("启动核心 · 挑战熔心巨像");foreach(var c in creatures)c.SetPhysicsProcess(false);for(int i=0;i<4;i++)InteractMainline(layout.Anchor(i),"energy_anchor");giant=creatures.Single(c=>c.Definition.Id=="colossus");giant.Damage(100000);Check(world.State.Mainline.Completed&&!world.State.Mainline.FinaleActive&&world.State.Mainline.Proofs.Contains("dawn"),"actual giant death failed completion");ShowMainlineJournal();Check(Children(overlay).OfType<Label>().Any(l=>l.Text.Contains("已完成")),"completion not shown");RequestSave();await Until(()=>saveCallbacks.Count==0);Check(saves.Load(world.State.WorldId).Snapshot.Mainline.Completed,"completion lost on disk");Mark();Record(true,"");GD.Print("MAINLINE_PASS: native UI, actual elite deaths, facilities, physical nodes, projectile anchors, giant shield/core, save reconstruction, death reset and completion");QuitCleanly();
        }catch(Exception error){Record(false,error.ToString());GD.PushError("MAINLINE_FAILED: "+error);QuitCleanly(9);}
    }
    private async void RunMainlinePreview()
    {
        string folder=ProjectSettings.GlobalizePath("res://docs/previews");Directory.CreateDirectory(folder);
        try{
            if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("模型截图需要渲染窗口");
            var viewport=new SubViewport{OwnWorld3D=true,Size=new(800,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
            viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("18232f"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.75f}});viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-40,-25,0),LightEnergy=1.25f});
            var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Current=true};viewport.AddChild(camera);using var sheet=Image.CreateEmpty(1600,1300,false,Image.Format.Rgba8);int index=0;
            foreach(string kind in new[]{"mine_warden","furnace_warden","observatory_warden","colossus"}){
                var model=CreatureModel.Create(kind);viewport.AddChild(model);bool giant=kind=="colossus";float k=giant?4.5f:1;camera.Size=giant?17:4.8f;camera.Position=new(3.5f*k,2.7f*k,-6*k);camera.LookAt(new(giant?1:0,model.BodyHeight*.5f,0));
                var ground=new MeshInstance3D{Mesh=new BoxMesh{Size=new(giant?24:7,.10f,giant?24:7)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new("3b4858")},Position=new(0,-.12f,0)};viewport.AddChild(ground);
                var label=new Label{Text=creatureDefs.Single(d=>d.Id==kind).Name+(giant?" · 10.9 格高 / 人物 1.8 格":""),Position=new(24,22)};label.AddThemeFontSizeOverride("font_size",28);viewport.AddChild(label);
                Node3D? avatar=null;if(giant){avatar=PresentationModel.Avatar(null);avatar.Position=new(5.2f,0,0);viewport.AddChild(avatar);}
                for(int i=0;i<6;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var picture=viewport.GetTexture().GetImage();picture.SavePng(Path.Combine(folder,kind+".png"));sheet.BlitRect(picture,new(0,0,800,650),new(index%2*800,index/2*650));index++;
                foreach(Node n in new Node?[]{model,ground,label,avatar}.OfType<Node>()){viewport.RemoveChild(n);n.QueueFree();}
            }
            sheet.SavePng(Path.Combine(folder,"mainline-monsters.png"));RemoveChild(viewport);viewport.QueueFree();GD.Print("MAINLINE_PREVIEW_PASS: "+folder);QuitCleanly();
        }catch(Exception error){GD.PushError("MAINLINE_PREVIEW_FAILED: "+error);QuitCleanly(9);}
    }
}
