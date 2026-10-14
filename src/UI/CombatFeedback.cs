using System;
using Godot;
namespace EmberLighthouse.UI;

public partial class CombatFeedback : Control
{
    public double Remaining {get;private set;}
    public string TargetName {get;private set;}="";
    public double Damage {get;private set;}
    public double Health {get;private set;}
    public double Maximum {get;private set;}
    public int ConfirmedHits {get;private set;}
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);}
    public void Confirm(string name,double damage,double health,double maximum){TargetName=name;Damage=damage;Health=health;Maximum=maximum;Remaining=2.5;ConfirmedHits++;QueueRedraw();}
    public void Reset(){Remaining=0;QueueRedraw();}
    public override void _Process(double delta){if(Remaining<=0)return;Remaining=Math.Max(0,Remaining-delta);QueueRedraw();}
    public override void _Draw()
    {
        if(Remaining<=0)return;float a=(float)Math.Min(1,Remaining/.35),cx=Size.X/2,cy=Size.Y/2;
        Color color=Damage>0?new(1,.83f,.48f,a):new(.65f,.85f,1,a);
        if(Remaining>2.15)foreach(var dir in new[]{new Vector2(1,1),new(1,-1),new(-1,1),new(-1,-1)})DrawLine(new Vector2(cx,cy)+dir*7,new Vector2(cx,cy)+dir*12,color,2,true);
        string text=Damage>0?$"{TargetName}  −{Damage:0.#}  ·  {Health:0.#}/{Maximum:0.#}":$"{TargetName}  ·  护盾阻挡";
        var font=ThemeDB.FallbackFont;int fs=18;float width=font.GetStringSize(text,HorizontalAlignment.Left,-1,fs).X;
        var position=new Vector2(cx-width/2,cy+49);DrawStringOutline(font,position,text,HorizontalAlignment.Left,-1,fs,4,new(0,0,0,a));DrawString(font,position,text,HorizontalAlignment.Left,-1,fs,color);
        float bar=Math.Min(230,Size.X*.65f);DrawRect(new(cx-bar/2,cy+60,bar,6),new(0,0,0,.7f*a));DrawRect(new(cx-bar/2+1,cy+61,(bar-2)*(float)Math.Clamp(Health/Math.Max(1,Maximum),0,1),4),color);
    }
}
