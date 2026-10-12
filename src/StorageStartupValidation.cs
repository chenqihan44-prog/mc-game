using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool storageStartupCheck;
    private string storageFixture="";
    private string StorageValidationDirectory=>ProjectSettings.GlobalizePath("res://test-results/storage-startup");
    private string FixturePath(string path)
    {
        string full=Path.GetFullPath(path),allowed=Path.GetFullPath(Path.Combine(StorageValidationDirectory,"runs"))+Path.DirectorySeparatorChar;
        if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Storage fixture path outside isolated runs: "+full);
        return full;
    }
    private string PrepareStorageStartupFixture()
    {
        if(OS.GetCmdlineUserArgs().Contains("--storage-startup-reload")){
            string root=FixturePath(File.ReadAllText(Path.Combine(StorageValidationDirectory,"reload-root.txt")).Trim());
            storageFixture=Path.GetDirectoryName(root)!;return root;
        }
        storageFixture=FixturePath(Path.Combine(StorageValidationDirectory,"runs",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(storageFixture);
        string blocked=Path.Combine(storageFixture,"blocked-parent");File.WriteAllText(blocked,"startup storage obstruction fixture");return blocked;
    }
    private async void RunStorageStartupCheck()
    {
        bool reload=OS.GetCmdlineUserArgs().Contains("--storage-startup-reload"),exitOnly=OS.GetCmdlineUserArgs().Contains("--storage-startup-exit");
        var timer=Stopwatch.StartNew();string stage="startup";var samples=new List<object>();
        static void Check(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node root){foreach(var child in root.GetChildren()){yield return child;foreach(var n in Descendants(child))yield return n;}}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> ready){double end=timer.Elapsed.TotalSeconds+30;while(!ready()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        void Press(string text)=>Descendants(overlay).OfType<Button>().Single(b=>b.Text==text).EmitSignal(Button.SignalName.Pressed);
        string Text()=>string.Join("\n",Descendants(overlay).OfType<Label>().Select(l=>l.Text));
        void Failure(string path,int attempts){Check(!startupReady&&screen=="startup-error"&&startupAttempts==attempts,"failure not recoverable "+attempts);Check(world==null&&saveQueue==null,"partial initialization created a world or save worker");Check(Text().Contains(path),"failure UI missing actual path");Check(!Directory.GetFiles(storageFixture,".startup-probe-*",SearchOption.AllDirectories).Any(),"temporary probe left behind");}
        static Dictionary<string,string> Hashes(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        static void SameHashes(Dictionary<string,string> expected,string root){var actual=Hashes(root);Check(actual.Count==expected.Count&&expected.All(p=>actual.GetValueOrDefault(p.Key)==p.Value),"existing world files modified during failed startup/retry");}
        void StateMatches(WorldSnapshot actual,WorldSnapshot expected){
            Check(actual.WorldId==expected.WorldId&&actual.FormatVersion==WorldSnapshot.CurrentFormat,"existing world identity/format changed");
            Check(actual.Changes.Count==expected.Changes.Count&&expected.Changes.All(p=>actual.Changes.GetValueOrDefault(p.Key)==p.Value),"existing terrain changed");
            Check(JsonSerializer.Serialize(actual.Inventory,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Inventory,Catalog.JsonOptions)&&JsonSerializer.Serialize(actual.Equipment,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Equipment,Catalog.JsonOptions),"inventory or equipment wear changed");
            Check(JsonSerializer.Serialize(actual.Entities,Catalog.JsonOptions)==JsonSerializer.Serialize(expected.Entities,Catalog.JsonOptions),"stored chest or contents changed");
        }
        void SettingsMatch(){Check(settings.ViewDistance==4&&settings.UiScale==1.25f&&!settings.DamageFlash&&settings.Keys.GetValueOrDefault("jump")== (long)Key.J,"existing settings not restored");}
        async Task LoadThroughUi(WorldSnapshot expected){
            Press("读取与备份世界");await Until(()=>screen=="worlds");Check(Text().Contains(expected.Name),"existing world absent from actual list");Press("读取");await Until(()=>world!=null&&screen=="play");player!.SetPhysicsProcess(false);ShowPause();await Frames();StateMatches(world!.State,expected);
        }
        void Record(bool passed,string error)=>File.WriteAllText(Path.Combine(StorageValidationDirectory,reload?"reload.json":exitOnly?"exit.json":"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedImage=false,humanPlaytest=false,controlledSetup=true,grantedFixtureMaterials=!reload,fixture=storageFixture,attempts=startupAttempts,samples},Catalog.JsonOptions));
        try{
            await Frames(8);
            if(reload){
                stage="independent process reload";Check(startupReady&&screen=="menu","cold startup failed");SettingsMatch();var expected=JsonSerializer.Deserialize<WorldSnapshot>(File.ReadAllText(Path.Combine(StorageValidationDirectory,"reload-world.json")),Catalog.JsonOptions)!;
                StateMatches(saves.Load(expected.WorldId).Snapshot,expected);await LoadThroughUi(expected);samples.Add(new{stage,worldId=expected.WorldId,chestWear=37,helmetWear=73});Press("保存并返回主菜单");await Until(()=>world==null&&screen=="menu");Record(true,"");GD.Print("STORAGE_STARTUP_RELOAD_PASS: independent EXE reads existing settings, terrain, inventory, armor and chest through world-list UI");Press("退出");return;
            }
            stage="blocked root and repeated retry";string root=FixturePath(Path.Combine(storageFixture,"blocked-parent"));string obstructionHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(root)));Failure(root,1);
            await Frames(30);Press("重试");await Frames();Failure(root,2);Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(root)))==obstructionHash,"root obstruction overwritten");
            foreach(var size in new[]{new Vector2I(1280,720),new(1920,1080),new(2560,1440)}){
                GetWindow().Size=size;await Frames(4);var viewport=overlay.GetViewportRect().Size;
                foreach(var button in Descendants(overlay).OfType<Button>()){var rect=button.GetGlobalRect();Check(rect.Position.X>=0&&rect.Position.Y>=0&&rect.End.X<=viewport.X+.5&&rect.End.Y<=viewport.Y+.5,"failure buttons outside viewport "+size);}
                samples.Add(new{stage="failure button geometry",width=size.X,height=size.Y,renderedImage=false});
            }
            GetWindow().Size=new(1280,720);
            if(exitOnly){Record(true,"");GD.Print("STORAGE_STARTUP_EXIT_PASS: blocked startup remains recoverable and actual Exit button invoked");Press("退出");return;}
            stage="existing world fixture";File.Delete(FixturePath(root));Directory.CreateDirectory(root);
            var fixtureStore=new SaveStore(Path.Combine(root,"worlds"),catalog);var expectedWorld=new WorldSnapshot{Name="启动恢复原有世界",Seed=42,Creative=true,Position=[0,80,0],GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}};
            expectedWorld.Inventory.Add("wood",11,catalog);expectedWorld.Equipment.Slots[0]=new("iron_helmet",1,73);expectedWorld.Changes[new Cell(2,79,0).ToString()]="stone";fixtureStore.Save(expectedWorld);
            string fixtureSettings=Path.Combine(root,"settings.json");File.WriteAllText(fixtureSettings,JsonSerializer.Serialize(new GameSettings{ViewDistance=4,UiScale=1.25f,DamageFlash=false,Keys=new(){{"jump",(long)Key.J}}},Catalog.JsonOptions));
            string settingsHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixtureSettings)));string worlds=FixturePath(fixtureStore.Root),retained=FixturePath(Path.Combine(root,"retained-worlds"));var originalHashes=Hashes(worlds);
            stage="blocked worlds subdirectory";Directory.Move(worlds,retained);File.WriteAllText(worlds,"worlds obstruction fixture");Press("重试");await Frames(8);Failure(worlds,3);SameHashes(originalHashes,retained);
            Check(File.ReadAllText(worlds)=="worlds obstruction fixture","worlds obstruction overwritten");File.Delete(FixturePath(worlds));Directory.Move(FixturePath(retained),FixturePath(worlds));
            stage="locked existing settings";using(var locked=new FileStream(fixtureSettings,FileMode.Open,System.IO.FileAccess.ReadWrite,FileShare.None)){Press("重试");await Frames(8);Failure(fixtureSettings,4);SameHashes(originalHashes,worlds);}
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixtureSettings)))==settingsHash,"locked settings changed");samples.Add(new{stage,worldFilesUnchanged=true});
            stage="read-only existing settings";var attributes=File.GetAttributes(fixtureSettings);
            try{File.SetAttributes(FixturePath(fixtureSettings),attributes|System.IO.FileAttributes.ReadOnly);Press("重试");await Frames(8);Failure(fixtureSettings,5);SameHashes(originalHashes,worlds);}
            finally{File.SetAttributes(FixturePath(fixtureSettings),attributes);}
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixtureSettings)))==settingsHash,"read-only settings changed");samples.Add(new{stage,worldFilesUnchanged=true,settingsUnchanged=true});
            stage="retry recovers original settings and world";Press("重试");await Frames(8);Check(startupReady&&screen=="menu"&&startupAttempts==6&&saveQueue!=null,"retry did not initialize menu/worker once");SettingsMatch();SameHashes(originalHashes,worlds);StateMatches(saves.Load(expectedWorld.WorldId).Snapshot,expectedWorld);
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixtureSettings)))==settingsHash,"successful startup rewrote settings");await LoadThroughUi(expectedWorld);SameHashes(originalHashes,worlds);
            stage="real world edit and save-menu button";var chest=new Cell(4,80,0);world!.Set(chest,0);Check(world.Place(chest,"chest",false,out string error),error);renderer!.RefreshEdit(chest);world.State.Entities[chest.ToString()].Storage.Slots[0]=new("iron_boots",1,37);world.State.Inventory.Add("stone",3,catalog);expectedWorld=world.State.Copy();
            Press("保存并返回主菜单");await Until(()=>world==null&&screen=="menu"&&saveCallbacks.Count==0);Check(!saveFailed,"save failed after recovered startup");StateMatches(saves.Load(expectedWorld.WorldId).Snapshot,expectedWorld);
            Check(saves.List().Count()==1,"retry created duplicate or blank worlds");Check(!Directory.GetFiles(root,".startup-probe-*",SearchOption.AllDirectories).Any(),"probe remained after successful retry");
            File.WriteAllText(Path.Combine(StorageValidationDirectory,"reload-root.txt"),root);File.WriteAllText(Path.Combine(StorageValidationDirectory,"reload-world.json"),JsonSerializer.Serialize(expectedWorld,Catalog.JsonOptions));
            samples.Add(new{stage,worldId=expectedWorld.WorldId,attempts=startupAttempts,helmetWear=73,chestWear=37,wood=11,stone=3});Record(true,"");GD.Print("STORAGE_STARTUP_PASS: blocked root/worlds, locked settings, real Retry/Exit buttons, existing world/settings preserved, actual world-list load/edit/save-menu");Press("退出");
        }catch(Exception error){Record(false,error.Message);GD.PushError("STORAGE_STARTUP_FAILED: "+error);QuitCleanly(9);}
    }
}
