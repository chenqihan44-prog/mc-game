using Godot;
namespace EmberLighthouse.UI;
public partial class MiningProgress : Control
{
    public double Progress {get;set;}
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);}
    public override void _Draw()
    {
        if(Progress<=0)return;var origin=Size/2+new Vector2(-42,20);DrawRect(new(origin-Vector2.One,new(86,8)),new("79603b"));DrawRect(new(origin,new(84,6)),new("21190f"));DrawRect(new(origin,new((float)(84*Mathf.Clamp(Progress,0,1)),6)),UiFactory.Gold);
    }
}
