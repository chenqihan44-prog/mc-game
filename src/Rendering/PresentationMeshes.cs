using System;
using System.Collections.Generic;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class PresentationModel : Node3D
{
    private readonly List<Resource> owned=[];
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
    public override void _ExitTree(){foreach(var resource in owned)resource.Dispose();owned.Clear();}
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
        var model=new PresentationModel();model.TexturedBox(model,new(.22f,.66f,.22f),new(0,-.33f,0),face=>Skin("arm",face,left));return model;
    }
    public static PresentationModel Avatar(Inventory? equipment)
    {
        var model=new PresentationModel{Head=new(),RightArm=new(),LeftArm=new()};model.Head.Position=new(0,1.5f,0);model.AddChild(model.Head);model.RightArm.Position=new(-.34f,1.24f,0);model.LeftArm.Position=new(.34f,1.24f,0);model.AddChild(model.RightArm);model.AddChild(model.LeftArm);
        model.TexturedBox(model.Head,new(.45f,.45f,.45f),new(0,.225f,0),face=>Skin("head",face));model.TexturedBox(model,new(.45f,.68f,.25f),new(0,1.15f,0),face=>Skin("body",face));
        foreach(var (arm,left) in new[]{(model.RightArm,false),(model.LeftArm,true)})model.TexturedBox(arm,new(.22f,.68f,.25f),new(0,-.26f,0),face=>Skin("arm",face,left));
        model.TexturedBox(model,new(.22f,.68f,.25f),new(-.115f,.48f,0),face=>Skin("leg",face));model.TexturedBox(model,new(.22f,.68f,.25f),new(.115f,.48f,0),face=>Skin("leg",face,true));
        Color armor=new("b9c4c8"),shade=new("7e909e");
        if(equipment?.Slots[0]!=null){model.Box(model.Head,new(.49f,.1f,.49f),new(0,.435f,0),armor);model.Box(model.Head,new(.055f,.34f,.49f),new(-.23f,.26f,0),shade);model.Box(model.Head,new(.055f,.34f,.49f),new(.23f,.26f,0),shade);}
        if(equipment?.Slots[1]!=null){model.Box(model,new(.49f,.61f,.28f),new(0,1.15f,0),armor);model.Box(model,new(.28f,.20f,.28f),new(-.34f,1.3f,0),shade);model.Box(model,new(.28f,.20f,.28f),new(.34f,1.3f,0),shade);model.Box(model,new(.05f,.5f,.01f),new(0,1.15f,.15f),shade);}
        if(equipment?.Slots[2]!=null)foreach(float x in new[]{-.115f,.115f})model.Box(model,new(.235f,.47f,.27f),new(x,.63f,0),shade);
        if(equipment?.Slots[3]!=null)foreach(float x in new[]{-.115f,.115f})model.Box(model,new(.24f,.24f,.32f),new(x,.22f,.025f),armor);
        return model;
    }
    private static Image Skin(string part,int face,bool left=false)
    {
        int height=part=="head"?8:12;var image=Image.CreateEmpty(8,height,false,Image.Format.Rgba8);
        for(int y=0;y<height;y++)for(int x=0;x<8;x++){
            Color c=part switch{"head"=>new("c99570"),"body" or "arm"=>new("467c70"),_=>new("465c73")};float variation=((x*3+y*7+face*11)%7-3)*.015f;c=variation>=0?c.Lightened(variation):c.Darkened(-variation);
            if(part=="head"){
                if(face is 4 or 1||y<2||face is 2 or 3&&y<4)c=new("4b3328");
                if(face==0){if(y==3&&x is 1 or 2 or 5 or 6)c=new("f0ded0");if(y==3&&x is 2 or 5)c=new("406672");if(y==5&&x is 3 or 4)c=new("af704f");if(y==6&&x is >1 and <6)c=new("634334");if(y==2&&x is 1 or 2 or 5 or 6)c=new("5b3b29");}
            }else if(part=="body"){
                if(y==0&&x is 3 or 4)c=new("c99570");if(x is 0 or 7)c=c.Darkened(.18f);if(y>9)c=new("705b43");if(face==0&&x==4&&y>1&&y<9)c=new("a9bdae");if(face==0&&x is 1 or 2&&y is 4 or 5)c=new("366356");if(face==1&&x>2&&x<6&&y>2&&y<9)c=new("8c6b49");
            }else if(part=="arm"){
                if(y>7)c=new("c99570");if(y==7)c=new("92aea1");if(x==0||x==7)c=c.Darkened(.1f);if(left&&face==0&&y==3&&x is 3 or 4)c=new("ba9e68");
            }else{if(x is 0 or 7)c=new("35465e");if(y>8)c=new("4a3b30");if(y==11)c=new("282822");if(y==2&&x==3)c=new("6b8192");}
            image.SetPixel(x,y,c);
        }
        return image;
    }
}
