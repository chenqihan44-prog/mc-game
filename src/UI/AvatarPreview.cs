using System;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using Godot;
namespace EmberLighthouse.UI;
public partial class AvatarPreview : Control
{
    public Inventory? Equipment {get;set;}
    public Catalog? Catalog {get;set;}
    private string appearanceId="adventurer";
    public string AppearanceId
    {
        get=>appearanceId;
        set{
            string next=CharacterAppearances.Normalize(value);if(next==appearanceId)return;appearanceId=next;
            if(model!=null&&GodotObject.IsInstanceValid(model)){var parent=model.GetParent();model.QueueFree();model=PresentationModel.Avatar(Equipment,Catalog,appearanceId);parent.AddChild(model);}
        }
    }
    private PresentationModel model=null!;
    private SubViewport viewport=null!;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        viewport=new SubViewport{OwnWorld3D=true,Size=PreviewResolution(),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("202020"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new("dce8e3"),AmbientLightEnergy=.8f}});
        viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-30,-30,0),LightEnergy=1.1f});
        var camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=2.18f,Position=new(2.5f,1.6f,5)};viewport.AddChild(camera);camera.LookAt(new(0,.99f,0));
        model=PresentationModel.Avatar(Equipment,Catalog,AppearanceId);viewport.AddChild(model);
        var image=new TextureRect{Texture=viewport.GetTexture(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(image);
    }
    public override void _Process(double delta)
    {
        var desired=PreviewResolution();if(viewport.Size!=desired)viewport.Size=desired;
        if(Size.X<=0||Size.Y<=0)return;
        var direction=(GetGlobalMousePosition()-GlobalPosition-Size/2)/Size;
        model.Head.Rotation=new(Math.Clamp(direction.Y*.35f,-.25f,.25f),Math.Clamp(direction.X*.45f,-.45f,.45f),0);
    }
    private Vector2I PreviewResolution()=>new(Math.Max(1,(int)Math.Ceiling(Math.Max(Size.X,CustomMinimumSize.X)*2)),Math.Max(1,(int)Math.Ceiling(Math.Max(Size.Y,CustomMinimumSize.Y)*2)));
}
