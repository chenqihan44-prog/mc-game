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
    private Node3D? armorAnchor;
    private double wanderUntil;
    private Vector3 wanderDestination;
    private bool wandering;
    private Vector3 WanderDestination()
    {
        if(clock>=wanderUntil){
            wandering=!wandering;uint seed=Convert.ToUInt32(State.Id[..8],16);
            double angle=clock*.37+seed%628*.01;
            wanderUntil=clock+(wandering?3.5+seed%3:1.2+seed%4*.45);
            wanderDestination=GlobalPosition+new Vector3((float)Math.Sin(angle),0,(float)Math.Cos(angle))*(2+seed%4);
        }
        return wandering&&GlobalPosition.DistanceSquaredTo(wanderDestination)>.3?wanderDestination:GlobalPosition;
    }
    private bool TickCommon(double delta,float distance)
    {
        bool baby=State.BabyUntil>World.State.Time;float nextScale=baby?.6f:1;
        if(appearanceScale!=nextScale){
            appearanceScale=nextScale;model.Scale=Vector3.One*nextScale;
            ((BoxShape3D)bodyCollision.Shape).Size=adultCollisionSize*nextScale;
            bodyCollision.Position=Vector3.Up*adultCollisionSize.Y*nextScale*.5f;
            nameLabel.Position=Vector3.Up*model.LabelHeight*nextScale;
        }
        string appearance=State.Armor?.Id+":"+State.Armor?.Dye;
        if(appearance!=equippedAppearance){
            equippedAppearance=appearance;
            if(animalArmor!=null){animalArmor.GetParent().RemoveChild(animalArmor);animalArmor.QueueFree();animalArmor=null;armorAnchor?.QueueFree();armorAnchor=null;}
            if(State.Armor is {} armor){
                animalArmor=new();Color tint=new(ArmorTraits.Color(armor,World.Catalog));
                float width=Mathf.Max(.3f,model.BoundSize.X*.78f),length=Mathf.Max(.5f,model.BoundSize.Z*.52f),top=model.VisualHeight*.64f,sideHeight=model.VisualHeight*.22f;
                animalArmor.Box(animalArmor,new(width,.07f,length),new(0,top,0),tint);
                foreach(float side in new[]{-1f,1f})animalArmor.Box(animalArmor,new(.04f,sideHeight,length),new(side*width*.5f,top-sideHeight*.42f,0),tint.Darkened(.18f));
                armorAnchor=model.AttachBodyAccessory(animalArmor);
            }
        }
        if(Definition.Id is "zombie" or "skeleton"){
            var p=Explorer.ToCell(GlobalPosition);if(!CreatureRules.Night(World.State.SkyTime)&&p.Y>=World.Terrain.Surface(p.X,p.Z)&&World.Get(p+new Cell(0,3,0))==0){Damage(delta*1.2);if(State.Health<=0)return true;}
        }
        if(LifeRules.Aquatic(Definition.Id)){
            Behavior="水中游动";var direction=new Vector3(MathF.Sin((float)clock*.35f+GlobalPosition.Z),MathF.Sin((float)clock*.3f)*.12f,MathF.Cos((float)clock*.25f));var velocity=direction*(float)Definition.Speed;var ahead=GlobalPosition+velocity*(float)delta;
            if(World.Catalog.Id(World.Get(Explorer.ToCell(ahead)))!="water")velocity=-velocity;
            velocity=Velocity.Lerp(velocity,1-MathF.Exp(-3*(float)delta));
            if(World.Catalog.Id(World.Get(Explorer.ToCell(GlobalPosition+velocity*(float)delta)))!="water"||!ReadyTerrain(GlobalPosition+velocity*(float)delta))velocity=Vector3.Zero;
            Velocity=velocity;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];if(new Vector2(velocity.X,velocity.Z).LengthSquared()>.01)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(-velocity.X,-velocity.Z),1-MathF.Exp(-3*(float)delta)),0);return true;
        }
        if(!LifeRules.Animal(Definition.Id)&&Definition.Id!="deer"){
            if(Definition.Id=="spider"&&IsOnWall()&&IsThreat&&!IsPreparingAttack)Velocity=new(Velocity.X,5.5f,Velocity.Z);return false;
        }
        telegraph.Visible=false;nameLabel.Visible=distance<12;if(Mounted){Behavior="骑乘";return true;}
        if(State.Sitting){Behavior="停留";Velocity=Vector3.Zero;return true;}
        nameLabel.Visible=distance<12;
        bool lure=LureId!=""&&LureId==LifeRules.Food(Definition.Id)&&distance<12;
        var foe=Definition.Id=="wolf"&&State.Tamed?Neighbors().Where(c=>c!=this&&c.Definition.Hostile&&c.State.Health>0&&c.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)<14*14).OrderBy(c=>c.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault():null;
        Vector3 destination;
        bool selfDefense=State.Provoked&&Definition.Id=="wolf"&&!State.Tamed&&!World.State.Creative&&!CreaturePopulation.SafeCamp(World,Explorer.ToCell(Player.GlobalPosition));
        if(Definition.Id=="wolf")TickWolfAttack(delta,foe,selfDefense);
        if(foe!=null){destination=foe.GlobalPosition;Behavior="保护主人";}
        else if(lure||State.Tamed&&distance>4){destination=Player.GlobalPosition;Behavior=lure?"被食物吸引":"跟随";}
        else if(selfDefense){destination=Player.GlobalPosition;Behavior="自卫";}
        else if(State.Provoked||Definition.Id=="deer"&&distance<8||distance<5&&Definition.Id is "fox" or "cat"){destination=GlobalPosition+(GlobalPosition-Player.GlobalPosition).Normalized()*7;Behavior="逃离";}
        else {destination=WanderDestination();Behavior=wandering?"游荡":"休息";}
        var velocityGround=Velocity;var move=destination-GlobalPosition;move.Y=0;float speed=(float)Definition.Speed*(State.Provoked?1.35f:1)*(baby?.7f:1);
        if((lure||State.Tamed)&&distance<2.5&&foe==null||IsStriking||IsRecovering||(foe!=null||selfDefense)&&move.LengthSquared()<1.6f*1.6f)move=Vector3.Zero;
        if(move.LengthSquared()>.01){var target=Explorer.ToCell(destination);if(pathClock<=0){path=VoxelPath.Find(World,Explorer.ToCell(GlobalPosition),target,96);pathIndex=0;pathClock=.9;}
            if(!WalkClear(destination)){while(pathIndex<path.Count&&GlobalPosition.DistanceSquaredTo(new(path[pathIndex].X+.5f,path[pathIndex].Y,path[pathIndex].Z+.5f))<.25)pathIndex++;if(pathIndex<path.Count){var p=path[pathIndex];move=new Vector3(p.X+.5f,p.Y,p.Z+.5f)-GlobalPosition;if(move.Y>.25&&IsOnFloor())velocityGround.Y=7.3f;move.Y=0;}else move=Vector3.Zero;}
        }
        var desired=move.LengthSquared()>.01?move.Normalized()*speed:Vector3.Zero;
        if(IsPreparingAttack)desired*=.35f;
        float acceleration=1-MathF.Exp(-(State.Provoked?9:5)*(float)delta);
        velocityGround.X=Mathf.Lerp(velocityGround.X,desired.X,acceleration);velocityGround.Z=Mathf.Lerp(velocityGround.Z,desired.Z,acceleration);if(!IsOnFloor())velocityGround.Y-=20*(float)delta;else if(velocityGround.Y<0)velocityGround.Y=0;
        var facing=IsPreparingAttack||IsStriking?Aim-GlobalPosition:desired;facing.Y=0;
        if(facing.LengthSquared()>.01)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(-facing.X,-facing.Z),1-MathF.Exp(-6*(float)delta)),0);
        if(ReadyTerrain(GlobalPosition+velocityGround*(float)delta)){Velocity=velocityGround;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];}else Velocity=Vector3.Zero;
        nameLabel.Text=Definition.Name+(baby?" · 幼体":"")+(State.Tamed?" · 已驯服":"");return true;
    }
    public string LureId {get;set;}="";
}
