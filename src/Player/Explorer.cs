using System;
using EmberLighthouse.Core;
using EmberLighthouse.World;
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
    public float Sensitivity {get;set;}=.0023f;
    public bool Flying {get;set;}
    public bool Swimming {get;private set;}
    public bool Crouching {get;private set;}
    private float pitch,fallSpeed;
    private double boundaryNoticeCooldown;
    private CollisionShape3D collision=null!;
    public override void _Ready()
    {
        collision=new(){Shape=new CapsuleShape3D{Radius=BodyRadius,Height=1.8f},Position=new(0,.9f,0)};AddChild(collision);
        Camera=new(){Position=new(0,1.62f,0),Current=true,Fov=78,Far=400};AddChild(Camera);
        FloorSnapLength=.3f;SafeMargin=BodyMargin;
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(!Enabled||!AcceptUserInput)return;
        if(e is InputEventMouseMotion motion&&Input.MouseMode==Input.MouseModeEnum.Captured){RotateY(-motion.Relative.X*Sensitivity);pitch=Math.Clamp(pitch-motion.Relative.Y*Sensitivity,-1.53f,1.53f);Camera.Rotation=new(pitch,0,0);LookMoved?.Invoke(motion.Relative);}
        if(e.IsActionPressed("fly")&&Model.State.Creative)Flying=!Flying;
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!Enabled){Velocity=Vector3.Zero;return;}
        boundaryNoticeCooldown=Math.Max(0,boundaryNoticeCooldown-delta);
        var inside=KeepInsideWorld(GlobalPosition);
        if(inside!=GlobalPosition)GlobalPosition=inside;
        void SyncPosition()=>Model.State.Position=[GlobalPosition.X,GlobalPosition.Y,GlobalPosition.Z];
        if(!TerrainReady(GlobalPosition)){Velocity=Vector3.Zero;SyncPosition();return;}
        bool crouch=Input.IsActionPressed("crouch");
        if(Crouching&&!crouch&&Model.Solid(ToCell(GlobalPosition+Vector3.Up*1.6f)))crouch=true;
        if(crouch!=Crouching){Crouching=crouch;((CapsuleShape3D)collision.Shape).Height=crouch?1.1f:1.8f;collision.Position=new(0,crouch?.55f:.9f,0);Camera.Position=new(0,crouch?.95f:1.62f,0);}
        Swimming=Model.Catalog.Id(Model.Get(ToCell(GlobalPosition+Vector3.Up*.8f)))=="water";
        Vector2 input=Input.GetVector("left","right","forward","back");Vector3 direction=(Transform.Basis*new Vector3(input.X,0,input.Y)).Normalized();
        float speed=Crouching?2:Input.IsActionPressed("sprint")&&Model.State.Hunger>5?7.2f:4.6f;if(Swimming)speed=2.8f;if(Flying)speed*=2;
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
            if(!IsOnFloor()){velocity.Y-=22*(float)delta;fallSpeed=Math.Max(fallSpeed,-velocity.Y);}
            else{if(fallSpeed>12)Hurt((fallSpeed-12)*3);fallSpeed=0;velocity.Y=0;if(Input.IsActionJustPressed("jump")&&!Crouching)velocity.Y=7.3f;}
            if(Crouching&&IsOnFloor()){
                var ahead=GlobalPosition+direction*.5f;bool ground=false;for(int depth=1;depth<=2;depth++)ground|=Model.Solid(ToCell(ahead+Vector3.Down*depth));if(!ground){velocity.X=0;velocity.Z=0;}
            }
        }
        float step=(float)delta;if(step<=0)return;
        var next=GlobalPosition+velocity*step;inside=KeepInsideWorld(next);
        if(inside!=next){
            if(inside.X!=next.X)velocity.X=(inside.X-GlobalPosition.X)/step;
            if(inside.Z!=next.Z)velocity.Z=(inside.Z-GlobalPosition.Z)/step;
            if(boundaryNoticeCooldown<=0){BoundaryHit?.Invoke();boundaryNoticeCooldown=2;}
        }
        if(!TerrainReady(inside)){Velocity=Vector3.Zero;SyncPosition();return;}
        Velocity=velocity;MoveAndSlide();
        inside=KeepInsideWorld(GlobalPosition);if(inside!=GlobalPosition)GlobalPosition=inside;
        if(GlobalPosition.Y<-5)Hurt(1000);
        SyncPosition();
    }
    public static Cell ToCell(Vector3 p)=>new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Floor(p.Z));
    public (Cell Hit,Cell Before,float Distance)? Target(float range=6)
    {
        Vector3 origin=Camera.GlobalPosition,direction=-Camera.GlobalTransform.Basis.Z;Cell previous=ToCell(origin);
        for(float d=0;d<=range;d+=.035f){var p=origin+direction*d;var c=ToCell(p);ushort id=Model.Get(c);var def=Model.Catalog.Block(id);
            if(def!=null&&def.Id!="water"){
                Vector3 local=p-new Vector3(c.X,c.Y,c.Z);bool hit=true;
                int rotation=Model.State.Entities.TryGetValue(c.ToString(),out var entity)?entity.Rotation:0;if(entity?.Open==true&&def.Shape=="door")rotation++;
                for(int r=0;r<rotation%4;r++){float oldX=local.X;local.X=local.Z;local.Z=1-oldX;}
                if(def.Shape=="slab"||def.Shape=="stairs")hit=local.Y<.5f||(def.Shape=="stairs"&&local.Z>.5f);
                if(def.Shape=="rug")hit=local.Y<.07f;
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
