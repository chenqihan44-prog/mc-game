using System;
using System.Collections.Generic;
using System.Numerics;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

// Separate sky and block light. Opaque cells stop both; light can travel
// around a cave bend, but cannot cross a sealed wall or ceiling.
public sealed class VoxelLighting
{
    public Cell Origin {get;}
    private readonly int width,height,depth;
    private readonly byte[] opacity,sky,glow;
    private VoxelLighting(Cell origin,int w,int h,int d){Origin=origin;width=w;height=h;depth=d;opacity=new byte[w*h*d];sky=new byte[w*h*d];glow=new byte[w*h*d];}
    private int Index(int x,int y,int z)=>x+width*(z+depth*y);
    public static VoxelLighting Build(Cell origin,int width,int height,int depth,Func<Cell,ushort> read,Catalog catalog,Func<int,int,int> skyTop,Func<Cell,bool>? open=null,Func<Cell,bool>? burning=null)
    {
        var field=new VoxelLighting(origin,width,height,depth);var skyQueue=new Queue<int>();var lightQueue=new Queue<int>();
        byte Opacity(Cell p,ushort number){var d=catalog.Block(number);if(d==null||open?.Invoke(p)==true)return 0;return d.Id=="water"?(byte)2:d.Id=="glass"?(byte)1:d.Id.EndsWith("leaves",StringComparison.Ordinal)?(byte)2:d.Solid&&d.Shape is "cube" or "slab" or "stairs"?(byte)15:d.Shape=="door"?(byte)15:(byte)0;}
        for(int z=0;z<depth;z++)for(int x=0;x<width;x++){
            int level=15,top=Math.Min(Coordinates.WorldHeight-1,Math.Max(origin.Y+height,skyTop(origin.X+x,origin.Z+z)));
            for(int y=top;y>=origin.Y;y--){var p=new Cell(origin.X+x,y,origin.Z+z);ushort id=read(p);byte block=Opacity(p,id);if(block>=15)level=0;else if(block>0)level=Math.Max(0,level-block);
                if(y>=origin.Y+height)continue;int i=field.Index(x,y-origin.Y,z);field.opacity[i]=block;field.sky[i]=(byte)level;if(level>1)skyQueue.Enqueue(i);
                string name=catalog.Id(id);int emission=name switch{"torch"=>14,"lantern" or "dawn_heart"=>15,"campfire"=>13,"energy_anchor" or "core_console"=>12,"furnace" when burning?.Invoke(p)==true=>13,"beacon" when burning?.Invoke(p)==true=>15,_=>0};
                if(emission>0){field.opacity[i]=0;field.glow[i]=(byte)emission;lightQueue.Enqueue(i);}
            }
        }
        field.Flood(field.sky,skyQueue);field.Flood(field.glow,lightQueue);return field;
    }
    private void Flood(byte[] values,Queue<int> queue)
    {
        while(queue.TryDequeue(out int i)){int level=values[i];if(level<=1)continue;int x=i%width,z=i/width%depth,y=i/(width*depth);
            void Visit(int nx,int ny,int nz){if(nx<0||nx>=width||ny<0||ny>=height||nz<0||nz>=depth)return;int n=Index(nx,ny,nz);int next=level-Math.Max(1,(int)opacity[n]);if(next<=values[n]||opacity[n]>=15)return;values[n]=(byte)next;queue.Enqueue(n);}
            Visit(x-1,y,z);Visit(x+1,y,z);Visit(x,y-1,z);Visit(x,y+1,z);Visit(x,y,z-1);Visit(x,y,z+1);
        }
    }
    public Vector2 At(Cell p){int x=p.X-Origin.X,y=p.Y-Origin.Y,z=p.Z-Origin.Z;if(x<0||x>=width||y<0||y>=height||z<0||z>=depth)return Vector2.Zero;int i=Index(x,y,z);return new(sky[i]/15f,glow[i]/15f);}
    public Vector2 Surface(Vector3 point,Vector3 normal)
    {
        Vector3 u=Math.Abs(normal.Y)>.5f?Vector3.UnitX:Vector3.UnitY,v=Vector3.Cross(normal,u);Vector2 sum=Vector2.Zero;
        // Four air samples at each corner avoid a hard seam between adjoining
        // faces and add gentle contact shading beside solid blocks.
        for(int a=0;a<2;a++)for(int b=0;b<2;b++){var p=point+normal*.08f+u*(a==0?-.28f:.28f)+v*(b==0?-.28f:.28f);sum+=At(new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Floor(p.Z)));}
        return sum*.25f;
    }
}
