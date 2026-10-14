using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private async Task CaptureUpdatedPresentation(string folder)
    {
        var viewport=new SubViewport{OwnWorld3D=true,Size=new(600,440),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("c8ccd4"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.7f}});
        viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-42,-28,0),LightEnergy=1.15f});
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=2.9f,Current=true};viewport.AddChild(camera);
        async Task Shot(Node3D model,Vector3 eye,Vector3 focus,string title,Image sheet,Vector2I offset)
        {
            viewport.AddChild(model);camera.Position=eye;camera.LookAt(focus);
            var label=new Label3D{Text=title,FontSize=38,PixelSize=.003f,Position=new(0,-.17f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new("20232e"),NoDepthTest=true};viewport.AddChild(label);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var picture=viewport.GetTexture().GetImage();sheet.BlitRect(picture,new(0,0,600,440),offset);viewport.RemoveChild(model);model.QueueFree();viewport.RemoveChild(label);label.QueueFree();
        }
        using(var sheet=Image.CreateEmpty(1800,440,false,Image.Format.Rgba8)){
            int i=0;foreach(float angle in new[]{0f,90f,180f}){var model=PresentationModel.Avatar(null);model.RotationDegrees=new(0,angle,0);await Shot(model,new(.7f,1.35f,5),new(0,1.03f,0),i==0?"人物 · 正面":i==1?"人物 · 侧面":"人物 · 背面",sheet,new(i++*600,0));}sheet.SavePng(Path.Combine(folder,"adventurer-model.png"));
        }
        using(var sheet=Image.CreateEmpty(1800,((creatureDefs.Length+2)/3)*440,false,Image.Format.Rgba8)){
            int i=0;foreach(var def in creatureDefs){var model=CreatureModel.Create(def.Id);camera.Size=Math.Max(2.7f,model.BodyHeight*1.45f);float scale=Math.Max(1,model.BodyHeight/2);await Shot(model,new Vector3(3,2,-5)*scale,new(0,model.BodyHeight*.55f,0),def.Name,sheet,new(i%3*600,i/3*440));i++;}sheet.SavePng(Path.Combine(folder,"creature-models.png"));
        }
        RemoveChild(viewport);viewport.QueueFree();
        SetScreen("play");player!.SetPhysicsProcess(false);player.Camera.LookAt(player.GlobalPosition+new Vector3(0,0,-5));world!.State.Weather.Blend=0;world.State.Weather.Phase=0;world.State.Weather.Remaining=180;
        async Task<double> ScreenShot(string name){for(int i=0;i<24;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var picture=GetViewport().GetTexture().GetImage();picture.SavePng(Path.Combine(folder,name+".png"));
            double sum=0;int count=0;for(int y=(int)(picture.GetHeight()*.38);y<picture.GetHeight()*.75;y+=4)for(int x=(int)(picture.GetWidth()*.25);x<picture.GetWidth()*.75;x+=4){var c=picture.GetPixel(x,y);sum+=c.R*.2126+c.G*.7152+c.B*.0722;count++;}return sum/count;
        }
        double day=0,night=0;
        foreach(var sample in new[]{("day",300d),("sunset",600d),("night",900d)}){world.State.DayTime=sample.Item2;UpdateDaylight();double brightness=await ScreenShot(sample.Item1);if(sample.Item1=="day")day=brightness;if(sample.Item1=="night")night=brightness;}
        var cell=EmberLighthouse.Player.Explorer.ToCell(player.GlobalPosition)+new Cell(-2,0,-3);world.State.Inventory.Add("torch",1,catalog);world.Set(cell,0);if(!world.Place(cell,"torch",false,out var error))throw new InvalidOperationException(error);renderer!.RefreshEdit(cell);
        double torch=await ScreenShot("night-torch");world.Set(cell,0);world.State.Entities.Remove(cell.ToString());renderer.RefreshEdit(cell);world.State.Inventory.Slots[selected]=new("torch",1);double held=await ScreenShot("night-held-torch");
        if(!(night<day*.3&&torch>night*1.5&&held>night*1.5+.01))throw new InvalidOperationException($"rendered lighting contrast failed: day={day}, night={night}, torch={torch}, held={held}");
        world.State.Inventory.Slots[selected]=new("wood_pick",1,43);world.State.DayTime=1199.8;world.Advance(.2);UpdateDaylight();await ScreenShot("dawn");
        File.WriteAllText(Path.Combine(folder,"lighting.json"),JsonSerializer.Serialize(new{passed=true,rendered=true,dayLuminance=day,nightLuminance=night,placedTorchLuminance=torch,heldTorchLuminance=held,sampledRegion="central scene below crosshair, excludes HUD",controlledCamera=true},Catalog.JsonOptions));
        ShowInventory();await ScreenShot("inventory-updated");
        SlotButton? Find(Node parent){foreach(var child in parent.GetChildren()){if(child is SlotButton slot&&slot.Item?.Id=="wood_pick")return slot;var nested=Find(child);if(nested!=null)return nested;}return null;}
        var source=Find(overlay)!;var tooltip=source._MakeCustomTooltip(source.TooltipText);overlay.AddChild(tooltip);tooltip.Position=source.GlobalPosition+new Vector2(24,-110);await ScreenShot("inventory-tooltip");tooltip.QueueFree();
    }
}

