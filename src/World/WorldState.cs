using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Creatures;

namespace EmberLighthouse.World;

public sealed class BlockEntity
{
    public string Kind {get;set;}="";
    public Inventory Storage {get;set;}=new(27);
    public double LastTime {get;set;}
    public int Stage {get;set;}
    public int Rotation {get;set;}
    public bool Open {get;set;}
    public string? RecipeId {get;set;}
    public double FinishTime {get;set;}
    public string? CropId {get;set;}
    // Missing in older saves: retain their original 90-second stages.
    public double GrowthStepSeconds {get;set;}=90;
}
public sealed class Grave
{
    public double[] Position {get;set;}=[0,40,0];
    public Inventory Items {get;set;}=new();
}
public sealed class WorldSnapshot
{
    public const int CurrentFormat=4;
    public int FormatVersion {get;set;}=CurrentFormat;
    public int GeneratorVersion {get;set;}=Terrain.Version;
    public string Name {get;set;}="新世界";
    public string WorldId {get;set;}=Guid.NewGuid().ToString("N");
    public int Seed {get;set;}=42;
    public bool Creative {get;set;}
    public bool Casual {get;set;}
    public double Time {get;set;}=240;
    public double Health {get;set;}=100;
    public double Hunger {get;set;}=100;
    public double Air {get;set;}=15;
    public double[] Position {get;set;}=[0,39,0];
    public double[] Respawn {get;set;}=[0,39,0];
    public Dictionary<string,string> Changes {get;set;}=[];
    public Dictionary<string,BlockEntity> Entities {get;set;}=[];
    public Inventory Inventory {get;set;}=new();
    public List<Grave> Graves {get;set;}=[];
    public HashSet<int> Beacons {get;set;}=[];
    public HashSet<string> Discovered {get;set;}=[];
    public bool GuardianDefeated {get;set;}
    public int TutorialStep {get;set;}
    public bool TutorialHidden {get;set;}
    public List<CreatureState> Creatures {get;set;}=[];
    public List<ProjectileState> Projectiles {get;set;}=[];
    public CreaturePopulationState Population {get;set;}=new();
    public HashSet<string> FavoriteRecipes {get;set;}=[];
    public List<double[]> Markers {get;set;}=[];
    public int Experience {get;set;}
    public Inventory Crafting {get;set;}=new(4);
    public Inventory Equipment {get;set;}=new(5);
    public Stack? Cursor {get;set;}
    public WeatherState Weather {get;set;}=new();
    public void ValidateShape()
    {
        static bool PositionOk(double[]? position)=>position is {Length:3}&&position.All(double.IsFinite);
        if(string.IsNullOrWhiteSpace(Name)||WorldId==null||WorldId.Length!=32||!WorldId.All(Uri.IsHexDigit)||!PositionOk(Position)||!PositionOk(Respawn)||!double.IsFinite(Time)||Time<0||!double.IsFinite(Health)||!double.IsFinite(Hunger)||!double.IsFinite(Air)||Health<0||Health>100||Hunger<0||Hunger>100||Air<0||Air>15||Experience<0)throw new InvalidOperationException("玩家状态或世界标识无效");
        if(Changes==null||Entities==null||Inventory==null||Crafting==null||Equipment==null||Graves==null||Beacons==null||Discovered==null||Creatures==null||FavoriteRecipes==null||Markers==null||Weather==null||Population==null||Projectiles==null)throw new InvalidOperationException("存档缺少状态集合");
        if(Creatures.Any(c=>c==null||!CreatureRules.Known(c.Kind)))throw new InvalidOperationException("生物类型无效");
        if(!double.IsFinite(Population.NextSpawnTime)||Population.NextSpawnTime<0||Population.Sequence<0||Creatures.Count>CreaturePopulation.Limit||Creatures.Count(c=>c.Kind=="guardian")>1)throw new InvalidOperationException("生物数量或生成进度无效");
        if(Entities.Any(p=>p.Value==null||p.Value.Storage==null||!Coordinates.InWorld(Cell.Parse(p.Key)))||Graves.Any(g=>g==null||g.Items==null||!PositionOk(g.Position))||Creatures.Any(c=>c==null||!PositionOk(c.Position)||!double.IsFinite(c.Health))||Markers.Any(p=>p is not {Length:2}||!p.All(double.IsFinite))||Beacons.Any(i=>i<0||i>=Terrain.Towers.Length))throw new InvalidOperationException("容器、生物、背包或地图状态无效");
        foreach(var c in Creatures)if(!double.IsFinite(c.AttackWindup)||c.AttackWindup< -1||c.AttackWindup>5||!double.IsFinite(c.AttackCooldown)||c.AttackCooldown<0||c.AttackCooldown>10||!double.IsFinite(c.ChargeRemaining)||c.ChargeRemaining<0||c.ChargeRemaining>4||c.AttackAim!=null&&!PositionOk(c.AttackAim)||(c.AttackWindup>=0||c.ChargeRemaining>0)&&c.AttackAim==null)throw new InvalidOperationException("生物战斗状态无效");
        if(Projectiles.Count>ProjectileFlight.Limit||Projectiles.Any(p=>p==null))throw new InvalidOperationException("投射物数量无效");foreach(var p in Projectiles)p.Validate();
    }
    public WorldSnapshot Copy()
    {
        ValidateShape();
        var copy=(WorldSnapshot)MemberwiseClone();copy.Position=(double[])Position.Clone();copy.Respawn=(double[])Respawn.Clone();copy.Changes=new(Changes);copy.Inventory=Inventory.Copy();copy.Crafting=Crafting.Copy();copy.Equipment=Equipment.Copy();copy.Cursor=Cursor?.Copy();
        copy.Entities=Entities.ToDictionary(p=>p.Key,p=>new BlockEntity{Kind=p.Value.Kind,Storage=p.Value.Storage.Copy(),LastTime=p.Value.LastTime,Stage=p.Value.Stage,Rotation=p.Value.Rotation,Open=p.Value.Open,RecipeId=p.Value.RecipeId,FinishTime=p.Value.FinishTime,CropId=p.Value.CropId,GrowthStepSeconds=p.Value.GrowthStepSeconds});
        copy.Graves=Graves.Select(g=>new Grave{Position=(double[])g.Position.Clone(),Items=g.Items.Copy()}).ToList();copy.Beacons=new(Beacons);copy.Discovered=new(Discovered);copy.FavoriteRecipes=new(FavoriteRecipes);copy.Markers=Markers.Select(p=>(double[])p.Clone()).ToList();
        copy.Creatures=Creatures.Select(c=>new CreatureState{Kind=c.Kind,Position=(double[])c.Position.Clone(),Health=c.Health,AttackWindup=c.AttackWindup,AttackCooldown=c.AttackCooldown,ChargeRemaining=c.ChargeRemaining,AttackAim=c.AttackAim==null?null:(double[])c.AttackAim.Clone(),Provoked=c.Provoked}).ToList();copy.Projectiles=Projectiles.Select(p=>p.Copy()).ToList();copy.Population=new(){Initialized=Population.Initialized,NextSpawnTime=Population.NextSpawnTime,Sequence=Population.Sequence};copy.Weather=new(){Phase=Weather.Phase,Cycle=Weather.Cycle,Remaining=Weather.Remaining,Blend=Weather.Blend};return copy;
    }
}

