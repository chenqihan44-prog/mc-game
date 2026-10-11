using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.World;

namespace EmberLighthouse.Creatures;

public sealed class CreaturePopulationState
{
    public bool Initialized {get;set;}
    public double NextSpawnTime {get;set;}
    public int Sequence {get;set;}
}

public sealed record PopulationChanges(List<CreatureState> Added,List<CreatureState> Removed);

public static class CreaturePopulation
{
    public const int Limit=48;
    private static readonly Dictionary<string,int> kindLimits=new(){["deer"]=10,["boar"]=8,["skitter"]=12,["husk"]=10,["sentinel"]=8,["guardian"]=1};
    public static void Initialize(WorldState world,IReadOnlyList<CreatureDef> definitions)
    {
        var state=world.State;
        if(state.GuardianDefeated)state.Creatures.RemoveAll(c=>c.Kind=="guardian");
        if(!state.Population.Initialized){
            // Existing saves keep their surviving creatures. Only a fresh empty
            // population gets starter wildlife; defeated guardians stay defeated.
            if(state.Creatures.Count==0){
                for(int attempt=0;attempt<96&&state.Creatures.Count<6;attempt++){
                    int x=16+attempt%8*3,z=-18+attempt/8*3;
                    if(attempt%2==1)x=-x;
                    var p=new Cell(x,world.Terrain.Surface(x,z)+1,z);
                    if(!CanStand(world,p)||SafeCamp(world,p))continue;
                    Add(state,definitions,attempt%3==0?"boar":"deer",p);
                }
            }
            state.Population.Initialized=true;state.Population.NextSpawnTime=state.Time+12;
        }
        EnsureGuardian(world,definitions);
    }
    private static CreatureState? EnsureGuardian(WorldState world,IReadOnlyList<CreatureDef> definitions)
    {
        var state=world.State;
        if(state.GuardianDefeated||state.Creatures.Any(c=>c.Kind=="guardian")||state.Creatures.Count>=Limit)return null;
        var tower=Terrain.Towers[2];var p=new Cell(tower.X+2,world.Terrain.Surface(tower.X,tower.Z)+1,tower.Z+1);
        return CanStand(world,p,3)?Add(state,definitions,"guardian",p):null;
    }
    public static PopulationChanges Tick(WorldState world,IReadOnlyList<CreatureDef> definitions,Cell player,Func<Cell,bool> ready)
    {
        var state=world.State;var added=new List<CreatureState>();var removed=new List<CreatureState>();
        if(state.Time<state.Population.NextSpawnTime)return new(added,removed);
        state.Population.NextSpawnTime=state.Time+12;
        foreach(var c in state.Creatures.ToArray()){
            double dx=c.Position[0]-player.X,dz=c.Position[2]-player.Z;
            if(c.Kind!="guardian"&&(c.Health<=0||dx*dx+dz*dz>120*120)){state.Creatures.Remove(c);removed.Add(c);}
        }
        var guardian=EnsureGuardian(world,definitions);if(guardian!=null)added.Add(guardian);
        bool underground=player.Y<world.Terrain.Surface(player.X,player.Z)-4;
        for(int attempt=0;attempt<16&&added.Count<2&&state.Creatures.Count<Limit;attempt++){
            int sequence=state.Population.Sequence;state.Population.Sequence=unchecked(sequence+1)&int.MaxValue;
            uint hash=RandomField.Hash(state.Seed,player.X/16,sequence,player.Z/16,37);
            double angle=(hash%4096)/4096.0*Math.Tau;int radius=18+(int)(hash>>12)%25;
            int x=player.X+(int)Math.Round(Math.Cos(angle)*radius),z=player.Z+(int)Math.Round(Math.Sin(angle)*radius);
            if(x<=-2047||x>=2047||z<=-2047||z>=2047)continue;
            int y=underground?player.Y+(int)(hash>>20)%5-2:world.Terrain.Surface(x,z)+1;
            var p=new Cell(x,y,z);if(!ready(p)||!CanStand(world,p)||SafeCamp(world,p))continue;
            string? kind=KindAt(world,p,hash);if(kind==null||state.Creatures.Count(c=>c.Kind==kind)>=kindLimits[kind])continue;
            if(state.Creatures.Any(c=>DistanceSquared(c.Position,p)<8*8))continue;
            added.Add(Add(state,definitions,kind,p));
        }
        return new(added,removed);
    }
    public static string? KindAt(WorldState world,Cell p,uint variation)
    {
        if(p.Y<world.Terrain.Surface(p.X,p.Z)-4)return "skitter";
        int rx=Coordinates.FloorDiv(p.X,96)*96+32,rz=Coordinates.FloorDiv(p.Z,96)*96+24;
        bool ruins=Math.Abs(p.X-rx)<=7&&Math.Abs(p.Z-rz)<=7&&world.Terrain.Surface(rx,rz)>Terrain.SeaLevel;
        if(ruins||Terrain.Towers.Any(t=>Math.Abs(t.X-p.X)<=7&&Math.Abs(t.Z-p.Z)<=7))return "sentinel";
        if(CreatureRules.Night(world.State.Time)&&variation%3==0)return "husk";
        return world.Terrain.Biome(p.X,p.Z) switch{0=>"deer",1=>variation%2==0?"deer":"boar",3=>"boar",_=>null};
    }
    public static bool CanStand(WorldState world,Cell p,int height=2)
    {
        if(!Coordinates.InWorld(p+new Cell(0,height-1,0))||!Coordinates.InWorld(p+new Cell(0,-1,0))||!world.Solid(p+new Cell(0,-1,0)))return false;
        for(int i=0;i<height;i++)if(world.Get(p+new Cell(0,i,0))!=0)return false;
        return true;
    }
    public static bool SafeCamp(WorldState world,Cell p)=>world.State.Entities.Any(e=>e.Value.Kind=="campfire"&&DistanceSquared(Cell.Parse(e.Key),p)<7*7);
    private static double DistanceSquared(double[] position,Cell p)=>Math.Pow(position[0]-p.X,2)+Math.Pow(position[1]-p.Y,2)+Math.Pow(position[2]-p.Z,2);
    private static double DistanceSquared(Cell a,Cell b)=>Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2);
    private static CreatureState Add(WorldSnapshot state,IReadOnlyList<CreatureDef> definitions,string kind,Cell p)
    {
        var creature=new CreatureState{Kind=kind,Health=definitions.First(d=>d.Id==kind).Health,Position=[p.X+.5,p.Y+.02,p.Z+.5]};state.Creatures.Add(creature);return creature;
    }
}
