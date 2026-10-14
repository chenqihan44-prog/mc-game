using System;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using Godot;
namespace EmberLighthouse.UI;

public partial class HeldItemView : Control
{
    public Catalog Catalog {get;set;}=null!;
    public string AppearanceId {get;set;}="adventurer";
    private SubViewport viewport=null!;
    private Node3D right=null!,left=null!;
    private PresentationModel? mainItem,offItem;
    private PresentationModel? rightArm,leftArm;
    private string renderedAppearance="";
    private string mainId="",offId="";
    private float phase,miningPhase,swing,offSwing,equip=1;
    private Vector2 look;
    public float SwingAmount=>swing;
    public string MainItemId=>mainId;
    public bool RenderingActive=>viewport!=null&&viewport.RenderTargetUpdateMode!=SubViewport.UpdateMode.Disabled;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;Visible=false;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        viewport=new(){OwnWorld3D=true,TransparentBg=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled,Size=new(1280,720)};AddChild(viewport);
        var image=new TextureRect{Texture=viewport.GetTexture(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(image);
        viewport.AddChild(new Camera3D{Current=true,Fov=70,Near=.03f,Far=10});viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-25,-30,0),LightEnergy=1.15f});viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new("d5e1dd"),AmbientLightEnergy=.8f,BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=Colors.Transparent}});
        right=new();left=new();viewport.AddChild(right);viewport.AddChild(left);
        UpdateArms();
    }
    private void UpdateArms()
    {
        string desired=CharacterAppearances.Normalize(AppearanceId);if(desired==renderedAppearance&&rightArm!=null)return;
        foreach(var arm in new[]{rightArm,leftArm})if(arm!=null){arm.GetParent().RemoveChild(arm);arm.QueueFree();}
        rightArm=PresentationModel.Arm(false,desired);leftArm=PresentationModel.Arm(true,desired);right.AddChild(rightArm);left.AddChild(leftArm);
        foreach(var arm in new[]{rightArm,leftArm}){arm.Position=new(0,-.15f,.16f);arm.RotationDegrees=new(58,0,0);}
        renderedAppearance=desired;
    }
    public void Look(Vector2 delta)=>look=(look+delta*.0009f).Clamp(new Vector2(-.06f,-.04f),new Vector2(.06f,.04f));
    public void Swing(bool offhand=false){if(offhand)offSwing=1;else swing=1;}
    public void SetActive(bool active){Visible=active;if(viewport!=null)viewport.RenderTargetUpdateMode=active?SubViewport.UpdateMode.Always:SubViewport.UpdateMode.Disabled;if(!active)swing=offSwing=0;}
    public void Tick(double delta,Stack? main,Stack? offhand,Vector3 velocity,bool mining,bool active)
    {
        SetActive(active);if(!active)return;
        UpdateArms();
        var size=GetViewportRect().Size;var desired=new Vector2I(Math.Max(1,(int)size.X),Math.Max(1,(int)size.Y));if(viewport.Size!=desired)viewport.Size=desired;
        void SetItem(string id,bool off){var old=off?offItem:mainItem;if(old!=null){old.GetParent().RemoveChild(old);old.QueueFree();}PresentationModel? model=id==""?null:PresentationModel.Item(Catalog.Items[id]);if(model!=null){(off?left:right).AddChild(model);model.Position=new(off?-.04f:.04f,.10f,-.14f);model.RotationDegrees=new(-12,off?22:-22,off?-12:12);}if(off){offItem=model;offId=id;}else{mainItem=model;mainId=id;equip=0;}}
        if((main?.Id??"")!=mainId)SetItem(main?.Id??"",false);if((offhand?.Id??"")!=offId)SetItem(offhand?.Id??"",true);left.Visible=offhand!=null;
        float dt=(float)Math.Min(delta,.1);equip=Math.Min(1,equip+dt*6);swing=Math.Max(0,swing-dt*3.8f);offSwing=Math.Max(0,offSwing-dt*3.8f);look=look.Lerp(Vector2.Zero,Math.Min(1,dt*9));
        float speed=new Vector2(velocity.X,velocity.Z).Length();phase+=dt*(speed>0.2f?speed*2.2f:1);float bob=speed>.2f?MathF.Sin(phase)*.02f:MathF.Sin(phase)*.003f;
        miningPhase+=dt*18;float hit=mining?MathF.Sin(miningPhase)*.5f+.5f:MathF.Sin(swing*MathF.PI);float offHit=MathF.Sin(offSwing*MathF.PI);
        right.Position=new(.57f+look.X-hit*.10f,-.37f+bob-(1-equip)*.32f+hit*.06f,-.86f+look.Y+hit*.08f);right.RotationDegrees=new(-hit*28, -12-hit*16, -10+hit*29);
        left.Position=new(-.57f-look.X,-.4f-bob+offHit*.06f,-.9f+offHit*.08f);left.RotationDegrees=new(-offHit*28,12+offHit*16,10-offHit*29);
    }
}
