using System;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Player;
using EmberLighthouse.Rendering;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.Creatures;

public partial class Creature
{
    public Func<Creature[]> Neighbors {get;set;}=()=>[];
    public Action<Creature>? Explode {get;set;}
    public bool Mounted {get;set;}
    public bool Exploded {get;set;}
    private string equippedAppearance="";
    private PresentationModel? animalArmor;
    private bool TickCommon(double delta,float distance)
    {
        bool baby=State.BabyUntil>World.State.Time;model.Scale=Vector3.One*(baby?.6f:1);
        string appearance=State.Armor?.Id+":"+State.Armor?.Dye;
        if(appearance!=equippedAppearance){equippedAppearance=appearance;if(animalArmor!=null){model.RemoveChild(animalArmor);animalArmor.QueueFree();animalArmor=null;}if(State.Armor is {} armor){animalArmor=new();model.AddChild(animalArmor);Color tint=new(ArmorTraits.Color(armor,World.Catalog));animalArmor.Box(animalArmor,new(Definition.Id=="horse"?.75f:.64f,.12f,Definition.Id=="horse"?1.2f:.87f),new(0,Definition.Id=="horse"?1.53f:.96f,0),tint);foreach(float side in new[]{-1f,1f})animalArmor.Box(animalArmor,new(.045f,.4f,.82f),new(side*(Definition.Id=="horse"?.37f:.32f),Definition.Id=="horse"?1.35f:.75f,0),tint.Darkened(.18f));}}
        if(Definition.Id is "zombie" or "skeleton"){
            var p=Explorer.ToCell(GlobalPosition);if(!CreatureRules.Night(World.State.SkyTime)&&p.Y>=World.Terrain.Surface(p.X,p.Z)&&World.Get(p+new Cell(0,3,0))==0){Damage(delta*1.2);if(State.Health<=0)return true;}
        }
        if(LifeRules.Aquatic(Definition.Id)){
            Behavior="水中游动";var direction=new Vector3(MathF.Sin((float)clock*.35f+GlobalPosition.Z),MathF.Sin((float)clock*.3f)*.12f,MathF.Cos((float)clock*.25f));var velocity=direction*(float)Definition.Speed;var ahead=GlobalPosition+velocity*(float)delta;
            if(World.Catalog.Id(World.Get(Explorer.ToCell(ahead)))!="water")velocity=-velocity;if(World.Catalog.Id(World.Get(Explorer.ToCell(GlobalPosition+velocity*(float)delta)))!="water"||!ReadyTerrain(GlobalPosition+velocity*(float)delta))velocity=Vector3.Zero;Velocity=velocity;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];if(new Vector2(velocity.X,velocity.Z).LengthSquared()>.01)Rotation=new(0,Mathf.Atan2(-velocity.X,-velocity.Z),0);return true;
        }
        if(!LifeRules.Animal(Definition.Id)){
            if(Definition.Id=="spider"&&IsOnWall()&&IsThreat&&!IsPreparingAttack)Velocity=new(Velocity.X,5.5f,Velocity.Z);return false;
        }
        telegraph.Visible=false;nameLabel.Visible=distance<12;if(Mounted){Behavior="骑乘";return true;}
        if(State.Sitting){Behavior="停留";Velocity=Vector3.Zero;return true;}
        nameLabel.Visible=distance<12;
        bool lure=LureId==LifeRules.Food(Definition.Id)&&distance<12;
        var foe=Definition.Id=="wolf"&&State.Tamed?Neighbors().Where(c=>c!=this&&c.Definition.Hostile&&c.State.Health>0&&c.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)<14*14).OrderBy(c=>c.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault():null;
        Vector3 destination;
        if(foe!=null){destination=foe.GlobalPosition;Behavior="保护主人";if(GlobalPosition.DistanceTo(destination)<2&&State.AttackCooldown<=0&&HasLineOfSight(destination+Vector3.Up*.5f)){foe.Damage(8);State.AttackCooldown=1.5;}}
        else if(lure||State.Tamed&&distance>4){destination=Player.GlobalPosition;Behavior=lure?"被食物吸引":"跟随";}
        else if(State.Provoked&&Definition.Id=="wolf"&&!World.State.Creative&&!CreaturePopulation.SafeCamp(World,Explorer.ToCell(Player.GlobalPosition))){destination=Player.GlobalPosition;Behavior="自卫";if(distance<2&&State.AttackCooldown<=0){AttackPlayer(8);State.AttackCooldown=2;}}
        else if(State.Provoked||distance<5&&Definition.Id is "fox" or "cat"){destination=GlobalPosition+(GlobalPosition-Player.GlobalPosition).Normalized()*7;Behavior="逃离";}
        else {destination=GlobalPosition+new Vector3(MathF.Sin((float)clock*.15f+GlobalPosition.X*.01f),0,MathF.Cos((float)clock*.13f+GlobalPosition.Z*.01f))*3;Behavior="游荡";}
        var velocityGround=Velocity;var move=destination-GlobalPosition;move.Y=0;float speed=(float)Definition.Speed*(State.Provoked?1.35f:1)*(baby?.7f:1);
        if((lure||State.Tamed)&&distance<2.5&&foe==null)move=Vector3.Zero;
        if(move.LengthSquared()>.01){var target=Explorer.ToCell(destination);if(pathClock<=0){path=VoxelPath.Find(World,Explorer.ToCell(GlobalPosition),target,96);pathIndex=0;pathClock=.9;}
            if(!WalkClear(destination)){while(pathIndex<path.Count&&GlobalPosition.DistanceSquaredTo(new(path[pathIndex].X+.5f,path[pathIndex].Y,path[pathIndex].Z+.5f))<.25)pathIndex++;if(pathIndex<path.Count){var p=path[pathIndex];move=new Vector3(p.X+.5f,p.Y,p.Z+.5f)-GlobalPosition;if(move.Y>.25&&IsOnFloor())velocityGround.Y=6;move.Y=0;}else move=Vector3.Zero;}
        }
        var desired=move.LengthSquared()>.01?move.Normalized()*speed:Vector3.Zero;velocityGround.X=desired.X;velocityGround.Z=desired.Z;if(!IsOnFloor())velocityGround.Y-=20*(float)delta;else if(velocityGround.Y<0)velocityGround.Y=0;
        if(desired.LengthSquared()>.01)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(-desired.X,-desired.Z),Mathf.Min(1,(float)delta*7)),0);
        if(ReadyTerrain(GlobalPosition+velocityGround*(float)delta)){Velocity=velocityGround;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];}else Velocity=Vector3.Zero;
        nameLabel.Text=Definition.Name+(baby?" · 幼体":"")+(State.Tamed?" · 已驯服":"");return true;
    }
    public string LureId {get;set;}="";
}
