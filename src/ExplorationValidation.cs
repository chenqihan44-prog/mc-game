using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Persistence;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private bool explorationCheck,explorationPreview;
    private async void RunExplorationCheck()
    {
        var watch=Stopwatch.StartNew();string stage="setup";var milestones=new List<string>();string folder=ProjectSettings.GlobalizePath("res://test-results/exploration");Directory.CreateDirectory(folder);
        static void Check(bool result,string error){if(!result)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Children(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var n in Children(child))yield return n;}}
        void Click(string text)=>Children(overlay).OfType<Button>().Single(b=>b.Text==text).EmitSignal(Button.SignalName.Pressed);
        async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Frames(int n=4){for(int i=0;i<n;i++)await Frame();}
        async Task Until(Func<bool> done){double end=watch.Elapsed.TotalSeconds+45;while(!done()){if(watch.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frame();}}
        void Mark(){milestones.Add(stage);GD.Print("EXPLORATION_STAGE: "+stage);}
        string previewFolder=ProjectSettings.GlobalizePath("res://docs/previews");
        async Task Capture(string name){await Frames(6);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(previewFolder,name+".png"));}
        try{
            settings.ViewDistance=3;StartWorld(new(){Name="探索更新验证",Seed=42,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);SetScreen("journal");await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));
            stage="journal native selection, drag, zoom, branches and wrapped layout";world!.State.Mainline.Proofs.Add("mine");world.State.Beacons.Add(0);
            foreach(float scale in new[]{.8f,1f,1.4f}){
                settings.UiScale=scale;ApplySettings();ShowMainlineJournal();await Frames();var graph=journalGraph!;Check(graph.Size.X>230&&graph.Size.Y>230&&graph.GetGlobalRect().End.X<=GetViewport().GetVisibleRect().Size.X+1,"graph layout exceeds viewport");
                graph.SelectNode("forge",true);await Frames();Check(Children(overlay).OfType<Label>().Any(l=>l.Text.Contains("炉心棱镜")),"node selection missed rewards");Check(!Children(overlay).OfType<ScrollContainer>().Any(s=>s.HorizontalScrollMode!=ScrollContainer.ScrollMode.Disabled),"detail pane horizontally scrolls");
                var node=graph.Nodes.Single(n=>n.Id=="snow");graph.SetView(new(850,290),.65f);var hit=graph.NodeRect(node).GetCenter();graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=hit});Check(graph.SelectedId=="snow","native node click failed");
                var anchor=new Vector2(50,50);var before=graph.WorldPoint(anchor);float zoom=graph.Zoom;graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.WheelUp,Pressed=true,Position=anchor});Check(graph.Zoom>zoom&&graph.WorldPoint(anchor).DistanceTo(before)<.01f,"zoom drift at cursor");
                var center=graph.Center;graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Middle,Pressed=true,Position=anchor});graph._GuiInput(new InputEventMouseMotion{Position=anchor+new Vector2(50,20),Relative=new(50,20)});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Middle,Pressed=false,Position=anchor});Check(graph.Center.DistanceTo(center)>10,"drag did not pan");
                graph.SelectNode("snow",true);Click("追踪此目标");UpdateHud();Check(screen=="play"&&objective.Text.Contains("雪岭观测所"),"tracking not shown in HUD");ShowMainlineJournal();await Frames();Check(journalGraph!.SelectedId=="snow","selection lost on reopen");Click("全图");Check(journalGraph.Zoom<=.5f,"fit did not zoom out");Click("当前目标");Check(journalGraph.SelectedId=="forge","current objective did not focus");
            }
            settings.UiScale=1;ApplySettings();ShowMainlineJournal();await Frames();journalGraph!.SetView(new(850,260),.9f);journalGraph.SelectNode("forge");if(explorationPreview){Directory.CreateDirectory(previewFolder);await Capture("exploration-journal");}Mark();
            stage="natural and player chest interaction, emptied save and reload";var chestCell=new Cell(34,38,26);world.Set(chestCell,catalog.Number("chest"));SetScreen("play");player!.GlobalPosition=new(chestCell.X+.5f,chestCell.Y,chestCell.Z-2);player.Camera.LookAt(new Vector3(chestCell.X+.5f,chestCell.Y+.5f,chestCell.Z+.5f));Interact();Check(screen=="inventory"&&world.State.Entities[chestCell.ToString()].Storage.Count("map")==1,"real chest interaction missed starter loot: screen="+screen+" target="+player.Target()?.Hit+" camera="+player.Camera.GlobalPosition+" map="+(world.State.Entities.GetValueOrDefault(chestCell.ToString())?.Storage.Count("map")??-1));var chest=world.State.Entities[chestCell.ToString()];chest.Storage=new();SetScreen("journal");RequestSave();await Until(()=>saveCallbacks.Count==0);var loaded=saves.Load(world.State.WorldId).Snapshot;StartWorld(loaded);player!.SetPhysicsProcess(false);SetScreen("play");player!.GlobalPosition=new(chestCell.X+.5f,chestCell.Y,chestCell.Z-2);player.Camera.LookAt(new Vector3(chestCell.X+.5f,chestCell.Y+.5f,chestCell.Z+.5f));Interact();Check(world!.State.Entities[chestCell.ToString()].Storage.Slots.All(s=>s==null),"reload refilled emptied chest");SetScreen("journal");await Until(()=>saveCallbacks.Count==0);Mark();
            stage="old world confirmation cancel, delete and restore using native UI";var old=new WorldSnapshot{Name="可删除的旧世界",GeneratorVersion=2};old.Inventory.Add("iron_ingot",7,catalog);saves.Save(old);var entry=saves.ListDetails().Single(e=>e.Id==old.WorldId);ShowDeleteWorld(entry);Click("取消");await Until(()=>worldIo==null);Check(saves.ListDetails().Any(e=>e.Id==old.WorldId),"cancel deleted save");ShowDeleteWorld(entry);Click("确认删除");await Until(()=>worldIo==null&&screen=="worlds");Check(!saves.ListDetails().Any(e=>e.Id==old.WorldId),"deleted world remains listed");ShowDeletedWorlds();await Until(()=>worldIo==null&&screen=="deleted-worlds");var restore=Children(overlay).OfType<Button>().First(b=>b.Text=="恢复世界");restore.EmitSignal(Button.SignalName.Pressed);await Until(()=>worldIo==null&&screen=="worlds");Check(saves.Load(old.WorldId).Snapshot.Inventory.Count("iron_ingot")==7&&saves.Load(old.WorldId).Snapshot.GeneratorVersion==2,"restore changed old save");StartWorld(saves.Load(old.WorldId).Snapshot);player!.SetPhysicsProcess(false);ShowMainlineJournal();await Frames();Check(journalGraph!.Nodes.Count==7&&!journalGraph.Nodes.Any(n=>n.Id=="colossus"),"legacy journal route changed");journalGraph.SelectNode("guardian",true);Click("追踪此目标");UpdateHud();Check(objective.Text.Contains("遗迹守护者"),"legacy target tracking absent");SetScreen("journal");Mark();
            if(explorationPreview){
                if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("预览需要渲染窗口");
                stage="render six seeded starting landscapes";var seedByProfile=new Dictionary<int,int>();for(int seed=0;seed<100;seed++)seedByProfile.TryAdd(Landscape.StartProfile(seed),seed);
                var canvas=new CanvasLayer{Layer=10};AddChild(canvas);var label=UiFactory.Label("",26);label.Position=new(25,25);canvas.AddChild(label);using var sheet=Image.CreateEmpty(1920,720,false,Image.Format.Rgba8);
                foreach(var (profile,seed) in seedByProfile.OrderBy(p=>p.Key)){
                    settings.ViewDistance=4;StartWorld(new(){Name="地貌预览",Seed=seed,Time=480,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);SetScreen("journal");overlay.Visible=false;hudRoot.Visible=false;notice.Visible=false;
                    int h=world!.Terrain.Surface(30,30);player.GlobalPosition=new(30,h+30,30);player.Camera.GlobalPosition=new(8,h+30,-8);player.Camera.LookAt(new Vector3(52,world.Terrain.Surface(52,52)+3,52));UpdateDaylight();label.Text=Landscape.Names[profile]+" · 种子 "+seed;
                    await Until(()=>renderer!.Pending==0&&renderer.Loaded>=290);await Frames(8);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var shot=GetViewport().GetTexture().GetImage();shot.SavePng(Path.Combine(previewFolder,"landscape-"+profile+".png"));shot.Resize(640,360,Image.Interpolation.Lanczos);sheet.BlitRect(shot,new(0,0,640,360),new(profile%3*640,profile/3*360));
                }
                sheet.SavePng(Path.Combine(previewFolder,"exploration-landscapes.png"));canvas.QueueFree();Mark();
            }
            File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=true,stage,milestones,seconds=watch.Elapsed.TotalSeconds,rendered=explorationPreview,controlledSetup=true},Catalog.JsonOptions));GD.Print("EXPLORATION_PASS: old world delete/restore, deterministic persisted chest loot, interactive branch journal and seeded terrain");QuitCleanly();
        }catch(Exception error){File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=false,stage,error=error.ToString(),milestones},Catalog.JsonOptions));GD.PushError("EXPLORATION_FAILED: "+stage+" / "+error);QuitCleanly(9);}
    }
}

