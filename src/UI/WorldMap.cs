using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.UI;

public partial class WorldMap : Control
{
    private const int Pixels=128,CacheLimit=96;
    private readonly record struct TileKey(int X,int Z,int Step);
    private sealed record Tile(ImageTexture Texture,byte[] Bytes){public long Use;}
    private sealed record Job(TileKey Key,long Version,long Exploration,Task<byte[]> Work);
    private readonly Dictionary<TileKey,Tile> tiles=[];
    private readonly List<Job> jobs=[];
    private readonly CancellationTokenSource cancellation=new();
    private List<TileKey> visible=[];
    private long version=-1,explorationVersion=-1,tick;
    private HashSet<int> explored=[];
    private double redrawClock,trimClock;
    private bool dragging;
    private Vector2 cursor;
    private Vector2 preparedCenter,preparedSize;
    private float preparedZoom;
    public WorldState World {get;set;}=null!;
    public Vector3 PlayerPosition {get;set;}
    public Func<Vector3>? PositionSource {get;set;}
    public Func<float>? FacingSource {get;set;}
    public Action<string>? StatusChanged {get;set;}
    public Vector2 MapCenter {get;private set;}
    public float Zoom {get;private set;}=2;
    public int CachedTileCount=>tiles.Count;
    public bool IsMapReady=>version==World.EditVersion&&explorationVersion==World.ExplorationVersion&&preparedCenter==MapCenter&&preparedZoom==Zoom&&preparedSize==Size&&visible.Count>0&&visible.All(tiles.ContainsKey);
    private int Step {get{int step=1;while(step*Zoom<1)step*=2;return step;}}

    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Stop;FocusMode=FocusModeEnum.All;TextureFilter=TextureFilterEnum.Nearest;ClipContents=true;MouseDefaultCursorShape=CursorShape.Drag;
        MouseExited+=()=>TooltipText="";FocusPlayer();
    }
    public Vector2 WorldPoint(Vector2 local)=>MapCenter+(local-Size/2)/Zoom;
    public Vector2 ScreenPoint(float x,float z)=>(new Vector2(x,z)-MapCenter)*Zoom+Size/2;
    public void SetView(Vector2 center,float zoom)
    {
        Zoom=Math.Clamp(zoom,1f/64,16);MapCenter=new(Math.Clamp(center.X,-Coordinates.Limit,Coordinates.Limit),Math.Clamp(center.Y,-Coordinates.Limit,Coordinates.Limit));QueueRedraw();Report();
    }
    public void ZoomAt(Vector2 anchor,float factor)
    {
        var point=WorldPoint(anchor);float zoom=Math.Clamp(Zoom*factor,1f/64,16);SetView(point-(anchor-Size/2)/zoom,zoom);
    }
    public void FocusPlayer(){PlayerPosition=PositionSource?.Invoke()??PlayerPosition;SetView(new(PlayerPosition.X,PlayerPosition.Z),Zoom);}
    public void FitWorld()=>SetView(Vector2.Zero,Math.Min(Size.X,Size.Y)/(Coordinates.Limit*2)*.92f);
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton button){
            cursor=button.Position;
            if(button.ButtonIndex is MouseButton.Left or MouseButton.Middle){dragging=button.Pressed;GrabFocus();AcceptEvent();}
            else if(button.Pressed&&(button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)){ZoomAt(button.Position,button.ButtonIndex==MouseButton.WheelUp?1.25f:.8f);AcceptEvent();}
        }else if(e is InputEventMouseMotion motion){
            cursor=motion.Position;if(dragging){SetView(MapCenter-motion.Relative/Zoom,Zoom);AcceptEvent();}else Report();UpdateTooltip();
        }else if(e is InputEventPanGesture pan){SetView(MapCenter+pan.Delta*24/Zoom,Zoom);AcceptEvent();}
        else if(e is InputEventMagnifyGesture magnify){ZoomAt(magnify.Position,magnify.Factor);AcceptEvent();}
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventMouseButton button&&!button.Pressed&&(button.ButtonIndex is MouseButton.Left or MouseButton.Middle))dragging=false;
    }
    private void Report(){var p=WorldPoint(cursor);StatusChanged?.Invoke($"中心 X {MapCenter.X:0} / Z {MapCenter.Y:0} · 缩放 {Zoom:0.##} 像素/格 · 光标 X {p.X:0} / Z {p.Y:0}"+(World.MapKnown((int)Math.Floor(p.X),(int)Math.Floor(p.Y))?"":" · 未探索"));}
    private List<TileKey> VisibleTiles()
    {
        var low=WorldPoint(Vector2.Zero);var high=WorldPoint(Size);int step=Step,width=Pixels*step;
        int minX=Coordinates.FloorDiv((int)Math.Max(-Coordinates.Limit,Math.Floor(low.X)),width),maxX=Coordinates.FloorDiv((int)Math.Min(Coordinates.Limit-1,Math.Floor(high.X)),width);
        int minZ=Coordinates.FloorDiv((int)Math.Max(-Coordinates.Limit,Math.Floor(low.Y)),width),maxZ=Coordinates.FloorDiv((int)Math.Min(Coordinates.Limit-1,Math.Floor(high.Y)),width);
        var result=new List<TileKey>();for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)result.Add(new(x,z,step));
        return result.OrderBy(k=>(new Vector2((k.X+.5f)*width,(k.Z+.5f)*width)-MapCenter).LengthSquared()).ToList();
    }
    public override void _Process(double delta)
    {
        if(!IsVisibleInTree())return;tick++;PlayerPosition=PositionSource?.Invoke()??PlayerPosition;
        if(version!=World.EditVersion||explorationVersion!=World.ExplorationVersion){foreach(var t in tiles.Values)t.Texture.Dispose();tiles.Clear();version=World.EditVersion;explorationVersion=World.ExplorationVersion;explored=new(World.State.ExploredMap);}
        foreach(var job in jobs.Where(j=>j.Work.IsCompleted).ToArray()){
            if(job.Work.IsCompletedSuccessfully&&job.Version==version&&job.Exploration==explorationVersion){using var bitmap=Image.CreateFromData(Pixels,Pixels,false,Image.Format.Rgba8,job.Work.Result);tiles[job.Key]=new(ImageTexture.CreateFromImage(bitmap),job.Work.Result){Use=tick};}
            else if(job.Work.IsFaulted)GD.PushWarning("大地图更新失败："+job.Work.Exception?.GetBaseException().Message);
            jobs.Remove(job);
        }
        visible=VisibleTiles();preparedCenter=MapCenter;preparedZoom=Zoom;preparedSize=Size;foreach(var key in visible)if(tiles.TryGetValue(key,out var t))t.Use=tick;
        while(tiles.Count>CacheLimit){var oldest=tiles.MinBy(p=>p.Value.Use);oldest.Value.Texture.Dispose();tiles.Remove(oldest.Key);}
        foreach(var key in visible){
            if(jobs.Count>=2)break;if(tiles.ContainsKey(key)||jobs.Any(j=>j.Key==key))continue;
            int width=Pixels*key.Step,x=key.X*width,z=key.Z*width;
            var edits=width<=256?World.CaptureRegion(x+width/2,z+width/2,width/2):World.Changes.Where(p=>p.Key.X>=x&&p.Key.X<x+width&&p.Key.Z>=z&&p.Key.Z<z+width).ToDictionary(p=>p.Key,p=>p.Value);
            var terrain=World.Terrain;var catalog=World.Catalog;var token=cancellation.Token;var knowledge=explored;
            jobs.Add(new(key,version,explorationVersion,Task.Run(()=>MapRaster.Render(terrain,catalog,edits,x,z,key.Step,Pixels,token,knowledge),token)));
        }
        redrawClock+=delta;trimClock+=delta;if(trimClock>=1){trimClock=0;World.Terrain.TrimCaches();}
        if(redrawClock>=.1){redrawClock=0;UpdateTooltip();Report();QueueRedraw();}
    }
    public bool TrySample(int x,int z,out Color color)
    {
        int step=Step,width=Pixels*step;var key=new TileKey(Coordinates.FloorDiv(x,width),Coordinates.FloorDiv(z,width),step);color=default;
        if(!tiles.TryGetValue(key,out var tile))return false;int k=((x-key.X*width)/step+(z-key.Z*width)/step*Pixels)*4;color=new(tile.Bytes[k]/255f,tile.Bytes[k+1]/255f,tile.Bytes[k+2]/255f);return true;
    }
    private void UpdateTooltip()
    {
        TooltipText="";
        var point=WorldPoint(cursor);if(!World.MapKnown((int)Math.Floor(point.X),(int)Math.Floor(point.Y))){TooltipText=$"未探索 · X {point.X:0} / Z {point.Y:0}";return;}
        for(int i=0;i<World.Terrain.TowerLocations.Length;i++){var t=World.Terrain.TowerLocations[i];if(ScreenPoint(t.X,t.Z).DistanceTo(cursor)<12){TooltipText=$"灯塔 {i+1} · X {t.X} / Z {t.Z} · {(World.State.Beacons.Contains(i)?"已激活":"未激活")}";return;}}
        if(World.Terrain.Adventure is {} a)foreach(var site in a.Sites){var p=site.Entry;if(ScreenPoint(p.X,p.Z).DistanceTo(cursor)<12){TooltipText=$"{site.Name}入口 · X {p.X} / Y {p.Y} / Z {p.Z}";return;}}
        foreach(var g in World.State.Graves.Where(g=>g.Death||g.LegacyContainer))if(ScreenPoint((float)g.Position[0],(float)g.Position[2]).DistanceTo(cursor)<10){TooltipText=$"死亡掉落 · 接近拾取 · X {g.Position[0]:0} / Z {g.Position[2]:0}";return;}
        for(int i=0;i<World.State.Markers.Count;i++){var m=World.State.Markers[i];if(ScreenPoint((float)m[0],(float)m[1]).DistanceTo(cursor)<10){TooltipText=$"标记 {i+1} · X {m[0]:0} / Z {m[1]:0}";return;}}
    }
    public override void _ExitTree(){cancellation.Cancel();cancellation.Dispose();foreach(var tile in tiles.Values)tile.Texture.Dispose();tiles.Clear();}
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("182025"));
        foreach(var key in visible){int width=Pixels*key.Step;var rect=new Rect2(ScreenPoint(key.X*width,key.Z*width),Vector2.One*width*Zoom);if(tiles.TryGetValue(key,out var tile))DrawTextureRect(tile.Texture,rect,false);else DrawRect(rect,new Color("1e2428"));}
        if(Zoom>=6){var low=WorldPoint(Vector2.Zero);var high=WorldPoint(Size);var grid=new Color(1,1,1,.14f);for(int x=(int)Math.Ceiling(low.X);x<high.X;x++){float sx=ScreenPoint(x,0).X;DrawLine(new(sx,0),new(sx,Size.Y),grid);}for(int z=(int)Math.Ceiling(low.Y);z<high.Y;z++){float sy=ScreenPoint(0,z).Y;DrawLine(new(0,sy),new(Size.X,sy),grid);}}
        var font=GetThemeFont("font");
        void Marker(float x,float z,Color color,float radius,string label=""){
            if(!World.MapKnown((int)Math.Floor(x),(int)Math.Floor(z)))return;
            var p=ScreenPoint(x,z);if(!new Rect2(Vector2.Zero,Size).Grow(-radius).HasPoint(p))return;DrawCircle(p,radius+2,Colors.Black);DrawCircle(p,radius,color);
            if(label!=""&&Zoom>=.3){DrawString(font,p+new Vector2(10,7),label,fontSize:16,modulate:Colors.Black);DrawString(font,p+new Vector2(9,6),label,fontSize:16,modulate:Colors.White);}
        }
        for(int i=0;i<World.Terrain.TowerLocations.Length;i++){var t=World.Terrain.TowerLocations[i];Marker(t.X,t.Z,World.State.Beacons.Contains(i)?new("7ff7d3"):new("f4c16a"),6,$"灯塔 {i+1}");}
        if(World.Terrain.Adventure is {} a)foreach(var site in a.Sites)Marker(site.Entry.X,site.Entry.Z,new("dda6ff"),5,site.Name);
        foreach(var g in World.State.Graves.Where(g=>g.Death||g.LegacyContainer))Marker((float)g.Position[0],(float)g.Position[2],new("da885b"),4);
        foreach(var m in World.State.Markers)Marker((float)m[0],(float)m[1],new("6ac7ff"),4);
        var player=ScreenPoint(PlayerPosition.X,PlayerPosition.Z);float yaw=FacingSource?.Invoke()??0;var heading=new Vector2(-MathF.Sin(yaw),-MathF.Cos(yaw));var side=new Vector2(-heading.Y,heading.X);
        if(new Rect2(Vector2.Zero,Size).Grow(-9).HasPoint(player)){DrawCircle(player,10,Colors.Black);DrawColoredPolygon([player+heading*9,player-heading*5+side*5,player-heading*5-side*5],Colors.White);}
        DrawRect(new Rect2(8,8,66,29),new Color(0,0,0,.8f));DrawString(font,new(15,29),"N ↑ 北",fontSize:16,modulate:Colors.White);
        int length=1;while(length*Zoom<65)length*=2;float bar=length*Zoom;var origin=new Vector2(16,Size.Y-18);DrawRect(new Rect2(8,Size.Y-60,bar+24,52),new Color(0,0,0,.8f));DrawLine(origin,origin+new Vector2(bar,0),Colors.White,2);DrawLine(origin-new Vector2(0,5),origin+new Vector2(0,5),Colors.White,2);DrawLine(origin+new Vector2(bar,-5),origin+new Vector2(bar,5),Colors.White,2);DrawString(font,origin-new Vector2(0,13),$"{length} 格",fontSize:15,modulate:Colors.White);
        DrawRect(new Rect2(Size.X-174,8,166,29),new Color(0,0,0,.8f));DrawString(font,new(Size.X-166,29),IsMapReady?"仅显示已探索区域":"绘制已探索地形…",fontSize:15,modulate:Colors.White);
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("8aa38a"),false,2);
    }
}

