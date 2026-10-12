using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.UI;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private static IEnumerable<Node> UiDescendants(Node node)
    {
        foreach(var child in node.GetChildren()){yield return child;foreach(var nested in UiDescendants(child))yield return nested;}
    }
    private void DrawCursorItem()
    {
        cursorGlyph=null;if(world?.State.Cursor==null)return;
        cursorGlyph=new(){Catalog=catalog,Item=world.State.Cursor,Size=new(54,54),PixelScale=2.7f,Position=GetViewport().GetMousePosition()-Vector2.One*24,ZIndex=20,PivotOffset=new(27,27),Scale=Vector2.One*.86f};overlay.AddChild(cursorGlyph);
        cursorGlyph.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).TweenProperty(cursorGlyph,"scale",Vector2.One,.12);
    }
    private void AnimateSlotArrival(int surface,int index)
    {
        var target=UiDescendants(overlay).OfType<SlotButton>().FirstOrDefault(b=>b.SurfaceIndex==surface&&b.SlotIndex==index);
        var item=Surface(surface)?.Slots[index];if(target==null||item==null)return;
        var glyph=new ItemGlyph{Catalog=catalog,Item=item.Copy(),Size=target.Size,PixelScale=target.Size.X/20,Position=GetViewport().GetMousePosition()-target.Size/2,ZIndex=30,PivotOffset=target.Size/2};hudRoot.AddChild(glyph);
        var tween=glyph.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(glyph,"position",target.GlobalPosition,.14);tween.TweenProperty(glyph,"scale",Vector2.One*.92f,.14);
        tween.Chain().TweenCallback(Callable.From(()=>glyph.QueueFree()));
    }
    private static void ExclusiveModes(CheckButton creative,CheckButton casual)
    {
        creative.Toggled+=on=>{if(on)casual.SetPressedNoSignal(false);};
        casual.Toggled+=on=>{if(on)creative.SetPressedNoSignal(false);};
    }
}
