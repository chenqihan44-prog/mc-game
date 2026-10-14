using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class CreatureModel
{
    private sealed record ModelProfile(string Id,string Family,float Height,float BodyHeight,float LabelHeight,float Stride,float SourceHeight=0,string Style="lux3d");
    private static Dictionary<string,ModelProfile>? profiles;
    private static readonly Dictionary<string,PackedScene> generatedScenes=[];
    private readonly Dictionary<string,int> boneIndices=[];
    private readonly Dictionary<int,Quaternion> restRotations=[];
    private readonly Dictionary<int,Quaternion> localRestRotations=[];
    private Skeleton3D? skeleton;
    private ModelProfile profile=null!;
    private Node3D visual=null!;
    private float motionTime,gaitPhase,smoothSpeed,turnLean,previousYaw;
    private float hipHeight;
    private float modelScale=1;
    private readonly List<(int Bone,Vector3 Local)> footSamples=[];
    public bool Generated {get;private set;}
    public string MotionPhase {get;private set;}="idle";
    public Skeleton3D? GeneratedSkeleton=>skeleton;
    public Vector3 BoundSize {get;private set;}
    public float VisualHeight=>Generated?profile.Height:BodyHeight;
    public bool PixelModel=>Generated&&profile.Style=="block-v2";
    public float LowestFootHeight=>footSamples.Count==0?0:MinimumFootY()*modelScale+visual.Position.Y;
    public int FootSampleCount=>footSamples.Count;
    public Vector3 CollisionSize=>new(Mathf.Clamp(BoundSize.X*.8f,.25f,.6f),BodyHeight,Mathf.Clamp(BoundSize.Z*.55f,.25f,.6f));
    public Node3D? AttachBodyAccessory(Node3D accessory)
    {
        if(skeleton==null){AddChild(accessory);return null;}
        int body=skeleton.FindBone("Body");var attachment=new BoneAttachment3D{BoneName="Body",BoneIdx=body};
        skeleton.AddChild(attachment);attachment.AddChild(accessory);
        accessory.Transform=skeleton.GetBoneGlobalRest(body).AffineInverse()*new Transform3D(Basis.Identity.Scaled(Vector3.One/modelScale),Vector3.Zero);return attachment;
    }
    public float StrikeDuration(string style)=>style switch{"slam"=>.22f,"bite"=>.13f,"melee"=>.18f,"bolt"=>.12f,"explode"=>.08f,_=>.1f};
    public float RecoveryDuration(string style)=>style switch{"slam"=>.8f,"melee"=>.42f,"bite"=>.28f,"bolt"=>.35f,"charge"=>.6f,_=>.3f};

    private bool LoadGenerated(string kind)
    {
        if(profiles==null){
            const string file="res://data/creature_models.json";
            if(!FileAccess.FileExists(file))return false;
            profiles=JsonSerializer.Deserialize<ModelProfile[]>(FileAccess.GetFileAsString(file))!.ToDictionary(p=>p.Id);
        }
        if(!profiles.TryGetValue(kind,out profile!))throw new InvalidOperationException("Missing generated creature profile: "+kind);
        if(!generatedScenes.TryGetValue(kind,out var scene))generatedScenes[kind]=scene=GD.Load<PackedScene>("res://assets/creatures/"+kind+".glb")??throw new InvalidOperationException("Creature model failed to import: "+kind);
        modelScale=profile.SourceHeight>0?profile.Height/profile.SourceHeight:1;
        visual=scene.Instantiate<Node3D>();visual.Scale=Vector3.One*modelScale;AddChild(visual);
        MeshInstance3D? sourceMesh=null;
        void Inspect(Node node){
            if(node is Skeleton3D rig)skeleton=rig;
            if(node is AnimationPlayer animation)animation.Stop();
            if(node is MeshInstance3D mesh){
                sourceMesh=mesh;BoundSize=mesh.GetAabb().Size*modelScale;
                mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.On;
                mesh.LodBias=.8f;mesh.ExtraCullMargin=profile.Height*.2f;
                for(int n=0;n<mesh.Mesh.GetSurfaceCount();n++){
                    if(mesh.GetActiveMaterial(n) is StandardMaterial3D shared){
                        var material=(StandardMaterial3D)shared.Duplicate();
                        material.TextureFilter=profile.Style=="block-v2"?BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps:BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
                        material.Roughness=Mathf.Max(.78f,material.Roughness);
                        material.Metallic=profile.Family=="biped"?Mathf.Min(.35f,material.Metallic):0;
                        if(profile.Family!="biped"){material.MetallicTexture=null;material.RoughnessTexture=null;}
                        mesh.SetSurfaceOverrideMaterial(n,material);colors.Add((material,material.AlbedoColor));
                    }
                }
            }
            foreach(var child in node.GetChildren())Inspect(child);
        }
        Inspect(visual);
        if(skeleton==null)throw new InvalidOperationException("Creature has no bound skeleton: "+kind);
        skeleton.ResetBonePoses();
        for(int n=0;n<skeleton.GetBoneCount();n++){boneIndices[skeleton.GetBoneName(n)]=n;restRotations[n]=skeleton.GetBoneGlobalRest(n).Basis.GetRotationQuaternion();localRestRotations[n]=skeleton.GetBoneRest(n).Basis.GetRotationQuaternion();}
        if(profile.Family is "biped" or "quadruped" or "bird")hipHeight=boneIndices.Where(p=>p.Key.StartsWith("Leg",StringComparison.Ordinal)).Max(p=>skeleton.GetBoneGlobalRest(p.Value).Origin.Y)*modelScale;
        if(profile.Style=="block-v2"&&profile.Family is "biped" or "quadruped" or "bird" or "arthropod")CollectFeet(sourceMesh!);
        BodyHeight=profile.BodyHeight;LabelHeight=profile.LabelHeight;Generated=true;
        return true;
    }
    private void CollectFeet(MeshInstance3D mesh)
    {
        var skin=mesh.Skin!;var unique=new HashSet<(int,Vector3)>();
        for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
            var arrays=mesh.Mesh.SurfaceGetArrays(surface);var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var joints=arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();var weights=arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            int influences=joints.Length/vertices.Length;
            for(int n=0;n<vertices.Length;n++){
                if(vertices[n].Y>profile.SourceHeight*.025f)continue;
                for(int k=0;k<influences;k++){
                    int offset=n*influences+k;if(weights[offset]<.99f)continue;int bind=joints[offset];
                    int bone=skin.GetBindBone(bind);if(bone<0)bone=skeleton!.FindBone(skin.GetBindName(bind).ToString());
                    if(bone<0)continue;string name=skeleton!.GetBoneName(bone);
                    if(!name.StartsWith("Knee",StringComparison.Ordinal)&&!name.StartsWith("Leg",StringComparison.Ordinal)&&!name.StartsWith("Tip",StringComparison.Ordinal))continue;
                    var point=(bone,skin.GetBindPose(bind)*vertices[n]);if(unique.Add(point))footSamples.Add(point);
                }
            }
        }
        if(footSamples.Count==0)throw new InvalidOperationException("No bound sole vertices: "+profile.Id);
    }
    private float MinimumFootY()
    {
        float lowest=float.PositiveInfinity;
        foreach(var point in footSamples)lowest=Mathf.Min(lowest,(skeleton!.GetBoneGlobalPose(point.Bone)*point.Local).Y);
        return lowest;
    }
    private static float Ease(float x){x=Mathf.Clamp(x,0,1);return x*x*(3-2*x);}
    private void Pose(string name,Vector3 angles,float blend)
    {
        if(skeleton==null||!boneIndices.TryGetValue(name,out int index))return;
        // Convert a gameplay-space rotation to the bone's own bind axes.
        var rest=restRotations[index];var target=(localRestRotations[index]*(rest.Inverse()*Quaternion.FromEuler(angles)*rest)).Normalized();
        var current=skeleton.GetBonePoseRotation(index).Normalized();
        skeleton.SetBonePoseRotation(index,current.Slerp(target,blend).Normalized());
    }
    private void AnimateGenerated(double delta,Vector3 velocity,bool active,float windup,string style,float strike,float recovery,bool charging,bool sitting,bool airborne)
    {
        if(!active)return;
        float dt=(float)Math.Min(delta,.1),blend=1-MathF.Exp(-14*dt);
        motionTime+=dt;
        float speed=new Vector2(velocity.X,velocity.Z).Length();smoothSpeed=Mathf.Lerp(smoothSpeed,speed,1-MathF.Exp(-9*dt));
        float moving=Mathf.Clamp(smoothSpeed/.65f,0,1),running=Ease((smoothSpeed-2.6f)/2.3f);
        float stride=PixelModel&&hipHeight>0?Mathf.Max(.20f,hipHeight*1.48f*Scale.X):profile.Stride;
        gaitPhase+=dt*Mathf.Min(20,smoothSpeed*Mathf.Tau/(stride*(1+running*.65f)));
        float amplitude=moving*Mathf.Lerp(.38f,.66f,running),idle=MathF.Sin(motionTime*1.8f);
        float wound=Ease(windup),swing=strike>=0?Ease(strike):-1,settle=recovery>=0?1-Ease(recovery):0;
        float yaw=GetParent<Node3D>().Rotation.Y;
        float turn=dt>.0001f?Mathf.Wrap(yaw-previousYaw,-Mathf.Pi,Mathf.Pi)/dt:0;previousYaw=yaw;
        turnLean=Mathf.Lerp(turnLean,Mathf.Clamp(turn,-2,2)*moving,1-MathF.Exp(-6*dt));
        MotionPhase=charging?"charge":strike>=0?"strike":windup>0?"windup":recovery>=0?"recovery":sitting?"sit":moving>.12f?profile.Family is "fish" or "squid"?"swim":running>.4f?"run":"walk":"idle";
        var desired=new Dictionary<string,Vector3>();
        void Set(string name,float x=0,float y=0,float z=0)=>desired[name]=new(x,y,z);
        float bodyPitch=charging?-.20f:wound*(style is "bite" or "charge"?-.18f:.08f);
        if(swing>=0)bodyPitch=Mathf.Lerp(.08f,style=="slam"?-.32f:-.14f,swing);
        if(recovery>=0)bodyPitch=(style=="slam"?-.32f:-.14f)*settle;
        Set("Body",bodyPitch+idle*.002f*(1-moving),0,-turnLean*.045f+MathF.Sin(gaitPhase)*moving*.015f);
        float graze=profile.Id is "deer" or "cow" or "sheep" or "pig"?Mathf.Max(0,MathF.Sin(motionTime*.55f)-.45f)*.30f*(1-moving):0;
        Set("Head",idle*.012f-graze-(style=="bite"?(swing>=0?.32f*swing:wound*.10f):0),MathF.Sin(motionTime*.65f)*.065f*(1-moving)-turnLean*.07f);
        Set("Tail",0,MathF.Sin(motionTime*2.8f+gaitPhase*.3f)*.14f+turnLean*.10f);
        Set("TailTip",0,MathF.Sin(motionTime*2.8f+gaitPhase*.3f-.4f)*.07f+turnLean*.06f);
        Set("Jaw",style=="bite"?-.38f*(swing>=0?1-swing:wound):0);
        if(profile.Id=="creeper"&&windup>0)Set("Body",bodyPitch,MathF.Sin(motionTime*17)*wound*.035f,MathF.Sin(motionTime*20)*wound*.04f);
        if(profile.Family=="biped"){
            float arms=wound*(style=="slam"?1.15f:style=="bolt"?1.05f:.85f);
            if(swing>=0)arms=style=="bolt"?1.05f+MathF.Sin(swing*Mathf.Pi)*.06f:Mathf.Lerp(style=="slam"?1.15f:1.05f,style=="slam"?-.25f:.35f,swing);
            if(recovery>=0)arms=(style=="bolt"?1.05f:style=="slam"?-.25f:.35f)*settle;
            foreach(var side in new[]{"L","R"}){
                float phase=gaitPhase+(side=="R"?Mathf.Pi:0),wave=MathF.Sin(phase);
                Set("Leg"+side,wave*amplitude*(airborne?.35f:1));Set("Knee"+side,-Mathf.Max(0,-wave)*amplitude*.7f);
                Set("Arm"+side,-wave*amplitude*.6f+arms,side=="L"?-.05f:.05f,side=="L"?-.03f:.03f);
                Set("Hand"+side,arms*.16f);
            }
        }else if(profile.Family is "quadruped" or "bird"){
            foreach(var pair in new[]{("FrontL",0f),("FrontR",Mathf.Pi),("RearL",Mathf.Pi),("RearR",0f)}){
                float phase=gaitPhase+pair.Item2;
                float wave=MathF.Sin(phase),fold=sitting?-.6f:airborne?-.22f:0;
                if(profile.Id=="turtle")Set("Leg"+pair.Item1,0,wave*amplitude*.60f);
                else Set("Leg"+pair.Item1,wave*amplitude+fold);
                Set("Knee"+pair.Item1,-Mathf.Max(0,-wave)*amplitude*.65f-fold*.7f);
            }
            Set("WingL",0,0,-(.06f+moving*.12f+MathF.Sin(motionTime*7)*moving*.09f));Set("WingR",0,0,.06f+moving*.12f+MathF.Sin(motionTime*7)*moving*.09f);
        }else if(profile.Family=="arthropod"){
            for(int n=0;n<4;n++)foreach(var side in new[]{"L","R"}){
                float phase=gaitPhase+n*Mathf.Pi+(side=="R"?Mathf.Pi:0),wave=MathF.Sin(phase),sign=side=="L"?-1:1;
                Set("Leg"+n+side,Mathf.Max(0,wave)*moving*.15f,MathF.Cos(phase)*moving*.20f*sign);
                Set("Tip"+n+side,0,0,Mathf.Max(0,wave)*moving*.12f*sign);
            }
        }else if(profile.Family=="fish"){
            Set("Body",0,MathF.Sin(motionTime*4)*.06f,turnLean*.025f);Set("Tail",0,MathF.Sin(motionTime*6)*(.10f+moving*.25f));
            Set("FinL",0,0,MathF.Sin(motionTime*5)*.20f);Set("FinR",0,0,-MathF.Sin(motionTime*5)*.20f);
        }else{
            for(int n=0;n<8;n++){float a=n*Mathf.Tau/8,pulse=MathF.Sin(motionTime*3+n*.55f)*(.09f+moving*.09f);Set("Tentacle"+n,MathF.Sin(a)*pulse,0,MathF.Cos(a)*pulse);Set("TipTentacle"+n,MathF.Sin(a)*pulse*.60f,0,MathF.Cos(a)*pulse*.60f);}
            Set("Body",MathF.Sin(motionTime*2)*.035f,MathF.Sin(motionTime*.7f)*.04f);
        }
        foreach(var name in boneIndices.Keys)Pose(name,desired.GetValueOrDefault(name,Vector3.Zero),blend);
        float bob=profile.Family is "fish" or "squid"?MathF.Sin(motionTime*2)*profile.Height*.022f:MathF.Sin(gaitPhase*2)*moving*profile.Height*.008f;
        if(!airborne&&!sitting&&hipHeight>0)bob-=hipHeight*(1-MathF.Cos(MathF.Sin(gaitPhase)*amplitude))*moving;
        // Breathing is subtle and never stretches the entire skinned mesh.
        bob+=idle*profile.Height*.002f;
        if(footSamples.Count>0&&!airborne&&!sitting){
            // Bone poses already blend; a second offset delay would bury the soles.
            bob=-MinimumFootY()*modelScale;visual.Position=new(0,bob,0);
        }else visual.Position=visual.Position.Lerp(new(0,bob,0),blend);
    }
}
