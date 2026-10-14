using Godot;
namespace EmberLighthouse.UI;
public partial class UiOrnament : Control
{
    public string Kind="horizon";
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;Resized+=QueueRedraw;}
    public override void _Draw(){var gold=UiFactory.Gold;float mid=Size.Y*.55f;DrawLine(new(0,mid),new(Size.X,mid),new(gold.R,gold.G,gold.B,.5f),1,true);DrawColoredPolygon([new(3,mid-3),new(6,mid),new(3,mid+3),new(0,mid)],gold);if(Kind!="horizon")return;float x=Size.X-94;DrawPolyline([new(x,mid),new(x+7,mid-18),new(x+13,mid-18),new(x+20,mid)],gold,1.4f,true);DrawRect(new(x+5,mid-22,10,5),gold,false,1);DrawLine(new(x+10,mid-27),new(x+3,mid-22),gold,1,true);DrawLine(new(x+10,mid-27),new(x+17,mid-22),gold,1,true);foreach(float tx in new[]{x-22,x-44,x+42}){DrawLine(new(tx,mid),new(tx,mid-16),gold,1,true);for(int k=0;k<3;k++){float y=mid-13+k*4;DrawLine(new(tx,y-4),new(tx-5,y),gold,1,true);DrawLine(new(tx,y-4),new(tx+5,y),gold,1,true);}}}
}
