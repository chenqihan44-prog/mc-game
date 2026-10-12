using System.Numerics;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.Rendering;
using EmberLighthouse.World;
public static class LightingDropTests
{
    public static void Run(Catalog catalog,Action<string,Action> test,string root)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        test("collision revisions exclude light-only neighbors but include edited seams and door panels",()=>{
            var w=new WorldState(catalog,new());var p=new Cell(8,110,8);w.Set(p,catalog.Number("plank"));var c=Coordinates.Chunk(p);var lightNeighbor=c+new Cell(1,0,1);Check(w.Revision(lightNeighbor)>0&&w.CollisionRevision(lightNeighbor)==0&&w.CollisionRevision(c)>0,"lighting dirtied unchanged collision");
            int old=w.CollisionRevision(c);w.Set(p,w.Get(p));Check(w.CollisionRevision(c)==old,"unchanged cube geometry invalidated collision");
            var seam=new Cell(15,127,15);w.Set(seam,catalog.Number("plank"));Check(w.CollisionRevision(new(1,3,0))>0&&w.CollisionRevision(new(0,4,0))>0&&w.CollisionRevision(new(0,3,1))>0,"cross-chunk collision faces were not invalidated");
            w.Set(p,catalog.Number("door"));old=w.CollisionRevision(c);w.Set(p,w.Get(p));Check(w.CollisionRevision(c)>old,"open/rotated door geometry ignored");
        });
        test("sealed caves block daylight and torch light decays without crossing solid walls",()=>{
            var cells=new Dictionary<Cell,ushort>();for(int x=2;x<=21;x++)for(int z=2;z<=9;z++)for(int y=1;y<=6;y++)if(x is 2 or 21||z is 2 or 9||y is 1 or 6||x==12)cells[new(x,y,z)]=catalog.Number("stone");
            VoxelLighting Build()=>VoxelLighting.Build(new(0,0,0),24,9,12,p=>cells.GetValueOrDefault(p),catalog,(_,_)=>8);
            var dark=Build();Check(dark.At(new(6,3,5)).X==0&&dark.At(new(0,3,5)).X==1,"ceiling leaked daylight");
            cells[new(6,3,3)]=catalog.Number("torch");var light=Build();Check(light.At(new(6,3,3)).Y>light.At(new(8,3,3)).Y&&light.At(new(8,3,3)).Y>light.At(new(11,3,3)).Y&&light.At(new(13,3,3)).Y==0,"torch falloff or wall occlusion failed");
            Check(light.Surface(new(12,3,4),-Vector3.UnitX).Y>0,"illuminated wall became black");cells.Remove(new(6,6,5));var entry=Build();Check(entry.At(new(6,3,5)).X==1&&entry.At(new(10,3,5)).X<1,"cave opening lacks gradual skylight");
        });
        test("wall torches attach to all four walls, reject air and drop when support breaks",()=>{
            var w=new WorldState(catalog,new());var p=new Cell(600,100,600);w.State.Inventory.Add("torch",5,catalog);Check(!w.Place(p,"torch",false,out _)&&w.State.Inventory.Count("torch")==5,"unsupported torch consumed item");
            for(int mount=1;mount<=4;mount++){var support=TorchMount.Support(p,mount);w.Set(support,catalog.Number("stone"));Check(w.Place(p,"torch",false,out _,support:support)&&w.State.Entities[p.ToString()].Stage==mount,"wall orientation missing");
                var local=new Cell(0,0,0);var mesh=VoxelMesher.Build(c=>c==local?catalog.Number("torch"):(ushort)0,local,catalog,rotation:_=>mount);Check(mesh.Vertices.Min(v=>v.Y)>.1f&&mesh.Vertices.Max(v=>v.Y)<.71f,"wall torch vertical bounds");
                if(mount==1)Check(mesh.Vertices.Min(v=>v.X)<.02&&mesh.Vertices.Max(v=>v.X)>.2,"torch floats away from wall");Check(w.Break(support,3,out _)&&w.Get(p)==0&&!w.State.Entities.ContainsKey(p.ToString()),"unsupported wall torch remained suspended");
            }Check(w.State.Graves.Sum(g=>g.Items.Count("torch"))==4,"detached torch lost or duplicated");
        });
        test("bonus chest is opt in, unique, creative free and persists across backup",()=>{
            foreach(int seed in new[]{16,42,73,107,999})foreach(bool bonus in new[]{false,true}){var w=new WorldState(catalog,new(){Seed=seed,BonusChest=bonus});w.InitializeRewards();w.InitializeRewards();var boxes=w.State.Entities.Where(e=>e.Value.LootTheme=="出生奖励宝箱").ToArray();Check(boxes.Length==(bonus?1:0),"bonus toggle ignored or duplicate cache");if(bonus)Check(boxes[0].Value.Storage.Count("wood_pick")==1&&boxes[0].Value.Storage.Count("wood")==6,"bonus contents invalid");}
            var creative=new WorldState(catalog,new(){BonusChest=true,Creative=true});creative.InitializeRewards();Check(!creative.State.Entities.Any(),"creative received survival rewards");
            var on=new WorldState(catalog,new(){BonusChest=true});on.InitializeRewards();on.State.Entities.Values.Single().Storage=new();var store=new SaveStore(Path.Combine(root,"lighting-bonus","worlds"),catalog);store.Save(on.State);var loaded=new WorldState(catalog,store.Load(on.State.WorldId).Snapshot);loaded.InitializeRewards();Check(loaded.State.BonusChest&&loaded.State.BonusChestInitialized&&loaded.State.Entities.Values.Single().Storage.Slots.All(s=>s==null),"reload refilled rewards");
        });
        test("wild chests are rare and legacy corrections preserve owned construction and opened boxes",()=>{
            int allowed=0;for(int x=-15;x<=15;x++)for(int z=-15;z<=15;z++)if(NaturalRules.WildChestAllowed(42,x*96+34,z*96+26))allowed++;Check(allowed is >100 and <220,"wild chest density not near one sixth");
            var p=new Cell(34,38,26);var w=new WorldState(catalog,new(){GeneratorVersion=6});Check(catalog.Id(w.Terrain.Get(p))=="chest"&&w.Get(p)==0,"unopened old starter cache survives");w.State.Entities[p.ToString()]=new(){Kind="chest"};w.State.Entities[p.ToString()].Storage.Add("wood",11,catalog);Check(w.Get(p)==catalog.Number("chest"),"opened legacy chest erased");
            var q=new Cell(400,w.Terrain.Surface(400,400)+1,400);w.Set(q,catalog.Number("crystal_ore"));Check(w.Get(q)==catalog.Number("crystal_ore"),"placed crystal erased");
            var terrain=w.Terrain;Check(NaturalRules.Correct(terrain,catalog,q,catalog.Number("crystal_ore"))==catalog.Number("basalt"),"natural surface crystal pillar remained");
        });
        test("natural seabeds replace submerged grass while crystal remains deep underground",()=>{
            int beds=0,crystals=0;var w=new WorldState(catalog,new(){Seed=73});for(int x=-300;x<=300;x+=9)for(int z=-300;z<=300;z+=9){int h=w.Terrain.Surface(x,z);if(h<Terrain.SeaLevel){beds++;Check(catalog.Id(w.Get(new(x,h,z))) is not("grass" or "snow" or "moss"),"submerged grass survived");}for(int y=1;y<h+6;y++)if(catalog.Id(w.Get(new(x,y,z)))=="crystal_ore"){crystals++;Check(y<16&&y<h-3,"crystal exposed on surface");}}
            Check(beds>100&&crystals>0,"missing underwater/deep ore fixtures");
        });
        test("drops require body contact, keep metadata, reject walls and retain inventory overflow",()=>{
            var w=new WorldState(catalog,new());var p=new Cell(600,100,600);w.Set(p,catalog.Number("wood"));Check(w.Break(p,0,out _)&&w.State.Inventory.Count("wood")==0,"mining auto inserted inventory");w.Advance(.5);Check(w.PickTouchingItems([602,100,600.5])==0,"remote vacuum pickup");Check(w.PickTouchingItems([600.5,100,600.5])==1&&w.State.Inventory.Count("wood")==1,"body contact failed");
            var armor=new Stack("leather_helmet",1,17){Dye="red",Protection=2,Unbreaking=1};var g=w.Drop(armor,[601.5,100,600.5]);w.Advance(.5);w.Set(new(601,100,600),catalog.Number("stone"));Check(w.PickTouchingItems([601.1,100,600.5])==0,"pickup crossed stone");w.Set(new(601,100,600),0);for(int i=0;i<36;i++)w.State.Inventory.Slots[i]=new("stone",64);Check(w.PickTouchingItems([601.5,100,600.5])==0&&g.Items.Slots[0]?.Durability==17,"full backpack lost armor");w.State.Inventory.Slots[0]=null;Check(w.PickTouchingItems([601.5,100,600.5])==1&&w.State.Inventory.Slots[0]?.Dye=="red"&&w.State.Inventory.Slots[0]?.Protection==2,"pickup repaired armor or stripped dye");
        });
        test("drop physics stops at ground, saves individual positions and migrates old containers",()=>{
            var w=new WorldState(catalog,new());for(int x=600;x<604;x++)for(int z=600;z<604;z++)w.Set(new(x,99,z),catalog.Number("stone"));var item=w.Drop(new Stack("iron_chestplate",1,19){HeatLining=true},[601.5,104,601.5]);for(int i=0;i<150;i++)w.TickDroppedItems(.02,_=>true);var p=WorldState.ItemPosition(item,0);Check(p[1]>100.1&&p[1]<100.2,"item fell through floor or floats high");var copy=w.State.Copy();item.ItemPositions[0][0]=900;Check(copy.Graves[0].ItemPositions[0][0]<604,"drop position aliases snapshot");var store=new SaveStore(Path.Combine(root,"lighting-drops","worlds"),catalog);store.Save(copy);var loaded=store.Load(copy.WorldId).Snapshot;Check(loaded.Graves.Single().ItemPositions[0][1]==copy.Graves[0].ItemPositions[0][1]&&loaded.Graves[0].Items.Slots[0]?.HeatLining==true,"saved drop lost model position or metadata");
            loaded.FormatVersion=6;SaveMigrations.ToCurrent(loaded);Check(loaded.Graves.Single().LegacyContainer&&loaded.FormatVersion==7,"old bags cannot be recovered");
        });
    }
}
