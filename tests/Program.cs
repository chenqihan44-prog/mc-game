using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.World;
using EmberLighthouse.Rendering;
using EmberLighthouse.Creatures;

var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
var catalog=Catalog.Load(Path.Combine(root,"data"));
var creatureDefs=JsonSerializer.Deserialize<CreatureDef[]>(File.ReadAllText(Path.Combine(root,"data/creatures.json")),Catalog.JsonOptions)!;
void WriteFixture(string storeRoot,WorldSnapshot state,int version=1,string generation="9999-fixture")
{
    string path=Path.Combine(storeRoot,state.WorldId);Directory.CreateDirectory(path);byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(state,Catalog.JsonOptions);File.WriteAllBytes(Path.Combine(path,generation+".json"),bytes);
    var manifest=new SaveManifest(version,generation,Convert.ToHexString(SHA256.HashData(bytes)),DateTime.UtcNow);byte[] commit=JsonSerializer.SerializeToUtf8Bytes(manifest,Catalog.JsonOptions);File.WriteAllBytes(Path.Combine(path,generation+".commit"),commit);File.WriteAllBytes(Path.Combine(path,"CURRENT"),commit);
}
if(args.Length>0&&args[0]=="fault-child"){
    var store=new SaveStore(args[1],catalog);var s=store.Load(args[2]).Snapshot;s.Inventory.Add("crystal",5,catalog);s.Changes["1,50,1"]="plank";s.Equipment.Slots[0]=new("iron_helmet",1,80);s.Equipment.Slots[4]=new("stone",3);
    store.Save(s,point=>{if(point==args[3])Environment.Exit(71);});return;
}
var results=new List<object>();int failed=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);}
void Test(string name,Action run){var timer=Stopwatch.StartNew();try{run();Console.WriteLine($"PASS {name} ({timer.ElapsedMilliseconds}ms)");results.Add(new{name,pass=true,ms=timer.ElapsedMilliseconds});}catch(Exception e){failed++;Console.WriteLine($"FAIL {name}: {e}");results.Add(new{name,pass=false,error=e.Message});}}
Test("map surface reflects high roofs, removed blocks, immutable snapshots and world bounds",()=>{
    var w=new WorldState(catalog,new(){Seed=42});var p=new Cell(-17,150,-33);
    byte[] Sample(IReadOnlyDictionary<Cell,ushort> edits,int x=-17,int z=-33)=>MapRaster.Render(w.Terrain,catalog,edits,x,z,1,1);
    var original=Sample(w.CaptureRegion(p.X,p.Z,0));long version=w.EditVersion;
    w.Set(p,catalog.Number("plank"));var snapshot=w.CaptureRegion(p.X,p.Z,0);var roof=Sample(snapshot);
    Check(w.EditVersion>version&&!roof.SequenceEqual(original),"high roof or edit invalidation missing");
    w.Set(p,0);Check(Sample(snapshot).SequenceEqual(roof)&&Sample(w.CaptureRegion(p.X,p.Z,0)).SequenceEqual(original),"worker snapshot changed or removed roof stays visible");
    for(int y=191;y>=0;y--)if(w.Get(new(p.X,y,p.Z))!=0){w.Set(new(p.X,y,p.Z),catalog.Number("ruin_brick"));break;}
    var modified=Sample(w.CaptureRegion(p.X,p.Z,0));Check(!modified.SequenceEqual(original),"edited ground ignored");
    Check(Sample(new Dictionary<Cell,ushort>(),2048,2048).SequenceEqual(new byte[]{24,32,37,255}),"world exterior rendered as playable bedrock");
    using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{MapRaster.Render(w.Terrain,catalog,snapshot,0,0,1,128,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"closed map worker cannot cancel");
});
Test("map reveals travelled circles, hides unknown terrain and preserves immutable exploration",()=>{
    var w=new WorldState(catalog,new());Check(!w.MapKnown(0,0),"new snapshot already explored");w.RevealMap(0,0);Check(w.MapKnown(0,0)&&w.MapKnown(-20,10)&&!w.MapKnown(39,39)&&!w.MapKnown(100,0),"square or worldwide reveal");
    long version=w.ExplorationVersion;var copy=w.State.Copy();var mask=new HashSet<int>(copy.ExploredMap);w.RevealMap(80,0);Check(w.MapKnown(80,0)&&w.MapKnown(0,0)&&w.ExplorationVersion>version&&!MapExploration.Known(copy.ExploredMap,80,0),"movement loses visited area or mutates save copy");
    var bytes=MapRaster.Render(w.Terrain,catalog,new Dictionary<Cell,ushort>(),80,0,1,1,explored:mask);Check(bytes.SequenceEqual(new byte[]{30,36,40,255}),"unknown terrain leaks through fog");
    var restored=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(w.State,Catalog.JsonOptions),Catalog.JsonOptions)!;var reloaded=new WorldState(catalog,restored);Check(reloaded.MapKnown(0,0)&&reloaded.MapKnown(80,0),"reload forgot exploration");
    w.RevealMap(-2047,-2047);w.RevealMap(2047,2047);Check(w.State.ExploredMap.All(i=>i>=0&&i<MapExploration.Count)&&!w.MapKnown(2048,0),"boundary exploration escaped world");
    restored.ExploredMap.Add(-1);bool invalid=false;try{new WorldState(catalog,restored);}catch(InvalidOperationException){invalid=true;}Check(invalid,"invalid exploration accepted");
});
Test("natural starter biome removes square seams and both generator versions retain safe objectives",()=>{
    int oldSeams=0,newSeams=0;
    for(int seed=0;seed<50;seed++){
        var old=new Terrain(seed,catalog,1);var natural=new Terrain(seed,catalog,2);
        for(int i=-47;i<=47;i++)foreach(var pair in new[]{(new Cell(47,0,i),new Cell(48,0,i)),(new Cell(-47,0,i),new Cell(-48,0,i)),(new Cell(i,0,47),new Cell(i,0,48)),(new Cell(i,0,-47),new Cell(i,0,-48))}){
            if(old.Biome(pair.Item1.X,pair.Item1.Z)!=old.Biome(pair.Item2.X,pair.Item2.Z))oldSeams++;
            if(natural.Biome(pair.Item1.X,pair.Item1.Z)!=natural.Biome(pair.Item2.X,pair.Item2.Z))newSeams++;
        }
        foreach(var terrain in new[]{old,natural}){Check(terrain.Surface(0,0)==36&&catalog.Id(terrain.Get(new(34,38,26)))=="chest","starter floor/cache changed");foreach(var t in Terrain.Towers)Check(catalog.Id(terrain.Get(new(t.X,terrain.Surface(t.X,t.Z)+1,t.Z)))=="beacon","missing tower");}
    }
    Check(oldSeams>1000&&newSeams<oldSeams*.35,$"square edge remains: old {oldSeams} / new {newSeams}");
});
Test("shaped workbench consumes each occupied cell and accepts mirrors and translations",()=>{
    foreach(var recipe in catalog.Recipes.Values.Where(r=>r.Station is "hand" or "workbench"&&r.Seconds==0)){
        var grid=new Inventory(9);var input=new Inventory();foreach(var p in recipe.Inputs)input.Add(p.Key,p.Value*2,catalog);
        Check(WorkbenchGrid.Arrange(input,grid,recipe,catalog,out var error),recipe.Id+": "+error);foreach(var s in grid.Slots)if(s!=null)s.Count*=2;
        var output=new Inventory();Check(WorkbenchGrid.CraftTo(grid,output,recipe,catalog,out error)&&WorkbenchGrid.Matches(grid,recipe),"first batch unbalanced cells: "+recipe.Id);
        Check(WorkbenchGrid.CraftTo(grid,output,recipe,catalog,out error)&&grid.Slots.All(s=>s==null)&&output.Count(recipe.Output)==recipe.Count*2,"second batch count: "+recipe.Id);
    }
    var axe=catalog.Recipes["iron_axe"];var mirrored=new Inventory(9){Slots=WorkbenchGrid.Layout(axe).Select(p=>p==null?null:new Stack(p.Id,p.Count)).ToArray()};
    for(int row=0;row<3;row++)(mirrored.Slots[row*3],mirrored.Slots[row*3+2])=(mirrored.Slots[row*3+2],mirrored.Slots[row*3]);Check(WorkbenchGrid.Matches(mirrored,axe),"mirrored axe rejected");
    var plank=new Inventory(9);plank.Slots[8]=new("wood",1);Check(WorkbenchGrid.Matches(plank,catalog.Recipes["plank"]),"translated single ingredient rejected");plank.Slots[0]=new("coal",1);Check(!WorkbenchGrid.Matches(plank,catalog.Recipes["plank"]),"extra ingredient accepted");
});
Test("workbench refuses blocked output and full inventory without losing materials or wear",()=>{
    var recipe=catalog.Recipes["iron_pick"];var input=new Inventory();input.Add("iron_ingot",3,catalog);input.Add("stick",2,catalog);var grid=new Inventory(9);Check(WorkbenchGrid.Arrange(input,grid,recipe,catalog,out _),"arrange setup");
    var output=new Inventory(1){Slots=[new("iron_sword",1)]};string before=JsonSerializer.Serialize(new{grid,output},Catalog.JsonOptions);Check(!WorkbenchGrid.CraftTo(grid,output,recipe,catalog,out _)&&JsonSerializer.Serialize(new{grid,output},Catalog.JsonOptions)==before,"blocked output mutated transaction");
    for(int i=0;i<36;i++)input.Slots[i]=new("stone",64);grid=new(9);grid.Slots[0]=new("iron_helmet",1,17);grid.Slots[1]=new("stick",2);before=JsonSerializer.Serialize(new{input,grid},Catalog.JsonOptions);
    Check(!WorkbenchGrid.Arrange(input,grid,catalog.Recipes["stone_pick"],catalog,out _)&&JsonSerializer.Serialize(new{input,grid},Catalog.JsonOptions)==before,"full backpack discarded or changed materials/wear");
});
Test("crafting needs an existing nearby station and commits timed ingredients once",()=>{
    var w=new WorldState(catalog,new WorldSnapshot());var p=new Cell(3,37,2);w.State.Inventory.Add("workbench",1,catalog);w.State.Inventory.Add("stone",16,catalog);
    Check(w.Place(p,"workbench",false,out _),"station placement");var furnace=catalog.Recipes["furnace"];
    foreach(var (station,position) in new (Cell?,double[])[]{(null,[0,37,0]),(p,[30,37,0]),(p,[double.NaN,37,0]),(new(4,37,2),[0,37,0])}){Check(!w.Craft(furnace,station,position,out _),"invalid station crafted");Check(w.State.Inventory.Count("stone")==16&&w.State.Inventory.Count("furnace")==0,"rejected craft changed items");}
    Check(w.Craft(furnace,p,[0,37,0],out _),"nearby station refused");Check(w.State.Inventory.Count("stone")==8&&w.State.Inventory.Count("furnace")==1,"instant craft conservation");
    Check(w.Break(p,0,out _),"remove station");Check(!w.Craft(furnace,p,[0,37,0],out _),"removed station still usable");
    var f=new Cell(4,37,2);Check(w.Place(f,"furnace",false,out _),"place furnace");w.State.Inventory.Add("raw_iron",4,catalog);w.State.Inventory.Add("coal",2,catalog);
    var smelt=catalog.Recipes["iron_ingot"];Check(w.Craft(smelt,f,[0,37,0],out _),"smelt start");Check(w.State.Inventory.Count("raw_iron")==2&&w.State.Inventory.Count("coal")==1,"smelt cost");
    Check(!w.Craft(smelt,f,[0,37,0],out _)&&w.State.Inventory.Count("raw_iron")==2,"busy facility took items");w.Advance(7.99);Check(w.State.Entities[f.ToString()].Storage.Count("iron_ingot")==0,"early output");w.Advance(.02);w.Advance(100);Check(w.State.Entities[f.ToString()].Storage.Count("iron_ingot")==2,"output duplicated");
    var full=w.State.Entities[f.ToString()];for(int i=0;i<full.Storage.Slots.Length;i++)full.Storage.Slots[i]=new("stone",64);Check(!w.Craft(smelt,f,[0,37,0],out _)&&w.State.Inventory.Count("raw_iron")==2,"full output consumed input");
});
Test("busy facilities cannot be dismantled before output is safely removed",()=>{
    foreach(bool creative in new[]{false,true}){
        var w=new WorldState(catalog,new(){Creative=creative});var p=new Cell(300,80,300);
        w.State.Inventory.Add("furnace",1,catalog);w.State.Inventory.Add("raw_iron",2,catalog);w.State.Inventory.Add("coal",1,catalog);
        Check(w.Place(p,"furnace",false,out _),"furnace placement");Check(w.Craft(catalog.Recipes["iron_ingot"],p,[300,80,300],out _),"start smelting");
        var before=JsonSerializer.Serialize(w.State,Catalog.JsonOptions);Check(!w.Break(p,3,out _),"busy furnace dismantled and consumed ingredients vanished");Check(JsonSerializer.Serialize(w.State,Catalog.JsonOptions)==before,"rejected dismantle changed state");
        w.Advance(8);Check(w.State.Entities[p.ToString()].Storage.Count("iron_ingot")==2,"output missing after refused dismantle");
        Check(!w.Break(p,3,out _),"filled furnace dismantled");Check(w.State.Entities[p.ToString()].Storage.Remove("iron_ingot",2),"remove finished output");
        Check(w.Break(p,3,out _)&&w.Get(p)==0&&!w.State.Entities.ContainsKey(p.ToString()),"empty idle furnace cannot be dismantled");
    }
});
Test("negative coordinates and chunk edges",()=>{
    for(int x=-65;x<=65;x++)for(int y=0;y<192;y+=7)for(int z=-33;z<33;z+=3){var p=new Cell(x,y,z);var c=Coordinates.Chunk(p);var o=Coordinates.Origin(c);int index=Coordinates.Index(p);Check(o.X+index%16==x&&o.Z+index/16%16==z&&o.Y+index/256==y,"coordinate roundtrip");}
});
Test("content identities and recipes",()=>{Check(catalog.Items.Count==206,"206 items including approved expansion");Check(catalog.BlockIds.Length==79,"78 blocks + air");Check(catalog.Recipes.Count==137,"137 recipes");foreach(var id in catalog.BlockIds)Check(catalog.Id(catalog.Number(id))==id,"stable ID");});
Test("combat shape checks follow doors, stairs, slabs and fences",()=>{
    var world=new WorldState(catalog,new(){Creative=true});var p=new Cell(300,80,300);
    Check(world.Place(p,"door",false,out _),"door setup");
    Check(!CombatGeometry.Clear(world,new(300.5f,80.6f,299.5f),new(300.5f,80.6f,301.5f)),"closed door passed projectile");
    world.State.Entities[p.ToString()].Open=true;
    Check(CombatGeometry.Clear(world,new(300.5f,80.6f,299.5f),new(300.5f,80.6f,301.5f)),"open doorway blocks projectile");
    Check(CombatGeometry.Blocks(world,new(300.94f,80.6f,300.5f)),"open panel has no collision");
    world.Set(p,catalog.Number("slab"));world.State.Entities.Remove(p.ToString());Check(CombatGeometry.Blocks(world,new(300.5f,80.3f,300.5f))&&!CombatGeometry.Blocks(world,new(300.5f,80.8f,300.5f)),"slab shape");
    world.Set(p,catalog.Number("stairs"));Check(CombatGeometry.Blocks(world,new(300.5f,80.8f,300.8f))&&!CombatGeometry.Blocks(world,new(300.5f,80.8f,300.2f)),"stair shape");
    world.Set(p,catalog.Number("fence"));Check(CombatGeometry.Blocks(world,new(300.5f,80.6f,300.5f))&&!CombatGeometry.Blocks(world,new(300.2f,80.6f,300.1f)),"fence post and gap");
    Check(CombatGeometry.InArea(new(2,37,0),new(0,37,0),3)&&!CombatGeometry.InArea(new(4,37,0),new(0,37,0),3),"slam dodge area");
});
Test("projectiles sweep walls, miss displaced targets and respect body height",()=>{
    var state=new WorldSnapshot{Position=[10.5,80,.5],GuardianDefeated=true};var world=new WorldState(catalog,state);
    ProjectileState Shot(bool hostile=true,float y=80.9f)=>new(){Position=[.5,y,.5],Velocity=[50,0,0],Remaining=2,Hostile=hostile};
    world.Set(new(4,80,0),catalog.Number("stone"));var shot=Shot();var hit=ProjectileFlight.Step(world,shot,.8,_=>true);Check(hit.Outcome==ProjectileOutcome.Blocked&&shot.Position[0]<5,"tunneled through wall at long delta");world.Set(new(4,80,0),0);
    state.Position[2]=3;shot=Shot();Check(ProjectileFlight.Step(world,shot,.3,_=>true).Outcome==ProjectileOutcome.Flying,"projectile tracked displaced target");state.Position[2]=.5;
    shot=Shot(y:81.5f);Check(ProjectileFlight.Step(world,shot,.3,_=>true,1.8f).Outcome==ProjectileOutcome.Player,"standing target missed");shot=Shot(y:81.5f);Check(ProjectileFlight.Step(world,shot,.3,_=>true,1.1f).Outcome==ProjectileOutcome.Flying,"crouch did not change body height");
    state.Creative=true;shot=Shot();Check(ProjectileFlight.Step(world,shot,.3,_=>true).Outcome==ProjectileOutcome.Absorbed,"creative target vulnerable");state.Creative=false;
    state.Entities["13,80,1"]=new(){Kind="campfire"};shot=Shot();Check(ProjectileFlight.Step(world,shot,.3,_=>true).Outcome==ProjectileOutcome.Absorbed,"safe camp target vulnerable");state.Entities.Clear();
    var enemy=new CreatureState{Kind="husk",Health=40,Position=[5.5,80,.5]};state.Creatures.Add(enemy);shot=Shot(false);hit=ProjectileFlight.Step(world,shot,.3,_=>true);Check(hit.Outcome==ProjectileOutcome.Creature&&ReferenceEquals(hit.Target,enemy),"arrow missed creature");
    shot=Shot();Check(ProjectileFlight.Step(world,shot,.3,_=>false).Outcome==ProjectileOutcome.Blocked,"projectile crossed unready terrain");
    shot=Shot();shot.Remaining=.01;Check(ProjectileFlight.Step(world,shot,.3,_=>true).Outcome==ProjectileOutcome.Expired,"projectile lifetime not bounded");
});
Test("pending combat and projectiles survive immutable save and reject malformed state",()=>{
    var state=new WorldSnapshot();state.Creatures.Add(new(){Kind="boar",Provoked=true,AttackWindup=.4,AttackAim=[1,37,2],AttackCooldown=2});state.Projectiles.Add(new(){Position=[1,80,2],Velocity=[10,0,0],Remaining=1.2});
    var frozen=state.Copy();state.Creatures[0].AttackAim![0]=9;state.Projectiles[0].Position[0]=9;Check(frozen.Creatures[0].AttackAim![0]==1&&frozen.Projectiles[0].Position[0]==1,"shared pending combat vectors");
    var store=new SaveStore(Path.Combine(root,"test-results","combat-save"),catalog);store.Save(frozen);var loaded=store.Load(frozen.WorldId).Snapshot;
    Check(loaded.Creatures[0].Provoked&&loaded.Creatures[0].AttackWindup==.4&&loaded.Projectiles[0].Remaining==1.2&&loaded.Projectiles[0].Velocity[0]==10,"pending combat reset on disk reload");
    foreach(Action<WorldSnapshot> invalid in new Action<WorldSnapshot>[]{s=>s.Creatures[0].AttackAim=null,s=>s.Creatures[0].ChargeRemaining=double.NaN,s=>s.Projectiles[0].Velocity=[0,0,0],s=>s.Projectiles[0].Remaining=double.NaN,s=>{while(s.Projectiles.Count<25)s.Projectiles.Add(new());}}){var bad=frozen.Copy();invalid(bad);bool rejected=false;try{new WorldState(catalog,bad);}catch(InvalidOperationException){rejected=true;}Check(rejected,"malformed combat accepted");}
    var old=JsonSerializer.Deserialize<WorldSnapshot>("{\"Creatures\":[{\"Kind\":\"boar\",\"Position\":[0,37,0],\"Health\":45}]}",Catalog.JsonOptions)!;new WorldState(catalog,old);Check(old.Projectiles.Count==0&&old.Creatures[0].AttackWindup== -1,"legacy combat defaults");
});
Test("creature definitions and population saves reject invalid state",()=>{
    CreatureRules.Validate(creatureDefs,catalog);
    foreach(var bad in new[]{creatureDefs[..5],creatureDefs.Select(d=>d.Id=="deer"?d with{Health=double.NaN}:d).ToArray(),creatureDefs.Select(d=>d.Id=="deer"?d with{Drop="missing"}:d).ToArray()}){bool rejected=false;try{CreatureRules.Validate(bad,catalog);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid creature definition accepted");}
    var state=new WorldSnapshot();state.Population.Sequence=7;var copy=state.Copy();state.Population.Sequence=8;Check(copy.Population.Sequence==7,"shared population state");
    foreach(Action<WorldSnapshot> invalid in new Action<WorldSnapshot>[] {s=>s.Population.NextSpawnTime=double.NaN,s=>s.Population.Sequence=-1,s=>s.Creatures.Add(new(){Kind="missing"}),s=>{s.Creatures.Add(new(){Kind="guardian"});s.Creatures.Add(new(){Kind="guardian"});}}){var s=new WorldSnapshot();invalid(s);bool rejected=false;try{new WorldState(catalog,s);}catch(InvalidOperationException){rejected=true;}Check(rejected,"invalid population save accepted");}
});
Test("guardian spawn clearance, defeat and empty reload persist",()=>{
    for(int seed=0;seed<100;seed++){
        var state=new WorldSnapshot{Seed=seed};var world=new WorldState(catalog,state);CreaturePopulation.Initialize(world,creatureDefs);
        Check(state.Population.Initialized&&state.Creatures.Count<=7&&state.Creatures.Count(c=>c.Kind=="guardian")==1,"initial population");
        var guardian=state.Creatures.Single(c=>c.Kind=="guardian");var p=new Cell((int)Math.Floor(guardian.Position[0]),(int)Math.Floor(guardian.Position[1]),(int)Math.Floor(guardian.Position[2]));
        Check(CreaturePopulation.CanStand(world,p,3),"guardian overlaps beacon or ceiling");
        state.GuardianDefeated=true;state.Creatures.Clear();var restored=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(state,Catalog.JsonOptions),Catalog.JsonOptions)!;var loaded=new WorldState(catalog,restored);CreaturePopulation.Initialize(loaded,creatureDefs);Check(restored.Creatures.Count==0,"empty cleared world reseeded on reload");
        loaded.Advance(12);CreaturePopulation.Tick(loaded,creatureDefs,new(0,37,0),_=>false);Check(restored.Creatures.Count==0,"defeated guardian respawned");
    }
    var old=JsonSerializer.Deserialize<WorldSnapshot>("{\"GuardianDefeated\":true}",Catalog.JsonOptions)!;var legacy=new WorldState(catalog,old);CreaturePopulation.Initialize(legacy,creatureDefs);Check(old.Creatures.All(c=>c.Kind!="guardian"),"legacy defeat lost");
});
Test("regional population obeys time, readiness, safe camps and cap",()=>{
    var state=new WorldSnapshot{GeneratorVersion=3};var world=new WorldState(catalog,state);CreaturePopulation.Initialize(world,creatureDefs);
    Check(CreaturePopulation.KindAt(world,new(0,37,0),0)=="deer","day starter wildlife");state.Time=900;Check(CreaturePopulation.KindAt(world,new(0,37,0),0)=="husk","night population");
    Check(CreaturePopulation.KindAt(world,new(0,10,0),0)=="skitter","cave population");var tower=world.Terrain.TowerLocations[0];Check(CreaturePopulation.KindAt(world,new(tower.X+2,world.Terrain.Surface(tower.X,tower.Z)+1,tower.Z),1)=="sentinel","ruin population");
    int before=state.Creatures.Count;state.Population.NextSpawnTime=state.Time+12;Check(CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>true).Added.Count==0&&state.Creatures.Count==before,"spawn advanced without world time");
    world.Advance(12);Check(CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>false).Added.Count==0&&state.Creatures.Count==before,"unloaded terrain populated");
    var p=new Cell(600,80,600);world.Set(p,0);world.Set(p+new Cell(0,1,0),0);world.Set(p+new Cell(0,-1,0),catalog.Number("stone"));Check(CreaturePopulation.CanStand(world,p),"clear standing space");world.Set(p,catalog.Number("water"));Check(!CreaturePopulation.CanStand(world,p),"spawn in water");world.Set(p,0);state.Entities[p.ToString()]=new(){Kind="campfire"};Check(CreaturePopulation.SafeCamp(world,p)&&!CreaturePopulation.SafeCamp(world,p+new Cell(8,0,0)),"camp safety range");
    for(int i=0;i<150;i++){world.Advance(12);var changes=CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>true);Check(changes.Added.Count<=2&&state.Creatures.Count<=48,"unbounded population");foreach(var c in changes.Added){var q=new Cell((int)Math.Floor(c.Position[0]),(int)Math.Floor(c.Position[1]),(int)Math.Floor(c.Position[2]));Check(CreaturePopulation.CanStand(world,q)&&!CreaturePopulation.SafeCamp(world,q),"invalid generated position");}}
    var clone=state.Copy();var loaded=new WorldState(catalog,clone);world.Advance(12);loaded.Advance(12);CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>true);CreaturePopulation.Tick(loaded,creatureDefs,new(0,37,0),_=>true);Check(JsonSerializer.Serialize(state.Creatures)==JsonSerializer.Serialize(clone.Creatures)&&state.Population.Sequence==clone.Population.Sequence,"reload changed next population decision");
    var boss=state.Creatures.Single(c=>c.Kind=="guardian");world.Advance(12);var retired=CreaturePopulation.Tick(world,creatureDefs,new(900,37,900),_=>false);Check(retired.Removed.Count>0&&state.Creatures.Contains(boss)&&state.Creatures.Count==1,"distant wildlife or guardian retirement");
});
Test("daylight is dark at night and continuous through dawn and dusk",()=>{
    Check(DayCycle.Daylight(300)==1&&DayCycle.Daylight(900)==0,"no midday/midnight contrast");
    foreach(double boundary in new[]{0d,600d,1200d})Check(Math.Abs(DayCycle.Daylight(boundary-.01)-DayCycle.Daylight(boundary+.01))<.001,"day boundary flashes");
    double previous=DayCycle.Daylight(540);for(double time=541;time<=650;time++){double next=DayCycle.Daylight(time);Check(next<=previous&&Math.Abs(next-previous)<.03,"abrupt sunset");previous=next;}
    Check(DayCycle.Twilight(600)>DayCycle.Twilight(300),"sunset color absent");
});
Test("day lasts ten minutes, night five and time partitions preserve sunrise",()=>{
    Check(DayCycle.Advance(0,600)==600&&DayCycle.Advance(600,299.9)<1200&&DayCycle.Advance(600,300)==1200,"day/night durations");
    Check(!DayCycle.Night(DayCycle.Advance(600,300))&&DayCycle.Advance(0,900)==1200&&DayCycle.Advance(0,2700)==3600,"cycle failed to return to day");
    foreach(double start in new[]{0d,599.9,900,1199.9,1000000}){double split=start;for(int i=0;i<9000;i++)split=DayCycle.Advance(split,.1);Check(Math.Abs(split-DayCycle.Advance(start,900))<.0001,"frame partition changed dawn");}
    var legacy=new WorldSnapshot{Time=1057.81};var w=new WorldState(catalog,legacy);w.Advance(70);Check(legacy.Time==1127.81&&DayCycle.Night(legacy.SkyTime),"legacy phase reset or real simulation doubled");
    w.Advance(2);Check(!DayCycle.Night(legacy.SkyTime),"legacy night never ended");
    var store=new SaveStore(Path.Combine(root,"test-results","solar-save"),catalog);store.Save(legacy);var loaded=store.Load(legacy.WorldId).Snapshot;Check(loaded.DayTime==legacy.DayTime&&loaded.Time==legacy.Time,"save reset solar phase");
    var restored=new WorldState(catalog,loaded);w.Advance(901);restored.Advance(901);Check(loaded.SkyTime==legacy.SkyTime,"reload changed cycle");
});
Test("wooden tools establish mining progression and tools wear without repairing on transfer",()=>{
    var stone=catalog.Items["stone"];var wood=catalog.Items["wood_pick"];var pick=catalog.Items["stone_pick"];var iron=catalog.Items["iron_pick"];
    Check(MiningRules.Seconds(stone,null)==8&&Math.Abs(MiningRules.Seconds(stone,wood)-1.2)<.001&&Math.Abs(MiningRules.Seconds(stone,pick)-.6)<.001,"stone mining speed");
    Check(!MiningRules.CanHarvest(stone,null)&&MiningRules.CanHarvest(stone,wood)&&!MiningRules.CanHarvest(catalog.Items["iron_ore"],wood)&&MiningRules.CanHarvest(catalog.Items["iron_ore"],pick)&&!MiningRules.CanHarvest(catalog.Items["crystal_ore"],pick)&&MiningRules.CanHarvest(catalog.Items["crystal_ore"],iron),"pick tiers");
    Check(!MiningRules.CanHarvest(stone,catalog.Items["iron_axe"])&&MiningRules.Seconds(catalog.Items["wood"],catalog.Items["wood_axe"])<MiningRules.Seconds(catalog.Items["wood"],wood),"wrong tool bypassed requirement");
    var inv=new Inventory();inv.Add("plank",3,catalog);inv.Add("stick",2,catalog);Check(inv.Craft(catalog.Recipes["wood_pick"],catalog,out _)&&inv.Count("wood_pick")==1&&inv.Count("plank")==0&&inv.Count("stick")==0,"wood recipe inaccessible");
    int slot=Array.FindIndex(inv.Slots,s=>s?.Id=="wood_pick");ToolUse.Wear(inv,slot,catalog,false);Check(inv.Slots[slot]!.Durability==59,"legacy null durability did not initialize on first use");
    var destination=new Inventory();Inventory.Move(inv,slot,destination,0,1,catalog);destination.Sort(catalog);Check(destination.Slots[0]!.Durability==59,"transfer repaired tool");
    ToolUse.Wear(destination,0,catalog,true,1000);Check(destination.Slots[0]!.Durability==59,"creative wore tool");Check(ToolUse.Wear(destination,0,catalog,false,59)&&destination.Slots[0]==null,"exhausted tool survived");
    var state=new WorldSnapshot();state.Inventory.Slots[0]=new("iron_pick",1,17);state.Equipment.Slots[4]=new("shield",1,29);var store=new SaveStore(Path.Combine(root,"test-results","tool-wear-save"),catalog);store.Save(state);var loaded=store.Load(state.WorldId).Snapshot;
    Check(loaded.Inventory.Slots[0]!.Durability==17&&loaded.Equipment.Slots[4]!.Durability==29,"reload repaired tools");
});
Test("duplicate world names retain separate metadata and valid save dates",()=>{
    var store=new SaveStore(Path.Combine(root,"test-results","world-cards-"+Guid.NewGuid().ToString("N")),catalog);
    var first=new WorldSnapshot{Name="我的营地",Seed=42,Time=900,Position=[12,40,-4]};var second=new WorldSnapshot{Name="我的营地",Seed=73,Creative=true};store.Save(first);store.Save(second);
    var list=store.ListDetails().ToArray();Check(list.Length==2&&list.Select(e=>e.Id).Distinct().Count()==2,"same-name worlds collapsed");
    var entry=list.Single(e=>e.Id==first.WorldId);Check(entry.Seed==42&&entry.Time==900&&entry.Position[0]==12&&entry.SavedUtc.HasValue&&!entry.Damaged,"wrong save card metadata");Check(list.Single(e=>e.Id==second.WorldId).Creative,"world mode lost");
});
Test("night and cave spawning occur, camps block candidates and saved sequence continues",()=>{
    var state=new WorldSnapshot{GeneratorVersion=3,Time=900,GuardianDefeated=true,Population=new(){Initialized=true}};var world=new WorldState(catalog,state);
    int added=0;for(int i=0;i<8;i++){world.Advance(12);added+=CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>true).Added.Count;}
    Check(added>0&&state.Creatures.Any(c=>c.Kind=="husk"),"night population never appeared");Check(state.Population.NextSpawnTime==state.Time+6,"night cadence unchanged");
    string storeRoot=Path.Combine(root,"test-results","population-save");var store=new SaveStore(storeRoot,catalog);store.Save(state);var restored=store.Load(state.WorldId).Snapshot;
    Check(restored.Population.Initialized&&restored.Population.Sequence==state.Population.Sequence&&restored.Population.NextSpawnTime==state.Population.NextSpawnTime,"save store lost population progress");
    var loaded=new WorldState(catalog,restored);world.Advance(12);loaded.Advance(12);CreaturePopulation.Tick(world,creatureDefs,new(0,37,0),_=>true);CreaturePopulation.Tick(loaded,creatureDefs,new(0,37,0),_=>true);Check(JsonSerializer.Serialize(state.Creatures)==JsonSerializer.Serialize(restored.Creatures),"disk reload changed next spawns");
    var caveState=new WorldSnapshot{GeneratorVersion=3,GuardianDefeated=true,Population=new(){Initialized=true}};var cave=new WorldState(catalog,caveState);
    for(int x=-44;x<=44;x++)for(int z=-44;z<=44;z++){cave.Set(new(x,19,z),catalog.Number("stone"));cave.Set(new(x,20,z),0);cave.Set(new(x,21,z),0);}
    added=0;for(int i=0;i<8;i++){cave.Advance(12);var change=CreaturePopulation.Tick(cave,creatureDefs,new(0,20,0),_=>true);Check(change.Added.All(c=>c.Kind=="skitter"),"wrong cave creature");added+=change.Added.Count;}
    Check(added>0,"cave population never appeared");
    var baseline=new WorldSnapshot{GeneratorVersion=3,Creative=true,GuardianDefeated=true,Population=new(){Initialized=true}};var open=new WorldState(catalog,baseline);var before=baseline.Copy();var candidates=CreaturePopulation.Tick(open,creatureDefs,new(0,37,0),_=>true);Check(candidates.Added.Count>0,"baseline candidate absent");
    var first=candidates.Added[0];var p=new Cell((int)Math.Floor(first.Position[0]),(int)Math.Floor(first.Position[1]),(int)Math.Floor(first.Position[2]));var camp=new WorldState(catalog,before);
    Check(camp.Place(p,"campfire",false,out _),"campfire fixture placement");var blocked=CreaturePopulation.Tick(camp,creatureDefs,new(0,37,0),_=>true);
    Check(blocked.Added.All(c=>!CreaturePopulation.SafeCamp(camp,new((int)Math.Floor(c.Position[0]),(int)Math.Floor(c.Position[1]),(int)Math.Floor(c.Position[2])))),"camp did not block actual candidates");
});
Test("audio waveforms avoid clipping, fade endpoints and bound synthesis allocation",()=>{
    var hashes=new HashSet<string>();
    foreach(string cue in new[]{"step_wood","step_stone","step_grass","step_sand","step_snow","step_water","pickup","hurt","death","craft","ui","warn_guardian","warn_skitter","wind","fire","music_day","music_night"}){
        long allocated=GC.GetAllocatedBytesForCurrentThread();byte[] data=EmberLighthouse.Audio.ProceduralAudio.Create(cue);allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(data.Length>=4000&&data.Length<=22050*2*18,"bounded clip duration");Check(allocated<2_000_000,"no per-sample array allocations");
        int peak=0;long square=0;for(int i=0;i<data.Length;i+=2){short s=(short)(data[i]|data[i+1]<<8);peak=Math.Max(peak,Math.Abs((int)s));square+=(long)s*s;}
        Check(peak<=26214&&Math.Sqrt(square/(data.Length/2))>100,"audible PCM with peak headroom");
        Check(Math.Abs((short)(data[0]|data[1]<<8))<100&&Math.Abs((short)(data[^2]|data[^1]<<8))<100,"no endpoint discontinuity");
        Check(hashes.Add(Convert.ToHexString(SHA256.HashData(data))),"distinct cues");
    }
});
Test("immutable save snapshot isolates nested mutable state",()=>{
    var state=new WorldSnapshot{Position=[1,42,3],Cursor=new("stone",7)};state.Inventory.Add("stone",20,catalog);state.Entities["1,42,3"]=new(){Kind="chest",Storage=new Inventory()};state.Entities["1,42,3"].Storage.Add("wood",4,catalog);state.Graves.Add(new(){Position=[2,40,5],Items=new Inventory()});state.Creatures.Add(new(){Position=[3,40,4]});state.Markers.Add([4,5]);state.Changes["1,42,3"]="plank";state.Beacons.Add(1);
    var copy=state.Copy();string original=JsonSerializer.Serialize(copy);state.Position[0]=9;state.Inventory.Remove("stone",7);state.Cursor.Count=1;state.Entities["1,42,3"].Storage.Remove("wood",2);state.Graves[0].Position[0]=8;state.Creatures[0].Position[0]=6;state.Markers[0][0]=10;state.Weather.Phase=2;state.Changes.Clear();state.Beacons.Clear();Check(JsonSerializer.Serialize(copy)==original,"background snapshot shares mutable state");
});
Test("surface sounds have four deterministic variations and bounded transients",()=>{
    foreach(string material in new[]{"grass","wood","stone","sand","snow","water"})foreach(string action in new[]{"step","hit","break","place"}){
        var hashes=new HashSet<string>();
        for(int variant=0;variant<4;variant++){
            byte[] data=EmberLighthouse.Audio.ProceduralAudio.Create(action+"_"+material,variant);
            Check(hashes.Add(Convert.ToHexString(SHA256.HashData(data))),"repeated variant");
            Check(data.SequenceEqual(EmberLighthouse.Audio.ProceduralAudio.Create(action+"_"+material,variant)),"non deterministic sound");
            int peak=0;long energy=0;for(int i=0;i<data.Length;i+=2){int pcm=(short)(data[i]|data[i+1]<<8);peak=Math.Max(peak,Math.Abs(pcm));energy+=(long)pcm*pcm;}
            Check(peak<26214&&Math.Sqrt(energy/(data.Length/2))>100,"silent or clipped material cue");
            Check((short)(data[0]|data[1]<<8)==0&&(short)(data[^2]|data[^1]<<8)==0,"impact discontinuity");
        }
    }
});
Test("fractional crop deadlines mature exactly and never before or regress",()=>{
    foreach(double step in new[]{60d,90d,120d})for(int sample=1;sample<=1000;sample++){
        var crop=new BlockEntity{LastTime=240+sample/1000d,GrowthStepSeconds=step};
        for(int stage=1;stage<=3;stage++){
            double deadline=crop.LastTime+step*stage;
            Check(CropGrowth.Stage(crop,deadline)==stage,"fractional exact deadline lagged");
            Check(CropGrowth.Stage(crop,Math.BitDecrement(deadline))==stage-1,"matured before representable deadline");
            Check(CropGrowth.Stage(crop,Math.BitIncrement(deadline))==stage,"deadline advancement");
        }
        crop.Stage=2;Check(CropGrowth.Stage(crop,crop.LastTime-1)==2,"clock rollback regressed crop");Check(CropGrowth.Stage(crop,double.MaxValue)==3,"large elapsed time failed");
    }
});
Test("crop cycles, old saves, reload and atomic harvest",()=>{
    var state=new WorldSnapshot();var w=new WorldState(catalog,state);
    string[] seeds=["carrot_seed","wheat_seed","potato_seed"];var positions=new List<Cell>();
    for(int i=0;i<3;i++){var ground=new Cell(400+i,80,400);var p=ground+new Cell(0,1,0);positions.Add(p);w.Set(ground,catalog.Number("farmland"));w.Set(p,0);state.Inventory.Add(seeds[i],1,catalog);Check(w.Plant(ground,seeds[i],out _),"plant");}
    Check(!w.Harvest(positions[0],out _),"unripe harvest accepted");w.Advance(180);
    Check(state.Entities[positions[0].ToString()].Stage==3&&state.Entities[positions[1].ToString()].Stage==2&&state.Entities[positions[2].ToString()].Stage==1,"distinct crop cycles");
    var clone=state.Copy();Check(clone.Entities[positions[2].ToString()].GrowthStepSeconds==120,"snapshot lost cycle");
    var restored=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(clone,Catalog.JsonOptions),Catalog.JsonOptions)!;var loaded=new WorldState(catalog,restored);
    w.Advance(90);loaded.Advance(90);Check(restored.Entities[positions[1].ToString()].Stage==3&&restored.Entities[positions[2].ToString()].Stage==2,"reload lost elapsed growth");
    Check(JsonSerializer.Serialize(state.Entities)==JsonSerializer.Serialize(restored.Entities),"reload changed crop stages");
    restored.Inventory=new();for(int i=0;i<36;i++)restored.Inventory.Slots[i]=new("stone",64);string before=JsonSerializer.Serialize(restored);
    Check(loaded.Harvest(positions[0],out _)&&restored.Inventory.Count("stone")==36*64&&restored.Graves.Sum(g=>g.Items.Count("carrot"))==3&&loaded.Get(positions[0])==0,"full harvest lost ground drops");
    loaded.Advance(.5);var at=positions[0];Check(loaded.PickTouchingItems([at.X+.5,at.Y,at.Z+.5])==0,"full backpack collected items");
    restored.Inventory=new(1);restored.Inventory.Add("carrot",61,catalog);Check(loaded.PickTouchingItems([at.X+.5,at.Y,at.Z+.5])==3&&restored.Graves.Sum(g=>g.Items.Count("carrot_seed"))==2,"seed overflow lost items");
    var partial=restored.Inventory.Slots[0]!.Copy();restored.Inventory=new();restored.Inventory.Slots[0]=partial;Check(loaded.PickTouchingItems([at.X+.5,at.Y,at.Z+.5])==2&&restored.Inventory.Count("carrot")==64&&restored.Inventory.Count("carrot_seed")==2,"ground harvest conservation");
    Check(!loaded.Harvest(positions[0],out _),"harvest duplicated items");loaded.Advance(90);Check(restored.Entities[positions[2].ToString()].Stage==3,"potato never ripe");
    foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1}){before=JsonSerializer.Serialize(restored);bool rejected=false;try{loaded.Advance(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected&&JsonSerializer.Serialize(restored)==before,"invalid time mutated world");}
    var legacy=JsonSerializer.Deserialize<BlockEntity>("{\"Kind\":\"crop\",\"CropId\":\"carrot_seed\",\"LastTime\":0}",Catalog.JsonOptions)!;
    Check(legacy.GrowthStepSeconds==90&&CropGrowth.Stage(legacy,180)==2&&CropGrowth.Stage(legacy,270)==3,"old crop timing changed");
});
Test("save queue is bounded, coalesces complete snapshots and recovers after failure",()=>{
    using var started=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var recorded=new ConcurrentQueue<WorldSnapshot>();int calls=0,writers=0,peak=0;
    var queue=new SaveQueue(snapshot=>{int current=Interlocked.Increment(ref writers);peak=Math.Max(peak,current);try{if(Interlocked.Increment(ref calls)==1){started.Set();if(!release.Wait(10000))throw new Exception("queue release timeout");}recorded.Enqueue(snapshot);return "test-generation";}finally{Interlocked.Decrement(ref writers);}});
    var state=new WorldSnapshot();var first=queue.Enqueue(state);Check(started.Wait(10000),"worker not started");Task<SaveReceipt>? last=null;var tasks=new HashSet<Task<SaveReceipt>>{first};
    for(int i=1;i<=100;i++){state.Inventory.Slots[0]=new("stone",i%64+1);state.Changes["0,70,0"]=i%2==0?"plank":"brick";state.Time=i;last=queue.Enqueue(state);tasks.Add(last);Check(queue.BufferedSnapshots==2,"save queue grew past active plus pending");}
    release.Set();Check(Task.WaitAll([first,last!],10000),"save queue hung");var snapshots=recorded.ToArray();Check(tasks.Count==2&&snapshots.Length==2&&snapshots[0].Time==240&&snapshots[1].Time==100&&snapshots[1].Inventory.Slots[0]!.Count==37&&peak==1&&queue.BufferedSnapshots==0,"save ordering or coherent snapshot changed");
    int attempts=0;var retry=new SaveQueue(_=>{if(Interlocked.Increment(ref attempts)==1)throw new IOException("injected disk failure");return "recovered";});bool failed=false;try{retry.Enqueue(state).GetAwaiter().GetResult();}catch(IOException){failed=true;}Check(failed&&retry.Enqueue(state).GetAwaiter().GetResult().Generation=="recovered","queue stuck after write failure");
});
Test("compressed sparse and dense chunks preserve cells and stable IDs",()=>{
    var terrain=new Terrain(31,catalog);var chunk=new Cell(-2,3,2);var origin=Coordinates.Origin(chunk);var edits=new Dictionary<Cell,string>();
    for(int i=0;i<200;i++)edits[origin+new Cell(i%16,i/256,i/16%16)]=i%3==0?"brick":"plank";
    var encoded=ChunkCodec.Encode(chunk,edits,terrain,catalog);Check(encoded.Encoding=="sparse"&&encoded.ModifiedCount==200,"sparse selection");var decoded=ChunkCodec.Decode(encoded.Bytes,chunk,terrain,catalog,encoded.Encoding,encoded.ModifiedCount);Check(edits.All(p=>decoded[p.Key]==p.Value),"sparse cells changed");
    for(int i=200;i<8192;i++)edits[origin+new Cell(i%16,i/256,i/16%16)]=i%3==0?"brick":"plank";encoded=ChunkCodec.Encode(chunk,edits,terrain,catalog);Check(encoded.Encoding=="dense"&&encoded.ModifiedCount==8192&&encoded.Bytes.Length<2048,"dense snapshot not compressed");decoded=ChunkCodec.Decode(encoded.Bytes,chunk,terrain,catalog,encoded.Encoding,encoded.ModifiedCount);Check(decoded.Count==8192&&edits.All(p=>decoded[p.Key]==p.Value),"dense cells changed");
    bool wrongSeed=false;try{ChunkCodec.Decode(encoded.Bytes,chunk,new Terrain(32,catalog),catalog,"dense",8192);}catch(InvalidDataException){wrongSeed=true;}Check(wrongSeed,"chunk used with wrong world seed");
});
Test("weather timeline, climate and save continuation",()=>{
    var a=new WorldSnapshot{Seed=14};var b=new WorldSnapshot{Seed=14};var wa=new WorldState(catalog,a);var wb=new WorldState(catalog,b);
    wa.Advance(2000);for(int i=0;i<2000;i++)wb.Advance(1);
    Check(a.Weather.Phase==b.Weather.Phase&&a.Weather.Cycle==b.Weather.Cycle&&Math.Abs(a.Weather.Remaining-b.Weather.Remaining)<.001&&Math.Abs(a.Weather.Blend-b.Weather.Blend)<.001,"frame partition changed weather");
    var old=JsonSerializer.Deserialize<WorldSnapshot>("{\"Seed\":14}")!;new WorldState(catalog,old);Check(old.Weather.Phase==0,"old saves need clear weather default");
    var restored=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(a))!;var wr=new WorldState(catalog,restored);wa.Advance(700);wr.Advance(700);Check(JsonSerializer.Serialize(a.Weather)==JsonSerializer.Serialize(restored.Weather),"weather did not continue after reload");
    var wet=new WeatherState{Phase=2};Check(wet.Describe(3)=="雪"&&wet.Describe(1)=="雨"&&wet.Describe(2)=="阴","climate mismatch");
});
Test("recipe and station progression has no circular resource lock",()=>{
    var obtainable=new HashSet<string>{"wood","stone","berry","leaves","dirt","sand","snow","clay","gravel","ice","cactus","moss","meat","leather","grain","carrot","potato","copper_ore"};
    obtainable.UnionWith(["brown_mushroom","red_mushroom","red_flower","blue_flower","reed","sugar_cane","pumpkin_seed","melon_seed","apple","bone","feather","string","gunpowder","ink_sac","raw_cod","egg","turtle_scute","armadillo_scute","wool"]);
    var unlocked=new HashSet<string>();bool changed=true;
    while(changed){changed=false;if(obtainable.Contains("stone_pick")){changed|=obtainable.Add("raw_iron");changed|=obtainable.Add("coal");changed|=obtainable.Add("basalt");}if(obtainable.Contains("iron_pick")){changed|=obtainable.Add("crystal");changed|=obtainable.Add("gold_ore");}if(obtainable.Contains("glass_bottle"))changed|=obtainable.Add("water_bottle");if(obtainable.Contains("pumpkin_seed"))changed|=obtainable.Add("pumpkin");if(obtainable.Contains("melon_seed"))changed|=obtainable.Add("melon");
        foreach(var recipe in catalog.Recipes.Values)if(!unlocked.Contains(recipe.Id)&&(recipe.Station=="hand"||obtainable.Contains(recipe.Station))&&recipe.Inputs.Keys.All(obtainable.Contains)){unlocked.Add(recipe.Id);obtainable.Add(recipe.Output);changed=true;}
    }
    Check(unlocked.Count==catalog.Recipes.Count,"unreachable recipes: "+string.Join(",",catalog.Recipes.Keys.Except(unlocked)));
    Check(obtainable.Contains("beacon_sigil")&&obtainable.Contains("crystal_pick"),"objective progression blocked");
});
Test("100 seeds deterministic terrain and guaranteed objectives",()=>{
    for(int seed=0;seed<100;seed++){
        var a=new Terrain(seed,catalog);var b=new Terrain(seed,catalog);
        Check(a.Surface(0,0)==36&&a.Get(new(0,36,0))!=0,"spawn floor");
        Check(a.StarterResources.Values.Count(id=>id=="loose_stone")==6,"guaranteed scattered stone");
        Check(a.StarterResources.Values.Count(id=>id=="wood")==6&&a.StarterResources.All(e=>catalog.Id(a.Get(new(e.Key.X,a.Surface(e.Key.X,e.Key.Z)+1,e.Key.Z)))==e.Value),"guaranteed scattered supplies");
        Check(catalog.Id(a.Get(new(34,38,26)))!="chest","unrequested starter chest");
        for(int i=0;i<300;i++){var p=new Cell(i*13%100-50,i%70,i*7%100-50);Check(a.Get(p)==b.Get(p),"determinism");}
        foreach(var t in a.TowerLocations){int y=a.Surface(t.X,t.Z);Check(catalog.Id(a.Get(new(t.X,y+1,t.Z)))=="beacon","guaranteed beacon");Check(a.Get(new(t.X,y+1,t.Z+4))==0,"accessible tower doorway");}
    }
});
Test("random seeds vary terrain and reload reproduces each generated world",()=>{
    var signatures=new HashSet<string>();int? previous=null;
    for(int i=0;i<12;i++){
        int seed=WorldSeeds.Random(previous);Check(seed!=previous,"random seed immediately repeated");previous=seed;var terrain=new Terrain(seed,catalog);var samples=new List<int>();
        for(int x=-192;x<=192;x+=24)for(int z=-192;z<=192;z+=24){samples.Add(terrain.Surface(x,z));samples.Add(terrain.Biome(x,z));}
        Check(signatures.Add(string.Join(",",samples)),"different random seeds produced identical sampled terrain");
        var state=new WorldSnapshot{Seed=seed};var restored=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(state,Catalog.JsonOptions),Catalog.JsonOptions)!;var copy=new WorldState(catalog,restored);
        Check(copy.State.Seed==seed&&copy.Terrain.Generate(new(3,0,3)).SequenceEqual(terrain.Generate(new(3,0,3))),"stored seed changed generated blocks");
    }
});
Test("greedy faces, neighbor halo and partial collision",()=>{
    ushort stone=catalog.Number("stone");Cell origin=new(0,0,0);
    var cube=VoxelMesher.Build(p=>p==origin?stone:(ushort)0,origin,catalog);Check(cube.Quads==6&&cube.Collision.Count==108,"single cube faces");
    var pair=VoxelMesher.Build(p=>p==origin||p==new Cell(1,0,0)?stone:(ushort)0,origin,catalog);Check(pair.Quads==6,"greedy merge");
    var boundary=VoxelMesher.Build(p=>p==new Cell(15,0,0)||p==new Cell(16,0,0)?stone:(ushort)0,origin,catalog);Check(boundary.Quads==5,"cross-chunk hidden face");
    var slab=VoxelMesher.Build(p=>p==origin?catalog.Number("slab"):(ushort)0,origin,catalog);Check(slab.Vertices.Max(v=>v.Y)==.5f&&slab.Collision.Count>0,"slab geometry");
});
Test("timed furnace completion is atomic under full storage",()=>{
    var state=new WorldSnapshot();var e=new BlockEntity{Kind="furnace",Storage=new Inventory(1),RecipeId="iron_ingot",FinishTime=250};e.Storage.Add("iron_ingot",63,catalog);state.Entities["1,80,1"]=e;var w=new WorldState(catalog,state);w.Advance(20);Check(e.Storage.Count("iron_ingot")==63&&e.RecipeId!=null,"partial output duplicated");e.Storage.Remove("iron_ingot",1);w.Advance(1);Check(e.Storage.Count("iron_ingot")==64&&e.RecipeId==null,"completion failed");
});
Test("loose wood lies on terrain and does not turn placed timber into logs",()=>{
    var w=new WorldState(catalog,new(){GeneratorVersion=4});var p=new Cell(-3,37,4);Check(w.Terrain.IsLooseWood(p)&&catalog.Id(w.Get(p))=="wood"&&!w.Solid(p),"starter log classification");
    var mesh=VoxelMesher.Build(c=>c==new Cell(0,0,0)?catalog.Number("wood"):(ushort)0,new(0,0,0),catalog,groundLog:c=>c==new Cell(0,0,0));Check(mesh.Vertices.Max(v=>v.Y)<=.31f&&mesh.Collision.Count==0,"ground wood still full cube");
    w.Set(new(0,50,0),catalog.Number("wood"));Check(w.Solid(new(0,50,0))&&!w.Terrain.IsLooseWood(new(0,50,0)),"placed wood changed shape");Check(!w.Terrain.IsLooseWood(new(-3,38,4)),"suspended log classification");
    w.State.Inventory.Add("wood",1,catalog);Check(w.Break(p,0,out _)&&w.Place(p,"wood",false,out _)&&w.Solid(p)&&w.State.Entities[p.ToString()].Kind=="wood","wood placed at starter position became decorative log");Check(new WorldState(catalog,w.State.Copy()).Solid(p),"placed starter-position wood changed shape after reload");
});
Test("crop appearance revisions and breaking returns seeds",()=>{
    var state=new WorldSnapshot();var w=new WorldState(catalog,state);var p=new Cell(4,70,4);w.Set(p,catalog.Number("berry_bush"));state.Entities[p.ToString()]=new(){Kind="crop",CropId="carrot_seed",LastTime=state.Time};int revision=w.Revision(Coordinates.Chunk(p));w.Advance(100);
    Check(state.Entities[p.ToString()].Stage==1&&w.Revision(Coordinates.Chunk(p))>revision,"growth did not invalidate mesh");Check(w.Break(p,0,out _)&&state.Graves.Sum(g=>g.Items.Count("carrot_seed"))==1&&state.Inventory.Count("carrot_seed")==0&&state.Inventory.Count("berry")==0,"breaking crop produced berries");
    var bush=VoxelMesher.Build(c=>c==new Cell(0,0,0)?catalog.Number("berry_bush"):(ushort)0,new(0,0,0),catalog);Check(bush.Vertices.Count==0&&bush.PlantVertices.Count==8&&bush.PlantIndices.Count==12&&bush.Collision.Count==0,"bush is still a box");
});
Test("inventory transactions and randomized conservation",()=>{
    var a=new Inventory(36);var b=new Inventory(27);a.Add("stone",500,catalog);a.Add("wood",200,catalog);var random=new Random(62);
    for(int i=0;i<10000;i++){Inventory.Move(random.Next(2)==0?a:b,random.Next(27),random.Next(2)==0?a:b,random.Next(27),random.Next(1,65),catalog);a.Validate(catalog);b.Validate(catalog);Check(a.Count("stone")+b.Count("stone")==500&&a.Count("wood")+b.Count("wood")==200,"item duplication/loss");}
    var full=new Inventory(1);full.Add("stone",64,catalog);string before=JsonSerializer.Serialize(full);Check(!full.Craft(catalog.Recipes["plank"],catalog,out _),"missing ingredient accepted");Check(before==JsonSerializer.Serialize(full),"failed craft changed state");
    var i2=new Inventory(1);i2.Add("wood",64,catalog);before=JsonSerializer.Serialize(i2);Check(!i2.Craft(catalog.Recipes["plank"],catalog,out _),"overflow accepted");Check(before==JsonSerializer.Serialize(i2),"overflow lost input");
});
Test("2x2 crafting output transaction and save persistence",()=>{
    var source=new Inventory(4);source.Add("wood",2,catalog);var full=new Inventory(1);full.Add("stone",64,catalog);string before=JsonSerializer.Serialize(source);
    Check(!Inventory.CraftTo(source,full,catalog.Recipes["plank"],catalog,out _)&&JsonSerializer.Serialize(source)==before,"grid overflow consumed input");
    var target=new Inventory();Check(Inventory.CraftTo(source,target,catalog.Recipes["plank"],catalog,out _)&&source.Count("wood")==1&&target.Count("plank")==4,"grid crafting failed");
    var state=new WorldSnapshot{Crafting=source,Experience=17};var clone=JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(state))!;Check(clone.Crafting.Count("wood")==1&&clone.Experience==17,"grid state lost on save");
});
Test("Minecraft left/right clicks conserve items and split correctly",()=>{
    var inventory=new Inventory(36);inventory.Add("stone",255,catalog);inventory.Add("wood",127,catalog);Stack? cursor=null;
    Check(Inventory.Click(inventory,3,cursor,true,catalog,out cursor)&&cursor!.Count==32&&inventory.Slots[3]!.Count==31,"right click must take rounded-up half");
    Check(Inventory.Click(inventory,8,cursor,true,catalog,out cursor)&&inventory.Slots[8]!.Count==1&&cursor!.Count==31,"right click must place one");
    var random=new Random(77);for(int i=0;i<10000;i++){Inventory.Click(inventory,random.Next(36),cursor,random.Next(2)==0,catalog,out cursor);inventory.Validate(catalog);Check(inventory.Count("stone")+(cursor?.Id=="stone"?cursor.Count:0)==255&&inventory.Count("wood")+(cursor?.Id=="wood"?cursor.Count:0)==127,"cursor item loss/duplication");}
});
Test("cross-boundary revisions and block transaction",()=>{
    var s=new WorldSnapshot();s.Inventory.Add("plank",2,catalog);var w=new WorldState(catalog,s);var p=new Cell(-1,80,15);
    Check(w.Place(p,"plank",false,out _),"place failed");Check(w.Revision(new(-1,2,0))>0&&w.Revision(new(0,2,0))>0&&w.Revision(new(-1,2,1))>0,"neighbor revision");
    Check(!w.Place(p,"plank",false,out _)&&s.Inventory.Count("plank")==1,"duplicate placement");Check(w.Break(p,1,out _)&&s.Inventory.Count("plank")==1&&s.Graves.Sum(g=>g.Items.Count("plank"))==1,"break conservation");
});
Test("chunk-scoped edit snapshots keep halo, isolate changes and remove reverted cells",()=>{
    var w=new WorldState(catalog,new());Cell[] near=[new(-1,64,-1),new(0,65,0),new(16,65,16),new(-16,63,0)];foreach(var p in near)w.Set(p,catalog.Number("plank"));var far=new Cell(1000,100,-900);w.Set(far,catalog.Number("brick"));
    var snapshot=w.CaptureChunk(new(0,2,0));Check(near.All(snapshot.ContainsKey)&&!snapshot.ContainsKey(far),"chunk halo snapshot includes wrong cells");w.Set(near[0],catalog.Number("glass"));Check(snapshot[near[0]]==catalog.Number("plank"),"snapshot changed after capture");w.Set(near[0],w.Terrain.Get(near[0]));Check(!w.CaptureChunk(new(0,2,0)).ContainsKey(near[0]),"reverted edit stayed indexed");var map=w.CaptureRegion(0,0,12);Check(map.ContainsKey(near[1])&&!map.ContainsKey(near[2])&&!map.ContainsKey(far),"map region snapshot incorrect");
});
Test("two-cell doors and crops reject invalid placement without item loss",()=>{
    for(int rotation=0;rotation<4;rotation++){
        var mesh=VoxelMesher.Build(p=>p==new Cell(0,0,0)?catalog.Number("door"):(ushort)0,new(0,0,0),catalog,doorOpen:_=>true,rotation:_=>rotation);
        Check(mesh.Collision.Count>0,"open door lost physical panel");float[] xs=mesh.Collision.Where((_,i)=>i%3==0).ToArray(),zs=mesh.Collision.Where((_,i)=>i%3==2).ToArray();
        Check(rotation%2==0?xs.Max()-xs.Min()<.16f&&zs.Max()-zs.Min()>.99f:zs.Max()-zs.Min()<.16f&&xs.Max()-xs.Min()>.99f,"open door collision did not rotate with panel");
    }
    var state=new WorldSnapshot();state.Inventory.Add("door",3,catalog);state.Inventory.Add("wheat_seed",2,catalog);var w=new WorldState(catalog,state);
    var top=new Cell(0,191,0);Check(!w.Place(top,"door",false,out _)&&state.Inventory.Count("door")==3&&w.Changes.Count==0&&state.Entities.Count==0,"top boundary consumed door or left half");
    var p=new Cell(-1,31,-1);var upper=p+new Cell(0,1,0);w.Set(p,0);w.Set(upper,catalog.Number("stone"));int edits=w.Changes.Count;
    Check(!w.Place(p,"door",false,out _)&&state.Inventory.Count("door")==3&&w.Changes.Count==edits,"blocked upper space consumed door");w.Set(upper,0);
    Check(!w.Place(p,"door",false,out _,2,true)&&state.Inventory.Count("door")==3,"upper player collision consumed door");
    Check(w.Place(p,"door",false,out _,2)&&state.Inventory.Count("door")==2&&w.DoorPartner(p)==upper&&w.DoorPartner(upper)==p&&state.Entities[upper.ToString()].Rotation==2,"cross-boundary door incomplete");
    var loaded=new WorldState(catalog,state.Copy());Check(loaded.DoorPartner(upper)==p,"door pair lost in snapshot");
    state.Inventory=new();for(int i=0;i<state.Inventory.Slots.Length;i++)state.Inventory.Slots[i]=new("stone",catalog.Items["stone"].Stack);
    Check(w.Break(upper,0,out _)&&w.Get(p)==0&&w.Get(upper)==0&&state.Graves.Sum(g=>g.Items.Count("door"))==1&&state.Inventory.Count("stone")==36*64,"full backpack lost door drop");
    state.Inventory=new();w.Advance(.5);Check(w.PickTouchingItems([upper.X+.5,upper.Y,upper.Z+.5])==1&&state.Inventory.Count("door")==1&&!state.Entities.ContainsKey(p.ToString())&&!state.Entities.ContainsKey(upper.ToString()),"door drop duplicated item or left half");
    state.Inventory.Add("wheat_seed",2,catalog);w.Set(top,catalog.Number("farmland"));Check(!w.Plant(top,"wheat_seed",out _)&&state.Inventory.Count("wheat_seed")==2,"top crop consumed seed");
    var ground=new Cell(15,31,15);var crop=ground+new Cell(0,1,0);w.Set(ground,catalog.Number("farmland"));w.Set(crop,catalog.Number("stone"));Check(!w.Plant(ground,"wheat_seed",out _)&&state.Inventory.Count("wheat_seed")==2,"blocked crop consumed seed");
    w.Set(crop,0);Check(w.Plant(ground,"wheat_seed",out _)&&state.Inventory.Count("wheat_seed")==1&&state.Entities[crop.ToString()].CropId=="wheat_seed","valid planting failed");Check(w.Break(crop,0,out _)&&state.Inventory.Count("wheat_seed")==1&&state.Graves.Sum(g=>g.Items.Count("wheat_seed"))==1,"crop break lost seed");
});
Test("multiple death backpacks persist",()=>{
    var s=new WorldSnapshot();var w=new WorldState(catalog,s);s.Inventory.Add("stone",12,catalog);w.Die();s.Inventory.Add("wood",5,catalog);w.Die();Check(s.Graves.Count==2&&s.Graves[0].Items.Count("stone")==12&&s.Graves[1].Items.Count("wood")==5,"previous grave deleted");
});
string testRoot=Path.Combine(root,"test-results","saves-"+Guid.NewGuid().ToString("N"));
Test("published generator1 world retains terrain, edited chunks and exploration through save and backup",()=>{
    string fixture=Path.Combine(root,"tests/fixtures/published-format3/worlds"),target=Path.Combine(testRoot,"published-generator1/worlds");
    foreach(string file in Directory.GetFiles(fixture,"*",SearchOption.AllDirectories)){string output=Path.Combine(target,Path.GetRelativePath(fixture,file));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output);}
    var store=new SaveStore(target,catalog);string id=Directory.GetDirectories(target).Select(Path.GetFileName).Single()!;var loaded=store.Load(id);Check(loaded.Snapshot.GeneratorVersion==1&&loaded.Snapshot.ExploredMap.Count==0,"published save changed generation or fabricated exploration");
    var world=new WorldState(catalog,loaded.Snapshot);var edits=world.State.Changes.ToDictionary(p=>p.Key,p=>world.Get(Cell.Parse(p.Key)));int[] surface=Enumerable.Range(-60,120).Select(x=>world.Terrain.Surface(x,44)).ToArray();world.RevealMap(world.State.Position[0],world.State.Position[2]);store.Save(world.State);
    var reloaded=new WorldState(catalog,store.Load(id).Snapshot);Check(reloaded.Terrain.GeneratorVersion==1&&surface.SequenceEqual(Enumerable.Range(-60,120).Select(x=>reloaded.Terrain.Surface(x,44)))&&edits.All(p=>reloaded.Get(Cell.Parse(p.Key))==p.Value)&&reloaded.State.ExploredMap.SetEquals(world.State.ExploredMap),"legacy terrain/edits/exploration changed");
    string backup=store.Backup(id);var entry=store.ListBackups().Single(b=>b.Key==Path.GetFileName(backup));var restored=new WorldState(catalog,store.Load(store.RestoreBackup(entry.Key)).Snapshot);Check(restored.State.ExploredMap.SetEquals(world.State.ExploredMap)&&restored.Terrain.GeneratorVersion==1,"backup loses exploration/generator");
    var oldTerrain=new Terrain(42,catalog,1);var chunk=new Cell(0,1,0);var changes=new Dictionary<Cell,string>{{new(1,50,1),"glass"}};var encoded=ChunkCodec.Encode(chunk,changes,oldTerrain,catalog);bool refused=false;try{ChunkCodec.Decode(encoded.Bytes,chunk,new Terrain(42,catalog,2),catalog,encoded.Encoding,encoded.ModifiedCount);}catch(InvalidDataException){refused=true;}Check(refused,"legacy chunks decoded against new terrain");
});
Test("save generations, checksum recovery, backup, future version",()=>{
    var store=new SaveStore(testRoot,catalog);var s=new WorldSnapshot();s.Inventory.Add("stone",10,catalog);store.Save(s);s.Changes["1,80,1"]="glass";string newest=store.Save(s);
    Check(store.Load(s.WorldId).Snapshot.Changes["1,80,1"]=="glass","roundtrip");Check(Directory.Exists(store.Backup(s.WorldId)),"backup");
    File.WriteAllText(Path.Combine(testRoot,s.WorldId,newest+".json"),"corrupt");var recovered=store.Load(s.WorldId);Check(recovered.Recovered&&recovered.Snapshot.Changes.Count==0&&recovered.Snapshot.Inventory.Count("stone")==10,"checksum recovery");
    s.FormatVersion=99;bool refusedWrite=false;try{store.Save(s);}catch(UnsupportedSaveException){refusedWrite=true;}Check(refusedWrite,"future snapshot written");WriteFixture(testRoot,s);byte[] future=File.ReadAllBytes(Path.Combine(testRoot,s.WorldId,"CURRENT"));
    bool rejected=false;try{store.Load(s.WorldId);}catch(UnsupportedSaveException){rejected=true;}Check(rejected,"future version loaded");s.FormatVersion=WorldSnapshot.CurrentFormat;try{store.Save(s);throw new Exception("future world overwritten");}catch(UnsupportedSaveException){}Check(File.ReadAllBytes(Path.Combine(testRoot,s.WorldId,"CURRENT")).SequenceEqual(future),"future pointer changed");
});
Test("process termination at every save commit boundary",()=>{
    foreach(string point in new[]{"chunk-written","chunk-committed","state-written","manifest-written","generation-committed","pointer-written","pointer-committed"}){
        var path=Path.Combine(testRoot,point);var store=new SaveStore(path,catalog);var s=new WorldSnapshot();s.Inventory.Add("stone",10,catalog);s.Equipment.Slots[0]=new("iron_helmet",1,100);s.Equipment.Slots[4]=new("plank",12);store.Save(s);
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(Inventory).Assembly.Location);
        foreach(var arg in new[]{"fault-child",path,s.WorldId,point})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;Check(process.WaitForExit(15000),"fault process hung");Check(process.ExitCode==71,"fault not injected");
        var loaded=store.Load(s.WorldId).Snapshot;bool before=loaded.Inventory.Count("crystal")==0&&loaded.Changes.Count==0&&loaded.Equipment.Slots[0]?.Durability==100&&loaded.Equipment.Slots[4]?.Id=="plank"&&loaded.Equipment.Slots[4]?.Count==12;bool after=loaded.Inventory.Count("crystal")==5&&loaded.Changes["1,50,1"]=="plank"&&loaded.Equipment.Slots[0]?.Durability==80&&loaded.Equipment.Slots[4]?.Id=="stone"&&loaded.Equipment.Slots[4]?.Count==3;Check(before||after,"mixed save generation including armor and offhand");
    }
});
Test("legacy migration keeps raw original and creates a durable backup",()=>{
    string legacyRoot=Path.Combine(testRoot,"legacy","worlds");var store=new SaveStore(legacyRoot,catalog);var state=new WorldSnapshot{FormatVersion=1,Seed=73,Name="legacy"};state.Inventory.Add("stone",15,catalog);state.Changes["-1,36,-1"]="air";state.Changes["17,70,-18"]="glass";WriteFixture(legacyRoot,state,generation:"20000101000000-legacy");
    string rawFile=Path.Combine(legacyRoot,state.WorldId,"20000101000000-legacy.json");byte[] raw=File.ReadAllBytes(rawFile);var loaded=store.Load(state.WorldId);Check(loaded.Migrated&&loaded.Snapshot.FormatVersion==WorldSnapshot.CurrentFormat&&loaded.Snapshot.Inventory.Count("stone")==15&&loaded.Snapshot.Changes["17,70,-18"]=="glass","legacy migration changed state");Check(File.ReadAllBytes(rawFile).SequenceEqual(raw),"load rewrote legacy file");
    store.Save(loaded.Snapshot);Check(store.ListBackups().Single().Name=="legacy"&&File.ReadAllBytes(rawFile).SequenceEqual(raw),"migration original not retained");Check(!store.Load(state.WorldId).Migrated,"new format still flagged migrated");string restored=store.RestoreBackup(store.ListBackups().Single().Key);Check(store.Load(restored).Snapshot.Inventory.Count("stone")==15,"legacy backup cannot restore");
});
Test("format2 compressed worlds migrate with raw chunk backup and preserve gameplay",()=>{
    string migrationRoot=Path.Combine(testRoot,"format2","worlds");var store=new SaveStore(migrationRoot,catalog);
    var state=new WorldSnapshot{FormatVersion=2,Name="format2 migration",Seed=73,GuardianDefeated=true};state.Inventory.Add("iron_ingot",6,catalog);state.Beacons.Add(1);state.Population=new(){Initialized=true,Sequence=23,NextSpawnTime=252};state.Weather.Phase=1;
    var cell=new Cell(17,70,-18);state.Entities[cell.ToString()]=new(){Kind="chest"};state.Entities[cell.ToString()].Storage.Add("wood",11,catalog);
    var chunk=Coordinates.Chunk(cell);var encoded=ChunkCodec.Encode(chunk,new Dictionary<Cell,string>{{cell,"chest"}},new Terrain(state.Seed,catalog),catalog);string chunkHash=Convert.ToHexString(SHA256.HashData(encoded.Bytes));
    string path=Path.Combine(migrationRoot,state.WorldId);Directory.CreateDirectory(Path.Combine(path,"chunks"));File.WriteAllBytes(Path.Combine(path,"chunks",chunkHash+".chunk"),encoded.Bytes);
    var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(state,Catalog.JsonOptions))!.AsObject();node.Remove("Projectiles");byte[] raw=JsonSerializer.SerializeToUtf8Bytes(node,Catalog.JsonOptions);const string generation="20000102000000-format2";
    File.WriteAllBytes(Path.Combine(path,generation+".json"),raw);var manifest=new SaveManifest(2,generation,Convert.ToHexString(SHA256.HashData(raw)),DateTime.UtcNow,new(){{chunk.ToString(),new(chunkHash,encoded.Encoding,encoded.ModifiedCount)}});byte[] commit=JsonSerializer.SerializeToUtf8Bytes(manifest,Catalog.JsonOptions);File.WriteAllBytes(Path.Combine(path,generation+".commit"),commit);File.WriteAllBytes(Path.Combine(path,"CURRENT"),commit);
    var loaded=store.Load(state.WorldId);Check(loaded.Migrated&&loaded.Snapshot.FormatVersion==WorldSnapshot.CurrentFormat&&loaded.Snapshot.Changes[cell.ToString()]=="chest"&&loaded.Snapshot.Entities[cell.ToString()].Storage.Count("wood")==11&&loaded.Snapshot.Inventory.Count("iron_ingot")==6&&loaded.Snapshot.Beacons.Contains(1)&&loaded.Snapshot.Population.Sequence==23&&loaded.Snapshot.GuardianDefeated,"format2 gameplay changed");
    Check(File.ReadAllBytes(Path.Combine(path,generation+".json")).SequenceEqual(raw)&&File.ReadAllBytes(Path.Combine(path,"CURRENT")).SequenceEqual(commit),"loading rewrote old world");
    store.Save(loaded.Snapshot);var backup=store.ListBackups().Single();string backupPath=Path.Combine(migrationRoot,"..","backups",backup.Key);
    Check(File.ReadAllBytes(Path.Combine(backupPath,generation+".json")).SequenceEqual(raw)&&File.ReadAllBytes(Path.Combine(backupPath,"chunks",chunkHash+".chunk")).SequenceEqual(encoded.Bytes),"raw format2 backup missing or changed");
    store.Save(store.Load(state.WorldId).Snapshot);Check(store.ListBackups().Count()==1,"each save repeated migration backup");string restored=store.RestoreBackup(backup.Key);Check(store.Load(restored).Snapshot.Entities[cell.ToString()].Storage.Count("wood")==11,"old compressed backup restore failed");
    File.WriteAllText(Path.Combine(root,"test-results","format2-compat-path.json"),JsonSerializer.Serialize(new{storeRoot=migrationRoot,worldId=state.WorldId,metadataFormat=WorldSnapshot.CurrentFormat},Catalog.JsonOptions));
});
Test("generation pruning keeps three complete saves and future manifests cannot be overwritten",()=>{
    string pruneRoot=Path.Combine(testRoot,"prune","worlds");var store=new SaveStore(pruneRoot,catalog);var state=new WorldSnapshot();var generations=new List<string>();
    for(int i=0;i<6;i++){state.Changes["0,80,0"]=i%2==0?"plank":"brick";state.Inventory.Slots[0]=new("stone",i+1);generations.Add(store.Save(state));}
    string path=Path.Combine(pruneRoot,state.WorldId);Check(Directory.GetFiles(path,"*.commit").Length==3&&generations.Take(3).All(g=>!File.Exists(Path.Combine(path,g+".json")))&&store.Load(state.WorldId).Snapshot.Inventory.Count("stone")==6,"generation retention wrong");
    WriteFixture(pruneRoot,state,version:99);byte[] original=File.ReadAllBytes(Path.Combine(path,"CURRENT"));bool refused=false;try{store.Save(state);}catch(UnsupportedSaveException){refused=true;}Check(refused&&File.ReadAllBytes(Path.Combine(path,"CURRENT")).SequenceEqual(original),"future manifest overwritten");
});
Test("malformed complete metadata recovers earlier valid state",()=>{
    string shapeRoot=Path.Combine(testRoot,"shape","worlds");var store=new SaveStore(shapeRoot,catalog);var state=new WorldSnapshot();state.Inventory.Add("stone",12,catalog);store.Save(state);
    var broken=state.Copy();broken.FormatVersion=1;broken.Position=[1,2];WriteFixture(shapeRoot,broken,generation:"9999-shape");var recovered=store.Load(state.WorldId);Check(recovered.Recovered&&recovered.Snapshot.Position.Length==3&&recovered.Snapshot.Inventory.Count("stone")==12,"malformed position reached game");
    string commit=Path.Combine(shapeRoot,state.WorldId,"9999-shape.commit");Check(File.Exists(commit),"bad metadata deleted");bool malformed=false;try{Cell.Parse("1,2");}catch(FormatException){malformed=true;}Check(malformed,"invalid coordinate not diagnosed");
    string path=Path.Combine(shapeRoot,state.WorldId),good=store.Load(state.WorldId).Generation;
    var manifest=JsonSerializer.Deserialize<SaveManifest>(File.ReadAllBytes(Path.Combine(path,good+".commit")),Catalog.JsonOptions)!;
    string badGeneration="99999-null-chunk";File.Copy(Path.Combine(path,good+".json"),Path.Combine(path,badGeneration+".json"));
    var nullManifest=manifest with{Generation=badGeneration,Chunks=new(){["0,0,0"]=null!}};byte[] nullBytes=JsonSerializer.SerializeToUtf8Bytes(nullManifest,Catalog.JsonOptions);File.WriteAllBytes(Path.Combine(path,badGeneration+".commit"),nullBytes);File.WriteAllBytes(Path.Combine(path,"CURRENT"),nullBytes);
    Check(store.Load(state.WorldId).Recovered&&!store.List().Single().Damaged,"null chunk reference prevented recovery or listing");store.Save(state);Check(File.Exists(Path.Combine(path,badGeneration+".commit")),"damaged manifest deleted during pruning");
    for(int i=0;i<3;i++){broken=state.Copy();broken.FormatVersion=1;broken.Position=[1,2];WriteFixture(shapeRoot,broken,generation:"999999-shape-"+i);}
    store.Save(state);Check(Directory.GetFiles(path,"*.commit").Count(p=>!Path.GetFileName(p).StartsWith("999",StringComparison.Ordinal))==3,"malformed commits displaced complete retention generations");
    broken=state.Copy();broken.FormatVersion=1;broken.Inventory=new(27);WriteFixture(shapeRoot,broken,generation:"9999999-short-inventory");Check(store.Load(state.WorldId).Snapshot.Inventory.Slots.Length==36,"bad inventory size reached HUD");
    broken=state.Copy();broken.FormatVersion=1;broken.Entities["0,70,0"]=new(){Kind="furnace",RecipeId="missing-job"};WriteFixture(shapeRoot,broken,generation:"99999999-unknown-job");Check(!store.Load(state.WorldId).Snapshot.Entities.ContainsKey("0,70,0"),"unknown job reached simulation");
});
Test("large save reuses chunks, prunes generations and recovers from damaged chunk",()=>{
    string bigRoot=Path.Combine(testRoot,"large","worlds");var store=new SaveStore(bigRoot,catalog);var state=new WorldSnapshot{Name="dense stress"};
    for(int chunkX=0;chunkX<12;chunkX++)for(int i=0;i<8192;i++)state.Changes[new Cell(chunkX*16+i%16,96+i/256,i/16%16).ToString()]="plank";
    state.Inventory.Add("iron_ingot",20,catalog);var timer=Stopwatch.StartNew();var captured=state.Copy();double captureMs=timer.Elapsed.TotalMilliseconds;timer.Restart();string first=store.Save(captured);double saveMs=timer.Elapsed.TotalMilliseconds;
    string worldPath=Path.Combine(bigRoot,state.WorldId),chunksPath=Path.Combine(worldPath,"chunks");var files=Directory.GetFiles(chunksPath,"*.chunk").ToDictionary(p=>Path.GetFileName(p)!,p=>File.GetLastWriteTimeUtc(p));Check(files.Count==12,"expected 12 dense chunks");long rawBytes=JsonSerializer.SerializeToUtf8Bytes(state,Catalog.JsonOptions).LongLength,totalBytes=Directory.GetFiles(worldPath,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);Check(totalBytes<rawBytes/10,"dense save exceeds compression bound");
    state.Time+=10;store.Save(state);Check(Directory.GetFiles(chunksPath,"*.chunk").Length==12&&Directory.GetFiles(chunksPath,"*.chunk").All(p=>files[Path.GetFileName(p)]==File.GetLastWriteTimeUtc(p)),"unchanged chunk rewritten");var roundtrip=store.Load(state.WorldId).Snapshot;Check(roundtrip.Changes.Count==98304&&roundtrip.Changes.All(p=>state.Changes[p.Key]==p.Value)&&roundtrip.Inventory.Count("iron_ingot")==20,"large save lost state");
    state.Changes["0,96,0"]="brick";state.Inventory.Remove("iron_ingot",3);string newest=store.Save(state);var manifest=JsonSerializer.Deserialize<SaveManifest>(File.ReadAllText(Path.Combine(worldPath,newest+".commit")),Catalog.JsonOptions)!;string changedChunk=Path.Combine(chunksPath,manifest.Chunks!["0,3,0"].Hash+".chunk");File.WriteAllText(changedChunk,"damaged");var recovered=store.Load(state.WorldId);Check(recovered.Recovered&&recovered.Snapshot.Changes["0,96,0"]=="plank"&&recovered.Snapshot.Inventory.Count("iron_ingot")==20,"mixed chunk/player recovery");
    File.WriteAllText(Path.Combine(root,"test-results","large-save.json"),JsonSerializer.Serialize(new{edits=98304,captureMs,saveMs,rawBytes,totalBytes,chunkCount=12,roundtrip=true,reuse=true,recovery=true},Catalog.JsonOptions));
});
Test("backup restore keeps original and refuses damaged or escaped input",()=>{
    var store=new SaveStore(Path.Combine(testRoot,"restore","worlds"),catalog);var s=new WorldSnapshot{Name="test backup",Weather=new(){Phase=2,Remaining=100,Blend=.8}};s.Inventory.Add("crystal",3,catalog);s.Changes["2,40,2"]="plank";store.Save(s);
    string backup=store.Backup(s.WorldId);string key=Path.GetFileName(backup);Check(store.ListBackups().Single().Name==s.Name,"backup not listed");s.Inventory.Remove("crystal",3);store.Save(s);
    string newId=store.RestoreBackup(key);Check(newId!=s.WorldId&&store.Load(newId).Snapshot.Inventory.Count("crystal")==3&&store.Load(s.WorldId).Snapshot.Inventory.Count("crystal")==0,"restore overwrote original");Check(store.Load(newId).Snapshot.Weather.Phase==2,"weather missing from backup");
    bool escaped=false;try{store.RestoreBackup("../worlds");}catch(ArgumentException){escaped=true;}Check(escaped,"backup traversal accepted");
    string stateFile=Directory.GetFiles(backup,"*.json").Single();File.WriteAllText(stateFile,"bad");bool damaged=false;try{store.RestoreBackup(key);}catch(InvalidDataException){damaged=true;}Check(damaged&&store.ListBackups().Single().Damaged,"corrupt backup restored");
});
Test("equipment transfers reject invalid slots and conserve worn stacks",()=>{
    var state=new WorldSnapshot();state.Inventory.Slots[0]=new("iron_helmet",1,50);state.Inventory.Slots[1]=new("iron_helmet",1,100);state.Inventory.Slots[2]=new("stone",40);state.Inventory.Slots[3]=new("shield",1);Stack? cursor=null;
    bool Rule(int i,Stack? s)=>Equipment.Accepts(catalog,i,s);
    Check(!Inventory.Move(state.Inventory,2,state.Equipment,0,64,catalog,targetAccepts:Rule),"stone equipped as helmet");
    Check(Inventory.Move(state.Inventory,0,state.Equipment,0,1,catalog,targetAccepts:Rule),"helmet refused");
    Check(!Inventory.Move(state.Equipment,0,state.Inventory,2,64,catalog,sourceAccepts:Rule),"invalid swap put stone in armor slot");
    Check(!Inventory.Click(state.Equipment,1,new("iron_helmet",1,20),false,catalog,out _,Rule),"wrong armor cursor accepted");
    Dictionary<string,int> Counts()=>state.Inventory.Slots.Concat(state.Equipment.Slots).Append(cursor).Where(s=>s!=null).GroupBy(s=>$"{s!.Id}:{s.Durability}").ToDictionary(g=>g.Key,g=>g.Sum(s=>s!.Count));
    var original=Counts();var random=new Random(7521);
    for(int n=0;n<2000;n++){var a=random.Next(2)==0?state.Inventory:state.Equipment;var b=random.Next(2)==0?state.Inventory:state.Equipment;int from=random.Next(a.Slots.Length),to=random.Next(b.Slots.Length);
        if(random.Next(3)==0){Inventory.Click(a,from,cursor,random.Next(2)==0,catalog,out cursor,ReferenceEquals(a,state.Equipment)?Rule:null);}else Inventory.Move(a,from,b,to,random.Next(1,65),catalog,ReferenceEquals(a,state.Equipment)?Rule:null,ReferenceEquals(b,state.Equipment)?Rule:null);
        state.Inventory.Validate(catalog);Equipment.Validate(state.Equipment,catalog);var counts=Counts();Check(counts.Count==original.Count&&original.All(p=>counts.GetValueOrDefault(p.Key)==p.Value),"equipment transaction changed item/durability multiset");
    }
    var worn=new Inventory(4);worn.Add(new("iron_helmet",1,50),catalog);worn.Add(new("iron_helmet",1,100),catalog);worn.Sort(catalog);Check(worn.Slots.Where(s=>s!=null).Select(s=>s!.Durability).SequenceEqual(new int?[]{50,100}),"sorting repaired or merged worn armor");
});
Test("armor mitigates attacks, wears once, breaks and excludes creative damage",()=>{
    var state=new WorldSnapshot();foreach(var def in catalog.Items.Values.Where(d=>d.Use=="armor"&&d.Id.StartsWith("iron_")))state.Equipment.Slots[def.ArmorSlot]=new(def.Id,1);
    Check(Equipment.ArmorPoints(state,catalog)==15&&Math.Abs(Equipment.Absorb(state,catalog,20)-8)<.00001,"iron armor protection");
    foreach(var item in state.Equipment.Slots.Take(4))Check(item!.Durability==catalog.Items[item.Id].Durability-5,"wear not committed once");
    state.Equipment.Slots[0]!.Durability=1;Equipment.Absorb(state,catalog,4);Check(state.Equipment.Slots[0]==null&&Equipment.ArmorPoints(state,catalog)==13,"broken helmet still protects");
    int? durability=state.Equipment.Slots[1]!.Durability;state.Creative=true;Equipment.Absorb(state,catalog,100);Check(state.Equipment.Slots[1]!.Durability==durability,"creative wore armor");
});
Test("offhand placement and seeds consume the correct owned slot atomically",()=>{
    var state=new WorldSnapshot();var w=new WorldState(catalog,state);var p=new Cell(300,80,300);w.Set(p,0);state.Inventory.Slots[0]=new("plank",5);state.Inventory.Slots[5]=new("plank",7);state.Equipment.Slots[4]=new("plank",3);
    Check(w.Place(p,"plank",false,out _,supply:state.Equipment,supplySlot:4)&&state.Equipment.Slots[4]!.Count==2&&state.Inventory.Count("plank")==12,"offhand removed main inventory materials");
    Check(!w.Place(p,"plank",false,out _,supply:state.Equipment,supplySlot:4)&&state.Equipment.Slots[4]!.Count==2,"failed placement charged offhand");
    var q=p+new Cell(1,0,0);w.Set(q,0);Check(!w.Place(q,"plank",false,out _,supply:new Inventory(1){Slots=[new("plank",5)]},supplySlot:0),"foreign inventory supplied survival placement");
    Check(w.Place(q,"plank",false,out _,supply:state.Inventory,supplySlot:5)&&state.Inventory.Slots[0]!.Count==5&&state.Inventory.Slots[5]!.Count==6,"selected slot placement drained a different stack");
    var farm=p+new Cell(0,-1,1);w.Set(farm,catalog.Number("farmland"));w.Set(farm+new Cell(0,1,0),0);state.Equipment.Slots[4]=new("wheat_seed",2);Check(w.Plant(farm,"wheat_seed",out _,state.Equipment,4)&&state.Equipment.Slots[4]!.Count==1,"offhand seed use");
});
Test("equipment death, immutable snapshots, save and backup preserve durability",()=>{
    var state=new WorldSnapshot();state.Inventory.Slots[0]=new("stone",64);state.Equipment.Slots[0]=new("iron_helmet",1,30);state.Equipment.Slots[4]=new("plank",12);var snapshot=state.Copy();state.Equipment.Slots[0]!.Durability=20;Check(snapshot.Equipment.Slots[0]!.Durability==30,"snapshot aliases live equipment");
    var store=new SaveStore(Path.Combine(testRoot,"equipment","worlds"),catalog);store.Save(snapshot);var loaded=store.Load(snapshot.WorldId).Snapshot;Check(loaded.Equipment.Slots[0]!.Durability==30&&loaded.Equipment.Slots[4]!.Count==12,"equipment save failed");
    string backup=store.Backup(snapshot.WorldId);string restored=store.RestoreBackup(Path.GetFileName(backup));Check(store.Load(restored).Snapshot.Equipment.Slots[0]!.Durability==30,"backup repaired armor");
    new WorldState(catalog,state).Die();Check(state.Inventory.Slots.All(s=>s==null)&&state.Equipment.Slots.All(s=>s==null)&&state.Graves.Single().Items.Slots[36]!.Durability==20&&state.Graves.Single().Items.Count("plank")==12,"death omitted equipment");
    var casual=snapshot.Copy();casual.Casual=true;new WorldState(catalog,casual).Die();Check(casual.Equipment.Slots[0]!.Durability==30&&casual.Inventory.Count("stone")==64&&casual.Graves.Count==0,"casual death lost equipment");
});
Test("format3 migrates to equipment format4 with raw backup and empty new slots",()=>{
    string migrationRoot=Path.Combine(testRoot,"format3","worlds");var store=new SaveStore(migrationRoot,catalog);var state=new WorldSnapshot{FormatVersion=3,Name="format3 equipment migration"};state.Inventory.Add("iron_ingot",7,catalog);state.Changes["17,70,-18"]="glass";
    WriteFixture(migrationRoot,state,generation:"20000103000000-format3");string rawFile=Path.Combine(migrationRoot,state.WorldId,"20000103000000-format3.json");byte[] raw=File.ReadAllBytes(rawFile);
    var loaded=store.Load(state.WorldId);Check(loaded.Migrated&&loaded.Snapshot.FormatVersion==WorldSnapshot.CurrentFormat&&loaded.Snapshot.Equipment.Slots.Length==5&&loaded.Snapshot.Equipment.Slots.All(s=>s==null)&&loaded.Snapshot.Inventory.Count("iron_ingot")==7,"format3 changed gameplay");
    store.Save(loaded.Snapshot);var backup=store.ListBackups().Single();Check(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(migrationRoot)!,"backups",backup.Key,"20000103000000-format3.json")).SequenceEqual(raw),"raw format3 backup changed");store.Save(store.Load(state.WorldId).Snapshot);Check(store.ListBackups().Count()==1,"format3 backup repeated");
});
NaturalSurfaceTests.Run(catalog,(name,action)=>Test(name,action),testRoot);
LightingDropTests.Run(catalog,(name,action)=>Test(name,action),testRoot);
ExpansionTests.Run(catalog,creatureDefs,(name,action)=>Test(name,action),testRoot);
ExplorationTests.Run(catalog,(name,action)=>Test(name,action),testRoot);
MainlineTests.Run(catalog,creatureDefs,(name,action)=>Test(name,action),testRoot);
Directory.CreateDirectory(Path.Combine(root,"test-results"));File.WriteAllText(Path.Combine(root,"test-results","core.json"),JsonSerializer.Serialize(new{date=DateTime.UtcNow,failed,results},Catalog.JsonOptions));
Environment.ExitCode=failed==0?0:1;

