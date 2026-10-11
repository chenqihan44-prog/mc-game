using System;
using Godot;
namespace EmberLighthouse.UI;
public partial class SlotButton : Button
{
    public int SlotIndex {get;set;}
    public bool Container {get;set;}
    public int SurfaceIndex {get;set;}
    public Action<int,int,int,int>? Transfer {get;set;}
    public override Variant _GetDragData(Vector2 position)
    {
        var label=new Label{Text=Text};SetDragPreview(label);
        return new Godot.Collections.Dictionary{{"slot",SlotIndex},{"surface",SurfaceIndex}};
    }
    public override bool _CanDropData(Vector2 position,Variant data){if(data.VariantType!=Variant.Type.Dictionary)return false;var d=data.AsGodotDictionary();return d.ContainsKey("slot")&&d.ContainsKey("surface")&&d["slot"].VariantType==Variant.Type.Int&&d["surface"].VariantType==Variant.Type.Int;}
    public override void _DropData(Vector2 position,Variant data){if(!_CanDropData(position,data))return;var d=data.AsGodotDictionary();Transfer?.Invoke(d["surface"].AsInt32(),d["slot"].AsInt32(),SurfaceIndex,SlotIndex);}
}
