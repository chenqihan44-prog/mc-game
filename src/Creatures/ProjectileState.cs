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
    public void Validate()
    {
        static bool VectorOk(double[]? value)=>value is {Length:3}&&Array.TrueForAll(value,double.IsFinite);
        if(!VectorOk(Position)||!Array.TrueForAll(Position,v=>Math.Abs(v)<=1_000_000)||!VectorOk(Velocity)||!double.IsFinite(Damage)||Damage<0||Damage>1000||!double.IsFinite(Remaining)||Remaining<0||Remaining>4||!double.IsFinite(Radius)||Radius<.01||Radius>.3||Speed<=0||Speed>60)throw new InvalidOperationException("投射物状态无效");
    }
    private double Speed=>Math.Sqrt(Velocity[0]*Velocity[0]+Velocity[1]*Velocity[1]+Velocity[2]*Velocity[2]);
    public ProjectileState Copy()=>new(){Position=(double[])Position.Clone(),Velocity=(double[])Velocity.Clone(),Damage=Damage,Remaining=Remaining,Radius=Radius,Hostile=Hostile};
    public Vector3 Point=>new((float)Position[0],(float)Position[1],(float)Position[2]);
    public Vector3 Direction=>new((float)Velocity[0],(float)Velocity[1],(float)Velocity[2]);
}

public enum ProjectileOutcome {Flying,Blocked,Player,Creature,Expired,Absorbed}
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
            if(!ready(point)||CombatGeometry.Blocked(world,point,(float)shot.Radius))return new(ProjectileOutcome.Blocked);
            if(shot.Hostile){
                if(CombatGeometry.BodyHit(point,player,playerHeight,.3f,(float)shot.Radius))return new(world.State.Creative||CreaturePopulation.SafeCamp(world,CombatGeometry.CellAt(player))?ProjectileOutcome.Absorbed:ProjectileOutcome.Player);
            }else foreach(var creature in world.State.Creatures){
                if(creature.Health<=0)continue;bool boss=creature.Kind=="guardian";
                if(CombatGeometry.BodyHit(point,new((float)creature.Position[0],(float)creature.Position[1],(float)creature.Position[2]),boss?2.6f:1.5f,boss?.9f:.45f,(float)shot.Radius))return new(ProjectileOutcome.Creature,creature);
            }
        }
        return new(shot.Remaining<=0?ProjectileOutcome.Expired:ProjectileOutcome.Flying);
    }
}
