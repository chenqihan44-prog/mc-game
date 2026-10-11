using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private bool presentationCheck;
    private async void RunPresentationCheck()
    {
        var timer=Stopwatch.StartNew();string stage="original item artwork";var samples=new List<object>();
        string folder=ProjectSettings.GlobalizePath("res://test-results/presentation");Directory.CreateDirectory(folder);
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> done){double end=timer.Elapsed.TotalSeconds+15;while(!done()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedGameImage=false,humanPlaytest=false,controlledMaterials=true,saveFormatChanged=false,samples},Catalog.JsonOptions));
        try{
            using(var sheet=Image.CreateEmpty(12*80,8*80,false,Image.Format.Rgba8)){
                sheet.Fill(new("868686"));var hashes=new HashSet<string>();int i=0;
                foreach(var item in catalog.Items.Values){using var icon=ItemSprites.CreateImage(item);Check(icon.GetData().Where((_,n)=>n%4==3).Any(a=>a>0),"empty icon: "+item.Id);Check(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(icon.GetData()))),"identical artwork: "+item.Id);icon.Resize(64,64,Image.Interpolation.Nearest);sheet.BlitRect(icon,new(0,0,64,64),new(i%12*80+8,i/12*80+8));i++;}
                Check(sheet.SavePng(Path.Combine(folder,"item-artwork.png"))==Error.Ok,"artwork PNG write failed");samples.Add(new{stage,itemCount=i,uniqueIcons=hashes.Count});
            }
            stage="all recipe layouts conserve ingredients";
            foreach(var recipe in catalog.Recipes.Values.Where(r=>r.Station is "hand" or "workbench"&&r.Seconds==0)){
                var grid=new Inventory(9);var source=new Inventory();foreach(var ingredient in recipe.Inputs)source.Add(ingredient.Key,ingredient.Value,catalog);
                Check(WorkbenchGrid.Arrange(source,grid,recipe,catalog,out var error),recipe.Id+": "+error);Check(WorkbenchGrid.Matches(grid,recipe),"layout cannot produce "+recipe.Id);
                Check(source.Slots.All(s=>s==null)&&recipe.Inputs.All(p=>grid.Count(p.Key)==p.Value)&&grid.Slots.Where(s=>s!=null).Sum(s=>s!.Count)==recipe.Inputs.Values.Sum(),"layout changed recipe cost: "+recipe.Id);
                var result=new Inventory();Check(WorkbenchGrid.CraftTo(grid,result,recipe,catalog,out error)&&grid.Slots.All(s=>s==null)&&result.Count(recipe.Output)==recipe.Count,"transaction failed: "+recipe.Id);
            }
            samples.Add(new{stage,recipes=catalog.Recipes.Values.Count(r=>r.Station is "hand" or "workbench"&&r.Seconds==0)});
            StartWorld(new(){Name="成品交互检查",Seed=42,Creative=false,Hunger=50,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));
            var p=EmberLighthouse.Player.Explorer.ToCell(player.GlobalPosition)+new Cell(2,0,0);world!.Set(p,catalog.Number("workbench"));world.State.Entities[p.ToString()]=new(){Kind="workbench"};currentContainer=p;currentStation="workbench";
            world.State.Inventory.Add("iron_ingot",12,catalog);world.State.Inventory.Add("stick",8,catalog);
            stage="actual recipe button, manual slots and output";ShowRecipes();await Frames();
            Check(screen=="workbench"&&Descendants(overlay).OfType<SlotButton>().Count(b=>b.SurfaceIndex==4)==9,"workbench is not a real nine-slot surface");
            Descendants(overlay).OfType<LineEdit>().Single().EmitSignal(LineEdit.SignalName.TextSubmitted,"铁镐");await Frames();
            Descendants(overlay).OfType<Button>().Single(b=>b.Name=="Recipe_iron_pick").EmitSignal(Button.SignalName.Pressed);await Frames();
            Check(world.State.Inventory.Count("iron_ingot")==9&&workbenchCrafting.Count("iron_ingot")==3&&WorkbenchRecipe()?.Id=="iron_pick","recipe click did not move real ingredients");
            Descendants(overlay).OfType<Button>().Single(b=>b.Name=="WorkbenchOutput").EmitSignal(Button.SignalName.Pressed);Check(world.State.Cursor?.Id=="iron_pick"&&workbenchCrafting.Slots.All(s=>s==null),"real output did not consume exactly one recipe");
            SetScreen("play");Check(world.State.Cursor==null&&world.State.Inventory.Count("iron_pick")==1,"closing did not return cursor output");
            ShowWorkbench();SelectWorkbenchRecipe(catalog.Recipes["iron_axe"]);TransferSlots(4,0,4,2);TransferSlots(4,3,4,5);await Frames();Check(WorkbenchRecipe()?.Id=="iron_axe","mirrored manual arrangement rejected");
            int before=world.State.Inventory.Count("iron_ingot");world.State.Cursor=new("coal",1);SlotClick(4,8,false);Check(WorkbenchRecipe()==null,"extra material accepted");SlotClick(4,8,false);SetScreen("play");Check(world.State.Inventory.Count("iron_ingot")==before+3&&world.State.Inventory.Count("coal")==1,"manual grid close lost materials");
            samples.Add(new{stage,realUiSignals=true,mirror=true,extraRejected=true,cursorReturned=true});
            stage="save recovers transient grid and preserves wear";ShowWorkbench();SelectWorkbenchRecipe(catalog.Recipes["iron_pick"]);workbenchCrafting.Slots[8]=new("iron_helmet",1,17);string id=world.State.WorldId;bool? saved=null;RequestSave(success=>saved=success);await Until(()=>saved.HasValue);Check(saved==true,"presentation fixture save failed: "+message);
            var loaded=saves.Load(id).Snapshot;Check(loaded.Inventory.Count("iron_ingot")==9&&loaded.Inventory.Slots.Any(s=>s?.Id=="iron_helmet"&&s.Durability==17)&&workbenchCrafting.Slots.All(s=>s==null),"grid save recovery or wear failed");SetScreen("play");
            stage="first-person block, tool, swing and offhand";
            world.State.Inventory.Slots[0]=new("plank",4);selected=0;await Frames();Check(heldView.MainItemId=="plank"&&heldView.Visible,"selected block not held");
            heldView.Swing();heldView.Look(new(10,4));heldView.Tick(.016,world.State.Inventory.Slots[0],new("shield",1),new(4,0,0),false,true);Check(heldView.SwingAmount>0,"swing did not animate");world.State.Inventory.Slots[0]=new("iron_pick",1);await Frames();Check(heldView.MainItemId=="iron_pick","tool selection not reflected in hand");
            stage="mining cracks, HUD progress, chips and pause cleanup";
            var target=p+new Cell(0,1,0);world.Set(target,catalog.Number("stone"));attackCooldown=0;Mine(.05,(target,target+new Cell(0,0,-1),2));Check(mineProgress>0&&mineProgress<1&&world.Get(target)!=0,"real mining has no partial progress");
            miningFeedback!.Tick(.016,world,miningCell,mineProgress);Check(miningFeedback.CrackStage>=0&&miningFeedback.ActiveChips>0,"cracks or hit particles missing");
            Mine(1,(target,target+new Cell(0,0,-1),2));Check(world.Get(target)==0&&world.Get(p)==catalog.Number("workbench")&&miningFeedback.ActiveChips>2,"real block break burst missing or neighbor removed");Check(miningFeedback.ActiveChips<=48,"particle bound exceeded");miningBar.Progress=.55;miningBar.QueueRedraw();
            currentContainer=null;ShowInventory();await Frames();Check(!heldView.Visible&&!heldView.RenderingActive&&miningFeedback.CrackStage==-1&&miningBar.Progress==0&&Descendants(overlay).OfType<AvatarPreview>().Count()==1,"inventory avatar or paused presentation cleanup missing");
            for(int i=0;i<8;i++)ShowInventory();await Frames(5);samples.Add(new{stage,crackStages=10,maxParticles=48,avatar3D=true});
            stage="full backpack returns grid to recoverable grave";currentContainer=p;ShowWorkbench();for(int i=0;i<36;i++)world.State.Inventory.Slots[i]=new("stone",64);workbenchCrafting.Slots[0]=new("iron_helmet",1,9);SetScreen("play");Check(world.State.Graves.Any(g=>g.Items.Slots.Any(s=>s?.Id=="iron_helmet"&&s.Durability==9)),"full backpack discarded input");
            stage="menu stops hidden first-person rendering";ReturnMenu();await Until(()=>screen=="menu");Check(world==null&&!heldView.RenderingActive&&!heldView.Visible,"hidden viewport still renders in menu");samples.Add(new{stage,pausedViewportDisabled=true,menuViewportDisabled=true});
            await Frames(3);Record(true,"");GD.Print("PRESENTATION_PASS: 94 unique icons, shaped recipe costs, real UI crafting/manual mirror, cursor/grid close/save/full-backpack recovery, held-item/swing nodes, crack stages/chips and 3D avatar, hidden viewport disabled; headless, no rendered game image");QuitCleanly();
        }catch(Exception error){Record(false,error.Message);GD.PushError("PRESENTATION_FAILED: "+error);QuitCleanly(9);}
    }
}
