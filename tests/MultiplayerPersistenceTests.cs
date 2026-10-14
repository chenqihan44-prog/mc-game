using System.Text.Json;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.World;

public static class MultiplayerPersistenceTests
{
    public static void Run(Catalog catalog,Action<string,Action> test,string testRoot)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        test("live multiplayer scopes preserve captured UI inventory callbacks across other players and deep-copy saves",()=>{
            var snapshot=new WorldSnapshot();snapshot.Inventory.Slots[0]=new("stone",12);var host=MultiplayerPlayerState.CaptureLive(snapshot);snapshot.Players[host.Id]=host;
            var guest=MultiplayerPlayers.Create(snapshot,"callback-guest","朋友");guest.Inventory.Slots[0]=new("plank",5);
            var other=MultiplayerPlayers.Create(snapshot,"other-guest","另一位朋友");other.Inventory.Slots[0]=new("wood",9);
            var world=new WorldState(catalog,snapshot);var target=new EmberLighthouse.Core.Cell(300,80,300);world.Set(target,0);
            void Scope(MultiplayerPlayerState player,Action action){var previous=MultiplayerPlayerState.CaptureLive(snapshot);player.ApplyLive(snapshot);try{action();}finally{player.CaptureLiveFrom(snapshot);previous.ApplyLive(snapshot);}}
            Inventory capturedInventory=null!,capturedEquipment=null!;Action callback=null!;
            Scope(guest,()=>{
                capturedInventory=snapshot.Inventory;capturedEquipment=snapshot.Equipment;
                callback=()=>{
                    Check(ReferenceEquals(capturedInventory,snapshot.Inventory)&&ReferenceEquals(capturedEquipment,snapshot.Equipment),"UI callback lost the active player's owned inventory references");
                    Check(world.Place(target,"plank",false,out var error,supply:capturedInventory,supplySlot:0),"captured UI placement rejected as foreign inventory: "+error);
                    Check(Inventory.Move(capturedInventory,0,capturedEquipment,4,1,catalog,targetAccepts:(slot,stack)=>Equipment.Accepts(catalog,slot,stack)),"captured UI equipment transfer failed");
                };
            });
            Scope(other,()=>{snapshot.Inventory.Add("iron_ingot",1,catalog);snapshot.Health=83;});
            Check(snapshot.Inventory.Count("stone")==12&&snapshot.Health==100,"intervening guest changed host active state");
            Scope(guest,()=>callback());
            Check(guest.Inventory.Count("plank")==3&&guest.Equipment.Slots[4]?.Id=="plank"&&guest.Equipment.Slots[4]?.Count==1&&world.Get(target)==catalog.Number("plank"),"callback did not change the correct guest inventory and shared world");
            Check(other.Inventory.Count("wood")==9&&other.Inventory.Count("iron_ingot")==1&&other.Health==83&&host.Inventory.Count("stone")==12&&snapshot.Inventory.Count("plank")==0,"guest callback changed another player's inventory or vitals");
            Check(ReferenceEquals(guest.Inventory,capturedInventory)&&ReferenceEquals(guest.Equipment,capturedEquipment),"committing live projection replaced persistent callback references");
            var diskSnapshot=snapshot.Copy();guest.Inventory.Slots[0]!.Count=1;guest.Equipment.Slots[4]!.Count=2;other.Inventory.Slots[0]!.Count=1;snapshot.Inventory.Slots[0]!.Count=1;
            Check(diskSnapshot.Players[guest.Id].Inventory.Count("plank")==3&&diskSnapshot.Players[guest.Id].Equipment.Slots[4]!.Count==1&&diskSnapshot.Players[other.Id].Inventory.Count("wood")==9&&diskSnapshot.Inventory.Count("stone")==12,"disk snapshot aliases live UI projections");
        });
        test("multiplayer player scopes isolate inventory and progression while keeping shared world edits",()=>{
            var snapshot=new WorldSnapshot{Health=72,Hunger=61,Air=10,Experience=9,Position=[12,40,18],Respawn=[10,40,10],TutorialStep=3,SelectedHotbar=5};
            snapshot.Inventory.Slots[0]=new("stone",12);snapshot.Equipment.Slots[0]=new("iron_helmet",1,30);snapshot.Crafting.Slots[0]=new("plank",2);snapshot.Cursor=new("wood",1);
            snapshot.Discovered.Add("stone");snapshot.ExploredMap.Add(0);snapshot.FavoriteRecipes.Add("test-recipe");snapshot.Markers.Add([12,18]);snapshot.Effects.FireUntil=500;
            var host=MultiplayerPlayers.CaptureHost(snapshot);string token=host.ReconnectToken;
            var guest=MultiplayerPlayers.Create(snapshot,"guest-1","朋友");Check(guest.Inventory.Count("stone")==0&&guest.Equipment.Slots.All(s=>s==null)&&guest.Position.SequenceEqual(snapshot.Respawn),"guest copied host possessions or wrong spawn");
            guest.Apply(snapshot);snapshot.Inventory.Add("iron_ingot",3,catalog);snapshot.Health=85;snapshot.Position[0]=90;snapshot.Effects.FireUntil=600;snapshot.Markers.Add([90,20]);snapshot.Changes["90,40,20"]="plank";snapshot.Beacons.Add(1);snapshot.Mainline.Proofs.Add("mine");
            guest.CaptureFrom(snapshot);host.Apply(snapshot);
            Check(snapshot.Inventory.Count("stone")==12&&snapshot.Inventory.Count("iron_ingot")==0&&snapshot.Health==72&&snapshot.Position[0]==12&&snapshot.Effects.FireUntil==500&&snapshot.Markers.Count==1&&snapshot.SelectedHotbar==5,"guest state overwrote host");
            Check(snapshot.Changes["90,40,20"]=="plank"&&snapshot.Beacons.Contains(1)&&snapshot.Mainline.Proofs.Contains("mine"),"restoring host rolled back shared progression");
            Check(guest.Inventory.Count("iron_ingot")==3&&guest.Health==85&&guest.Position[0]==90&&guest.Markers.Count==1&&guest.Effects.FireUntil==600,"guest capture lost state");
            snapshot.Inventory.Slots[0]!.Count=1;snapshot.Equipment.Slots[0]!.Durability=5;snapshot.Crafting.Slots[0]!.Count=1;snapshot.Cursor!.Count=2;snapshot.Respawn[0]=99;snapshot.Discovered.Add("wood");snapshot.ExploredMap.Add(1);snapshot.FavoriteRecipes.Clear();snapshot.Markers[0][0]=99;
            Check(host.Inventory.Count("stone")==12&&host.Equipment.Slots[0]!.Durability==30&&host.Crafting.Slots[0]!.Count==2&&host.Cursor!.Count==1&&host.Respawn[0]==10&&!host.Discovered.Contains("wood")&&!host.ExploredMap.Contains(1)&&host.FavoriteRecipes.Count==1&&host.Markers[0][0]==12,"applied personal state aliases DTO");
            Check(MultiplayerPlayers.CaptureHost(snapshot).ReconnectToken==token,"host update rotates reconnect credential");
        });
        test("multiplayer snapshots and disk saves preserve disconnected players appearance and credential",()=>{
            var snapshot=new WorldSnapshot();MultiplayerPlayers.CaptureHost(snapshot);var guest=MultiplayerPlayers.Create(snapshot,"guest-2","朋友二","adventurer");
            guest.Inventory.Add("wood",7,catalog);guest.Equipment.Slots[1]=new("iron_chestplate",1,20){HeatLining=true};guest.Position=[40,45,60];guest.Respawn=[38,45,59];guest.Health=44;guest.Experience=17;guest.Markers.Add([40,60]);guest.Discovered.Add("wood");guest.ExploredMap.Add(2);guest.SelectedHotbar=8;guest.TutorialHidden=true;guest.Effects.Shield=5;guest.Effects.ShieldUntil=700;
            snapshot.Graves.Add(new(){OwnerId=guest.Id,Position=[40,45,60]});var copy=snapshot.Copy();guest.Inventory.Remove("wood",1);guest.Equipment.Slots[1]!.Durability=2;guest.Position[0]=90;guest.Markers[0][0]=90;guest.Discovered.Clear();guest.ExploredMap.Clear();guest.Effects.Shield=1;
            var store=new SaveStore(Path.Combine(testRoot,"multiplayer","worlds"),catalog);store.Save(copy);var loaded=store.Load(snapshot.WorldId).Snapshot;var saved=loaded.Players[guest.Id];
            Check(saved.Inventory.Count("wood")==7&&saved.Equipment.Slots[1]!.Durability==20&&saved.Equipment.Slots[1]!.HeatLining&&saved.Position[0]==40&&saved.Markers[0][0]==40&&saved.Discovered.Contains("wood")&&saved.ExploredMap.Contains(2)&&saved.Effects.Shield==5,"save copy aliases disconnected personal state");
            Check(saved.ReconnectToken==guest.ReconnectToken&&saved.Nickname==guest.Nickname&&saved.AppearanceId=="adventurer"&&saved.SelectedHotbar==8&&saved.Health==44&&saved.Experience==17&&saved.TutorialHidden&&loaded.Graves[0].OwnerId==guest.Id,"reconnect metadata or owner lost");
            saved.Apply(loaded);Check(loaded.Inventory.Count("wood")==7&&loaded.Position[0]==40&&loaded.SelectedHotbar==8,"reconnect did not recover personal state");
        });
        test("format7 migration preserves legacy active player and creates independent host record",()=>{
            var legacy=new WorldSnapshot{FormatVersion=7,Health=63,Hunger=70,Experience=12,Position=[20,44,25],TutorialStep=4};legacy.Inventory.Add("stone",9,catalog);legacy.Equipment.Slots[0]=new("iron_helmet",1,30);legacy.Graves.Add(new(){Death=true});
            string raw=JsonSerializer.Serialize(legacy,Catalog.JsonOptions);var node=System.Text.Json.Nodes.JsonNode.Parse(raw)!.AsObject();node.Remove("Players");node.Remove("SelectedHotbar");
            var migrated=JsonSerializer.Deserialize<WorldSnapshot>(node.ToJsonString(),Catalog.JsonOptions)!;
            Check(SaveMigrations.ToCurrent(migrated)&&migrated.FormatVersion==8&&migrated.Players.Count==1,"format7 did not create host");var host=migrated.Players[MultiplayerPlayers.LegacyHostId];
            Check(host.Inventory.Count("stone")==9&&host.Health==63&&host.Equipment.Slots[0]!.Durability==30&&host.Position[0]==20&&host.TutorialStep==4&&host.Experience==12&&host.AppearanceId=="adventurer","migration lost legacy player");
            Check(migrated.Inventory.Count("stone")==9&&migrated.Graves[0].OwnerId==null&&!migrated.Graves[0].LegacyContainer,"migration changed old active fields or grave semantics");
            migrated.Inventory.Slots[0]!.Count=2;Check(host.Inventory.Count("stone")==9,"migration host aliases legacy inventory");string token=host.ReconnectToken;Check(!SaveMigrations.ToCurrent(migrated)&&migrated.Players["host"].ReconnectToken==token,"current migration rotates credential");
            _=new WorldState(catalog,migrated);
            string storeRoot=Path.Combine(testRoot,"multiplayer-format7","worlds"),worldPath=Path.Combine(storeRoot,legacy.WorldId);Directory.CreateDirectory(worldPath);
            const string generation="20000107000000-format7";byte[] bytes=System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());string originalFile=Path.Combine(worldPath,generation+".json");File.WriteAllBytes(originalFile,bytes);
            byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new SaveManifest(1,generation,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),DateTime.UtcNow),Catalog.JsonOptions);
            File.WriteAllBytes(Path.Combine(worldPath,generation+".commit"),manifest);File.WriteAllBytes(Path.Combine(worldPath,"CURRENT"),manifest);
            var store=new SaveStore(storeRoot,catalog);var loaded=store.Load(legacy.WorldId);Check(loaded.Migrated&&loaded.Snapshot.Players["host"].Inventory.Count("stone")==9&&File.ReadAllBytes(originalFile).SequenceEqual(bytes),"legacy load changed original bytes or lost host");
            string savedToken=loaded.Snapshot.Players["host"].ReconnectToken;store.Save(loaded.Snapshot);Check(!store.Load(legacy.WorldId).Migrated&&store.Load(legacy.WorldId).Snapshot.Players["host"].ReconnectToken==savedToken,"new save re-migrates identity");
            var backup=store.ListBackups().Single();Check(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(storeRoot)!,"backups",backup.Key,generation+".json")).SequenceEqual(bytes),"format7 original backup changed");
        });
        test("malformed multiplayer identities vitals and equipment are rejected before simulation",()=>{
            var valid=new WorldSnapshot();MultiplayerPlayers.CaptureHost(valid);var guest=MultiplayerPlayers.Create(valid,"guest","朋友");
            foreach(Action<MultiplayerPlayerState> breakState in new Action<MultiplayerPlayerState>[]{p=>p.Health=double.NaN,p=>p.Position=[1,2],p=>p.Inventory=new(27),p=>p.ReconnectToken="",p=>p.Nickname="bad\nname",p=>p.SelectedHotbar=9,p=>p.Equipment.Slots[0]=new("stone",1)}){
                var broken=valid.Copy();breakState(broken.Players[guest.Id]);bool refused=false;try{_=new WorldState(catalog,broken);}catch(InvalidOperationException){refused=true;}Check(refused,"malformed player reached simulation");
            }
            var mismatch=valid.Copy();mismatch.Players["guest"].Id="other";bool invalid=false;try{mismatch.Copy();}catch(InvalidOperationException){invalid=true;}Check(invalid,"dictionary identity mismatch accepted");
        });
    }
}
