using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.World;
public static class NatureFeedbackTests
{
    public static void Run(Catalog catalog,Action<string,Action> test,string root)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        test("coal and copper are less frequent across cave heights and signed coordinates",()=>{
            var counts=new Dictionary<string,int[]>();int solid=0;
            foreach(int seed in new[]{16,42,73}){var old=new Terrain(seed,catalog,7);var updated=new Terrain(seed,catalog,8);
                for(int x=-160;x<160;x+=3)for(int z=-90;z<90;z+=3)for(int y=8;y<33;y+=4){var p=new Cell(x,y,z);string a=catalog.Id(old.Get(p)),b=catalog.Id(updated.Get(p));
                    if(a=="air"){Check(b=="air","ore change altered cave cavity");continue;}solid++;
                    foreach(string id in new[]{"coal_ore","copper_vein"}){if(!counts.TryGetValue(id,out var pair))counts[id]=pair=new int[2];if(a==id)pair[0]++;if(b==id)pair[1]++;}
                }
            }
            Check(solid>30000,"not enough solid samples");foreach(var (id,pair) in counts){Check(pair[0]>1000&&pair[1]>200&&pair[1]<pair[0]*.65,"ore failed to become scarce or was removed entirely");Console.WriteLine($"ORE_DENSITY {id}: {pair[0]} -> {pair[1]} / {solid} solid blocks");}
            string folder=Path.Combine(root,"nature-density");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"validation.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,solidBlocks=solid,counts},Catalog.JsonOptions));
        });
        test("tree crowns cover the trunk tip, vary height and include broad trunks and mixed species",()=>{
            var species=new HashSet<string>();var heights=new HashSet<int>();int thick=0;
            for(int i=0;i<1200;i++)for(int climate=0;climate<6;climate++){
                var shape=NaturalTrees.Shape(RandomField.Hash(42,i*8,0,-i*8,209),climate);species.Add(shape.Log);heights.Add(shape.Height);if(shape.Width==2)thick++;
                Check(NaturalTrees.Block(shape,0,shape.Height,0)==shape.Log,"missing trunk tip");
                for(int y=shape.Height;y<=shape.Height+2;y++){
                    int leaves=0;for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++)if(NaturalTrees.Block(shape,x,y,z)==shape.Leaves)leaves++;
                    Check(leaves>=4,"isolated single-column leaf spike");
                }
            }
            Check(species.Count==4&&heights.Count>=8&&thick>300,"tree diversity missing");
            var pine=NaturalTrees.Shape(123456,3);var rings=new List<int>();for(int y=pine.Height-6;y<=pine.Height+2;y++){int count=0;for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++)if(NaturalTrees.Block(pine,x,y,z)==pine.Leaves)count++;rings.Add(count);}Check(rings.Distinct().Count()>=4,"pine has one uniform canopy");
        });
        test("opt-in nature refresh preserves edits, surface clearance, profiles and backups",()=>{
            var original=new WorldSnapshot{GeneratorVersion=7,Seed=42};var old=new WorldState(catalog,original);int h=old.Terrain.Surface(65,22);old.Set(new(65,h+4,22),catalog.Number("plank"));old.Set(new(64,h+3,22),0);old.Set(new(-21,15,-22),catalog.Number("copper_vein"));original.Inventory.Add("wood",13,catalog);var saved=original.Copy();var updated=NaturalTrees.Refresh(original,catalog);var next=new WorldState(catalog,updated);
            Check(original.GeneratorVersion==7&&original.Changes.Count==saved.Changes.Count,"refresh mutates input");Check(updated.GeneratorVersion==8&&updated.Inventory.Count("wood")==13,"refresh loses player profile");foreach(var p in saved.Changes)Check(next.Get(Cell.Parse(p.Key))==old.Get(Cell.Parse(p.Key)),"player edit changed");
            for(int x=59;x<=71;x++)for(int z=16;z<=28;z++)for(int y=old.Terrain.Surface(x,z)+1;y<=old.Terrain.Surface(x,z)+18;y++)Check(next.Get(new(x,y,z))==old.Get(new(x,y,z)),"trees grow into protected construction columns");
            var store=new SaveStore(Path.Combine(root,"nature-update","worlds"),catalog);store.Save(saved);var backup=store.Backup(saved.WorldId);store.Save(updated);Check(store.Load(saved.WorldId).Snapshot.GeneratorVersion==8,"updated world cannot reload");string restored=store.RestoreBackup(Path.GetFileName(backup));Check(store.Load(restored).Snapshot.GeneratorVersion==7,"backup loses original generator");
        });
        test("projectile ownership survives snapshots and save reload",()=>{
            var state=new WorldSnapshot();string owner=Guid.NewGuid().ToString("N");state.Projectiles.Add(new(){OwnerId=owner,Position=[0,110,0],Velocity=[0,0,-22],Remaining=1.2});var copy=state.Copy();Check(copy.Projectiles.Single().OwnerId==owner,"snapshot strips shooter identity");var store=new SaveStore(Path.Combine(root,"projectile-owner","worlds"),catalog);store.Save(copy);Check(store.Load(copy.WorldId).Snapshot.Projectiles.Single().OwnerId==owner,"reload strips shooter identity");
        });
    }
}
