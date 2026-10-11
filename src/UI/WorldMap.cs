using System;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.UI;
public partial class WorldMap : Control
{
    public WorldState World {get;set;}=null!;
    public Vector3 PlayerPosition {get;set;}
    public override void _Ready(){CustomMinimumSize=new(600,260);MouseFilter=MouseFilterEnum.Ignore;QueueRedraw();}
    public override void _Draw()
    {
        const int cells=60;float scale=Size.X/cells,yscale=Size.Y/cells;Color[] biome=[new("728e5a"),new("466f53"),new("bca26c"),new("b8cbd0")];
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int wx=x*8-240,wz=z*8-240;int h=World.Terrain.Surface(wx,wz);var color=h<Terrain.SeaLevel?new Color("497888"):biome[World.Terrain.Biome(wx,wz)];DrawRect(new Rect2(x*scale,z*yscale,scale+1,yscale+1),color);}
        Vector2 Point(double x,double z)=>new((float)((x+240)/480*Size.X),(float)((z+240)/480*Size.Y));
        for(int i=0;i<Terrain.Towers.Length;i++){var t=Terrain.Towers[i];DrawCircle(Point(t.X,t.Z),6,World.State.Beacons.Contains(i)?new("7ff7d3"):new("f4c16a"));}
        foreach(var g in World.State.Graves)DrawCircle(Point(g.Position[0],g.Position[2]),4,new("da885b"));
        foreach(var m in World.State.Markers)DrawCircle(Point(m[0],m[1]),3,Colors.White);
        var p=Point(PlayerPosition.X,PlayerPosition.Z);if(p.X>=0&&p.X<=Size.X&&p.Y>=0&&p.Y<=Size.Y){DrawCircle(p,5,Colors.White);DrawCircle(p,2,new("20352e"));}
    }
}
