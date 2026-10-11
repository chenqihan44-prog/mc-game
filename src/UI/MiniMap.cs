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
    private const int Pixels=96;
    private readonly Rect2 mapRect=new(6,22,180,180);
    public void Initialize(WorldState value){world=value;texture?.Dispose();texture=null;timer=0;}
    public void UpdatePlayer(Vector3 value,float facing){position=value;yaw=facing;QueueRedraw();}
    public override void _Ready(){Position=new(12,12);Size=new(192,246);MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Nearest;}
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
        var edits=world.CaptureRegion(x,z,48);
        pending=Task.Run(()=>{
            var bytes=new byte[Pixels*Pixels*4];var tops=new Dictionary<(int,int),int>();
            foreach(var p in edits)if(p.Value!=0){var key=(p.Key.X,p.Key.Z);tops[key]=Math.Max(tops.GetValueOrDefault(key),p.Key.Y);}
            for(int j=0;j<Pixels;j++)for(int i=0;i<Pixels;i++){
                int wx=x+i-48,wz=z+j-48,k=(i+j*Pixels)*4;
                if(wx<-Coordinates.Limit||wx>=Coordinates.Limit||wz<-Coordinates.Limit||wz>=Coordinates.Limit){bytes[k]=60;bytes[k+1]=48;bytes[k+2]=48;bytes[k+3]=255;continue;}
                int h=terrain.Surface(wx,wz),top=Math.Max(Math.Max(h+10,Terrain.SeaLevel),tops.GetValueOrDefault((wx,wz)));ushort block=0;
                for(int y=Math.Min(top,Coordinates.WorldHeight-1);y>=0;y--){var cell=new Cell(wx,y,wz);block=edits.TryGetValue(cell,out var n)?n:terrain.Get(cell);if(block!=0)break;}
                string color=catalog.Block(block)?.Color.TrimStart('#')??"20352e";
                float shade=Math.Clamp(1+(h-terrain.Surface(wx-1,wz-1))*.035f,.7f,1.1f);
                for(int c=0;c<3;c++)bytes[k+c]=(byte)Math.Clamp(Convert.ToInt32(color.Substring(c*2,2),16)*shade,0,255);
                bytes[k+3]=255;
            }return bytes;
        });
    }
    public override void _ExitTree(){texture?.Dispose();texture=null;world=null;}
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("252525"));DrawRect(new Rect2(2,2,188,242),new Color("c6c6c6"));
        DrawRect(mapRect,new Color("334c36"));if(texture!=null)DrawTextureRect(texture,mapRect,false);
        var font=ThemeDB.FallbackFont;DrawString(font,new(8,17),"N ↑",fontSize:14,modulate:new Color("373737"));
        if(world==null)return;
        Vector2 Point(double x,double z)=>mapRect.Position+new Vector2((float)(x-centerX+48),(float)(z-centerZ+48))*mapRect.Size/Pixels;
        void Marker(double x,double z,Color color,int radius){var p=Point(x,z);if(mapRect.Grow(-radius).HasPoint(p)){DrawCircle(p,radius+1,Colors.Black);DrawCircle(p,radius,color);}}
        for(int i=0;i<Terrain.Towers.Length;i++){var t=Terrain.Towers[i];Marker(t.X,t.Z,world.State.Beacons.Contains(i)?new("7ff7d3"):new("f4c16a"),4);}
        foreach(var g in world.State.Graves)Marker(g.Position[0],g.Position[2],new("d3a165"),3);
        foreach(var m in world.State.Markers)Marker(m[0],m[1],Colors.White,3);
        var player=Point(position.X,position.Z);var heading=new Vector2(-MathF.Sin(yaw),-MathF.Cos(yaw));var side=new Vector2(-heading.Y,heading.X);
        if(mapRect.Grow(-7).HasPoint(player)){DrawCircle(player,7,Colors.Black);DrawColoredPolygon([player+heading*7,player-heading*4+side*4,player-heading*4-side*4],Colors.White);}
        DrawString(font,new(8,219),$"X {position.X:0}   Z {position.Z:0}",fontSize:14,modulate:new Color("373737"));
        DrawString(font,new(8,238),"天气 · "+WeatherText,fontSize:14,modulate:new Color("373737"));
    }
}
