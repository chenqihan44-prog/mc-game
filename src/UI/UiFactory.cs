using System;
using Godot;

namespace EmberLighthouse.UI;

public static class UiFactory
{
    public static Theme Theme()
    {
        var theme=new Theme{DefaultFont=new SystemFont{FontNames=["Microsoft YaHei UI","Microsoft YaHei","Segoe UI"]},DefaultFontSize=17};
        theme.SetColor("font_color","Label",new("e2e7db"));theme.SetColor("font_color","Button",new("e2e7db"));
        theme.SetColor("font_shadow_color","Label",new(0,0,0,.7f));theme.SetConstant("shadow_offset_x","Label",1);theme.SetConstant("shadow_offset_y","Label",1);
        theme.SetStylebox("panel","PanelContainer",Style("182625",16));
        theme.SetStylebox("normal","Button",Style("31403a",10));theme.SetStylebox("hover","Button",Style("4b6251",10));theme.SetStylebox("pressed","Button",Style("627c5b",10));
        theme.SetStylebox("normal","LineEdit",Style("27332f",10));theme.SetStylebox("normal","TextEdit",Style("27332f",10));
        return theme;
    }
    public static StyleBoxFlat Style(string color,int padding)
    {
        return new(){BgColor=new(color),CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    }
    public static Label Label(string text,int size=17){var l=new Label{Text=text};l.AddThemeFontSizeOverride("font_size",size);return l;}
    public static Button Button(string text,Action action){var b=new Button{Text=text,CustomMinimumSize=new(0,40)};b.Pressed+=action;return b;}
    public static VBoxContainer Column(Control parent){var b=new VBoxContainer();b.AddThemeConstantOverride("separation",9);parent.AddChild(b);return b;}
    public static HBoxContainer Row(Control parent){var b=new HBoxContainer();b.AddThemeConstantOverride("separation",9);parent.AddChild(b);return b;}
    public static Control Screen(Control root,string title)
    {
        foreach(var c in root.GetChildren()){root.RemoveChild(c);c.QueueFree();}
        var shade=new ColorRect{Color=new(0.035f,.06f,.055f,.94f),MouseFilter=Control.MouseFilterEnum.Stop};shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(shade);
        var center=new CenterContainer();center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(center);
        var panel=new PanelContainer{CustomMinimumSize=new(650,0)};center.AddChild(panel);var col=Column(panel);col.AddChild(Label(title,30));return col;
    }
    public static ScrollContainer Scroll(Control parent,int height=400){var s=new ScrollContainer{CustomMinimumSize=new(620,height)};parent.AddChild(s);return s;}
}
