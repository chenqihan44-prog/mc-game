using System;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class WeatherView : Node3D
{
    private WorldState world=null!;
    private MeshInstance3D clouds=null!;
    private ShaderMaterial cloudMaterial=null!;
    private MultiMeshInstance3D precipitation=null!;
    private MultiMesh drops=null!;
    private BoxMesh dropMesh=null!;
    private StandardMaterial3D dropMaterial=null!;
    private AudioStreamPlayer rainSound=null!;
    private AudioStreamWav rainClip=null!;
    private readonly Vector3[] positions=new Vector3[256];
    private readonly Random random=new(713);
    private double roofClock;
    private bool sheltered;
    private bool silent;
    private int lastBiome=-1;
    public void Initialize(WorldState value)
    {
        world=value;
        silent=DisplayServer.GetName()=="headless";
        cloudMaterial=new(){Shader=GD.Load<Shader>("res://assets/clouds.gdshader")};
        clouds=new(){Mesh=new PlaneMesh{Size=new(640,640)},MaterialOverride=cloudMaterial,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(clouds);
        dropMaterial=new(){AlbedoColor=new(.67f,.8f,.9f,.6f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
        dropMesh=new(){Size=new(.018f,.6f,.018f)};
        drops=new(){TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=dropMesh};drops.InstanceCount=positions.Length;
        precipitation=new(){Multimesh=drops,MaterialOverride=dropMaterial,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(precipitation);
        for(int i=0;i<positions.Length;i++)positions[i]=new(0,-1000,0);
        var bytes=new byte[22050*2];double smooth=0;var noise=new Random(712);
        for(int i=0;i<22050;i++){smooth=smooth*.75+(noise.NextDouble()*2-1)*.25;short sample=(short)(smooth*3800);bytes[i*2]=(byte)sample;bytes[i*2+1]=(byte)(sample>>8);}
        rainClip=new(){Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=22050,Data=bytes,LoopMode=AudioStreamWav.LoopModeEnum.Forward,LoopBegin=0,LoopEnd=22050};rainSound=new(){Bus="Ambience",Stream=rainClip};AddChild(rainSound);
    }
    public void Tick(double delta,Vector3 eye,float daylight,float volume,bool effects)
    {
        int biome=world.Terrain.Biome((int)Math.Floor(eye.X),(int)Math.Floor(eye.Z));var state=world.State.Weather;
        clouds.Position=new(eye.X,Math.Max(126,eye.Y+30),eye.Z);
        cloudMaterial.SetShaderParameter("cover",.28f+(float)state.Blend*.65f);
        cloudMaterial.SetShaderParameter("drift",new Vector2((float)world.State.Time*.8f,(float)world.State.Time*.25f));
        var color=new Color(.94f,.95f,.96f).Lerp(new(.4f,.45f,.5f),(float)state.Blend).Lerp(new(.13f,.17f,.23f),1-daylight);cloudMaterial.SetShaderParameter("cloud_color",new Vector3(color.R,color.G,color.B));
        bool snow=biome==3,wet=state.Phase==2&&biome!=2;
        if(biome!=lastBiome){lastBiome=biome;dropMesh.Size=snow?new(.09f,.09f,.09f):new(.018f,.6f,.018f);dropMaterial.AlbedoColor=snow?new(.95f,.98f,1,.85f):new(.67f,.8f,.9f,.6f);}
        roofClock-=delta;if(roofClock<=0){roofClock=.25;sheltered=false;var cell=new Cell((int)Math.Floor(eye.X),(int)Math.Floor(eye.Y)+1,(int)Math.Floor(eye.Z));for(int y=cell.Y;y<Coordinates.WorldHeight;y++)if(world.Solid(cell with{Y=y})){sheltered=true;break;}}
        precipitation.Visible=effects&&wet&&!sheltered;
        float loudness=!silent&&wet&&!snow?volume*(sheltered?.12f:.35f):0;
        if(loudness>0){rainSound.VolumeDb=Mathf.LinearToDb(loudness);if(!rainSound.Playing)rainSound.Play();}else rainSound.Stop();
        if(!precipitation.Visible)return;
        for(int i=0;i<positions.Length;i++){
            var p=positions[i];p+=new Vector3(snow?.25f:1,snow?-1.8f:-18,.15f)*(float)delta;
            if(p.Y<eye.Y-8||Math.Abs(p.X-eye.X)>11||Math.Abs(p.Z-eye.Z)>11||world.Solid(new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Floor(p.Z))))p=new(eye.X+(float)(random.NextDouble()*20-10),eye.Y+(float)(random.NextDouble()*8+2),eye.Z+(float)(random.NextDouble()*20-10));
            positions[i]=p;drops.SetInstanceTransform(i,new Transform3D(Basis.Identity,p));
        }
    }
    public void PauseAudio(bool paused){rainSound.StreamPaused=paused;}
    public override void _ExitTree(){rainSound.Stop();rainSound.Stream=null;rainClip.Dispose();}
}
