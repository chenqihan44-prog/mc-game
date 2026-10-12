using System;
using System.Collections.Generic;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class PresentationModel : Node3D
{
    private readonly List<Resource> owned=[];
    private readonly Dictionary<StandardMaterial3D,Color> originalLightColors=[];
    public void SetWorldLight(Color light)
    {
        foreach(var resource in owned)if(resource is StandardMaterial3D material){if(!originalLightColors.TryGetValue(material,out var color)){color=material.AlbedoColor;originalLightColors[material]=color;}material.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;material.AlbedoColor=new(color.R*light.R,color.G*light.G,color.B*light.B,color.A);}
    }
    public Node3D Head {get;private set;}=null!;
    public Node3D RightArm {get;private set;}=null!;
    public Node3D LeftArm {get;private set;}=null!;
    public void Box(Node3D parent,Vector3 size,Vector3 position,Color color)
    {
        var mesh=new BoxMesh{Size=size};var material=new StandardMaterial3D{AlbedoColor=color,Roughness=1};owned.Add(mesh);owned.Add(material);parent.AddChild(new MeshInstance3D{Mesh=mesh,MaterialOverride=material,Position=position,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
    public void TexturedBox(Node3D parent,Vector3 size,Vector3 position,Func<int,Image> faceImage)
    {
        Box(parent,size,position,Colors.White);
        // Each face has its own pixel texture, so sleeves, palms and hair do not repeat.
        var normals=new[]{Vector3.Back,Vector3.Forward,Vector3.Right,Vector3.Left,Vector3.Up,Vector3.Down};
        for(int face=0;face<6;face++){
            var normal=normals[face];bool horizontal=face>=4;var dimensions=horizontal?new Vector2(size.X,size.Z):face<2?new(size.X,size.Y):new(size.Z,size.Y);
            using var image=faceImage(face);var texture=ImageTexture.CreateFromImage(image);
            var material=new StandardMaterial3D{AlbedoTexture=texture,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,Roughness=1};var mesh=new QuadMesh{Size=dimensions};owned.Add(texture);owned.Add(material);owned.Add(mesh);
            var panel=new MeshInstance3D{Mesh=mesh,MaterialOverride=material,Position=position+normal*(horizontal?size.Y/2+.0005f:face<2?size.Z/2+.0005f:size.X/2+.0005f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            panel.Rotation=face switch{1=>new(0,Mathf.Pi,0),2=>new(0,Mathf.Pi/2,0),3=>new(0,-Mathf.Pi/2,0),4=>new(-Mathf.Pi/2,0,0),5=>new(Mathf.Pi/2,0,0),_=>Vector3.Zero};parent.AddChild(panel);
        }
    }
    public override void _ExitTree(){foreach(var resource in owned)resource.Dispose();owned.Clear();originalLightColors.Clear();}
    public static PresentationModel Item(ItemDef item)
    {
        var model=new PresentationModel();Color color=Color.FromString(item.Color,Colors.White);
        void Block(Vector3 size,Vector3 p)=>model.TexturedBox(model,size,p,face=>{var image=Image.CreateEmpty(16,16,false,Image.Format.Rgba8);for(int y=0;y<16;y++)for(int x=0;x<16;x++)image.SetPixel(x,y,ItemSprites.Tile(item,x,y,face==4?2:face%2));return image;});
        if(item.Block&&item.Shape!="bush"){
            switch(item.Id){
                case "door":Block(new(.5f,.75f,.07f),Vector3.Zero);break;
                case "window":Block(new(.5f,.5f,.07f),Vector3.Zero);break;
                case "fence":Block(new(.12f,.5f,.12f),new(-.15f,0,0));Block(new(.12f,.5f,.12f),new(.15f,0,0));Block(new(.4f,.07f,.07f),new(0,.12f,0));Block(new(.4f,.07f,.07f),new(0,-.12f,0));break;
                case "lantern":model.Box(model,new(.22f,.3f,.22f),Vector3.Zero,new("ffdb83"));model.Box(model,new(.27f,.06f,.27f),new(0,.18f,0),new("4a545d"));model.Box(model,new(.27f,.06f,.27f),new(0,-.18f,0),new("4a545d"));break;
                case "torch":model.Box(model,new(.09f,.42f,.09f),Vector3.Zero,new("926133"));model.Box(model,new(.13f,.17f,.13f),new(0,.23f,0),new("ffcd61"));break;
                default:
                    if(item.Shape=="stairs"){Block(new(.5f,.25f,.5f),new(0,-.125f,0));Block(new(.5f,.25f,.25f),new(0,.125f,-.125f));}
                    else Block(new(.5f,item.Shape is "slab" or "rug"?item.Shape=="rug"?.05f:.25f:.5f,.5f),Vector3.Zero);
                    break;
            }
        }else{
            using var image=ItemSprites.CreateImage(item);var texture=ImageTexture.CreateFromImage(image);
            var material=new StandardMaterial3D{AlbedoTexture=texture,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor,AlphaScissorThreshold=.5f,CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=1};var front=new QuadMesh{Size=new(.55f,.55f)};model.owned.Add(texture);model.owned.Add(material);model.owned.Add(front);model.AddChild(new MeshInstance3D{Mesh=front,MaterialOverride=material,Position=new(0,0,.012f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
            // Exposed pixel edges give tools and food real thickness.
            var sides=new SurfaceTool();sides.Begin(Mesh.PrimitiveType.Triangles);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tint){sides.SetColor(tint);foreach(var vertex in new[]{a,b,c,a,c,d})sides.AddVertex(vertex);}
            const float pixel=.55f/32,depth=.022f;
            bool Visible(int x,int y)=>x>=0&&x<32&&y>=0&&y<32&&image.GetPixel(x,y).A>.5f;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(Visible(x,y)){
                float left=(x-16)*pixel,right=left+pixel,top=(16-y)*pixel,bottom=top-pixel;Color tint=image.GetPixel(x,y).Darkened(.24f);
                if(!Visible(x-1,y))Quad(new(left,bottom,-depth),new(left,top,-depth),new(left,top,depth),new(left,bottom,depth),tint);
                if(!Visible(x+1,y))Quad(new(right,top,-depth),new(right,bottom,-depth),new(right,bottom,depth),new(right,top,depth),tint);
                if(!Visible(x,y-1))Quad(new(left,top,-depth),new(right,top,-depth),new(right,top,depth),new(left,top,depth),tint);
                if(!Visible(x,y+1))Quad(new(right,bottom,-depth),new(left,bottom,-depth),new(left,bottom,depth),new(right,bottom,depth),tint);
            }
            sides.GenerateNormals();var sideMesh=sides.Commit();sides.Dispose();var sideMaterial=new StandardMaterial3D{VertexColorUseAsAlbedo=true,CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=1};model.owned.Add(sideMesh);model.owned.Add(sideMaterial);model.AddChild(new MeshInstance3D{Mesh=sideMesh,MaterialOverride=sideMaterial,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        }
        return model;
    }
    public static PresentationModel Arm(bool left=false)
    {
        var model=new PresentationModel();model.TexturedBox(model,new(.22f,.66f,.22f),new(0,-.33f,0),face=>Skin("arm",face,left));Sleeve(model,model,left);return model;
    }
    public static PresentationModel Avatar(Inventory? equipment,Catalog? catalog=null)
    {
        var model=new PresentationModel{Head=new(),RightArm=new(),LeftArm=new()};model.Head.Position=new(0,1.5f,0);model.AddChild(model.Head);model.RightArm.Position=new(-.34f,1.24f,0);model.LeftArm.Position=new(.34f,1.24f,0);model.AddChild(model.RightArm);model.AddChild(model.LeftArm);
        model.TexturedBox(model.Head,new(.45f,.45f,.45f),new(0,.225f,0),face=>Skin("head",face));model.TexturedBox(model,new(.45f,.68f,.25f),new(0,1.15f,0),face=>Skin("body",face));
        foreach(var (arm,left) in new[]{(model.RightArm,false),(model.LeftArm,true)})model.TexturedBox(arm,new(.22f,.68f,.25f),new(0,-.26f,0),face=>Skin("arm",face,left));
        model.TexturedBox(model,new(.22f,.68f,.25f),new(-.115f,.48f,0),face=>Skin("leg",face));model.TexturedBox(model,new(.22f,.68f,.25f),new(.115f,.48f,0),face=>Skin("leg",face,true));
        // Layered hair, hood, coat tails and pouches follow the supplied adventurer sheet.
        Color hair=new("24242d"),blue=new("4169c2"),dark=new("262831"),silver=new("b8becb");
        model.Box(model.Head,new(.49f,.12f,.48f),new(0,.45f,-.015f),hair);
        for(int i=0;i<5;i++){var tuft=new Node3D{Position=new(-.17f+i*.085f,.51f,-.05f+(i%2)*.12f),Rotation=new(.12f,0,(i%2==0?1:-1)*.22f)};model.Head.AddChild(tuft);model.Box(tuft,new(.105f,.065f,.18f),Vector3.Zero,i%2==0?hair:new("303039"));}
        for(int i=0;i<6;i++){
            float x=-.205f+i*.082f,drop=i switch{0=>.17f,1=>.21f,2=>.11f,3=>.25f,4=>.12f,_=>.18f};
            model.Box(model.Head,new(.088f,drop,.075f),new(x,.43f-drop/2,.242f),i%2==0?hair:new("303039"));
            model.Box(model.Head,new(.075f,.24f+(i%2)*.06f,.055f),new(x,.29f,-.252f),hair);
        }
        foreach(float x in new[]{-.248f,.248f}){model.Box(model.Head,new(.065f,.25f,.38f),new(x,.31f,-.055f),hair);model.Box(model.Head,new(.075f,.09f,.12f),new(x*1.06f,.42f,.035f),new("30313c"));}
        model.Box(model,new(.54f,.14f,.35f),new(0,1.45f,-.025f),dark);model.Box(model,new(.42f,.055f,.075f),new(0,1.465f,.18f),blue);
        model.Box(model,new(.45f,.47f,.06f),new(0,1.15f,-.17f),dark);model.Box(model,new(.09f,.42f,.02f),new(0,1.19f,-.212f),blue);
        foreach(float sign in new[]{-1f,1f}){
            var tail=new Node3D{Position=new(sign*.20f,.83f,-.14f),Rotation=new(0,0,-sign*.17f)};model.AddChild(tail);model.Box(tail,new(.18f,.51f,.07f),new(0,-.255f,0),blue);model.Box(tail,new(.07f,.37f,.012f),new(-sign*.047f,-.23f,.044f),dark);model.Box(tail,new(.085f,.026f,.012f),new(0,-.37f,.045f),silver);model.Box(tail,new(.026f,.085f,.012f),new(0,-.37f,.046f),silver);
            model.Box(model,new(.13f,.16f,.10f),new(sign*.24f,.83f,.15f),new("695546"));model.Box(model,new(.055f,.06f,.018f),new(sign*.24f,.85f,.21f),silver);
        }
        Sleeve(model,model.RightArm,false);Sleeve(model,model.LeftArm,true);
        model.Box(model,new(.075f,.04f,.02f),new(0,1.30f,-.214f),silver);model.Box(model,new(.035f,.10f,.02f),new(0,1.30f,-.215f),silver);
        foreach(float s in new[]{-1f,1f})for(int i=0;i<3;i++)model.Box(model,new(.055f,.045f,.018f),new(s*(.14f-i*.043f),1.30f-i*.04f,-.215f),silver);
        Color armor=new("b9c4c8"),shade=new("7e909e");
        void Tint(int slot){if(equipment?.Slots[slot] is {} item){armor=catalog!=null?new(ArmorTraits.Color(item,catalog)):new("b9c4c8");shade=armor.Darkened(.25f);}}
        Tint(0);
        if(equipment?.Slots[0]!=null){model.Box(model.Head,new(.49f,.1f,.49f),new(0,.435f,0),armor);model.Box(model.Head,new(.055f,.34f,.49f),new(-.23f,.26f,0),shade);model.Box(model.Head,new(.055f,.34f,.49f),new(.23f,.26f,0),shade);}
        Tint(1);
        if(equipment?.Slots[1]?.Id=="elytra"){foreach(float s in new[]{-1f,1f}){var wing=new Node3D{Position=new(s*.13f,1.40f,-.22f),Rotation=new(0,s*.22f,-s*.22f)};model.AddChild(wing);model.Box(wing,new(.24f,.75f,.05f),new(s*.13f,-.32f,-.02f),armor);}}
        else if(equipment?.Slots[1]!=null){model.Box(model,new(.49f,.61f,.28f),new(0,1.15f,0),armor);model.Box(model,new(.28f,.20f,.28f),new(-.34f,1.3f,0),shade);model.Box(model,new(.28f,.20f,.28f),new(.34f,1.3f,0),shade);model.Box(model,new(.05f,.5f,.01f),new(0,1.15f,.15f),shade);}
        Tint(2);
        if(equipment?.Slots[2]!=null)foreach(float x in new[]{-.115f,.115f})model.Box(model,new(.235f,.47f,.27f),new(x,.63f,0),shade);
        Tint(3);if(equipment?.Slots[3]!=null)foreach(float x in new[]{-.115f,.115f})model.Box(model,new(.24f,.24f,.32f),new(x,.22f,.025f),armor);
        return model;
    }
    private static void Sleeve(PresentationModel model,Node3D arm,bool left)
    {
        float side=left?1:-1;Color silver=new("b8becb"),blue=new("4169c2");
        model.Box(arm,new(.25f,.16f,.28f),new(0,-.08f,0),new("535663"));
        model.Box(arm,new(.018f,.045f,.11f),new(side*.135f,-.08f,.01f),blue);model.Box(arm,new(.019f,.11f,.038f),new(side*.136f,-.08f,.01f),blue);
        model.Box(arm,new(.235f,.06f,.265f),new(0,-.39f,0),silver);model.Box(arm,new(.24f,.12f,.27f),new(0,-.52f,0),new("24252d"));model.Box(arm,new(.015f,.055f,.10f),new(side*.127f,-.51f,0),silver);
    }
    private static Image Skin(string part,int face,bool left=false)
    {
        int height=part=="head"?16:24;var image=Image.CreateEmpty(16,height,false,Image.Format.Rgba8);
        for(int y=0;y<height;y++)for(int x=0;x<16;x++){
            Color c=part=="head"?new("f1cdb5"):new("2c2d37");float variation=((x*3+y*7+face*11)%7-3)*.012f;c=variation>=0?c.Lightened(variation):c.Darkened(-variation);
            if(part=="head"){
                if(face is 4 or 1||y<3||face is 2 or 3&&y<10)c=new("262630");
                if(face==0){
                    if(y is 8 or 9&&x is >=2 and <=5 or >=10 and <=13)c=new("f5f1eb");
                    if(y is 8 or 9&&x is 4 or 5 or 10 or 11)c=new("4d7ed7");
                    if(y==8&&x is 5 or 10)c=new("253765");if(y==7&&x is >=2 and <=5 or >=10 and <=13)c=new("292831");
                    if(y==12&&x is 7 or 8)c=new("b38677");if(y<6&&x%5<2||x is 0 or 15&&y<10)c=new("262630");
                }
            }else if(part=="body"){
                if(face==0){
                    if(x is >=6 and <=9)c=new("dce0e6");if(x is 4 or 11)c=new("4169c2");
                    if(y is >=5 and <=13&&Math.Abs(x-(13-y/2))<=1)c=new("6a584c");
                    if(x is 2 or 13&&y is 4 or 9 or 14)c=new("b8becb");if(y==0&&x is 7 or 8)c=new("f1cdb5");
                }
                if(face==1&&x is 7 or 8)c=new("4169c2");if(y>19)c=new("665447");if(y is 20 or 23)c=new("25252d");if(face==0&&y is 20 or 21&&x is 6 or 9)c=new("b8becb");
            }else if(part=="arm"){
                if(y is >=12 and <=15)c=new("bec3cd");if(y is >=16 and <=18||y>=22)c=new("f1cdb5");if(y is >=19 and <=21)c=new("22232b");if(x is 0 or 15)c=c.Darkened(.12f);
                if(face==0&&y is 4 or 5&&x is >=6 and <=9)c=new("4169c2");
            }else{
                if(x is 0 or 15)c=new("20212a");if(y is 4 or 5)c=new("67544b");if(y is 11 or 12)c=new("a5aab7");if(y>=17)c=new("20212a");if(y is 18 or 19&&x is >=9 and <=12)c=new("4169c2");if(y>=22)c=new("b8becb");
            }
            image.SetPixel(x,y,c);
        }
        return image;
    }
}
