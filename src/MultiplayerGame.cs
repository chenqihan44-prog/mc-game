using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Networking;
using EmberLighthouse.Player;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;

namespace EmberLighthouse;

public partial class Game
{
    private NetworkSession? network;
    private readonly Dictionary<int,RoomPlayer> roomPlayers=[];
    private readonly Dictionary<string,RemoteAvatar> remoteAvatars=[];
    private readonly Dictionary<string,IncomingTransfer> incomingTransfers=[];
    private readonly Dictionary<string,int> containerOwners=[];
    private Explorer? hostActor;
    private RoomPlayer? executingGuest;
    private NetworkIdentity networkIdentity=new();
    private string identityPath="",networkNickname="旅人",networkAppearance="adventurer",networkAddress="",networkFingerprint="";
    private string joiningWorld="",clientPlayerId="";
    private long clientCommand,clientMotion,frameSequence;
    private double roomClock,networkSendClock,clientWaitClock,clientMineClock,rosterClock;
    private bool receivingInitial,networkSaveRequested,networkUiPending,networkStopping,clientMiningActive;
    private bool NetClient=>network?.IsClient==true;
    private bool NetHost=>network?.IsHost==true;
    private bool InRoom=>NetClient||NetHost;
    private bool RemoteContext=>executingGuest!=null;
    private const double GuestMiningTimeout=.75;
    private static readonly JsonSerializerOptions NetJson=new(Catalog.JsonOptions){WriteIndented=false,DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull};

    private sealed class NetworkIdentity
    {
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        public string Nickname {get;set;}="旅人";
        public string Appearance {get;set;}="adventurer";
        public Dictionary<string,string> Credentials {get;set;}=[];
    }
    private sealed class RoomPlayer
    {
        public int Peer;
        public MultiplayerPlayerState State=null!;
        public Explorer Actor=null!;
        public Control UiRoot=null!;
        public Dictionary<int,Control> UiControls=[];
        public Dictionary<string,string> KnownChanges=[];
        public string Screen="play",Station="hand",WorkbenchRecipe="";
        public string? MiningTool;
        public string? Container;
        public Inventory Workbench=new(9);
        public Cell? MiningCell;
        public double MiningProgress,AttackCooldown,LastMotionTime,LastCommandTime;
        public long LastCommand,LastMotion,UiRevision;
        public int Selected,Rotation,Tutorial;
        public bool Sleeping,ForcePosition,Blocking,UiDirty;
        public Cell? FishingWater;
        public double FishBite,FishExpires;
        public Inventory? FishingSupply;
        public int FishingSlot;
        public MeshInstance3D? FloatView;
        public ItemGlyph? CursorGlyph;
        public string RecipeQuery="";
        public int RecipePage;
        public bool CraftableOnly;
        public double LastMineTime;
        public long CorrectionMotion;
        public double CommandWindow;
        public int CommandsInWindow;
    }
    private sealed class Wire { public string Kind {get;set;}="";public string Json {get;set;}=""; }
    private sealed class TransferPiece { public string Id {get;set;}="";public string Kind {get;set;}="";public int Index {get;set;}public int Count {get;set;}public string Data {get;set;}=""; }
    private sealed class IncomingTransfer { public string Kind="";public string[] Pieces=[];public int Bytes;public double Started; }
    private sealed class Hello
    {
        public int Protocol {get;set;}=1;public string Fingerprint {get;set;}="";public string Id {get;set;}="";
        public string Nickname {get;set;}="";public string Appearance {get;set;}="adventurer";public string Token {get;set;}="";
    }
    private sealed class Welcome { public WorldSnapshot Snapshot {get;set;}=new();public string Id {get;set;}="";public string Token {get;set;}="";public List<AvatarPose> Roster {get;set;}=[]; }
    private sealed class AvatarPose
    {
        public string Id {get;set;}="";public string Nickname {get;set;}="";public string Appearance {get;set;}="adventurer";
        public double[] Position {get;set;}=[0,39,0];public float Yaw {get;set;}public float Pitch {get;set;}public bool Crouching {get;set;}public Inventory? Equipment {get;set;}
    }
    private sealed class MotionInput
    {
        public long Sequence {get;set;}public double[] Position {get;set;}=[0,39,0];public float Yaw {get;set;}public float Pitch {get;set;}
        public bool Crouching {get;set;}public bool Blocking {get;set;}public int Selected {get;set;}
        public string MountId {get;set;}="";public bool Flying {get;set;}public bool Gliding {get;set;}
    }
    private sealed class GameAction
    {
        public long Sequence {get;set;}public string Kind {get;set;}="";public int Token {get;set;}public long UiRevision {get;set;}
        public bool Half {get;set;}public bool Shift {get;set;}public string Text {get;set;}="";public bool Toggle {get;set;}
        public int Selected {get;set;}public int Rotation {get;set;}public double Damage {get;set;}public int Token2 {get;set;}
        public float? MineYaw {get;set;}public float? MinePitch {get;set;}
    }
    private sealed class MenuUpdate { public string Screen {get;set;}="play";public long Revision {get;set;}public RemoteUiDocument? Document {get;set;} }
    private sealed class WorldFrame
    {
        public long Sequence {get;set;}public MultiplayerPlayerState Player {get;set;}=new();public bool ForcePosition {get;set;}
        public Dictionary<string,string?> Changes {get;set;}=[];public Dictionary<string,BlockEntity> Entities {get;set;}=[];
        public List<Grave> Graves {get;set;}=[];public List<CreatureState> Creatures {get;set;}=[];
        public List<ProjectileState> Projectiles {get;set;}=[];public HashSet<int> Beacons {get;set;}=[];
        public MainlineProgress Mainline {get;set;}=new();public WeatherState Weather {get;set;}=new();
        public double Time {get;set;}public double? DayTime {get;set;}public bool Creative {get;set;}public bool Casual {get;set;}public bool GuardianDefeated {get;set;}
        public List<AvatarPose> Roster {get;set;}=[];
        public string MountId {get;set;}="";public double GlideBoost {get;set;}public double[] Impulse {get;set;}=[0,0,0];
        public string? MiningCell {get;set;}public string? MiningTool {get;set;}public double MiningProgress {get;set;}
    }
    private long lastNetworkFrame;

