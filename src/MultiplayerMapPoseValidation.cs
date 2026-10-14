using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    // A native, headless-compatible check. The caller owns startup isolation and
    // completion/quit. Rendered proof files stay in that run's isolated folder.
    private async Task ValidateMultiplayerMapAndCrouch()
    {
        var samples=new List<object>();var avatars=new List<RemoteAvatar>();WorldMap? map=null;
        if(string.IsNullOrWhiteSpace(uiStyleDirectory))throw new InvalidOperationException("Map/pose validation requires an isolated UI validation directory.");
        string proof=Path.Combine(uiStyleDirectory,"multiplayer-map-pose.json");var screenshots=new List<string>();
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        void Record(bool passed,string error)=>File.WriteAllText(proof,JsonSerializer.Serialize(new{passed,error,nativeEngine=true,headless=DisplayServer.GetName()=="headless",screenshotsGenerated=screenshots.Count>0,screenshots,samples},Catalog.JsonOptions));
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(proof)!);
            var armor=new Inventory(5);armor.Slots[0]=new("iron_helmet",1);armor.Slots[1]=new("iron_chestplate",1);armor.Slots[2]=new("iron_leggings",1);armor.Slots[3]=new("iron_boots",1);
            foreach(string id in CharacterAppearances.Ids.Append("adventurer")){
                var avatar=new RemoteAvatar{Catalog=catalog,AppearanceId=id,Nickname="姿态检查"};AddChild(avatar);avatar.SetProcess(false);avatars.Add(avatar);
                avatar.UpdateState(new(300,80,300),.4f,.23f,false,armor);avatar._Process(1d/60);var model=avatar.Model!;
                var dimensions=Descendants(model).OfType<MeshInstance3D>().Where(m=>m.Mesh is BoxMesh).ToDictionary(m=>m.GetInstanceId(),m=>((BoxMesh)m.Mesh).Size);
                float standingHead=model.Head.GlobalPosition.Y;
                avatar.UpdateState(new(300,80,300),.4f,.23f,true,armor);avatar._Process(1d/60);
                Check(avatar.CrouchBlend>0&&avatar.CrouchBlend<1,id+" crouch transition snaps instead of interpolating");
                for(int frame=0;frame<120;frame++)avatar._Process(1d/60);
                Check(avatar.CrouchBlend>.99f&&model.Scale==Vector3.One&&Descendants(model).OfType<Node3D>().All(n=>n.Scale==Vector3.One),id+" crouch compresses character geometry");
                Check(Descendants(model).OfType<MeshInstance3D>().Where(m=>m.Mesh is BoxMesh).All(m=>((BoxMesh)m.Mesh).Size==dimensions[m.GetInstanceId()]),id+" crouch changes proportions");
                Check(model.Head.GlobalPosition.Y<standingHead-.25f&&model.Torso.Rotation.X>.6f&&model.RightLeg.Rotation.X<-.9f&&model.RightLowerLeg.Rotation.X>1.5f,id+" head/waist/knees do not form a crouch pose");
                Check(Math.Abs(model.Head.Rotation.X+model.Torso.Rotation.X+.23f)<.001f,id+" crouch overrides the synced look pitch");
                Check(model.RightLowerLeg.GetChildren().OfType<MeshInstance3D>().Any(m=>m.Mesh is BoxMesh b&&Math.Abs(b.Size.Z-.32f)<.001f),id+" boots do not follow the knee hinge");
                float crouchingHead=model.Head.GlobalPosition.Y;
                avatar.UpdateState(new(300,80,300),.4f,.23f,false,armor);for(int frame=0;frame<120;frame++)avatar._Process(1d/60);
                Check(avatar.CrouchBlend<.001f&&model.Head.GlobalPosition.Y>standingHead-.01f,id+" returning to standing retains the crouch offset");
                avatar.UpdateState(new(300.2f,80,300),.4f,.23f,false,armor);avatar._Process(.02);
                Check(Math.Abs(model.RightArm.Rotation.X)>0&&Math.Abs(model.RightLeg.Rotation.X)>0,id+" walking animation was lost");
                samples.Add(new{appearance=id,standingHead,crouchingHead,unchangedBoxMeshes=dimensions.Count,pitchPreserved=true,armorAndWalking=true});
            }
            var snapshot=new WorldSnapshot();var isolated=new WorldState(catalog,snapshot);int discovered=snapshot.ExploredMap.Count;
            var self=new MapPlayerMarker("local","本机旅人",new(0,39,0),.2f,true);var friend=new MapPlayerMarker("friend-map","异地朋友",new(120,40,0),1.2f);var roster=new List<MapPlayerMarker>{self,friend};string status="";
            map=new WorldMap{World=isolated,PlayerPosition=self.Position,PositionSource=()=>self.Position,FacingSource=()=>self.Yaw,PlayerMarkersSource=()=>roster,Size=new(640,320),Theme=UiFactory.Theme(),StatusChanged=text=>status=text};AddChild(map);
            var timer=Stopwatch.StartNew();while(!map.IsMapReady){if(timer.Elapsed.TotalSeconds>20)throw new TimeoutException("isolated multiplayer map tiles");await Frame();}
            var fog=new Color(30/255f,36/255f,40/255f);Check(map.PlayerMarkers.Count==2&&map.TrySample(120,0,out var color)&&color==fog&&snapshot.ExploredMap.Count==discovered,"online friend reveals unknown terrain or is absent from map");
            Vector2 point=map.PlayerScreenPoint(friend);map._GuiInput(new InputEventMouseMotion{Position=point});Check(map.TooltipText.Contains("异地朋友")&&map.TooltipText.Contains("120"),"player hover does not identify name and coordinates in fog");
            map._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=point});Check(map.SelectedPlayerId==friend.Id&&status.Contains("异地朋友"),"clicking player marker does not select its details");
            Color stableColor=WorldMap.PlayerColor(friend);friend=friend with{Position=new(140,41,8),Yaw=-.8f};roster=[self,friend];map._Process(.11);
            Check(map.PlayerMarkers.Single(m=>m.Id==friend.Id).Position==friend.Position&&map.PlayerMarkers.Single(m=>m.Id==friend.Id).Yaw==friend.Yaw&&WorldMap.PlayerColor(friend)==stableColor,"live roster movement/heading is stale or changes player color");
            friend=friend with{Position=new(900,41,8)};roster=[self,friend];map._Process(.11);Check(status.Contains("视野外")&&new Rect2(Vector2.Zero,map.Size).HasPoint(map.PlayerScreenPoint(friend)),"out-of-view friend has no edge indicator/status");
            roster=[self];map._Process(.11);Check(map.PlayerMarkers.Count==1&&map.SelectedPlayerId==null&&!status.Contains("异地朋友")&&snapshot.ExploredMap.Count==discovered,"disconnected player remains marked or marker viewing reveals terrain");
            self=self with{Position=new(5,39,3)};roster=[self];map._Process(.11);Check(map.PlayerPosition==self.Position&&map.PlayerMarkers[0].Position==self.Position,"local player marker does not update while map is open");
            Check(WorldMap.PlayerColor(self)!=stableColor,"local and friend markers share color");
            samples.Add(new{mapLiveRoster=true,unknownTerrainStayedFogged=true,hoverAndClick=true,outsideIndicator=true,disconnectRemoved=true,colorsDeterministic=true});
            if(DisplayServer.GetName()!="headless"){
                string crouchPath=Path.Combine(uiStyleDirectory,"multiplayer-crouch-native.png");await CaptureNativeCrouchProof(crouchPath);screenshots.Add(crouchPath);
                string mapPath=Path.Combine(uiStyleDirectory,"multiplayer-map-native.png");await CaptureNativeMapProof(isolated,mapPath);screenshots.Add(mapPath);
            }
            Record(true,"");GD.Print("MULTIPLAYER_MAP_POSE_PASS: rigid crouch with knees/look/armor/walk and live self/friend markers without revealing fog");
        }catch(Exception error){Directory.CreateDirectory(Path.GetDirectoryName(proof)!);Record(false,error.Message);throw;}
        finally{map?.QueueFree();foreach(var avatar in avatars)avatar.QueueFree();await Frame();}
    }

    private async Task CaptureNativeCrouchProof(string path)
    {
        var viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,720),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        try{
            viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("252522"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.7f}});
            viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-25,0),LightEnergy=1});
            var camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=2.9f,Position=new(3.8f,2.0f,6)};viewport.AddChild(camera);camera.LookAt(new(0,1.05f,0));
            foreach(bool crouching in new[]{false,true}){
                var avatar=new RemoteAvatar{Catalog=catalog,AppearanceId="ranger",Nickname=crouching?"蹲下 · 腰与膝弯曲":"站立"};viewport.AddChild(avatar);avatar.SetProcess(false);
                avatar.UpdateState(new(crouching?.95f:-.95f,0,0),Mathf.Pi,0,crouching,null);for(int frame=0;frame<120;frame++)avatar._Process(1d/60);
            }
            var canvas=new CanvasLayer();viewport.AddChild(canvas);var root=new Control{Theme=UiFactory.Theme(),MouseFilter=Control.MouseFilterEnum.Ignore};canvas.AddChild(root);
            var title=UiFactory.Label("远端角色 · 站立与蹲姿",30);title.Position=new(24,18);title.Size=new(1152,48);title.HorizontalAlignment=HorizontalAlignment.Center;title.AddThemeColorOverride("font_color",UiFactory.Gold);root.AddChild(title);
            var note=UiFactory.Label("头部保留观察方向，躯干前倾与膝盖弯曲；各部位保持原有尺寸。",20);note.Position=new(24,662);note.Size=new(1152,38);note.HorizontalAlignment=HorizontalAlignment.Center;root.AddChild(note);
            for(int frame=0;frame<6;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var picture=viewport.GetTexture().GetImage();if(picture.SavePng(path)!=Error.Ok)throw new IOException("Native crouch screenshot could not be saved.");
        }finally{viewport.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }

    private async Task CaptureNativeMapProof(WorldState isolated,string path)
    {
        var viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,720),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        try{
            var background=new ColorRect{Color=new("21190f"),Position=Vector2.Zero,Size=new(1200,720),MouseFilter=Control.MouseFilterEnum.Ignore};viewport.AddChild(background);
            var root=new Control{Theme=UiFactory.Theme(),MouseFilter=Control.MouseFilterEnum.Ignore};viewport.AddChild(root);
            var title=UiFactory.Label("荒野地图 · 在线玩家",30);title.Position=new(28,16);title.Size=new(1144,50);title.HorizontalAlignment=HorizontalAlignment.Center;title.AddThemeColorOverride("font_color",UiFactory.Gold);root.AddChild(title);
            var legend=UiFactory.Label("暖白箭头是我 · 彩色箭头是朋友 · 暗区仍未探索",18);legend.Position=new(28,74);legend.Size=new(1144,32);legend.HorizontalAlignment=HorizontalAlignment.Center;root.AddChild(legend);
            var roster=new List<MapPlayerMarker>{new("local","本机旅人",new(5,39,3),.2f,true),new("friend-map","异地朋友",new(120,40,18),1.2f),new("friend-outside","远处队友",new(900,39,-30),-.4f)};
            isolated.RevealMap(roster[0].Position.X,roster[0].Position.Z);
            var details=UiFactory.Label("",16);details.Position=new(28,630);details.Size=new(1144,68);details.AutowrapMode=TextServer.AutowrapMode.WordSmart;root.AddChild(details);
            var widget=new WorldMap{World=isolated,PlayerPosition=roster[0].Position,PlayerMarkersSource=()=>roster,Position=new(28,120),Size=new(1144,490),Theme=UiFactory.Theme(),StatusChanged=text=>details.Text=text};root.AddChild(widget);
            var timer=Stopwatch.StartNew();while(!widget.IsMapReady){if(timer.Elapsed.TotalSeconds>20)throw new TimeoutException("Native multiplayer map screenshot tiles");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            widget._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=widget.PlayerScreenPoint(roster[1])});widget._Process(.11);
            if(!widget.TrySample(120,18,out var fog)||fog!=new Color(30/255f,36/255f,40/255f))throw new InvalidOperationException("Screenshot friend location revealed its terrain.");
            for(int frame=0;frame<4;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var picture=viewport.GetTexture().GetImage();if(picture.SavePng(path)!=Error.Ok)throw new IOException("Native multiplayer map screenshot could not be saved.");
        }finally{viewport.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    }
}
