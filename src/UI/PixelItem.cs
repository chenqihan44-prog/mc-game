using System;
using EmberLighthouse.Items;
using Godot;
namespace EmberLighthouse.UI;
public static class PixelItem
{
    public static void Draw(Control canvas,ItemDef item,Vector2 origin,float scale)=>canvas.DrawTextureRect(ItemSprites.Texture(item),new(origin,Vector2.One*16*scale),false);
    public static void DrawDurability(Control canvas,ItemDef def,Stack item,Vector2 origin,float scale)
    {
        if(def.Durability<=0||item.Durability is not { } remaining||remaining>=def.Durability)return;
        float ratio=remaining/(float)def.Durability;canvas.DrawRect(new(origin+new Vector2(1,14)*scale,new Vector2(14,2)*scale),new("252525"));canvas.DrawRect(new(origin+new Vector2(1,14)*scale,new Vector2(14*ratio,1)*scale),new Color(1-ratio,ratio,.05f));
    }
}
public partial class ItemGlyph : Control
{
    public Catalog Catalog {get;set;}=null!;
    public Stack? Item {get;set;}
    public float PixelScale {get;set;}=2.5f;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Nearest;}
    public override void _Draw()
    {
        if(Item==null)return;var def=Catalog.Items[Item.Id];float s=Math.Min(PixelScale,Math.Min(Size.X-6,Size.Y-6)/16);var origin=(Size-Vector2.One*16*s)/2;
        PixelItem.Draw(this,def,origin,s);PixelItem.DrawDurability(this,def,Item,origin,s);
        if(Item.Count>1){string count=Item.Count.ToString();var font=ThemeDB.FallbackFont;int size=Math.Clamp((int)(8*s),12,24);float width=font.GetStringSize(count,HorizontalAlignment.Left,-1,size).X;var p=new Vector2(Size.X-width-3,Size.Y-4);DrawStringOutline(font,p,count,HorizontalAlignment.Left,-1,size,3,new("252525"));DrawString(font,p,count,HorizontalAlignment.Left,-1,size,Colors.White);}
    }
}
