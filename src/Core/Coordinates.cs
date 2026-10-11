using System;
using System.Globalization;

namespace EmberLighthouse.Core;

public readonly record struct Cell(int X, int Y, int Z)
{
    public static Cell operator +(Cell a, Cell b) => new(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
    public static Cell operator -(Cell a, Cell b) => new(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
    public override string ToString() => $"{X},{Y},{Z}";
    public static Cell Parse(string text) { var p=text.Split(',');if(p.Length!=3||!int.TryParse(p[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out int x)||!int.TryParse(p[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int y)||!int.TryParse(p[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out int z))throw new FormatException("方块坐标必须包含三个整数");return new(x,y,z); }
}

public static class Coordinates
{
    public const int Width=16, Height=32, WorldHeight=192, Limit=2048;
    public static readonly Cell[] Neighbors=[new(1,0,0),new(-1,0,0),new(0,1,0),new(0,-1,0),new(0,0,1),new(0,0,-1)];
    public static int FloorDiv(int a,int b) => (int)Math.Floor((double)a/b);
    public static int Mod(int a,int b) => ((a%b)+b)%b;
    public static Cell Chunk(Cell p) => new(FloorDiv(p.X,Width),FloorDiv(p.Y,Height),FloorDiv(p.Z,Width));
    public static int Index(Cell p) => Mod(p.X,16)+16*(Mod(p.Z,16)+16*Mod(p.Y,32));
    public static Cell Origin(Cell chunk) => new(chunk.X*16,chunk.Y*32,chunk.Z*16);
    public static bool InWorld(Cell p) => p.X>=-Limit && p.X<Limit && p.Z>=-Limit && p.Z<Limit && p.Y>=0 && p.Y<WorldHeight;
}

public static class RandomField
{
    public static uint Hash(int seed,int x,int y,int z,int stage=0)
    {
        unchecked {
            uint h=(uint)seed ^ 0x9e3779b9u;
            h=Mix(h,x);h=Mix(h,y);h=Mix(h,z);h=Mix(h,stage);
            return h;
        }
    }
    private static uint Mix(uint h,int v){unchecked{h^=(uint)v+0x9e3779b9u+(h<<6)+(h>>2);h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);}}
    public static double Unit(int seed,int x,int y,int z,int stage=0) => Hash(seed,x,y,z,stage)/(double)uint.MaxValue;
    public static double Noise(int seed,double x,double z,int scale,int stage)
    {
        int ix=(int)Math.Floor(x/scale),iz=(int)Math.Floor(z/scale);
        double fx=x/scale-ix,fz=z/scale-iz;
        fx=fx*fx*(3-2*fx); fz=fz*fz*(3-2*fz);
        double a=Unit(seed,ix,0,iz,stage),b=Unit(seed,ix+1,0,iz,stage),c=Unit(seed,ix,0,iz+1,stage),d=Unit(seed,ix+1,0,iz+1,stage);
        return (a+(b-a)*fx)*(1-fz)+(c+(d-c)*fx)*fz;
    }
}
