using System;
using System.Collections.Generic;
using EmberLighthouse.Core;
using EmberLighthouse.Items;

namespace EmberLighthouse.Rendering;

public readonly record struct Vertex(float X,float Y,float Z,float Nx,float Ny,float Nz,ushort Material);
public readonly record struct PlantVertex(float X,float Y,float Z,float U,float V);
public sealed class MeshData
{
    public List<Vertex> Vertices {get;}=[];
    public List<int> Indices {get;}=[];
    public List<Vertex> WaterVertices {get;}=[];
    public List<int> WaterIndices {get;}=[];
    public List<PlantVertex> PlantVertices {get;}=[];
    public List<int> PlantIndices {get;}=[];
    public List<float> Collision {get;}=[];
    public int Quads {get;set;}
}

public static class VoxelMesher
{
    public static MeshData Build(Func<Cell,ushort> read,Cell chunk,Catalog catalog,Func<Cell,bool>? doorOpen=null,Func<Cell,int>? rotation=null,Func<Cell,(int Stage,string? Crop)>? crop=null,Func<Cell,bool>? groundLog=null)
    {
        var result=new MeshData();var origin=Coordinates.Origin(chunk);int[] dims=[16,32,16];
        // Greedy merging within a material/direction. The one-cell halo removes cross-chunk faces.
        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2){
            int u=(axis+1)%3,v=(axis+2)%3;var mask=new ushort[dims[u]*dims[v]];var xyz=new int[3];
            for(int slice=0;slice<dims[axis];slice++){
                for(int j=0;j<dims[v];j++)for(int i=0;i<dims[u];i++){
                    xyz[axis]=slice;xyz[u]=i;xyz[v]=j;var p=origin+new Cell(xyz[0],xyz[1],xyz[2]);
                    ushort id=read(p);var def=catalog.Block(id);xyz[axis]+=sign;ushort adjacent=read(origin+new Cell(xyz[0],xyz[1],xyz[2]));var other=catalog.Block(adjacent);
                    var neighbor=p+new Cell(axis==0?sign:0,axis==1?sign:0,axis==2?sign:0);
                    bool visible=def!=null&&def.Shape=="cube"&&!(groundLog?.Invoke(p)??false)&&(other==null||!other.Solid||other.Shape!="cube"||(groundLog?.Invoke(neighbor)??false))&&(adjacent!=id||(groundLog?.Invoke(neighbor)??false));
                    mask[i+j*dims[u]]=visible?id:(ushort)0;
                }
                for(int j=0;j<dims[v];j++)for(int i=0;i<dims[u];){
                    ushort id=mask[i+j*dims[u]];if(id==0){i++;continue;}
                    int width=1;while(i+width<dims[u]&&mask[i+width+j*dims[u]]==id)width++;
                    int height=1;bool same=true;
                    while(j+height<dims[v]&&same){for(int k=0;k<width;k++)if(mask[i+k+(j+height)*dims[u]]!=id){same=false;break;}if(same)height++;}
                    var a=new float[3];a[axis]=slice+(sign>0?1:0);a[u]=i;a[v]=j;
                    var b=(float[])a.Clone();b[u]+=width;var c=(float[])b.Clone();c[v]+=height;var d=(float[])a.Clone();d[v]+=height;
                    if(sign<0)(b,d)=(d,b);
                    AddQuad(result,a,b,c,d,axis,sign,id,catalog.Items[catalog.Id(id)].Solid,catalog.Id(id)=="water"||catalog.Id(id)=="glass");
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)mask[i+x+(j+y)*dims[u]]=0;i+=width;
                }
            }
        }
        for(int y=0;y<32;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++){
            var p=origin+new Cell(x,y,z);ushort id=read(p);var d=catalog.Block(id);if(d==null)continue;
            if(groundLog?.Invoke(p)??false){GroundLog(result,x,y,z,id,catalog.Number("plank"),(p.X+p.Z)&1);continue;}
            if(d.Shape=="cube")continue;
            bool open=d.Shape=="door"&&(doorOpen?.Invoke(p)??false);int r=rotation?.Invoke(p)??0;
            switch(d.Shape){
                case "bush":
                    var plant=crop?.Invoke(p)??(-1,(string?)null);float height=plant.Item1<0?.95f:.25f+plant.Item1*.2f;
                    int style=plant.Item2=="wheat_seed"?(plant.Item1==3?2:1):plant.Item2=="carrot_seed"?3:plant.Item2=="potato_seed"?4:0;
                    CrossPlant(result,x,y,z,height,style);
                    break;
                case "stairs":Box(result,x,y,z,0,0,0,1,.5f,1,id,d.Solid,r);Box(result,x,y,z,0,.5f,.5f,1,1,1,id,d.Solid,r);break;
                case "slab":Box(result,x,y,z,0,0,0,1,.5f,1,id,d.Solid,r);break;
                case "rug":Box(result,x,y,z,0,0,0,1,.06f,1,id,false,r);break;
                case "door":Box(result,x,y,z,0,0,0,1,1,.15f,id,d.Solid,(r+(open?1:0))%4);break;
                case "window":Box(result,x,y,z,0,0,0,1,1,.15f,id,d.Solid,r);break;
                case "fence":Box(result,x,y,z,.35f,0,.35f,.65f,1,.65f,id,d.Solid,r);Box(result,x,y,z,0,.3f,.43f,1,.5f,.57f,id,d.Solid,r);Box(result,x,y,z,0,.75f,.43f,1,.9f,.57f,id,d.Solid,r);break;
            }
        }
        return result;
    }
    private static void GroundLog(MeshData mesh,int x,int y,int z,ushort bark,ushort end,int rotation)
    {
        float[] Point(float along,float angle){var p=new float[]{along,.17f+MathF.Sin(angle)*.13f,.5f+MathF.Cos(angle)*.13f};if(rotation==1){float px=p[0];p[0]=1-p[2];p[2]=px;}p[0]+=x;p[1]+=y;p[2]+=z;return p;}
        for(int i=0;i<8;i++){
            float a=i*MathF.Tau/8,b=(i+1)*MathF.Tau/8;
            AddQuad(mesh,Point(.1f,a),Point(.9f,a),Point(.9f,b),Point(.1f,b),1,1,bark,false,false);
            foreach(float along in new[]{.1f,.9f}){
                var center=Point(along,0);center[1]=y+.17f;if(rotation==0)center[2]=z+.5f;else center[0]=x+.5f;
                AddQuad(mesh,center,Point(along,a),Point(along,b),center,rotation==0?0:2,along<.5f?-1:1,end,false,false);
            }
        }
    }
    private static void CrossPlant(MeshData mesh,int x,int y,int z,float height,int style)
    {
        float u0=style/5f,u1=(style+1)/5f;
        for(int plane=0;plane<2;plane++){
            int start=mesh.PlantVertices.Count;float z0=plane==0?.08f:.92f,z1=plane==0?.92f:.08f;
            mesh.PlantVertices.AddRange([new(x+.08f,y,z+z0,u0,1),new(x+.92f,y,z+z1,u1,1),new(x+.92f,y+height,z+z1,u1,0),new(x+.08f,y+height,z+z0,u0,0)]);
            foreach(int index in new[]{0,2,1,0,3,2})mesh.PlantIndices.Add(start+index);
        }
    }
    private static void Box(MeshData mesh,int x,int y,int z,float x0,float y0,float z0,float x1,float y1,float z1,ushort id,bool solid,int rotation)
    {
        float[] lo=[x0,y0,z0],hi=[x1,y1,z1];
        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2){
            int u=(axis+1)%3,v=(axis+2)%3;var a=new float[3];a[axis]=sign>0?hi[axis]:lo[axis];a[u]=lo[u];a[v]=lo[v];var b=(float[])a.Clone();b[u]=hi[u];var c=(float[])b.Clone();c[v]=hi[v];var d=(float[])a.Clone();d[v]=hi[v];if(sign<0)(b,d)=(d,b);
            foreach(var p in new[]{a,b,c,d}){for(int r=0;r<rotation;r++){float xx=p[0];p[0]=1-p[2];p[2]=xx;}p[0]+=x;p[1]+=y;p[2]+=z;}
            int normalAxis=axis,normalSign=sign;if(axis!=1)for(int r=0;r<rotation;r++){if(normalAxis==0){normalAxis=2;}else{normalAxis=0;normalSign=-normalSign;}}
            AddQuad(mesh,a,b,c,d,normalAxis,normalSign,id,solid,false);
        }
    }
    private static void AddQuad(MeshData data,float[] a,float[] b,float[] c,float[] d,int axis,int sign,ushort id,bool solid,bool water)
    {
        var vertices=water?data.WaterVertices:data.Vertices;var indices=water?data.WaterIndices:data.Indices;int start=vertices.Count;float[] n=new float[3];n[axis]=sign;
        foreach(var p in new[]{a,b,c,d})vertices.Add(new(p[0],p[1],p[2],n[0],n[1],n[2],id));
        // Godot front faces use clockwise winding.
        foreach(int i in new[]{0,2,1,0,3,2})indices.Add(start+i);
        if(solid)foreach(int i in new[]{0,2,1,0,3,2}){float[] p=i==0?a:i==1?b:i==2?c:d;data.Collision.AddRange(p);}
        data.Quads++;
    }
}
