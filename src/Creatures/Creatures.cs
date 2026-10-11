using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Player;
using EmberLighthouse.World;
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
    private MeshInstance3D body=null!;
    private StandardMaterial3D material=null!;
    private double clock,pathClock;
    private List<Cell> path=[];
    private int pathIndex;
    private int revision;
    private double hitFlash,idleCueClock=4;
    private Label3D nameLabel=null!;
    private MeshInstance3D telegraph=null!;
    private ImmediateMesh telegraphMesh=null!;
    private StandardMaterial3D telegraphMaterial=null!;
    public string Behavior {get;private set;}="游荡";
    public bool IsPreparingAttack=>State.AttackWindup>=0;
    public bool IsCharging=>State.ChargeRemaining>0;
    public bool IsThreat=>(Definition.Hostile||State.Provoked)&&Definition.Damage>0;
    public Vector3? MarkedArea=>Definition.Attack=="slam"&&telegraph.Visible?telegraph.GlobalPosition:null;
    public override void _Ready()
    {
        GlobalPosition=new((float)State.Position[0],(float)State.Position[1],(float)State.Position[2]);
        bool boss=Definition.Id=="guardian";float size=boss?1.8f:.8f;
        material=new(){AlbedoColor=Color.FromString(Definition.Color,Colors.White)};
        body=new(){Mesh=new BoxMesh{Size=new(size,size,size*1.3f)},MaterialOverride=material,Position=Vector3.Up*size*.6f};AddChild(body);
        var head=new MeshInstance3D{Mesh=new BoxMesh{Size=Vector3.One*size*.6f},MaterialOverride=material,Position=new(0,size*1.1f,-size*.65f)};AddChild(head);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=new(size*.18f,size*.45f,size*.18f)},MaterialOverride=material,Position=new(x*size*.3f,size*.22f,z*size*.4f)});
        for(int eye=-1;eye<=1;eye+=2)head.AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=Vector3.One*size*.075f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new("171c1c")},Position=new(eye*size*.17f,size*.07f,-size*.31f)});
        nameLabel=new(){Text=Definition.Name,Position=new(0,size*1.6f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=22,PixelSize=.005f};AddChild(nameLabel);
        // Ordinary creatures need clearance from the open door's 0.15-wide panel.
        // Keep the guardian's larger physical footprint.
        float collisionWidth=boss?size:.6f;
        AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(collisionWidth,size*1.3f,collisionWidth)},Position=Vector3.Up*size*.65f});FloorSnapLength=.5f;
        telegraphMesh=new();telegraphMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        if(Definition.Attack=="slam")for(int i=0;i<64;i++){float a=i*Mathf.Tau/64,b=(i+1)*Mathf.Tau/64,r=(float)Definition.AttackRadius;telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));telegraphMesh.SurfaceAddVertex(new(Mathf.Cos(b)*r,0,Mathf.Sin(b)*r));}
        else if(Definition.Attack=="charge")foreach(var pair in new[]{(Vector3.Zero,new Vector3(0,0,-4)),(new Vector3(0,0,-4),new Vector3(-.5f,0,-3.3f)),(new Vector3(0,0,-4),new Vector3(.5f,0,-3.3f))}){telegraphMesh.SurfaceAddVertex(pair.Item1);telegraphMesh.SurfaceAddVertex(pair.Item2);}
        else{telegraphMesh.SurfaceAddVertex(new(-.35f,0,0));telegraphMesh.SurfaceAddVertex(new(.35f,0,0));}
        telegraphMesh.SurfaceEnd();telegraphMaterial=new(){AlbedoColor=new("ffae66"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
        telegraph=new(){Mesh=telegraphMesh,MaterialOverride=telegraphMaterial,Visible=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(telegraph);
    }
    public override void _PhysicsProcess(double delta)
    {
        if(State.Health<=0){Velocity=Vector3.Zero;telegraph.Visible=false;return;}
        bool ready=ReadyTerrain(GlobalPosition);Visible=ready;if(!Player.Enabled||!ready){Velocity=Vector3.Zero;return;}
        float distance=GlobalPosition.DistanceTo(Player.GlobalPosition);if(distance>55){Velocity=Vector3.Zero;telegraph.Visible=false;return;}
        clock+=delta;State.AttackCooldown=Math.Max(0,State.AttackCooldown-delta);pathClock-=delta;
        hitFlash=Math.Max(0,hitFlash-delta);material.AlbedoColor=hitFlash>0?Colors.White:IsPreparingAttack||IsCharging?Colors.OrangeRed:Color.FromString(Definition.Color,Colors.White);
        idleCueClock-=delta;if(idleCueClock<=0){idleCueClock=8+Math.Abs(GlobalPosition.X)%7;if(distance<18&&HasLineOfSight(Player.GlobalPosition+Vector3.Up))SoundCue?.Invoke("idle_"+Definition.Id,GlobalPosition);}
        nameLabel.Visible=distance<18;
        nameLabel.Text=Definition.Name+(IsPreparingAttack?"\n"+(Definition.Attack switch{"charge"=>"冲锋预备","bolt"=>"蓄能射击","slam"=>"离开预警圈",_=>"攻击前摇"}):IsCharging?"\n冲锋":"");
        bool safe=CreaturePopulation.SafeCamp(World,Explorer.ToCell(Player.GlobalPosition));
        bool hostile=IsThreat&&!World.State.Creative&&!safe;
        if(!hostile&&(IsPreparingAttack||IsCharging))CancelAttack();
        if(IsPreparingAttack){State.AttackWindup-=delta;Behavior=Definition.Attack switch{"charge"=>"冲锋预备","bolt"=>"蓄能射击","slam"=>"范围重击预警",_=>"攻击前摇"};UpdateTelegraph();if(State.AttackWindup<=0)ReleaseAttack();}
        else if(hostile&&!IsCharging&&Definition.Attack!="none"&&distance<Definition.Range&&State.AttackCooldown<=0&&HasLineOfSight(Player.GlobalPosition+Vector3.Up)){
            State.AttackWindup=Definition.Windup;State.AttackAim=[Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z];Velocity=Vector3.Zero;SoundCue?.Invoke("warn_"+Definition.Id,GlobalPosition);UpdateTelegraph();return;
        }
        if(IsCharging){Charge(delta);return;}
        telegraph.Visible=IsPreparingAttack;
        var target=Explorer.ToCell(Player.GlobalPosition);
        if(!hostile||State.Health<Definition.Health*.2){Behavior=distance<8?"撤退":"游荡";Vector3 away=(GlobalPosition-Player.GlobalPosition).Normalized();if(distance<8)target=Explorer.ToCell(GlobalPosition+away*8);else target=Explorer.ToCell(GlobalPosition+new Vector3(MathF.Sin((float)clock*.2f)*4,0,MathF.Cos((float)clock*.2f)*4));}
        else if(!IsPreparingAttack)Behavior="追逐";
        Cell current=Explorer.ToCell(GlobalPosition);int currentRevision=World.Revision(Coordinates.Chunk(current));
        if(pathClock<=0||currentRevision!=revision){path=VoxelPath.Find(World,current,target);pathIndex=0;pathClock=.7;revision=currentRevision;}
        Vector3 velocity=Velocity;velocity.X=0;velocity.Z=0;
        if(!IsPreparingAttack&&pathIndex<path.Count){var next=path[pathIndex];Vector3 point=new(next.X+.5f,next.Y,next.Z+.5f);var direction=point-GlobalPosition;direction.Y=0;
            if(direction.Length()<.25f)pathIndex++;else{direction=direction.Normalized();Rotation=new(0,Mathf.Atan2(-direction.X,-direction.Z),0);velocity.X=direction.X*(float)Definition.Speed;velocity.Z=direction.Z*(float)Definition.Speed;if(next.Y>GlobalPosition.Y+.3f&&IsOnFloor())velocity.Y=7.3f;}
        }
        if(!IsOnFloor())velocity.Y-=20*(float)delta;Velocity=velocity;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];
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
        telegraph.Visible=IsPreparingAttack;
        if(Definition.Attack=="slam"){telegraph.GlobalPosition=Aim+Vector3.Up*.05f;telegraph.GlobalRotation=Vector3.Zero;}
        else{telegraph.GlobalPosition=GlobalPosition+Vector3.Up*.05f;var direction=Aim-GlobalPosition;direction.Y=0;if(direction.LengthSquared()>.001)telegraph.GlobalBasis=Basis.LookingAt(direction.Normalized());}
    }
    private void ReleaseAttack()
    {
        State.AttackWindup=-1;State.AttackCooldown=Definition.Cooldown;telegraph.Visible=false;
        if(Definition.Attack=="charge"){State.ChargeRemaining=Definition.AttackDuration;return;}
        if(Definition.Attack=="bolt"){
            var origin=GlobalPosition+Vector3.Up*1.1f;var direction=(Aim+Vector3.Up*.9f-origin).Normalized();var velocity=direction*(float)Definition.AttackSpeed;
            FireProjectile?.Invoke(new(){Position=[origin.X,origin.Y,origin.Z],Velocity=[velocity.X,velocity.Y,velocity.Z],Damage=Definition.Damage,Remaining=Definition.AttackDuration,Radius=Definition.AttackRadius,Hostile=true});
        }else{
            bool hit=Definition.Attack=="slam"?CombatGeometry.InArea(new(Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z),new(Aim.X,Aim.Y,Aim.Z),(float)Definition.AttackRadius):GlobalPosition.DistanceTo(Player.GlobalPosition)<Definition.Range+.3;
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
        direction=direction.Normalized();var ahead=GlobalPosition+direction*(float)(Definition.AttackSpeed*delta);
        Rotation=new(0,Mathf.Atan2(-direction.X,-direction.Z),0);
        if(!ReadyTerrain(ahead)){EndCharge();return;}
        var velocity=Velocity;velocity.X=direction.X*(float)Definition.AttackSpeed;velocity.Z=direction.Z*(float)Definition.AttackSpeed;if(!IsOnFloor())velocity.Y-=20*(float)delta;
        Velocity=velocity;MoveAndSlide();State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];State.ChargeRemaining=Math.Max(0,State.ChargeRemaining-delta);
        var point=GlobalPosition+Vector3.Up*.6f;
        if(CombatGeometry.BodyHit(new(point.X,point.Y,point.Z),new(Player.GlobalPosition.X,Player.GlobalPosition.Y,Player.GlobalPosition.Z),Player.Crouching?1.1f:1.8f,.3f,(float)Definition.AttackRadius)&&HasLineOfSight(Player.GlobalPosition+Vector3.Up)){AttackPlayer(Definition.Damage);EndCharge();}
        else if(State.ChargeRemaining<=0||GetSlideCollisionCount()>0&&GetLastSlideCollision().GetNormal().Y<.5f)EndCharge();
    }
    private void EndCharge(){State.ChargeRemaining=0;State.AttackAim=null;State.AttackCooldown=Definition.Cooldown;Velocity=new(0,Velocity.Y,0);}
    public override void _ExitTree(){telegraphMesh.Dispose();telegraphMaterial.Dispose();}
    public void Damage(double amount){if(!double.IsFinite(amount)||amount<=0||State.Health<=0)return;State.Health=Math.Max(0,State.Health-amount);State.Provoked=true;hitFlash=.15;material.AlbedoColor=Colors.White;if(State.Health<=0)Died(this);}
}
