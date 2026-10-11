using System;
using EmberLighthouse.Core;
using Godot;

namespace EmberLighthouse.Rendering;

public static class PlantSprites
{
    // Original 32-pixel cutout sprites. Clear pixels remain empty between branches.
    public static ImageTexture CreateAtlas()
    {
        var bitmap=Image.CreateEmpty(160,32,false,Image.Format.Rgba8);bitmap.Fill(Colors.Transparent);
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
        var texture=ImageTexture.CreateFromImage(bitmap);bitmap.Dispose();return texture;
    }
}
