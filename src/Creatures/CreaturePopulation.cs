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
    static CreaturePopulation(){foreach(string kind in new[]{"cow","pig","sheep","chicken","wolf","cat","horse","fox","armadillo","cod","squid","turtle","zombie","skeleton","creeper","spider"})kindLimits[kind]=kind is "cod" or "chicken"?5:3;}
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
                    string kind=state.GeneratorVersion>=6?KindAt(world,p,(uint)(attempt*17+state.Seed))??"deer":attempt%3==0?"boar":"deer";if(!LifeRules.Aquatic(kind))Add(state,definitions,kind,p);
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
        var tower=world.Terrain.TowerLocations[2];var p=world.Terrain.Adventure is {} adventure?adventure.Site("cave").At(0,1,-8):new Cell(tower.X+2,world.Terrain.Surface(tower.X,tower.Z)+1,tower.Z+1);
        return CanStand(world,p,3)?Add(state,definitions,"guardian",p):null;
    }
    public static PopulationChanges Tick(WorldState world,IReadOnlyList<CreatureDef> definitions,Cell player,Func<Cell,bool> ready)
    {
        var state=world.State;var added=new List<CreatureState>();var removed=new List<CreatureState>();
        if(state.Time<state.Population.NextSpawnTime)return new(added,removed);
        state.Population.NextSpawnTime=state.Time+(CreatureRules.Night(state.SkyTime)?6:12);
        foreach(var c in state.Creatures.ToArray()){
            double dx=c.Position[0]-player.X,dz=c.Position[2]-player.Z;
            if(!CreatureRules.Elite(c.Kind)&&c.Home==""&&(c.Health<=0||!c.Persistent&&!c.Tamed&&dx*dx+dz*dz>120*120)){state.Creatures.Remove(c);removed.Add(c);}
        }
        var guardian=EnsureGuardian(world,definitions);if(guardian!=null)added.Add(guardian);
        if(world.Terrain.Adventure is {} layout){
            foreach(var (site,kind,proof,y) in new[]{("mine","mine_warden","mine",1),("forge","furnace_warden","forge",1),("snow","observatory_warden","snow",17)}){
                var target=layout.Site(site).At(0,y,-16);if(state.Mainline.Proofs.Contains(proof)||state.Creatures.Any(c=>c.Kind==kind)||state.Creatures.Count>=Limit||DistanceSquared(player,target)>65*65||!ready(target)||!CanStand(world,target))continue;added.Add(Add(state,definitions,kind,target));
            }
            var guard=layout.Site("vault").At(16,1,16);if(!state.Mainline.Nodes.Contains("guard")&&!state.Creatures.Any(c=>c.Home=="vault_guard")&&state.Creatures.Count<Limit&&DistanceSquared(player,guard)<50*50&&ready(guard)&&CanStand(world,guard)){var c=Add(state,definitions,"sentinel",guard);c.Home="vault_guard";added.Add(c);}
        }
        bool underground=player.Y<world.Terrain.Surface(player.X,player.Z)-4;
        for(int attempt=0;attempt<16&&added.Count<2&&state.Creatures.Count<Limit;attempt++){
            int sequence=state.Population.Sequence;state.Population.Sequence=unchecked(sequence+1)&int.MaxValue;
            uint hash=RandomField.Hash(state.Seed,player.X/16,sequence,player.Z/16,37);
            double angle=(hash%4096)/4096.0*Math.Tau;int radius=18+(int)(hash>>12)%25;
            int x=player.X+(int)Math.Round(Math.Cos(angle)*radius),z=player.Z+(int)Math.Round(Math.Sin(angle)*radius);
            if(x<=-2047||x>=2047||z<=-2047||z>=2047)continue;
            int y=underground?player.Y+(int)(hash>>20)%5-2:world.Terrain.Surface(x,z)+1;
            var p=new Cell(x,y,z);if(world.Terrain.GeneratorVersion>=4&&world.Terrain.Surface(x,z)<Terrain.SeaLevel-1)p=new(x,Terrain.SeaLevel-1,z);
            string? kind=KindAt(world,p,hash);if(kind==null||state.Creatures.Count(c=>c.Kind==kind)>=kindLimits.GetValueOrDefault(kind,3)||!ready(p))continue;
            if(LifeRules.Aquatic(kind)){if(world.Catalog.Id(world.Get(p))!="water")continue;}else if(!CanStand(world,p)||SafeCamp(world,p)&&definitions.First(d=>d.Id==kind).Hostile)continue;
            if(state.Creatures.Any(c=>DistanceSquared(c.Position,p)<8*8))continue;
            added.Add(Add(state,definitions,kind,p));
        }
        return new(added,removed);
    }
    public static string? KindAt(WorldState world,Cell p,uint variation)
    {
        if(world.State.GeneratorVersion>=4){
            if(world.Catalog.Id(world.Get(p))=="water")return variation%3==0&&world.Terrain.Surface(p.X,p.Z)<Terrain.SeaLevel-3?"squid":"cod";
            if(p.Y<world.Terrain.Surface(p.X,p.Z)-4)return new[]{"skitter","spider","skeleton"}[variation%3];
            if(CreatureRules.Night(world.State.SkyTime)&&variation%4!=3)return new[]{"husk","zombie","skeleton","creeper","spider"}[variation%5];
            int profile=Landscape.Profile(world.State.Seed,p.X,p.Z);if(world.NearWater(p+new Cell(0,-1,0))&&world.Catalog.Id(world.Get(p+new Cell(0,-1,0)))=="sand")return "turtle";
            string[] kinds=profile switch{0=>["deer","pig","chicken","wolf","fox"],1=>["cow","sheep","horse","chicken","cat","pig"],2=>["armadillo","horse"],3=>["wolf","fox","boar","sheep"],4=>["sheep","horse","armadillo","deer"],_=>["pig","chicken","cow","cat"]};
            return kinds[variation%(uint)kinds.Length];
        }
        if(p.Y<world.Terrain.Surface(p.X,p.Z)-4)return "skitter";
        int rx=Coordinates.FloorDiv(p.X,96)*96+32,rz=Coordinates.FloorDiv(p.Z,96)*96+24;
        bool ruins=Math.Abs(p.X-rx)<=7&&Math.Abs(p.Z-rz)<=7&&world.Terrain.Surface(rx,rz)>Terrain.SeaLevel;
        if(ruins||world.Terrain.TowerLocations.Any(t=>Math.Abs(t.X-p.X)<=7&&Math.Abs(t.Z-p.Z)<=7))return "sentinel";
        if(CreatureRules.Night(world.State.SkyTime)&&variation%4!=3)return "husk";
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
        var idBytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{state.WorldId}:{state.Population.Sequence}:{kind}:{p}:{state.Time}"));
        var creature=new CreatureState{Id=new Guid(idBytes.AsSpan(0,16)).ToString("N"),Kind=kind,Health=definitions.First(d=>d.Id==kind).Health,Position=[p.X+.5,p.Y+.02,p.Z+.5]};state.Creatures.Add(creature);return creature;
    }
}
