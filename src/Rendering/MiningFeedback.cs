using System;
using System.Collections.Generic;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.Rendering;

public partial class MiningFeedback : Node3D
{
    private readonly List<Resource> owned=[];
    private readonly List<(MeshInstance3D Mesh,Vector3 Velocity,float Life)> chips=[];
    private readonly ImageTexture[] stages=new ImageTexture[10];
    private Node3D cracks=null!;
    private StandardMaterial3D material=null!;
    private readonly Random random=new(731);
    private double dustClock;
    private string shapeKey="";
    private QuadMesh faceMesh=null!;
    public int ActiveChips=>chips.Count;
    public int CrackStage {get;private set;}=-1;
    public override void _Ready()
    {
        material=new(){Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor,AlphaScissorThreshold=.5f,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,CullMode=BaseMaterial3D.CullModeEnum.Disabled};owned.Add(material);
        for(int stage=0;stage<10;stage++){
            using var image=Image.CreateEmpty(32,32,false,Image.Format.Rgba8);image.Fill(Colors.Transparent);
            var rng=new Random(97);for(int branch=0;branch<stage+2;branch++){
                int x=16,y=16;for(int n=0;n<3+stage;n++){int nx=Math.Clamp(x+rng.Next(-5,6),0,31),ny=Math.Clamp(y+rng.Next(-5,6),0,31);int steps=Math.Max(Math.Abs(nx-x),Math.Abs(ny-y));for(int p=0;p<=steps;p++){float t=steps==0?0:(float)p/steps;int px=(int)MathF.Round(x+(nx-x)*t),py=(int)MathF.Round(y+(ny-y)*t);image.SetPixel(px,py,new("29231e"));if(stage>5&&px<31)image.SetPixel(px+1,py,new("29231e"));}x=nx;y=ny;}
            }stages[stage]=ImageTexture.CreateFromImage(image);owned.Add(stages[stage]);
        }
        cracks=new(){Visible=false};AddChild(cracks);
        faceMesh=new(){Size=Vector2.One};owned.Add(faceMesh);
    }
    public void SetTarget(WorldState world,Cell? cell,double progress)
    {
        cracks.Visible=cell.HasValue&&progress>0&&world.Get(cell.Value)!=0;CrackStage=cracks.Visible?Math.Clamp((int)(progress*10),0,9):-1;if(!cracks.Visible)return;
        var p=cell!.Value;var def=world.Catalog.Block(world.Get(p))!;bool looseWood=def.Id=="wood"&&world.Terrain.IsLooseWood(p)&&!world.Changes.ContainsKey(p)&&!world.State.Entities.ContainsKey(p.ToString());
        string key=looseWood?"ground_log":def.Shape;if(shapeKey!=key){shapeKey=key;foreach(var child in cracks.GetChildren()){cracks.RemoveChild(child);child.QueueFree();}
            switch(key){
                case "ground_log":Box(new(.1f,.04f,.37f),new(.9f,.30f,.63f));break;
                case "slab":Box(Vector3.Zero,new(1,.5f,1));break;
                case "rug":Box(Vector3.Zero,new(1,.06f,1));break;
                case "door":case "window":Box(Vector3.Zero,new(1,1,.15f));break;
                case "stairs":Box(Vector3.Zero,new(1,.5f,1));Box(new(0,.5f,.5f),Vector3.One);break;
                case "fence":Box(new(.35f,0,.35f),new(.65f,1,.65f));Box(new(0,.3f,.43f),new(1,.5f,.57f));Box(new(0,.75f,.43f),new(1,.9f,.57f));break;
                case "bush":Box(new(.08f,0,.08f),new(.92f,.95f,.92f));break;
                default:Box(Vector3.Zero,Vector3.One);break;
            }
        }
        int rotation=world.State.Entities.TryGetValue(p.ToString(),out var entity)?entity.Rotation:0;if(entity?.Open==true&&def.Shape=="door")rotation++;
        if(looseWood)rotation=(p.X+p.Z)&1;cracks.Rotation=new(0,-rotation*Mathf.Pi/2,0);cracks.Position=new(p.X+.5f,p.Y+.5f,p.Z+.5f);material.AlbedoTexture=stages[CrackStage];
    }
    private void Box(Vector3 low,Vector3 high)
    {
        var size=high-low;var center=(low+high)/2-Vector3.One/2;var normals=new[]{Vector3.Back,Vector3.Forward,Vector3.Right,Vector3.Left,Vector3.Up,Vector3.Down};
        for(int face=0;face<6;face++){
            bool horizontal=face>=4;var dimensions=horizontal?new Vector2(size.X,size.Z):face<2?new(size.X,size.Y):new(size.Z,size.Y);
            var panel=new MeshInstance3D{Mesh=faceMesh,MaterialOverride=material,Scale=new(dimensions.X,dimensions.Y,1),Position=center+normals[face]*(horizontal?size.Y/2+.003f:face<2?size.Z/2+.003f:size.X/2+.003f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            panel.Rotation=face switch{1=>new(0,Mathf.Pi,0),2=>new(0,Mathf.Pi/2,0),3=>new(0,-Mathf.Pi/2,0),4=>new(-Mathf.Pi/2,0,0),5=>new(Mathf.Pi/2,0,0),_=>Vector3.Zero};cracks.AddChild(panel);
        }
    }
    public void Tick(double delta,WorldState world,Cell? target,double progress)
    {
        SetTarget(world,target,progress);dustClock-=delta;if(progress>0&&target.HasValue&&world.Get(target.Value)!=0&&dustClock<=0){dustClock=.13;Emit(target.Value,world.Catalog.Block(world.Get(target.Value))!,2);}
        float dt=(float)Math.Min(delta,.1);for(int i=chips.Count-1;i>=0;i--){var chip=chips[i];float life=chip.Life-dt;if(life<=0){chip.Mesh.QueueFree();chips.RemoveAt(i);continue;}var velocity=chip.Velocity+Vector3.Down*(dt*8);chip.Mesh.Position+=velocity*dt;chip.Mesh.Rotation+=new Vector3(3,4,2)*dt;chip.Mesh.Scale=Vector3.One*Math.Min(1,life*5);chips[i]=(chip.Mesh,velocity,life);}
    }
    public void Emit(Cell cell,ItemDef item,int count=18)
    {
        using var mesh=new BoxMesh{Size=Vector3.One*.065f};using var tint=new StandardMaterial3D{AlbedoColor=Color.FromString(item.Color,Colors.White),Roughness=1};
        for(int i=0;i<count&&chips.Count<48;i++){
            var particle=new MeshInstance3D{Mesh=mesh,MaterialOverride=tint,Position=new(cell.X+.5f+(float)(random.NextDouble()-.5)*.8f,cell.Y+.5f+(float)(random.NextDouble()-.5)*.5f,cell.Z+.5f+(float)(random.NextDouble()-.5)*.8f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(particle);chips.Add((particle,new((float)(random.NextDouble()-.5)*2.5f,(float)random.NextDouble()*2+1,(float)(random.NextDouble()-.5)*2.5f),.35f+(float)random.NextDouble()*.4f));
        }
    }
    public void HideCracks(){if(cracks!=null)cracks.Visible=false;CrackStage=-1;}
    public override void _ExitTree(){foreach(var resource in owned)resource.Dispose();owned.Clear();chips.Clear();}
}
