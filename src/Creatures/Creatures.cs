using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using EmberLighthouse.Rendering;
using Godot;

namespace EmberLighthouse.Creatures;

public static class VoxelPath
{
    public static List<Cell> Find(WorldState world,Cell start,Cell target,int budget=256)
    {
        var open=new PriorityQueue<Cell,int>();var cost=new Dictionary<Cell,int>{{start,0}};var parent=new Dictionary<Cell,Cell>();open.Enqueue(start,0);Cell best=start;int closest=Distance(start,target);
        while(open.Count>0&&budget-->0){var p=open.Dequeue();int d=Distance(p,target);if(d<closest){closest=d;best=p;}if(d<2){best=p;break;}
            foreach(var step in new Cell[]{new(1,0,0),new(-1,0,0),new(0,0,1),new(0,0,-1)}){
                Cell q=p+step;bool valid=false;foreach(int dy in new[]{0,1,-1}){var n=q+new Cell(0,dy,0);if(!world.Solid(n)&&!world.Solid(n+new Cell(0,1,0))&&world.Solid(n+new Cell(0,-1,0))){q=n;valid=true;break;}}
                if(!valid)continue;int next=cost[p]+1;if(cost.TryGetValue(q,out var old)&&old<=next)continue;cost[q]=next;parent[q]=p;open.Enqueue(q,next+Distance(q,target));
            }
        }
        var path=new List<Cell>();while(best!=start&&parent.TryGetValue(best,out var from)){path.Add(best);best=from;}path.Reverse();return path;
    }
    private static int Distance(Cell a,Cell b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)+Math.Abs(a.Z-b.Z);
}

