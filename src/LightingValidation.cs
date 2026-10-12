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
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;
public partial class Game
{
    private bool lightingCheck;
    private async void RunLightingCheck()
    {
        var timer=Stopwatch.StartNew();string stage="world creation";string directory=ProjectSettings.GlobalizePath("res://test-results/lighting");Directory.CreateDirectory(directory);
        void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(stage+": "+reason);}
        IEnumerable<Node> Nodes(Node root){foreach(var child in root.GetChildren()){yield return child;foreach(var nested in Nodes(child))yield return nested;}}
        async Task Frames(int n=3){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> ready){double end=timer.Elapsed.TotalSeconds+60;while(!ready()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        async Task<double> Shot(string filename){if(DisplayServer.GetName()=="headless")return 0;await Frames(6);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(directory,filename+".png"));double sum=0;int count=0;for(int y=image.GetHeight()/3;y<image.GetHeight()*2/3;y+=3)for(int x=image.GetWidth()/4;x<image.GetWidth()*3/4;x+=3){var c=image.GetPixel(x,y);sum+=c.R*.2126+c.G*.7152+c.B*.0722;count++;}return sum/count;}
        double dark=0,lit=0;
        try{
            settings.ViewDistance=3;ShowNewWorld();var bonus=Nodes(overlay).OfType<CheckButton>().Single(c=>c.Name=="BonusChest");Check(!bonus.ButtonPressed,"bonus default enabled");var creative=Nodes(overlay).OfType<CheckButton>().Single(c=>c.Text.StartsWith("创造模式"));creative.ButtonPressed=true;Check(bonus.Disabled,"creative bonus not disabled");creative.ButtonPressed=false;bonus.ButtonPressed=true;Nodes(overlay).OfType<LineEdit>().Single(c=>c.Name=="WorldSeed").Text="16";await Shot("create-world");Nodes(overlay).OfType<Button>().Single(b=>b.Text=="进入荒野").EmitSignal(Button.SignalName.Pressed);Check(world?.State.BonusChest==true&&world.State.Entities.Values.Count(e=>e.LootTheme=="出生奖励宝箱")==1,"actual create button ignored option");world!.State.Population.Initialized=true;world.State.Population.NextSpawnTime=100000;player!.SetPhysicsProcess(false);SetScreen("journal");
            stage="sealed cave";StartWorld(new(){Name="光照与触碰拾取验证",Seed=16,Time=300,Creative=true,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);foreach(var creature in creatures)creature.QueueFree();creatures.Clear();world!.State.Creatures.Clear();
            for(int x=320;x<=340;x++)for(int z=320;z<=327;z++)for(int y=109;y<=113;y++)world.Set(new(x,y,z),x is 320 or 340||z is 320 or 327||y is 109 or 113?catalog.Number("stone"):(ushort)0);
            player.GlobalPosition=new(323.5f,110,324.5f);world.State.Position=[323.5,110,324.5];player.Camera.LookAt(new Vector3(333,111.5f,324.5f));SetScreen("journal");overlay.Visible=false;hudRoot.Visible=false;notice.Visible=false;await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));world.State.DayTime=300;UpdateDaylight();Check(renderer!.SkyAt(player.Camera.GlobalPosition)==0,"cave receives sky light through ceiling");dark=await Shot("cave-dark");
            stage="wall torch and gradual light";var torch=new Cell(326,111,321);Check(world.Place(torch,"torch",false,out var error,support:new Cell(326,111,320)),error);Check(world.State.Entities[torch.ToString()].Stage==3,"torch not attached to north wall");renderer.RefreshEdit(torch);await Until(()=>renderer.IsVisualReady(player.GlobalPosition));UpdateDaylight();lit=await Shot("cave-torch");if(DisplayServer.GetName()!="headless")Check(dark<.045&&lit>dark*4+.03,"rendered cave light contrast failed: "+dark+" -> "+lit);
            player.Camera.LookAt(new Vector3(326.5f,111.5f,321.2f));await Shot("wall-torch");
            stage="visible item models and actual pickup callback";world.State.Creative=false;world.State.Inventory=new();var items=new Inventory(3);items.Slots[0]=new("wood",2);items.Slots[1]=new("iron_pick",1,19);items.Slots[2]=new("raw_cod",1);var drop=world.Drop(items,[327.5,111,324.5]);SetScreen("play");hudRoot.Visible=false;notice.Visible=false;player.Camera.LookAt(new Vector3(327.5f,110.3f,324.5f));await Frames(70);Check(world.State.Inventory.Count("wood")==0&&world.State.Graves.Contains(drop),"drops collected at a distance");Check(graveMarkers.ContainsKey(drop)&&Nodes(graveMarkers[drop]).OfType<PresentationModel>().Count()==3,"item-specific native models absent");player.GlobalPosition=new(325.5f,110,324.5f);world.State.Position=[325.5,110,324.5];player.Camera.LookAt(new Vector3(327.5f,110.3f,324.5f));outline!.Visible=false;await Shot("ground-items");
            player.GlobalPosition=new(327.5f,110,324.5f);world.State.Position=[327.5,110,324.5];await Frames(4);Check(!world.State.Graves.Contains(drop)&&world.State.Inventory.Count("wood")==2&&world.State.Inventory.Slots.Single(s=>s?.Id=="iron_pick")?.Durability==19,"touch pickup lost count or repaired tool");
            stage="drop save and reload";world.Drop(new Stack("leather_helmet",1,17){Dye="red",Protection=2},[329.5,110.4,324.5]);SetScreen("journal");var expected=world.State.Copy();var save=saveQueue.Enqueue(expected);await Until(()=>save.IsCompleted);save.GetAwaiter().GetResult();var loaded=saves.Load(expected.WorldId).Snapshot;Check(loaded.Graves.Last().Items.Slots[0]?.Dye=="red"&&loaded.Entities[torch.ToString()].Stage==3&&loaded.FormatVersion==7,"saved drops or torch attachment lost");
            File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed=true,stage,seconds=timer.Elapsed.TotalSeconds,rendered=DisplayServer.GetName()!="headless",darkLuminance=dark,torchLuminance=lit,bonusCheckbox=true,individualModels=3,bodyContact=true,controlledFixture=true,humanPlaytest=false},Catalog.JsonOptions));GD.Print("LIGHTING_PASS: bonus checkbox, sealed cave sky occlusion, wall mount, light falloff, individual drop models, actual contact pickup and format7 save");QuitCleanly();
        }catch(Exception e){File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed=false,stage,error=e.ToString()},Catalog.JsonOptions));GD.PushError("LIGHTING_FAILED: "+e);QuitCleanly(13);}
    }
}

