using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool characterPreviewCheck;

    private void InitializeCharacterValidation()
    {
        bool menuPreview=OS.GetCmdlineUserArgs().Contains("--multiplayer-menu-preview");
        characterPreviewCheck=menuPreview||OS.GetCmdlineUserArgs().Contains("--character-preview-check");
        if(menuPreview)RunMultiplayerMenuPreview();else if(characterPreviewCheck)RunCharacterPreviewCheck();
    }

    private async void RunMultiplayerMenuPreview()
    {
        string path=ProjectSettings.GlobalizePath("res://docs/previews/multiplayer-join-native.png");
        string proof=ProjectSettings.GlobalizePath("res://test-results/multiplayer-menu-preview.json");
        var samples=new List<object>();
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        void Record(bool passed,string error)=>File.WriteAllText(proof,JsonSerializer.Serialize(new{passed,error,rendered=passed,nativeJoinMenu=true,path,samples},Catalog.JsonOptions));
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);Directory.CreateDirectory(Path.GetDirectoryName(proof)!);
            Check(DisplayServer.GetName()!="headless","Native join-menu preview requires a rendered hidden window.");
            ShowJoinRoom();await Frames(20);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var previews=Descendants(overlay).OfType<AvatarPreview>().ToArray();
            Check(CharacterAppearances.Ids.All(id=>previews.Count(p=>p.AppearanceId==id)==1),"join menu is missing an approved appearance or duplicates a card");
            foreach(var preview in previews){
                var viewport=preview.GetChildren().OfType<SubViewport>().Single();
                Check(viewport.Size.X>=100&&viewport.Size.Y>=100,preview.AppearanceId+" preview viewport remains too small after container layout");
                using var picture=viewport.GetTexture().GetImage();var colors=new HashSet<string>();for(int y=0;y<picture.GetHeight();y+=4)for(int x=0;x<picture.GetWidth();x+=4)colors.Add(picture.GetPixel(x,y).ToHtml());
                Check(colors.Count>20,preview.AppearanceId+" preview is blank or lost pixel textures");
                samples.Add(new{id=preview.AppearanceId,size=new[]{viewport.Size.X,viewport.Size.Y},visibleColors=colors.Count});
            }
            foreach(string id in CharacterAppearances.Ids){
                var button=Descendants(overlay).OfType<Button>().Single(b=>b.Text.Replace(" ✓","")==CharacterAppearances.Name(id));button.EmitSignal(Button.SignalName.Pressed);
                Check(networkAppearance==id&&button.Text.EndsWith(" ✓",StringComparison.Ordinal)&&Descendants(overlay).OfType<Button>().Count(b=>b.Text.EndsWith(" ✓",StringComparison.Ordinal))==1,"appearance card selection did not update the join identity: "+id);
            }
            await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var picture=GetViewport().GetTexture().GetImage())Check(picture.SavePng(path)==Error.Ok,"native join-menu PNG could not be saved");
            Record(true,"");GD.Print("MULTIPLAYER_MENU_PREVIEW_PASS: approved appearance cards render at effective size and update selection; "+path);QuitCleanly();
        }catch(Exception error){Directory.CreateDirectory(Path.GetDirectoryName(proof)!);Record(false,error.Message);GD.PushError("MULTIPLAYER_MENU_PREVIEW_FAILED: "+error);QuitCleanly(15);}
    }

    private async void RunCharacterPreviewCheck()
    {
        const int width=1800,height=1200;
        string path=ProjectSettings.GlobalizePath("res://docs/previews/multiplayer-characters-native.png");
        string proof=ProjectSettings.GlobalizePath("res://test-results/character-preview.json");
        var samples=new List<object>();string stage="native character scene";SubViewport? viewport=null;
        static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        void Record(bool passed,string error)=>File.WriteAllText(proof,JsonSerializer.Serialize(new{passed,error,stage,rendered=passed&&DisplayServer.GetName()!="headless",nativeGameModels=true,humanPlaytest=false,width,height,path,samples},Catalog.JsonOptions));
        (string Geometry,string Palette,int Meshes,int Colors) Signature(PresentationModel model)
        {
            var geometry=new List<string>();var colors=new HashSet<string>();var textures=new Dictionary<ulong,string>();
            foreach(var mesh in Descendants(model).OfType<MeshInstance3D>()){
                string size=mesh.Mesh switch{BoxMesh box=>box.Size.ToString(),QuadMesh quad=>quad.Size.ToString(),_=>mesh.Mesh.GetClass()};
                var parent=(Node3D)mesh.GetParent();string pivot=parent==model?"root":parent.Position+":"+parent.Rotation;
                geometry.Add(pivot+":"+size+":"+mesh.Position);
                if(mesh.MaterialOverride is StandardMaterial3D material){colors.Add(material.AlbedoColor.ToHtml());if(material.AlbedoTexture!=null){ulong id=material.AlbedoTexture.GetInstanceId();if(!textures.ContainsKey(id)){using var pixels=material.AlbedoTexture.GetImage();textures[id]=Convert.ToHexString(SHA256.HashData(pixels.GetData()));}}}
            }
            string Hash(IEnumerable<string> data)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",data.OrderBy(s=>s,StringComparer.Ordinal)))));
            return(Hash(geometry),Hash(colors.Concat(textures.Values)),geometry.Count,colors.Count);
        }
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);Directory.CreateDirectory(Path.GetDirectoryName(proof)!);
            Check(DisplayServer.GetName()!="headless","Native character capture requires a renderer; run the hidden window with --position -10000,-10000.");
            hudRoot.Hide();overlay.Hide();SetProcessInput(false);SetProcessUnhandledInput(false);
            Check(CharacterAppearances.Ids.Length==3&&CharacterAppearances.Ids.Distinct().Count()==3,"three approved appearances missing");
            viewport=new SubViewport{OwnWorld3D=true,Size=new(width,height),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
            viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("e0e2e5"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.7f}});
            viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-30,0),LightEnergy=1.0f});
            var camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=6.7f,Position=new(0,.8f,8)};viewport.AddChild(camera);camera.LookAt(Vector3.Zero);
            var canvas=new CanvasLayer();viewport.AddChild(canvas);var labels=new Control{Theme=UiFactory.Theme(),MouseFilter=Control.MouseFilterEnum.Ignore};canvas.AddChild(labels);
            void LabelAt(string text,float x,float y,float labelWidth,int size){var label=new Label{Text=text,Position=new(x,y),Size=new(labelWidth,52),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",new("28333e"));label.AddThemeConstantOverride("shadow_offset_x",0);label.AddThemeConstantOverride("shadow_offset_y",0);labels.AddChild(label);}
            LabelAt("方野：余烬灯塔 · 三款联机角色",0,28,width,36);
            LabelAt("游戏内实际模型 · 正面与背面",0,82,width,22);
            var geometryHashes=new HashSet<string>();var paletteHashes=new HashSet<string>();
            var armor=new Inventory(5);for(int slot=0;slot<4;slot++){var item=catalog.Items.Values.First(d=>d.Use=="armor"&&d.Id.StartsWith("iron_",StringComparison.Ordinal)&&d.ArmorSlot==slot);armor.Slots[slot]=new(item.Id,1);}
            for(int i=0;i<CharacterAppearances.Ids.Length;i++){
                string id=CharacterAppearances.Ids[i];float x=(i-1)*3.35f;
                var front=PresentationModel.Avatar(null,catalog,id);front.Position=new(x,.3f,0);front.RotationDegrees=new(0,-15,0);viewport.AddChild(front);
                var back=PresentationModel.Avatar(null,catalog,id);back.Position=new(x,-2.4f,0);back.RotationDegrees=new(0,165,0);viewport.AddChild(back);
                Check(front.Head!=null&&front.RightArm!=null&&front.LeftArm!=null&&front.RightLeg!=null&&front.LeftLeg!=null,id+" movable body pivots missing");
                var signature=Signature(front);Check(signature.Meshes>40&&signature.Colors>=5,id+" incomplete character geometry or materials");
                Check(geometryHashes.Add(signature.Geometry)&&paletteHashes.Add(signature.Palette),id+" shares geometry or palette with another approved character");
                var equipped=PresentationModel.Avatar(armor,catalog,id);equipped.Position=new(100,0,0);viewport.AddChild(equipped);
                Check(Signature(equipped).Meshes>signature.Meshes&&equipped.RightLeg!.GetChildren().Count>front.RightLeg!.GetChildren().Count&&equipped.RightArm!.GetChildren().Count>front.RightArm!.GetChildren().Count,id+" armor does not follow moving limbs");
                samples.Add(new{id,name=CharacterAppearances.Name(id),meshes=signature.Meshes,materialColors=signature.Colors,geometrySignature=signature.Geometry,paletteSignature=signature.Palette,armorCompatible=true,movableLimbs=true});equipped.QueueFree();
                // The camera's aspect ratio gives each model an equal 600 px column.
                LabelAt(CharacterAppearances.Name(id)+" · 正面",i*600,135,600,26);LabelAt(CharacterAppearances.Name(id)+" · 背面",i*600,630,600,26);
            }
            LabelAt("进入房间前选择外观 · 同伴在世界中看到相同角色",0,1120,width,24);
            stage="native rendered image";await Frames(8);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var image=viewport.GetTexture().GetImage()){
                Check(image.GetWidth()==width&&image.GetHeight()==height,"native viewport image has wrong dimensions");
                var renderedColors=new HashSet<string>();for(int y=180;y<1090;y+=8)for(int x=120;x<1680;x+=8)renderedColors.Add(image.GetPixel(x,y).ToHtml());Check(renderedColors.Count>100,"character render is blank or lacks textures");
                Check(image.SavePng(path)==Error.Ok,"native character preview PNG could not be saved");
            }
            stage="complete";Record(true,"");GD.Print("CHARACTER_PREVIEW_PASS: three approved native models, front/back, distinct geometry/palettes and armor compatibility; "+path);
            viewport.QueueFree();await Frames(2);QuitCleanly();
        }catch(Exception error){Directory.CreateDirectory(Path.GetDirectoryName(proof)!);Record(false,error.Message);GD.PushError("CHARACTER_PREVIEW_FAILED: "+error);viewport?.QueueFree();QuitCleanly(14);}
    }
}
