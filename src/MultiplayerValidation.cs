using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Networking;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool multiplayerCheck;
    private readonly Stopwatch multiplayerWatch=new();
    private readonly List<string> multiplayerAssertions=[];
    private string multiplayerRole="",multiplayerFolder="",multiplayerStage="setup";
    private int multiplayerPhase,multiplayerPort=NetworkSession.DefaultPort;
    private bool multiplayerFinished,multiplayerSaved;
    private double multiplayerCommandAt;
    private double multiplayerDebugClock;
    private string multiplayerDebugStage="";
    private int multiplayerOriginalPeer;
    private int validationMiningHostIndex,validationMiningClientIndex,validationMiningClientPhase;
    private double validationMiningHostStarted,validationMiningClientStarted;
    private bool validationMiningSawPartial,validationMiningSawSwitch;
    private Inventory? validationMiningOriginalInventory;
    private EmberLighthouse.Creatures.Creature? validationHitCreature;
    private bool validationHitSent;
    private int validationHitCount;
    private sealed record MiningFixture(string Name,Cell Cell,string Block,int Tool,string Mode="break",Cell? Alternate=null);
    private static readonly MiningFixture[] ValidationMiningFixtures=[
        new("bare hands harvest placed wood",new(6,111,10),"wood",8),
        new("wood pick harvests stone",new(10,111,10),"stone",5),
        new("iron pick harvests iron ore",new(11,111,11),"iron_ore",7),
        new("target change resets partial stroke",new(5,111,10),"stone",5,"switch",new(9,111,11)),
        new("stop and resume starts new stroke",new(12,111,10),"dirt",8,"pause"),
        new("friend mines while host inventory is open",new(6,111,6),"dirt",8,"menu"),
        new("wrong tool gives explicit rejection",new(10,111,11),"stone",8,"wrong"),
        new("out of reach block remains authoritative",new(8,111,17),"wood",8,"range"),
        new("packet burst cannot accelerate mining",new(8,111,11),"dirt",8,"burst")
    ];
    private const string ValidationGuestId="a812d345f0124348a7b1a9176543fabc";
    private const string ValidationObserver1Id="c1111111111111111111111111111111",ValidationObserver2Id="c2222222222222222222222222222222";
    private static readonly Cell ValidationSupport=new(8,111,5),ValidationPlaced=new(8,111,6),ValidationMarker=new(14,110,8);
    private sealed class ValidationMenuOffer {public int Token {get;set;}public int TargetToken {get;set;}public long Revision {get;set;}}

    private void InitializeMultiplayerValidation()
    {
        var args=OS.GetCmdlineUserArgs();multiplayerCheck=args.Contains("--multiplayer-check");if(!multiplayerCheck)return;
        string Arg(string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        multiplayerRole=Arg("--multiplayer-role","host");string run=Arg("--multiplayer-run","manual");
        if(run.Length>80||!run.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("invalid multiplayer run id");
        multiplayerFolder=Path.GetFullPath(Arg("--multiplayer-report-dir",ProjectSettings.GlobalizePath("res://test-results/multiplayer-check/"+run)));Directory.CreateDirectory(multiplayerFolder);multiplayerWatch.Start();
        try{
            var zeroVitals=new MultiplayerPlayerState{Hunger=0,Air=0};var decodedVitals=JsonSerializer.Deserialize<MultiplayerPlayerState>(JsonSerializer.Serialize(zeroVitals,NetJson),Catalog.JsonOptions)!;
            RequireMultiplayer(decodedVitals.Hunger==0&&decodedVitals.Air==0,"network preserves zero hunger and air rather than constructor defaults");
            var hiddenControl=new RemoteUiNode{Visible=false};var decodedControl=JsonSerializer.Deserialize<RemoteUiNode>(JsonSerializer.Serialize(hiddenControl,NetJson),Catalog.JsonOptions)!;
            RequireMultiplayer(!decodedControl.Visible,"network preserves explicitly hidden UI controls");
            multiplayerPort=int.Parse(Arg("--multiplayer-port",NetworkSession.DefaultPort.ToString()));
            settings.ViewDistance=2;networkNickname=multiplayerRole switch{"host"=>"验证房主","observer1"=>"观察者一","observer2"=>"观察者二",_=>"验证朋友"};networkAppearance=multiplayerRole switch{"host"=>"ranger","observer1"=>"frost","observer2"=>"ranger",_=>"artisan"};
            string identity=multiplayerRole switch{"host"=>"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","observer1"=>ValidationObserver1Id,"observer2"=>ValidationObserver2Id,_=>ValidationGuestId};
            networkIdentity=new(){Id=identity,Nickname=networkNickname,Appearance=networkAppearance};identityPath=Path.Combine(multiplayerFolder,multiplayerRole+"-identity.json");
            if(multiplayerRole=="host"){
                StartWorld(new(){Name="虚拟局域网联机运行时验证",Seed=42,Creative=true,Time=300,Position=[12.5,110,8.5],Respawn=[8.5,110,8.5],Population=new(){Initialized=true,NextSpawnTime=100000}});
                RequireMultiplayer(world!=null&&player!=null,"host world created");player!.SetPhysicsProcess(false);
                foreach(var creature in creatures)creature.QueueFree();creatures.Clear();world!.State.Creatures.Clear();
                for(int x=5;x<=15;x++)for(int z=4;z<=11;z++){world.Set(new(x,109,z),catalog.Number("stone"));for(int y=110;y<=114;y++)world.Set(new(x,y,z),0);}
                world.Set(ValidationSupport,catalog.Number("stone"));world.State.Inventory=new();world.State.Inventory.Slots[0]=new("stone",11);world.State.Health=93;world.State.Hunger=50;
                RequireMultiplayer(network!.CreateServer(multiplayerPort)==Error.Ok,"real ENet host listening");MultiplayerPlayers.CaptureHost(world.State,networkNickname,networkAppearance);CreateHostActor();
                WriteMultiplayerEvent("host-ready",new{port=multiplayerPort});multiplayerStage="waiting for guest handshake";
            }else if(multiplayerRole is "client" or "observer1" or "observer2"){
                RequireMultiplayer(network!.Join("127.0.0.1",multiplayerPort)==Error.Ok,"real ENet client connection initiated");multiplayerStage="waiting for welcome";
            }else throw new ArgumentException("role must be host, client, observer1 or observer2");
        }catch(Exception error){FinishMultiplayerValidation(false,error.ToString());}
    }
    private void TickMultiplayerValidation(double delta)
    {
        if(!multiplayerCheck||multiplayerFinished)return;
        try{
            if(multiplayerWatch.Elapsed.TotalSeconds>180)throw new TimeoutException("multiplayer stage: "+multiplayerStage);
            if(player!=null)player.SetPhysicsProcess(false);
            if(multiplayerRole=="host")TickMultiplayerHost();else if(multiplayerRole=="client")TickMultiplayerClient();else TickMultiplayerObserver();
            multiplayerDebugClock+=delta;
            if(!multiplayerFinished&&(multiplayerDebugStage!=multiplayerStage||multiplayerDebugClock>=.5)){
                multiplayerDebugStage=multiplayerStage;multiplayerDebugClock=0;WriteMultiplayerEvent(multiplayerRole+"-progress",MultiplayerDebugSnapshot());
            }
        }catch(Exception error){FinishMultiplayerValidation(false,error.ToString());}
    }
    private void TickMultiplayerHost()
    {
        var guest=roomPlayers.Values.FirstOrDefault(p=>p.State.Id==ValidationGuestId);
        if(multiplayerPhase==3&&!HasMultiplayerEvent("mining-suite-verified")){if(guest!=null)TickValidationMiningHost(guest);return;}
        if(multiplayerPhase==3&&!HasMultiplayerEvent("combat-feedback-verified")){
            if(guest==null)return;multiplayerStage="guest authoritative hit feedback";
            if(validationHitCreature==null){var p=guest.Actor.GlobalPosition+Vector3.Right*2.2f;var state=new EmberLighthouse.Creatures.CreatureState{Kind="pig",Position=[p.X,p.Y,p.Z],Health=creatureDefs.Single(d=>d.Id=="pig").Health};world!.State.Creatures.Add(state);SpawnCreature(state);validationHitCreature=creatures.Single(c=>c.State.Id==state.Id);validationHitCreature.SetPhysicsProcess(false);validationHitCount=combatFeedback.ConfirmedHits;SendWorldFrame(guest);WriteMultiplayerEvent("combat-feedback-ready",new{passed=true});return;}
            if(!HasMultiplayerEvent("client-combat-feedback"))return;
            RequireMultiplayer(validationHitCreature.State.Health==validationHitCreature.Definition.Health-4&&combatFeedback.ConfirmedHits==validationHitCount,"guest hit damages exactly once and does not show host a private hit marker");
            world!.State.Creatures.Remove(validationHitCreature.State);creatures.Remove(validationHitCreature);validationHitCreature.QueueFree();WriteMultiplayerEvent("combat-feedback-verified",new{passed=true});return;
        }
        switch(multiplayerPhase){
            case 0:
                if(guest==null||roomPlayers.Count!=3||!HasMultiplayerEvent("client-joined")||!HasMultiplayerEvent("observer1-joined")||!HasMultiplayerEvent("observer2-joined"))return;
                RequireMultiplayer(remoteAvatars.Count==3&&remoteAvatars.ContainsKey(ValidationObserver1Id)&&remoteAvatars.ContainsKey(ValidationObserver2Id),"four real processes join and host renders three other player models");
                VerifyValidationMapRoster(4);
                RequireMultiplayer(guest.State.AppearanceId=="artisan"&&remoteAvatars.ContainsKey(ValidationGuestId),"host renders selected guest model");multiplayerOriginalPeer=guest.Peer;
                foreach(var observer in roomPlayers.Values.Where(p=>p.State.Id!=ValidationGuestId)){
                    var position=observer.State.Id==ValidationObserver1Id?new Vector3(13.5f,110,10.5f):new Vector3(5.5f,110,11.5f);
                    observer.State.Position=[position.X,position.Y,position.Z];observer.Actor.SetReplicaPose(position,0,0,false);observer.ForcePosition=true;SendWorldFrame(observer);
                }
                guest.State.Inventory=new();guest.State.Inventory.Slots[0]=new("plank",8);guest.State.Inventory.Slots[2]=new("apple",3);guest.State.Health=73;guest.State.Hunger=50;guest.ForcePosition=true;
                world!.State.Creative=false;world.Set(ValidationMarker,catalog.Number("stone"));world.Drop(new EmberLighthouse.Items.Stack("wood",5),guest.State.Position,0);SendWorldFrame(guest);
                multiplayerPhase=1;multiplayerStage="guest pickup and edit replication";break;
            case 1:
                if(guest==null||!HasMultiplayerEvent("client-observed"))return;
                RequireMultiplayer(guest.State.Inventory.Count("wood")==5&&world!.State.Inventory.Count("wood")==0,"pickup credited exclusively to touching guest");
                RequireMultiplayer(world!.State.Inventory.Count("stone")==11&&world.State.Health==93,"guest state preserves host inventory and health");
                WriteMultiplayerEvent("fixture-verified",new{passed=true});multiplayerPhase=2;multiplayerStage="client authoritative placement";break;
            case 2:
                if(guest==null||!HasMultiplayerEvent("client-placed"))return;
                RequireMultiplayer(world!.Get(ValidationPlaced)==catalog.Number("plank")&&guest.State.Inventory.Count("plank")==7,"ENet use places one block with guest cost");
                RequireMultiplayer(world.State.Inventory.Count("plank")==0,"guest placement does not consume host inventory");
                WriteMultiplayerEvent("placement-verified",new{passed=true});multiplayerPhase=3;multiplayerStage="remote inventory menu";break;
            case 3:
                if(guest?.Screen!="inventory"||guest.UiControls.Count==0)return;
                var source=guest.UiControls.FirstOrDefault(p=>p.Value is SlotButton b&&b.SurfaceIndex==0&&b.SlotIndex==0);
                if(source.Value==null)return;WriteMultiplayerEvent("menu-take",new ValidationMenuOffer{Token=source.Key,Revision=guest.UiRevision});multiplayerPhase=4;multiplayerStage="remote slot take callback";break;
            case 4:
                if(guest?.State.Cursor?.Id!="plank"||guest.State.Cursor.Count!=7)return;
                RequireMultiplayer(guest.State.Inventory.Count("plank")==0&&world!.State.Cursor==null,"remote token takes guest stack and isolates cursor");
                RequireMultiplayer(guest.State.Inventory.Slots[3]==null,"remote cursor destination slot is empty before placement");
                var destination=guest.UiControls.FirstOrDefault(p=>p.Value is SlotButton b&&b.SurfaceIndex==0&&b.SlotIndex==3);
                if(destination.Value==null)return;WriteMultiplayerEvent("menu-place",new ValidationMenuOffer{Token=destination.Key,Revision=guest.UiRevision});multiplayerPhase=5;multiplayerStage="remote slot place callback";break;
            case 5:
                if(guest==null||!HasMultiplayerEvent("client-ui-complete"))return;
                if(!HasMultiplayerEvent("drag-out")){RequireMultiplayer(guest.State.Cursor==null&&guest.State.Inventory.Slots[3]?.Id=="plank"&&guest.State.Inventory.Slots[3]?.Count==7,"remote second token puts complete guest stack into destination");OfferValidationDrag(guest,"drag-out",3,4);return;}
                if(!HasMultiplayerEvent("client-drag-out"))return;
                if(!HasMultiplayerEvent("drag-back")){RequireMultiplayer(guest.State.Inventory.Slots[4]?.Count==7&&guest.State.Inventory.Slots[3]==null&&screen=="play","guest drag moves only guest items without opening host inventory");OfferValidationDrag(guest,"drag-back",4,3);return;}
                if(!HasMultiplayerEvent("client-drag-complete"))return;
                RequireMultiplayer(guest.State.Inventory.Slots[3]?.Count==7&&guest.State.Inventory.Slots[4]==null&&world!.State.Inventory.Count("plank")==0,"two authenticated drags conserve guest items and host state");
                WriteMultiplayerEvent("ui-verified",new{passed=true});multiplayerPhase=6;multiplayerStage="disconnect and durable save";break;
            case 6:
                if(guest!=null||!HasMultiplayerEvent("client-disconnected"))return;
                RequireMultiplayer(roomPlayers.Count==2,"other two friends stay connected during guest disconnect");
                VerifyValidationMapRoster(3);RequireMultiplayer(CaptureMapRoster("host",player!).All(m=>m.Id!=ValidationGuestId),"disconnected friend disappears from host map roster");
                multiplayerPhase=7;RequestSave(success=>{if(!success){FinishMultiplayerValidation(false,"host save callback failed");return;}multiplayerSaved=true;});break;
            case 7:
                if(!multiplayerSaved)return;
                var loaded=saves.Load(world!.State.WorldId).Snapshot;
                RequireMultiplayer(loaded.Players.TryGetValue(ValidationGuestId,out var persisted)&&persisted.Inventory.Count("plank")==7&&persisted.Inventory.Count("wood")==5&&persisted.AppearanceId=="artisan"&&persisted.Health==73,"disconnected guest inventory, health and skin survive actual save/load");
                RequireMultiplayer(loaded.Players["host"].Inventory.Count("stone")==11,"host profile saved independently");
                WriteMultiplayerEvent("reconnect-ready",new{passed=true});multiplayerPhase=8;multiplayerStage="authenticated reconnect";break;
            case 8:
                if(guest==null||!HasMultiplayerEvent("client-reconnected"))return;
                RequireMultiplayer(guest.Peer!=multiplayerOriginalPeer&&guest.State.Inventory.Slots[3]?.Count==7&&guest.State.Inventory.Count("wood")==5&&guest.State.AppearanceId=="artisan","new ENet peer restores same authenticated profile");
                RequireMultiplayer(roomPlayers.Count==3,"four-player room restored after reconnect");
                VerifyValidationMapRoster(4);
                WriteMultiplayerEvent("reconnect-verified",new{passed=true});multiplayerPhase=9;multiplayerStage="waiting for four process assertions";break;
            case 9:
                if(!HasMultiplayerEvent("client-passed")||!HasMultiplayerEvent("observer1-passed")||!HasMultiplayerEvent("observer2-passed"))return;
                RequireMultiplayer(network!.ReceivedMessages>0&&network.SentMessages>0,"real bidirectional ENet traffic observed");
                RequireMultiplayer(roomPlayers.Count==3&&network.Connected,"all three clients remain online when host closes the room");
                var probeState=new EmberLighthouse.Creatures.CreatureState{Kind="husk",Health=creatureDefs.Single(d=>d.Id=="husk").Health,
                    Position=[player!.GlobalPosition.X+3,player.GlobalPosition.Y,player.GlobalPosition.Z]};
                SpawnCreature(probeState);var probe=creatures[^1];probe.SetPhysicsProcess(false);
                RequireMultiplayer(probe.SelectTarget!=null,"native creature uses room target selection before room closure");
                var continuedWorld=world;
                StopNetworking();
                RequireMultiplayer(world!=null&&ReferenceEquals(world,continuedWorld)&&player!=null&&screen=="play"&&!InRoom&&!network.Connected,
                    "host closing room retains loaded world and resumes single-player gameplay");
                RequireMultiplayer(roomPlayers.Count==0&&remoteAvatars.Count==0,"host closing room removes all guest actors and models");
                RequireMultiplayer(hostActor==null&&projectiles!=null&&projectiles.Players==null&&projectiles.HitAnyPlayer==null&&ReferenceEquals(projectiles.Player,player),
                    "host room closure restores native projectile targeting to single player");
                RequireMultiplayer(probe.SelectTarget==null&&ReferenceEquals(probe.Player,player),
                    "host room closure restores native creature AI target to local player");
                creatures.Remove(probe);probe.QueueFree();
                WriteMultiplayerEvent("host-room-closed",new{passed=true});multiplayerPhase=10;multiplayerStage="waiting for three client close acknowledgements";break;
            case 10:
                if(!HasMultiplayerEvent("client-closed")||!HasMultiplayerEvent("observer1-closed")||!HasMultiplayerEvent("observer2-closed"))return;
                RequireMultiplayer(world!=null&&screen=="play"&&!network!.Connected,"host remains in single-player world after every guest returns to menu");
                FinishMultiplayerValidation(true,"");break;
        }
    }
    private void TickMultiplayerClient()
    {
        if(multiplayerPhase==5&&!HasMultiplayerEvent("mining-suite-verified")){
            if(HasMultiplayerEvent("placement-verified"))TickValidationMiningClient();
            return;
        }
        if(multiplayerPhase==5&&!HasMultiplayerEvent("combat-feedback-verified")){
            multiplayerStage="guest waiting for actual hit confirmation";
            var pig=creatures.FirstOrDefault(c=>c.Definition.Id=="pig");if(pig==null||!HasMultiplayerEvent("combat-feedback-ready"))return;
            if(!validationHitSent){selected=4;player!.LookAt(new(pig.GlobalPosition.X,player.GlobalPosition.Y,pig.GlobalPosition.Z));player.Camera.LookAt(pig.GlobalPosition+Vector3.Up*.5f);validationHitCount=combatFeedback.ConfirmedHits;SendAction("mine");validationHitSent=true;return;}
            if(combatFeedback.ConfirmedHits<=validationHitCount)return;
            RequireMultiplayer(combatFeedback.Damage==4&&combatFeedback.Health==pig.Definition.Health-4&&combatFeedback.TargetName==pig.Definition.Name,"guest receives reliable authoritative damage and remaining health");WriteMultiplayerEvent("client-combat-feedback",new{passed=true});return;
        }
        switch(multiplayerPhase){
            case 0:
                if(world==null||player==null||clientPlayerId!=ValidationGuestId||remoteAvatars.Count!=3)return;
                RequireMultiplayer(remoteAvatars.ContainsKey("host")&&remoteAvatars.ContainsKey(ValidationObserver1Id)&&remoteAvatars.ContainsKey(ValidationObserver2Id),"active client renders all three other players in four-player room");
                RequireMultiplayer(remoteAvatars["host"].AppearanceId=="ranger"&&remoteAvatars["host"].Nickname=="验证房主","client renders host nickname and selected model");
                VerifyValidationMapRoster(4);
                RequireMultiplayer(world.State.Players.Count==0,"welcome exposes own personal state without other private profiles");
                WriteMultiplayerEvent("client-joined",new{passed=true});multiplayerPhase=1;multiplayerStage="replicated pickup and world edit";break;
            case 1:
                if(world==null||world.State.Inventory.Count("wood")!=5||world.State.Inventory.Count("plank")!=8||world.Get(ValidationMarker)!=catalog.Number("stone"))return;
                RequireMultiplayer(world.State.Health==73&&!world.State.Creative,"guest personal health and room rules replicated");WriteMultiplayerEvent("client-observed",new{passed=true});multiplayerPhase=2;multiplayerStage="waiting for fixture verification";break;
            case 2:
                if(!HasMultiplayerEvent("fixture-verified"))return;
                player!.Camera.LookAt(new(8.5f,111.5f,5.5f));selected=0;
                RequireMultiplayer(player.Target()?.Before==ValidationPlaced,"client camera resolves expected placement cell");
                SendNet(1,"motion",new MotionInput{Sequence=++clientMotion,Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z],Yaw=player.Rotation.Y,Pitch=player.Camera.Rotation.X,Selected=0});multiplayerCommandAt=multiplayerWatch.Elapsed.TotalSeconds;multiplayerPhase=3;multiplayerStage="motion before placement";break;
            case 3:
                if(multiplayerWatch.Elapsed.TotalSeconds-multiplayerCommandAt<.2)return;
                SendAction("use");multiplayerPhase=4;multiplayerStage="awaiting replicated placement";break;
            case 4:
                if(world!.Get(ValidationPlaced)!=catalog.Number("plank")||world.State.Inventory.Count("plank")!=7)return;
                RequireMultiplayer(world.State.Inventory.Count("wood")==5,"placement preserves picked guest items");WriteMultiplayerEvent("client-placed",new{passed=true});multiplayerPhase=5;multiplayerStage="opening remote menu";break;
            case 5:
                if(!HasMultiplayerEvent("placement-verified"))return;
                SendAction("inventory");multiplayerPhase=6;multiplayerStage="waiting for captured inventory menu";break;
            case 6:
                if(screen!="remote-ui"||!HasMultiplayerEvent("menu-take"))return;
                RequireMultiplayer(UiDescendants(overlay).OfType<Button>().Count()>36,"host inventory menu rendered on client");
                RequireMultiplayer(UiDescendants(overlay).OfType<TextureRect>().Any(t=>t.Texture?.ResourcePath=="res://assets/ui/lighthouse-sunset.png"),"remote inventory preserves native lantern background rather than an empty texture control");
                var styledSlot=UiDescendants(overlay).OfType<RemoteSlotButton>().First();var styledBorder=styledSlot.GetThemeStylebox("normal") as StyleBoxFlat;
                RequireMultiplayer(styledBorder!=null&&styledBorder.BorderColor.R>styledBorder.BorderColor.B&&styledBorder.BgColor.R>styledBorder.BgColor.B,"remote inventory retains authoritative gold borders and warm dark slots");
                SendValidationSlot("menu-take");multiplayerPhase=7;multiplayerStage="replicated cursor after remote callback";break;
            case 7:
                if(world!.State.Cursor?.Id!="plank"||world.State.Cursor.Count!=7||!HasMultiplayerEvent("menu-place"))return;
                RequireMultiplayer(world.State.Inventory.Count("plank")==0,"guest cursor state replicated after token click");SendValidationSlot("menu-place");multiplayerPhase=8;multiplayerStage="replicated destination slot";break;
            case 8:
                if(world!.State.Cursor!=null||world.State.Inventory.Slots[3]?.Id!="plank"||world.State.Inventory.Slots[3]?.Count!=7)return;
                RequireMultiplayer(world.State.Inventory.Count("wood")==5&&world.State.Inventory.Count("apple")==3,"remote slot callbacks conserve unrelated items");WriteMultiplayerEvent("client-ui-complete",new{passed=true});multiplayerPhase=20;multiplayerStage="remote inventory drag";break;
            case 20:
                if(!HasMultiplayerEvent("drag-out"))return;SendValidationDrag("drag-out");multiplayerPhase=21;break;
            case 21:
                if(world!.State.Inventory.Slots[4]?.Count!=7||world.State.Inventory.Slots[3]!=null)return;
                RequireMultiplayer(screen=="remote-ui"&&UiDescendants(overlay).OfType<RemoteSlotButton>().Any(),"guest drag returns a usable authoritative inventory menu");WriteMultiplayerEvent("client-drag-out",new{passed=true});multiplayerPhase=22;break;
            case 22:
                if(!HasMultiplayerEvent("drag-back"))return;SendValidationDrag("drag-back");multiplayerPhase=23;break;
            case 23:
                if(world!.State.Inventory.Slots[3]?.Count!=7||world.State.Inventory.Slots[4]!=null)return;
                RequireMultiplayer(world.State.Inventory.Count("wood")==5&&world.State.Inventory.Count("apple")==3&&world.State.Cursor==null,"guest drag round trip conserves all inventory contents");WriteMultiplayerEvent("client-drag-complete",new{passed=true});multiplayerPhase=9;multiplayerStage="disconnecting";break;
            case 9:
                if(!HasMultiplayerEvent("ui-verified"))return;
                StopNetworking();ClearWorldView();ShowMenu();WriteMultiplayerEvent("client-disconnected",new{passed=true});multiplayerPhase=10;multiplayerStage="waiting for durable save";break;
            case 10:
                if(!HasMultiplayerEvent("reconnect-ready"))return;
                lastNetworkFrame=0;clientCommand=clientMotion=0;clientPlayerId="";RequireMultiplayer(network!.Join("127.0.0.1",multiplayerPort)==Error.Ok,"second actual ENet connection initiated");multiplayerPhase=11;multiplayerStage="restoring guest via saved reconnect token";break;
            case 11:
                if(world==null||clientPlayerId!=ValidationGuestId||!remoteAvatars.ContainsKey("host"))return;
                RequireMultiplayer(world.State.Inventory.Slots[3]?.Id=="plank"&&world.State.Inventory.Slots[3]?.Count==7&&world.State.Inventory.Count("wood")==5&&world.State.Health==73,"authenticated reconnect restores own inventory and health");
                VerifyValidationMapRoster(4);
                RequireMultiplayer(networkIdentity.Credentials.ContainsKey(world.State.WorldId),"reconnect credential retained for same world");WriteMultiplayerEvent("client-reconnected",new{passed=true});multiplayerPhase=12;multiplayerStage="host reconnect acknowledgement";break;
            case 12:
                if(!HasMultiplayerEvent("reconnect-verified"))return;
                RequireMultiplayer(network!.Connected&&world!=null,"active client remains online after all gameplay assertions");
                WriteMultiplayerEvent("client-passed",new{passed=true,readyForHostClose=true});multiplayerPhase=13;multiplayerStage="waiting for host to close live room";break;
            case 13:
                if(!HasMultiplayerEvent("host-room-closed")||network!.Connected||world!=null||screen!="menu")return;
                RequireMultiplayer(!NetClient&&remoteAvatars.Count==0,"host room-close notification returns active client to clean main menu");
                RequireMultiplayer(CaptureMapRoster(ValidationGuestId,null!).Count==0,"closed client map roster contains no stale player markers");
                WriteMultiplayerEvent("client-closed",new{passed=true});FinishMultiplayerValidation(true,"");break;
        }
    }
    private void TickValidationMiningHost(RoomPlayer guest)
    {
        multiplayerStage="authoritative survival mining "+validationMiningHostIndex;
        if(validationMiningHostIndex>=ValidationMiningFixtures.Length){
            RequireMultiplayer(guest.State.Inventory.Count("wood")==5&&guest.State.Inventory.Count("apple")==3&&guest.State.Inventory.Count("plank")==7&&guest.State.Health==73,"mining fixtures preserve original guest inventory and health before restoration");
            guest.State.Inventory=validationMiningOriginalInventory!.Copy();guest.State.Health=73;guest.Selected=guest.State.SelectedHotbar=0;guest.AttackCooldown=0;guest.MiningCell=null;guest.MiningTool=null;guest.MiningProgress=0;
            SetScreen("play");SendWorldFrame(guest);WriteMultiplayerEvent("mining-suite-verified",new{passed=true,cases=ValidationMiningFixtures.Length});return;
        }
        var test=ValidationMiningFixtures[validationMiningHostIndex];string key="mining-"+validationMiningHostIndex;
        if(!HasMultiplayerEvent(key+"-ready")){
            validationMiningOriginalInventory??=guest.State.Inventory.Copy();
            foreach(var (slot,id) in new[]{(5,"wood_pick"),(6,"stone_pick"),(7,"iron_pick")})guest.State.Inventory.Slots[slot]=new(id,1,catalog.Items[id].Durability);
            guest.State.Inventory.Slots[8]=null;guest.MiningCell=null;guest.MiningTool=null;guest.MiningProgress=0;guest.LastMineTime=roomClock;guest.AttackCooldown=0;
            world!.Set(test.Cell,catalog.Number(test.Block));renderer!.RefreshEdit(test.Cell);if(test.Alternate is {} other){world.Set(other,catalog.Number(test.Block));renderer.RefreshEdit(other);}
            if(test.Mode=="menu")ShowInventory();SendWorldFrame(guest);validationMiningHostStarted=multiplayerWatch.Elapsed.TotalSeconds;validationMiningSawPartial=validationMiningSawSwitch=false;
            WriteMultiplayerEvent(key+"-ready",new{test.Name,test.Mode,test.Tool});return;
        }
        if(test.Mode=="switch"){
            if(guest.MiningCell==test.Cell&&guest.MiningProgress>.05)validationMiningSawPartial=true;
            if(!validationMiningSawSwitch&&guest.MiningCell==test.Alternate){RequireMultiplayer(guest.MiningProgress<.3,"changing aim resets authoritative partial mining progress");validationMiningSawSwitch=true;}
        }
        if(test.Mode=="pause"&&HasMultiplayerEvent(key+"-paused")&&!HasMultiplayerEvent(key+"-pause-verified")&&guest.MiningCell==null&&guest.MiningProgress==0){
            RequireMultiplayer(world!.Get(test.Cell)==catalog.Number(test.Block),"mine-stop clears guest progress without breaking partially mined block");WriteMultiplayerEvent(key+"-pause-verified",new{passed=true});
        }
        if(!HasMultiplayerEvent(key+"-observed"))return;
        double elapsed=multiplayerWatch.Elapsed.TotalSeconds-validationMiningHostStarted;
        var tool=test.Tool==5?catalog.Items["wood_pick"]:test.Tool==7?catalog.Items["iron_pick"]:null;
        if(test.Mode is "wrong" or "range"){
            RequireMultiplayer(world!.Get(test.Cell)==catalog.Number(test.Block)&&guest.MiningProgress==0,test.Name+" without client block mutation");
        }else{
            var goal=test.Alternate??test.Cell;RequireMultiplayer(world!.Get(goal)==0,"actual host block removed: "+test.Name);
            RequireMultiplayer(elapsed>=MiningRules.Seconds(catalog.Items[test.Block],tool)-.2,"server elapsed time bounds mining speed: "+test.Name);
            if(test.Tool is 5 or 7)RequireMultiplayer(guest.State.Inventory.Slots[test.Tool]?.Durability==tool!.Durability-1,"authoritative mining charges exactly one tool wear: "+test.Name);
            if(test.Mode=="switch")RequireMultiplayer(validationMiningSawPartial&&validationMiningSawSwitch&&world.Get(test.Cell)==catalog.Number(test.Block),"target switch preserves first block and completes only the new target");
            if(test.Mode=="pause")RequireMultiplayer(HasMultiplayerEvent(key+"-pause-verified"),"server acknowledged mining release before resumed completion");
            if(test.Mode=="menu"){RequireMultiplayer(screen=="inventory"&&world.State.Inventory.Count("stone")==11&&world.State.Health==93,"guest mining continues while host inventory remains open and isolated");SetScreen("play");}
        }
        RequireMultiplayer(world!.State.Inventory.Count("stone")==11&&world.State.Health==93,"guest mining keeps host personal state isolated");
        WriteMultiplayerEvent(key+"-verified",new{passed=true,test.Name,elapsed});validationMiningHostIndex++;
    }
    private void TickValidationMiningClient()
    {
        multiplayerStage="client survival mining "+validationMiningClientIndex;
        if(validationMiningClientIndex>=ValidationMiningFixtures.Length){Input.ActionRelease("mine");StopClientMining();selected=0;return;}
        var test=ValidationMiningFixtures[validationMiningClientIndex];string key="mining-"+validationMiningClientIndex;
        if(validationMiningClientPhase==0){
            if(!HasMultiplayerEvent(key+"-ready")||world!.Get(test.Cell)!=catalog.Number(test.Block))return;
            if(test.Tool is 5 or 7&&world.State.Inventory.Slots[test.Tool]?.Id!=(test.Tool==5?"wood_pick":"iron_pick"))return;
            selected=test.Tool;message="";AimValidationMining(test.Cell);validationMiningClientStarted=multiplayerWatch.Elapsed.TotalSeconds;
            if(test.Mode=="burst")for(int i=0;i<50;i++)SendAction("mine");
            actionsArmed=true;Input.ActionPress("mine");validationMiningClientPhase=1;return;
        }
        double elapsed=multiplayerWatch.Elapsed.TotalSeconds-validationMiningClientStarted;
        if(elapsed>MiningRules.Seconds(catalog.Items[test.Block],test.Tool==5?catalog.Items["wood_pick"]:test.Tool==7?catalog.Items["iron_pick"]:null)+12)throw new TimeoutException("survival mining "+test.Name);
        if(validationMiningClientPhase==1){
            if(test.Mode=="switch"&&elapsed>=.3){RequireMultiplayer(world!.Get(test.Cell)!=0,"client switches target before original block breaks");AimValidationMining(test.Alternate!.Value);validationMiningClientPhase=3;return;}
            if(test.Mode=="pause"&&elapsed>=.25){Input.ActionRelease("mine");StopClientMining();WriteMultiplayerEvent(key+"-paused",new{passed=true});validationMiningClientPhase=2;return;}
            if(test.Mode=="wrong"){
                if(elapsed<.6||!message.Contains("需要合适等级",StringComparison.Ordinal))return;
                RequireMultiplayer(world!.Get(test.Cell)!=0&&mineProgress==0,"wrong tool shows server notice with zero client progress instead of stuck 99 percent");FinishValidationMiningCase(key);return;
            }
            if(test.Mode=="range"){
                if(elapsed<.6)return;RequireMultiplayer(player!.Target()?.Hit!=test.Cell&&world!.Get(test.Cell)!=0&&mineProgress==0,"native six-block aim refuses out-of-range mining");FinishValidationMiningCase(key);return;
            }
            if(test.Mode=="burst"&&elapsed<.25){RequireMultiplayer(world!.Get(test.Cell)!=0,"burst of fifty reliable commands cannot instantly destroy block");return;}
        }
        if(validationMiningClientPhase==2){
            if(!HasMultiplayerEvent(key+"-pause-verified")||elapsed<.8)return;
            RequireMultiplayer(world!.Get(test.Cell)!=0,"stopped client leaves partially mined block intact");actionsArmed=true;Input.ActionPress("mine");validationMiningClientPhase=3;return;
        }
        if(validationMiningClientPhase is 1 or 3&&world!.Get(test.Alternate??test.Cell)==0){RequireMultiplayer(true,"friend observes replicated host block removal: "+test.Name);FinishValidationMiningCase(key);return;}
        if(validationMiningClientPhase==4&&HasMultiplayerEvent(key+"-verified")){validationMiningClientIndex++;validationMiningClientPhase=0;selected=0;}
    }
    private void AimValidationMining(Cell cell)
    {
        player!.LookAt(new(cell.X+.5f,player.GlobalPosition.Y,cell.Z+.5f));player.Camera.LookAt(new(cell.X+.5f,cell.Y+.5f,cell.Z+.5f));
        if(cell.Z<15)RequireMultiplayer(player.Target()?.Hit==cell,"native client ray selects survival mining fixture");
    }
    private void FinishValidationMiningCase(string key)
    {
        Input.ActionRelease("mine");StopClientMining();WriteMultiplayerEvent(key+"-observed",new{passed=true});validationMiningClientPhase=4;
    }
    private void VerifyValidationMapRoster(int expected)
    {
        string self=NetHost?"host":clientPlayerId;var roster=CaptureMapRoster(self,player!);
        RequireMultiplayer(roster.Count==expected&&roster.Select(m=>m.Id).Distinct().Count()==expected,"map roster contains self and every connected friend without duplicates");
        var local=roster.Single(m=>m.Id==self);RequireMultiplayer(local.IsSelf&&local.Nickname==networkNickname&&local.Position.DistanceSquaredTo(player!.GlobalPosition)<.000001f,"map self marker retains nickname and current actor coordinates");
        foreach(var marker in roster.Where(m=>m.Id!=self)){
            if(NetHost){var actor=roomPlayers.Values.Single(p=>p.State.Id==marker.Id);RequireMultiplayer(marker.Nickname==actor.State.Nickname&&marker.Position.DistanceSquaredTo(actor.Actor.GlobalPosition)<.000001f&&Math.Abs(Mathf.AngleDifference(marker.Yaw,actor.Actor.Rotation.Y))<.001,"host map friend markers match authoritative names, positions and facing");}
            else{var avatar=remoteAvatars[marker.Id];RequireMultiplayer(marker.Nickname==avatar.Nickname&&marker.Position.DistanceSquaredTo(avatar.LatestPosition)<.000001f&&Math.Abs(Mathf.AngleDifference(marker.Yaw,avatar.LatestYaw))<.001,"client map friend markers match latest network pose rather than interpolated model coordinates");}
        }
    }
    private void TickMultiplayerObserver()
    {
        string own=multiplayerRole=="observer1"?ValidationObserver1Id:ValidationObserver2Id;
        if(multiplayerPhase==0){
            if(world==null||clientPlayerId!=own||remoteAvatars.Count!=3)return;
            RequireMultiplayer(remoteAvatars.ContainsKey("host")&&remoteAvatars.ContainsKey(ValidationGuestId)&&remoteAvatars.ContainsKey(multiplayerRole=="observer1"?ValidationObserver2Id:ValidationObserver1Id),"observer renders all three other players in four-player room");
            RequireMultiplayer(remoteAvatars[ValidationGuestId].AppearanceId=="artisan"&&remoteAvatars["host"].AppearanceId=="ranger","observer receives selected friend and host appearances");
            RequireMultiplayer(remoteAvatars.Values.All(a=>GodotObject.IsInstanceValid(a)&&a.GetChildren().OfType<EmberLighthouse.Rendering.PresentationModel>().Any()),"observer remote avatars contain native character meshes");
            VerifyValidationMapRoster(4);
            WriteMultiplayerEvent(multiplayerRole+"-joined",new{passed=true});multiplayerPhase=1;multiplayerStage="observing shared world and reconnect";return;
        }
        if(multiplayerPhase==1){
            if(world==null||world.Get(ValidationPlaced)!=catalog.Number("plank"))return;
            RequireMultiplayer(world.State.Inventory.Count("plank")==0&&world.State.Inventory.Count("wood")==0,"observer inventories isolated from active friend placement and pickup");
            if(!HasMultiplayerEvent("client-passed"))return;
            RequireMultiplayer(network!.Connected&&remoteAvatars.ContainsKey("host"),"observer stays connected through another friend's authenticated reconnect");
            WriteMultiplayerEvent(multiplayerRole+"-passed",new{passed=true,readyForHostClose=true});
            multiplayerPhase=2;multiplayerStage="waiting for host to close live room";return;
        }
        if(multiplayerPhase==2){
            if(!HasMultiplayerEvent("host-room-closed")||network!.Connected||world!=null||screen!="menu")return;
            RequireMultiplayer(!NetClient&&remoteAvatars.Count==0,"host room-close notification returns observer to clean main menu");
            WriteMultiplayerEvent(multiplayerRole+"-closed",new{passed=true});FinishMultiplayerValidation(true,"");
        }
    }
    private void SendValidationSlot(string name)
    {
        var offer=JsonSerializer.Deserialize<ValidationMenuOffer>(File.ReadAllText(Path.Combine(multiplayerFolder,name+".json")),Catalog.JsonOptions)!;
        SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-click",Token=offer.Token,UiRevision=offer.Revision,Selected=selected,Rotation=rotation});
    }
    private void OfferValidationDrag(RoomPlayer guest,string name,int source,int target)
    {
        int Token(int slot)=>guest.UiControls.Single(p=>p.Value is SlotButton b&&b.SurfaceIndex==0&&b.SlotIndex==slot).Key;
        WriteMultiplayerEvent(name,new ValidationMenuOffer{Token=Token(source),TargetToken=Token(target),Revision=guest.UiRevision});
    }
    private void SendValidationDrag(string name)
    {
        var offer=JsonSerializer.Deserialize<ValidationMenuOffer>(File.ReadAllText(Path.Combine(multiplayerFolder,name+".json")),Catalog.JsonOptions)!;
        SendNet(1,"action",new GameAction{Sequence=++clientCommand,Kind="ui-drag",Token=offer.Token,Token2=offer.TargetToken,UiRevision=offer.Revision,Selected=selected,Rotation=rotation});
    }
    private object MultiplayerDebugSnapshot()
    {
        static string InventorySummary(Inventory? inventory)=>inventory==null?"":string.Join(",",inventory.Slots.Select((item,index)=>(item,index)).Where(p=>p.item!=null).Select(p=>$"{p.index}:{p.item!.Id}x{p.item.Count}"));
        return new{role=multiplayerRole,phase=multiplayerPhase,stage=multiplayerStage,screen,seconds=multiplayerWatch.Elapsed.TotalSeconds,connected=network?.Connected,avatarCount=remoteAvatars.Count,lastNetworkFrame,
            miningCell=miningCell?.ToString(),miningToolId,mineProgress,validationMiningHostIndex,validationMiningClientIndex,validationMiningClientPhase,
            cursor=world?.State.Cursor==null?"":$"{world.State.Cursor.Id}x{world.State.Cursor.Count}",inventory=InventorySummary(world?.State.Inventory),
            guests=roomPlayers.Values.Select(g=>new{g.Peer,g.State.Id,g.Screen,g.UiRevision,cursor=g.State.Cursor==null?"":$"{g.State.Cursor.Id}x{g.State.Cursor.Count}",inventory=InventorySummary(g.State.Inventory),commands=g.LastCommand,controls=g.UiControls.Count,miningCell=g.MiningCell?.ToString(),g.MiningTool,g.MiningProgress,g.LastMineTime}).ToArray()};
    }
    private void RequireMultiplayer(bool condition,string assertion){if(!condition)throw new InvalidOperationException(assertion);if(!multiplayerAssertions.Contains(assertion))multiplayerAssertions.Add(assertion);}
    private bool HasMultiplayerEvent(string name)=>File.Exists(Path.Combine(multiplayerFolder,name+".json"));
    private void WriteMultiplayerEvent(string name,object value)
    {
        string path=Path.Combine(multiplayerFolder,name+".json"),temporary=path+"."+multiplayerRole+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(value,Catalog.JsonOptions));File.Move(temporary,path,true);
    }
    private void FinishMultiplayerValidation(bool passed,string error)
    {
        if(multiplayerFinished)return;multiplayerFinished=true;Input.ActionRelease("mine");
        WriteMultiplayerEvent(multiplayerRole+"-validation",new{passed,error,role=multiplayerRole,stage=multiplayerStage,seconds=multiplayerWatch.Elapsed.TotalSeconds,assertions=multiplayerAssertions,actualEnet=true,actualProcesses=true,sent=network?.SentMessages,received=network?.ReceivedMessages,debug=MultiplayerDebugSnapshot()});
        if(passed)GD.Print("MULTIPLAYER_PASS: "+multiplayerRole+" "+string.Join("; ",multiplayerAssertions));else GD.PushError("MULTIPLAYER_FAILED: "+multiplayerRole+" "+multiplayerStage+" "+error);
        QuitCleanly(passed?0:23);
    }
}
