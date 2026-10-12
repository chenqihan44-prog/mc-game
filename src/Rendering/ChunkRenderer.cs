using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class ChunkRenderer : Node3D
{
    private WorldState world=null!;
    private readonly Dictionary<Cell,Node3D> loaded=[];
    // Keep peers alive until unload; dispose on the game thread rather than
    // racing native deletion with resource finalizers.
    private readonly Dictionary<Cell,List<Resource>> chunkResources=[];
    private readonly Dictionary<Cell,(int Revision,Task<MeshData> Task)> pending=[];
    private readonly Dictionary<Cell,int> renderedRevision=[];
    private readonly Dictionary<Cell,int> renderedCollisionRevision=[];
    private readonly Dictionary<Cell,VoxelLighting> lightFields=[];
    public void SetDaylight(float value)=>((ShaderMaterial)opaque).SetShaderParameter("daylight",value);
    public float SkyAt(Vector3 position){var p=new Cell((int)Math.Floor(position.X),(int)Math.Floor(position.Y),(int)Math.Floor(position.Z));return lightFields.TryGetValue(Coordinates.Chunk(p),out var field)?field.At(p).X:position.Y>world.Terrain.Surface(p.X,p.Z)+1?1:0;}
    public Color ItemLightAt(Vector3 position)
    {
        var p=new Cell((int)Math.Floor(position.X),(int)Math.Floor(position.Y),(int)Math.Floor(position.Z));var light=lightFields.TryGetValue(Coordinates.Chunk(p),out var field)?field.At(p):new System.Numerics.Vector2(SkyAt(position),0);
        float day=(float)DayCycle.Daylight(world.State.SkyTime)*(1-(float)world.State.Weather.Blend*.35f),sky=MathF.Pow(light.X,1.6f)*(.025f+day*.975f),torch=MathF.Pow(light.Y,1.8f)*1.45f;
        return new(Math.Max(.0025f,Math.Max(sky*(.16f+.84f*day),torch)),Math.Max(.0025f,Math.Max(sky*(.21f+.77f*day),torch*.7f)),Math.Max(.0025f,Math.Max(sky*(.34f+.6f*day),torch*.36f)));
    }
    private readonly HashSet<Cell> desired=[];
    private Material opaque=null!,transparent=null!;
    private Material plants=null!;
    private Color[] blockColors=[];
    private byte[] blockStyles=[];
    public int Radius {get;set;}=4;
    public int Loaded=>loaded.Count;
    public int Pending=>pending.Count;
    public bool HasPending(Cell chunk)=>pending.ContainsKey(chunk);
    public int StaleResultsDiscarded {get;private set;}
    public double LastEditMs {get;private set;}
    public void Initialize(WorldState model)
    {
        world=model;
        blockColors=world.Catalog.BlockIds.Select(id=>id=="air"?Colors.White:Color.FromString(world.Catalog.Items[id].Color,Colors.White)).ToArray();
        blockStyles=world.Catalog.BlockIds.Select(MaterialStyle.For).ToArray();
        opaque=new ShaderMaterial{Shader=GD.Load<Shader>("res://assets/voxel.gdshader")};
        transparent=new StandardMaterial3D{VertexColorUseAsAlbedo=true,Roughness=.5f,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
        plants=new StandardMaterial3D{AlbedoTexture=PlantSprites.CreateAtlas(),TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor,AlphaScissorThreshold=.5f,CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=1};
    }
    public void Stream(Vector3 position)
    {
        var player=Coordinates.Chunk(new((int)Math.Floor(position.X),(int)Math.Floor(position.Y),(int)Math.Floor(position.Z)));
        desired.Clear();
        // Full height uses sparse empty chunk nodes; data is generated on demand.
        for(int dz=-Radius;dz<=Radius;dz++)for(int dx=-Radius;dx<=Radius;dx++)if(dx*dx+dz*dz<=Radius*Radius)
            for(int y=0;y<6;y++)desired.Add(new(player.X+dx,y,player.Z+dz));
        foreach(var c in loaded.Keys.ToArray())if(!desired.Contains(c)){var old=loaded[c];RemoveChild(old);ReleaseResources(c);old.QueueFree();loaded.Remove(c);renderedRevision.Remove(c);renderedCollisionRevision.Remove(c);lightFields.Remove(c);}
        int submits=0;
        foreach(var c in pending.Keys.ToArray()){
            var p=pending[c];if(!p.Task.IsCompleted)continue;
            pending.Remove(c);
            if(p.Task.IsFaulted){GD.PushError(p.Task.Exception);continue;}
            if(!desired.Contains(c))continue;
            if(p.Revision!=world.Revision(c)){StaleResultsDiscarded++;continue;}
            Apply(c,p.Task.Result);renderedRevision[c]=p.Revision;if(++submits>=2)break;
        }
        var order=desired.Where(c=>!pending.ContainsKey(c)&&(!renderedRevision.TryGetValue(c,out var revision)||revision!=world.Revision(c)))
            .OrderBy(c=>(c.X-player.X)*(c.X-player.X)+(c.Z-player.Z)*(c.Z-player.Z)+Math.Abs(c.Y-player.Y)*.15).Take(Math.Max(0,4-pending.Count));
        foreach(var c in order){int revision=world.Revision(c);var changes=world.CaptureRegion(c.X*16+8,c.Z*16+8,32);var entities=world.State.Entities.ToDictionary(p=>Cell.Parse(p.Key),p=>(p.Value.Open,p.Value.Kind=="torch"?p.Value.Stage:p.Value.Rotation));
            var crops=world.State.Entities.Where(p=>p.Value.Kind=="crop").ToDictionary(p=>Cell.Parse(p.Key),p=>(p.Value.Stage,p.Value.CropId));
            var burning=ActiveSources();pending[c]=(revision,Task.Run(()=>Build(c,changes,entities,crops,burning)));
        }
    }
    private HashSet<Cell> ActiveSources()=>world.State.Entities.Where(p=>p.Value.Kind=="furnace"&&p.Value.RecipeId!=null).Select(p=>Cell.Parse(p.Key)).Concat(world.State.Beacons.Select(i=>{var p=world.Terrain.TowerLocations[i];return p with{Y=world.Terrain.Surface(p.X,p.Z)+1};})).ToHashSet();
    private MeshData Build(Cell c,Dictionary<Cell,ushort> changes,Dictionary<Cell,(bool Open,int Rotation)> entities,Dictionary<Cell,(int Stage,string? Crop)> crops,HashSet<Cell> burning,bool calculateLighting=true)
    {
        if(c.Y>=3&&c.X>=-128&&c.X<128&&c.Z>=-128&&c.Z<128&&!changes.Keys.Any(p=>Coordinates.Chunk(p)==c)){
            if(world.Terrain.GeneratorVersion<4)return new();
            int max=0;for(int x=c.X*16-4;x<c.X*16+20;x++)for(int z=c.Z*16-4;z<c.Z*16+20;z++)max=Math.Max(max,world.Terrain.Surface(x,z)+13);
            if(max<c.Y*32)return new();
        }
        var o=Coordinates.Origin(c);var halo=new ushort[18*34*18];
        for(int y=-1;y<=32;y++)for(int z=-1;z<=16;z++)for(int x=-1;x<=16;x++){
            var p=o+new Cell(x,y,z);halo[x+1+18*(z+1+18*(y+1))]=changes.TryGetValue(p,out var id)?id:NaturalRules.Correct(world.Terrain,world.Catalog,p,world.Terrain.Get(p),entities.ContainsKey(p));
        }
        ushort Read(Cell p){var a=p-o;return halo[a.X+1+18*(a.Z+1+18*(a.Y+1))];}
        ushort wood=world.Catalog.Number("wood");
        var data=VoxelMesher.Build(Read,c,world.Catalog,p=>entities.TryGetValue(p,out var e)&&e.Open,p=>entities.TryGetValue(p,out var e)?e.Rotation:0,p=>crops.TryGetValue(p,out var crop)?crop:(-1,null),p=>Read(p)==wood&&!changes.ContainsKey(p)&&!entities.ContainsKey(p)&&world.Terrain.IsLooseWood(p));
        if(calculateLighting&&data.Vertices.Count+data.WaterVertices.Count+data.PlantVertices.Count>0){
            ushort LightRead(Cell p)=>changes.TryGetValue(p,out var id)?id:NaturalRules.Correct(world.Terrain,world.Catalog,p,world.Terrain.Get(p),entities.ContainsKey(p));
            int highestEdit=changes.Count==0?0:changes.Keys.Max(p=>p.Y)+1;
            data.Lighting=VoxelLighting.Build(o+new Cell(-15,-15,-15),46,62,46,LightRead,world.Catalog,(x,z)=>Math.Max(highestEdit,world.Terrain.Surface(x,z)+(world.Terrain.Adventure?.Nearby(new Cell(x,40,z))==null?15:48)),p=>entities.TryGetValue(p,out var entity)&&entity.Open,p=>burning.Contains(p));
        }
        return data;
    }
    public bool IsTerrainReady(Vector3 position)=>IsReady(position,false);
    public bool IsVisualReady(Vector3 position)=>IsReady(position,true);
    private bool IsReady(Vector3 position,bool visual)
    {
        if(!float.IsFinite(position.X)||!float.IsFinite(position.Y)||!float.IsFinite(position.Z)||position.X<-Coordinates.Limit||position.X>=Coordinates.Limit||position.Z<-Coordinates.Limit||position.Z>=Coordinates.Limit)return false;
        // Above the building height still depends on the loaded terrain column;
        // otherwise every vertical neighbor is skipped and an empty world is "ready".
        var c=Coordinates.Chunk(new((int)Math.Floor(position.X),(int)Math.Floor(Math.Clamp(position.Y,0,Coordinates.WorldHeight-1)),(int)Math.Floor(position.Z)));
        for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)for(int dy=-1;dy<=1;dy++){
            var near=c+new Cell(dx,dy,dz);if(near.Y<0||near.Y>=6)continue;
            // Lighting-only updates keep their existing, valid collision.
            // Waiting for every visual neighbor used to freeze a jumping player.
            var revisions=visual?renderedRevision:renderedCollisionRevision;
            if(!revisions.TryGetValue(near,out int r)||r!=(visual?world.Revision(near):world.CollisionRevision(near)))return false;
        }
        return true;
    }
    public void RefreshEdit(Cell p)
    {
        var timer=Stopwatch.StartNew();var chunks=new HashSet<Cell>{Coordinates.Chunk(p)};
        foreach(var n in Coordinates.Neighbors)chunks.Add(Coordinates.Chunk(p+n));
        var entities=world.State.Entities.ToDictionary(e=>Cell.Parse(e.Key),e=>(e.Value.Open,e.Value.Kind=="torch"?e.Value.Stage:e.Value.Rotation));
        var crops=world.State.Entities.Where(p=>p.Value.Kind=="crop").ToDictionary(p=>Cell.Parse(p.Key),p=>(p.Value.Stage,p.Value.CropId));
        var burning=ActiveSources();foreach(var c in chunks)if(loaded.ContainsKey(c)){
            // Commit geometry now using the last available lighting. The full
            // light flood runs on the streaming workers, away from the jump.
            var data=Build(c,world.CaptureRegion(c.X*16+8,c.Z*16+8,32),entities,crops,burning,false);data.Lighting=lightFields.GetValueOrDefault(c);Apply(c,data);renderedRevision[c]=-1;
        }
        LastEditMs=timer.Elapsed.TotalMilliseconds;
    }
    private void Apply(Cell c,MeshData data)
    {
        renderedCollisionRevision[c]=world.CollisionRevision(c);
        // Remove old collision immediately; mesh and collision become visible in the same physics frame.
        if(loaded.Remove(c,out var old)){RemoveChild(old);ReleaseResources(c);old.QueueFree();}
        var node=new Node3D{Position=new(c.X*16,c.Y*32,c.Z*16)};AddChild(node);loaded[c]=node;
        if(data.Lighting!=null)lightFields[c]=data.Lighting;else lightFields.Remove(c);
        var mesh=new ArrayMesh();AddSurface(mesh,data.Vertices,data.Indices,opaque,false,c);AddSurface(mesh,data.WaterVertices,data.WaterIndices,transparent,true,c);
        if(data.PlantVertices.Count>0){
            var vertices=data.PlantVertices.Select(p=>new Vector3(p.X,p.Y,p.Z)).ToArray();var uv=data.PlantVertices.Select(p=>new Vector2(p.U,p.V)).ToArray();
            var normals=data.PlantVertices.Select(_=>Vector3.Up).ToArray();using var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);arrays[(int)Mesh.ArrayType.Vertex]=vertices;arrays[(int)Mesh.ArrayType.Normal]=normals;arrays[(int)Mesh.ArrayType.TexUV]=uv;arrays[(int)Mesh.ArrayType.Index]=data.PlantIndices.ToArray();SubmitSurface(mesh,arrays,plants);
        }
        var resources=new List<Resource>();chunkResources[c]=resources;
        if(mesh.GetSurfaceCount()>0){resources.Add(mesh);node.AddChild(new MeshInstance3D{Mesh=mesh});}else mesh.Dispose();
        if(data.Collision.Count>0){var faces=new Vector3[data.Collision.Count/3];for(int i=0;i<faces.Length;i++)faces[i]=new(data.Collision[i*3],data.Collision[i*3+1],data.Collision[i*3+2]);
            var shape=new ConcavePolygonShape3D{Data=faces,BackfaceCollision=true};resources.Add(shape);
            var body=new StaticBody3D();body.AddChild(new CollisionShape3D{Shape=shape});node.AddChild(body);
        }
    }
    private void ReleaseResources(Cell chunk){if(chunkResources.Remove(chunk,out var resources))foreach(var resource in resources)resource.Dispose();}
    public override void _ExitTree(){foreach(var chunk in chunkResources.Keys.ToArray())ReleaseResources(chunk);}
    private void AddSurface(ArrayMesh mesh,List<Vertex> source,List<int> index,Material material,bool water,Cell chunk)
    {
        if(source.Count==0)return;if(!water)source=Refine(source);var uv=new Vector2[source.Count];var vertices=new Vector3[source.Count];var normals=new Vector3[source.Count];var colors=new Color[source.Count];
        byte style=0;var origin=Coordinates.Origin(chunk);
        for(int i=0;i<source.Count;i++){
            var v=source[i];if(lightFields.TryGetValue(chunk,out var field)){var light=field.Surface(new System.Numerics.Vector3(v.X+origin.X,v.Y+origin.Y,v.Z+origin.Z),new(v.Nx,v.Ny,v.Nz));uv[i]=new(light.X,light.Y);}vertices[i]=new(v.X,v.Y,v.Z);normals[i]=new(v.Nx,v.Ny,v.Nz);var color=blockColors[v.Material];float shade=v.Ny>0?1:v.Ny<0?.58f:.82f;
            if(i%4==0&&!water){
                style=blockStyles[v.Material];
                if(style is 2 or 3 or 6 or 13){var center=Vector3.Zero;for(int j=0;j<4;j++){var corner=source[i+j];center+=new Vector3(corner.X,corner.Y,corner.Z)*.25f;}center-=new Vector3(v.Nx,v.Ny,v.Nz)*.002f;var cell=origin+new Cell((int)Math.Floor(center.X),(int)Math.Floor(center.Y),(int)Math.Floor(center.Z));
                    world.State.Entities.TryGetValue(cell.ToString(),out var entity);
                    if(style==6&&entity?.Stage==1)style=7;
                    if(style==13){int r=entity?.Rotation??0;bool front=r switch{0=>v.Nz<-.5f,1=>v.Nx>.5f,2=>v.Nz>.5f,_=>v.Nx<-.5f};if(!front)style=30;}
                    if(style is 2 or 3&&entity==null&&!world.Changes.ContainsKey(cell)&&world.Terrain.IsLooseWood(cell)&&world.Get(cell)==world.Catalog.Number("wood"))style=v.Ny>.5f?(byte)18:(byte)19;
                }
            }
            // Opaque shader uses alpha only as a tag; it never writes ALPHA.
            colors[i]=new(color.R*shade,color.G*shade,color.B*shade,water?.65f:style/255f);
        }
        using var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);arrays[(int)Mesh.ArrayType.Vertex]=vertices;arrays[(int)Mesh.ArrayType.Normal]=normals;arrays[(int)Mesh.ArrayType.Color]=colors;arrays[(int)Mesh.ArrayType.TexUV]=uv;arrays[(int)Mesh.ArrayType.Index]=water?index.ToArray():Enumerable.Range(0,source.Count/4).SelectMany(q=>new[]{q*4,q*4+2,q*4+1,q*4,q*4+3,q*4+2}).ToArray();
        SubmitSurface(mesh,arrays,material);
    }
    private static List<Vertex> Refine(List<Vertex> source)
    {
        var result=new List<Vertex>();for(int i=0;i<source.Count;i+=4){var a=source[i];var b=source[i+1];var c=source[i+2];var d=source[i+3];
            if(Math.Abs(c.X-b.X-d.X+a.X)+Math.Abs(c.Y-b.Y-d.Y+a.Y)+Math.Abs(c.Z-b.Z-d.Z+a.Z)>.001f){result.AddRange(source.GetRange(i,4));continue;}
            float Distance(Vertex p,Vertex q)=>MathF.Sqrt((p.X-q.X)*(p.X-q.X)+(p.Y-q.Y)*(p.Y-q.Y)+(p.Z-q.Z)*(p.Z-q.Z));
            int nx=Math.Max(1,(int)Math.Ceiling(Distance(a,b))),ny=Math.Max(1,(int)Math.Ceiling(Distance(a,d)));
            Vertex Point(float u,float v)=>new(a.X+(b.X-a.X)*u+(d.X-a.X)*v,a.Y+(b.Y-a.Y)*u+(d.Y-a.Y)*v,a.Z+(b.Z-a.Z)*u+(d.Z-a.Z)*v,a.Nx,a.Ny,a.Nz,a.Material);
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)result.AddRange([Point(x/(float)nx,y/(float)ny),Point((x+1)/(float)nx,y/(float)ny),Point((x+1)/(float)nx,(y+1)/(float)ny),Point(x/(float)nx,(y+1)/(float)ny)]);
        }return result;
    }
    private static void SubmitSurface(ArrayMesh mesh,Godot.Collections.Array arrays,Material material)
    {
        // The generated binding creates native collections for omitted optional arguments.
        // Own every collection across the native call, including the empty blend shapes/LODs.
        var blendShapes=new Godot.Collections.Array<Godot.Collections.Array>();
        using var blendStorage=(Godot.Collections.Array)blendShapes;
        using var lods=new Godot.Collections.Dictionary();
        int before=mesh.GetSurfaceCount();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays,blendShapes,lods);
        GC.KeepAlive(arrays);GC.KeepAlive(blendShapes);GC.KeepAlive(blendStorage);GC.KeepAlive(lods);
        if(mesh.GetSurfaceCount()!=before+1)throw new InvalidOperationException("区块网格提交失败");
        mesh.SurfaceSetMaterial(before,material);
    }
}

