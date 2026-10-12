using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.Rendering;
using EmberLighthouse.World;
public static class NaturalSurfaceTests
{
    public static void Run(Catalog catalog,Action<string,Action> test,string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        test("scattered starter supplies vary across seeds and remain accessible without fixed rows",()=>{
            var layouts=new HashSet<string>();for(int seed=0;seed<100;seed++){var t=new Terrain(seed,catalog);var supplies=t.StarterResources;Check(supplies.Count==15&&supplies.Values.Count(id=>id=="wood")==6&&supplies.Values.Count(id=>id=="loose_stone")==6&&supplies.Values.Count(id=>id=="berry_bush")==3,"lost hand gathering progression");
                foreach(var (p,id) in supplies){int h=t.Surface(p.X,p.Z);Check(h>Terrain.SeaLevel&&catalog.Id(t.Get(new(p.X,h+1,p.Z)))==id&&p.X*p.X+p.Z*p.Z>=36,"submerged, missing or crowded spawn supplies");if(id=="wood")Check(t.IsLooseWood(new(p.X,h+1,p.Z)),"log became solid tree");}
                Check(supplies.Keys.All(a=>supplies.Keys.All(b=>a==b||(a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z)>=9)),"materials form a cluster");layouts.Add(string.Join(";",supplies.Select(e=>$"{e.Key}:{e.Value}")));}
            Check(layouts.Count==100,"starter pattern repeats across seeds");var old=new Terrain(42,catalog,4);Check(catalog.Id(old.Get(new(-4,37,4)))=="wood"&&catalog.Id(old.Get(new(4,37,4)))=="loose_stone","old generator changed");
        });
        test("sparse vegetation lowers every habitat's coverage while retaining species and terrain",()=>{
            var seeds=new Dictionary<int,int>();for(int seed=0;seed<100;seed++)seeds.TryAdd(Landscape.StartProfile(seed),seed);var species=new HashSet<string>();
            foreach(var (profile,seed) in seeds){var old=new Terrain(seed,catalog,4);var sparse=new Terrain(seed,catalog,5);int before=0,after=0,total=0;
                for(int x=-100;x<=100;x+=2)for(int z=-100;z<=100;z+=2){int h=old.Surface(x,z);Check(sparse.Surface(x,z)==h&&sparse.Get(new(x,10,z))==old.Get(new(x,10,z)),"terrain or caves changed");if(h<Terrain.SeaLevel)continue;total++;string a=catalog.Id(old.Get(new(x,h+1,z))),b=catalog.Id(sparse.Get(new(x,h+1,z)));if(catalog.Items.TryGetValue(a,out var first)&&first.Shape=="bush")before++;if(catalog.Items.TryGetValue(b,out var second)&&second.Shape=="bush"){after++;species.Add(b);}}
                Check(after<before*.35&&after<total*.10,$"habitat {profile} remains dense: {before} -> {after} / {total}");Console.WriteLine($"VEGETATION profile {profile}: {before/(double)total:P1} -> {after/(double)total:P1}");}
            Check(new[]{"fern","reed","tall_grass","dry_grass","red_flower","blue_flower","brown_mushroom","red_mushroom"}.All(species.Contains),"species lost in thinning: "+string.Join(",",species));
            var mesh=VoxelMesher.Build(p=>p==new Cell(0,0,0)?catalog.Number("loose_stone"):(ushort)0,new(0,0,0),catalog);Check(mesh.Collision.Count==0&&mesh.Vertices.Max(v=>v.Y)<.3f&&mesh.Quads>6,"loose stones still look like brick slabs");
        });
        test("surface refresh preserves placed matching blocks, crops, containers and mainline through save",()=>{
            var old=new WorldState(catalog,new(){Seed=42,GeneratorVersion=4});var p=new Cell(-4,37,4);old.State.Inventory.Add("wood",1,catalog);Check(old.Break(p,0,out _)&&old.Place(p,"wood",false,out _),"placement setup");Check(!old.State.Changes.ContainsKey(p.ToString()),"placed block no longer matches baseline");
            var chest=new Cell(34,38,26);old.State.Entities[chest.ToString()]=new(){Kind="chest",Storage=ChestLoot.Generate(old,chest).Items};ChestLoot.Open(old,chest).Storage.Add("iron_ingot",7,catalog);old.State.Mainline.Proofs.UnionWith(["mine","forge"]);old.State.Beacons.Add(0);var crop=new Cell(5,70,5);old.Set(crop,catalog.Number("berry_bush"));old.State.Entities[crop.ToString()]=new(){Kind="crop",CropId="wheat_seed",Stage=2,GrowthStepSeconds=90};
            string original=JsonSerializer.Serialize(old.State,Catalog.JsonOptions);var refreshed=StarterSupplies.Refresh(old.State,catalog);Check(JsonSerializer.Serialize(old.State,Catalog.JsonOptions)==original,"refresh mutated original");var world=new WorldState(catalog,refreshed);Check(world.Get(p)==catalog.Number("wood")&&world.Solid(p)&&world.State.Entities[crop.ToString()].Stage==2&&world.State.Entities[chest.ToString()].Storage.Count("iron_ingot")==7&&world.State.Mainline.Proofs.Contains("forge"),"refresh damaged owned state");
            var store=new SaveStore(Path.Combine(root,"surface-refresh","worlds"),catalog);store.Save(refreshed);var loaded=new WorldState(catalog,store.Load(refreshed.WorldId).Snapshot);Check(loaded.State.GeneratorVersion==5&&loaded.Get(p)==catalog.Number("wood")&&loaded.State.Entities[chest.ToString()].Storage.Count("iron_ingot")==7,"refresh lost state on reload");
        });
        test("permanent deletion removes selected world, recycle copies and backups without touching neighbors",()=>{
            var store=new SaveStore(Path.Combine(root,"permanent","worlds"),catalog);var a=new WorldSnapshot{Name="同名"};var b=new WorldSnapshot{Name="同名"};store.Save(a);store.Save(b);string aBackup=store.Backup(a.WorldId),bBackup=store.Backup(b.WorldId),deleted=store.DeleteWorld(a.WorldId,a.Name);store.Save(a);
            using(var busy=new FileStream(Path.Combine(store.Root,a.WorldId,".saving.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){bool blocked=false;try{store.PermanentlyDeleteWorld(a.WorldId);}catch(IOException){blocked=true;}Check(blocked&&Directory.Exists(aBackup)&&Directory.Exists(Path.Combine(store.Root,a.WorldId)),"busy save was partially deleted");}
            store.PermanentlyDeleteWorld(a.WorldId);Check(!Directory.Exists(Path.Combine(store.Root,a.WorldId))&&!Directory.Exists(aBackup)&&!store.ListDeletedWorlds().Any(e=>e.Id==a.WorldId),"world left files behind");Check(store.Load(b.WorldId).Snapshot.Name==b.Name&&Directory.Exists(bBackup),"same-name neighbor or backup removed");
            deleted=store.DeleteWorld(b.WorldId,b.Name);store.PermanentlyDeleteDeletedWorld(deleted,false);Check(store.ListDeletedWorlds().Length==0&&Directory.Exists(bBackup),"backup retention option ignored");store.DeleteBackup(Path.GetFileName(bBackup));Check(!Directory.Exists(bBackup)&&!store.ListBackups().Any(),"backup remains on disk");
            var damaged=new WorldSnapshot();Directory.CreateDirectory(Path.Combine(store.Root,damaged.WorldId));File.WriteAllText(Path.Combine(store.Root,damaged.WorldId,"CURRENT"),"broken");store.PermanentlyDeleteWorld(damaged.WorldId);Check(!Directory.Exists(Path.Combine(store.Root,damaged.WorldId)),"damaged world can't be purged");
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(store.Root)!,"backups","damaged-backup"));File.WriteAllText(Path.Combine(Path.GetDirectoryName(store.Root)!,"backups","damaged-backup","CURRENT"),"broken");store.DeleteBackup("damaged-backup");
            foreach(string key in new[]{"..","../worlds","C:\\","x/../"}){bool blocked=false;try{store.DeleteBackup(key);}catch(ArgumentException){blocked=true;}Check(blocked,"backup escape accepted");}
        });
    }
}

