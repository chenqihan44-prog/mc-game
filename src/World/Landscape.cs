using System;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public static class Landscape
{
    public static readonly string[] Names=["森林丘陵","草甸湖岸","赤沙高地","针叶雪原","岩脊谷地","沼泽湿地"];
    public static int StartProfile(int seed)=>(int)(RandomField.Hash(seed,0,0,0,201)%6);
    public static int Profile(int seed,int x,int z)
    {
        double distance=Math.Sqrt((double)x*x+(double)z*z);if(distance<180)return StartProfile(seed);
        // Smooth regional noise varies climate; the seed selects the nearby landscape.
        double temperature=RandomField.Noise(seed,x,z,260,202),moisture=RandomField.Noise(seed,x,z,190,203);
        int region=temperature<.29?3:temperature>.72?2:moisture>.69?5:moisture<.32?4:moisture>.51?0:1;
        if(distance<320&&RandomField.Noise(seed,x,z,100,208)>(distance-180)/140)return StartProfile(seed);return region;
    }
    public static int Height(int seed,int x,int z)
    {
        double broad=RandomField.Noise(seed,x,z,110,204),fine=RandomField.Noise(seed,x,z,25,205);
        double ridge=1-Math.Abs(RandomField.Noise(seed,x,z,65,206)*2-1);
        int ax=Coordinates.FloorDiv(x,48)*48,az=Coordinates.FloorDiv(z,48)*48;double u=(x-ax)/48.0,v=(z-az)/48.0;u=u*u*(3-2*u);v=v*v*(3-2*v);
        double a=HeightFor(Profile(seed,ax,az),broad,fine,ridge,x,z,seed),b=HeightFor(Profile(seed,ax+48,az),broad,fine,ridge,x,z,seed),c=HeightFor(Profile(seed,ax,az+48),broad,fine,ridge,x,z,seed),d=HeightFor(Profile(seed,ax+48,az+48),broad,fine,ridge,x,z,seed);
        double height=(a+(b-a)*u)*(1-v)+(c+(d-c)*u)*v;
        return Math.Clamp((int)Math.Round(height),20,113);
    }
    private static double HeightFor(int profile,double broad,double fine,double ridge,int x,int z,int seed)=>profile switch{
            0=>34+broad*24+(fine-.5)*10,
            1=>24+broad*24+(fine-.5)*4,
            2=>32+broad*27+(fine-.5)*10+Math.Sin(x*.10+z*.025+seed%31)*3,
            3=>39+broad*38+ridge*14+(fine-.5)*9,
            4=>32+broad*30+ridge*35+(fine-.5)*10,
            _=>26+broad*14+(fine-.5)*5
        };
    public static ushort Above(Terrain terrain,Catalog catalog,Cell p,int ground)
    {
        int seed=terrain.Seed,profile=Profile(seed,p.X,p.Z);
        int ax=Coordinates.FloorDiv(p.X,8)*8,az=Coordinates.FloorDiv(p.Z,8)*8;
        for(int tx=ax-8;tx<=ax+8;tx+=8)for(int tz=az-8;tz<=az+8;tz+=8){
            uint hash=RandomField.Hash(seed,tx,0,tz,209);int cx=tx+2+(int)(hash%3),cz=tz+2+(int)((hash>>4)%3);
            if(cx*cx+cz*cz<14*14)continue;
            int type=Profile(seed,cx,cz),density=type switch{0=>3,1=>8,2=>11,3=>4,4=>8,_=>4};if(hash%(uint)density!=0)continue;
            int ruinX=Coordinates.FloorDiv(cx,96)*96+32,ruinZ=Coordinates.FloorDiv(cz,96)*96+24;
            if(Math.Abs(cx-ruinX)<9&&Math.Abs(cz-ruinZ)<9)continue;
            bool protectedSite=false;foreach(var tower in terrain.TowerLocations)if(Math.Abs(cx-tower.X)<13&&Math.Abs(cz-tower.Z)<13){protectedSite=true;break;}if(protectedSite)continue;
            if(terrain.Adventure is {} layout&&layout.Nearby(new(cx,41,cz)) is {} site&&layout.Inside(new(cx,41,cz),site.Id,6))continue;
            int h=terrain.Surface(cx,cz);if(h<=Terrain.SeaLevel)continue;int dx=p.X-cx,dz=p.Z-cz,dy=p.Y-h;
            if(dy<1||dy>12)continue;
            bool birch=type==0&&(hash>>8)%2==0,pine=type==3||type==4,acacia=type==2;
            int trunk=pine?7+(int)((hash>>11)%3):birch?5+(int)((hash>>11)%2):acacia?4:5;
            string log=pine?"pine_log":birch?"birch_log":acacia?"acacia_log":"wood",leaves=pine?"pine_leaves":birch?"birch_leaves":acacia?"acacia_leaves":"leaves";
            if(dx==0&&dz==0&&dy<=trunk)return catalog.Number(log);
            int width=pine?Math.Max(0,3-(dy-(trunk-3))):acacia?3:2;
            bool crown=pine?dy>=trunk-3&&dy<=trunk+2&&Math.Abs(dx)<=width&&Math.Abs(dz)<=width&&Math.Abs(dx)+Math.Abs(dz)<=width+1:acacia?dy>=trunk&&dy<=trunk+1&&Math.Abs(dx)<=3&&Math.Abs(dz)<=2:dy>=trunk-1&&dy<=trunk+1&&Math.Abs(dx)<=2&&Math.Abs(dz)<=2&&Math.Abs(dx)+Math.Abs(dz)<4;
            if(crown)return catalog.Number(leaves);
        }
        if(p.Y!=ground+1||Math.Abs(p.X)<(terrain.GeneratorVersion>=5?5:10)&&Math.Abs(p.Z)<(terrain.GeneratorVersion>=5?5:10))return 0;
        if(terrain.GeneratorVersion>=5)return SparsePlant(terrain,catalog,p,ground,profile);
        uint n=RandomField.Hash(seed,p.X,0,p.Z,210);bool shore=false;for(int i=-2;i<=2;i+=4)if(terrain.Surface(p.X+i,p.Z)<Terrain.SeaLevel||terrain.Surface(p.X,p.Z+i)<Terrain.SeaLevel)shore=true;
        string? plant=profile switch{
            0=>n%17==0?(n%2==0?"brown_mushroom":"red_mushroom"):n%7==0?"fern":n%11==0?"blue_flower":n%5==0?"tall_grass":null,
            1=>shore&&n%3==0?"reed":n%13==0?"red_flower":n%11==0?"blue_flower":n%4==0?"tall_grass":null,
            2=>n%89==0?"cactus":n%12==0?"dry_grass":null,
            3=>n%41==0?"berry_bush":n%37==0?"brown_mushroom":null,
            4=>n%19==0?"fern":n%17==0?"dry_grass":n%37==0?"blue_flower":null,
            _=>shore&&n%2==0?"reed":n%7==0?"fern":n%23==0?"brown_mushroom":n%15==0?"red_mushroom":n%8==0?"tall_grass":null
        };return plant==null?(ushort)0:catalog.Number(plant);
    }
    private static ushort SparsePlant(Terrain terrain,Catalog catalog,Cell p,int ground,int profile)
    {
        uint rank=RandomField.Hash(terrain.Seed,p.X,0,p.Z,210)%10000;if(rank>=1500)return 0;
        double density=profile switch{0=>.09,1=>.07,2=>.025,3=>.012,4=>.035,_=>.12};
        double patch=.35+RandomField.Noise(terrain.Seed,p.X,p.Z,28,211)*1.15;if(rank>=density*patch*10000)return 0;
        for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if((dx!=0||dz!=0)&&RandomField.Hash(terrain.Seed,p.X+dx,0,p.Z+dz,210)%10000<rank)return 0;
        bool shore=false;foreach(var d in new[]{new Cell(1,0,0),new(-1,0,0),new(0,0,1),new(0,0,-1)}){
            if(Math.Abs(terrain.Surface(p.X+d.X,p.Z+d.Z)-ground)>2)return 0;
            if(terrain.Surface(p.X+d.X*2,p.Z+d.Z*2)<Terrain.SeaLevel)shore=true;
        }
        uint kind=RandomField.Hash(terrain.Seed,p.X,0,p.Z,213)%100;
        string plant=profile switch{
            0=>kind<35?"fern":kind<65?"tall_grass":kind<75?"brown_mushroom":kind<85?"red_mushroom":kind<95?"blue_flower":"berry_bush",
            1=>shore&&kind<35?"reed":kind<70?"tall_grass":kind<85?"red_flower":"blue_flower",
            2=>kind<95?"dry_grass":"cactus",
            3=>kind<70?"berry_bush":kind<90?"brown_mushroom":"dry_grass",
            4=>kind<55?"dry_grass":kind<85?"fern":"blue_flower",
            _=>shore&&kind<60?"reed":kind<40?"fern":kind<65?"tall_grass":kind<75?"brown_mushroom":kind<85?"red_mushroom":kind<95?"blue_flower":"red_flower"
        };return catalog.Number(plant);
    }
}
