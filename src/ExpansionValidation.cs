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
    private bool expansionCheck;
    private async void RunExpansionCheck()
    {
        static IEnumerable<Node> Descendants(Node root){foreach(var child in root.GetChildren()){yield return child;foreach(var grandchild in Descendants(child))yield return grandchild;}}
        string stage="start";var timer=Stopwatch.StartNew();string directory=ProjectSettings.GlobalizePath("res://test-results/expansion");Directory.CreateDirectory(directory);
        void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(stage+": "+reason);}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Until(Func<bool> ready){double deadline=timer.Elapsed.TotalSeconds+40;while(!ready()){if(timer.Elapsed.TotalSeconds>deadline)throw new TimeoutException(stage);await Frames(1);}}
        void Aim(Vector3 at){player!.Camera.LookAt(at);}
        Creature Animal(string id,Vector3 position){var d=creatureDefs.Single(d=>d.Id==id);var s=new CreatureState{Kind=id,Health=d.Health,Position=[position.X,position.Y,position.Z],Persistent=true};world!.State.Creatures.Add(s);SpawnCreature(s);return creatures.Last();}
        void Give(string id){world!.State.Inventory.Slots[0]=new(id,1);selected=0;}
        try{
            settings.ViewDistance=3;StartWorld(new(){Name="物品与生物扩充验证",Seed=16,Creative=true,Time=900,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});
            player!.SetPhysicsProcess(false);foreach(var c in creatures)c.QueueFree();creatures.Clear();world!.State.Creatures.Clear();
            for(int x=-5;x<=25;x++)for(int z=-5;z<=25;z++){world.Set(new(x,39,z),catalog.Number("stone"));for(int y=40;y<=43;y++)world.Set(new(x,y,z),0);}
            player.GlobalPosition=new(.5f,40,.5f);world.State.Position=[.5,40,.5];renderer!.RefreshEdit(new(0,39,0));await Until(()=>renderer.IsTerrainReady(player.GlobalPosition));
            stage="actual milking, shearing, taming, pet armor";var cow=Animal("cow",new(3,40,.5f));cow.SetPhysicsProcess(false);Aim(cow.GlobalPosition+Vector3.Up*.5f);Give("bucket");world.State.Creative=false;Use();Check(world.State.Inventory.Count("milk_bucket")==1,"right-use milk failed");
            var sheep=Animal("sheep",new(3,40,2));sheep.SetPhysicsProcess(false);Aim(sheep.GlobalPosition+Vector3.Up*.5f);Give("shears");Use();Check(world.State.Graves.Sum(g=>g.Items.Count("wool"))==3,"right-use shears failed");
            var wolf=Animal("wolf",new(3,40,-2));wolf.SetPhysicsProcess(false);Aim(wolf.GlobalPosition+Vector3.Up*.5f);Give("bone");Use();Check(wolf.State.Tamed,"right-use tame failed");Give("wolf_armor");Use();Check(wolf.State.Armor?.Id=="wolf_armor","wolf armor failed");
            stage="actual riding and safe dismount";var horse=Animal("horse",new(3,40,4));horse.SetPhysicsProcess(false);Aim(horse.GlobalPosition+Vector3.Up);Give("apple");Use();Give("saddle");Use();Check(horse.State.Tamed&&horse.State.Saddled,"horse preparation failed");Interact();var ride=Descendants(overlay).OfType<Button>().Single(b=>b.Text.StartsWith("骑乘"));ride.EmitSignal(Button.SignalName.Pressed);player.SetPhysicsProcess(true);Input.ActionPress("forward");await Frames(12);Input.ActionRelease("forward");Check(player.Mount==horse&&player.GlobalPosition.DistanceTo(horse.GlobalPosition)<2,"mount adapter failed");Check(player.Dismount(),"safe dismount failed");player.SetPhysicsProcess(false);player.GlobalPosition=new(.5f,40,.5f);world.State.Position=[.5,40,.5];
            stage="water-ray fishing and bowl/potion returns";var water=new Cell(2,40,0);world.Set(water,catalog.Number("water"));Aim(new(2.5f,40.5f,.5f));Give("fishing_rod");Use();Check(fishingWater==water,"water targeting missing");world.Advance(fishBite-world.State.Time+.1);Use();Check(world.State.Graves.Sum(g=>g.Items.Count("raw_cod"))>=1&&fishingWater==null,"fish catch failed");
            Give("healing_potion");world.State.Health=40;Use();Check(world.State.Health==70&&world.State.Inventory.Count("glass_bottle")>0,"potion UI missing effect");world.State.Hunger=40;Give("mushroom_stew");Use();Check(world.State.Hunger==76&&world.State.Inventory.Count("bowl")==1,"bowl return failed");
            stage="real brewing timed output and enchanting UI";var bench=new Cell(0,40,3);world.State.Creative=true;world.Set(bench,0);Give("brewing_stand");Check(world.Place(bench,"brewing_stand",false,out var error),error);renderer.RefreshEdit(bench);Aim(new(.5f,40.5f,3.5f));Interact();Check(currentStation=="brewing_stand"&&screen=="recipes","brewing UI not routed");world.State.Creative=false;foreach(var need in catalog.Recipes["healing_potion"].Inputs)world.State.Inventory.Add(need.Key,need.Value,catalog);Check(world.Craft(catalog.Recipes["healing_potion"],bench,world.State.Position,out error),error);world.Advance(12);Check(world.State.Entities[bench.ToString()].Storage.Count("healing_potion")==1,"timed output failed");
            var altar=new Cell(2,40,3);world.State.Creative=true;world.Set(altar,0);Give("enchanting_table");Check(world.Place(altar,"enchanting_table",false,out error),error);renderer.RefreshEdit(altar);SetScreen("play");Aim(new(2.5f,40.5f,3.5f));Interact();Check(screen=="enchant","enchant UI missing");Give("gold_helmet");ShowEnchantments();world.State.Creative=false;world.State.Experience=20;world.State.Inventory.Add("crystal",1,catalog);var enchant=Descendants(overlay).OfType<Button>().First(b=>b.Text=="保护");enchant.EmitSignal(Button.SignalName.Pressed);Check(world.State.Inventory.Slots[0]?.Protection==2,"enchant callback lost item");
            stage="distinct native models, armor slots, sprites and save";world.State.Creative=true;SetScreen("play");
            int index=0;foreach(var d in creatureDefs.Where(d=>d.Id is "cow" or "pig" or "sheep" or "chicken" or "wolf" or "cat" or "horse" or "fox" or "armadillo" or "cod" or "squid" or "turtle" or "zombie" or "skeleton" or "creeper" or "spider")){var c=Animal(d.Id,new(8+index%4*3,40,3+index/4*3));c.SetPhysicsProcess(false);var model=c.GetChild<CreatureModel>(0);Check(model.Generated&&model.GeneratedSkeleton?.GetBoneCount()>=6&&model.BodyHeight>0,"missing bound native model "+d.Id);index++;}
            var meshes=creatures.GroupBy(c=>c.Definition.Id).Select(g=>Descendants(g.First().GetChild<CreatureModel>(0)).OfType<MeshInstance3D>().First(m=>m.Skin!=null).Mesh.GetInstanceId()).ToArray();Check(meshes.Distinct().Count()==meshes.Length,"different species reused the same source mesh");
            stage="long-range projectile reduction";world.State.Creative=false;world.State.Health=100;world.State.Hunger=50;world.State.Effects=new();world.State.Equipment=new(5);for(int i=0;i<4;i++)world.State.Equipment.Slots[i]=new("chain_"+new[]{"helmet","chestplate","leggings","boots"}[i],1);SetScreen("play");await Until(()=>renderer.IsTerrainReady(new(18,40,.5f)));Check(projectiles!.Fire(new(){Position=[18,40.9,.5],Velocity=[-14,0,0],Hostile=true,Arrow=true,Damage=20,Remaining=2,Radius=.06}),"projectile fire failed");await Until(()=>projectiles.Active==0);Check(Math.Abs(world.State.Health-91.264)<.01,"chain armor did not reduce long-range projectile damage");
            world.State.Creative=true;stage="equipment slots and content save";for(int i=0;i<4;i++)world.State.Equipment.Slots[i]=new("ember_"+new[]{"helmet","chestplate","leggings","boots"}[i],1,30){Protection=1};currentContainer=null;ShowInventory();Check(Descendants(overlay).OfType<UI.SlotButton>().Count(b=>b.SurfaceIndex==3)==5,"equipment slots missing");await Frames(3);
            using(var sheet=Image.CreateEmpty(12*64,((catalog.Items.Count+11)/12)*64,false,Image.Format.Rgba8)){sheet.Fill(new("202526"));int n=0;foreach(var d in catalog.Items.Values){using var sprite=UI.ItemSprites.CreateImage(d);Check(sprite.GetData().Where((v,i)=>i%4==3&&v>0).Any(),"empty icon "+d.Id);sheet.BlitRect(sprite,new(0,0,32,32),new(n%12*64+16,n/12*64+16));n++;}sheet.SavePng(Path.Combine(directory,"items-native.png"));}
            if(DisplayServer.GetName()!="headless"){using var screenshot=GetViewport().GetTexture().GetImage();screenshot.SavePng(Path.Combine(directory,"equipment-native.png"));SetScreen("play");player.Camera.Position=new(0,8,14);Aim(new(12,41,8));await Frames(6);using var mobs=GetViewport().GetTexture().GetImage();mobs.SavePng(Path.Combine(directory,"creatures-native.png"));}
            SetScreen("saving");var expected=world.State.Copy();var save=saveQueue.Enqueue(world.State);await Until(()=>save.IsCompleted);save.GetAwaiter().GetResult();var loaded=saves.Load(world.State.WorldId).Snapshot;Check(loaded.Creatures.Single(c=>c.Id==wolf.State.Id).Armor?.Id=="wolf_armor"&&loaded.Creatures.Single(c=>c.Id==horse.State.Id).Saddled&&loaded.Equipment.Slots[1]?.Protection==1,"saved content missing");
            File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed=true,stage,seconds=timer.Elapsed.TotalSeconds,items=catalog.Items.Count,recipes=catalog.Recipes.Count,creatures=creatureDefs.Length,rendered=DisplayServer.GetName()!="headless",controlledSetup=true,grantedFixtureMaterials=true},Catalog.JsonOptions));GD.Print("EXPANSION_PASS: right-use milking/shearing/taming, companion armor, riding/dismount, water fishing, potion/bowl returns, brewing/enchant UI, 16 native models, equipment slots and durable save");QuitCleanly();
        }catch(Exception e){File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed=false,stage,error=e.ToString()},Catalog.JsonOptions));GD.PushError("EXPANSION_FAILED: "+e);QuitCleanly(12);}
    }
}


