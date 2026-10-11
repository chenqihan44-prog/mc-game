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
    private bool equipmentCheck;
    private async void RunEquipmentCheck()
    {
        bool reload=OS.GetCmdlineUserArgs().Contains("--equipment-reload");var timer=Stopwatch.StartNew();string stage="setup";var samples=new List<object>();
        string directory=ProjectSettings.GlobalizePath("res://test-results/equipment");Directory.CreateDirectory(directory);
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var n in Descendants(child))yield return n;}}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> done){double end=timer.Elapsed.TotalSeconds+15;while(!done()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        SlotButton Slot(int surface,int index)=>Descendants(overlay).OfType<SlotButton>().Single(s=>s.SurfaceIndex==surface&&s.SlotIndex==index);
        void Click(int surface,int index)=>Slot(surface,index).EmitSignal(Button.SignalName.Pressed);
        int Find(string id)=>Array.FindIndex(world!.State.Inventory.Slots,s=>s?.Id==id);
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(directory,reload?"reload.json":"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedImage=false,controlledSetup=true,grantedFixtureMaterials=!reload,samples},Catalog.JsonOptions));
        try{
            if(reload){
                var expected=JsonSerializer.Deserialize<WorldSnapshot>(File.ReadAllText(Path.Combine(directory,"reload-world.json")),Catalog.JsonOptions)!;
                var loaded=saves.Load(expected.WorldId).Snapshot;Check(loaded.FormatVersion==4,"equipment save not format4");
                for(int i=0;i<5;i++){var a=loaded.Equipment.Slots[i];var b=expected.Equipment.Slots[i];Check(a?.Id==b?.Id&&a?.Count==b?.Count&&a?.Durability==b?.Durability,"cold reload changed equipment "+i);}
                Check(JsonSerializer.Serialize(loaded.Graves,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Graves,Catalog.JsonOptions),"cold reload changed overflow armor wear");
                StartWorld(loaded);player!.SetPhysicsProcess(false);await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));ShowInventory();await Frames();Check(Descendants(overlay).OfType<SlotButton>().Count(s=>s.SurfaceIndex==3)==5,"cold reload equipment UI missing");
                samples.Add(new{stage="cold equipment reload",armorPoints=Equipment.ArmorPoints(world!.State,catalog),offhand=world.State.Equipment.Slots[4]!.Id});Record(true,"");GD.Print("EQUIPMENT_RELOAD_PASS: actual independent EXE load preserves armor/offhand/durability and five UI slots");QuitCleanly();return;
            }
            StartWorld(new(){Name="护甲与副手原生回归",Seed=42,Hunger=50,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));
            world!.State.Inventory.Add("workbench",1,catalog);world.State.Inventory.Add("iron_ingot",24,catalog);var bench=new Cell(2,39,0);world.Set(bench,0);Check(world.Place(bench,"workbench",false,out var error),error);renderer!.RefreshEdit(bench);
            currentContainer=bench;currentStation="workbench";stage="four actual armor crafts";
            foreach(string id in new[]{"iron_helmet","iron_chestplate","iron_leggings","iron_boots"}){Craft(catalog.Recipes[id]);Check(world.State.Inventory.Count(id)==1,"armor craft refused "+id);}
            Check(world.State.Inventory.Count("iron_ingot")==0,"armor recipe costs changed");currentContainer=null;
            stage="native MC equipment layout";foreach(float scale in new[]{.8f,1f,1.4f}){settings.UiScale=scale;ApplySettings();ShowInventory();await Frames();
                var slots=Descendants(overlay).OfType<SlotButton>().ToArray();Check(slots.Count(s=>s.SurfaceIndex==0)==36&&slots.Count(s=>s.SurfaceIndex==2)==4&&slots.Count(s=>s.SurfaceIndex==3)==5,"MC slot layout incomplete");var view=overlay.GetViewportRect().Size;
                foreach(var slot in slots){var rect=slot.GetGlobalRect();Check(rect.Position.X>=0&&rect.Position.Y>=0&&rect.End.X<=view.X+.5&&rect.End.Y<=view.Y+.5,"inventory slot outside viewport at scale "+scale+": "+slot.SurfaceIndex+":"+slot.SlotIndex+" "+rect);}
                samples.Add(new{stage,scale,slots=slots.Length,viewport=new[]{view.X,view.Y}});
            }
            stage="click, invalid slots, equipment drag";Click(0,Find("iron_helmet"));Click(3,1);Check(world.State.Cursor?.Id=="iron_helmet"&&world.State.Equipment.Slots[1]==null,"wrong armor slot accepted cursor");Click(3,0);Check(world.State.Cursor==null&&world.State.Equipment.Slots[0]?.Id=="iron_helmet","helmet click equip failed");
            foreach(string id in new[]{"iron_chestplate","iron_leggings","iron_boots"}){int from=Find(id),to=catalog.Items[id].ArmorSlot;using var data=new Godot.Collections.Dictionary{{"surface",0},{"slot",from}};Slot(3,to)._DropData(Vector2.Zero,data);await Frames();Check(world.State.Equipment.Slots[to]?.Id==id,"armor drag failed "+id);}
            using(var invalid=new Godot.Collections.Dictionary()){Check(!Slot(3,0)._CanDropData(Vector2.Zero,invalid),"malformed drag accepted");Slot(3,0)._DropData(Vector2.Zero,invalid);}
            world.State.Inventory.Slots[0]=new("iron_sword",1);TransferSlots(3,0,0,0);await Frames();Check(world.State.Equipment.Slots[0]?.Id=="iron_helmet"&&world.State.Inventory.Slots[0]?.Id=="iron_sword","invalid armor swap lost items");
            world.State.Inventory.Slots[1]=new("shield",1);ShowInventory();Input.ActionPress("inventory_transfer");Click(0,1);Input.ActionRelease("inventory_transfer");Check(world.State.Equipment.Slots[4]?.Id=="shield"&&world.State.Inventory.Slots[1]==null,"shift shield did not equip offhand");
            stage="armor and active directional offhand shield";SetScreen("play");selected=0;player.Camera.LookAt(player.GlobalPosition+new Vector3(0,1,-4));var front=player.GlobalPosition+new Vector3(0,0,-4);double health=world.State.Health;HurtFrom(20,front);Check(Math.Abs(world.State.Health-(health-8))<.001,"iron armor attack reduction");
            int durability=world.State.Equipment.Slots[0]!.Durability!.Value;Input.ActionPress("use");health=world.State.Health;HurtFrom(20,front);Input.ActionRelease("use");Check(Math.Abs(world.State.Health-(health-2.8))<.001&&world.State.Equipment.Slots[0]!.Durability==durability-2,"offhand shield did not reduce frontal held-right damage");
            Input.ActionPress("use");health=world.State.Health;HurtFrom(20,player.GlobalPosition+new Vector3(0,0,4));Input.ActionRelease("use");Check(Math.Abs(world.State.Health-(health-8))<.001,"shield protected the back");
            health=world.State.Health;Hurt(10);Check(world.State.Health==health-10,"environmental damage incorrectly used armor");samples.Add(new{stage,armor=15,attackDamage=8,shieldDamage=2.8,environmentalDamage=10});
            world.State.Equipment.Slots[0]!.Durability=1;HurtFrom(4,front);Check(world.State.Equipment.Slots[0]==null&&Equipment.ArmorPoints(world.State,catalog)==13,"broken helmet still equipped");world.State.Equipment.Slots[0]=new("iron_helmet",1,30);
            stage="actual hostile projectile with armor";world.State.Health=100;int hits=projectiles!.PlayerHits;var origin=player.GlobalPosition+new Vector3(0,1,-2);Check(projectiles.Fire(new(){Position=[origin.X,origin.Y,origin.Z],Velocity=[0,0,10],Damage=20,Remaining=1,Hostile=true}),"armor projectile fixture refused");await Until(()=>projectiles.PlayerHits>hits);Check(Math.Abs(world.State.Health-92)<.001,"actual projectile bypassed armor");
            stage="F hand exchange, offhand food and placement";SwapHands();Check(world.State.Inventory.Slots[0]?.Id=="shield"&&world.State.Equipment.Slots[4]?.Id=="iron_sword","hand exchange failed");SwapHands();Check(world.State.Inventory.Slots[0]?.Id=="iron_sword"&&world.State.Equipment.Slots[4]?.Id=="shield","hand exchange did not reverse");
            world.State.Equipment.Slots[4]=new("berry",3);double hunger=world.State.Hunger;Use();Check(world.State.Equipment.Slots[4]?.Count==2&&world.State.Hunger>hunger,"offhand food did not consume its own stack");
            world.State.Inventory.Slots[9]=new("plank",5);world.State.Equipment.Slots[4]=new("plank",3);player.Camera.LookAt(new Vector3(2.5f,39.5f,.5f));await Frames();var target=player.Target();Check(target.HasValue,"offhand building target missing");Use();Check(world.Get(target!.Value.Before)==catalog.Number("plank")&&world.State.Equipment.Slots[4]?.Count==2&&world.State.Inventory.Slots[9]?.Count==5,"offhand placement charged wrong inventory");
            world.State.Equipment.Slots[4]=new("shield",1);stage="real death bag and recovery";var carried=world.State.Equipment.Copy();SetScreen("play");Hurt(1000);Check(screen=="death"&&world.State.Equipment.Slots.All(s=>s==null),"death retained equipment");var grave=world.State.Graves.Last();Check(grave.Items.Slots.Length==41&&grave.Items.Slots[36]?.Durability==carried.Slots[0]!.Durability&&grave.Items.Count("shield")==1,"death bag omitted equipment/wear");
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="从营地复活").EmitSignal(Button.SignalName.Pressed);player.GlobalPosition=new((float)grave.Position[0],(float)grave.Position[1],(float)grave.Position[2]);Interact();Check(!world.State.Graves.Contains(grave),"recovery left equipment behind");
            ShowInventory();foreach(var item in carried.Slots.Take(4)){Click(0,Find(item!.Id));Click(3,catalog.Items[item.Id].ArmorSlot);}Click(0,Find("shield"));Click(3,4);Check(world.State.Equipment.Slots[0]?.Durability==carried.Slots[0]!.Durability,"bag recovery repaired armor");
            stage="full inventory close preserves cursor and crafting armor wear";SetScreen("play");var inventoryBeforeOverflow=world.State.Inventory.Copy();int gravesBefore=world.State.Graves.Count;
            world.State.Inventory=new();world.State.Inventory.Slots[0]=new("iron_helmet",1,37);world.State.Inventory.Slots[1]=new("iron_boots",1,29);world.State.Inventory.Slots[2]=new("iron_chestplate",1,41);
            ShowInventory();Click(0,0);TransferSlots(0,1,2,0);TransferSlots(0,2,2,3);await Frames();for(int i=0;i<36;i++)world.State.Inventory.Slots[i]=new("stone",64);ShowInventory();
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="关闭").EmitSignal(Button.SignalName.Pressed);
            Check(world.State.Graves.Count==gravesBefore+1,"full inventory close lost overflow");var overflow=world.State.Graves.Last();
            Check(overflow.Items.Slots[4]?.Durability==37&&overflow.Items.Slots[0]?.Durability==29&&overflow.Items.Slots[3]?.Durability==41,"overflow repaired worn cursor or crafting armor");
            Check(world.State.Cursor==null&&world.State.Crafting.Slots.All(s=>s==null)&&world.State.Inventory.Count("stone")==36*64,"overflow duplicated inventory or retained cursor/crafting");
            var overflowBeforeRecovery=overflow.Items.Copy();world.State.Inventory=new();player.GlobalPosition=new((float)overflow.Position[0],(float)overflow.Position[1],(float)overflow.Position[2]);Interact();Check(!world.State.Graves.Contains(overflow)&&world.State.Inventory.Slots.Single(s=>s?.Id=="iron_helmet")?.Durability==37,"overflow recovery repaired armor");world.State.Inventory=inventoryBeforeOverflow;
            world.State.Graves.Add(new(){Position=(double[])overflow.Position.Clone(),Items=overflowBeforeRecovery});samples.Add(new{stage,cursorDurability=37,craftingDurabilities=new[]{29,41},savedOverflow=true});
            stage="equipment save and cold reload fixture";SetScreen("play");var expectedWorld=world.State.Copy();RequestSave();await Until(()=>saveCallbacks.Count==0);var loadedWorld=saves.Load(world.State.WorldId).Snapshot;for(int i=0;i<5;i++)Check(loadedWorld.Equipment.Slots[i]?.Durability==expectedWorld.Equipment.Slots[i]?.Durability&&loadedWorld.Equipment.Slots[i]?.Id==expectedWorld.Equipment.Slots[i]?.Id,"disk equipment changed");
            Check(JsonSerializer.Serialize(loadedWorld.Graves,Catalog.JsonOptions)==JsonSerializer.Serialize(expectedWorld.Graves,Catalog.JsonOptions),"disk overflow changed wear");
            File.WriteAllText(Path.Combine(directory,"reload-world.json"),JsonSerializer.Serialize(expectedWorld,Catalog.JsonOptions));samples.Add(new{stage,worldId=expectedWorld.WorldId,metadataFormat=4,armorPoints=Equipment.ArmorPoints(world.State,catalog)});Record(true,"");GD.Print("EQUIPMENT_PASS: four crafts, actual MC slots/click/shift/drag, armor/wear, held-right directional shield, projectile, offhand use, death bag, full inventory overflow and save");QuitCleanly();
        }catch(Exception error){Input.ActionRelease("use");Input.ActionRelease("inventory_transfer");Record(false,error.Message);GD.PushError("EQUIPMENT_FAILED: "+error);QuitCleanly(9);}
    }
}
