using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool estateCheck;
    private async void RunEstateCheck()
    {
        bool reload=OS.GetCmdlineUserArgs().Contains("--estate-reload");var timer=Stopwatch.StartNew();string stage="setup";
        var samples=new List<object>();var construction=new Dictionary<Cell,string>();int placed=0;double walked=0;
        string directory=ProjectSettings.GlobalizePath("res://test-results/estate");Directory.CreateDirectory(directory);
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var n in Descendants(child))yield return n;}}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> ready,double seconds=30){double end=timer.Elapsed.TotalSeconds+seconds;while(!ready()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        void Pose(Vector3 position){player!.GlobalPosition=position;player.Velocity=Vector3.Zero;world!.State.Position=[position.X,position.Y,position.Z];}
        async Task Ready(){await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition),60);for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        SlotButton Slot(int surface,int index)=>Descendants(overlay).OfType<SlotButton>().Single(b=>b.SurfaceIndex==surface&&b.SlotIndex==index);
        void Click(int surface,int index)=>Slot(surface,index).EmitSignal(Button.SignalName.Pressed);
        void PlaceAt(Cell cell,string id,int facing=0){Check(world!.Place(cell,id,false,out string error,facing),"place "+id+" at "+cell+": "+error);construction[cell]=id;placed++;if(id=="door")construction[cell+new Cell(0,1,0)]=id;}
        void CheckState(WorldSnapshot actual,WorldSnapshot expected){
            Check(actual.Changes.Count==expected.Changes.Count&&expected.Changes.All(p=>actual.Changes.GetValueOrDefault(p.Key)==p.Value),"saved building cells differ");
            Check(JsonSerializer.Serialize(actual.Entities.OrderBy(p=>p.Key),Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Entities.OrderBy(p=>p.Key),Catalog.JsonOptions),"saved containers, doors, rotations, crop stages or recipes differ");
            Check(JsonSerializer.Serialize(actual.Inventory,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Inventory,Catalog.JsonOptions)&&JsonSerializer.Serialize(actual.Equipment,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Equipment,Catalog.JsonOptions),"saved inventory or wear differs");
            Check(actual.Respawn.SequenceEqual(expected.Respawn)&&actual.Time==expected.Time,"saved bed or world time differs");
        }
        bool Ray(Vector3 from,Vector3 to){using var query=PhysicsRayQueryParameters3D.Create(from,to);query.Exclude=[player!.GetRid()];using var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);return hit.Count>0;}
        void CheckCollision(){
            Check(Ray(new(15.5f,84,21.5f),new(15.5f,81.5f,21.5f))&&Ray(new(16.5f,84,21.5f),new(16.5f,81.5f,21.5f)),"bridge floor collision missing across chunk edge");
            Check(Ray(new(13.5f,83,-1),new(13.5f,83,1)),"house wall collision missing");
            Check(Ray(new(16.5f,83,-1),new(16.5f,83,1)),"saved closed house door has no collision");
        }
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(directory,reload?"reload.json":"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedImage=false,controlledSetup=true,creativeConstruction=!reload,grantedFixtureMaterials=!reload,testTeleports=true,clockAcceleration=!reload,humanPlaytest=false,placed,walked,samples},Catalog.JsonOptions));
        try{
            if(reload){
                var expected=JsonSerializer.Deserialize<WorldSnapshot>(File.ReadAllText(Path.Combine(directory,"reload-world.json")),Catalog.JsonOptions)!;
                var actual=saves.Load(expected.WorldId).Snapshot;CheckState(actual,expected);StartWorld(actual);player!.SetPhysicsProcess(false);SetScreen("pause");await Ready();CheckCollision();
                currentContainer=new Cell(24,82,4);ShowInventory();await Frames();Check(Descendants(overlay).OfType<SlotButton>().Count(s=>s.SurfaceIndex==1)==27,"cold warehouse container UI missing");
                Check(world!.State.Entities[currentContainer.Value.ToString()].Storage.Slots[0]?.Durability==73,"cold warehouse repaired armor");
                samples.Add(new{stage="independent EXE estate reload",cells=actual.Changes.Count,entities=actual.Entities.Count,warehouseWear=73});Record(true,"");GD.Print("ESTATE_RELOAD_PASS: independent EXE preserves full buildings, warehouse wear, rotated components, farm, bed and native collision");QuitCleanly();return;
            }
            settings.UiScale=1;StartWorld(new(){Name="住宅仓库农场桥梁原生回归",Seed=42,Creative=true,Position=[16.5,82.05,7.5],GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);SetScreen("pause");
            stage="controlled terrain and complete construction";
            for(int x=6;x<=36;x++)for(int z=-3;z<=45;z++)for(int y=79;y<=87;y++)world!.Set(new(x,y,z),0);
            for(int x=6;x<=36;x++)for(int z=-3;z<=45;z++)if(z<20||z>22||x<=8||x>=34)PlaceAt(new(x,80,z),"stone");
            void House(int left,string wall,string floor){
                for(int x=left;x<=left+6;x++)for(int z=0;z<=6;z++){PlaceAt(new(x,81,z),floor);PlaceAt(new(x,85,z),"tile");
                    if(x==left||x==left+6||z==0||z==6)for(int y=82;y<=84;y++){
                        if(x==left+3&&z==0&&y<84)continue;
                        PlaceAt(new(x,y,z),y==83&&z==3&&(x==left||x==left+6)?"window":wall,x==left?1:x==left+6?3:0);
                    }
                }
                PlaceAt(new(left+3,82,0),"door");
            }
            House(13,"plank","plank");House(23,"brick","cobble");
            var bed=new Cell(14,82,4);PlaceAt(bed,"bed");PlaceAt(new(18,84,4),"lantern");PlaceAt(new(17,82,4),"bookshelf");PlaceAt(new(16,82,3),"rug");
            var chest=new Cell(24,82,4);var secondChest=new Cell(28,82,4);var furnace=new Cell(26,82,4);var camp=new Cell(28,82,2);var bench=new Cell(24,82,2);
            PlaceAt(chest,"chest");PlaceAt(secondChest,"chest");PlaceAt(furnace,"furnace",2);PlaceAt(camp,"campfire");PlaceAt(bench,"workbench");
            for(int x=13;x<=19;x++)for(int z=10;z<=14;z++)PlaceAt(new(x,81,z),"farmland");
            for(int x=8;x<=34;x++)for(int z=20;z<=22;z++){world!.Set(new(x,79,z),catalog.Number("water"));PlaceAt(new(x,81,z),"plank");if(z!=21)PlaceAt(new(x,82,z),"fence");}
            for(int facing=0;facing<4;facing++){PlaceAt(new(9+facing*3,81,17),"stairs",facing);PlaceAt(new(9+facing*3,81,18),"slab");PlaceAt(new(23+facing*3,81,17),"roof",facing);}
            var gallery=catalog.Items.Values.Where(d=>d.Block&&d.Id is not("water" or "bedrock" or "beacon")).OrderBy(d=>d.Id).ToArray();
            for(int i=0;i<gallery.Length;i++)PlaceAt(new(8+i%8*3,81,28+i/8*3),gallery[i].Id,i%4);
            await Ready();CheckCollision();samples.Add(new{stage,house=true,warehouse=true,farm=true,bridge=true,placeableBlockKinds=gallery.Length,constructionCells=construction.Count,placed,spansChunkEdges=new[]{16,32}});
            stage="door collision and real bridge traversal";
            var door=new Cell(16,82,0);var partner=world!.DoorPartner(door)!.Value;
            bool Blocks()=>player.TestMove(new Transform3D(Basis.Identity,new(16.5f,82.04f,-1.1f)),new(0,0,2.4f));
            Check(Blocks(),"closed house door did not block player capsule");
            foreach(var p in new[]{door,partner}){world.State.Entities[p.ToString()].Open=true;world.Set(p,world.Get(p));renderer!.RefreshEdit(p);}await Ready();Check(!Blocks(),"open house doorway blocked player capsule");
            foreach(var p in new[]{door,partner}){world.State.Entities[p.ToString()].Open=false;world.Set(p,world.Get(p));renderer!.RefreshEdit(p);}await Ready();Check(Blocks(),"reclosed house door lost collision");
            Pose(new(9.5f,82.05f,21.5f));player.Rotation=Vector3.Zero;player.Camera.Rotation=Vector3.Zero;await Ready();await Until(()=>renderer!.IsTerrainReady(new(33,82,21)),60);
            SetScreen("play");player.SetPhysicsProcess(true);Input.ActionPress("right");float lowest=player.GlobalPosition.Y;
            await Until(()=>{lowest=Math.Min(lowest,player.GlobalPosition.Y);return player.GlobalPosition.X>=33;},15);Input.ActionRelease("right");player.SetPhysicsProcess(false);SetScreen("pause");walked=player.GlobalPosition.X-9.5;
            Check(lowest>=81.9f&&Math.Abs(player.GlobalPosition.Z-21.5f)<.15f,"player fell or left bridge lane");samples.Add(new{stage,walked,lowest,realPhysicsMovement=true});
            stage="actual bed and warehouse slot transfers";Pose(new(14.5f,82.05f,2.5f));SetScreen("play");player.Camera.LookAt(new Vector3(14.5f,82.25f,4.5f));Interact();Check(world.State.Respawn.SequenceEqual(new double[]{14.5,82.7,4.5}),"house bed did not set respawn");SetScreen("pause");
            world.State.Creative=false;world.State.Inventory=new();world.State.Inventory.Slots[0]=new("iron_helmet",1,73);world.State.Inventory.Slots[1]=new("crystal",7);currentContainer=chest;ShowInventory();await Frames();Click(0,0);Click(1,0);
            using(var drag=new Godot.Collections.Dictionary{{"surface",0},{"slot",1}})Slot(1,1)._DropData(Vector2.Zero,drag);await Frames();
            Check(world.State.Entities[chest.ToString()].Storage.Slots[0]?.Durability==73&&world.State.Entities[chest.ToString()].Storage.Count("crystal")==7,"warehouse click/drag changed items or wear");
            Input.ActionPress("inventory_transfer");Click(1,1);Input.ActionRelease("inventory_transfer");Check(world.State.Inventory.Count("crystal")==7,"warehouse shortcut lost crystal");currentContainer=secondChest;ShowInventory();await Frames();int crystal=Array.FindIndex(world.State.Inventory.Slots,s=>s?.Id=="crystal");Click(0,crystal);Click(1,0);
            Check(world.State.Inventory.Count("crystal")==0&&world.State.Entities[secondChest.ToString()].Storage.Count("crystal")==7,"second warehouse transfer failed");Check(!world.Break(chest,3,out _),"nonempty warehouse chest dismantled");
            stage="all timed production and safe dismantling";var outputs=new Dictionary<string,int>();
            foreach(var recipe in catalog.Recipes.Values.Where(r=>r.Seconds>0).OrderBy(r=>r.Id)){
                foreach(var input in recipe.Inputs)Check(world.State.Inventory.Add(input.Key,input.Value,catalog)==0,"fixture ingredients overflow");
                var station=recipe.Station=="furnace"?furnace:camp;Pose(new(station.X+.5f,82.05f,station.Z-1.5f));currentContainer=station;currentStation=recipe.Station;ShowRecipes();Craft(recipe);
                var facility=world.State.Entities[station.ToString()];Check(facility.RecipeId==recipe.Id,"native craft did not start "+recipe.Id);Check(!world.Break(station,3,out _),"busy facility dismantled "+recipe.Id);
                world.Advance(recipe.Seconds);Check(facility.RecipeId==null&&facility.Storage.Count(recipe.Output)>=recipe.Count,"production did not finish "+recipe.Id);Check(!world.Break(station,3,out _),"output-containing facility dismantled");
                outputs[recipe.Output]=outputs.GetValueOrDefault(recipe.Output)+recipe.Count;ShowInventory();await Frames();
                for(int i=0;i<facility.Storage.Slots.Length;i++)if(facility.Storage.Slots[i]!=null){Input.ActionPress("inventory_transfer");Click(1,i);Input.ActionRelease("inventory_transfer");}
                Check(facility.Storage.Slots.All(s=>s==null),"production output transfer incomplete");
            }
            Check(world.Break(camp,3,out _)&&world.Get(camp)==0,"idle empty campfire could not be removed");PlaceAt(camp,"campfire");
            samples.Add(new{stage,timedRecipes=outputs.Count,outputKinds=outputs.Keys.Order().ToArray(),warehouseArmorWear=73});
            stage="three crop cycles harvest and replant";currentContainer=null;SetScreen("pause");var crops=new List<(Cell Cell,string Seed)>();
            foreach(var (x,seed) in new[]{(14,"carrot_seed"),(16,"wheat_seed"),(18,"potato_seed")}){Check(world.State.Inventory.Add(seed,3,catalog)==0,"seed fixture overflow");for(int z=10;z<=12;z++){var ground=new Cell(x,81,z);Check(world.Plant(ground,seed,out string error),error);crops.Add((ground+new Cell(0,1,0),seed));}}
            double plantedAt=world.State.Time;world.Advance(180);Check(crops.Where(c=>c.Seed=="carrot_seed").All(c=>world.State.Entities[c.Cell.ToString()].Stage==3),"carrots not mature at 180 seconds");Check(crops.Where(c=>c.Seed!="carrot_seed").All(c=>world.State.Entities[c.Cell.ToString()].Stage<3),"slower crops matured early");
            foreach(var c in crops.Where(c=>c.Seed=="carrot_seed"))Harvest(c.Cell,world.State.Entities[c.Cell.ToString()]);var replanted=new Cell(14,81,10);Check(world.Plant(replanted,"carrot_seed",out _),"replant failed");
            world.Advance(90);foreach(var c in crops.Where(c=>c.Seed=="wheat_seed")){Check(world.State.Entities[c.Cell.ToString()].Stage==3,"wheat not mature at 270 seconds");Harvest(c.Cell,world.State.Entities[c.Cell.ToString()]);}
            world.Advance(90);foreach(var c in crops.Where(c=>c.Seed=="potato_seed")){Check(world.State.Entities[c.Cell.ToString()].Stage==3,"potatoes not mature at 360 seconds");Harvest(c.Cell,world.State.Entities[c.Cell.ToString()]);}
            Check(world.State.Entities[(replanted+new Cell(0,1,0)).ToString()].Stage==3&&world.State.Inventory.Count("carrot")>=9&&world.State.Inventory.Count("grain")>=9&&world.State.Inventory.Count("potato")>=9,"crop harvest/replant conservation failed");
            samples.Add(new{stage,plots=crops.Count,cycles=new[]{180,270,360},replant=true,elapsedWorldSeconds=world.State.Time-plantedAt});
            stage="cross-chunk edits unload and native reload";world.State.Creative=true;
            foreach(var p in new[]{new Cell(15,81,21),new Cell(16,81,21)}){Check(world.Break(p,3,out _),"bridge edit break");PlaceAt(p,"glass");renderer!.RefreshEdit(p);}await Ready();CheckCollision();
            Pose(new(256.5f,82.05f,21.5f));await Frames(5);Check(!renderer!.GetChildren().OfType<Node3D>().Any(n=>n.Position==new Vector3(0,64,16)),"old bridge chunk did not unload");
            Pose(new(16.5f,82.05f,7.5f));await Ready();CheckCollision();foreach(var p in construction)Check(world.Get(p.Key)==catalog.Number(p.Value),"construction changed after unload "+p.Key);
            stage="full estate save backup restore";SetScreen("pause");world.State.Creative=false;currentContainer=null;var expectedWorld=world.State.Copy();RequestSave();await Until(()=>saveCallbacks.Count==0);CheckState(saves.Load(expectedWorld.WorldId).Snapshot,expectedWorld);
            string backup=saves.Backup(expectedWorld.WorldId);string restored=saves.RestoreBackup(Path.GetFileName(backup));Check(restored!=expectedWorld.WorldId,"backup restore overwrote source world");CheckState(saves.Load(restored).Snapshot,expectedWorld);CheckState(saves.Load(expectedWorld.WorldId).Snapshot,expectedWorld);
            File.WriteAllText(Path.Combine(directory,"reload-world.json"),JsonSerializer.Serialize(expectedWorld,Catalog.JsonOptions));samples.Add(new{stage,worldId=expectedWorld.WorldId,cells=expectedWorld.Changes.Count,entities=expectedWorld.Entities.Count,backupRestoredAs=restored});Record(true,"");GD.Print("ESTATE_PASS: complete controlled housing/warehouse/farm/bridge, 45 placeable blocks, real traversal/UI/production, unloaded collisions, full save and backup");QuitCleanly();
        }catch(Exception error){Input.ActionRelease("right");Input.ActionRelease("inventory_transfer");Record(false,error.Message);GD.PushError("ESTATE_FAILED: "+error);QuitCleanly(10);}
    }
}