public sealed class WorldState
{
    public Catalog Catalog {get;}
    public Terrain Terrain {get;}
    public WorldSnapshot State {get;}
    public Dictionary<Cell,ushort> Changes {get;}=[];
    private readonly Dictionary<Cell,Dictionary<Cell,ushort>> changesByChunk=[];
    public long Session {get;}=DateTime.UtcNow.Ticks;
    public Dictionary<Cell,int> Revisions {get;}=[];
    public WorldState(Catalog catalog,WorldSnapshot snapshot)
    {
        Catalog=catalog;State=snapshot;
        snapshot.ValidateShape();
        if(snapshot.GeneratorVersion!=Terrain.Version||snapshot.FormatVersion!=WorldSnapshot.CurrentFormat)throw new InvalidOperationException("不支持的世界版本，请保留原档");
        snapshot.Weather.Validate();
        snapshot.Inventory.Validate(catalog);
        if(snapshot.Inventory.Slots.Length!=36)throw new InvalidOperationException("玩家背包格数无效");
        snapshot.Crafting.Validate(catalog);
        Items.Equipment.Validate(snapshot.Equipment,catalog);
        if(snapshot.Crafting.Slots.Length!=4)throw new InvalidOperationException("合成格数量无效");
        if(snapshot.Cursor!=null)new Inventory(1){Slots=[snapshot.Cursor]}.Validate(catalog);
        foreach(var e in snapshot.Entities.Values){
            e.Storage.Validate(catalog);
            if(!double.IsFinite(e.LastTime)||!double.IsFinite(e.FinishTime)||e.LastTime<0||e.FinishTime<0||e.Rotation<0||e.Rotation>3||e.RecipeId!=null&&(!catalog.Recipes.TryGetValue(e.RecipeId,out var recipe)||recipe.Station!=e.Kind))throw new InvalidOperationException("设施计时、朝向或配方无效");
            if(e.Kind=="crop"&&(e.Stage<0||e.Stage>3||e.CropId==null||!catalog.Items.TryGetValue(e.CropId,out var seed)||seed.Use!="seed"))throw new InvalidOperationException("作物状态无效");
            if(e.Kind=="crop"&&(!double.IsFinite(e.GrowthStepSeconds)||e.GrowthStepSeconds<30||e.GrowthStepSeconds>300))throw new InvalidOperationException("作物生长周期无效");
        }
        foreach(var g in snapshot.Graves)g.Items.Validate(catalog);
        Terrain=new(snapshot.Seed,catalog);
        foreach(var p in snapshot.Changes){var c=Cell.Parse(p.Key);if(!Coordinates.InWorld(c))throw new InvalidOperationException("存档坐标越界");Changes[c]=p.Value=="air"?(ushort)0:catalog.Number(p.Value);IndexChange(c,Changes[c]);}
    }
    public ushort Get(Cell p)=>Changes.TryGetValue(p,out var id)?id:Terrain.Get(p);
    public bool Solid(Cell p)=>Catalog.Block(Get(p))?.Solid==true && !(Terrain.IsLooseWood(p)&&!Changes.ContainsKey(p)&&!State.Entities.ContainsKey(p.ToString())) && !(State.Entities.TryGetValue(p.ToString(),out var e)&&e.Kind=="door"&&e.Open);
    public void Set(Cell p,ushort id)
    {
        if(!Coordinates.InWorld(p))throw new ArgumentOutOfRangeException(nameof(p));
        if(Terrain.Get(p)==id){Changes.Remove(p);State.Changes.Remove(p.ToString());}else{Changes[p]=id;State.Changes[p.ToString()]=Catalog.Id(id);}
        if(Changes.ContainsKey(p))IndexChange(p,id);else if(changesByChunk.TryGetValue(Coordinates.Chunk(p),out var edits)){edits.Remove(p);if(edits.Count==0)changesByChunk.Remove(Coordinates.Chunk(p));}
        var c=Coordinates.Chunk(p);Bump(c);
        foreach(var n in Coordinates.Neighbors){var other=Coordinates.Chunk(p+n);if(other!=c)Bump(other);}
    }
    private void Bump(Cell p)=>Revisions[p]=Revisions.GetValueOrDefault(p)+1;
    private void IndexChange(Cell p,ushort value)
    {
        var chunk=Coordinates.Chunk(p);if(!changesByChunk.TryGetValue(chunk,out var edits)){edits=[];changesByChunk.Add(chunk,edits);}edits[p]=value;
    }
    public Dictionary<Cell,ushort> CaptureChunk(Cell chunk)
    {
        var copy=new Dictionary<Cell,ushort>();for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(changesByChunk.TryGetValue(chunk+new Cell(x,y,z),out var edits))foreach(var edit in edits)copy.Add(edit.Key,edit.Value);return copy;
    }
    public Dictionary<Cell,ushort> CaptureRegion(int centerX,int centerZ,int radius)
    {
        var copy=new Dictionary<Cell,ushort>();for(int x=Coordinates.FloorDiv(centerX-radius,16);x<=Coordinates.FloorDiv(centerX+radius,16);x++)for(int z=Coordinates.FloorDiv(centerZ-radius,16);z<=Coordinates.FloorDiv(centerZ+radius,16);z++)for(int y=0;y<6;y++)if(changesByChunk.TryGetValue(new(x,y,z),out var edits))foreach(var edit in edits)if(Math.Abs(edit.Key.X-centerX)<=radius&&Math.Abs(edit.Key.Z-centerZ)<=radius)copy.Add(edit.Key,edit.Value);return copy;
    }
    public int Revision(Cell c)=>Revisions.GetValueOrDefault(c);
    public bool Place(Cell p,string id,bool playerOverlaps,out string error,int rotation=0,bool upperPlayerOverlaps=false,Inventory? supply=null,int supplySlot=-1)
    {
        if(!Coordinates.InWorld(p)){error="世界边界";return false;}
        if(Get(p)!=0&&Catalog.Id(Get(p))!="water"){error="空间已被占用";return false;}
        if(playerOverlaps&&Catalog.Items[id].Solid){error="不能在角色身上放置";return false;}
        if(!Catalog.Items[id].Block){error="这不是建筑材料";return false;}
        var upper=p+new Cell(0,1,0);
        if(id=="door"&&(!Coordinates.InWorld(upper)||Get(upper)!=0||upperPlayerOverlaps)){error="门需要世界边界内的两格净空";return false;}
        if(!State.Creative&&!Consume(id,supply,supplySlot)){error="物品不足";return false;}
        Set(p,Catalog.Number(id));var def=Catalog.Items[id];
        if(def.Use!=""||def.Shape is "door" or "stairs" or "window" or "fence"||id=="wood"&&Terrain.IsLooseWood(p))State.Entities[p.ToString()]=new(){Kind=id,Rotation=rotation,LastTime=State.Time};
        if(id=="door"){Set(upper,Catalog.Number(id));State.Entities[upper.ToString()]=new(){Kind=id,Rotation=rotation,Stage=1,LastTime=State.Time};}
        State.Discovered.Add(id);error="";return true;
    }
    public Cell? DoorPartner(Cell p)
    {
        if(!State.Entities.TryGetValue(p.ToString(),out var e)||e.Kind!="door")return null;
        var other=p+new Cell(0,e.Stage==1?-1:1,0);
        return Coordinates.InWorld(other)&&Catalog.Id(Get(other))=="door"&&State.Entities.TryGetValue(other.ToString(),out var partner)&&partner.Kind=="door"&&partner.Stage==1-e.Stage&&partner.Rotation==e.Rotation?other:null;
    }
    public bool Craft(Recipe recipe,Cell? station,double[] position,out string error)
    {
        if(position is not {Length:3}||!position.All(double.IsFinite)){error="玩家位置无效";return false;}
        if(!Catalog.Recipes.TryGetValue(recipe.Id,out var canonical)||canonical!=recipe){error="配方无效";return false;}
        BlockEntity? facility=null;
        if(recipe.Station!="hand"){
            if(station is not Cell cell||!State.Entities.TryGetValue(cell.ToString(),out facility)||facility.Kind!=recipe.Station||Catalog.Id(Get(cell))!=recipe.Station){error="需要对应制作设施";return false;}
            double dx=position[0]-(cell.X+.5),dy=position[1]+1-(cell.Y+.5),dz=position[2]-(cell.Z+.5);
            if(dx*dx+dy*dy+dz*dz>36){error="离制作设施太远";return false;}
        }
        if(recipe.Seconds<=0){if(!State.Inventory.Craft(recipe,Catalog,out error))return false;State.Discovered.Add(recipe.Output);return true;}
        if(facility==null){error="请在对应设施处制作";return false;}
        if(facility.RecipeId!=null){error="设施正在制作或产物空间不足";return false;}
        var input=State.Inventory.Copy();foreach(var ingredient in recipe.Inputs)if(!input.Remove(ingredient.Key,ingredient.Value)){error="缺少 "+Catalog.Items[ingredient.Key].Name;return false;}
        var output=facility.Storage.Copy();if(output.Add(recipe.Output,recipe.Count,Catalog)>0){error="设施容器已满";return false;}
        State.Inventory=input;facility.RecipeId=recipe.Id;facility.FinishTime=State.Time+recipe.Seconds;State.Discovered.Add(recipe.Output);error="";return true;
    }
    private bool Consume(string id,Inventory? supply,int slot)
    {
        if(supply==null)return State.Inventory.Remove(id,1);
        if(!ReferenceEquals(supply,State.Inventory)&&!ReferenceEquals(supply,State.Equipment)||slot<0||slot>=supply.Slots.Length||ReferenceEquals(supply,State.Equipment)&&slot!=Items.Equipment.Offhand)return false;
        if(supply.Slots[slot] is not { } item||item.Id!=id||item.Count<1)return false;
        if(--item.Count==0)supply.Slots[slot]=null;return true;
    }
    public bool Plant(Cell ground,string seed,out string error,Inventory? supply=null,int supplySlot=-1)
    {
        if(!Catalog.Items.TryGetValue(seed,out var def)||def.Use!="seed"){error="这不是种子";return false;}
        if(!Coordinates.InWorld(ground)||Catalog.Id(Get(ground))!="farmland"){error="需要放置耕地后播种";return false;}
        var p=ground+new Cell(0,1,0);
        if(!Coordinates.InWorld(p)){error="世界边界内没有种植空间";return false;}
        if(Get(p)!=0){error="上方空间被占用";return false;}
        if(!State.Creative&&!Consume(seed,supply,supplySlot)){error="种子不足";return false;}
        Set(p,Catalog.Number("berry_bush"));State.Entities[p.ToString()]=new(){Kind="crop",CropId=seed,LastTime=State.Time,Stage=0,GrowthStepSeconds=CropGrowth.StepSeconds(seed)};error="";return true;
    }
    public bool Harvest(Cell p,out string error)
    {
        if(!State.Entities.TryGetValue(p.ToString(),out var crop)||crop.Kind!="crop"){error="此处没有作物";return false;}
        if(crop.Stage<3){error="作物尚未成熟";return false;}
        string seed=crop.CropId!,produce=CropGrowth.Produce(seed);var next=State.Inventory.Copy();
        if(next.Add(produce,3,Catalog)>0||next.Add(seed,2,Catalog)>0){error="背包空间不足";return false;}
        State.Inventory=next;Set(p,0);State.Entities.Remove(p.ToString());State.Discovered.Add(produce);error="";return true;
    }
    public bool Break(Cell p,int tier,out string error)
    {
        var d=Catalog.Block(Get(p));
        if(d==null){error="没有可挖掘的方块";return false;}
        if(d.Id=="bedrock"||d.Id=="beacon"){error="无法拆除";return false;}
        if(!State.Creative&&tier<d.Tier){error="工具等级不足";return false;}
        if(State.Entities.TryGetValue(p.ToString(),out var entity)&&Array.Exists(entity.Storage.Slots,s=>s!=null)){error="请先取出容器内物品";return false;}
        if(entity?.RecipeId!=null){error="设施仍在制作或等待产物空间，请先完成并取出物品";return false;}
        string drop=d.Id switch{"grass"=>"dirt","iron_ore"=>"raw_iron","coal_ore"=>"coal","crystal_ore"=>"crystal","berry_bush"=>"berry","loose_stone"=>"stone",_=>d.Id};
        if(entity?.Kind=="crop")drop=entity.CropId??"wheat_seed";
        var next=State.Inventory.Copy();
        if(!State.Creative&&next.Add(drop,1,Catalog)>0){error="背包已满";return false;}
        var doorPartner=DoorPartner(p);
        State.Inventory=next;Set(p,0);State.Entities.Remove(p.ToString());State.Discovered.Add(drop);
        if(doorPartner is Cell partner){Set(partner,0);State.Entities.Remove(partner.ToString());}
        if(!State.Creative&&d.Id is "coal_ore" or "iron_ore" or "crystal_ore")State.Experience+=d.Id=="crystal_ore"?4:1;
        error="";return true;
    }
    public void Die()
    {
        if(!State.Casual&&!State.Creative){var dropped=new Inventory(41);for(int i=0;i<36;i++)dropped.Slots[i]=State.Inventory.Slots[i]?.Copy();for(int i=0;i<5;i++)dropped.Slots[36+i]=State.Equipment.Slots[i]?.Copy();State.Graves.Add(new(){Position=(double[])State.Position.Clone(),Items=dropped});State.Inventory=new();State.Equipment=new(5);State.Experience=0;}
        State.Position=(double[])State.Respawn.Clone();State.Health=100;State.Hunger=80;State.Air=15;
    }
    public void Advance(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0||!double.IsFinite(State.Time+seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
        State.Time+=seconds;
        State.Weather.Advance(seconds,State.Seed);
        foreach(var pair in State.Entities){var e=pair.Value;
            if(e.Kind=="crop"){int stage=CropGrowth.Stage(e,State.Time);if(stage!=e.Stage){e.Stage=stage;var p=Cell.Parse(pair.Key);Set(p,Get(p));}}
            if(e.RecipeId!=null&&State.Time>=e.FinishTime){var r=Catalog.Recipes[e.RecipeId];var next=e.Storage.Copy();if(next.Add(r.Output,r.Count,Catalog)==0){e.Storage=next;e.RecipeId=null;}}
        }
    }
}
