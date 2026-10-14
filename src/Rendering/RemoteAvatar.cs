using System;
using System.Linq;
using EmberLighthouse.Items;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class RemoteAvatar : Node3D
{
    public string Nickname {get;set;}="朋友";
    public string AppearanceId {get;set;}="adventurer";
    public Catalog? Catalog {get;set;}
    public Func<Vector3,Color>? WorldLight {get;set;}
    private PresentationModel? model;
    private Label3D label=null!;
    private Vector3 targetPosition;
    private float targetYaw,targetPitch;
    private bool crouching,hasState;
    private double walkClock;
    private Inventory? equipment;
    private string equipmentKey="";
    private string renderedAppearance="";
    private float crouchBlend;
    public Vector3 LatestPosition=>targetPosition;
    public float LatestYaw=>targetYaw;
    public float CrouchBlend=>crouchBlend;
    public PresentationModel? Model=>model;
    public override void _Ready()
    {
        label=new(){Text=Nickname,Position=new(0,2.2f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=.004f};AddChild(label);
        Rebuild();
        if(hasState){GlobalPosition=targetPosition;Rotation=new(0,targetYaw,0);}
    }
    public void UpdateState(Vector3 position,float yaw,float pitch,bool isCrouching,Inventory? worn)
    {
        if(!float.IsFinite(position.X)||!float.IsFinite(position.Y)||!float.IsFinite(position.Z)||!float.IsFinite(yaw)||!float.IsFinite(pitch))return;
        targetPosition=position;targetYaw=yaw;targetPitch=Math.Clamp(pitch,-1.53f,1.53f);crouching=isCrouching;
        if(!hasState&&IsInsideTree()){GlobalPosition=position;Rotation=new(0,yaw,0);}
        hasState=true;
        if(worn!=null){string key=string.Join("|",worn.Slots.Select(s=>s==null?"":$"{s.Id}:{s.Dye}:{s.HeatLining}:{s.Protection}"));if(key!=equipmentKey){equipmentKey=key;equipment=worn.Copy();if(IsNodeReady())Rebuild();}}
    }
    private void Rebuild()
    {
        if(model!=null){RemoveChild(model);model.QueueFree();}
        model=PresentationModel.Avatar(equipment,Catalog,AppearanceId);renderedAppearance=AppearanceId;model.Rotation=new(0,Mathf.Pi,0);AddChild(model);
    }
    public override void _Process(double delta)
    {
        if(!hasState||model==null)return;
        if(renderedAppearance!=AppearanceId)Rebuild();
        var previous=GlobalPosition;float weight=1-MathF.Exp(-14*(float)delta);
        GlobalPosition=previous.DistanceSquaredTo(targetPosition)>64?targetPosition:previous.Lerp(targetPosition,weight);
        Rotation=new(0,Mathf.LerpAngle(Rotation.Y,targetYaw,weight),0);
        var movement=GlobalPosition-previous;movement.Y=0;float speed=delta>0?movement.Length()/(float)delta:0;
        if(speed>.12f)walkClock+=delta*Math.Min(speed,8)*2;
        crouchBlend=Mathf.Lerp(crouchBlend,crouching?1:0,weight);
        float torsoLean=.75f*crouchBlend;float swing=speed>.12f?MathF.Sin((float)walkClock)*Mathf.Lerp(.5f,.18f,crouchBlend):0;
        // Pose a rigid torso and two-piece legs: no geometry is compressed.
        model.Scale=Vector3.One;model.Position=new(0,-.34f*crouchBlend,.09f*crouchBlend);
        model.Torso.Rotation=new(torsoLean,0,0);model.Head.Position=new(0,.68f-.08f*crouchBlend,.02f*crouchBlend);
        model.RightArm.Rotation=new(swing-torsoLean*.65f,0,0);model.LeftArm.Rotation=new(-swing-torsoLean*.65f,0,0);
        model.Head.Rotation=new(-targetPitch-torsoLean,0,0);
        model.RightLeg.Rotation=new(-swing-crouchBlend,0,0);model.LeftLeg.Rotation=new(swing-crouchBlend,0,0);
        model.RightLowerLeg.Rotation=new(1.65f*crouchBlend,0,0);model.LeftLowerLeg.Rotation=new(1.65f*crouchBlend,0,0);
        label.Position=new(0,Mathf.Lerp(2.2f,1.7f,crouchBlend),0);label.Text=Nickname;
        if(WorldLight!=null)model.SetWorldLight(WorldLight(GlobalPosition+Vector3.Up));
    }
}
