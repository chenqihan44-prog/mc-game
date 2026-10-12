using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.UI;

public partial class MiniMap : Control
{
    private WorldState? world;
    private Task<byte[]>? pending;
    private ImageTexture? texture;
    private int centerX,centerZ,nextX,nextZ;
    private long pendingSession;
    private double timer;
    private Vector3 position;
    private float yaw;
    public string WeatherText {get;set;}="晴";
    public string ClockText {get;set;}="";
    private const int Pixels=96;
    private readonly Rect2 mapRect=new(6,22,180,180);
    public void Initialize(WorldState value){world=value;texture?.Dispose();texture=null;timer=0;}
    public void UpdatePlayer(Vector3 value,float facing){position=value;yaw=facing;QueueRedraw();}
    public override void _Ready(){Position=new(12,12);Size=new(192,268);MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Nearest;}
    public override void _Process(double delta)
    {
        if(world==null||!IsVisibleInTree())return;
        timer-=delta;
        if(pending?.IsCompleted==true){
            if(pending.IsCompletedSuccessfully&&pendingSession==world.Session){
                using var bitmap=Image.CreateFromData(Pixels,Pixels,false,Image.Format.Rgba8,pending.Result);
                // Same size and format: reuse the GPU texture and managed peer.
                if(texture==null)texture=ImageTexture.CreateFromImage(bitmap);else texture.Update(bitmap);
                centerX=nextX;centerZ=nextZ;QueueRedraw();
            }else if(pending.IsFaulted)GD.PushWarning("小地图更新失败："+pending.Exception?.GetBaseException().Message);
            pending=null;
        }
        if(pending!=null||timer>0)return;
        timer=.75;nextX=(int)Math.Floor(position.X);nextZ=(int)Math.Floor(position.Z);pendingSession=world.Session;
        int x=nextX,z=nextZ;var terrain=world.Terrain;var catalog=world.Catalog;
        var edits=world.CaptureRegion(x,z,48);var explored=new HashSet<int>(world.State.ExploredMap);
        pending=Task.Run(()=>MapRaster.Render(terrain,catalog,edits,x-48,z-48,1,Pixels,explored:explored));
    }
    public override void _ExitTree(){texture?.Dispose();texture=null;world=null;}
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("252525"));DrawRect(new Rect2(2,2,188,264),new Color("c6c6c6"));
        DrawRect(mapRect,new Color("1e2428"));if(texture!=null)DrawTextureRect(texture,mapRect,false);
        var font=ThemeDB.FallbackFont;DrawString(font,new(8,17),"N ↑",fontSize:14,modulate:new Color("373737"));
        if(world==null)return;
        Vector2 Point(double x,double z)=>mapRect.Position+new Vector2((float)(x-centerX+48),(float)(z-centerZ+48))*mapRect.Size/Pixels;
        void Marker(double x,double z,Color color,int radius){if(!world.MapKnown((int)Math.Floor(x),(int)Math.Floor(z)))return;var p=Point(x,z);if(mapRect.Grow(-radius).HasPoint(p)){DrawCircle(p,radius+1,Colors.Black);DrawCircle(p,radius,color);}}
        for(int i=0;i<world.Terrain.TowerLocations.Length;i++){var t=world.Terrain.TowerLocations[i];Marker(t.X,t.Z,world.State.Beacons.Contains(i)?new("7ff7d3"):new("f4c16a"),4);}
        foreach(var g in world.State.Graves.Where(g=>g.Death||g.LegacyContainer))Marker(g.Position[0],g.Position[2],new("d3a165"),3);
        foreach(var m in world.State.Markers)Marker(m[0],m[1],Colors.White,3);
        var player=Point(position.X,position.Z);var heading=new Vector2(-MathF.Sin(yaw),-MathF.Cos(yaw));var side=new Vector2(-heading.Y,heading.X);
        if(mapRect.Grow(-7).HasPoint(player)){DrawCircle(player,7,Colors.Black);DrawColoredPolygon([player+heading*7,player-heading*4+side*4,player-heading*4-side*4],Colors.White);}
        DrawString(font,new(8,219),$"X {position.X:0}   Z {position.Z:0}",fontSize:14,modulate:new Color("373737"));
        DrawString(font,new(8,238),"天气 · "+WeatherText,fontSize:14,modulate:new Color("373737"));
        DrawString(font,new(8,258),ClockText,fontSize:13,modulate:new Color("202020"));
    }
}

