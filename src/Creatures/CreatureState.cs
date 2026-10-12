// Pure save model is separated from the engine adapter so core tests need no Godot runtime.
namespace EmberLighthouse.Creatures;
public sealed class CreatureState
{
    public string Id {get;set;}=System.Guid.NewGuid().ToString("N");
    public bool Tamed {get;set;}
    public bool Persistent {get;set;}
    public bool Sitting {get;set;}
    public bool Saddled {get;set;}
    public double BabyUntil {get;set;}
    public double LoveUntil {get;set;}
    public double BreedReady {get;set;}
    public double ProduceReady {get;set;}
    public bool ScutePending {get;set;}
    public EmberLighthouse.Items.Stack? Armor {get;set;}
    public CreatureState Copy(){var c=(CreatureState)MemberwiseClone();c.Position=(double[])Position.Clone();c.AttackAim=AttackAim==null?null:(double[])AttackAim.Clone();c.Armor=Armor?.Copy();return c;}
    public string Kind {get;set;}="deer";
    public string Home {get;set;}="";
    public string AttackStyle {get;set;}="";
    public double[] Position {get;set;}=[0,40,0];
    public double Health {get;set;}=30;
    public double AttackWindup {get;set;}=-1;
    public double AttackCooldown {get;set;}
    public double ChargeRemaining {get;set;}
    public double[]? AttackAim {get;set;}
    public bool Provoked {get;set;}
}
