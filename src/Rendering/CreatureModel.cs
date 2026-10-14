using System;
using System.Collections.Generic;
using Godot;
namespace EmberLighthouse.Rendering;

public partial class CreatureModel : PresentationModel
{
    private readonly List<(Node3D Joint,float Phase,float RestAngle)> limbs=[];
    private readonly List<(StandardMaterial3D Material,Color Color)> colors=[];
    private int tintState=-1;
    private float stride;
    public float BodyHeight {get;private set;}
    public float LabelHeight {get;private set;}
    private void Part(Node3D parent,Vector3 size,Vector3 position,string color)
    {
        Box(parent,size,position,new(color));
        if(color is "ffb347" or "ffc277" or "9be7ec" or "b2e967"){
            var mesh=parent.GetChild<MeshInstance3D>(parent.GetChildCount()-1);var material=(StandardMaterial3D)mesh.MaterialOverride;material.EmissionEnabled=true;material.Emission=new(color);material.EmissionEnergyMultiplier=.65f;
        }
    }
    private void Fur(Vector3 size,Vector3 position,string color)
    {
        TexturedBox(this,size,position,face=>{
            var image=Image.CreateEmpty(16,16,false,Image.Format.Rgba8);var tint=new Color(color);
            for(int y=0;y<16;y++)for(int x=0;x<16;x++){float shade=((x*7+y*13+face*5)%11)*.012f;image.SetPixel(x,y,tint.Darkened(shade));}return image;
        });
    }
    private Node3D Limb(Vector3 origin,float phase,float rest=0)
    {
        var joint=new Node3D{Position=origin,Rotation=new(0,0,rest)};AddChild(joint);limbs.Add((joint,phase,rest));return joint;
    }
    public static CreatureModel Create(string kind)
    {
        var m=new CreatureModel();
        if(m.LoadGenerated(kind))return m;
        switch(kind){
            case "deer":
                m.BodyHeight=1.55f;m.LabelHeight=2.03f;
                m.Fur(new(.48f,.49f,.90f),new(0,.91f,0),"b88a54");m.Part(m,new(.38f,.19f,.71f),new(0,.71f,0),"e6d4b1");
                m.Fur(new(.25f,.50f,.29f),new(0,1.16f,-.42f),"b88a54");m.Fur(new(.31f,.33f,.42f),new(0,1.43f,-.54f),"cba370");m.Part(m,new(.25f,.16f,.25f),new(0,1.33f,-.79f),"ead9b7");m.Part(m,new(.16f,.09f,.06f),new(0,1.36f,-.94f),"352d27");
                foreach(float s in new[]{-1f,1f}){
                    m.Part(m,new(.14f,.10f,.27f),new(s*.23f,1.55f,-.51f),"b88a54");m.Part(m,new(.08f,.035f,.18f),new(s*.23f,1.61f,-.51f),"e6d4b1");m.Part(m,new(.026f,.05f,.065f),new(s*.164f,1.46f,-.66f),"201f21");
                    m.Part(m,new(.07f,.29f,.07f),new(s*.13f,1.74f,-.48f),"69503b");m.Part(m,new(.26f,.06f,.07f),new(s*.23f,1.81f,-.48f),"69503b");m.Part(m,new(.06f,.20f,.06f),new(s*.34f,1.87f,-.48f),"69503b");m.Part(m,new(.06f,.13f,.06f),new(s*.22f,1.85f,-.48f),"69503b");
                    foreach(float z in new[]{-.30f,.30f}){var leg=m.Limb(new(s*.18f,.73f,z),s*z<0?Mathf.Pi:0);m.Part(leg,new(.105f,.57f,.105f),new(0,-.285f,0),"ad8151");m.Part(leg,new(.12f,.13f,.15f),new(0,-.67f,-.015f),"3b3029");}
                }
                m.Part(m,new(.12f,.13f,.23f),new(0,1.12f,.50f),"e6d4b1");break;
            case "boar":
                m.BodyHeight=1.03f;m.LabelHeight=1.32f;
                m.Fur(new(.77f,.59f,1.02f),new(0,.64f,.06f),"55443a");m.Fur(new(.56f,.46f,.44f),new(0,.58f,-.61f),"695043");m.Part(m,new(.43f,.25f,.22f),new(0,.49f,-.91f),"9d7569");
                foreach(float s in new[]{-1f,1f}){
                    m.Part(m,new(.08f,.08f,.026f),new(s*.11f,.50f,-1.035f),"352b2c");m.Part(m,new(.08f,.24f,.08f),new(s*.255f,.53f,-.88f),"e4dabd");m.Part(m,new(.14f,.08f,.08f),new(s*.22f,.68f,-.88f),"e4dabd");m.Part(m,new(.026f,.06f,.08f),new(s*.286f,.68f,-.71f),"211f21");m.Part(m,new(.15f,.17f,.16f),new(s*.22f,.87f,-.57f),"40362f");
                    foreach(float z in new[]{-.27f,.37f}){var leg=m.Limb(new(s*.28f,.43f,z),s*z<0?Mathf.Pi:0);m.Part(leg,new(.18f,.29f,.20f),new(0,-.145f,0),"5b493c");m.Part(leg,new(.20f,.12f,.23f),new(0,-.37f,-.015f),"27262a");}
                }
                for(int i=0;i<6;i++)m.Part(m,new(.18f,.12f+(i%2)*.045f,.10f),new(0,.98f,-.32f+i*.14f),"302c2b");m.Part(m,new(.10f,.10f,.22f),new(0,.65f,.65f),"40362f");break;
            case "skitter":
                m.BodyHeight=.65f;m.LabelHeight=.94f;
                m.Fur(new(.63f,.37f,.75f),new(0,.45f,.23f),"384839");m.Fur(new(.49f,.29f,.41f),new(0,.37f,-.36f),"607349");
                for(int i=0;i<3;i++)m.Part(m,new(.46f,.06f,.09f),new(0,.65f,.01f+i*.24f),"718346");
                foreach(float s in new[]{-1f,1f}){
                    for(int i=0;i<3;i++)m.Part(m,new(.07f,.065f,.025f),new(s*(.09f+i*.055f),.40f,-.58f+i*.02f),"b2e967");
                    m.Part(m,new(.065f,.15f,.09f),new(s*.12f,.20f,-.57f),"293730");
                    for(int i=0;i<4;i++){var leg=m.Limb(new(s*.25f,.38f,-.39f+i*.22f),i%2*Mathf.Pi+(s<0?Mathf.Pi:0),s*-.35f);m.Part(leg,new(.39f,.09f,.10f),new(s*.19f,.035f,0),"536940");var lower=new Node3D{Position=new(s*.38f,.08f,0),Rotation=new(0,0,s*.58f)};leg.AddChild(lower);m.Part(lower,new(.10f,.32f,.11f),new(0,-.16f,0),"2d3a31");}
                }break;
            case "husk":
                m.BodyHeight=1.83f;m.LabelHeight=2.07f;
                m.Fur(new(.38f,.56f,.26f),new(0,1.19f,.055f),"495049");m.Fur(new(.36f,.37f,.34f),new(0,1.64f,-.075f),"7f9d9f");m.Part(m,new(.24f,.12f,.09f),new(0,1.52f,-.27f),"566d70");
                foreach(float s in new[]{-1f,1f}){
                    m.Part(m,new(.095f,.045f,.024f),new(s*.10f,1.68f,-.255f),"9be7ec");var arm=m.Limb(new(s*.265f,1.40f,.035f),s<0?0:Mathf.Pi,s*-.10f);m.Part(arm,new(.14f,.38f,.17f),new(0,-.16f,0),"495049");m.Part(arm,new(.11f,.44f,.13f),new(0,-.54f,-.04f),"76918e");m.Part(arm,new(.13f,.18f,.13f),new(0,-.83f,-.04f),"657f7c");
                    var leg=m.Limb(new(s*.11f,.93f,.07f),s<0?Mathf.Pi:0);m.Part(leg,new(.13f,.68f,.15f),new(0,-.35f,0),"627d78");m.Part(leg,new(.17f,.18f,.27f),new(0,-.84f,-.055f),"344440");m.Part(m,new(.12f,.25f,.04f),new(s*.15f,.87f,-.10f),"383b35");
                }
                m.Part(m,new(.07f,.37f,.028f),new(0,1.20f,-.094f),"6a5b43");break;
            case "sentinel":
            case "guardian":
                bool boss=kind=="guardian";float k=boss?1.5f:1;
                string stone=boss?"32343e":"707b73",metal=boss?"474752":"a06c49",glow=boss?"ffb347":"ffc277";
                m.BodyHeight=1.8f*k;m.LabelHeight=2.12f*k;
                void Block(Node3D p,Vector3 size,Vector3 at,string color)=>m.Part(p,size*k,at*k,color);
                m.Fur(new Vector3(.63f,.52f,.39f)*k,new Vector3(0,1.18f,0)*k,stone);Block(m,new(.29f,.27f,.042f),new(0,1.18f,-.22f),metal);Block(m,new(.14f,.17f,.023f),new(0,1.19f,-.251f),glow);
                Block(m,new(.34f,.33f,.32f),new(0,1.63f,0),stone);Block(m,new(.12f,.22f,.028f),new(0,1.65f,-.18f),glow);Block(m,new(.39f,.08f,.36f),new(0,1.83f,0),metal);
                foreach(float s in new[]{-1f,1f}){
                    Block(m,new(.35f,.27f,.42f),new(s*.47f,1.43f,0),metal);Block(m,new(.25f,.06f,.43f),new(s*.47f,1.58f,0),stone);
                    var arm=m.Limb(new Vector3(s*.47f,1.33f,0)*k,s<0?0:Mathf.Pi);Block(arm,new(.23f,.39f,.26f),new(0,-.19f,0),stone);Block(arm,new(boss?.35f:.27f,.35f,.32f),new(0,-.54f,-.02f),metal);Block(arm,new(.045f,.22f,.025f),new(s*.06f,-.52f,-.192f),glow);Block(arm,new(.28f,.20f,.31f),new(0,-.77f,-.04f),stone);
                    if(!boss&&s<0){Block(arm,new(.13f,.13f,.18f),new(0,-.57f,-.26f),stone);Block(arm,new(.07f,.07f,.02f),new(0,-.57f,-.36f),glow);}
                    var leg=m.Limb(new Vector3(s*.20f,.89f,0)*k,s<0?Mathf.Pi:0);Block(leg,new(.23f,.37f,.28f),new(0,-.17f,0),metal);Block(leg,new(.25f,.28f,.30f),new(0,-.52f,0),stone);Block(leg,new(.29f,.17f,.42f),new(0,-.80f,-.055f),stone);
                    Block(leg,new(.16f,.15f,.18f),new(0,-.68f,0),"343a3b");Block(arm,new(.16f,.12f,.17f),new(0,-.36f,0),"343a3b");
                    if(boss){
                        Block(m,new(.08f,.26f,.10f),new(s*.16f,1.97f,0),stone);Block(m,new(.09f,.05f,.028f),new(s*.17f,1.65f,-.18f),glow);Block(m,new(.04f,.16f,.026f),new(s*.24f,1.22f,-.21f),glow);Block(m,new(.08f,.03f,.026f),new(s*.22f,1.14f,-.211f),glow);Block(m,new(.03f,.15f,.026f),new(s*.19f,1.07f,-.211f),glow);
                        Block(m,new(.09f,.19f,.11f),new(s*.54f,1.65f,.03f),stone);Block(m,new(.22f,.11f,.035f),new(s*.18f,1.39f,-.223f),metal);Block(arm,new(.28f,.10f,.36f),new(0,-.50f,-.02f),stone);Block(arm,new(.29f,.09f,.37f),new(0,-.67f,-.035f),stone);
                    }else{Block(m,new(.54f,.055f,.045f),new(0,1.07f,-.225f),metal);Block(m,new(.18f,.07f,.055f),new(s*.20f,1.37f,-.224f),metal);}
                }break;
            case "mine_warden":case "furnace_warden":case "observatory_warden":case "colossus":m.BuildMainline(kind);break;
            default:m.BuildCommon(kind);break;
        }
        void Cache(Node n){foreach(var child in n.GetChildren()){if(child is MeshInstance3D mesh&&mesh.MaterialOverride is StandardMaterial3D material){mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.On;m.colors.Add((material,material.AlbedoColor));}Cache(child);}}Cache(m);return m;
    }
    private void BuildMainline(string kind)
    {
        bool giant=kind=="colossus",furnace=kind=="furnace_warden",snow=kind=="observatory_warden";float scale=giant?4.5f:furnace?1.25f:snow?1.08f:1;
        string stone=giant?"353642":furnace?"49453e":snow?"708c9f":"657064",metal=giant?"a88346":furnace?"a16a42":snow?"a3bfcb":"b48b59",glow=snow?"9be7ec":"ffb347";
        BodyHeight=giant?10.9f:furnace?2.9f:snow?2.35f:2.2f;LabelHeight=BodyHeight+.3f;
        void B(Node3D parent,Vector3 size,Vector3 at,string tint)=>Part(parent,size*scale,at*scale,tint);
        void Body(Vector3 size,Vector3 at,string tint)=>Fur(size*scale,at*scale,tint);
        float wide=giant?1.1f:furnace?.95f:snow?.48f:.65f;
        Body(new(wide,.62f,.43f),new(0,1.25f,0),stone);B(this,new(wide+.04f,.12f,.48f),new(0,.93f,0),metal);
        Body(new(.36f,.34f,.33f),new(0,1.82f,-.02f),stone);
        B(this,new(.30f,.075f,.045f),new(0,1.84f,-.205f),glow);B(this,new(.46f,.09f,.40f),new(0,2.02f,-.01f),metal);
        if(!giant&&!snow){B(this,new(.36f,.12f,.31f),new(0,2.12f,.01f),stone);B(this,new(.12f,.12f,.065f),new(0,2.11f,-.185f),glow);}
        foreach(float side in new[]{-1f,1f}){
            var leg=Limb(new Vector3(side*(giant?.28f:snow?.13f:.22f),.88f,0)*scale,side<0?Mathf.Pi:0);B(leg,new(snow?.13f:giant?.30f:.23f,.60f,.27f),new(0,-.30f,0),stone);B(leg,new(snow?.20f:giant?.42f:.32f,.18f,.43f),new(0,-.76f,-.055f),metal);
            B(leg,new(.11f,.18f,.028f),new(0,-.44f,-.15f),metal);
            float shoulder=wide*.5f+.17f;B(this,new(giant?.40f:furnace?.37f:.28f,.27f,.48f),new(side*shoulder,1.56f,0),metal);
            var arm=Limb(new Vector3(side*shoulder,1.47f,0)*scale,side<0?0:Mathf.Pi);B(arm,new(snow?.12f:.24f,.44f,.26f),new(0,-.23f,0),stone);
            bool gun=!giant&&!furnace&&side<0;
            B(arm,new(gun?.25f:giant?.40f:furnace?.37f:.22f,.42f,gun?.64f:.32f),new(0,-.64f,gun?-.21f:0),metal);B(arm,new(gun?.16f:.25f,.22f,.30f),new(0,-.89f,gun?-.40f:-.045f),stone);
            if(gun){B(arm,new(.09f,.09f,.04f),new(0,-.71f,-.555f),glow);B(arm,new(.29f,.065f,.70f),new(0,-.45f,-.24f),stone);}
            if(giant){B(this,new(.28f,.70f,.34f),new(side*.60f,1.96f,.07f),stone);B(this,new(.11f,.47f,.045f),new(side*.60f,1.95f,-.125f),glow);B(this,new(.035f,.15f,.08f),new(side*.60f,2.35f,.07f),glow);B(arm,new(.07f,.25f,.03f),new(0,-.67f,-.185f),glow);B(leg,new(.035f,.31f,.035f),new(0,-.42f,-.16f),glow);}
            if(furnace){B(this,new(.20f,.32f,.22f),new(side*.54f,1.92f,.03f),stone);B(this,new(.12f,.21f,.12f),new(side*.54f,2.14f,.03f),glow);B(arm,new(.38f,.10f,.37f),new(0,-.79f,0),stone);}
        }
        if(snow){B(this,new(.06f,.50f,.06f),new(0,2.14f,.20f),metal);B(this,new(.77f,.07f,.07f),new(0,2.37f,.20f),metal);foreach(float s in new[]{-1f,1f}){B(this,new(.07f,.33f,.07f),new(s*.35f,2.52f,.20f),metal);B(this,new(.027f,.22f,.03f),new(s*.35f,2.53f,.145f),glow);}BodyHeight=2.78f;LabelHeight=3.08f;}
        if(furnace){B(this,new(.51f,.49f,.05f),new(0,1.25f,-.25f),"242832");B(this,new(.41f,.37f,.03f),new(0,1.25f,-.286f),glow);for(int i=-1;i<=1;i++)B(this,new(.05f,.43f,.035f),new(i*.13f,1.25f,-.31f),stone);}
        else if(giant){B(this,new(.60f,.57f,.065f),new(0,1.28f,-.255f),metal);B(this,new(.44f,.41f,.04f),new(0,1.28f,-.31f),"292e35");B(this,new(.27f,.31f,.04f),new(0,1.28f,-.352f),glow);B(this,new(.38f,.09f,.045f),new(0,1.28f,-.365f),glow);foreach(float s in new[]{-1f,1f})B(this,new(.06f,.36f,.04f),new(s*.42f,1.26f,-.25f),glow);B(this,new(.09f,.28f,.10f),new(0,2.17f,0),metal);}
        else{B(this,new(.22f,.22f,.045f),new(0,1.26f,-.25f),metal);B(this,new(.08f,.13f,.025f),new(0,1.26f,-.285f),glow);}
    }
    public void Animate(double delta,Vector3 velocity,bool active,float attack=0,string style="",float strikeProgress=-1,float recoveryProgress=-1,bool charging=false,bool sitting=false,bool airborne=false)
    {
        if(Generated){AnimateGenerated(delta,velocity,active,attack,style,strikeProgress,recoveryProgress,charging,sitting,airborne);return;}
        float speed=new Vector2(velocity.X,velocity.Z).Length();if(active)stride+=(float)delta*Mathf.Min(speed*3,16);
        float amount=active?Mathf.Min(speed/3,1)*.32f:0;
        attack=active?Mathf.Clamp(attack,0,1):0;Rotation=new(-attack*.14f,0,0);
        foreach(var limb in limbs)limb.Joint.Rotation=new(Mathf.Sin(stride+limb.Phase)*amount+(limb.Joint.Position.Y>BodyHeight*.65f?-attack*.9f:0),0,limb.RestAngle);
    }
    public void Tint(bool hit,bool warning)
    {
        int state=hit?2:warning?1:0;if(state==tintState)return;tintState=state;
        foreach(var pair in colors)pair.Material.AlbedoColor=hit?pair.Color.Lerp(Colors.White,.75f):warning?pair.Color.Lerp(Colors.OrangeRed,.55f):pair.Color;
    }
}
