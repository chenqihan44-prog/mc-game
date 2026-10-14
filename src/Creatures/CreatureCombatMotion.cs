using System;
using Godot;
using EmberLighthouse.World;
namespace EmberLighthouse.Creatures;

public partial class Creature
{
    private void AnimateMotion(double delta,Vector3 velocity,bool active)
    {
        float windup=IsPreparingAttack?(float)(1-State.AttackWindup/Math.Max(.01,State.AttackWindupDuration>0?State.AttackWindupDuration:Windup)):0;
        float strike=IsStriking?1-(float)(State.StrikeRemaining/model.StrikeDuration(State.AttackStyle)):-1;
        float recovery=IsRecovering?1-(float)(State.RecoveryRemaining/model.RecoveryDuration(State.AttackStyle)):-1;
        model.Animate(delta,velocity,active,windup,State.AttackStyle,strike,recovery,IsCharging,State.Sitting,!IsOnFloor()&&!LifeRules.Aquatic(Definition.Id));
    }
    private void BeginAttack(string style,Vector3 aim,string targetId="")
    {
        State.AttackStyle=style;State.AttackWindupDuration=Windup;State.AttackWindup=Windup;
        State.AttackAim=[aim.X,aim.Y,aim.Z];State.AttackTargetId=targetId;
        Velocity=new(0,Velocity.Y,0);SoundCue?.Invoke("warn_"+Definition.Id,GlobalPosition);
    }
    private bool MeleeInFront(Vector3 target,double reach)
    {
        var offset=target-GlobalPosition;if(Math.Abs(offset.Y)>1.6)return false;offset.Y=0;
        if(offset.LengthSquared()>reach*reach)return false;
        return offset.LengthSquared()<.09f||(-GlobalBasis.Z).Dot(offset.Normalized())>.15f;
    }
    private void TickWolfAttack(double delta,Creature? victim,bool selfDefense)
    {
        bool allowed=victim!=null||selfDefense;
        if(!allowed&&(IsPreparingAttack||IsStriking)){CancelAttack();return;}
        if(IsPreparingAttack){State.AttackWindup=Math.Max(0,State.AttackWindup-delta);if(State.AttackWindup<=0)ReleaseAttack();}
        else if(IsStriking){State.StrikeRemaining=Math.Max(0,State.StrikeRemaining-delta);if(State.StrikeRemaining<=0)ResolveAttack();}
        else if(allowed&&!IsRecovering&&State.AttackCooldown<=0){var aim=victim?.GlobalPosition??Player.GlobalPosition;if(GlobalPosition.DistanceTo(aim)<2&&HasLineOfSight(aim+Vector3.Up*.7f))BeginAttack("bite",aim,victim?.State.Id??"");}
    }
}
