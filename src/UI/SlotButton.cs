using System;
using EmberLighthouse.Items;
using Godot;
namespace EmberLighthouse.UI;
public partial class SlotButton : Button
{
    public int SlotIndex {get;set;}
    public bool Container {get;set;}
    public int SurfaceIndex {get;set;}
    public Action<int,int,int,int>? Transfer {get;set;}
    public Catalog Catalog {get;set;}=null!;
    public Stack? Item {get;set;}
    public Func<bool>? DragAllowed {get;set;}
    private bool dragging;
    public override Control _MakeCustomTooltip(string forText)
    {
        var panel=new PanelContainer{Theme=UiFactory.Theme(),MouseFilter=MouseFilterEnum.Ignore};panel.AddThemeStyleboxOverride("panel",panel.Theme.GetStylebox("panel","TooltipPanel"));
        string details=forText;if(Item!=null){var def=Catalog.Items[Item.Id];if(def.Use is "pick" or "axe" or "shovel" or "sword")details+="\n"+(def.Use switch{"pick"=>MiningRules.HarvestTier(def) switch{1=>"可采石块与煤矿；铁矿需要石镐",2=>"可采铁矿；晶矿需要铁镐",_=>"可采石块、铁矿与晶矿"},"axe"=>"砍伐木材更快","shovel"=>"挖土、砂与碎石更快",_=>"近战武器"});}
        if(Item?.HeatLining==true)details+="\n已安装耐热衬片 · 降低熔炉喷口伤害";
        if(Item!=null){var d=Catalog.Items[Item.Id];if(d.Description!="")details+="\n"+d.Description;if(d.Use=="armor")details+="\n护甲 "+d.ArmorPoints+" · "+ArmorTraits.Describe(d);if(Item.Protection>0||Item.Unbreaking>0)details+=$"\n附魔：保护 {Item.Protection} / 耐久 {Item.Unbreaking}";if(Item.Dye!="")details+="\n染色："+(Item.Dye=="red"?"红":"蓝");}
        var label=UiFactory.Label(details,19);label.AddThemeColorOverride("font_color",Colors.White);label.AddThemeColorOverride("font_shadow_color",Colors.Black);panel.AddChild(label);return panel;
    }
    public override Variant _GetDragData(Vector2 position)
    {
        if(Item==null||DragAllowed?.Invoke()==false)return default;
        SetDragPreview(CreateItemDragPreview());
        dragging=true;foreach(var child in GetChildren())if(child is ItemGlyph source)source.Modulate=new(1,1,1,.25f);
        return new Godot.Collections.Dictionary{{"slot",SlotIndex},{"surface",SurfaceIndex}};
    }
    public Control CreateItemDragPreview()
    {
        var preview=new Control{MouseFilter=MouseFilterEnum.Ignore};
        var shadow=new ItemGlyph{Catalog=Catalog,Item=Item!.Copy(),Size=Size,Position=-Size/2+new Vector2(3,5),Modulate=new(0,0,0,.35f),PixelScale=Size.X/20};preview.AddChild(shadow);
        var glyph=new ItemGlyph{Catalog=Catalog,Item=Item.Copy(),Size=Size,Position=-Size/2,PixelScale=Size.X/20,PivotOffset=Size/2,Scale=Vector2.One*.85f};preview.AddChild(glyph);
        preview.Ready+=()=>preview.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).TweenProperty(glyph,"scale",Vector2.One*1.08f,.12);
        return preview;
    }
    public override void _Notification(int what)
    {
        if(what!=NotificationDragEnd||!dragging)return;dragging=false;
        foreach(var child in GetChildren())if(child is ItemGlyph source)source.Modulate=Colors.White;
    }
    public override bool _CanDropData(Vector2 position,Variant data){if(data.VariantType!=Variant.Type.Dictionary)return false;var d=data.AsGodotDictionary();return d.ContainsKey("slot")&&d.ContainsKey("surface")&&d["slot"].VariantType==Variant.Type.Int&&d["surface"].VariantType==Variant.Type.Int;}
    public override void _DropData(Vector2 position,Variant data){if(!_CanDropData(position,data))return;var d=data.AsGodotDictionary();Transfer?.Invoke(d["surface"].AsInt32(),d["slot"].AsInt32(),SurfaceIndex,SlotIndex);}
}
