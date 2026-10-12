using System;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public static class NaturalRules
{
    public static bool WildChestAllowed(int seed,int x,int z)
    {
        // One in six ruin regions; the spawn neighbourhood has no compulsory cache.
        if((long)x*x+(long)z*z<80*80)return false;
        return RandomField.Hash(seed,Coordinates.FloorDiv(x,96),0,Coordinates.FloorDiv(z,96),331)%6==0;
    }
    public static string Seabed(int seed,int x,int z,int depth)=>depth<=3?"sand":RandomField.Hash(seed,x/3,0,z/3,332)%5==0?"clay":"gravel";
    public static ushort Correct(Terrain terrain,Catalog catalog,Cell p,ushort original,bool ownedEntity=false)
    {
        if(original==0||ownedEntity)return original;string id=catalog.Id(original);if(id is not("grass" or "snow" or "moss" or "crystal_ore" or "chest"))return original;int surface=terrain.Surface(p.X,p.Z);
        if(p.Y==surface&&surface<Terrain.SeaLevel&&id is "grass" or "snow" or "moss")return catalog.Number(Seabed(terrain.Seed,p.X,p.Z,Terrain.SeaLevel-surface));
        if(id=="crystal_ore"&&p.Y>=surface-3)return catalog.Number("basalt");
        if(id=="chest"&&!WildChestAllowed(terrain.Seed,p.X,p.Z)){
            var site=terrain.Adventure?.Nearby(p);if(site==null||!terrain.Adventure!.Inside(p,site.Id))return 0;
        }
        return original;
    }
}

public static class TorchMount
{
    public static Cell Support(Cell p,int attachment)=>p+(attachment switch{1=>new Cell(-1,0,0),2=>new(1,0,0),3=>new(0,0,-1),4=>new(0,0,1),_=>new(0,-1,0)});
    public static bool Sturdy(WorldState world,Cell p)=>world.Solid(p)&&world.Catalog.Block(world.Get(p))?.Shape=="cube";
    public static int Find(WorldState world,Cell p,Cell? hint)
    {
        for(int i=0;i<5;i++)if(hint==Support(p,i)&&Sturdy(world,Support(p,i)))return i;
        for(int i=0;i<5;i++)if(Sturdy(world,Support(p,i)))return i;return -1;
    }
    public static System.Numerics.Vector3 Flame(Cell p,int attachment)=>new(p.X+(attachment==1?.28f:attachment==2?.72f:.5f),p.Y+.7f,p.Z+(attachment==3?.28f:attachment==4?.72f:.5f));
}