public partial class Creature : CharacterBody3D
{
    public CreatureDef Definition {get;set;}=null!;
    public CreatureState State {get;set;}=null!;
    public WorldState World {get;set;}=null!;
    public Explorer Player {get;set;}=null!;
    public Action<double> AttackPlayer {get;set;}=null!;
    public Action<Creature> Died {get;set;}=null!;
    public Func<Vector3,bool> ReadyTerrain {get;set;}=null!;
    public Action<string,Vector3>? SoundCue {get;set;}
    public Func<ProjectileState,bool>? FireProjectile {get;set;}
    private CreatureModel model=null!;
    private double clock,pathClock;
    private List<Cell> path=[];
    private int pathIndex;
    private int revision;
    private Cell pathTarget;
    private double stuckClock;
    private double hitFlash,idleCueClock=4;
    private Label3D nameLabel=null!;
    private MeshInstance3D telegraph=null!;
    private ImmediateMesh telegraphMesh=null!;
    private StandardMaterial3D telegraphMaterial=null!;
    private string telegraphStyle="";
    private bool Enraged=>Definition.Id=="colossus"&&State.Health<Definition.Health*.35;
    private double Windup=>Definition.Windup*(Enraged?.8:1);
    private double Cooldown=>Definition.Cooldown*(Enraged?.8:1);
    private float AreaRadius=>Definition.Id is "colossus" or "observatory_warden"?4.5f:Definition.Id=="furnace_warden"?3:(float)Definition.AttackRadius;
    private string AttackKind {
        get {
            if((State.AttackWindup>=0||State.ChargeRemaining>0)&&State.AttackStyle!="")return State.AttackStyle;
            if(Definition.Id=="colossus")return World.State.Mainline.Anchors.Count<4?"bolt":((int)(World.State.Mainline.BattleTime/6)%3) switch{0=>"slam",1=>"charge",_=>"bolt"};
            if(Definition.Id=="furnace_warden")return (int)(World.State.Time/5)%2==0?"charge":"bolt";
            if(Definition.Id=="observatory_warden")return (int)(World.State.Time/6)%3==0?"slam":"bolt";
            return Definition.Attack;
        }
    }
    public string Behavior {get;private set;}="游荡";
    public bool IsPreparingAttack=>State.AttackWindup>=0;
    public bool IsCharging=>State.ChargeRemaining>0;
    public bool IsThreat=>(Definition.Hostile||State.Provoked)&&Definition.Damage>0;
    public Vector3? MarkedArea=>AttackKind=="slam"&&telegraph.Visible?telegraph.GlobalPosition:null;
    public override void _Ready()
    {
        GlobalPosition=new((float)State.Position[0],(float)State.Position[1],(float)State.Position[2]);
        bool boss=Definition.Id is "guardian" or "colossus";float size=Definition.Id=="colossus"?4.5f:boss?1.8f:.8f;
        model=CreatureModel.Create(Definition.Id);AddChild(model);
        nameLabel=new(){Text=Definition.Name,Position=new(0,model.LabelHeight,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=22,PixelSize=.005f};AddChild(nameLabel);
        // Ordinary creatures need clearance from the open door's 0.15-wide panel.
        // Keep the guardian's larger physical footprint.
        float collisionWidth=boss?size:.6f;
        AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(collisionWidth,model.BodyHeight,collisionWidth)},Position=Vector3.Up*model.BodyHeight*.5f});FloorSnapLength=.5f;
        telegraphMesh=new();telegraphMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        if(AttackKind=="slam")for(int i=0;i<64;i++){float a=i*Mathf.Tau/64,b=(i+1)*Mathf.Tau/64,r=(float)Definition.AttackRadius;telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(b)*r,0,Mathf.Sin(b)*r));}
        else if(AttackKind=="charge")foreach(var pair in new[]{(Vector3.Zero,new Vector3(0,0,-4)),(new Vector3(0,0,-4),new Vector3(-.5f,0,-3.3f)),(new Vector3(0,0,-4),new Vector3(.5f,0,-3.3f))}){telegraphMesh.SurfaceAddVertex(pair.Item1);telegraphMesh.SurfaceAddVertex(pair.Item2);}
        else{telegraphMesh.SurfaceAddVertex(new(-.35f,0,0));telegraphMesh.SurfaceAddVertex(new(.35f,0,0));}
        telegraphMesh.SurfaceEnd();telegraphMaterial=new(){AlbedoColor=new("ffae66"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
        telegraph=new(){Mesh=telegraphMesh,MaterialOverride=telegraphMaterial,Visible=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(telegraph);
    }
    public override void _PhysicsProcess(double delta)
    {
        if(State.Health<=0){Velocity=Vector3.Zero;telegraph.Visible=false;return;}
        if(Player.Enabled&&State.Tamed&&!State.Sitting&&!Mounted&&GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)>28*28){foreach(var offset in new[]{Vector3.Right*4,Vector3.Left*4,Vector3.Back*4,Vector3.Forward*4}){var spot=Player.GlobalPosition+offset;var cell=Explorer.ToCell(spot);if(ReadyTerrain(spot)&&CreaturePopulation.CanStand(World,cell)){GlobalPosition=new(cell.X+.5f,cell.Y,cell.Z+.5f);State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];pathClock=0;break;}}}
        bool ready=ReadyTerrain(GlobalPosition);Visible=ready;model.Animate(delta,Velocity,Player.Enabled&&ready,IsPreparingAttack?(float)(1-State.AttackWindup/Math.Max(.01,Definition.Windup)):0);if(!Player.Enabled||!ready){Velocity=Vector3.Zero;return;}
        float distance=GlobalPosition.DistanceTo(Player.GlobalPosition);if(distance>55){Velocity=Vector3.Zero;telegraph.Visible=false;return;}
        clock+=delta;State.AttackCooldown=Math.Max(0,State.AttackCooldown-delta);pathClock-=delta;
        hitFlash=Math.Max(0,hitFlash-delta);model.Tint(hitFlash>0,IsPreparingAttack||IsCharging);
        if(TickCommon(delta,distance))return;
        idleCueClock-=delta;if(idleCueClock<=0){idleCueClock=8+Math.Abs(GlobalPosition.X)%7;if(distance<18&&HasLineOfSight(Player.GlobalPosition+Vector3.Up))SoundCue?.Invoke("idle_"+Definition.Id,GlobalPosition);}
        nameLabel.Visible=distance<18;
        nameLabel.Text=Definition.Name+(IsPreparingAttack?"\n"+(AttackKind switch{"charge"=>"冲锋预备","bolt"=>"蓄能射击","slam"=>"离开预警圈",_=>"攻击前摇"}):IsCharging?"\n冲锋":"");
        bool safe=!CreatureRules.Elite(Definition.Id)&&State.Home==""&&CreaturePopulation.SafeCamp(World,Explorer.ToCell(Player.GlobalPosition));
        bool hostile=IsThreat&&!World.State.Creative&&!safe;
        if(Definition.Id=="creeper"&&Neighbors().Any(c=>c.Definition.Id=="cat"&&c.State.Tamed&&c.GlobalPosition.DistanceSquaredTo(GlobalPosition)<64)){hostile=false;State.Provoked=false;}
        if(!hostile&&(IsPreparingAttack||IsCharging))CancelAttack();
        if(IsPreparingAttack){State.AttackWindup=Math.Max(0,State.AttackWindup-delta);Behavior=AttackKind switch{"charge"=>"冲锋预备","bolt"=>"蓄能射击","slam"=>"范围重击预警",_=>"攻击前摇"};UpdateTelegraph();if(State.AttackWindup<=0)ReleaseAttack();}
        else if(hostile&&!IsCharging&&AttackKind!="none"&&distance<Definition.Range&&State.AttackCooldown<=0&&HasLineOfSight(Player.GlobalPosition+Vector3.Up)){
            State.AttackStyle=AttackKind;State.AttackWindup=Windup;State.AttackAim=[Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z];Velocity=Vector3.Zero;SoundCue?.Invoke("warn_"+Definition.Id,GlobalPosition);UpdateTelegraph();return;
        }
        if(IsCharging){Charge(delta);return;}
        telegraph.Visible=IsPreparingAttack;
        Vector3 destination=Player.GlobalPosition;
        if(!hostile){Behavior=distance<8?"撤退":"游荡";Vector3 away=(GlobalPosition-Player.GlobalPosition).Normalized();destination=distance<8?GlobalPosition+away*8:GlobalPosition+new Vector3(MathF.Sin((float)clock*.2f)*4,0,MathF.Cos((float)clock*.2f)*4);}
        else if(AttackKind=="bolt"&&!IsPreparingAttack&&HasLineOfSight(Player.GlobalPosition+Vector3.Up)&&distance<11){
            var away=GlobalPosition-Player.GlobalPosition;away.Y=0;away=away.Normalized();var side=new Vector3(-away.Z,0,away.X)*(Math.Sin(clock*.6)>=0?1:-1);
            destination=GlobalPosition+(distance<6?away*3:side*2);Behavior=distance<6?"拉开距离":"侧移射击";
        }
        else if(!IsPreparingAttack)Behavior="追逐";
        var target=Explorer.ToCell(destination);
        Cell current=Explorer.ToCell(GlobalPosition);int currentRevision=World.Revision(Coordinates.Chunk(current));
        if(pathClock<=0||currentRevision!=revision||target!=pathTarget&&pathClock<.15){path=VoxelPath.Find(World,current,target);pathIndex=0;pathClock=hostile&&distance<24?.35:.8;revision=currentRevision;pathTarget=target;}
        Vector3 velocity=Velocity,desired=Vector3.Zero;bool mobile=!IsPreparingAttack||AttackKind is "melee" or "bite";
        if(mobile){
            Vector3 direction=destination-GlobalPosition;direction.Y=0;
            bool close=hostile&&(AttackKind is "melee" or "bite")&&distance<Definition.Range*.68;
            if(!close&&direction.LengthSquared()>.04&&WalkClear(destination))desired=direction.Normalized()*(float)Definition.Speed;
            else if(!close){
                while(pathIndex<path.Count){var next=path[pathIndex];Vector3 point=new(next.X+.5f,next.Y,next.Z+.5f);direction=point-GlobalPosition;direction.Y=0;
                    if(direction.Length()<.3f){pathIndex++;continue;}
                    desired=direction.Normalized()*(float)Definition.Speed;if(next.Y>GlobalPosition.Y+.3f&&IsOnFloor())velocity.Y=7.3f;break;
                }
            }
            if(IsPreparingAttack)desired*=.4f;
        }
        if(Definition.Id=="spider"&&hostile&&!IsPreparingAttack){var pursuit=destination-GlobalPosition;pursuit.Y=0;if(pursuit.LengthSquared()>.01)desired=pursuit.Normalized()*(float)Definition.Speed;if(IsOnWall())velocity.Y=5.5f;}
        if(desired.LengthSquared()>.01)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(-desired.X,-desired.Z),Mathf.Min(1,(float)delta*10)),0);
        velocity.X=Mathf.MoveToward(velocity.X,desired.X,(float)delta*18);velocity.Z=Mathf.MoveToward(velocity.Z,desired.Z,(float)delta*18);
        if(!IsOnFloor())velocity.Y-=20*(float)delta;Velocity=velocity;var previous=GlobalPosition;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];
        stuckClock=desired.LengthSquared()>1&&GlobalPosition.DistanceSquaredTo(previous)<.00001?stuckClock+delta:0;
        if(stuckClock>.35){pathClock=0;stuckClock=0;}
    }
    private bool WalkClear(Vector3 destination)
    {
        if(Math.Abs(destination.Y-GlobalPosition.Y)>.4)return false;
        var direction=destination-GlobalPosition;direction.Y=0;float length=direction.Length();if(length>18)return false;
        var side=length>.01?new Vector3(-direction.Z,0,direction.X).Normalized()*.34f:Vector3.Zero;
        for(float d=.2f;d<=length+.2f;d+=.2f){var point=GlobalPosition+direction*Mathf.Min(d/Mathf.Max(length,.01f),1);
            for(int offset=-1;offset<=1;offset++){var cell=Explorer.ToCell(point+side*offset+Vector3.Up*.1f);if(World.Solid(cell)||World.Solid(cell+new Cell(0,1,0))||!World.Solid(cell+new Cell(0,-1,0)))return false;}
        }return true;
    }
    private static float CellDistance(Cell c,Vector3 p)=>new Vector3(c.X,c.Y,c.Z).DistanceTo(p);
    public bool HasLineOfSight(Vector3 target)
    {
        Vector3 start=GlobalPosition+Vector3.Up;
        return CombatGeometry.Clear(World,new(start.X,start.Y,start.Z),new(target.X,target.Y,target.Z));
    }
    private Vector3 Aim=>State.AttackAim is { } aim?new((float)aim[0],(float)aim[1],(float)aim[2]):GlobalPosition;
    private void CancelAttack(){State.AttackWindup=-1;State.ChargeRemaining=0;State.AttackAim=null;State.AttackCooldown=Math.Max(State.AttackCooldown,.5);telegraph.Visible=false;}
    private void UpdateTelegraph()
    {
        if(telegraphStyle!=AttackKind){telegraphStyle=AttackKind;telegraphMesh.ClearSurfaces();telegraphMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            if(AttackKind=="slam")for(int i=0;i<64;i++){float a=i*Mathf.Tau/64,b=(i+1)*Mathf.Tau/64;telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(a)*AreaRadius,0,Mathf.Sin(a)*AreaRadius));telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(b)*AreaRadius,0,Mathf.Sin(b)*AreaRadius));}
            else{telegraphMesh.SurfaceAddVertex(Vector3.Zero);telegraphMesh.SurfaceAddVertex(new(0,0,-6));telegraphMesh.SurfaceAddVertex(new(-.5f,0,-5));telegraphMesh.SurfaceAddVertex(new(0,0,-6));}telegraphMesh.SurfaceEnd();}
        telegraph.Visible=IsPreparingAttack;
        if(AttackKind=="slam"){telegraph.GlobalPosition=Aim+Vector3.Up*.05f;telegraph.GlobalRotation=Vector3.Zero;}
        else{telegraph.GlobalPosition=GlobalPosition+Vector3.Up*.05f;var direction=Aim-GlobalPosition;direction.Y=0;if(direction.LengthSquared()>.001)telegraph.GlobalBasis=Basis.LookingAt(direction.Normalized());}
    }
    private void ReleaseAttack()
    {
        string attack=AttackKind;State.AttackWindup=-1;State.AttackCooldown=Cooldown;telegraph.Visible=false;
        if(attack=="explode"){if(GlobalPosition.DistanceTo(Player.GlobalPosition)<Definition.AttackRadius&&HasLineOfSight(Player.GlobalPosition+Vector3.Up))AttackPlayer(Definition.Damage);Explode?.Invoke(this);return;}
        if(attack=="charge"){State.ChargeRemaining=Definition.Id=="colossus"?.85:Definition.AttackDuration;return;}
        if(attack=="bolt"){
            var origin=GlobalPosition+Vector3.Up*(Definition.Id=="colossus"?5.6f:1.1f);var direction=(Aim+Vector3.Up*.9f-origin).Normalized();
            int count=Definition.Id is "observatory_warden" or "furnace_warden"?3:1;
            for(int i=0;i<count;i++){var v=direction.Rotated(Vector3.Up,(i-(count-1)*.5f)*.1f)*(float)(Definition.Id is "colossus" or "furnace_warden"?12:Definition.AttackSpeed);FireProjectile?.Invoke(new(){Position=[origin.X,origin.Y,origin.Z],Velocity=[v.X,v.Y,v.Z],Damage=Definition.Id=="colossus"?32:Definition.Damage,Remaining=CreatureRules.Elite(Definition.Id)?3:Definition.AttackDuration,Radius=CreatureRules.Elite(Definition.Id)?.12:Definition.AttackRadius,Hostile=true});}
        }else{
            bool hit=attack=="slam"?CombatGeometry.InArea(new(Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z),new(Aim.X,Aim.Y,Aim.Z),AreaRadius):GlobalPosition.DistanceTo(Player.GlobalPosition)<Definition.Range+.3;
            if(hit&&HasLineOfSight(Player.GlobalPosition+Vector3.Up))AttackPlayer(Definition.Damage);
        }
        State.AttackAim=null;
    }
    private void Charge(double delta)
    {
        Behavior="冲锋";var direction=Aim-GlobalPosition;direction.Y=0;
        // The saved aim fixes the line; crossing it ends the charge rather than
        // turning the boar around to track the player.
        if(direction.LengthSquared()<.15f){EndCharge();return;}
        direction=direction.Normalized();var ahead=GlobalPosition+direction*(float)((Definition.Id=="colossus"?9:Definition.AttackSpeed)*delta);
        Rotation=new(0,Mathf.Atan2(-direction.X,-direction.Z),0);
        if(!ReadyTerrain(ahead)){EndCharge();return;}
        var velocity=Velocity;velocity.X=direction.X*(float)(Definition.Id=="colossus"?9:Definition.AttackSpeed);velocity.Z=direction.Z*(float)(Definition.Id=="colossus"?9:Definition.AttackSpeed);if(!IsOnFloor())velocity.Y-=20*(float)delta;
        Velocity=velocity;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];State.ChargeRemaining=Math.Max(0,State.ChargeRemaining-delta);
        var point=GlobalPosition+Vector3.Up*.6f;
        if(CombatGeometry.BodyHit(new(point.X,point.Y,point.Z),new(Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z),Player.Crouching?1.1f:1.8f,.3f,(Definition.Id=="colossus"?2.5f:(float)Definition.AttackRadius))&&HasLineOfSight(Player.GlobalPosition+Vector3.Up)){AttackPlayer(Definition.Damage);EndCharge();}
        else if(State.ChargeRemaining<=0||GetSlideCollisionCount()>0&&GetLastSlideCollision().GetNormal().Y<.5f)EndCharge();
    }
    private void EndCharge(){State.ChargeRemaining=0;State.AttackAim=null;State.AttackCooldown=Definition.Cooldown;Velocity=new(0,Velocity.Y,0);}
    public override void _ExitTree(){telegraphMesh.Dispose();telegraphMaterial.Dispose();}
    public void Damage(double amount){if(!double.IsFinite(amount)||amount<=0||State.Health<=0)return;if(State.Armor is {} armor){var def=World.Catalog.Items[armor.Id];amount*=Math.Max(.35,1-def.Power*.04);armor.Durability=(armor.Durability??def.Durability)-(int)Math.Max(1,Math.Ceiling(amount));if(armor.Durability<=0)State.Armor=null;}if(State.BabyUntil>World.State.Time)amount*=.8;if(Definition.Id=="armadillo"&&!State.Tamed)amount*=.6;if(Definition.Id=="colossus"){if(World.State.Mainline.Anchors.Count<4){SoundCue?.Invoke("shield",GlobalPosition);return;}if(State.AttackStyle!="slam"||State.AttackCooldown<1.4||State.AttackWindup>=0)amount*=.25;}State.Health=Math.Max(0,State.Health-amount);State.Provoked=!State.Tamed;if(Definition.Id=="creeper"&&IsPreparingAttack)CancelAttack();hitFlash=.15;model.Tint(true,false);if(State.Health<=0)Died(this);}
}
