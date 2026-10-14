using System;
using EmberLighthouse.Core;
namespace EmberLighthouse.World;

public readonly record struct TreeShape(string Log,string Leaves,int Height,int Width,int Variant);
public static class NaturalTrees
{
    public static TreeShape Shape(uint hash,int climate)
    {
        int pick=(int)((hash>>8)%10),variant=(int)((hash>>18)%4);
        string species=climate==2?"acacia":climate==3?(pick<8?"pine":"birch"):climate==4?(pick<6?"pine":"oak"):pick<3?"birch":pick==3?"pine":"oak";
        bool thick=(species is "pine" or "oak")&&(hash>>22)%5==0;
        int height=species switch{"pine"=>8+(int)((hash>>11)%5),"birch"=>5+(int)((hash>>11)%4),"acacia"=>4+(int)((hash>>11)%3),_=>5+(int)((hash>>11)%5)};
        return new(species switch{"pine"=>"pine_log","birch"=>"birch_log","acacia"=>"acacia_log",_=>"wood"},species switch{"pine"=>"pine_leaves","birch"=>"birch_leaves","acacia"=>"acacia_leaves",_=>"leaves"},height+(thick?2:0),thick?2:1,variant);
    }
    public static string? Block(TreeShape tree,int x,int y,int z,int localGround=0)
    {
        int h=tree.Height,w=tree.Width;bool trunk=x>=0&&x<w&&z>=0&&z<w;
        if(trunk&&y>localGround&&y<=h)return tree.Log;
        if(y<2||y>h+3)return null;
        double cx=(w-1)*.5,dx=Math.Abs(x-cx),dz=Math.Abs(z-cx),r;
        if(tree.Log=="pine_log"){
            // Overlapping whorls leave lateral foliage even around the top.
            if(y<h-6||y>h+2)return null;
            int fromTop=h+2-y;r=fromTop<=1?1:Math.Min(4,1+fromTop/3.0)+(fromTop%3==2?.75:fromTop%3==0?-.6:-.25)+(w==2?.5:0);
        }else if(tree.Log=="acacia_log"){
            if(y<h-1||y>h+2)return null;r=y>=h+1?2.2:3.5;
            dx=Math.Abs(x-cx-(tree.Variant%2==0?1:-1));dz*=1.2;
        }else{
            int bottom=h-(tree.Log=="birch_log"?2:3);if(y<bottom)return null;
            r=y>=h+2?1.5:y==h+1?2.4:tree.Log=="birch_log"?2.3:3.1+(tree.Variant%2)*.5;
            r+=w==2?.6:0;
        }
        return dx<=r&&dz<=r&&dx+dz<=r*1.65?tree.Leaves:null;
    }
    public static WorldSnapshot Refresh(WorldSnapshot original,EmberLighthouse.Items.Catalog catalog)
    {
        if(original.GeneratorVersion is <4 or >=Terrain.Version)throw new InvalidOperationException("此更新适用于地形版本 4–7 的世界");
        var copy=original.Copy();var old=new WorldState(catalog,original.Copy());var updated=new Terrain(copy.Seed,catalog,Terrain.Version);
        // Freeze surface columns near edits, so wider trees cannot grow into
        // buildings or restore harvested trunks. Underground edits stay exact.
        var columns=new System.Collections.Generic.HashSet<(int,int)>();
        foreach(var key in original.Changes.Keys){var c=Cell.Parse(key);if(c.Y<old.Terrain.Surface(c.X,c.Z)-1)continue;for(int x=c.X-6;x<=c.X+6;x++)for(int z=c.Z-6;z<=c.Z+6;z++)columns.Add((x,z));}
        foreach(var key in original.Entities.Keys)if(!copy.Changes.ContainsKey(key))copy.Changes[key]=catalog.Id(old.Get(Cell.Parse(key)));
        foreach(var (x,z) in columns){int ground=old.Terrain.Surface(x,z);for(int y=ground+1;y<=Math.Min(191,ground+18);y++){var p=new Cell(x,y,z);if(copy.Changes.ContainsKey(p.ToString()))continue;ushort was=old.Get(p),now=updated.Get(p);if(was!=now)copy.Changes[p.ToString()]=catalog.Id(was);}}
        copy.GeneratorVersion=Terrain.Version;return copy;
    }
}
