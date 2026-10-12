using System;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.Rendering;

public static class PlantSprites
{
    // Original 32-pixel cutout sprites. Clear pixels remain empty between branches.
    public static ImageTexture CreateAtlas()
    {
        var bitmap=Image.CreateEmpty(PlantVisuals.Count*32,32,false,Image.Format.Rgba8);bitmap.Fill(Colors.Transparent);
        Color[] greens=[new("345e32"),new("4d7d3d"),new("64934b"),new("789f53")];
        void Pixel(int style,int x,int y,Color color){if(x>=0&&x<32&&y>=0&&y<32)bitmap.SetPixel(style*32+x,y,color);}
        void Line(int style,int x0,int y0,int x1,int y1,Color color){int count=Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0));for(int i=0;i<=count;i++)Pixel(style,(int)Math.Round(x0+(x1-x0)*i/(double)Math.Max(1,count)),(int)Math.Round(y0+(y1-y0)*i/(double)Math.Max(1,count)),color);}
        Line(0,16,29,16,10,new("705536"));Line(0,16,23,5,13,new("705536"));Line(0,16,22,28,12,new("705536"));Line(0,16,16,8,7,new("705536"));
        foreach(var leaf in new[]{(8,22,7,5),(23,23,6,5),(10,10,6,4),(21,8,6,4),(16,16,7,4),(5,13,4,3),(27,13,4,3)}){
            var (cx,cy,rx,ry)=leaf;
            for(int y=cy-ry;y<=cy+ry;y++)for(int x=cx-rx;x<=cx+rx;x++)if((x-cx)*(x-cx)/(float)(rx*rx)+(y-cy)*(y-cy)/(float)(ry*ry)<1&&RandomField.Hash(713,x,y,0)%19!=0)Pixel(0,x,y,greens[RandomField.Hash(714,x,y,0)%4]);
        }
        foreach(var berry in new[]{(8,12),(22,9),(13,19),(25,19),(8,8),(20,17)}){int x=berry.Item1,y=berry.Item2;Pixel(0,x,y,new("d75848"));Pixel(0,x+1,y,new("b43438"));Pixel(0,x,y+1,new("ae3036"));Pixel(0,x+1,y+1,new("80272d"));}
        for(int style=1;style<=2;style++)for(int stem=0;stem<4;stem++){
            int x=5+stem*7,top=5+stem%2*3;var color=style==1?greens[2]:new Color("d0b961");
            Line(style,x,29,x,top,color);Line(style,x,22,x-4,17,color);Line(style,x,18,x+3,14,color);
            for(int y=top;y<top+7;y++){Pixel(style,x-1,y,color);Pixel(style,x+1,y,style==1?greens[1]:new("9d8745"));}
        }
        for(int style=3;style<=4;style++){
            for(int stem=-2;stem<=2;stem++){int x=16+stem*4;Line(style,16,28,x,10+Math.Abs(stem)*3,greens[1]);Line(style,x,18,x+3,14,greens[2]);Line(style,x,20,x-3,16,greens[2]);}
            for(int y=25;y<29;y++)for(int x=13;x<19;x++)Pixel(style,x,y,style==3?new("d77f32"):new("ba9d5d"));
        }
        for(int style=5;style<=6;style++)for(int stem=0;stem<7;stem++){
            int x=4+stem*4,top=8+(stem*7)%12;var color=style==5?greens[stem%4]:new Color(stem%2==0?"b9a268":"897548");Line(style,x,30,x+stem%3-1,top,color);Line(style,x,25,x-3,top+5,color);Line(style,x,27,x+3,top+8,color);
        }
        for(int branch=0;branch<7;branch++){int y=10+branch*3,width=4+branch;Line(7,16,30,16,6,new("588950"));for(int side=-1;side<=1;side+=2){Line(7,16,y,16+side*width,y-3,new("63a66a"));for(int leaf=1;leaf<width;leaf+=2)Line(7,16+side*leaf,y-1,16+side*(leaf+1),y-4,new("3f8058"));}}
        for(int stem=0;stem<4;stem++){int x=5+stem*7;Line(8,x,31,x,7+stem%2*3,new("82975d"));Line(8,x,25,x-4,16,new("637e50"));Line(8,x,23,x+4,14,new("9fba68"));for(int y=3+stem%2*3;y<12+stem%2*3;y++)for(int dx=-1;dx<=1;dx++)Pixel(8,x+dx,y,new("ad9165"));}
        for(int style=9;style<=10;style++){Line(style,16,30,16,10,new("5a8547"));Line(style,16,23,9,18,new("79a751"));Line(style,16,26,23,20,new("658e4c"));var flower=new Color(style==9?"db655c":"739ed5");for(int y=6;y<=15;y++)for(int x=10;x<=22;x++)if((x-16)*(x-16)+(y-11)*(y-11)<25||Math.Abs(x-16)<2||Math.Abs(y-11)<2)Pixel(style,x,y,flower);for(int y=10;y<=12;y++)for(int x=15;x<=17;x++)Pixel(style,x,y,new("eed998"));}
        for(int style=11;style<=12;style++){for(int y=20;y<=30;y++)for(int x=14;x<=18;x++)Pixel(style,x,y,new("d7c9a0"));for(int y=12;y<=22;y++)for(int x=6;x<=26;x++)if((x-16)*(x-16)/100f+(y-22)*(y-22)/100f<1)Pixel(style,x,y,new(style==11?"a98760":"c45e4f"));Line(style,7,22,25,22,new("795d49"));if(style==12)foreach(var dot in new[]{(12,17),(18,14),(22,19)}){Pixel(style,dot.Item1,dot.Item2,new("f1dfb9"));Pixel(style,dot.Item1+1,dot.Item2,new("f1dfb9"));}}
        for(int stem=0;stem<3;stem++){int x=7+stem*9;Line(13,x,31,x,3,new("83af53"));Line(13,x+1,31,x+1,3,new("aac768"));for(int y=8;y<28;y+=6){Line(13,x-1,y,x+2,y,new("507f3c"));Line(13,x,y,x+5,y-5,new("95bc5d"));}}
        for(int style=14;style<=15;style++){for(int stem=-2;stem<=2;stem++){Line(style,16,30,16+stem*5,7+Math.Abs(stem)*3,new("65944b"));Line(style,16+stem*3,20,22+stem*3,16,new("91ad54"));}for(int y=23;y<31;y++)for(int x=9;x<24;x++)Pixel(style,x,y,new(style==14?"d39c3d":"82a640"));for(int x=12;x<23;x+=4)Line(style,x,24,x,30,new(style==14?"aa762e":"477c3d"));}
        var texture=ImageTexture.CreateFromImage(bitmap);bitmap.Dispose();return texture;
    }
}
