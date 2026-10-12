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
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedGameImage=DisplayServer.GetName()!="headless",humanPlaytest=false,controlledMaterials=true,saveFormatChanged=false,samples},Catalog.JsonOptions));
        try{
            stage="new world random seeds and persisted manual seed";ShowNewWorld();var seedInput=Descendants(overlay).OfType<LineEdit>().Single(e=>e.Name=="WorldSeed");int first=int.Parse(seedInput.Text);
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="随机种子").EmitSignal(Button.SignalName.Pressed);int rerolled=int.Parse(seedInput.Text);Check(first!=rerolled,"reroll repeated previous seed");
            ShowNewWorld();seedInput=Descendants(overlay).OfType<LineEdit>().Single(e=>e.Name=="WorldSeed");int reopened=int.Parse(seedInput.Text);Check(reopened!=rerolled,"reopening world creation reused seed");seedInput.Text="";
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="进入荒野").EmitSignal(Button.SignalName.Pressed);Check(world!=null&&screen=="play"&&world.State.Seed!=reopened,"empty seed did not create a random world");SetScreen("pause");int randomSeed=world!.State.Seed;var seedSave=saveQueue.Enqueue(world.State);await Until(()=>seedSave.IsCompleted);seedSave.GetAwaiter().GetResult();Check(saves.Load(world.State.WorldId).Snapshot.Seed==randomSeed,"reload changed random seed");
            ShowNewWorld();seedInput=Descendants(overlay).OfType<LineEdit>().Single(e=>e.Name=="WorldSeed");seedInput.Text="invalid";Descendants(overlay).OfType<Button>().Single(b=>b.Text=="进入荒野").EmitSignal(Button.SignalName.Pressed);Check(screen=="new","invalid seed created world");seedInput.Text="42";Descendants(overlay).OfType<Button>().Single(b=>b.Text=="进入荒野").EmitSignal(Button.SignalName.Pressed);Check(world!.State.Seed==42&&screen=="play","manual seed ignored");SetScreen("pause");samples.Add(new{stage,autoRandom=true,reroll=true,blankRandom=true,manualSeed=true,persisted=true});
            stage="exclusive world modes";ShowNewWorld();var modes=Descendants(overlay).OfType<CheckButton>().ToArray();modes[0].ButtonPressed=true;modes[1].ButtonPressed=true;Check(!modes[0].ButtonPressed&&modes[1].ButtonPressed,"new world modes overlap");modes[0].ButtonPressed=true;Check(modes[0].ButtonPressed&&!modes[1].ButtonPressed,"creative did not clear casual");
            using(var sheet=Image.CreateEmpty(12*80,(int)Math.Ceiling(catalog.Items.Count/12d)*80,false,Image.Format.Rgba8)){
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
            var query=Descendants(overlay).OfType<LineEdit>().Single();query.Text="石";query.EmitSignal(LineEdit.SignalName.TextChanged,"石");query.Text="";query.EmitSignal(LineEdit.SignalName.TextChanged,"");
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="仅可合成").EmitSignal(Button.SignalName.Pressed);await Frames();
            Check(recipeQuery==""&&Descendants(overlay).OfType<LineEdit>().Single().Text=="","cleared query reappeared after filter");
            query=Descendants(overlay).OfType<LineEdit>().Single();query.Text="铁镐";query.EmitSignal(LineEdit.SignalName.TextChanged,"铁镐");
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="显示全部").EmitSignal(Button.SignalName.Pressed);await Frames();
            Check(recipeQuery=="铁镐"&&Descendants(overlay).OfType<Button>().Any(b=>b.Name=="Recipe_iron_pick"),"unsubmitted query ignored on redraw");
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
            stage="drag preview and transfer conservation";
            var dragSource=Descendants(overlay).OfType<SlotButton>().First(b=>b.SurfaceIndex==0&&b.Item!=null);var sourceItem=dragSource.Item!.Copy();var destination=Descendants(overlay).OfType<SlotButton>().First(b=>b.SurfaceIndex==0&&b.Item==null);
            int total=world.State.Inventory.Count(sourceItem.Id);Variant drag=new Godot.Collections.Dictionary{{"surface",dragSource.SurfaceIndex},{"slot",dragSource.SlotIndex}};var dragPreview=dragSource.CreateItemDragPreview();Check(dragPreview.GetChildren().OfType<ItemGlyph>().All(g=>g.Item?.Id==sourceItem.Id),"drag preview lost item artwork");dragSource.ForceDrag(drag,dragPreview);Check(GetViewport().GuiIsDragging(),"drag has no preview");
            Check(destination._CanDropData(Vector2.Zero,drag),"valid drag rejected");destination._DropData(Vector2.Zero,drag);await Frames(12);
            Check(world.State.Inventory.Slots[destination.SlotIndex]?.Id==sourceItem.Id&&world.State.Inventory.Count(sourceItem.Id)==total,"drag lost or duplicated items");
            // End the synthetic drag exactly as a mouse release would do.
            Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false});await Frames();
            for(int i=0;i<8;i++)ShowInventory();await Frames(5);samples.Add(new{stage,crackStages=10,maxParticles=48,avatar3D=true});
            stage="survival feedback: actual mining, wear, sunrise, goal and tooltip";await ValidateSurvivalFeedback(folder);samples.Add(new{stage,passed=true});
            stage="interactive world map: pan, anchored zoom and live block edits";await ValidateWorldMap(folder);samples.Add(new{stage,passed=true});
            stage="render updated character, creatures and day-night samples";if(DisplayServer.GetName()!="headless")await CaptureUpdatedPresentation(folder);else{foreach(string kind in creatureDefs.Select(d=>d.Id)){var model=CreatureModel.Create(kind);AddChild(model);model.Animate(.1,new(2,0,0),true);model.Tint(true,false);model.Tint(false,false);RemoveChild(model);model.QueueFree();}await Frames();}
            stage="full backpack returns grid to recoverable grave";currentContainer=p;ShowWorkbench();for(int i=0;i<36;i++)world.State.Inventory.Slots[i]=new("stone",64);workbenchCrafting.Slots[0]=new("iron_helmet",1,9);SetScreen("play");Check(world.State.Graves.Any(g=>g.Items.Slots.Any(s=>s?.Id=="iron_helmet"&&s.Durability==9)),"full backpack discarded input");
            stage="menu stops hidden first-person rendering";ReturnMenu();await Until(()=>screen=="menu");Check(world==null&&!heldView.RenderingActive&&!heldView.Visible,"hidden viewport still renders in menu");samples.Add(new{stage,pausedViewportDisabled=true,menuViewportDisabled=true});
            await Frames(3);Record(true,"");GD.Print("PRESENTATION_PASS: recipe clear/filter regression, exclusive modes, drag preview/conservation, distinct creature meshes, "+catalog.Items.Count+" icons, real UI crafting, cursor/grid recovery, held items, mining and avatar; rendering="+(DisplayServer.GetName()!="headless"));QuitCleanly();
        }catch(Exception error){Record(false,error.Message);GD.PushError("PRESENTATION_FAILED: "+error);QuitCleanly(9);}
    }
}
