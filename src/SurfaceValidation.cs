using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;
public partial class Game
{
    private bool surfaceCheck,surfacePreview;
    private async void RunSurfaceCheck()
    {
        string stage="setup";var milestones=new List<string>();var timer=Stopwatch.StartNew();string folder=ProjectSettings.GlobalizePath("res://test-results/surface");Directory.CreateDirectory(folder);
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static IEnumerable<Node> Children(Node root){foreach(var n in root.GetChildren()){yield return n;foreach(var child in Children(n))yield return child;}}
        void Click(string text)=>Children(overlay).OfType<Button>().Single(b=>b.Text==text).EmitSignal(Button.SignalName.Pressed);
        async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        async Task Frames(int n=4){for(int i=0;i<n;i++)await Frame();}
        async Task Until(Func<bool> done){double end=timer.Elapsed.TotalSeconds+35;while(!done()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frame();}}
        void Mark(){milestones.Add(stage);GD.Print("SURFACE_STAGE: "+stage);}
        try{
            settings.ViewDistance=2;StartWorld(new(){Name="自然地表交互验证",Seed=16,Time=480,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);SetScreen("journal");await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));
            stage="permanent active-world deletion cancels safely and removes owned backups";var a=new WorldSnapshot{Name="永久删除验证"};var b=new WorldSnapshot{Name="保留验证"};saves.Save(a);saves.Save(b);string copy=saves.Backup(a.WorldId),neighborCopy=saves.Backup(b.WorldId);var entry=saves.ListDetails().Single(w=>w.Id==a.WorldId);
            ShowDeleteWorld(entry);Click("取消");await Until(()=>worldIo==null&&screen=="worlds");Check(Directory.Exists(copy)&&saves.ListDetails().Any(w=>w.Id==a.WorldId),"cancel purged data");ShowDeleteWorld(entry);Click("永久删除世界");await Until(()=>worldIo==null&&screen=="worlds");Check(!Directory.Exists(copy)&&!saves.ListDetails().Any(w=>w.Id==a.WorldId)&&Directory.Exists(neighborCopy),"permanent world deletion left backup or damaged neighbor");Mark();
            stage="backup delete UI and recycled-world permanent deletion";var backup=saves.ListBackups().Single(e=>e.Key==Path.GetFileName(neighborCopy));ShowDeleteBackup(backup);Click("取消");await Until(()=>worldIo==null&&screen=="backups");Check(Directory.Exists(neighborCopy),"backup cancel deleted files");ShowDeleteBackup(backup);Click("确认永久删除备份");await Until(()=>worldIo==null&&screen=="backups");Check(!Directory.Exists(neighborCopy)&&saves.Load(b.WorldId).Snapshot.Name==b.Name,"backup delete removed active world or left files");
            string deleted=saves.DeleteWorld(b.WorldId,b.Name);var trashed=saves.ListDeletedWorlds().Single(e=>e.Key==deleted);ShowPurgeDeletedWorld(trashed);Click("确认永久删除");await Until(()=>worldIo==null&&screen=="deleted-worlds");Check(!saves.ListDeletedWorlds().Any(e=>e.Key==deleted),"trash UI did not purge");Mark();
            stage="refresh existing version-four save using native UI";var old=new WorldState(catalog,new(){Name="旧地表验证",Seed=16,GeneratorVersion=4});var placed=new Cell(-4,37,4);old.State.Inventory.Add("wood",1,catalog);Check(old.Break(placed,0,out _)&&old.Place(placed,"wood",false,out _),"placed wood setup");old.State.Mainline.Proofs.Add("mine");saves.Save(old.State);entry=saves.ListDetails().Single(e=>e.Id==old.State.WorldId);ShowSurfaceRefresh(entry);Click("确认更新地表");await Until(()=>worldIo==null&&screen=="worlds");var updated=new WorldState(catalog,saves.Load(old.State.WorldId).Snapshot);Check(updated.State.GeneratorVersion==5&&updated.Get(placed)==catalog.Number("wood")&&updated.Solid(placed)&&updated.State.Mainline.Proofs.Contains("mine"),"refresh damaged owned blocks or progress");Check(saves.ListBackups().Any(e=>e.Key.StartsWith(old.State.WorldId+"-")),"refresh backup absent");Mark();
            if(surfacePreview){
                if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("地表预览需要渲染窗口");string previews=ProjectSettings.GlobalizePath("res://docs/previews");Directory.CreateDirectory(previews);var canvas=new CanvasLayer{Layer=20};AddChild(canvas);var label=EmberLighthouse.UI.UiFactory.Label("",25);label.Position=new(22,22);canvas.AddChild(label);
                using var sheet=Image.CreateEmpty(1280,1440,false,Image.Format.Rgba8);int index=0;var seeds=Enumerable.Range(0,100).Where(seed=>Landscape.StartProfile(seed)==0).Take(2).ToArray();
                foreach(int seed in seeds){settings.ViewDistance=4;StartWorld(new(){Name="稀疏地表预览",Seed=seed,Time=480,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);SetScreen("journal");overlay.Visible=false;hudRoot.Visible=false;notice.Visible=false;player.GlobalPosition=new(0,40,-6);player.Camera.LookAt(new Vector3(0,37,12));UpdateDaylight();label.Text="新出生区 · 种子 "+seed+" · 分散物资 / 稀疏植物";
                    await Until(()=>renderer!.Loaded>=290&&renderer.Pending==0);await Frames(6);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var shot=GetViewport().GetTexture().GetImage();shot.SavePng(Path.Combine(previews,"natural-spawn-"+index+".png"));sheet.BlitRect(shot,new(0,0,1280,720),new(0,index*720));index++;}
                sheet.SavePng(Path.Combine(previews,"natural-spawns.png"));canvas.QueueFree();Mark();
            }
            File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=true,milestones,seconds=timer.Elapsed.TotalSeconds,controlledFixtures=true,userSavesTouched=false,rendered=surfacePreview},Catalog.JsonOptions));GD.Print("SURFACE_PASS: native permanent delete/cancel, backup purge, recycled world purge and safe surface refresh");QuitCleanly();
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=false,stage,error=e.ToString(),milestones},Catalog.JsonOptions));GD.PushError("SURFACE_FAILED: "+stage+" / "+e);QuitCleanly(9);}
    }
}
