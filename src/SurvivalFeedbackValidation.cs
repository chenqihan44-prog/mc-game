using System;
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
    private async Task ValidateSurvivalFeedback(string folder)
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        SetScreen("play");var savedInventory=world!.State.Inventory.Copy();bool creative=world.State.Creative;int oldSelected=selected;selected=0;world.State.Creative=false;
        var target=EmberLighthouse.Player.Explorer.ToCell(player!.GlobalPosition)+new Cell(3,1,0);
        void Prepare(Stack? item){world.State.Graves.Clear();world.State.Inventory=new();world.State.Inventory.Slots[0]=item;world.Set(target,catalog.Number("stone"));attackCooldown=0;miningCell=null;mineProgress=0;}
        void Dig(double seconds)=>Mine(seconds,(target,target+new Cell(0,0,-1),3));
        Prepare(null);Dig(1);Check(world.Get(target)!=0&&mineProgress<.2,"hands mine stone as quickly as a pick");Dig(7.1);Check(world.Get(target)!=0&&world.State.Inventory.Count("stone")==0,"hands harvested stone");
        Prepare(new("wood_pick",1));Dig(.6);Check(world.Get(target)!=0&&mineProgress>.4&&mineProgress<.6,"wood pick has no partial mining");Dig(.61);Check(world.Get(target)==0&&world.State.Inventory.Count("stone")==0&&world.State.Graves.Sum(g=>g.Items.Count("stone"))==1&&world.State.Inventory.Slots[0]!.Durability==59,"wood pick did not harvest/wear exactly once");
        Prepare(new("wood_pick",1,1));Dig(1.21);Check(world.Get(target)==0&&world.State.Inventory.Slots[0]==null&&world.State.Inventory.Count("stone")==0&&world.State.Graves.Sum(g=>g.Items.Count("stone"))==1,"last use did not yield stone and break tool");
        Prepare(new("iron_axe",1,7));Dig(8.1);Check(world.Get(target)!=0&&world.State.Inventory.Slots[0]!.Durability==7,"wrong tool harvested rock or wore on failed harvest");
        Prepare(new("stone_pick",1,7));for(int i=1;i<36;i++)world.State.Inventory.Slots[i]=new("berry",64);Dig(.61);Check(world.Get(target)==0&&world.State.Inventory.Slots[0]!.Durability==6&&world.State.Graves.Sum(g=>g.Items.Count("stone"))==1,"full inventory lost ground drop or tool wear");
        Prepare(new("wood_pick",1,7));world.State.Creative=true;Dig(.01);Check(world.Get(target)==0&&world.State.Inventory.Slots[0]!.Durability==7,"creative tool wore");world.State.Creative=false;
        Prepare(new("wood_pick",1));Dig(.6);world.State.Inventory.Slots[0]=new("iron_pick",1);Dig(.1);Check(world.Get(target)!=0&&mineProgress<.3,"switching tool inherited progress");
        world.Set(target,0);world.State.Inventory=new();world.State.Inventory.Add("plank",3,catalog);world.State.Inventory.Add("stick",2,catalog);currentContainer=null;currentStation="hand";string oldQuery=recipeQuery;recipeQuery="木镐";ShowRecipes();
        Button? MakeButton(Node root){foreach(var child in root.GetChildren()){if(child is Button button&&button.Text=="制作")return button;var nested=MakeButton(child);if(nested!=null)return nested;}return null;}
        var make=MakeButton(overlay)!;Check(!make.Disabled,"wood pick unavailable before workbench");make.EmitSignal(Button.SignalName.Pressed);Check(world.State.Inventory.Count("wood_pick")==1&&world.State.Inventory.Count("plank")==0&&world.State.Inventory.Count("stick")==0,"real hand recipe failed");recipeQuery=oldQuery;
        world.State.Inventory=savedInventory;world.State.Creative=creative;selected=oldSelected;miningCell=null;mineProgress=0;attackCooldown=0;
        world.State.DayTime=1199.8;ShowInventory();double paused=world.State.SkyTime;_Process(30);Check(world.State.SkyTime==paused,"inventory advanced day/night");SetScreen("play");_Process(.2);Check(!DayCycle.Night(world.State.SkyTime),"actual process never returned from night to day");
        world.State.DayTime=600;world.Advance(300);Check(!DayCycle.Night(world.State.SkyTime),"complete night failed to end");
        world.State.DayTime=910;var save=saveQueue.Enqueue(world.State);while(!save.IsCompleted)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);save.GetAwaiter().GetResult();
        var restored=saves.Load(world.State.WorldId).Snapshot;Check(restored.DayTime.HasValue&&restored.SkyTime>=910&&restored.SkyTime<912,"saved world reset sky time");
        world.State.TutorialHidden=false;tutorial=0;UpdateHud();for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        var center=GetViewport().GetVisibleRect().Size/2;var objectiveRect=objective.GetParent<Control>().GetGlobalRect();Check(!objectiveRect.HasPoint(center)&&objectiveRect.Position.X>center.X,"objective covers screen center");
        ShowInventory();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        SlotButton? FindSlot(Node root){foreach(var child in root.GetChildren()){if(child is SlotButton slot&&slot.Item!=null)return slot;var nested=FindSlot(child);if(nested!=null)return nested;}return null;}
        var button=FindSlot(overlay)!;var tooltip=button._MakeCustomTooltip(button.TooltipText);var label=tooltip.GetChild<Label>(0);Check(label.GetThemeColor("font_color")==Colors.White&&label.GetThemeFontSize("font_size")>=19,"tooltip lacks white readable text");tooltip.Free();
        File.WriteAllText(Path.Combine(folder,"survival-feedback.json"),JsonSerializer.Serialize(new{passed=true,actualMining=true,realWoodPickRecipeButton=true,woodPickSeconds=1.2,stonePickSeconds=.6,handsNoRockDrop=true,toolWearOnce=true,lastUseBreaks=true,failedMiningNoWear=true,creativeNoWear=true,toolSwitchResetsProgress=true,dayMinutes=10,nightMinutes=5,inventoryPausesCycle=true,processReturnsToDay=true,solarTimeSaved=true,objectiveAwayFromCenter=true,tooltipFont=19},Catalog.JsonOptions));
    }
}

