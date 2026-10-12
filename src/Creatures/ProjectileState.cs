using System;
using System.Numerics;
using EmberLighthouse.World;

namespace EmberLighthouse.Creatures;

public sealed class ProjectileState
{
    public double[] Position {get;set;}=[0,40,0];
    public double[] Velocity {get;set;}=[0,0,-10];
    public double Damage {get;set;}=10;
    public double Remaining {get;set;}=3;
    public double Radius {get;set;}=.08;
    public bool Hostile {get;set;}
    public bool Arrow {get;set;}
    public bool BypassCamp {get;set;}
    public void Validate()
    {
        static bool VectorOk(double[]? value)=>value is {Length:3}&&Array.TrueForAll(value,double.IsFinite);
        if(!VectorOk(Position)||!Array.TrueForAll(Position,v=>Math.Abs(v)<=1_000_000)||!VectorOk(Velocity)||!double.IsFinite(Damage)||Damage<0||Damage>1000||!double.IsFinite(Remaining)||Remaining<0||Remaining>4||!double.IsFinite(Radius)||Radius<.01||Radius>.3||Speed<=0||Speed>60)throw new InvalidOperationException("投射物状态无效");
    }
    private double Speed=>Math.Sqrt(Velocity[0]*Velocity[0]+Velocity[1]*Velocity[1]+Velocity[2]*Velocity[2]);
    public ProjectileState Copy()=>new(){Position=(double[])Position.Clone(),Velocity=(double[])Velocity.Clone(),Damage=Damage,Remaining=Remaining,Radius=Radius,Hostile=Hostile,Arrow=Arrow,BypassCamp=BypassCamp};
    public Vector3 Point=>new((float)Position[0],(float)Position[1],(float)Position[2]);
    public Vector3 Direction=>new((float)Velocity[0],(float)Velocity[1],(float)Velocity[2]);
}

public enum ProjectileOutcome {Flying,Blocked,Player,Creature,Expired,Absorbed,Anchor}
public readonly record struct ProjectileHit(ProjectileOutcome Outcome,CreatureState? Target=null);

public static class ProjectileFlight
{
    public const int Limit=24;
    public static ProjectileHit Step(WorldState world,ProjectileState shot,double seconds,Func<Vector3,bool> ready,float playerHeight=1.8f)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        if(shot.Remaining<=0)return new(ProjectileOutcome.Expired);
        double duration=Math.Min(seconds,shot.Remaining);var start=shot.Point;var end=start+shot.Direction*(float)duration;
        int steps=Math.Max(1,(int)Math.Ceiling(Vector3.Distance(start,end)/.075f));shot.Remaining=Math.Max(0,shot.Remaining-duration);
        var player=new Vector3((float)world.State.Position[0],(float)world.State.Position[1],(float)world.State.Position[2]);
        for(int i=0;i<=steps;i++){
            var point=Vector3.Lerp(start,end,i/(float)steps);shot.Position=[point.X,point.Y,point.Z];
            if(!ready(point))return new(ProjectileOutcome.Blocked);
            if(!shot.Hostile&&Mainline.CloseAnchor(world,CombatGeometry.CellAt(point)))return new(ProjectileOutcome.Anchor);
            if(CombatGeometry.Blocked(world,point,(float)shot.Radius)){
                // A projectile's leading edge touches a block before its center does.
                // Resolve the first solid sample so arrows cannot close anchors through walls.
                var radius=(float)shot.Radius;
                var samples=new[]{point,point+Vector3.Normalize(shot.Direction)*radius,point+Vector3.UnitX*radius,point-Vector3.UnitX*radius,point+Vector3.UnitY*radius,point-Vector3.UnitY*radius,point+Vector3.UnitZ*radius,point-Vector3.UnitZ*radius};
                foreach(var sample in samples){var cell=CombatGeometry.CellAt(sample);if(!world.Solid(cell))continue;if(!shot.Hostile&&Mainline.CloseAnchor(world,cell))return new(ProjectileOutcome.Anchor);return new(ProjectileOutcome.Blocked);}
                return new(ProjectileOutcome.Blocked);
            }
            if(shot.Hostile){
                if(CombatGeometry.BodyHit(point,player,playerHeight,.3f,(float)shot.Radius))return new(world.State.Creative||!shot.BypassCamp&&CreaturePopulation.SafeCamp(world,CombatGeometry.CellAt(player))&&!world.State.Mainline.FinaleActive?ProjectileOutcome.Absorbed:ProjectileOutcome.Player);
            }else foreach(var creature in world.State.Creatures){
                if(creature.Health<=0)continue;bool boss=creature.Kind is "guardian" or "colossus";
                float height=creature.Kind switch{"colossus"=>10.9f,"guardian"=>2.6f,"mine_warden"=>2.2f,"furnace_warden"=>2.9f,"observatory_warden"=>2.78f,"cod"=>.45f,"squid" or "chicken"=>.95f,"turtle"=>.55f,"spider" or "skitter"=>.65f,"horse"=>1.9f,_=>1.5f};if(creature.BabyUntil>world.State.Time)height*=.6f;
                if(CombatGeometry.BodyHit(point,new((float)creature.Position[0],(float)creature.Position[1],(float)creature.Position[2]),height,creature.Kind=="colossus"?2.25f:boss?.9f:.45f,(float)shot.Radius))return new(ProjectileOutcome.Creature,creature);
            }
        }
        return new(shot.Remaining<=0?ProjectileOutcome.Expired:ProjectileOutcome.Flying);
    }
}
