using System;
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
    private async Task ValidateWorldMap(string folder)
    {
        void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException("world map: "+reason);}
        async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Ready(WorldMap map){await Frame();var timer=Stopwatch.StartNew();while(!map.IsMapReady){if(timer.Elapsed.TotalSeconds>30)throw new TimeoutException("world map tiles");await Frame();}}
        Button FindButton(Node parent,string text){foreach(var child in parent.GetChildren()){if(child is Button button&&button.Text==text)return button;try{return FindButton(child,text);}catch(InvalidOperationException){}}throw new InvalidOperationException(text);}
        WorldMap FindMap(Node parent){foreach(var child in parent.GetChildren()){if(child is WorldMap map)return map;try{return FindMap(child);}catch(InvalidOperationException){}}throw new InvalidOperationException("map widget missing");}
        async Task Shot(string name){if(DisplayServer.GetName()=="headless")return;await Frame();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();Check(image.SavePng(Path.Combine(folder,name+".png"))==Error.Ok,"screenshot");}
        ShowMap();var map=FindMap(overlay);await Ready(map);
        var fog=new Color(30/255f,36/255f,40/255f);Check(map.TrySample(120,0,out var unknown)&&unknown==fog,"unvisited terrain is visible");int initialExplored=world!.State.ExploredMap.Count;
        Check(map.MouseFilter==Control.MouseFilterEnum.Stop&&map.TextureFilter==CanvasItem.TextureFilterEnum.Nearest,"no interaction / blurred texture filter");
        var rect=map.GetGlobalRect();Check(GetViewport().GetVisibleRect().Encloses(rect),"map outside viewport");
        double time=world!.State.Time,sky=world.State.SkyTime;_Process(60);Check(world.State.Time==time&&world.State.SkyTime==sky,"map advances survival time");
        float scaleX=(map.ScreenPoint(1,0)-map.ScreenPoint(0,0)).Length(),scaleZ=(map.ScreenPoint(0,1)-map.ScreenPoint(0,0)).Length();Check(Math.Abs(scaleX-scaleZ)<.001,"map distorts world proportions");
        var anchor=map.Size/2+new Vector2(73,-31);var before=map.WorldPoint(anchor);
        map._GuiInput(new InputEventMouseButton{Position=anchor,ButtonIndex=MouseButton.WheelUp,Pressed=true});Check(map.Zoom>2&&map.WorldPoint(anchor).DistanceTo(before)<.001,"wheel does not zoom at cursor");
        var center=map.MapCenter;map._GuiInput(new InputEventMouseButton{Position=anchor,ButtonIndex=MouseButton.Left,Pressed=true});map._GuiInput(new InputEventMouseMotion{Position=anchor+new Vector2(80,30),Relative=new(80,30)});map._Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false});
        Check(map.MapCenter.DistanceTo(center-new Vector2(80,30)/map.Zoom)<.001,"drag does not move map");center=map.MapCenter;map._GuiInput(new InputEventMouseMotion{Position=anchor,Relative=new(50,20)});Check(map.MapCenter==center,"drag remains stuck after outside release");
        float zoom=map.Zoom;FindButton(overlay,"＋").EmitSignal(Button.SignalName.Pressed);Check(map.Zoom>zoom,"zoom button");FindButton(overlay,"－").EmitSignal(Button.SignalName.Pressed);Check(Math.Abs(map.Zoom-zoom)<.001,"zoom-out button");
        int count=world.State.Markers.Count;FindButton(overlay,"标记当前位置").EmitSignal(Button.SignalName.Pressed);Check(world.State.Markers.Count==count+1&&map.MapCenter==center&&map.Zoom==zoom,"marking resets map view");
        FindButton(overlay,"定位玩家").EmitSignal(Button.SignalName.Pressed);Check(map.MapCenter.DistanceTo(new(player!.GlobalPosition.X,player.GlobalPosition.Z))<.001,"locate player");
        map.SetView(new(player.GlobalPosition.X,player.GlobalPosition.Z),4);await Ready(map);await Shot("world-map");
        int x=(int)Math.Floor(player.GlobalPosition.X)+6,z=(int)Math.Floor(player.GlobalPosition.Z)+5;var roof=new Cell(x,150,z);ushort original=world.Get(roof);
        Check(map.TrySample(x,z,out var ground),"surface unavailable");world.Set(roof,catalog.Number("plank"));await Ready(map);Check(map.TrySample(x,z,out var built)&&ground!=built,"construction not reflected live");
        world.Set(roof,original);await Ready(map);Check(map.TrySample(x,z,out var restored)&&ground==restored,"demolition does not refresh");
        var tower=world.Terrain.TowerLocations[0];map.SetView(new(tower.X,tower.Z),8);await Ready(map);map._GuiInput(new InputEventMouseMotion{Position=map.Size/2});Check(map.TooltipText.Contains("未探索")&&!map.TooltipText.Contains("灯塔")&&world.State.ExploredMap.Count==initialExplored,"panning leaks objectives or reveals terrain");world.RevealMap(tower.X,tower.Z);await Ready(map);bool activated=world.State.Beacons.Contains(0);world.State.Beacons.Add(0);await Frame();map._Process(.11);Check(map.TooltipText.Contains("已激活"),"marker state is stale");if(!activated)world.State.Beacons.Remove(0);await Shot("world-map-zoomed");
        FindButton(overlay,"全图").EmitSignal(Button.SignalName.Pressed);await Ready(map);Check(map.MapCenter==Vector2.Zero&&map.Zoom<.3,"fit entire world");await Shot("world-map-full");
        int beforeBrowsing=world.State.ExploredMap.Count;for(int j=0;j<5;j++)for(int i=0;i<5;i++){map.SetView(new(-1024+i*512,-1024+j*512),16);await Ready(map);Check(map.CachedTileCount<=96,"unbounded tile cache");}Check(map.CachedTileCount==96&&world.State.ExploredMap.Count==beforeBrowsing,"cache eviction missing or browsing reveals terrain");
        var savedPosition=player.GlobalPosition;center=map.MapCenter;player.GlobalPosition+=new Vector3(3,0,2);await Frame();map._Process(.11);Check(map.PlayerPosition==player.GlobalPosition&&map.MapCenter==center,"player position stale or pan overwritten");player.GlobalPosition=savedPosition;
        map.SetView(new(5000,-5000),1000);Check(map.MapCenter==new Vector2(2048,-2048)&&map.Zoom==16,"unbounded pan/zoom");map.SetView(Vector2.Zero,.00001f);Check(map.Zoom==1f/64,"zoom lower bound");
        FindButton(overlay,"返回游戏（M）").EmitSignal(Button.SignalName.Pressed);Check(screen=="play"&&player.Enabled,"map cannot close");
        Check(HandleMapInput(new InputEventAction{Action="map",Pressed=true})&&screen=="map","map action does not open");map=FindMap(overlay);await Ready(map);Check(map.TrySample(x,z,out restored)&&restored==ground,"reopened map uses outdated surface");Check(HandleMapInput(new InputEventAction{Action="map",Pressed=true})&&screen=="play","map action does not close");
        player.GlobalPosition=new(120,savedPosition.Y,0);_Process(.016);Check(world.MapKnown(120,0)&&world.MapKnown(0,0),"actual gameplay movement did not reveal/retain exploration");player.GlobalPosition=savedPosition;
        var save=saveQueue.Enqueue(world.State);while(!save.IsCompleted)await Frame();save.GetAwaiter().GetResult();var loaded=saves.Load(world.State.WorldId).Snapshot;Check(MapExploration.Known(loaded.ExploredMap,120,0)&&MapExploration.Known(loaded.ExploredMap,0,0),"actual save lost explored areas");
        if(DisplayServer.GetName()!="headless"){
            int seed=Enumerable.Range(0,1000).First(s=>{var terrain=new EmberLighthouse.World.Terrain(s,catalog,1);return terrain.Biome(48,0)==2&&terrain.Biome(0,48)==2;});var legacy=new EmberLighthouse.World.Terrain(seed,catalog,1);var natural=new EmberLighthouse.World.Terrain(seed,catalog,2);
            using var sheet=Image.CreateEmpty(512,256,false,Image.Format.Rgba8);int column=0;foreach(var terrain in new[]{legacy,natural}){var bytes=MapRaster.Render(terrain,catalog,new System.Collections.Generic.Dictionary<Cell,ushort>(),-128,-128,1,256);using var tile=Image.CreateFromData(256,256,false,Image.Format.Rgba8,bytes);sheet.BlitRect(tile,new(0,0,256,256),new(column++*256,0));}Check(sheet.SavePng(Path.Combine(folder,"starter-terrain-before-after.png"))==Error.Ok,"terrain comparison screenshot");
        }
        File.WriteAllText(Path.Combine(folder,"world-map.json"),JsonSerializer.Serialize(new{passed=true,mouseDrag=true,wheelAnchorZoom=true,zoomButtons=true,nearestPixels=true,uniformScale=true,liveConstruction=true,liveDemolition=true,liveMarkers=true,pausedWorld=true,fullWorld=true,boundedCache=96,unknownTerrainHidden=true,unknownObjectivesHidden=true,browsingDoesNotReveal=true,actualMovementReveals=true,actualSaveRetainsExploration=true,renderedScreenshots=DisplayServer.GetName()!="headless"},Catalog.JsonOptions));
    }
}

