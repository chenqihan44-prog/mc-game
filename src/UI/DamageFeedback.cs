using System;
using Godot;

namespace EmberLighthouse.UI;

public partial class DamageFeedback : ColorRect
{
    public const double FadeSeconds=.3;
    public const double MinimumInterval=.65;
    public const float PeakOpacity=.12f;
    private double remaining,cooldown;
    private bool flashEnabled=true;
    public bool FlashEnabled {
        get=>flashEnabled;
        set{flashEnabled=value;if(!value)Reset();}
    }
    public override void _Ready()
    {
        Name="DamageFeedback";MouseFilter=MouseFilterEnum.Ignore;FocusMode=FocusModeEnum.None;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);Reset();
    }
    public bool Trigger()
    {
        if(!flashEnabled||cooldown>0)return false;
        remaining=FadeSeconds;cooldown=MinimumInterval;SetOpacity(PeakOpacity);return true;
    }
    public override void _Process(double delta)
    {
        cooldown=Math.Max(0,cooldown-delta);remaining=Math.Max(0,remaining-delta);
        SetOpacity((float)(PeakOpacity*remaining/FadeSeconds));
    }
    public void Reset(){remaining=0;cooldown=0;SetOpacity(0);}
    private void SetOpacity(float alpha){Color=new(.85f,.03f,.03f,alpha);Visible=alpha>0;}
}
