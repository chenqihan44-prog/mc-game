using System;
using System.Collections.Generic;
using EmberLighthouse.Items;
using Godot;

namespace EmberLighthouse.UI;

// Original 32px artwork shared by inventory, recipe book and held-item meshes.
public static class ItemSprites
{
    private static readonly Dictionary<string,ImageTexture> textures=[];
    public static Texture2D Texture(ItemDef item)
    {
        if(textures.TryGetValue(item.Id,out var cached))return cached;
        using var art=CreateImage(item);var texture=ImageTexture.CreateFromImage(art);textures[item.Id]=texture;return texture;
    }
    public static void Release(){foreach(var t in textures.Values)t.Dispose();textures.Clear();}
    public static Image CreateImage(ItemDef item)
    {
        var art=new Art();var c=Color.FromString(item.Color,Colors.White);Color edge=new("292825"),wood=new("986636"),metal=item.Tier==3?new("65dfdd"):item.Tier==2?new("dce4e8"):new("b9bbae");
        void P(int x,int y,int w,int h,Color color)=>art.Rect(x,y,w,h,color);
        void L(int x,int y,int xx,int yy,Color color,int width=1)=>art.Line(x,y,xx,yy,color,width);
        void Shape(Color color,params Vector2[] points)=>art.Polygon(points,color);
        if(item.Use is "pick" or "axe" or "shovel" or "sword"){
            L(6,27,22,7,edge,5);L(6,26,22,7,wood,3);L(7,24,20,9,wood.Lightened(.25f));
            if(item.Use=="pick"){Shape(edge,new(8,4),new(23,2),new(29,8),new(29,15),new(25,15),new(24,8),new(11,9),new(6,12));Shape(metal,new(9,5),new(22,4),new(27,9),new(27,12),new(24,7),new(11,7),new(8,10));L(11,5,21,4,metal.Lightened(.25f));}
            if(item.Use=="axe"){Shape(edge,new(16,3),new(27,3),new(29,12),new(21,17),new(15,12));Shape(metal,new(18,5),new(25,5),new(27,11),new(21,14),new(17,11));L(19,5,25,5,Colors.White);}
            if(item.Use=="shovel"){Shape(edge,new(22,2),new(29,4),new(29,11),new(24,16),new(17,10));Shape(metal,new(23,4),new(27,5),new(27,10),new(23,13),new(19,9));}
            if(item.Use=="sword"){L(13,18,28,3,edge,7);L(14,17,28,3,metal,4);L(14,16,27,3,metal.Lightened(.2f));L(10,14,19,23,edge,4);L(10,14,19,23,metal.Darkened(.2f),2);P(4,25,5,4,edge);P(5,26,3,2,metal);}
        }
        else if(item.Use=="armor"){
            if(item.ArmorSlot==0){Shape(edge,new(5,7),new(10,3),new(23,3),new(27,7),new(27,24),new(22,24),new(22,15),new(10,15),new(10,24),new(5,24));P(7,8,18,6,metal);P(10,5,13,3,metal);P(7,13,3,9,metal.Darkened(.22f));P(22,13,3,9,metal.Darkened(.22f));L(10,6,22,6,Colors.White);}
            if(item.ArmorSlot==1){Shape(edge,new(2,8),new(9,4),new(13,8),new(19,8),new(23,4),new(30,8),new(27,17),new(24,15),new(24,28),new(8,28),new(8,15),new(5,17));P(9,10,14,16,metal);P(4,8,7,6,metal.Darkened(.15f));P(21,8,7,6,metal.Darkened(.15f));L(11,12,11,24,Colors.White);L(21,13,21,26,metal.Darkened(.25f));}
            if(item.ArmorSlot==2){P(7,3,19,25,edge);P(9,5,15,7,metal);P(9,12,6,14,metal);P(18,12,6,14,metal.Darkened(.2f));P(15,13,3,15,Colors.Transparent);L(10,6,22,6,Colors.White);}
            if(item.ArmorSlot==3){P(3,8,11,21,edge);P(18,8,11,21,edge);P(5,10,7,16,metal);P(20,10,7,16,metal.Darkened(.15f));P(2,23,12,6,edge);P(17,23,12,6,edge);P(3,24,10,3,metal);P(18,24,10,3,metal);}
        }
        else if(item.Id is "berry" or "cooked_berry"){
            foreach(var p in new[]{new Vector2I(8,12),new(17,9),new(19,19),new(10,21)}){art.Disc(p.X,p.Y,6,edge);art.Disc(p.X,p.Y,5,item.Id=="berry"?new("c95047"):new("b75545"));P(p.X-2,p.Y-3,2,2,new("f7946e"));}L(11,9,16,4,new("537a37"),3);Shape(new("75a847"),new(14,6),new(16,2),new(24,3),new(20,7));
        }
        else if(item.Id is "carrot" or "cooked_carrot"){
            Shape(edge,new(5,28),new(9,12),new(18,9),new(23,16));Shape(c,new(7,26),new(11,13),new(17,11),new(21,16));L(12,15,18,16,new("aa592b"));L(10,20,13,21,new("aa592b"));L(18,10,24,3,new("558933"),3);L(19,10,29,8,new("6caa40"),3);L(18,9,17,2,new("81b347"),2);
        }
        else if(item.Id is "potato" or "baked_potato"){
            Shape(edge,new(3,17),new(9,8),new(20,5),new(28,12),new(27,23),new(18,28),new(7,27));Shape(c,new(5,17),new(10,10),new(20,7),new(26,13),new(25,22),new(17,26),new(8,25));P(10,15,2,2,c.Darkened(.35f));P(20,11,2,2,c.Darkened(.35f));P(20,22,2,2,c.Darkened(.35f));if(item.Id=="baked_potato"){L(9,12,22,23,new("efe2ba"),4);P(14,15,5,4,new("f5d772"));}
        }
        else if(item.Id=="bread"){
            Shape(edge,new(2,19),new(7,10),new(21,6),new(29,12),new(29,20),new(12,28),new(4,26));Shape(new("c88b40"),new(4,19),new(9,12),new(21,8),new(27,13),new(27,19),new(12,26),new(6,24));L(9,14,13,20,new("f4cb78"),3);L(17,11,21,17,new("f4cb78"),3);L(23,12,26,15,new("f4cb78"),2);
        }
        else if(item.Id is "meat" or "cooked_meat"){
            Shape(edge,new(4,10),new(11,5),new(22,6),new(29,14),new(24,26),new(8,28),new(2,20));Shape(item.Id=="meat"?new("e9afa0"):new("d3ac7d"),new(6,11),new(12,7),new(21,8),new(27,14),new(22,24),new(9,26),new(4,20));Shape(item.Id=="meat"?new("ae5649"):new("815033"),new(8,12),new(19,10),new(24,15),new(20,21),new(10,22),new(6,18));art.Disc(16,16,3,new("e7cfb1"));if(item.Id=="cooked_meat"){L(7,14,12,12,new("493527"),2);L(19,21,23,16,new("493527"),2);}
        }
        else if(item.Use=="seed"){
            Shape(edge,new(6,12),new(12,8),new(23,9),new(28,23),new(23,29),new(7,28),new(3,20));Shape(new("c9b084"),new(8,13),new(13,10),new(21,11),new(25,23),new(22,26),new(8,25),new(6,20));P(10,14,11,9,new("e6d5a6"));Color emblem=item.Id=="carrot_seed"?new("e99234"):item.Id=="potato_seed"?new("ac874b"):new("86a448");art.Disc(15,19,3,emblem);L(16,16,18,12,new("5b8839"),2);P(11,6,10,3,new("5f8040"));
        }
        else if(item.Id=="grain"){
            foreach(int x in new[]{9,16,23}){L(x-3,29,x,3,new("aa7934"),2);for(int y=7;y<22;y+=5){L(x,y,x-4,y-3,new("e7bd56"),3);L(x,y+1,x+4,y-2,new("dba449"),3);}}L(7,23,23,25,new("916734"),2);
        }
        else if(item.Id is "iron_ingot" or "copper_ingot"){
            Shape(edge,new(3,17),new(10,9),new(25,9),new(30,16),new(27,24),new(6,24));Shape(c,new(5,18),new(11,11),new(24,11),new(27,17),new(25,22),new(7,22));Shape(c.Lightened(.27f),new(6,17),new(11,11),new(23,11),new(25,16));L(7,19,24,19,c.Darkened(.2f),2);
        }
        else if(item.Id is "coal" or "raw_iron" or "copper_ore"){
            Shape(edge,new(3,19),new(6,9),new(18,4),new(27,10),new(29,23),new(21,28),new(6,27));Shape(c.Darkened(.1f),new(5,19),new(8,11),new(18,6),new(25,11),new(27,22),new(20,26),new(7,25));Shape(c.Lightened(.22f),new(8,11),new(18,6),new(18,15),new(6,20));if(item.Id!="coal"){P(10,12,5,4,new("e6b891"));P(20,19,4,4,new("bd784d"));P(14,23,3,2,new("efbd82"));}
        }
        else if(item.Id=="crystal"){
            Shape(edge,new(10,2),new(22,3),new(29,13),new(16,30),new(3,13));Shape(c,new(11,4),new(21,5),new(26,13),new(16,27),new(6,13));Shape(new("c1ffec"),new(11,4),new(15,14),new(6,13));Shape(new("3a9fa8"),new(15,14),new(26,13),new(16,27));L(12,5,20,5,Colors.White,2);
        }
        else if(item.Id is "stick" or "arrow" or "charcoal"){
            L(5,28,26,4,edge,5);L(6,27,26,5,item.Id=="charcoal"?new("5d5040"):wood,3);L(7,25,24,6,item.Id=="charcoal"?new("80705b"):wood.Lightened(.3f));if(item.Id=="arrow"){Shape(new("d2dadb"),new(24,3),new(30,2),new(29,8),new(25,8));L(3,21,10,28,new("eae3ca"),3);L(5,18,13,26,new("bdbda3"),3);}
        }
        else if(item.Id=="bow"){
            L(9,3,9,29,new("e2d5b4"));for(int y=3;y<=28;y++){int x=9+(int)(Math.Sin((y-3)/25f*Math.PI)*14);P(x-1,y,5,1,edge);P(x,y,3,1,wood);P(x,y,1,1,wood.Lightened(.4f));}P(19,13,5,7,new("76543a"));
        }
        else if(item.Id=="shield"){
            Shape(edge,new(3,4),new(29,4),new(27,21),new(16,30),new(5,21));Shape(new("a17b48"),new(6,7),new(26,7),new(24,20),new(16,26),new(8,20));for(int x=9;x<26;x+=5)L(x,7,x-1,22,new("6c4e30"));L(5,6,27,6,new("e0dbbf"),2);L(15,7,15,26,new("bfc7c8"),3);P(13,14,6,5,new("d7dfda"));
        }
        else if(item.Id is "fiber" or "rope" or "cloth" or "leather"){
            if(item.Id=="fiber"){for(int i=0;i<7;i++)L(5+i*3,26,9+i*2,5,i%2==0?new("b7c480"):new("71854b"),2);L(6,17,25,20,new("c5a46b"),3);}
            if(item.Id=="rope"){for(int r=0;r<3;r++){art.Ellipse(16,10+r*6,10,5,edge);art.Ellipse(16,9+r*6,8,3,c);art.Ellipse(16,9+r*6,5,1,new("806b43"));}L(25,15,28,27,c,3);}
            if(item.Id=="cloth"){Shape(edge,new(5,6),new(25,6),new(29,23),new(22,28),new(2,26));Shape(c,new(7,8),new(23,8),new(26,23),new(21,25),new(5,23));L(8,10,23,10,c.Darkened(.18f));L(6,19,24,19,c.Darkened(.12f));L(9,11,10,23,c.Lightened(.15f));}
            if(item.Id=="leather"){Shape(edge,new(5,3),new(13,8),new(22,4),new(29,11),new(23,16),new(29,26),new(18,30),new(11,24),new(2,27),new(5,17),new(2,10));Shape(c,new(7,6),new(13,11),new(22,7),new(26,11),new(20,17),new(25,25),new(18,27),new(11,21),new(6,24),new(8,16),new(5,10));L(10,12,20,10,c.Lightened(.3f),2);}
        }
        else if(item.Id is "map" or "beacon_sigil"){
            if(item.Id=="map"){P(3,4,26,24,edge);P(5,6,22,20,new("e9d8ad"));Shape(new("9bb479"),new(7,9),new(14,7),new(18,13),new(12,23),new(7,20));L(20,7,18,12,new("74b7c4"),2);L(18,12,24,22,new("74b7c4"),2);L(14,15,21,15,new("b8583d"),2);L(18,11,18,19,new("b8583d"),2);P(3,4,3,24,new("b69e76"));}
            else{art.Disc(16,16,14,edge);art.Disc(16,16,12,new("4d848f"));art.Disc(16,16,9,new("85dcdf"));Shape(new("d5ffed"),new(16,5),new(24,16),new(16,27),new(8,16));Shape(new("418891"),new(16,10),new(20,16),new(16,22),new(12,16));}
        }
        else if(item.Shape=="bush"){
            L(15,29,16,12,new("725234"),3);foreach(var p in new[]{new Vector2I(10,12),new(22,14),new(16,7),new(8,22),new(23,23)}){art.Disc(p.X,p.Y,7,new("314f2b"));art.Disc(p.X,p.Y,5,c);P(p.X-2,p.Y-2,3,2,c.Lightened(.2f));}foreach(var p in new[]{new Vector2I(7,15),new(19,9),new(23,24)}){art.Disc(p.X,p.Y,2,new("cd5347"));P(p.X-1,p.Y-1,1,1,new("f29669"));}
        }
        else if(item.Id is "door" or "window" or "fence" or "lantern" or "campfire" or "bed" or "bookshelf"){
            if(item.Id=="door"){P(7,1,18,30,edge);P(9,3,14,26,c);P(11,5,10,10,new("cba77b"));P(12,6,3,3,new("849b9a"));P(17,6,3,3,new("849b9a"));P(11,18,10,9,c.Darkened(.18f));P(19,16,2,2,new("e7d091"));L(16,4,16,14,c.Darkened(.3f));}
            if(item.Id=="window"){P(3,3,26,26,edge);P(5,5,22,22,c);P(7,7,18,18,new("97c8d3"));L(8,22,22,8,new("e2f2ed"),2);P(14,6,3,20,wood);P(6,14,20,3,wood);}
            if(item.Id=="fence"){P(4,4,7,26,edge);P(22,4,7,26,edge);P(6,6,3,22,c);P(24,6,3,22,c);P(7,10,18,5,c.Darkened(.12f));P(7,20,18,5,c.Darkened(.12f));L(8,11,24,11,c.Lightened(.15f));}
            if(item.Id=="lantern"){L(13,3,19,3,edge,3);L(11,5,11,8,edge,2);L(21,5,21,8,edge,2);P(7,9,18,4,new("485358"));P(8,12,16,14,new("956139"));P(10,13,12,11,new("ffd477"));P(12,14,4,9,new("fff2bc"));P(7,26,18,3,new("485358"));L(8,12,8,26,edge,2);L(24,12,24,26,edge,2);}
            if(item.Id=="campfire"){L(5,23,27,29,edge,6);L(5,23,27,29,wood,4);L(6,29,27,22,edge,6);L(6,29,27,22,wood.Lightened(.25f),4);Shape(new("d87932"),new(8,23),new(5,13),new(11,16),new(16,3),new(20,15),new(25,10),new(24,23));Shape(new("ffd37a"),new(11,22),new(13,15),new(16,11),new(20,22));}
            if(item.Id=="bed"){art.Cube(item,new(2,8),7);P(7,8,12,6,new("ede4c8"));L(5,15,18,22,new("bc5449"),8);L(9,17,25,13,new("d77a62"),5);P(4,23,3,7,wood);P(25,20,3,7,wood);}
            if(item.Id=="bookshelf"){art.Cube(item,Vector2.Zero,15);P(5,12,9,10,new("3e3328"));foreach(var p in new[]{(6,"b96146"),(8,"789b88"),(10,"c9b477"),(12,"67819c")})P(p.Item1,13,1,7,new(p.Item2));P(18,15,10,10,new("3e3328"));foreach(var p in new[]{(19,"ba864f"),(21,"6b9280"),(23,"a26366"),(25,"c2b891")})P(p.Item1,16,1,7,new(p.Item2));}
        }
        else if(item.Block){
            if(item.Shape=="stairs"){art.Cube(item,new(0,7),7);art.Cube(item,new(0,-2),9,.65f);}
            else art.Cube(item,Vector2.Zero,item.Shape is "slab" or "rug"?item.Shape=="rug"?3:8:15);
        }
        else throw new InvalidOperationException("Missing item artwork: "+item.Id);
        return art.Image();
    }
    public static Color Tile(ItemDef item,int x,int y,int face=0)
    {
        var c=Color.FromString(item.Color,Colors.White);uint noise=unchecked((uint)(x*374761393+y*668265263+item.Id.Length*144269));float n=(noise%7-3f)*.025f;c=n>=0?c.Lightened(n):c.Darkened(-n);
        bool line=y%5==0;
        switch(item.Id){
            case "grass":return face==2||y<3?new Color("729752").Darkened((noise%4)*.04f):new Color("80604c").Darkened((noise%5)*.035f);
            case "wood":if(face==2){int r=Math.Max(Math.Abs(x-7),Math.Abs(y-7));return r%3==0?new("735134"):new("b58c54");}return x%4==0?c.Darkened(.3f):c.Lightened(.06f);
            case "plank":case "stairs":case "slab":case "fence":return line||x%8==((y/5)%2)*4?c.Darkened(.23f):c;
            case "brick":case "ruin_brick":case "tile":return y%5==0||(x+((y/5)%2)*4)%8==0?c.Darkened(.28f):c;
            case "coal_ore":case "iron_ore":case "crystal_ore":return (x*3+y*7)%17<5?Color.FromString(item.Color,Colors.White).Lightened(.12f):new Color("7b8182").Darkened((noise%5)*.03f);
            case "workbench":return face==2?(x%5==0||y%5==0?new("59422c"):new("c59b65")):y<3?new("a37342"):x%7==0?new("4b3727"):c;
            case "chest":return y==4||y==13?new("503c2c"):face==0&&x is >6 and <10&&y is >4 and <10?new("c3c2a4"):c;
            case "furnace":return face==0&&x is >3 and <12&&y is >5 and <13?new("242c30"):x%5==0||y%5==0?c.Darkened(.2f):c;
            case "farmland":return x%4==0?new("443626"):c;
            case "glass":case "ice":case "water":return x==0||y==0||x==15||y==15||Math.Abs(x-y)<2?c.Lightened(.25f):c.Darkened(.08f);
            case "cactus":return x%4==0?c.Darkened(.25f):(x+y*3)%17==0?new("dccd8a"):c;
            case "pillar":return x%4==0?c.Darkened(.2f):c;
            case "marble":return (x+y*2)%13<2?c.Darkened(.22f):c;
            case "copper_block":return x==0||y==0?c.Lightened(.25f):x==15||y==15?c.Darkened(.25f):c;
            case "sandstone":return y%6==0?c.Darkened(.12f):c;
            case "beacon":return Math.Abs(x-8)+Math.Abs(y-8)<5?new("d2fff4"):c.Darkened(.12f);
            default:return c;
        }
    }
    private sealed class Art
    {
        private readonly Color[] pixels=new Color[1024];
        private void Pixel(int x,int y,Color c){if(x>=0&&x<32&&y>=0&&y<32)pixels[x+y*32]=c;}
        public void Rect(int x,int y,int w,int h,Color c){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)Pixel(xx,yy,c);}
        public void Line(int x,int y,int xx,int yy,Color c,int width=1){int count=Math.Max(Math.Abs(xx-x),Math.Abs(yy-y));for(int i=0;i<=count;i++){float t=count==0?0:i/(float)count;Rect((int)MathF.Round(x+(xx-x)*t)-width/2,(int)MathF.Round(y+(yy-y)*t)-width/2,width,width,c);}}
        public void Disc(int x,int y,int radius,Color c)=>Ellipse(x,y,radius,radius,c);
        public void Ellipse(int x,int y,int rx,int ry,Color c){for(int yy=-ry;yy<=ry;yy++)for(int xx=-rx;xx<=rx;xx++)if(xx*xx/(float)(rx*rx)+yy*yy/(float)(ry*ry)<=1)Pixel(x+xx,y+yy,c);}
        public void Polygon(Vector2[] points,Color c){for(int y=0;y<32;y++)for(int x=0;x<32;x++){bool inside=false;for(int i=0,j=points.Length-1;i<points.Length;j=i++){var a=points[i];var b=points[j];if((a.Y>y+.5)!=(b.Y>y+.5)&&x+.5<(b.X-a.X)*(y+.5-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;}if(inside)Pixel(x,y,c);}}
        public void Cube(ItemDef item,Vector2 shift,int height,float size=1)
        {
            float w=14*size;Vector2 a=new Vector2(2,9)+shift,b=new Vector2(2+w,9-w*.57f)+shift,c=new Vector2(2+2*w,9)+shift,d=new Vector2(2+w,9+w*.57f)+shift;
            Face(a,b-a,d-a,2,1);Face(a,d-a,new(0,height),0,.68f);Face(d,c-d,new(0,height),1,.87f);
            void Face(Vector2 origin,Vector2 u,Vector2 v,int face,float shade){float determinant=u.X*v.Y-u.Y*v.X;for(int yy=0;yy<32;yy++)for(int xx=0;xx<32;xx++){var p=new Vector2(xx+.5f,yy+.5f)-origin;float s=(p.X*v.Y-p.Y*v.X)/determinant,t=(u.X*p.Y-u.Y*p.X)/determinant;if(s>=0&&s<1&&t>=0&&t<1){var color=Tile(item,(int)(s*16),(int)(t*16),face);Pixel(xx,yy,new(color.R*shade,color.G*shade,color.B*shade));}}}
        }
        public Image Image(){var image=Godot.Image.CreateEmpty(32,32,false,Godot.Image.Format.Rgba8);for(int y=0;y<32;y++)for(int x=0;x<32;x++)image.SetPixel(x,y,pixels[x+y*32]);return image;}
    }
}
