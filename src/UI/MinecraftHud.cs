using System;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.UI;

public partial class MinecraftHud : Control
{
    public WorldSnapshot State {get;set;}=null!;
    public Catalog Catalog {get;set;}=null!;
    public int Selected {get;set;}
    public float GuiScale {get;set;}=3;
    public double Air {get;set;}=15;
    public bool Underwater {get;set;}
    private static readonly string[] Heart=["0110110","1111111","1111111","0111110","0011100","0001000"];
    private static readonly string[] Food=["00001110","00011111","00111111","00111110","00011100","00101000","01010000","11100000"];
    private static readonly string[] Armor=["1100011","1111111","1111111","0111110","0111110","0111110","0111110"];
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Draw()
    {
        if(State==null)return;float s=GuiScale,x=(Size.X-182*s)/2,y=Size.Y-23*s;
        float centerX=Size.X/2,centerY=Size.Y/2;
        DrawRect(new(centerX-5,centerY-1,11,3),new(0,0,0,.6f));DrawRect(new(centerX-1,centerY-5,3,11),new(0,0,0,.6f));
        DrawRect(new(centerX-4,centerY,9,1),UiFactory.Parchment);DrawRect(new(centerX,centerY-4,1,9),UiFactory.Parchment);
        void Rect(float px,float py,float w,float h,Color color)=>DrawRect(new(x+px*s,y+py*s,w*s,h*s),color);
        // Keep the existing nine-cell geometry and input indices.
        Rect(0,0,182,22,new(0,0,0,.7f));Rect(1,1,180,20,new("846638"));Rect(2,2,178,18,new("352619"));
        for(int i=0;i<9;i++){
            float sx=2+i*20;Rect(sx,2,18,18,new("7c6039"));Rect(sx+1,3,16,16,new("21190f"));
            if(i==Selected){Rect(sx-2,0,22,22,UiFactory.Gold);Rect(sx-1,1,20,20,new("543b22"));Rect(sx,2,18,18,new("e4c68d"));Rect(sx+1,3,16,16,new("513922"));}
            var item=State.Inventory.Slots[i];if(item==null)continue;PixelItem.Draw(this,Catalog.Items[item.Id],new(x+(sx+1)*s,y+3*s),s);
            PixelItem.DrawDurability(this,Catalog.Items[item.Id],item,new(x+(sx+1)*s,y+3*s),s);
            if(item.Count>1){string count=item.Count.ToString();Vector2 position=new(x+(sx+18)*s,y+19*s);var font=ThemeDB.FallbackFont;int fontSize=(int)(8*s);float width=font.GetStringSize(count,HorizontalAlignment.Left,-1,fontSize).X;DrawStringOutline(font,position-new Vector2(width,0),count,HorizontalAlignment.Left,-1,fontSize,3,new("19130d"));DrawString(font,position-new Vector2(width,0),count,HorizontalAlignment.Left,-1,fontSize,UiFactory.Parchment);}
        }
        if(State.Equipment.Slots[Equipment.Offhand] is { } offhand){Rect(-26,0,22,22,new("21190f"));Rect(-25,1,20,20,UiFactory.Gold);Rect(-24,2,18,18,new("352619"));PixelItem.Draw(this,Catalog.Items[offhand.Id],new(x-23*s,y+3*s),s);PixelItem.DrawDurability(this,Catalog.Items[offhand.Id],offhand,new(x-23*s,y+3*s),s);if(offhand.Count>1)DrawString(ThemeDB.FallbackFont,new(x-12*s,y+20*s),offhand.Count.ToString(),HorizontalAlignment.Left,-1,(int)(8*s),UiFactory.Parchment);}
        if(State.Creative)return;
        void Icon(string[] pattern,float ix,float iy,double fill,Color active,Color empty){
            for(int row=0;row<pattern.Length;row++)for(int col=0;col<pattern[row].Length;col++)if(pattern[row][col]=='1'){
                Rect(ix+col,iy+row,1,1,new("191919"));
                bool edge=col==0||row==0||col==pattern[row].Length-1||row==pattern.Length-1||pattern[row][Math.Max(0,col-1)]=='0'||pattern[row][Math.Min(pattern[row].Length-1,col+1)]=='0';
                if(!edge)Rect(ix+col,iy+row,1,1,col<pattern[row].Length*fill?active:empty);
            }
        }
        int armor=Equipment.ArmorPoints(State,Catalog);
        for(int i=0;i<10;i++){
            double health=Math.Clamp((State.Health-i*10)/10,0,1);Icon(Heart,i*8,-16,health,new("dc7254"),new("392323"));
            double hunger=Math.Clamp((State.Hunger-i*10)/10,0,1);Icon(Food,174-i*8,-17,hunger,new("c49c60"),new("392b23"));
            if(armor>0)Icon(Armor,i*8,-26,Math.Clamp((armor-i*2)/2.0,0,1),UiFactory.Parchment,new("4c4336"));
        }
        Rect(0,-7,182,5,new("21190f"));Rect(1,-6,180,3,new("4c3923"));
        int level=0,remaining=State.Experience;while(remaining>=7+level*2){remaining-=7+level*2;level++;}
        float progress=remaining/(float)(7+level*2);Rect(1,-6,180*progress,3,UiFactory.Gold);Rect(1,-6,180*progress,1,new("f0d592"));
        if(level>0){string text=level.ToString();var font=ThemeDB.FallbackFont;int fs=(int)(10*s);float width=font.GetStringSize(text,HorizontalAlignment.Left,-1,fs).X;Vector2 pos=new(Size.X/2-width/2,y-7*s);DrawStringOutline(font,pos,text,HorizontalAlignment.Left,-1,fs,3,new("21190f"));DrawString(font,pos,text,HorizontalAlignment.Left,-1,fs,UiFactory.Gold);}
        if(Underwater){for(int i=0;i<10;i++){float bx=174-i*8;double amount=Air/15*10-i;if(amount>0){Rect(bx,-27,6,6,new("245893"));Rect(bx+1,-26,4,4,new("94d9ef"));Rect(bx+1,-26,2,1,Colors.White);}}}
    }
}
