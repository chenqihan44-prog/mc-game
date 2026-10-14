using System;
using System.Linq;
using EmberLighthouse.Creatures;
namespace EmberLighthouse;
public partial class Game
{
    private sealed class CreatureHitNotice
    {
        public string Name {get;set;}="";
        public double Damage {get;set;}
        public double Health {get;set;}
        public double Maximum {get;set;}
    }
    private void DealCreatureDamage(Creature creature,double amount,string owner)
    {
        double before=creature.State.Health;creature.Damage(amount);
        var hit=new CreatureHitNotice{Name=creature.Definition.Name,Damage=Math.Max(0,before-creature.State.Health),Health=creature.State.Health,Maximum=creature.Definition.Health};
        if(NetHost&&owner!=""&&owner!="host"){
            var guest=roomPlayers.Values.FirstOrDefault(p=>p.State.Id==owner);if(guest!=null)SendNet(guest.Peer,"creature-hit",hit);
        }else if(!RemoteContext&&screen=="play")ShowCreatureHit(hit);
    }
    private void ShowCreatureHit(CreatureHitNotice hit)
    {
        if(world==null||screen!="play")return;
        combatFeedback.Confirm(hit.Name,hit.Damage,hit.Health,hit.Maximum);
        audio.Play(hit.Damage>0?"impact":"shield");
    }
}