    private void InitializeNetwork(string userRoot)
    {
        identityPath=Path.Combine(userRoot,"multiplayer-identity.json");
        try{if(File.Exists(identityPath))networkIdentity=JsonSerializer.Deserialize<NetworkIdentity>(File.ReadAllText(identityPath),Catalog.JsonOptions)??new();}catch{networkIdentity=new();}
        if(networkIdentity.Id.Length!=32||!networkIdentity.Id.All(Uri.IsHexDigit))networkIdentity=new();
        networkNickname=networkIdentity.Nickname;networkAppearance=networkIdentity.Appearance;
        networkFingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("coop-v1-build-"+typeof(Game).Assembly.ManifestModule.ModuleVersionId+"-generator-"+Terrain.Version+FileAccessText("res://data/items.json")+FileAccessText("res://data/recipes.json")+FileAccessText("res://data/creatures.json"))));
        network=new();AddChild(network);network.Message+=ReceiveNetwork;network.Motion+=ReceiveNetwork;
        network.PeerJoined+=peer=>{if(NetHost&&world!=null)SendNet(peer,"room",world.State.WorldId);};
        network.PeerLeft+=RemoveGuest;
        network.ConnectedToHost+=()=>{clientWaitClock=0;Toast("已连接房主，正在验证游戏版本");};
        network.Disconnected+=reason=>{if(!networkStopping)CallDeferred(nameof(NetworkDisconnected),reason);};
    }
    private void SaveNetworkIdentity()
    {
        networkIdentity.Nickname=networkNickname;networkIdentity.Appearance=networkAppearance;
        try{File.WriteAllText(identityPath,JsonSerializer.Serialize(networkIdentity,Catalog.JsonOptions));}catch(Exception e){Toast("玩家身份保存失败："+e.Message);}
    }
    private void ShowJoinRoom()
    {
        SetScreen("join");var col=Screen(overlay,"加入朋友的世界");
        col.AddChild(Label("先加入同一个 ZeroTier 网络，再填写房主的虚拟 IP 和端口。",15));
        var nickname=new LineEdit{Text=networkNickname,PlaceholderText="昵称（最多32字）",MaxLength=32};col.AddChild(nickname);
        var address=new LineEdit{Text=networkAddress,PlaceholderText="例如 10.147.17.2:24567"};col.AddChild(address);
        AddAppearanceChoices(col,id=>networkAppearance=id,networkAppearance);
        col.AddChild(Button("加入世界",()=>{networkNickname=CleanNickname(nickname.Text);networkAddress=address.Text.Trim();var parts=networkAddress.Split(':');int port=NetworkSession.DefaultPort;if(parts.Length>2||parts.Length==2&&!int.TryParse(parts[1],out port)||parts[0].Length==0){Toast("请填写 IPv4 地址或主机名，可附加 :端口");return;}SaveNetworkIdentity();lastNetworkFrame=0;clientCommand=clientMotion=0;clientWaitClock=0;clientPlayerId="";var result=network!.Join(parts[0],port);if(result!=Error.Ok){Toast("连接失败："+result);return;}SetScreen("connecting");var waiting=Screen(overlay,"正在连接房主");waiting.AddChild(Label("请保留窗口；连接与世界同步需要一些时间。",16));waiting.AddChild(Button("取消",()=>{StopNetworking();ShowJoinRoom();}));}));
        col.AddChild(Button("返回",ShowMenu));
    }
    private void AddAppearanceChoices(Control parent,Action<string> choose,string current)
    {
        var row=Row(parent);foreach(var id in CharacterAppearances.Names.Keys){string selectedId=id;var card=Column(row);var preview=new AvatarPreview{Catalog=catalog,AppearanceId=id,CustomMinimumSize=new(132,160)};card.AddChild(preview);var button=Button(CharacterAppearances.Name(id)+(current==id?" ✓":""),()=>{choose(selectedId);foreach(var sibling in row.GetChildren().OfType<VBoxContainer>())foreach(var b in sibling.GetChildren().OfType<Button>())b.Text=b.Text.Replace(" ✓","");((Button)card.GetChild(1)).Text=CharacterAppearances.Name(selectedId)+" ✓";});card.AddChild(button);}
    }
    private void ShowHostRoom()
    {
        SetScreen("network");var col=Screen(overlay,NetHost?"联机房间":"开放世界联机");
        if(NetHost){col.AddChild(Label($"房主：{networkNickname} · 在线 {roomPlayers.Count+1}/4\n端口 UDP {NetworkSession.DefaultPort}",17));foreach(var ip in LocalAddresses())col.AddChild(Button(ip+":"+NetworkSession.DefaultPort+"  复制",()=>DisplayServer.ClipboardSet(ip+":"+NetworkSession.DefaultPort)));col.AddChild(Label("把 ZeroTier 网卡的地址给朋友；游戏中的地址不是 Network ID。",14));col.AddChild(Button("关闭联机，继续单人",()=>{StopNetworking();RequestSave();SetScreen("play");}));}
        else{
            var nick=new LineEdit{Text=networkNickname,MaxLength=32};col.AddChild(nick);AddAppearanceChoices(col,id=>networkAppearance=id,networkAppearance);
            col.AddChild(Label("最多4人，包含房主。你退出后朋友会断开，存档保存在你的电脑。",15));
            col.AddChild(Button("开始开放联机",()=>{networkNickname=CleanNickname(nick.Text);SaveNetworkIdentity();if(!SaveNow())return;try{saves.Backup(world!.State.WorldId);}catch(Exception e){Toast("开放前备份失败："+e.Message);return;}var result=network!.CreateServer(NetworkSession.DefaultPort);if(result!=Error.Ok){Toast("开房失败，请检查端口是否占用："+result);return;}MultiplayerPlayers.CaptureHost(world!.State,networkNickname,networkAppearance);CreateHostActor();ShowHostRoom();}));
        }
        col.AddChild(Button("返回游戏",()=>SetScreen("play")));
    }
    private static string CleanNickname(string text)=>new string(text.Trim().Where(c=>!char.IsControl(c)).Take(32).ToArray()) is {Length:>0} name?name:"旅人";
    private static IEnumerable<string> LocalAddresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).Select(a=>a.Address.ToString()).Where(a=>a!="127.0.0.1").Distinct();
    private void CreateHostActor()
    {
        if(hostActor!=null)return;world!.MultiplayerActive=true;hostActor=new(){NetworkReplica=true,AcceptUserInput=false,Enabled=true,Model=world!,TerrainReady=renderer!.IsTerrainReady,Hurt=Hurt};worldRoot!.AddChild(hostActor);hostActor.AddCollisionExceptionWith(player!);player!.AddCollisionExceptionWith(hostActor);SetupNetworkSimulation();
    }
    private IEnumerable<Explorer> RoomActors()=>hostActor==null?Enumerable.Empty<Explorer>():new[]{hostActor}.Concat(roomPlayers.Values.Select(p=>p.Actor));
    private void SetupNetworkSimulation()
    {
        if(!NetHost)return;
        foreach(var creature in creatures){var c=creature;c.SelectTarget=()=>RoomActors().Where(a=>a.Enabled).OrderBy(a=>a.GlobalPosition.DistanceSquaredTo(c.GlobalPosition)).FirstOrDefault();c.AttackPlayer=damage=>HurtRoomActor(c.Player,damage,c.GlobalPosition);}
        if(projectiles!=null){projectiles.Players=()=>RoomActors().Where(a=>a.Enabled);projectiles.HitAnyPlayer=(actor,damage,origin)=>HurtRoomActor(actor,damage,origin);}
    }
    private void HurtRoomActor(Explorer actor,double damage,Vector3 origin)
    {
        if(actor==hostActor){var previous=screen;screen="play";try{HurtFrom(damage,origin);}finally{if(screen=="play")screen=previous;}return;}
        var guest=roomPlayers.Values.FirstOrDefault(p=>p.Actor==actor);if(guest!=null)WithGuest(guest,()=>{var before=screen;screen="play";HurtFrom(damage,origin);if(screen=="play")screen=before;});
    }
    private void SendNet<T>(int peer,string kind,T value,bool reliable=true)
    {
        if(network?.Connected!=true)return;
        var body=JsonSerializer.Serialize(value,NetJson);
        if(body.Length<24000){network.Send(peer,JsonSerializer.Serialize(new Wire{Kind=kind,Json=body},NetJson),reliable);return;}
        if(!reliable)return;
        using var memory=new MemoryStream();using(var zip=new GZipStream(memory,CompressionLevel.Fastest,true)){var bytes=Encoding.UTF8.GetBytes(body);zip.Write(bytes);}
        var encoded=Convert.ToBase64String(memory.ToArray());if(encoded.Length>16*1024*1024)throw new InvalidOperationException("联机同步数据过大");
        string id=Guid.NewGuid().ToString("N");int count=(encoded.Length+23999)/24000;
        for(int i=0;i<count;i++)network.Send(peer,JsonSerializer.Serialize(new Wire{Kind="part",Json=JsonSerializer.Serialize(new TransferPiece{Id=id,Kind=kind,Index=i,Count=count,Data=encoded.Substring(i*24000,Math.Min(24000,encoded.Length-i*24000))},NetJson)},NetJson));
    }
    private void ReceiveNetwork(int peer,string message)
    {
        try{var wire=JsonSerializer.Deserialize<Wire>(message,Catalog.JsonOptions);if(wire==null)return;
            if(wire.Kind=="part"){
                if(!NetClient||peer!=1)return;var part=JsonSerializer.Deserialize<TransferPiece>(wire.Json,Catalog.JsonOptions)!;
                if(part.Id.Length!=32||part.Count<1||part.Count>700||part.Index<0||part.Index>=part.Count||part.Data.Length>24000)throw new InvalidDataException("同步分片无效");
                if(!incomingTransfers.TryGetValue(part.Id,out var transfer)){if(incomingTransfers.Count>=4)throw new InvalidDataException("同步队列已满");transfer=new(){Kind=part.Kind,Pieces=new string[part.Count],Started=roomClock};incomingTransfers.Add(part.Id,transfer);}
                if(transfer.Kind!=part.Kind||transfer.Pieces.Length!=part.Count)throw new InvalidDataException("同步分片冲突");
                if(transfer.Pieces[part.Index]!=null)return;transfer.Pieces[part.Index]=part.Data;transfer.Bytes+=part.Data.Length;
                if(transfer.Pieces.Any(p=>p==null))return;incomingTransfers.Remove(part.Id);using var input=new MemoryStream(Convert.FromBase64String(string.Concat(transfer.Pieces)));using var zip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();var buffer=new byte[8192];int n;while((n=zip.Read(buffer))>0){if(output.Length+n>32*1024*1024)throw new InvalidDataException("同步数据超过上限");output.Write(buffer,0,n);}HandleNetwork(peer,transfer.Kind,Encoding.UTF8.GetString(output.ToArray()));
            }else HandleNetwork(peer,wire.Kind,wire.Json);
        }catch(Exception e){GD.PushWarning("联机消息处理失败："+e.Message);if(NetClient)NetworkDisconnected("同步失败："+e.Message);else if(roomPlayers.ContainsKey(peer))SendNet(peer,"notice","操作失败："+e.Message);}
    }
    private void HandleNetwork(int peer,string kind,string json)
    {
        if(NetHost){
            if(kind=="hello"){AcceptGuest(peer,JsonSerializer.Deserialize<Hello>(json,Catalog.JsonOptions)!);return;}
            if(!roomPlayers.TryGetValue(peer,out var guest))return;
            if(kind=="motion"){ApplyGuestMotion(guest,JsonSerializer.Deserialize<MotionInput>(json,Catalog.JsonOptions)!);return;}
            if(kind=="action"){ApplyGuestAction(guest,JsonSerializer.Deserialize<GameAction>(json,Catalog.JsonOptions)!);return;}
            if(kind=="leave"){RemoveGuest(peer);return;}return;
        }
        if(!NetClient||peer!=1)return;
        switch(kind){
            case "room":joiningWorld=JsonSerializer.Deserialize<string>(json)!;SendNet(1,"hello",new Hello{Fingerprint=networkFingerprint,Id=networkIdentity.Id,Nickname=networkNickname,Appearance=networkAppearance,Token=networkIdentity.Credentials.GetValueOrDefault(joiningWorld)??""});break;
            case "welcome":ApplyWelcome(JsonSerializer.Deserialize<Welcome>(json,Catalog.JsonOptions)!);break;
            case "frame":ApplyWorldFrame(JsonSerializer.Deserialize<WorldFrame>(json,Catalog.JsonOptions)!);break;
            case "roster":ApplyRoster(JsonSerializer.Deserialize<List<AvatarPose>>(json,Catalog.JsonOptions)!);break;
            case "menu":ApplyGuestMenu(JsonSerializer.Deserialize<MenuUpdate>(json,Catalog.JsonOptions)!);break;
            case "notice":Toast(JsonSerializer.Deserialize<string>(json)!);break;
            case "creature-hit":ShowCreatureHit(JsonSerializer.Deserialize<CreatureHitNotice>(json,Catalog.JsonOptions)!);break;
            case "reject":case "closed":NetworkDisconnected(JsonSerializer.Deserialize<string>(json)!);break;
        }
    }
    private void AcceptGuest(int peer,Hello hello)
    {
        if(world==null||roomPlayers.ContainsKey(peer))return;
        if(hello.Protocol!=1||hello.Fingerprint!=networkFingerprint){SendNet(peer,"reject","游戏版本或内容不同，请使用同一个联机试玩包");return;}
        if(hello.Id.Length!=32||!hello.Id.All(Uri.IsHexDigit)||!CharacterAppearances.Names.ContainsKey(hello.Appearance)){SendNet(peer,"reject","玩家身份或外观无效");return;}
        if(roomPlayers.Values.Any(p=>p.State.Id==hello.Id)){SendNet(peer,"reject","这个角色已经在线");return;}
        MultiplayerPlayerState state;
        if(world.State.Players.TryGetValue(hello.Id,out var existing)){if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(existing.ReconnectToken),Encoding.UTF8.GetBytes(hello.Token))){SendNet(peer,"reject","角色重连凭据不匹配，请保留原来的玩家身份文件");return;}state=existing;state.Nickname=CleanNickname(hello.Nickname);state.AppearanceId=hello.Appearance;}
        else state=MultiplayerPlayers.Create(world.State,hello.Id,CleanNickname(hello.Nickname),hello.Appearance);
        var actor=new Explorer{NetworkReplica=true,AcceptUserInput=false,Enabled=true,Model=world,TerrainReady=renderer!.IsTerrainReady,Hurt=_=>{}};worldRoot!.AddChild(actor);actor.SetReplicaPose(ToVector(state.Position),0,0,false);actor.AddCollisionExceptionWith(player!);player!.AddCollisionExceptionWith(actor);
        foreach(var other in RoomActors()){actor.AddCollisionExceptionWith(other);other.AddCollisionExceptionWith(actor);}
        var hidden=new Control{Visible=false};AddChild(hidden);hidden.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);var guest=new RoomPlayer{Peer=peer,State=state,Actor=actor,UiRoot=hidden,Selected=state.SelectedHotbar,LastMotionTime=roomClock};roomPlayers.Add(peer,guest);
        var snapshot=world.State.Copy();snapshot.Players.Clear();state.Apply(snapshot);snapshot.Cursor=null; // only the guest's personal state crosses this connection
        guest.KnownChanges=new(world.State.Changes);SendNet(peer,"welcome",new Welcome{Snapshot=snapshot,Id=state.Id,Token=state.ReconnectToken,Roster=CaptureRoster()});
        SetupNetworkSimulation();RequestSave();Toast(state.Nickname+"加入了世界");
    }
    private void ApplyWelcome(Welcome welcome)
    {
        receivingInitial=true;try{clientPlayerId=welcome.Id;networkIdentity.Credentials[welcome.Snapshot.WorldId]=welcome.Token;SaveNetworkIdentity();StartWorld(welcome.Snapshot);if(world==null)throw new InvalidDataException("世界无法加载");selected=world.State.SelectedHotbar;foreach(var c in creatures){c.NetworkReplica=true;c.ApplyReplicaState(c.State);c.SetPhysicsProcess(false);}projectiles!.SetPhysicsProcess(false);ApplyRoster(welcome.Roster);Toast("已加入 "+world.State.Name+" · 外观："+CharacterAppearances.Name(networkAppearance));}finally{receivingInitial=false;}
    }
    private List<AvatarPose> CaptureRoster(bool includeEquipment=true)
    {
        if(world==null||player==null)return [];
        var list=new List<AvatarPose>{new(){Id="host",Nickname=networkNickname,Appearance=networkAppearance,Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z],Yaw=player.Rotation.Y,Pitch=player.Camera.Rotation.X,Crouching=player.Crouching,Equipment=includeEquipment?world.State.Equipment.Copy():null}};
        list.AddRange(roomPlayers.Values.Select(p=>new AvatarPose{Id=p.State.Id,Nickname=p.State.Nickname,Appearance=p.State.AppearanceId,Position=p.State.Position,Yaw=p.Actor.Rotation.Y,Pitch=p.Actor.Camera.Rotation.X,Crouching=p.Actor.Crouching,Equipment=includeEquipment?p.State.Equipment:null}));return list;
    }
    private static Vector3 ToVector(double[] p)=>new((float)p[0],(float)p[1],(float)p[2]);
    private IReadOnlyList<MapPlayerMarker> CaptureMapRoster(string selfId,Explorer localPlayer)
    {
        var markers=new List<MapPlayerMarker>();
        if(NetHost){
            if(hostActor!=null&&GodotObject.IsInstanceValid(hostActor))markers.Add(new("host",networkNickname,hostActor.GlobalPosition,hostActor.Rotation.Y));
            foreach(var guest in roomPlayers.Values)if(GodotObject.IsInstanceValid(guest.Actor))markers.Add(new(guest.State.Id,guest.State.Nickname,guest.Actor.GlobalPosition,guest.Actor.Rotation.Y));
        }else if(NetClient){
            foreach(var pair in remoteAvatars)if(GodotObject.IsInstanceValid(pair.Value))markers.Add(new(pair.Key,pair.Value.Nickname,pair.Value.LatestPosition,pair.Value.LatestYaw));
        }
        markers.RemoveAll(m=>m.Id==selfId);
        if(GodotObject.IsInstanceValid(localPlayer))markers.Add(new(selfId,roomPlayers.Values.FirstOrDefault(p=>p.State.Id==selfId)?.State.Nickname??networkNickname,localPlayer.GlobalPosition,localPlayer.Rotation.Y,true));
        return markers;
    }
    private void ApplyRoster(List<AvatarPose> roster)
    {
        if(worldRoot==null)return;string self=NetHost?"host":clientPlayerId;var keep=new HashSet<string>();
        foreach(var pose in roster){if(pose.Id==self||pose.Position.Length!=3)continue;keep.Add(pose.Id);if(!remoteAvatars.TryGetValue(pose.Id,out var avatar)){avatar=new(){Catalog=catalog,Nickname=pose.Nickname,AppearanceId=pose.Appearance,WorldLight=p=>renderer!.ItemLightAt(p)};worldRoot.AddChild(avatar);remoteAvatars.Add(pose.Id,avatar);}avatar.Nickname=pose.Nickname;avatar.AppearanceId=pose.Appearance;avatar.UpdateState(ToVector(pose.Position),pose.Yaw,pose.Pitch,pose.Crouching,pose.Equipment);}
        foreach(var id in remoteAvatars.Keys.Where(id=>!keep.Contains(id)).ToArray()){remoteAvatars[id].QueueFree();remoteAvatars.Remove(id);}
    }
    private void ApplyGuestMotion(RoomPlayer guest,MotionInput input)
    {
        if(input.Sequence<=guest.LastMotion||input.Position.Length!=3||input.Position.Any(p=>!double.IsFinite(p))||!float.IsFinite(input.Yaw)||!float.IsFinite(input.Pitch)||input.Selected<0||input.Selected>8)return;
        if(guest.Actor.Mount is {} mounted&&input.MountId!=mounted.State.Id){WithGuest(guest,()=>player!.Dismount());guest.ForcePosition=true;return;}
        guest.LastMotion=input.Sequence;var next=Explorer.KeepInsideWorld(ToVector(input.Position));var previous=guest.Actor.GlobalPosition;double dt=Math.Clamp(roomClock-guest.LastMotionTime,.01,.3);guest.LastMotionTime=roomClock;
        if(guest.ForcePosition&&previous.DistanceTo(next)>1)return;
        float maxStep=(float)(dt*(world!.State.Creative?28:20)+.75);float horizontal=new Vector2(next.X-previous.X,next.Z-previous.Z).Length();float vertical=Math.Abs(next.Y-previous.Y);bool blocked=false;float distance=previous.DistanceTo(next);int steps=Math.Max(1,(int)Math.Ceiling(distance/.2));
        if(horizontal<=maxStep&&vertical<=dt*80+1&&steps<=150)for(int i=1;i<=steps;i++){var at=previous.Lerp(next,i/(float)steps);if(world.Solid(Explorer.ToCell(at+Vector3.Up*.4f))||world.Solid(Explorer.ToCell(at+Vector3.Up*(input.Crouching?.9f:1.5f)))){blocked=true;break;}}
        if(horizontal>maxStep||vertical>dt*80+1||blocked||steps>150||next.Y<-4||next.Y>Coordinates.WorldHeight+64){guest.ForcePosition=true;return;}
        guest.Actor.SetReplicaPose(next,input.Yaw,Math.Clamp(input.Pitch,-1.53f,1.53f),input.Crouching);guest.State.Position=[next.X,next.Y,next.Z];guest.Selected=input.Selected;guest.State.SelectedHotbar=input.Selected;guest.Blocking=input.Blocking;
        guest.Actor.SetReplicaMovementFlags(input.Flying&&world.State.Creative,input.Gliding&&guest.State.Equipment.Slots[1]?.Id=="elytra");
        if(guest.Actor.Mount is {} horse){horse.GlobalPosition=next-Vector3.Up*1.15f;horse.State.Position=[horse.GlobalPosition.X,horse.GlobalPosition.Y,horse.GlobalPosition.Z];}
    }
    private bool SendAction(string kind,double damage=0)
    {
        if(!NetClient||receivingInitial)return false;SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind=kind,Selected=selected,Rotation=rotation,Damage=damage,MineYaw=kind=="mine"?player?.Rotation.Y:null,MinePitch=kind=="mine"?player?.Camera.Rotation.X:null});return true;
    }
    private void StopClientMining()
    {
        if(clientMiningActive&&NetClient)SendAction("mine-stop");clientMiningActive=false;clientMineClock=0;miningCell=null;miningToolId=null;mineProgress=0;
    }
    private void ApplyGuestAction(RoomPlayer guest,GameAction action)
    {
        if(action.Sequence<=guest.LastCommand||action.Selected<0||action.Selected>8||action.Rotation<0||action.Rotation>3)return;
        if(action.Kind=="mine"&&(action.MineYaw is {} yaw&&!float.IsFinite(yaw)||action.MinePitch is {} pitch&&(!float.IsFinite(pitch)||Math.Abs(pitch)>1.53f)))return;
        guest.LastCommand=action.Sequence;if(roomClock-guest.CommandWindow>=1){guest.CommandWindow=roomClock;guest.CommandsInWindow=0;}if(++guest.CommandsInWindow>100){SendNet(guest.Peer,"notice","操作过于频繁，请稍候");return;}guest.LastCommandTime=roomClock;
        WithGuest(guest,()=>{
            selected=action.Selected;rotation=action.Rotation;
            if(action.Kind.StartsWith("ui-",StringComparison.Ordinal)&&currentContainer is {} container&&player!.GlobalPosition.DistanceTo(new(container.X+.5f,container.Y+.5f,container.Z+.5f))>6){ReturnCraftingItems();currentContainer=null;SetScreen("play");Toast("已离开设施，操作取消");return;}
            switch(action.Kind){
                case "mine":if(guest.Screen=="play"){
                    if(action.MineYaw is {} aimYaw&&action.MinePitch is {} aimPitch)guest.Actor.SetReplicaPose(guest.Actor.GlobalPosition,Mathf.Wrap(aimYaw,-Mathf.Pi,Mathf.Pi),aimPitch,guest.Actor.Crouching);
                    double elapsed=roomClock-guest.LastMineTime;var target=player!.Target();bool fresh=miningCell!=target?.Hit||miningToolId!=Held()?.Id||elapsed>GuestMiningTimeout;
                    if(elapsed>GuestMiningTimeout){miningCell=null;miningToolId=null;mineProgress=0;}
                    guest.LastMineTime=roomClock;Mine(fresh?0:Math.Clamp(elapsed,0,.15),target);
                }break;
                case "mine-stop":miningCell=null;miningToolId=null;mineProgress=0;guest.LastMineTime=roomClock;break;
                case "use":if(guest.Screen=="play")Use();break;
                case "interact":if(guest.Screen=="play")Interact();break;
                case "drop":if(guest.Screen=="play")DropSelected();break;
                case "swap":if(guest.Screen=="play")SwapHands();break;
                case "inventory":currentContainer=null;ShowInventory();break;
                case "craft":currentStation="hand";ShowRecipes();break;
                case "close":ReturnCraftingItems();SetScreen("play");currentContainer=null;break;
                case "fall":if(double.IsFinite(action.Damage)&&action.Damage>0&&action.Damage<=1000)Hurt(action.Damage);break;
                case "ui-click":if(action.UiRevision==guest.UiRevision&&guest.UiControls.TryGetValue(action.Token,out var control)&&control is Button button&&!button.Disabled){bool shift=Input.IsActionPressed("inventory_transfer");try{if(action.Shift)Input.ActionPress("inventory_transfer");else Input.ActionRelease("inventory_transfer");if(action.Half)button.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Right,Pressed=true});else button.EmitSignal(BaseButton.SignalName.Pressed);}finally{if(shift)Input.ActionPress("inventory_transfer");else Input.ActionRelease("inventory_transfer");}}break;
                case "ui-drag":if(action.UiRevision==guest.UiRevision&&guest.UiControls.TryGetValue(action.Token,out var source)&&GodotObject.IsInstanceValid(source)&&source is SlotButton from&&!from.Disabled&&from.DragAllowed?.Invoke()!=false&&guest.UiControls.TryGetValue(action.Token2,out var destination)&&GodotObject.IsInstanceValid(destination)&&destination is SlotButton to&&!to.Disabled&&world!.State.Cursor==null)TransferSlots(from.SurfaceIndex,from.SlotIndex,to.SurfaceIndex,to.SlotIndex);break;
                case "ui-text":if(action.UiRevision==guest.UiRevision&&action.Text.Length<=200&&guest.UiControls.TryGetValue(action.Token,out var edit)&&GodotObject.IsInstanceValid(edit)&&edit is LineEdit line&&line.Editable){line.Text=action.Text;line.EmitSignal(LineEdit.SignalName.TextSubmitted,action.Text);}break;
                case "ui-toggle":if(action.UiRevision==guest.UiRevision&&guest.UiControls.TryGetValue(action.Token,out var check)&&GodotObject.IsInstanceValid(check)&&check is CheckButton toggle&&!toggle.Disabled)toggle.ButtonPressed=action.Toggle;break;
            }
        });SendWorldFrame(guest);if(guest.UiDirty)SendGuestMenu(guest);
    }
    private void WithGuest(RoomPlayer guest,Action action)
    {
        if(world==null||player==null||executingGuest!=null)return;
        var local=MultiplayerPlayerState.CaptureLive(world.State);var localPlayer=player;var localOverlay=overlay;var localScreen=screen;int localSelected=selected,localRotation=rotation,localTutorial=tutorial;var localContainer=currentContainer;var localStation=currentStation;var localWorkbench=workbenchCrafting;var localRecipe=selectedWorkbenchRecipe;var localMining=miningCell;var localTool=miningToolId;double localProgress=mineProgress,localCooldown=attackCooldown;
        var localFishing=fishingWater;var localSupply=fishingSupply;var localFloat=floatView;double localBite=fishBite,localExpires=fishExpires;int localFishingSlot=fishingSlot;var localCursorGlyph=cursorGlyph;string localQuery=recipeQuery;int localPage=recipePage;bool localCraftable=craftableOnly;var actorBefore=guest.Actor.GlobalPosition;
        string localMessage=message;double localNotice=noticeTime;
        executingGuest=guest;world.ActivePlayerId=guest.State.Id;guest.State.ApplyLive(world.State);player=guest.Actor;overlay=guest.UiRoot;screen=guest.Screen;selected=guest.Selected;rotation=guest.Rotation;tutorial=guest.Tutorial;currentContainer=guest.Container==null?null:Cell.Parse(guest.Container);currentStation=guest.Station;workbenchCrafting=guest.Workbench;selectedWorkbenchRecipe=guest.WorkbenchRecipe;miningCell=guest.MiningCell;miningToolId=guest.MiningTool;mineProgress=guest.MiningProgress;attackCooldown=guest.AttackCooldown;
        fishingWater=guest.FishingWater;fishingSupply=guest.FishingSupply;floatView=guest.FloatView;fishBite=guest.FishBite;fishExpires=guest.FishExpires;fishingSlot=guest.FishingSlot;
        cursorGlyph=guest.CursorGlyph;recipeQuery=guest.RecipeQuery;recipePage=guest.RecipePage;craftableOnly=guest.CraftableOnly;
        try{action();}finally{
            world.State.SelectedHotbar=selected;guest.State.CaptureLiveFrom(world.State);guest.Screen=screen;guest.Selected=selected;guest.Rotation=rotation;guest.Tutorial=tutorial;guest.Container=currentContainer?.ToString();guest.Station=currentStation;guest.Workbench=workbenchCrafting;guest.WorkbenchRecipe=selectedWorkbenchRecipe;guest.MiningCell=miningCell;guest.MiningTool=miningToolId;guest.MiningProgress=mineProgress;guest.AttackCooldown=attackCooldown;
            guest.FishingWater=fishingWater;guest.FishingSupply=fishingSupply;guest.FloatView=floatView;guest.FishBite=fishBite;guest.FishExpires=fishExpires;guest.FishingSlot=fishingSlot;
            guest.CursorGlyph=cursorGlyph;guest.RecipeQuery=recipeQuery;guest.RecipePage=recipePage;guest.CraftableOnly=craftableOnly;
            if(guest.Actor.GlobalPosition.DistanceSquaredTo(actorBefore)>.01){guest.ForcePosition=true;guest.CorrectionMotion=guest.LastMotion;}
            if(message!=localMessage)SendNet(guest.Peer,"notice",message);
            local.ApplyLive(world.State);world.ActivePlayerId="host";player=localPlayer;overlay=localOverlay;screen=localScreen;selected=localSelected;rotation=localRotation;tutorial=localTutorial;currentContainer=localContainer;currentStation=localStation;workbenchCrafting=localWorkbench;selectedWorkbenchRecipe=localRecipe;miningCell=localMining;miningToolId=localTool;mineProgress=localProgress;attackCooldown=localCooldown;
            fishingWater=localFishing;fishingSupply=localSupply;floatView=localFloat;fishBite=localBite;fishExpires=localExpires;fishingSlot=localFishingSlot;cursorGlyph=localCursorGlyph;recipeQuery=localQuery;recipePage=localPage;craftableOnly=localCraftable;message=localMessage;noticeTime=localNotice;executingGuest=null;
        }
    }
    private void SendGuestMenu(RoomPlayer guest)
    {
        guest.UiDirty=false;guest.UiRevision++;RemoteUiDocument? document=null;if(guest.Screen!="play")WithGuest(guest,()=>document=RemoteUi.Capture(guest.UiRoot,guest.UiControls));SendNet(guest.Peer,"menu",new MenuUpdate{Screen=guest.Screen,Revision=guest.UiRevision,Document=document});
    }
    private void ApplyGuestMenu(MenuUpdate menu)
    {
        if(world==null)return;if(menu.Screen=="play"){SetScreen("play");return;}SetScreen("remote-ui");
        if(menu.Document==null)return;
        RemoteUi.Render(overlay,menu.Document,catalog,(token,half,shift)=>SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-click",Token=token,UiRevision=menu.Revision,Half=half,Shift=shift,Selected=selected,Rotation=rotation}),(token,text)=>SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-text",Token=token,UiRevision=menu.Revision,Text=text,Selected=selected,Rotation=rotation}),(token,value)=>SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-toggle",Token=token,UiRevision=menu.Revision,Toggle=value,Selected=selected,Rotation=rotation}),(source,target)=>SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-drag",Token=source,Token2=target,UiRevision=menu.Revision,Selected=selected,Rotation=rotation}));
    }
    private void SendWorldFrame(RoomPlayer guest)
    {
        if(world==null)return;var state=world.State;var changes=new Dictionary<string,string?>();foreach(var p in state.Changes)if(!guest.KnownChanges.TryGetValue(p.Key,out var old)||old!=p.Value)changes[p.Key]=p.Value;foreach(var key in guest.KnownChanges.Keys)if(!state.Changes.ContainsKey(key))changes[key]=null;guest.KnownChanges=new(state.Changes);
        var own=guest.State.Copy();own.ReconnectToken="private";
        SendNet(guest.Peer,"frame",new WorldFrame{Sequence=++frameSequence,Player=own,ForcePosition=guest.ForcePosition,Changes=changes,Entities=state.Entities,Graves=state.Graves,Creatures=state.Creatures,Projectiles=state.Projectiles,Beacons=state.Beacons,Mainline=state.Mainline,Weather=state.Weather,Time=state.Time,DayTime=state.DayTime,Creative=state.Creative,Casual=state.Casual,GuardianDefeated=state.GuardianDefeated,Roster=CaptureRoster(),MountId=guest.Actor.Mount?.State.Id??"",GlideBoost=guest.Actor.GlideBoost,Impulse=[guest.Actor.Impact.X,guest.Actor.Impact.Y,guest.Actor.Impact.Z],MiningCell=guest.MiningCell?.ToString(),MiningTool=guest.MiningTool,MiningProgress=guest.MiningProgress});guest.ForcePosition=false;guest.Actor.Impact=Vector3.Zero;
    }
    private void ApplyWorldFrame(WorldFrame frame)
    {
        if(world==null||player==null||frame.Sequence<=lastNetworkFrame)return;lastNetworkFrame=frame.Sequence;var state=world.State;
        var previousEntities=state.Entities;state.Entities=frame.Entities;
        foreach(var pair in frame.Changes){var p=Cell.Parse(pair.Key);world.Set(p,pair.Value==null?world.NaturalBlock(p):pair.Value=="air"?(ushort)0:catalog.Number(pair.Value));renderer!.RefreshEdit(p);}
        foreach(var pair in frame.Entities)if(pair.Value.Kind=="door"&&(!previousEntities.TryGetValue(pair.Key,out var old)||old.Open!=pair.Value.Open)){var p=Cell.Parse(pair.Key);world.Set(p,world.Get(p));renderer!.RefreshEdit(p);}
        var position=player.GlobalPosition;frame.Player.Validate(catalog);frame.Player.Apply(state);state.Time=frame.Time;state.DayTime=frame.DayTime;state.Weather=frame.Weather;state.Graves=frame.Graves;state.Beacons=frame.Beacons;state.Mainline=frame.Mainline;state.Creative=frame.Creative;state.Casual=frame.Casual;state.GuardianDefeated=frame.GuardianDefeated;
        if(frame.ForcePosition){player.GlobalPosition=ToVector(frame.Player.Position);player.Velocity=Vector3.Zero;}else state.Position=[position.X,position.Y,position.Z];
        var ids=frame.Creatures.Select(c=>c.Id).ToHashSet();foreach(var c in creatures.Where(c=>!ids.Contains(c.State.Id)).ToArray()){c.QueueFree();creatures.Remove(c);}state.Creatures=frame.Creatures;
        foreach(var saved in frame.Creatures){var existing=creatures.FirstOrDefault(c=>c.State.Id==saved.Id);if(existing!=null)existing.ApplyReplicaState(saved);else{SpawnCreature(saved);var added=creatures.FirstOrDefault(c=>c.State.Id==saved.Id);if(added!=null){added.NetworkReplica=true;added.ApplyReplicaState(saved);added.SetPhysicsProcess(false);}}}
        state.Projectiles=frame.Projectiles;projectiles!.ApplyReplicaSnapshot(frame.Projectiles);
        var mount=creatures.FirstOrDefault(c=>c.State.Id==frame.MountId);if(player.Mount!=mount){if(player.Mount is {} old){old.Mounted=false;old.RemoveCollisionExceptionWith(player);player.RemoveCollisionExceptionWith(old);}player.Mount=mount;if(mount!=null){mount.Mounted=true;mount.AddCollisionExceptionWith(player);player.AddCollisionExceptionWith(mount);}}
        player.GlideBoost=Math.Max(player.GlideBoost,frame.GlideBoost);player.Impact+=ToVector(frame.Impulse);
        if(clientMiningActive&&miningCell?.ToString()==frame.MiningCell&&miningToolId==frame.MiningTool&&double.IsFinite(frame.MiningProgress))mineProgress=Math.Clamp(frame.MiningProgress,0,.99);
        ApplyRoster(frame.Roster);
    }
    private void TickNetworking(double delta)
    {
        roomClock+=delta;if(network==null)return;
        foreach(var key in incomingTransfers.Keys.Where(k=>roomClock-incomingTransfers[k].Started>30).ToArray())incomingTransfers.Remove(key);
        if(NetClient&&world==null){clientWaitClock+=delta;if(clientWaitClock>60)NetworkDisconnected("连接或同步超时，请检查房主地址、端口与防火墙");}
        if(world==null||player==null)return;
        if(NetClient){networkSendClock+=delta;if(networkSendClock>=.05){networkSendClock=0;SendNet(1,"motion",new MotionInput{Sequence=++clientMotion,Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z],Yaw=player.Rotation.Y,Pitch=player.Camera.Rotation.X,Crouching=player.Crouching,Blocking=screen=="play"&&Input.IsActionPressed("use"),Selected=selected,MountId=player.Mount?.State.Id??"",Flying=player.Flying,Gliding=player.Gliding},false);}return;}
        if(!NetHost)return;hostActor!.SetReplicaPose(player.GlobalPosition,player.Rotation.Y,player.Camera.Rotation.X,player.Crouching);hostActor.Enabled=screen!="death";
        if(screen!="play"){world.Advance(delta);TickPopulation(delta);TickMainline(delta);saveClock+=delta;if(saveClock>=60){saveClock=0;RequestSave();}}
        foreach(var guest in roomPlayers.Values.ToArray())WithGuest(guest,()=>{
            if(screen=="death")return;
            if(screen!="play"||roomClock-guest.LastMineTime>GuestMiningTimeout){miningCell=null;miningToolId=null;mineProgress=0;}
            attackCooldown=Math.Max(0,attackCooldown-delta);world.RevealMap(player!.GlobalPosition.X,player.GlobalPosition.Z);
            if(world.State.Effects.ShieldUntil<=world.State.Time)world.State.Effects.Shield=0;
            bool underwater=catalog.Id(world.Get(Explorer.ToCell(player.Camera.GlobalPosition)))=="water";world.State.Air=underwater?Math.Max(0,world.State.Air-delta):Math.Min(15,world.State.Air+delta*5);if(underwater&&world.State.Air<=0)Hurt(delta*4);
            if(!world.State.Creative){world.State.Hunger=Math.Max(0,world.State.Hunger-delta*(world.State.Casual?.035:.12));if(world.State.Hunger<=0)Hurt(delta*.8);else if(world.State.Hunger>65)world.State.Health=Math.Min(100,world.State.Health+delta*.6);}
            if(screen=="play")world.PickTouchingItems(world.State.Position,player.Crouching?1.1:1.8);
            if(fishingWater!=null)TickExpansion(delta);
        });
        if(screen!="play")world.TickDroppedItems(delta,p=>renderer!.IsTerrainReady(new(p.X,p.Y,p.Z)));
        if(world.State.Mainline.FinaleActive&&!RoomActors().Any(a=>a.Enabled))ResetMainlineBattle();
        if(networkSaveRequested){networkSaveRequested=false;RequestSave();}
        rosterClock+=delta;if(rosterClock>=.05){rosterClock=0;ApplyRoster(CaptureRoster());var motionRoster=CaptureRoster(false);foreach(var guest in roomPlayers.Values)SendNet(guest.Peer,"roster",motionRoster,false);}
        networkSendClock+=delta;if(networkSendClock>=.2){networkSendClock=0;SetupNetworkSimulation();foreach(var guest in roomPlayers.Values){SendWorldFrame(guest);if(guest.UiDirty)SendGuestMenu(guest);}}
    }
    private void RemoveGuest(int peer)
    {
        if(!roomPlayers.TryGetValue(peer,out var guest))return;
        WithGuest(guest,()=>{ReturnCraftingItems();EndFishing();if(player!.Mount is {} horse){horse.Mounted=false;horse.RemoveCollisionExceptionWith(player);player.RemoveCollisionExceptionWith(horse);player.Mount=null;}});roomPlayers.Remove(peer);ReleaseContainer(peer);guest.Actor.QueueFree();guest.UiRoot.QueueFree();RequestSave();Toast(guest.State.Nickname+"离开了世界");
    }
    private void ReleaseContainer(int peer){foreach(var key in containerOwners.Where(p=>p.Value==peer).Select(p=>p.Key).ToArray())containerOwners.Remove(key);}
    private bool TryLockContainer(Cell p)
    {
        if(!NetHost)return true;int peer=executingGuest?.Peer??1;string key=p.ToString();if(containerOwners.TryGetValue(key,out var owner)&&owner!=peer){Toast("另一位玩家正在使用这个容器");return false;}ReleaseContainer(peer);containerOwners[key]=peer;return true;
    }
    private void CaptureNetworkSave()
    {
        if(world==null||NetClient||RemoteContext)return;world.State.SelectedHotbar=selected;MultiplayerPlayers.CaptureHost(world.State,networkNickname,networkAppearance);
        if(NetHost)foreach(var guest in roomPlayers.Values)if(guest.Workbench.Slots.Any(s=>s!=null))WithGuest(guest,()=>{ReturnWorkbenchItems();if(screen=="workbench")ShowWorkbench();});
    }
    private void StopNetworking()
    {
        if(network==null||networkStopping)return;
        bool wasHost=NetHost;networkStopping=true;
        clientMiningActive=false;clientMineClock=0;
        try{
            if(wasHost){foreach(var guest in roomPlayers.Values.ToArray()){SendNet(guest.Peer,"closed","房主已关闭联机");RemoveGuest(guest.Peer);}}
            else if(NetClient&&network.Connected)SendNet(1,"leave",true);
            network.Close();foreach(var avatar in remoteAvatars.Values)avatar.QueueFree();remoteAvatars.Clear();
            if(hostActor!=null){hostActor.QueueFree();hostActor=null;}
            containerOwners.Clear();incomingTransfers.Clear();roomPlayers.Clear();
            if(world!=null)world.MultiplayerActive=false;hostSleeping=false;
            if(wasHost&&world!=null&&player!=null&&GodotObject.IsInstanceValid(player)){
                foreach(var creature in creatures){var c=creature;c.SelectTarget=null;c.Player=player;c.AttackPlayer=damage=>HurtFrom(damage,c.GlobalPosition);}
                if(projectiles!=null){projectiles.Players=null;projectiles.HitAnyPlayer=null;projectiles.Player=player;
                    projectiles.HitPlayer=(damage,origin)=>{projectileImpact=true;try{HurtFrom(damage,origin);}finally{projectileImpact=false;}};}
            }
        }finally{networkStopping=false;}
    }
    private void NetworkDisconnected(string reason)
    {
        if(quitting)return;
        StopNetworking();ClearWorldView();ShowMenu();Toast(reason);
    }
    private bool RoomSleep(Cell bed)
    {
        if(!NetHost)return false;world!.State.Respawn=[bed.X+.5,bed.Y+.7,bed.Z+.5];if(!DayCycle.Night(world.State.SkyTime)){Toast("已设置复活点");return true;}
        if(executingGuest!=null)executingGuest.Sleeping=true;else hostSleeping=true;
        if(hostSleeping&&roomPlayers.Values.All(p=>p.Sleeping)){world.Advance(DayCycle.SecondsUntilBoundary(world.State.SkyTime)+240);hostSleeping=false;foreach(var p in roomPlayers.Values)p.Sleeping=false;Toast("所有玩家已休息，天亮了");RequestSave();}else Toast("等待其他在线玩家使用床");return true;
    }
    private bool hostSleeping;
    private void DeferredGuestLeave(int peer)=>RemoveGuest(peer);
}
