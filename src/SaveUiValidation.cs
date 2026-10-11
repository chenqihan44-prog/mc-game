using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private bool saveUiCheck;
    private int saveUiStage;
    private string checkedWorldId="";
    private readonly Stopwatch saveUiWatch=new();
    private long saveUiFrame;
    private readonly List<double> saveUiFrames=[];
    private double lastCaptureMs,lastWriteMs;
    private double saveUiNextCollection;
    private int saveUiCollections;
    private void StartSaveUiCheck()
    {
        var snapshot=new WorldSnapshot{Name="异步保存验证",Seed=42,Creative=true};
        for(int column=0;column<12;column++)for(int i=0;i<8192;i++)snapshot.Changes[new Cell(512+column*16+i%16,96+i/256,512+i/16%16).ToString()]="plank";
        checkedWorldId=snapshot.WorldId;StartWorld(snapshot);saveUiWatch.Start();saveUiFrame=Stopwatch.GetTimestamp();
    }
    private void SaveUiFailure(string reason){GD.PushError("SAVE_UI_FAILED: "+reason);GetTree().Quit(7);}
    private bool CheckedSnapshot(WorldSnapshot snapshot)
    {
        return snapshot.Changes.Count==98305&&snapshot.Changes.GetValueOrDefault("512,96,512")=="plank"&&snapshot.Changes.GetValueOrDefault("703,127,527")=="plank"&&snapshot.Changes.GetValueOrDefault("0,70,0")=="glass"&&snapshot.Inventory.Count("stone")==7&&snapshot.Inventory.Count("wood")==2&&snapshot.Inventory.Count("berry")==3&&snapshot.Cursor==null&&snapshot.Crafting.Slots.All(s=>s==null);
    }
    private void RunSaveUiCheck()
    {
        long now=Stopwatch.GetTimestamp();double ms=(now-saveUiFrame)*1000.0/Stopwatch.Frequency;saveUiFrame=now;
        if(saveUiStage==1)saveUiFrames.Add(ms);
        if(gcStress&&saveUiWatch.Elapsed.TotalSeconds>=saveUiNextCollection){saveUiNextCollection=saveUiWatch.Elapsed.TotalSeconds+.5;GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();saveUiCollections++;}
        if(saveUiWatch.Elapsed.TotalSeconds>90){SaveUiFailure("file operation or terrain readiness timeout");return;}
        if(saveUiStage==0&&saveUiWatch.Elapsed.TotalSeconds>8&&renderer!.IsTerrainReady(player!.GlobalPosition)){
            world!.State.Inventory.Add("stone",10,catalog);world.State.Crafting.Add("wood",2,catalog);world.State.Cursor=new("berry",3);
            saveUiStage=1;RequestSave();world.State.Inventory.Remove("stone",3);world.Set(new(0,70,0),catalog.Number("glass"));renderer.RefreshEdit(new(0,70,0));
            RequestSave(success=>{if(!success){SaveUiFailure("background save failed");return;}saveUiStage=2;ReturnMenu();});
        }else if(saveUiStage==2&&world==null&&screen=="menu"){
            saveUiStage=3;RunWorldIo("验证退出与读取",()=>saves.Load(checkedWorldId),loaded=>{if(!CheckedSnapshot(loaded.Snapshot)){SaveUiFailure("state mismatch after returning to menu");return;}StartWorld(loaded.Snapshot);saveUiStage=4;},()=>SaveUiFailure("reload failed"));
        }else if(saveUiStage==4&&renderer!.IsTerrainReady(player!.GlobalPosition)){
            saveUiStage=5;BackupCurrentWorld();
        }else if(saveUiStage==5&&screen=="pause"&&worldIo==null&&saveCallbacks.Count==0){
            saveUiStage=6;RunWorldIo("验证备份恢复",()=>{var entry=saves.ListBackups().First(b=>b.Name=="异步保存验证"&&!b.Damaged);string restored=saves.RestoreBackup(entry.Key);return saves.Load(restored).Snapshot;},snapshot=>{if(snapshot.WorldId==checkedWorldId||!CheckedSnapshot(snapshot)){SaveUiFailure("restored backup state mismatch");return;}saveUiStage=7;ReturnMenu();},()=>SaveUiFailure("backup restore failed"));
        }else if(saveUiStage==7&&world==null&&screen=="menu"){
            saveUiStage=8;string path=ProjectSettings.GlobalizePath("res://test-results");Directory.CreateDirectory(path);var frames=saveUiFrames.Order().ToArray();
            File.WriteAllText(Path.Combine(path,"save-ui.json"),JsonSerializer.Serialize(new{passed=true,seconds=saveUiWatch.Elapsed.TotalSeconds,edits=98305,exitReload=true,backupRestore=true,craftingAndCursorReturned=true,lastCaptureMs,lastWriteMs,savingFrameSamples=frames.Length,savingP99Ms=frames.Length>0?frames[Math.Min(frames.Length-1,(int)(frames.Length*.99))]:0,forcedCollections=saveUiCollections,headless=DisplayServer.GetName()=="headless"},Catalog.JsonOptions));
            GD.Print("SAVE_UI_PASS: background snapshots, menu exit, reload, backup restore and cursor/crafting conservation");QuitCleanly();
        }
    }
}
