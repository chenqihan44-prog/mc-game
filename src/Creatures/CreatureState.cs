// Pure save model is separated from the engine adapter so core tests need no Godot runtime.
namespace EmberLighthouse.Creatures;
public sealed class CreatureState
{
    public string Kind {get;set;}="deer";
    public double[] Position {get;set;}=[0,40,0];
    public double Health {get;set;}=30;
    public double AttackWindup {get;set;}=-1;
    public double AttackCooldown {get;set;}
    public double ChargeRemaining {get;set;}
    public double[]? AttackAim {get;set;}
    public bool Provoked {get;set;}
}
