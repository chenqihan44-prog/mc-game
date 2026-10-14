using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace EmberLighthouse.UI;
public partial class LanternScreenFrame : CenterContainer
{
    public PanelContainer? Panel;
    public ScrollContainer? Scroller;
    public bool FitRemoteContent {get;set;}
    private readonly Dictionary<Control,Vector2> originalCanvas=[];
    private readonly Dictionary<Control,(Vector2 Minimum,Vector2 Size)> originalSlots=[];
    private readonly Dictionary<Control,(Vector2 Position,Vector2 Size,Vector2 Minimum,int Font,float Pixel)> originalChildren=[];
    public override void _Process(double delta)=>Refresh();
    public void BindContent()
    {
        Scroller=GetChildren().OfType<ScrollContainer>().FirstOrDefault();
        Panel=Scroller?.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        if(Scroller!=null)Scroller.FollowFocus=true;
        Refresh();
    }
    public void Refresh()
    {
        if(Panel==null||Scroller==null||!IsInsideTree())return;
        var extent=GetViewportRect().Size;float width=Math.Max(280,extent.X-56),height=Math.Max(180,extent.Y-56);
        if(FitRemoteContent)FitContents(width,height);
        var min=Panel.GetCombinedMinimumSize();
        if(min.Y>height&&Panel.GetChildCount()>0){foreach(var child in Panel.GetChild(0).GetChildren())if(child is ScrollContainer inner){var size=inner.CustomMinimumSize;size.Y=Math.Max(90,size.Y-(min.Y-height)-2);inner.CustomMinimumSize=size;break;}min=Panel.GetCombinedMinimumSize();}
        // Never inherit a stale zero-height server scroller from a hidden menu.
        var desired=new Vector2(Math.Min(min.X,width),Math.Min(Math.Max(1,min.Y),height));
        if(Scroller.CustomMinimumSize.DistanceSquaredTo(desired)>.5f)Scroller.CustomMinimumSize=desired;
    }
    private void FitContents(float width,float height)
    {
        if(Panel==null||Panel.GetChildCount()==0)return;
        var children=Panel.GetChild(0).GetChildren().OfType<Control>().ToArray();
        var canvases=children.Where(c=>c.GetType()==typeof(Control)&&c.CustomMinimumSize.X>0&&c.CustomMinimumSize.Y>0).ToArray();
        var grids=children.OfType<GridContainer>().Where(g=>g.GetChildren().OfType<Control>().All(c=>c is EmberLighthouse.Networking.RemoteSlotButton)).ToArray();
        foreach(var canvas in canvases)if(!originalCanvas.ContainsKey(canvas))originalCanvas[canvas]=canvas.CustomMinimumSize;
        foreach(var grid in grids)foreach(var slot in grid.GetChildren().OfType<Control>())
            if(!originalSlots.ContainsKey(slot))originalSlots[slot]=(slot.CustomMinimumSize,slot.Size);
        float originalHeight=canvases.Sum(c=>originalCanvas[c].Y),currentHeight=canvases.Sum(c=>c.CustomMinimumSize.Y),originalWidth=canvases.Length>0?canvases.Max(c=>originalCanvas[c].X):0;
        foreach(var grid in grids){int rows=(grid.GetChildCount()+grid.Columns-1)/grid.Columns;var slots=grid.GetChildren().OfType<Control>().ToArray();
            if(slots.Length==0)continue;originalHeight+=rows*slots.Max(c=>originalSlots[c].Minimum.Y);currentHeight+=grid.GetCombinedMinimumSize().Y;originalWidth=Math.Max(originalWidth,grid.Columns*slots.Max(c=>originalSlots[c].Minimum.X));}
        if(originalHeight<=0)return;
        float chrome=Math.Max(0,Panel.GetCombinedMinimumSize().Y-currentHeight);
        float horizontalPadding=Panel.GetThemeStylebox("panel").GetMinimumSize().X;
        float ratio=Math.Clamp(Math.Min(1,Math.Min((height-chrome-4)/originalHeight,(width-horizontalPadding-4)/Math.Max(1,originalWidth))),.35f,1);
        foreach(var canvas in canvases){var minimum=originalCanvas[canvas]*ratio;canvas.Scale=Vector2.One;if(canvas.CustomMinimumSize.DistanceSquaredTo(minimum)>.01f)canvas.CustomMinimumSize=minimum;FitAbsoluteChildren(canvas,ratio);}
        foreach(var grid in grids)foreach(var slot in grid.GetChildren().OfType<Control>()){
            var minimum=originalSlots[slot].Minimum*ratio;if(slot.CustomMinimumSize.DistanceSquaredTo(minimum)>.01f)slot.CustomMinimumSize=minimum;
            foreach(var glyph in slot.GetChildren().OfType<ItemGlyph>()){var size=originalSlots[slot].Size*ratio;if(size.X<=0||size.Y<=0)size=minimum;if(glyph.Size.DistanceSquaredTo(size)>.01f)glyph.Size=size;glyph.PixelScale=minimum.X/20;}}
    }
    private void FitAbsoluteChildren(Control parent,float ratio)
    {
        foreach(var child in parent.GetChildren().OfType<Control>()){
            if(!originalChildren.TryGetValue(child,out var original)){
                original=(child.Position,new Vector2(Math.Max(child.Size.X,child.GetCombinedMinimumSize().X),Math.Max(child.Size.Y,child.GetCombinedMinimumSize().Y)),child.CustomMinimumSize,child.GetThemeFontSize("font_size"),child is ItemGlyph glyph?glyph.PixelScale:0);
                originalChildren.Add(child,original);
            }
            // Resize coordinates rather than scaling the parent Control: native
            // hit rectangles and GetGlobalRect must agree with rendered slots.
            var minimum=original.Minimum*ratio;if(child.CustomMinimumSize.DistanceSquaredTo(minimum)>.01f)child.CustomMinimumSize=minimum;
            int font=Math.Max(8,(int)Math.Round(original.Font*ratio));if(child.GetThemeFontSize("font_size")!=font)child.AddThemeFontSizeOverride("font_size",font);
            child.Position=original.Position*ratio;child.Size=original.Size*ratio;
            if(child is ItemGlyph icon)icon.PixelScale=original.Pixel*ratio;
            if(child is not ItemGlyph and not AvatarPreview)FitAbsoluteChildren(child,ratio);
        }
    }
}
