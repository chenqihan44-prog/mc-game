using System;
using Godot;
namespace EmberLighthouse.UI;

public static class UiFactory
{
    public static readonly Color Gold=new("edc277"),Parchment=new("fff0cb"),Muted=new("c4b9a3");
    public static float FontScale=1;
    public static Theme Theme()
    {
        var theme=new Theme{DefaultFont=new SystemFont{FontNames=["Microsoft YaHei UI","Microsoft YaHei","Segoe UI"]},DefaultFontSize=(int)Math.Round(18*FontScale)};
        foreach(string type in new[]{"Label","Button","CheckButton","CheckBox","LineEdit","TextEdit","OptionButton","PopupMenu"}){
            theme.SetColor("font_color",type,Parchment);theme.SetColor("font_hover_color",type,new("fff8e5"));theme.SetColor("font_focus_color",type,new("fff8e5"));theme.SetColor("font_pressed_color",type,Gold);theme.SetColor("font_disabled_color",type,new("8e8575"));
        }
        theme.SetColor("font_shadow_color","Label",new(0,0,0,.65f));theme.SetConstant("shadow_offset_x","Label",1);theme.SetConstant("shadow_offset_y","Label",1);
        theme.SetStylebox("panel","PanelContainer",Panel());
        foreach(string type in new[]{"Button","OptionButton"}){
            theme.SetStylebox("normal",type,ButtonStyle());theme.SetStylebox("hover",type,ButtonStyle(true));theme.SetStylebox("pressed",type,ButtonStyle(true,true));theme.SetStylebox("disabled",type,ButtonStyle(disabled:true));theme.SetStylebox("focus",type,FocusStyle());
            theme.SetConstant("h_separation",type,14);theme.SetConstant("icon_max_width",type,26);
        }
        foreach(string type in new[]{"LineEdit","TextEdit"}){
            theme.SetStylebox("normal",type,Style("101821df",12));theme.SetStylebox("focus",type,FocusStyle());theme.SetStylebox("read_only",type,Style("101821bf",12));theme.SetColor("caret_color",type,Gold);theme.SetColor("selection_color",type,new("80613b"));theme.SetColor("font_placeholder_color",type,Muted);
        }
        theme.SetStylebox("panel","PopupMenu",Panel());theme.SetStylebox("hover","PopupMenu",ButtonStyle(true));
        foreach(string type in new[]{"HSlider","VSlider"}){
            var track=Style("101821e0",4);track.CornerRadiusTopLeft=track.CornerRadiusTopRight=track.CornerRadiusBottomLeft=track.CornerRadiusBottomRight=4;
            var filled=Style("edb962",4);filled.BorderColor=new("ffe2a1");
            theme.SetStylebox("slider",type,track);theme.SetStylebox("grabber_area",type,filled);theme.SetStylebox("grabber_area_highlight",type,filled);
            theme.SetIcon("grabber",type,UiSymbols.Texture("knob"));theme.SetIcon("grabber_highlight",type,UiSymbols.Texture("knob"));theme.SetIcon("grabber_disabled",type,UiSymbols.Texture("knob-off"));
        }
        foreach(string type in new[]{"VScrollBar","HScrollBar"}){
            theme.SetStylebox("scroll",type,Style("101821a0",3));theme.SetStylebox("grabber",type,Style("b89154",4));theme.SetStylebox("grabber_highlight",type,Style("f0c775",4));theme.SetStylebox("grabber_pressed",type,Style("edb962",4));
        }
        foreach(string type in new[]{"CheckButton","CheckBox"}){
            theme.SetStylebox("normal",type,Style("111b2480",8));theme.SetStylebox("hover",type,ButtonStyle(true));theme.SetStylebox("pressed",type,ButtonStyle(true,true));theme.SetStylebox("focus",type,FocusStyle());
            foreach(string suffix in new[]{"","_disabled"}){theme.SetIcon("checked"+suffix,type,UiSymbols.Texture(type=="CheckButton"?"switch-on":"check-on"));theme.SetIcon("unchecked"+suffix,type,UiSymbols.Texture(type=="CheckButton"?"switch-off":"check-off"));}
            theme.SetConstant("h_separation",type,12);
        }
        theme.SetIcon("arrow","OptionButton",UiSymbols.Texture("down"));theme.SetStylebox("panel","TooltipPanel",Panel(12));theme.SetColor("font_color","TooltipLabel",Parchment);theme.SetFontSize("font_size","TooltipLabel",17);return theme;
    }
    private static Color Surface(string value){var color=new Color(value);if(value.Length==6)color.A=.86f;return color;}
    public static StyleBoxFlat Style(string color,int padding)=>new(){BgColor=Surface(color),BorderColor=new("ba955c"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=10,CornerRadiusTopRight=10,CornerRadiusBottomLeft=10,CornerRadiusBottomRight=10,ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    public static StyleBoxFlat Panel(int padding=22){var s=Style("0e1924d9",padding);s.BorderColor=Gold;s.BorderWidthLeft=s.BorderWidthRight=s.BorderWidthTop=s.BorderWidthBottom=2;s.CornerRadiusTopLeft=s.CornerRadiusTopRight=s.CornerRadiusBottomLeft=s.CornerRadiusBottomRight=14;s.ShadowColor=new(.98f,.57f,.13f,.3f);s.ShadowSize=10;return s;}
    private static StyleBoxFlat ButtonStyle(bool hover=false,bool pressed=false,bool disabled=false){var s=Style(disabled?"111a24d0":pressed?"5b3a20ed":hover?"624426df":"18232bbf",12);s.BorderColor=disabled?new("6c6251"):hover?new("ffe2a0"):Gold;s.ShadowColor=new(1,.62f,.2f,hover?.4f:.08f);s.ShadowSize=hover?10:3;return s;}
    private static StyleBoxFlat FocusStyle(){var s=Style("00000000",0);s.DrawCenter=false;s.BorderColor=new("fff0cb");s.BorderWidthLeft=s.BorderWidthRight=s.BorderWidthTop=s.BorderWidthBottom=2;return s;}
    public static Label Label(string text,int size=17){var l=new Label{Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};l.SetMeta("ui_font_size",size);l.AddThemeFontSizeOverride("font_size",(int)Math.Round(size*FontScale));return l;}
    public static void ApplyFontScale(Control root,float scale){FontScale=scale;void Visit(Node node){if(node is Control control&&control.HasMeta("ui_font_size"))control.AddThemeFontSizeOverride("font_size",(int)Math.Round(control.GetMeta("ui_font_size").AsInt32()*scale));foreach(var child in node.GetChildren())Visit(child);}Visit(root);}
    public static Button Button(string text,Action action){var b=new Button{Text=text,CustomMinimumSize=new(0,44),FocusMode=Control.FocusModeEnum.All};b.Pressed+=action;return b;}
    public static void Primary(Button button){button.SetMeta("ui_primary",true);var normal=ButtonStyle(true);normal.BgColor=new("714622c9");normal.BorderWidthLeft=normal.BorderWidthRight=normal.BorderWidthTop=normal.BorderWidthBottom=2;normal.ShadowSize=12;button.AddThemeStyleboxOverride("normal",normal);button.AddThemeStyleboxOverride("hover",ButtonStyle(true));button.AddThemeStyleboxOverride("pressed",ButtonStyle(true,true));}
    public static void DecorateAction(Button button,string iconKey,bool primary=false){button.SetMeta("ui_icon",iconKey);button.Icon=UiSymbols.Texture(iconKey);button.ExpandIcon=true;button.AddThemeConstantOverride("icon_max_width",26);if(primary)Primary(button);}
    public static VBoxContainer Column(Control parent){var b=new VBoxContainer();b.AddThemeConstantOverride("separation",10);parent.AddChild(b);return b;}
    public static HBoxContainer Row(Control parent){var b=new HBoxContainer();b.AddThemeConstantOverride("separation",12);parent.AddChild(b);return b;}
    public static void Heading(Control parent,string text){var l=Label(text,22);l.AddThemeColorOverride("font_color",Gold);parent.AddChild(l);parent.AddChild(new UiOrnament{Kind="divider",CustomMinimumSize=new(0,10),MouseFilter=Control.MouseFilterEnum.Ignore});}
    public static Texture2D? BackdropTexture(bool night=false)=>ResourceLoader.Exists("res://assets/ui/"+(night?"lighthouse-night.png":"lighthouse-sunset.png"))?ResourceLoader.Load<Texture2D>("res://assets/ui/"+(night?"lighthouse-night.png":"lighthouse-sunset.png")):null;
    public static void Backdrop(Control root,bool night=false,float dim=.12f){
        var shade=new ColorRect{Name="ScreenShade",Color=new(.025f,.037f,.055f,dim),MouseFilter=Control.MouseFilterEnum.Stop};shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(shade);
        var image=new TextureRect{Name=night?"LanternBackdropNight":"LanternBackdropDay",Texture=BackdropTexture(night),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=-1};image.SetMeta("ui_backdrop",night?"night":"day");image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(image);
    }
    public static Control Screen(Control root,string title){
        foreach(var c in root.GetChildren()){root.RemoveChild(c);c.QueueFree();}Backdrop(root);
        var center=new LanternScreenFrame();center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(center);
        var scroll=new ScrollContainer{Name="ScreenOverflow",HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Auto,FollowFocus=true};center.AddChild(scroll);
        var panel=new PanelContainer{CustomMinimumSize=new(Math.Clamp(root.GetViewportRect().Size.X*.68f,650,1120),0),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(panel);
        var col=Column(panel);var label=Label(title,36);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;label.AddThemeFontOverride("font",new SystemFont{FontNames=["SimSun","Microsoft YaHei"],FontWeight=700});label.AddThemeColorOverride("font_color",Parchment);col.AddChild(label);col.AddChild(new UiOrnament{Kind="horizon",CustomMinimumSize=new(0,26),MouseFilter=Control.MouseFilterEnum.Ignore});center.Panel=panel;center.Scroller=scroll;center.Refresh();return col;
    }
    public static ScrollContainer Scroll(Control parent,int height=400){var viewport=parent.GetViewportRect().Size;var s=new ScrollContainer{CustomMinimumSize=new(0,Math.Min(height,Math.Max(160,viewport.Y*.47f))),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};parent.AddChild(s);return s;}
    public static void FitBody(Control column,int footerCount=1){var children=column.GetChildren();int end=children.Count-footerCount;if(end<=2)return;var scroll=new ScrollContainer{Name="FormBody",CustomMinimumSize=new(0,350),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};column.AddChild(scroll);column.MoveChild(scroll,2);var body=Column(scroll);body.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;for(int i=2;i<end;i++){var child=children[i];column.RemoveChild(child);body.AddChild(child);if(child is Label label)label.AutowrapMode=TextServer.AutowrapMode.WordSmart;if(child is CheckButton toggle){toggle.ClipText=true;toggle.TooltipText=toggle.Text;}}}
}
