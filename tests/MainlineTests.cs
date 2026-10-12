using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.World;

public static class MainlineTests
{
    public static void Run(Catalog catalog,CreatureDef[] definitions,Action<string,Action> test,string testRoot)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        test("mainline layouts preserve both legacy generators and every required access ramp",()=>{
            for(int seed=0;seed<40;seed++){
                var terrain=new Terrain(seed,catalog);var layout=terrain.Adventure!;Check(layout.Sites.Select(s=>s.Center).Distinct().Count()==5,"overlapping site origins");
                foreach(var t in terrain.TowerLocations)Check(catalog.Id(terrain.Get(new(t.X,terrain.Surface(t.X,t.Z)+1,t.Z)))=="beacon","missing new beacon");
                foreach(var site in layout.Sites){
                    Check(Coordinates.InWorld(site.Entry)&&terrain.Get(site.Entry)==0&&catalog.Block(terrain.Get(site.Entry+new Cell(0,-1,0)))?.Solid==true,"entry obstructed: "+site.Id);
                    for(int step=0;step<=40-site.Floor;step++){var p=site.At(0,step+1,site.Radius+step);Check(terrain.Get(p)==0&&terrain.Get(p+new Cell(0,1,0))==0&&catalog.Block(terrain.Get(p+new Cell(0,-1,0)))?.Solid==true,"ramp not walkable: "+site.Id);}
                }
                foreach(var d in layout.Devices)Check(catalog.Id(terrain.Get(d.Key))==d.Value,"missing physical device");
                Check(terrain.Biome(layout.Site("forge").Center.X,layout.Site("forge").Center.Z)==2&&terrain.Biome(layout.Site("snow").Center.X,layout.Site("snow").Center.Z)==3,"region climate wrong");
                foreach(int version in new[]{1,2}){var legacy=new Terrain(seed,catalog,version);Check(legacy.Adventure==null&&legacy.TowerLocations.SequenceEqual(Terrain.Towers),"legacy objectives moved");}
            }
        });
        test("mainline cannot pay before proofs; both regional orders and repeated repair conserve resources",()=>{
            foreach(bool reverse in new[]{false,true}){
                var w=new WorldState(catalog,new());w.State.Inventory.Add("copper_coil",8,catalog);w.State.Inventory.Add("iron_ingot",64,catalog);w.State.Inventory.Add("beacon_sigil",2,catalog);
                string before=JsonSerializer.Serialize(w.State);Check(!Mainline.Repair(w,0,out _)&&before==JsonSerializer.Serialize(w.State),"unearned tower consumed resources");
                Check(Mainline.RecordVictory(w.State,"mine_warden"),"mine proof missing");Check(Mainline.Repair(w,0,out _),"first repair refused");Check(Mainline.RecipeKnown(w.State,catalog.Recipes["survey_lens"]),"survey recipe locked");
                Mainline.RecordVictory(w.State,reverse?"observatory_warden":"furnace_warden");before=JsonSerializer.Serialize(w.State);Check(!Mainline.Repair(w,1,out _)&&before==JsonSerializer.Serialize(w.State),"only one prism accepted");
                Mainline.RecordVictory(w.State,reverse?"furnace_warden":"observatory_warden");Check(Mainline.Repair(w,1,out _)&&Mainline.RecipeKnown(w.State,catalog.Recipes["ember_alloy"]),"second repair failed");
                Mainline.RecordVictory(w.State,"guardian");Check(Mainline.Repair(w,2,out _)&&w.State.Inventory.Count("copper_coil")==3&&w.State.Inventory.Count("iron_ingot")==52&&w.State.Inventory.Count("beacon_sigil")==1,"repair totals wrong");before=JsonSerializer.Serialize(w.State);Check(!Mainline.Repair(w,2,out _)&&before==JsonSerializer.Serialize(w.State),"repeat repair charged twice");
                var blocked=new WorldState(catalog,new());blocked.State.Mainline.Proofs.Add("mine");blocked.State.Inventory.Add("copper_coil",1,catalog);before=JsonSerializer.Serialize(blocked.State);Check(!Mainline.Repair(blocked,0,out _)&&before==JsonSerializer.Serialize(blocked.State),"partly missing materials consumed coil");
            }
        });
        test("mainline key rewards survive full inventory, death, copies and save reload",()=>{
            var w=new WorldState(catalog,new());for(int i=0;i<36;i++)w.State.Inventory.Slots[i]=new("stone",64);
            Mainline.RecordVictory(w.State,"furnace_warden");Mainline.RecordVictory(w.State,"observatory_warden");var copy=w.State.Copy();w.State.Mainline.Proofs.Add("mine");Check(!copy.Mainline.Proofs.Contains("mine"),"mainline proof aliases snapshot");
            w.Die();Check(w.State.Mainline.Proofs.Contains("forge")&&w.State.Mainline.Proofs.Contains("snow"),"death lost keys");var store=new SaveStore(Path.Combine(testRoot,"mainline-save","worlds"),catalog);store.Save(w.State);var loaded=store.Load(w.State.WorldId).Snapshot;Check(loaded.Mainline.Proofs.SetEquals(w.State.Mainline.Proofs)&&loaded.GeneratorVersion==Terrain.Version&&loaded.FormatVersion==WorldSnapshot.CurrentFormat,"saved progress lost");
        });
        test("mainline finale requires actual repaired bridge and all nodes; death resets only battle",()=>{
            var w=new WorldState(catalog,new());var q=w.State.Mainline;Check(!Mainline.StartFinale(w,out _),"early final entry");w.State.Beacons.UnionWith([0,1,2]);Check(!Mainline.StartFinale(w,out _),"missing nodes accepted");var site=w.Terrain.Adventure!.Site("vault");Check(!Mainline.BridgeReady(w),"unrepaired bridge ready");
            for(int x=-22;x<=-17;x++)w.Set(site.At(x,4,-16),catalog.Number("plank"));Check(Mainline.BridgeReady(w),"built bridge not recognized");q.Nodes.UnionWith(["bridge","reflector","guard"]);Check(Mainline.StartFinale(w,out _),"prepared entry refused");
            var anchor=w.Terrain.Adventure.Anchor(0);Check(Mainline.CloseAnchor(w,anchor)&&!Mainline.CloseAnchor(w,anchor)&&q.Anchors.Count==1,"anchor not idempotent");w.State.Position=[site.Center.X,site.Floor+1,site.Center.Z];w.State.Inventory.Add("shield",1,catalog);w.State.Creatures.Add(new(){Kind="colossus",Home="vault",Health=500});q.BattleTime=20;
            w.Die();Check(!q.FinaleActive&&q.Anchors.Count==0&&q.Nodes.Count==3&&w.State.Beacons.Count==3&&w.State.Creatures.All(c=>c.Kind!="colossus"),"death reset permanent milestones or retained boss");var grave=w.State.Graves.Last();Check(grave.Position[1]==41&&grave.Items.Count("shield")==1,"final death bag not at surface entry");
        });
        test("arrows close exact anchors; walls still block and elite projectiles bypass camp",()=>{
            var w=new WorldState(catalog,new());var q=w.State.Mainline;q.FinaleActive=true;var anchor=w.Terrain.Adventure!.Anchor(2);
            var shot=new ProjectileState{Position=[anchor.X+.5,anchor.Y+.5,anchor.Z+3],Velocity=[0,0,-10],Remaining=1};Check(ProjectileFlight.Step(w,shot,.4,_=>true).Outcome==ProjectileOutcome.Anchor&&q.Anchors.Contains(2),"arrow failed to close high anchor");
            var blockedAnchor=w.Terrain.Adventure.Anchor(0);w.Set(blockedAnchor+new Cell(0,0,1),catalog.Number("stone"));shot=new(){Position=[blockedAnchor.X+.5,blockedAnchor.Y+.5,blockedAnchor.Z+3],Velocity=[0,0,-10],Remaining=1};Check(ProjectileFlight.Step(w,shot,.4,_=>true).Outcome==ProjectileOutcome.Blocked&&!q.Anchors.Contains(0),"arrow closed anchor through wall");
            var p=new Cell(300,80,300);w.State.Position=[p.X+.5,p.Y,p.Z+.5];w.State.Entities[(p+new Cell(1,0,0)).ToString()]=new(){Kind="campfire"};q.FinaleActive=false;
            var hostile=new ProjectileState{Position=[300.5,80.9,303],Velocity=[0,0,-10],Remaining=1,Hostile=true,BypassCamp=true};Check(ProjectileFlight.Step(w,hostile,.4,_=>true).Outcome==ProjectileOutcome.Player,"elite projectile absorbed by camp");
        });
        test("lining and repairs conserve metadata across sort click transfer death and disk",()=>{
            var w=new WorldState(catalog,new());w.State.Equipment.Slots[1]=new("iron_chestplate",1,20){HeatLining=true};w.State.Inventory.Add("repair_kit",1,catalog);Check(Mainline.RepairItem(w,w.State.Equipment,1,out _)&&w.State.Equipment.Slots[1]!.Durability==116&&w.State.Inventory.Count("repair_kit")==0,"repair wrong or kit duplicated");
            Check(Inventory.Move(w.State.Equipment,1,w.State.Inventory,4,1,catalog),"armor move");w.State.Inventory.Sort(catalog);int slot=Array.FindIndex(w.State.Inventory.Slots,s=>s?.Id=="iron_chestplate");Inventory.Click(w.State.Inventory,slot,null,false,catalog,out var cursor);Check(cursor!.HeatLining,"click lost lining");Inventory.Click(w.State.Inventory,slot,cursor,false,catalog,out _);var store=new SaveStore(Path.Combine(testRoot,"mainline-lining","worlds"),catalog);store.Save(w.State);var loaded=store.Load(w.State.WorldId).Snapshot;Check(loaded.Inventory.Slots.Any(s=>s?.HeatLining==true),"disk lost lining");w.Die();Check(w.State.Graves.Last().Items.Slots.Any(s=>s?.HeatLining==true),"death lost lining");
        });
        test("old formats migrate while preserving old route, terrain, armor and construction",()=>{
            var old=new WorldSnapshot{FormatVersion=4,GeneratorVersion=2,GuardianDefeated=true};old.Equipment.Slots[1]=new("iron_chestplate",1,40);old.Changes["100,37,100"]="plank";old.Beacons.Add(1);Check(SaveMigrations.ToCurrent(old)&&old.FormatVersion==WorldSnapshot.CurrentFormat&&!Mainline.Enabled(old),"legacy route changed");var w=new WorldState(catalog,old);Check(w.Terrain.Adventure==null&&w.Catalog.Id(w.Get(new(100,37,100)))=="plank"&&old.Equipment.Slots[1]!.Durability==40&&old.Beacons.Contains(1)&&Mainline.RecipeKnown(old,catalog.Recipes["beacon_sigil_legacy"]),"legacy world changed during migration");
        });
    }
}
