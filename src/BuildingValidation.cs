using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private readonly Stopwatch buildingWatch=new();
    private bool buildingStarted;
    private void StartBuildingCheck(){StartWorld(new(){Name="建造碰撞验证",Seed=42,Casual=true});buildingWatch.Start();}
    private void BuildingFailure(string reason){GD.PushError("BUILDING_FAILED: "+reason);GetTree().Quit(8);}
    private void RunBuildingCheck()
    {
        if(buildingWatch.Elapsed.TotalSeconds>45){BuildingFailure("timeout");return;}
        if(buildingStarted||renderer==null||player==null||buildingWatch.Elapsed.TotalSeconds<5||!renderer.IsTerrainReady(player.GlobalPosition))return;
        buildingStarted=true;CheckBuildingInEngine();
    }
    private async void CheckBuildingInEngine()
    {
        static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        try{
            var door=new Cell(4,37,4);var upper=door+new Cell(0,1,0);var farm=new Cell(6,37,6);var crop=farm+new Cell(0,1,0);var wood=new Cell(-3,37,4);
            world!.Set(door,0);world.Set(upper,0);world.State.Inventory.Add("door",1,catalog);
            Check(world.Place(door,"door",false,out var error),error);renderer!.RefreshEdit(door);renderer.RefreshEdit(upper);
            async System.Threading.Tasks.Task PhysicsSettled(){await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
            bool Blocks(Vector3 origin,Vector3 movement)=>player!.TestMove(new Transform3D(Basis.Identity,origin),movement);
            void Open(bool open){foreach(var p in new[]{door,upper}){world.State.Entities[p.ToString()].Open=open;world.Set(p,world.Get(p));renderer.RefreshEdit(p);}}
            await PhysicsSettled();
            Check(Blocks(new(4.5f,37.04f,3.1f),new(0,0,1.8f)),"closed lower door has no collision");
            Check(Blocks(new(4.5f,38.1f,3.1f),new(0,0,1.8f)),"closed upper door has no collision");
            Open(true);await PhysicsSettled();
            Check(!Blocks(new(4.5f,37.04f,3.1f),new(0,0,1.8f)),"open door still blocks doorway");
            Check(Blocks(new(3.1f,37.04f,4.5f),new(1.8f,0,0)),"open lower panel lost collision");
            Check(Blocks(new(3.1f,38.1f,4.5f),new(1.8f,0,0)),"open upper panel lost collision");
            Open(false);await PhysicsSettled();Check(Blocks(new(4.5f,37.04f,3.1f),new(0,0,1.8f)),"reclosed door still passable");
            Check(world.Break(wood,0,out error)&&world.Place(wood,"wood",false,out error)&&world.Solid(wood),"placed starter-position wood lost solidity: "+error);renderer.RefreshEdit(wood);
            world.Set(farm,0);world.Set(crop,0);world.State.Inventory.Add("farmland",1,catalog);world.State.Inventory.Add("wheat_seed",1,catalog);
            Check(world.Place(farm,"farmland",false,out error)&&world.Plant(farm,"wheat_seed",out error),error);world.Advance(270);renderer.RefreshEdit(farm);renderer.RefreshEdit(crop);
            Check(world.State.Entities[crop.ToString()].Stage==3,"crop did not mature");
            player!.GlobalPosition=new(4.5f,37.05f,1.5f);player.Velocity=Vector3.Zero;player.RotationDegrees=new(0,180,0);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            string results=ProjectSettings.GlobalizePath("res://test-results");bool headless=DisplayServer.GetName()=="headless";
            if(!headless){using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(results,"building.png"));}
            Check(world.Break(upper,0,out error)&&world.Get(door)==0&&world.Get(upper)==0&&world.State.Inventory.Count("door")==1,"breaking upper door lost or duplicated item");renderer.RefreshEdit(door);renderer.RefreshEdit(upper);
            Harvest(crop,world.State.Entities[crop.ToString()]);Check(world.Get(crop)==0&&world.State.Inventory.Count("grain")==3&&world.State.Inventory.Count("wheat_seed")==2,"crop harvest lost items");
            string id=world.State.WorldId;
            RequestSave(success=>{
                if(!success){BuildingFailure("save failed");return;}
                RunWorldIo("检查建造存档",()=>saves.Load(id),loaded=>{
                    var restored=new WorldState(catalog,loaded.Snapshot);
                    if(restored.Get(door)!=0||restored.Get(upper)!=0||!restored.Solid(wood)||restored.Get(farm)!=catalog.Number("farmland")||restored.Get(crop)!=0||restored.State.Inventory.Count("door")!=1||restored.State.Inventory.Count("grain")!=3||restored.State.Inventory.Count("wheat_seed")!=2){BuildingFailure("reload mismatch");return;}
                    File.WriteAllText(Path.Combine(results,"building.json"),JsonSerializer.Serialize(new{passed=true,seconds=buildingWatch.Elapsed.TotalSeconds,closedBlocks=true,openDoorwayPasses=true,openPanelBlocks=true,upperPanelBlocks=true,recloseBlocks=true,doorCount=1,grainCount=3,seedCount=2,placedStarterWoodSolid=true,reload=true,headless,renderedImage=!headless},EmberLighthouse.Items.Catalog.JsonOptions));
                    GD.Print("BUILDING_PASS: real door collision, open panel, planted crop, item conservation and reload");QuitCleanly();
                },()=>BuildingFailure("reload failed"));
            });
        }catch(Exception e){BuildingFailure(e.Message);}
    }
}
