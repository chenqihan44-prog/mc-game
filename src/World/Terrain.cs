using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Items;

namespace EmberLighthouse.World;

public sealed class Terrain(int seed,Catalog catalog,int generatorVersion=Terrain.Version)
{
    public const int Version=7, SeaLevel=34;
    public int Seed {get;}=seed;
    public int GeneratorVersion {get;}=Supports(generatorVersion)?generatorVersion:throw new ArgumentOutOfRangeException(nameof(generatorVersion));
    public static bool Supports(int version)=>version is >=1 and <=Version;
    public AdventureLayout? Adventure {get;}=generatorVersion>=3?new(seed):null;
    public Cell[] TowerLocations=>Adventure?.Towers??Towers;
    private readonly ConcurrentDictionary<(int,int),int> heights=new();
    private readonly ConcurrentDictionary<(int,int),int> biomes=new();
    private readonly Lazy<System.Collections.Generic.Dictionary<(int X,int Z),string>> supplies=new(()=>StarterSupplies.Generate(seed,catalog));
    public System.Collections.Generic.IReadOnlyDictionary<(int X,int Z),string> StarterResources=>supplies.Value;
    public static readonly Cell[] Towers=[new(64,0,48),new(-120,0,90),new(160,0,-144)];
    public int Biome(int x,int z)=>biomes.GetOrAdd((x,z),p=>CalculateBiome(p.Item1,p.Item2));
    private int CalculateBiome(int x,int z)
    {
        if(Adventure is {} adventure){var p=new Cell(x,40,z);if(adventure.Nearby(p) is {} site&&Math.Abs(x-site.Center.X)<site.Radius+8&&Math.Abs(z-site.Center.Z)<site.Radius+8){if(site.Id=="forge")return 2;if(site.Id=="snow")return 3;}}
        if(GeneratorVersion>=4)return Landscape.Profile(Seed,x,z) switch{2=>2,3=>3,0 or 4=>0,_=>1};
        double climate=RandomField.Noise(Seed,x,z,170,1)*4;
        if(GeneratorVersion==1)return Math.Abs(x)<48&&Math.Abs(z)<48?1:(int)climate%4;
        // The safe woodland blends into seeded climate along an uneven, rounded edge.
        double radius=56+(RandomField.Noise(Seed,x,z,28,31)-.5)*24;
        double blend=Math.Clamp((Math.Sqrt((double)x*x+(double)z*z)-18)/(radius-18),0,1);blend=blend*blend*(3-2*blend);
        return Math.Clamp((int)(1.5+(climate-1.5)*blend),0,3);
    }
    public int Surface(int x,int z)=>heights.GetOrAdd((x,z),p=>CalculateSurface(p.Item1,p.Item2));
    public void TrimCaches(){if(heights.Count>150000){heights.Clear();biomes.Clear();}}
    private int CalculateSurface(int x,int z)
    {
        double broad=RandomField.Noise(Seed,x,z,96,2),detail=RandomField.Noise(Seed,x,z,24,3);
        int height=30+(int)(broad*14+detail*6)+(Biome(x,z)==3?(int)(broad*16):0);
        if(GeneratorVersion>=4){double distance=Math.Sqrt((double)x*x+(double)z*z);double blend=Math.Clamp((distance-9)/29,0,1);blend=blend*blend*(3-2*blend);height=36+(int)Math.Round((Landscape.Height(Seed,x,z)-36)*blend);}
        else if(GeneratorVersion==1){
            if(Math.Abs(x)<14&&Math.Abs(z)<14)height=36;
            else if(Math.Max(Math.Abs(x),Math.Abs(z))<32){double blend=(Math.Max(Math.Abs(x),Math.Abs(z))-14)/18.0;height=36+(int)((height-36)*blend);}
        }else{
            double distance=Math.Sqrt((double)x*x+(double)z*z),edge=36+(RandomField.Noise(Seed,x,z,24,32)-.5)*12;
            double blend=Math.Clamp((distance-14)/(edge-14),0,1);blend=blend*blend*(3-2*blend);height=36+(int)((height-36)*blend);
        }
        if(Math.Abs(x-32)<=6&&Math.Abs(z-24)<=6)height=37;
        if(Adventure?.Nearby(new(x,40,z)) is {} site&&Math.Abs(x-site.Center.X)<=site.Radius+3&&z>=site.Center.Z-site.Radius-3&&z<=site.Center.Z+site.Radius+43)return 40;
        foreach(var t in TowerLocations)if(Math.Abs(x-t.X)<10&&Math.Abs(z-t.Z)<10)return SurfaceBase(t.X,t.Z);
        return height;
    }
    private int SurfaceBase(int x,int z)=>36+(int)(RandomField.Noise(Seed,x,z,96,2)*12);
    public bool IsLooseWood(Cell p)
    {
        if(GeneratorVersion>=5)return p.Y==Surface(p.X,p.Z)+1&&Math.Abs(p.X)<=20&&Math.Abs(p.Z)<=20&&StarterResources.GetValueOrDefault((p.X,p.Z))=="wood";
        if(Math.Abs(p.X)>=12||Math.Abs(p.Z)>=12)return false;
        if(p.Y!=Surface(p.X,p.Z)+1)return false;
        if(p.Y==37&&p.Z==4&&p.X>=-7&&p.X<=-2)return true;
        return Math.Abs(p.X)<12&&Math.Abs(p.Z)<12&&RandomField.Hash(Seed,p.X,0,p.Z,8)%35==0;
    }
    public ushort Get(Cell p)
    {
        if(!Coordinates.InWorld(p))return p.Y<0||Math.Abs(p.X)>=2048||Math.Abs(p.Z)>=2048?catalog.Number("bedrock"):(ushort)0;
        if(Adventure?.BlockAt(p) is {} block)return block=="air"?(ushort)0:catalog.Number(block);
        int h=Surface(p.X,p.Z),b=Biome(p.X,p.Z);
        foreach(var t in TowerLocations){
            int dx=Math.Abs(p.X-t.X),dz=Math.Abs(p.Z-t.Z),ty=Surface(t.X,t.Z);
            if(dx<=4&&dz<=4&&p.Y>=ty&&p.Y<ty+10){
                if(p.Y==ty||p.Y==ty+8)return catalog.Number("ruin_brick");
                if(dx==0&&dz==0&&p.Y==ty+1)return catalog.Number("beacon");
                if((dx==4||dz==4)&&!(p.X==t.X&&dz==4&&p.Y<ty+4))return catalog.Number("ruin_brick");
                return 0;
            }
        }
        // A safe starter cache uses renewable hand-gathered wood, stone and food.
        if(GeneratorVersion>=5){if(p.Y==h+1&&Math.Abs(p.X)<=20&&Math.Abs(p.Z)<=20&&StarterResources.TryGetValue((p.X,p.Z),out string? resource))return catalog.Number(resource);}
        else{
            if(p.Y==37&&p.Z==4&&p.X>=-7&&p.X<=-2)return catalog.Number("wood");
            if(p.Y==37&&p.Z==4&&p.X>=2&&p.X<=7)return catalog.Number("loose_stone");
            if(p.Y==37&&p.Z==-4&&p.X is -4 or 0 or 4)return catalog.Number("berry_bush");
            if(p.Y==h+1&&Math.Abs(p.X)<12&&Math.Abs(p.Z)<12){uint n=RandomField.Hash(Seed,p.X,0,p.Z,8)%35;if(n==0)return catalog.Number("wood");if(n==1)return catalog.Number("loose_stone");if(n==2)return catalog.Number("berry_bush");}
        }
        // Six deterministic ruin patterns are owned by 96-cell regions, so chunk order cannot duplicate them.
        int rx=Coordinates.FloorDiv(p.X,96),rz=Coordinates.FloorDiv(p.Z,96);
        int ruinX=rx*96+32,ruinZ=rz*96+24,dxr=p.X-ruinX,dzr=p.Z-ruinZ,rh=Surface(ruinX,ruinZ),pattern=(int)(RandomField.Hash(Seed,rx,0,rz,21)%6);
        if(rx==0&&rz==0)pattern=0;
        if(Math.Abs(dxr)<=5&&Math.Abs(dzr)<=5&&rh>SeaLevel&&p.Y>=rh&&p.Y<=rh+7){
            int dy=p.Y-rh;
            if(dxr==2&&dzr==2&&dy==1&&(GeneratorVersion<7||NaturalRules.WildChestAllowed(Seed,p.X,p.Z)))return catalog.Number("chest");
            if(pattern==0){if(dy==0)return catalog.Number("plank");if((Math.Abs(dxr)==4||Math.Abs(dzr)==4)&&dy<=2&&!(dxr==0&&dzr==4))return catalog.Number("cobble");}
            if(pattern==1){if((Math.Abs(dxr)==3||Math.Abs(dzr)==3)&&dy<=6&&!(dxr==0&&dzr==3&&dy<4))return catalog.Number("ruin_brick");}
            if(pattern==2){if((Math.Abs(dxr)==4&&Math.Abs(dzr)==4&&dy<=4)||(dy==4&&Math.Abs(dzr)==4))return catalog.Number("wood");if(dy==0)return catalog.Number("gravel");}
            if(pattern==3){if(dy==0)return catalog.Number("basalt");if(dxr==0&&dzr==0&&dy<=3)return catalog.Number(GeneratorVersion>=7?"pillar":"crystal_ore");}
            if(pattern==4){if(dy==0||(dy==5&&Math.Abs(dxr)<=3&&Math.Abs(dzr)<=3))return catalog.Number("ruin_brick");if(Math.Abs(dxr)==3&&Math.Abs(dzr)==3&&dy<=5)return catalog.Number("pillar");}
            if(pattern==5){if(dy==0)return catalog.Number("ruin_brick");if(Math.Abs(dxr)==5&&dy<5&&Math.Abs(dzr)!=0)return catalog.Number("ruin_brick");if(dy==5&&Math.Abs(dxr)==5)return catalog.Number("lantern");}
            if(GeneratorVersion>=4&&dy>0)return 0;
        }
        if(p.Y>h){
            if(GeneratorVersion>=6&&p.Y==h+1&&h>=SeaLevel&&h<=SeaLevel+2&&b!=3&&RandomField.Hash(Seed,p.X,0,p.Z,266)%67==0&&new[]{new Cell(2,0,0),new Cell(-2,0,0),new Cell(0,0,2),new Cell(0,0,-2)}.Any(n=>Surface(p.X+n.X,p.Z+n.Z)<SeaLevel))return catalog.Number("sugar_cane");
            if(p.Y<=SeaLevel)return catalog.Number(b==3&&p.Y==SeaLevel?"ice":"water");
            if(GeneratorVersion>=4)return Landscape.Above(this,catalog,p,h);
            if(b==1||b==0){
                int ax=Coordinates.FloorDiv(p.X,7)*7,az=Coordinates.FloorDiv(p.Z,7)*7;
                for(int tx=ax-7;tx<=ax+7;tx+=7)for(int tz=az-7;tz<=az+7;tz+=7){
                    int cx=tx+2,cz=tz+2,ch=Surface(cx,cz);
                    if(GeneratorVersion==1?Math.Abs(cx)<14&&Math.Abs(cz)<14:cx*cx+cz*cz<14*14)continue;
                    if(RandomField.Hash(Seed,tx,0,tz,9)%5!=0||ch<=SeaLevel)continue;
                    int dx=Math.Abs(p.X-cx),dz=Math.Abs(p.Z-cz),dy=p.Y-ch;
                    if(dx==0&&dz==0&&dy>=1&&dy<=5)return catalog.Number("wood");
                    if(dx<=2&&dz<=2&&dy>=4&&dy<=6&&dx+dz<4)return catalog.Number("leaves");
                }
            }
            if(p.Y==h+1&&RandomField.Hash(Seed,p.X,0,p.Z,10)%101==0)return catalog.Number(b==2?"cactus":"berry_bush");
            return 0;
        }
        if(p.Y==0)return catalog.Number("bedrock");
        // Open cave entrances near spawn and each objective, connected to the ore layer.
        bool entry=(p.X>=18&&p.X<=23&&p.Z>=-4&&p.Z<=4&&p.Y>h-16);
        double phase=GeneratorVersion>=4?(RandomField.Hash(Seed,0,0,0,207)%1000)/91.0:0;
        bool cave=p.Y>5&&p.Y<h-3&&(entry||(Math.Sin(p.X*.13+phase)+Math.Cos(p.Z*.11-phase)+Math.Sin(p.Y*.31+phase)>2.12));
        if(cave)return 0;
        if(p.Y==h){if(GeneratorVersion>=7&&h<SeaLevel)return catalog.Number(NaturalRules.Seabed(Seed,p.X,p.Z,SeaLevel-h));uint patch=RandomField.Hash(Seed,p.X/3,0,p.Z/3,12)%30;return catalog.Number(b==2?"sand":b==3?"snow":patch==0?"clay":patch==1?"gravel":b==1&&patch==2?"moss":"grass");}
        if(p.Y>h-4)return catalog.Number(b==2?"sand":"dirt");
        uint ore=RandomField.Hash(Seed,p.X/2,p.Y/2,p.Z/2,11)%100;
        if(GeneratorVersion>=6&&p.Y<28&&p.Y<h-5&&ore>=24&&ore<27)return catalog.Number("gold_ore");
        if(GeneratorVersion>=3&&p.Y<h-4&&ore>=17&&ore<24)return catalog.Number("copper_vein");
        if(p.Y<16&&ore<5)return catalog.Number("crystal_ore");
        if(p.Y<h-5&&ore<11)return catalog.Number("iron_ore");
        if(ore<17)return catalog.Number("coal_ore");
        if(p.Y<8)return catalog.Number("basalt");
        return catalog.Number("stone");
    }
    public ushort[] Generate(Cell chunk)
    {
        var a=new ushort[8192];var origin=Coordinates.Origin(chunk);
        for(int y=0;y<32;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)a[x+16*(z+16*y)]=Get(origin+new Cell(x,y,z));
        return a;
    }
}
