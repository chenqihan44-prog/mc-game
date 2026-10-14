using System;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using EmberLighthouse.Items;
using EmberLighthouse.Creatures;
using Godot;

namespace EmberLighthouse.Player;

public partial class Explorer : CharacterBody3D
{
    private const float BodyRadius=.3f,BodyMargin=.015f;
    public static Vector3 KeepInsideWorld(Vector3 position)=>new(Math.Clamp(position.X,-Coordinates.Limit+BodyRadius+BodyMargin,Coordinates.Limit-BodyRadius-BodyMargin),position.Y,Math.Clamp(position.Z,-Coordinates.Limit+BodyRadius+BodyMargin,Coordinates.Limit-BodyRadius-BodyMargin));
    public Camera3D Camera {get;private set;}=null!;
    public WorldState Model {get;set;}=null!;
    public Func<Vector3,bool> TerrainReady {get;set;}=null!;
    public Action<double> Hurt {get;set;}=null!;
    public Action? BoundaryHit {get;set;}
    public Action<Vector2>? LookMoved {get;set;}
    public bool Enabled {get;set;}
    public bool AcceptUserInput {get;set;}=true;
    public bool NetworkReplica {get;set;}
    public float Sensitivity {get;set;}=.0023f;
    public bool Flying {get;set;}
    public bool Swimming {get;private set;}
    public bool Crouching {get;private set;}
    public Creature? Mount {get;set;}
    public bool Gliding {get;private set;}
    public void SetReplicaMovementFlags(bool flying,bool gliding){if(NetworkReplica){Flying=flying;Gliding=gliding;}}
    public double GlideBoost {get;set;}
    public Vector3 Impact {get;set;}
    private double glideWearClock;
    public bool Dismount(){if(Mount==null)return true;var horse=Mount;foreach(var offset in new[]{Vector3.Right*1.6f,Vector3.Left*1.6f,Vector3.Back*1.6f,Vector3.Forward*1.6f}){var p=KeepInsideWorld(horse.GlobalPosition+offset);var cell=ToCell(p);if(TerrainReady(p)&&!Model.Solid(cell)&&!Model.Solid(cell+new Cell(0,1,0))&&Model.Solid(cell+new Cell(0,-1,0))){RemoveCollisionExceptionWith(horse);horse.RemoveCollisionExceptionWith(this);horse.Mounted=false;Mount=null;GlobalPosition=p;Velocity=Vector3.Zero;Model.State.Position=[p.X,p.Y,p.Z];return true;}}return false;}
    private float pitch,fallSpeed;
    private double boundaryNoticeCooldown;
    private CollisionShape3D collision=null!;
    private CapsuleShape3D bodyShape=null!;
    public override void _Ready()
    {
        bodyShape=new(){Radius=BodyRadius,Height=1.8f};collision=new(){Shape=bodyShape,Position=new(0,.9f,0)};AddChild(collision);
        Camera=new(){Position=new(0,1.62f,0),Current=!NetworkReplica,Fov=78,Far=400};AddChild(Camera);
        FloorSnapLength=.3f;SafeMargin=BodyMargin;
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(NetworkReplica||!Enabled||!AcceptUserInput)return;
        if(e is InputEventMouseMotion motion&&Input.MouseMode==Input.MouseModeEnum.Captured){RotateY(-motion.Relative.X*Sensitivity);pitch=Math.Clamp(pitch-motion.Relative.Y*Sensitivity,-1.53f,1.53f);Camera.Rotation=new(pitch,0,0);LookMoved?.Invoke(motion.Relative);}
        if(e.IsActionPressed("fly")&&Model.State.Creative)Flying=!Flying;
    }
    public override void _PhysicsProcess(double delta)
    {
        if(NetworkReplica)return;
        if(!Enabled){Velocity=Vector3.Zero;return;}
        if(Mount is {} horse){
            if(horse.State.Health<=0){RemoveCollisionExceptionWith(horse);horse.Mounted=false;Mount=null;}
            else {
                if(Input.IsActionJustPressed("crouch")&&Dismount())return;
                var mountInput=Input.GetVector("left","right","forward","back");
                var move=Transform.Basis*new Vector3(mountInput.X,0,mountInput.Y);var v=horse.Velocity;
                float blend=1-MathF.Exp(-8*(float)delta);v.X=Mathf.Lerp(v.X,move.X*8,blend);v.Z=Mathf.Lerp(v.Z,move.Z*8,blend);
                if(!horse.IsOnFloor())v.Y-=22*(float)delta;else{v.Y=0;if(Input.IsActionJustPressed("jump"))v.Y=7.3f;}
                var mountNext=KeepInsideWorld(horse.GlobalPosition+v*(float)delta);
                if(TerrainReady(mountNext)){
                    horse.Velocity=v;horse.MoveAndSlide();horse.GlobalPosition=KeepInsideWorld(horse.GlobalPosition);
                    horse.State.Position=[horse.GlobalPosition.X,horse.GlobalPosition.Y,horse.GlobalPosition.Z];
                    if(move.LengthSquared()>.01)horse.Rotation=new(0,Mathf.LerpAngle(horse.Rotation.Y,Mathf.Atan2(-move.X,-move.Z),1-MathF.Exp(-7*(float)delta)),0);
                }
                GlobalPosition=horse.GlobalPosition+Vector3.Up*1.15f;Model.State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];Velocity=Vector3.Zero;return;
            }
        }
        boundaryNoticeCooldown=Math.Max(0,boundaryNoticeCooldown-delta);
        var inside=KeepInsideWorld(GlobalPosition);
        if(inside!=GlobalPosition)GlobalPosition=inside;
        void SyncPosition()=>Model.State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];
        if(!TerrainReady(GlobalPosition)){SyncPosition();return;}
        bool crouch=Input.IsActionPressed("crouch");
        if(Crouching&&!crouch&&Model.Solid(ToCell(GlobalPosition+Vector3.Up*1.6f)))crouch=true;
        if(crouch!=Crouching){Crouching=crouch;bodyShape.Height=crouch?1.1f:1.8f;collision.Position=new(0,crouch?.55f:.9f,0);Camera.Position=new(0,crouch?.95f:1.62f,0);}
        Swimming=Model.Catalog.Id(Model.Get(ToCell(GlobalPosition+Vector3.Up*.8f)))=="water";
        Vector2 input=Input.GetVector("left","right","forward","back");Vector3 direction=(Transform.Basis*new Vector3(input.X,0,input.Y)).Normalized();
        float speed=Crouching?2:Input.IsActionPressed("sprint")&&Model.State.Hunger>5?7.2f:4.6f;speed*=(float)ArmorTraits.Mobility(Model.State);if(Swimming)speed=2.8f;if(Flying)speed*=2;
        var velocity=Velocity;velocity.X=direction.X*speed;velocity.Z=direction.Z*speed;
        if(Flying){velocity.Y=(Input.IsActionPressed("jump")?1:0)-(Input.IsActionPressed("crouch")?1:0);velocity.Y*=speed;fallSpeed=0;}
        else if(Swimming){
            // Standing on the bottom of shallow water must still allow a normal
            // jump onto a one-block bank; the swimming rise alone cannot clear it.
            if(IsOnFloor()&&Input.IsActionJustPressed("jump")&&!Crouching)velocity.Y=7.3f;
            else velocity.Y=Mathf.MoveToward(velocity.Y,Input.IsActionPressed("jump")?3:-.6f,(float)delta*8);
            fallSpeed=0;
        }
        else{
            if(!IsOnFloor()&&Input.IsActionJustPressed("jump")&&Model.State.Equipment.Slots[1]?.Id=="elytra")Gliding=!Gliding;
            if(IsOnFloor()||Model.State.Equipment.Slots[1]?.Id!="elytra")Gliding=false;
            if(Gliding){var look=-Camera.GlobalBasis.Z;float boost=GlideBoost>0?6:0;GlideBoost=Math.Max(0,GlideBoost-delta);velocity.X=look.X*(10+boost);velocity.Z=look.Z*(10+boost);velocity.Y=Mathf.MoveToward(velocity.Y,boost>0?2:Math.Clamp(look.Y*4-2,-8,-.7f),(float)delta*3);fallSpeed=0;glideWearClock+=delta;if(glideWearClock>=1){glideWearClock=0;ToolUse.Wear(Model.State.Equipment,1,Model.Catalog,Model.State.Creative);} }
            else{
            if(!IsOnFloor()){velocity.Y-=22*(float)delta;fallSpeed=Math.Max(fallSpeed,-velocity.Y);}
            else{if(fallSpeed>12)Hurt((fallSpeed-12)*3);fallSpeed=0;velocity.Y=0;if(Input.IsActionJustPressed("jump")&&!Crouching)velocity.Y=7.3f;}
            if(Crouching&&IsOnFloor()){
                var ahead=GlobalPosition+direction*.5f;bool ground=false;for(int depth=1;depth<=2;depth++)ground|=Model.Solid(ToCell(ahead+Vector3.Down*depth));if(!ground){velocity.X=0;velocity.Z=0;}
            }
            }
        }
        velocity+=Impact;Impact=Impact.MoveToward(Vector3.Zero,(float)delta*7);
        float step=(float)delta;if(step<=0)return;
        var next=GlobalPosition+velocity*step;inside=KeepInsideWorld(next);
        if(inside!=next){
            if(inside.X!=next.X)velocity.X=(inside.X-GlobalPosition.X)/step;
            if(inside.Z!=next.Z)velocity.Z=(inside.Z-GlobalPosition.Z)/step;
            if(boundaryNoticeCooldown<=0){BoundaryHit?.Invoke();boundaryNoticeCooldown=2;}
        }
        if(!TerrainReady(inside)){SyncPosition();return;}
        Velocity=velocity;MoveAndSlide();
        inside=KeepInsideWorld(GlobalPosition);if(inside!=GlobalPosition)GlobalPosition=inside;
        if(GlobalPosition.Y<-5)Hurt(1000);
        SyncPosition();
    }
    public void SetReplicaPose(Vector3 position,float yaw,float lookPitch,bool crouching)
    {
        GlobalPosition=KeepInsideWorld(position);Rotation=new(0,yaw,0);pitch=Math.Clamp(lookPitch,-1.53f,1.53f);Crouching=crouching;
        if(Camera!=null){Camera.Current=false;Camera.Rotation=new(pitch,0,0);Camera.Position=new(0,crouching?.95f:1.62f,0);}
        if(collision!=null){bodyShape.Height=crouching?1.1f:1.8f;collision.Position=new(0,crouching?.55f:.9f,0);}
    }
    public override void _ExitTree(){bodyShape?.Dispose();}
    public static Cell ToCell(Vector3 p)=>new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Floor(p.Z));
    public (Cell Hit,Cell Before,float Distance)? Target(float range=6,bool water=false)
    {
        Vector3 origin=Camera.GlobalPosition,direction=-Camera.GlobalTransform.Basis.Z;Cell previous=ToCell(origin);
        for(float d=0;d<=range;d+=.035f){var p=origin+direction*d;var c=ToCell(p);ushort id=Model.Get(c);var def=Model.Catalog.Block(id);
            if(def!=null&&(water||def.Id!="water")){
                Vector3 local=p-new Vector3(c.X,c.Y,c.Z);bool hit=true;
                int rotation=Model.State.Entities.TryGetValue(c.ToString(),out var entity)?entity.Rotation:0;if(entity?.Open==true&&def.Shape=="door")rotation++;
                for(int r=0;r<rotation%4;r++){float oldX=local.X;local.X=local.Z;local.Z=1-oldX;}
                if(def.Shape=="slab"||def.Shape=="stairs")hit=local.Y<.5f||(def.Shape=="stairs"&&local.Z>.5f);
                if(def.Shape=="rug")hit=local.Y<.07f;
                if(def.Shape=="rocks")hit=local.Y<.3f&&local.X>.1f&&local.X<.85f&&local.Z>.15f&&local.Z<.9f;
                if(def.Shape=="torch"){int mount=entity?.Stage??0;float h=local.Y-(mount==0?0:.12f);float tx=mount==1?.05f+h*.34f:mount==2?.95f-h*.34f:.5f,tz=mount==3?.05f+h*.34f:mount==4?.95f-h*.34f:.5f;hit=h>=0&&h<(mount==0?.70f:.60f)&&Math.Abs(local.X-tx)<.10f&&Math.Abs(local.Z-tz)<.10f;}
                if(def.Shape=="bush")hit=local.Y<.95f&&local.X>.1f&&local.X<.9f&&local.Z>.1f&&local.Z<.9f;
                if(def.Id=="wood"&&!Model.Changes.ContainsKey(c)&&entity==null&&Model.Terrain.IsLooseWood(c))hit=local.Y<.31f;
                if(def.Shape is "door" or "window"){
                    hit=local.Z<.16f;
                }
                if(hit)return(c,previous,d);
            }
            if(c!=previous)previous=c;
        }
        return null;
    }
}
