using System;
using EmberLighthouse.Items;
using Godot;
namespace EmberLighthouse.UI;
public static partial class ItemSprites
{
    private static bool PaintExpansion(Art art,ItemDef item)
    {
        Color c=new(item.Color),edge=new("292825"),wood=new("a7783f");void R(int x,int y,int w,int h,Color tint)=>art.Rect(x,y,w,h,tint);void L(int x,int y,int xx,int yy,Color tint,int width=1)=>art.Line(x,y,xx,yy,tint,width);
        if(item.Id is "glass_bottle" or "water_bottle" or "healing_potion" or "fire_resistance_potion"){
            R(12,2,8,4,wood);R(13,6,6,8,edge);R(8,13,16,15,edge);R(10,12,12,15,new("b5d9df"));R(12,7,8,9,new("c7e3e6"));if(item.Id!="glass_bottle")R(11,18,10,8,c);L(11,14,11,24,Colors.White,2);return true;
        }
        if(item.Id is "pumpkin_seed" or "melon_seed"){foreach(var p in new[]{new Vector2I(8,19),new(16,10),new(24,20),new(14,26),new(25,8)}){art.Ellipse(p.X,p.Y,4,6,edge);art.Ellipse(p.X,p.Y,2,4,c);R(p.X-1,p.Y-2,1,2,c.Lightened(.3f));}return true;}
        if(item.Id.EndsWith("bucket",StringComparison.Ordinal)||item.Id=="bucket"){
            R(5,8,22,21,edge);R(7,10,18,16,new("a3adb4"));L(8,11,8,25,new("e5e6dd"),2);art.Ellipse(16,8,11,4,edge);art.Ellipse(16,8,9,2,item.Id=="water_bucket"?new("75b8da"):item.Id=="milk_bucket"?new("ece4cf"):new("687078"));return true;
        }
        if(item.Id=="shears"){L(8,25,23,5,edge,5);L(10,27,27,9,edge,5);L(8,23,21,7,new("cbd2d3"),3);L(12,25,25,10,new("e3e2d8"),3);art.Disc(7,26,4,new("b85d4c"));art.Disc(13,27,4,new("b85d4c"));R(13,18,3,3,wood);return true;}
        if(item.Id=="iron_hoe"){L(6,28,24,7,edge,5);L(7,27,23,8,wood,3);L(10,5,26,9,new("c9d1cf"),4);L(11,5,8,10,new("c9d1cf"),4);return true;}
        if(item.Id=="fishing_rod"){L(5,28,24,3,edge,5);L(6,27,24,4,wood,3);L(25,5,27,23,new("dcded3"));art.Disc(27,25,3,new("dc6659"));R(26,21,3,2,new("e0e5da"));return true;}
        if(item.Id=="brewing_stand"){R(4,25,24,4,edge);R(6,26,20,2,new("96978a"));R(14,4,4,22,edge);R(15,5,2,20,c);L(6,11,26,11,c,2);foreach(int x in new[]{7,24}){R(x-2,14,5,9,new("b8d4d7"));R(x-1,17,3,5,new("d76d5b"));R(x-1,11,3,4,wood);}return true;}
        if(item.Id is "bone" or "feather"){L(7,26,25,6,edge,7);L(8,25,24,7,c,5);art.Disc(7,25,4,c);art.Disc(24,7,4,c);if(item.Id=="feather")for(int i=0;i<7;i++)L(9+i*2,24-i*2,16+i,23-i*3,c,3);return true;}
        if(item.Id is "bone_meal" or "gunpowder"){art.Polygon([new(4,25),new(10,14),new(15,8),new(23,15),new(29,25)],edge);art.Polygon([new(6,24),new(11,16),new(15,11),new(22,17),new(26,24)],c);return true;}
        if(item.Id is "apple" or "golden_apple"){art.Disc(12,18,9,edge);art.Disc(21,18,9,edge);art.Disc(12,18,7,c);art.Disc(21,18,7,c);R(9,12,4,5,c.Lightened(.4f));L(16,10,17,3,wood,2);L(18,5,25,4,new("75a94a"),3);return true;}
        if(item.Id is "raw_cod" or "cooked_cod"){art.Ellipse(16,16,11,6,edge);art.Ellipse(16,16,9,4,c);art.Polygon([new(3,17),new(1,11),new(7,15),new(1,22)],c);R(22,14,2,2,edge);L(13,13,17,11,c.Darkened(.2f),2);return true;}
        if(item.Id is "bowl" or "mushroom_stew"){art.Ellipse(16,17,12,7,edge);art.Ellipse(16,16,10,5,wood);R(7,19,18,8,wood.Darkened(.3f));art.Ellipse(16,14,10,3,item.Id=="bowl"?new("584632"):new("c2996c"));if(item.Id=="mushroom_stew"){R(10,13,4,3,new("d57958"));R(18,13,5,2,new("9dba67"));}return true;}
        if(item.Id=="sugar_cane"){foreach(int x in new[]{7,16,25}){L(x,29,x,5+x%3,new("628b43"),4);for(int y=10;y<28;y+=6){R(x-2,y,5,2,new("a3bd5a"));L(x,y,x+4,y-4,new("9abc60"),2);}}return true;}
        if(item.Id=="paper"){R(5,4,23,24,edge);R(7,6,19,20,c);L(10,12,22,12,c.Darkened(.25f));L(10,17,20,17,c.Darkened(.2f));return true;}
        if(item.Id=="gold_ingot"){art.Polygon([new(3,18),new(10,9),new(25,9),new(30,18),new(26,25),new(6,25)],edge);art.Polygon([new(5,18),new(11,11),new(24,11),new(27,18),new(24,23),new(8,23)],c);L(10,12,23,12,c.Lightened(.45f),2);return true;}
        if(item.Id=="elytra"){foreach(int side in new[]{-1,1}){int x=16+side*5;art.Polygon([new(x,3),new(x+side*7,8),new(x+side*9,26),new(x+side*3,29),new(x,19)],edge);L(x,6,x+side*7,26,c,5);}return true;}
        if(item.Use=="animal_armor"||item.Id=="saddle"){R(4,8,24,15,edge);R(6,10,20,11,c);R(4,15,5,11,c.Darkened(.25f));R(24,15,5,11,c.Darkened(.25f));R(11,10,10,6,new("5a463b"));return true;}
        if(item.Id is "turtle_scute" or "armadillo_scute" or "ink_sac" or "egg"){art.Ellipse(16,17,9,12,edge);art.Ellipse(16,16,7,10,c);R(12,8,3,6,c.Lightened(.3f));return true;}
        if(item.Id=="string"){for(int y=8;y<25;y+=5){art.Ellipse(16,y,10,4,c);art.Ellipse(16,y,7,2,edge);}return true;}
        if(item.Id=="firework"){L(7,28,23,6,wood,2);L(12,21,24,7,new("e7dcc0"),7);L(13,20,24,8,new("c95e53"),4);return true;}
        if(item.Id is "red_dye" or "blue_dye"){art.Disc(16,17,10,edge);art.Disc(16,16,8,c);return true;}
        if(item.Id=="melon"){art.Polygon([new(3,23),new(25,3),new(29,22),new(22,30)],new("638b3e"));art.Polygon([new(6,23),new(24,6),new(26,21),new(21,26)],new("d56356"));foreach(int x in new[]{12,18,22})R(x,17+(x%3),2,3,edge);return true;}
        if(item.Id=="pumpkin_pie"){art.Ellipse(16,19,13,9,edge);art.Ellipse(16,16,11,6,c);L(9,13,22,18,new("f0d79e"),2);return true;}
        return false;
    }
}
