using System;
using System.Collections.Generic;
using System.Threading;
using EmberLighthouse.Core;
using EmberLighthouse.Items;

namespace EmberLighthouse.World;

// Workers receive a copied edit region; live world dictionaries stay on the main thread.
public static class MapRaster
{
    public static byte[] Render(Terrain terrain,Catalog catalog,IReadOnlyDictionary<Cell,ushort> edits,int x,int z,int step,int pixels,CancellationToken cancellation=default,IReadOnlySet<int>? explored=null)
    {
        var colors=new byte[catalog.BlockIds.Length*3];
        for(int i=0;i<catalog.BlockIds.Length;i++){
            string color=catalog.Block((ushort)i)?.Color.TrimStart('#')??"20352e";
            for(int c=0;c<3;c++)colors[i*3+c]=(byte)Convert.ToInt32(color.Substring(c*2,2),16);
        }
        var tops=new Dictionary<(int,int),int>();
        foreach(var edit in edits)if(edit.Value!=0){var key=(edit.Key.X,edit.Key.Z);tops[key]=Math.Max(tops.GetValueOrDefault(key),edit.Key.Y);}
        var bytes=new byte[pixels*pixels*4];
        for(int j=0;j<pixels;j++){
            cancellation.ThrowIfCancellationRequested();
            for(int i=0;i<pixels;i++){
                int wx=x+i*step,wz=z+j*step,k=(i+j*pixels)*4;bytes[k+3]=255;
                if(wx<-Coordinates.Limit||wx>=Coordinates.Limit||wz<-Coordinates.Limit||wz>=Coordinates.Limit){bytes[k]=24;bytes[k+1]=32;bytes[k+2]=37;continue;}
                if(explored!=null&&!MapExploration.Known(explored,wx,wz)){bytes[k]=30;bytes[k+1]=36;bytes[k+2]=40;continue;}
                int h=terrain.Surface(wx,wz),top=Math.Min(Coordinates.WorldHeight-1,Math.Max(Math.Max(h+(terrain.GeneratorVersion>=4?13:10),Terrain.SeaLevel),tops.GetValueOrDefault((wx,wz))));ushort block=0;
                for(int y=top;y>=0;y--){var cell=new Cell(wx,y,wz);block=edits.TryGetValue(cell,out var edited)?edited:NaturalRules.Correct(terrain,catalog,cell,terrain.Get(cell));if(block!=0)break;}
                float shade=Math.Clamp(1+(h-terrain.Surface(wx-step,wz-step))*.035f,.7f,1.1f);
                for(int c=0;c<3;c++)bytes[k+c]=(byte)Math.Clamp(colors[block*3+c]*shade,0,255);
            }
        }
        return bytes;
    }
}
