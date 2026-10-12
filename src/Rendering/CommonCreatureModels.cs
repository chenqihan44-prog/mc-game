using System;
using Godot;
namespace EmberLighthouse.Rendering;

public partial class CreatureModel
{
    private void BuildCommon(string kind)
    {
        void B(Vector3 size,Vector3 at,string color)=>Part(this,size,at,color);
        void Eyes(float x,float y,float z,string color="202026"){foreach(float s in new[]{-1f,1f})B(new(.06f,.07f,.03f),new(s*x,y,z),color);}
        if(kind is "zombie" or "skeleton"){
            bool bones=kind=="skeleton";string skin=bones?"d2ccba":"719855",shirt=bones?skin:"447e87",pants=bones?skin:"6c6885";BodyHeight=1.8f;LabelHeight=2.1f;
            B(new(.44f,.47f,.42f),new(0,1.55f,-.02f),skin);Eyes(.11f,1.59f,-.242f);B(new(.16f,.04f,.028f),new(0,1.44f,-.245f),"343238");
            B(new(bones?.32f:.5f,.60f,.25f),new(0,1.02f,0),shirt);if(bones)for(int y=0;y<4;y++)B(new(.4f,.05f,.10f),new(0,.85f+y*.13f,-.13f),"e4ddcf");
            foreach(float s in new[]{-1f,1f}){var leg=Limb(new(s*.14f,.72f,0),s<0?Mathf.Pi:0);Part(leg,new(bones?.10f:.20f,.65f,.20f),new(0,-.33f,0),pants);var arm=Limb(new(s*.32f,1.3f,0),s<0?0:Mathf.Pi);Part(arm,new(.13f,.60f,.16f),new(0,-.23f,-.10f),skin);}
            if(bones){for(int i=0;i<7;i++)B(new(.04f,.07f,.06f),new(.45f+MathF.Sin(i*.5f)*.18f,.6f+i*.13f,-.23f),"9b7441");B(new(.025f,.82f,.02f),new(.45f,.98f,-.23f),"c7c2a3");}return;
        }
        if(kind=="creeper"){
            BodyHeight=1.7f;LabelHeight=2;Fur(new(.49f,.49f,.48f),new(0,1.43f,0),"63984e");Fur(new(.4f,.76f,.36f),new(0,.82f,0),"5a8f49");Eyes(.13f,1.48f,-.257f);B(new(.12f,.22f,.028f),new(0,1.31f,-.257f),"26352a");foreach(float s in new[]{-1f,1f}){B(new(.07f,.13f,.03f),new(s*.09f,1.27f,-.259f),"26352a");foreach(float z in new[]{-.18f,.18f}){var leg=Limb(new(s*.18f,.42f,z),s*z<0?Mathf.Pi:0);Part(leg,new(.22f,.35f,.24f),new(0,-.18f,0),"537d41");}}return;
        }
        if(kind=="spider"){
            BodyHeight=.65f;LabelHeight=.95f;Fur(new(.72f,.38f,.86f),new(0,.45f,.20f),"39313a");Fur(new(.57f,.29f,.40f),new(0,.38f,-.42f),"44333b");Eyes(.15f,.43f,-.635f,"d54540");foreach(float s in new[]{-1f,1f})for(int i=0;i<4;i++){var leg=Limb(new(s*.29f,.38f,-.36f+i*.22f),i%2*Mathf.Pi,s*-.15f);Part(leg,new(.75f,.10f,.11f),new(s*.35f,-.08f,(i-1.5f)*.09f),"38303a");}return;
        }
        if(kind=="cod"){
            BodyHeight=.45f;LabelHeight=.8f;Fur(new(.28f,.32f,.74f),new(0,.23f,0),"b3a67d");B(new(.26f,.25f,.26f),new(0,.23f,-.42f),"d0c9a2");B(new(.07f,.29f,.23f),new(0,.24f,.46f),"ac955c");B(new(.04f,.14f,.25f),new(0,.43f,0),"d0b46a");foreach(float s in new[]{-1f,1f})B(new(.20f,.03f,.15f),new(s*.18f,.16f,0),"b8a16a");Eyes(.09f,.29f,-.56f);return;
        }
        if(kind=="squid"){
            BodyHeight=.95f;LabelHeight=1.3f;Fur(new(.57f,.55f,.57f),new(0,.70f,0),"435470");Eyes(.15f,.7f,-.301f);for(int i=0;i<8;i++){float a=i*Mathf.Tau/8;var leg=Limb(new(Mathf.Cos(a)*.23f,.47f,Mathf.Sin(a)*.23f),a);Part(leg,new(.11f,.55f,.11f),new(Mathf.Cos(a)*.1f,-.25f,Mathf.Sin(a)*.1f),"516582");}return;
        }
        if(kind=="turtle"){
            BodyHeight=.55f;LabelHeight=.9f;Fur(new(.86f,.38f,1.02f),new(0,.33f,0),"4d8050");B(new(.75f,.10f,.89f),new(0,.14f,0),"c5ca84");for(int i=-1;i<=1;i++)B(new(.04f,.04f,.97f),new(i*.22f,.54f,0),"99b85f");B(new(.3f,.22f,.3f),new(0,.28f,-.66f),"83ac60");Eyes(.085f,.31f,-.82f);foreach(float s in new[]{-1f,1f})foreach(float z in new[]{-.32f,.32f}){var leg=Limb(new(s*.44f,.13f,z),s*z<0?Mathf.Pi:0);Part(leg,new(.35f,.10f,.22f),new(s*.12f,-.03f,0),"83ac60");}return;
        }
        if(kind=="chicken"){
            BodyHeight=.92f;LabelHeight=1.2f;Fur(new(.46f,.42f,.55f),new(0,.47f,0),"eee8d6");B(new(.31f,.32f,.29f),new(0,.75f,-.30f),"f5eedc");B(new(.22f,.11f,.17f),new(0,.72f,-.51f),"d7a443");B(new(.12f,.14f,.08f),new(0,.56f,-.43f),"bc5141");Eyes(.09f,.8f,-.451f);foreach(float s in new[]{-1f,1f}){B(new(.12f,.29f,.44f),new(s*.25f,.48f,0),"ddd9ca");var leg=Limb(new(s*.12f,.25f,.05f),s<0?Mathf.Pi:0);Part(leg,new(.06f,.25f,.06f),new(0,-.12f,0),"d0a64a");Part(leg,new(.13f,.045f,.21f),new(0,-.24f,-.055f),"d0a64a");}return;
        }
        if(kind is not("cow" or "pig" or "sheep" or "wolf" or "cat" or "horse" or "fox" or "armadillo"))throw new ArgumentOutOfRangeException(nameof(kind));
        bool large=kind is "cow" or "horse",small=kind is "cat" or "fox" or "armadillo";
        string color=kind switch{"cow"=>"835f45","pig"=>"dba29a","sheep"=>"e6e0d3","wolf"=>"a4adb5","cat"=>"d1a35c","horse"=>"95653f","fox"=>"c67c35",_=>"a08674"};float width=large?.7f:small?.4f:.58f,len=large?1.3f:small?.8f:1,legH=kind=="horse"?1:large?.65f:small?.28f:.4f;
        BodyHeight=kind=="horse"?1.9f:legH+.8f;LabelHeight=BodyHeight+.3f;Fur(new(width,.48f,len),new(0,legH+.24f,0),color);float headY=legH+.38f,headZ=-len*.5f-.19f;
        if(kind=="horse"){Fur(new(.34f,.64f,.39f),new(0,legH+.43f,-.63f),color);headY=1.68f;headZ=-.79f;B(new(.13f,.65f,.12f),new(0,1.35f,-.43f),"40332d");}
        Fur(new(small?.30f:.43f,.36f,.41f),new(0,headY,headZ),color);Eyes(small?.09f:.13f,headY+.05f,headZ-.216f);
        if(kind is "pig" or "cow" or "sheep")B(new(.28f,.17f,.14f),new(0,headY-.1f,headZ-.25f),kind=="pig"?"bd8784":"cfbd9a");
        if(kind is "wolf" or "fox" or "cat")B(new(.21f,.17f,.23f),new(0,headY-.07f,headZ-.24f),kind=="wolf"?"e3ded2":"efe0c2");
        foreach(float s in new[]{-1f,1f}){B(new(.10f,kind=="horse"?.20f:.13f,.13f),new(s*.16f,headY+.22f,headZ+.04f),color);foreach(float z in new[]{-len*.31f,len*.31f}){var leg=Limb(new(s*width*.35f,legH,z),s*z<0?Mathf.Pi:0);Part(leg,new(.12f,legH,.15f),new(0,-legH*.5f,0),kind=="sheep"?"a89782":color);Part(leg,new(.14f,.075f,.17f),new(0,-legH+.03f,0),large?"373332":color);}}
        if(kind=="cow"){foreach(float s in new[]{-1f,1f}){B(new(.23f,.22f,.024f),new(s*(width/2+.013f),legH+.26f,.25f),"ebe2c9");B(new(.1f,.2f,.1f),new(s*.2f,headY+.29f,headZ),"d5c5a0");}B(new(.32f,.12f,.24f),new(0,legH-.04f,.32f),"cda39b");}
        if(kind=="sheep")for(int i=0;i<3;i++)B(new(width+.04f,.09f,.08f),new(0,legH+.51f,-.3f+i*.3f),"f0eadb");
        if(kind=="cat"){B(new(.18f,.24f,.03f),new(-.14f,headY,headZ-.21f),"534a43");B(new(.41f,.24f,.22f),new(0,legH+.22f,.12f),"504c46");}
        if(kind=="armadillo")for(int i=0;i<5;i++)B(new(width+.06f,.10f,.12f),new(0,legH+.48f,-.34f+i*.16f),i%2==0?"c29b85":"957767");
        if(kind is "wolf" or "fox" or "cat" or "horse")B(new(small?.12f:.14f,kind=="horse"?.55f:.13f,.48f),new(0,legH+.24f,len*.5f+.15f),kind=="fox"?"ece2c9":kind=="horse"?"40332d":color);
    }
}
