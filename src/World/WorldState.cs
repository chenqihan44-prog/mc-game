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
    public string LootTheme {get;set;}="";
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
    public string? OwnerId {get;set;}
    public double[] Position {get;set;}=[0,40,0];
    public Inventory Items {get;set;}=new();
    public Dictionary<int,double[]> ItemPositions {get;set;}=[];
    public Dictionary<int,double[]> ItemVelocities {get;set;}=[];
    public double PickupAfter {get;set;}
    public bool LegacyContainer {get;set;}
    public bool Death {get;set;}
}
public sealed class WorldSnapshot
{
    public const int CurrentFormat=8;
    public Dictionary<string,MultiplayerPlayerState> Players {get;set;}=[];
    public int SelectedHotbar {get;set;}
    public bool BonusChest {get;set;}
    public bool BonusChestInitialized {get;set;}
    public ActiveEffects Effects {get;set;}=new();
    public int FormatVersion {get;set;}=CurrentFormat;
    public int GeneratorVersion {get;set;}=Terrain.Version;
    public string Name {get;set;}="新世界";
    public string WorldId {get;set;}=Guid.NewGuid().ToString("N");
    public int Seed {get;set;}=42;
    public bool Creative {get;set;}
    public bool Casual {get;set;}
    public double Time {get;set;}=240;
    // Optional in existing saves: inherit their current sun phase on first advance.
    public double? DayTime {get;set;}
    [System.Text.Json.Serialization.JsonIgnore] public double SkyTime=>DayTime??Time;
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
    public HashSet<int> ExploredMap {get;set;}=[];
    public bool GuardianDefeated {get;set;}
    public MainlineProgress Mainline {get;set;}=new();
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
        if(Players==null||Players.Any(p=>p.Value==null||p.Key!=p.Value.Id))throw new InvalidOperationException("联机玩家集合无效");
        foreach(var player in Players.Values)player.Validate();
        if(SelectedHotbar<0||SelectedHotbar>8)throw new InvalidOperationException("快捷栏选择无效");
        if(Mainline==null||Effects==null)throw new InvalidOperationException("主线进度或补给效果缺失");Mainline.Validate();Effects.Validate();
        static bool PositionOk(double[]? position)=>position is {Length:3}&&position.All(double.IsFinite);
        if(string.IsNullOrWhiteSpace(Name)||WorldId==null||WorldId.Length!=32||!WorldId.All(Uri.IsHexDigit)||!PositionOk(Position)||!PositionOk(Respawn)||!double.IsFinite(Time)||Time<0||!double.IsFinite(Health)||!double.IsFinite(Hunger)||!double.IsFinite(Air)||Health<0||Health>100||Hunger<0||Hunger>100||Air<0||Air>15||Experience<0)throw new InvalidOperationException("玩家状态或世界标识无效");
        if(DayTime is { } dayTime&&(!double.IsFinite(dayTime)||dayTime<0))throw new InvalidOperationException("昼夜计时无效");
        if(ExploredMap==null||ExploredMap.Any(i=>i<0||i>=MapExploration.Count))throw new InvalidOperationException("地图探索记录无效");
        if(Changes==null||Entities==null||Inventory==null||Crafting==null||Equipment==null||Graves==null||Beacons==null||Discovered==null||Creatures==null||FavoriteRecipes==null||Markers==null||Weather==null||Population==null||Projectiles==null)throw new InvalidOperationException("存档缺少状态集合");
        if(Creatures.Any(c=>c==null||!CreatureRules.Known(c.Kind)||c.Home is not("" or "vault" or "vault_guard")||c.AttackStyle is not("" or "none" or "bite" or "melee" or "charge" or "bolt" or "slam" or "explode")))throw new InvalidOperationException("生物类型或战斗归属无效");
        if(!double.IsFinite(Population.NextSpawnTime)||Population.NextSpawnTime<0||Population.Sequence<0||Creatures.Count>CreaturePopulation.Limit||Creatures.Where(c=>CreatureRules.Elite(c.Kind)).GroupBy(c=>c.Kind).Any(g=>g.Count()>1))throw new InvalidOperationException("生物数量或生成进度无效");
        if(Entities.Any(p=>p.Value==null||p.Value.Storage==null||!Coordinates.InWorld(Cell.Parse(p.Key)))||Graves.Any(g=>g==null||g.Items==null||!PositionOk(g.Position))||Creatures.Any(c=>c==null||!PositionOk(c.Position)||!double.IsFinite(c.Health))||Markers.Any(p=>p is not {Length:2}||!p.All(double.IsFinite))||Beacons.Any(i=>i<0||i>=Terrain.Towers.Length))throw new InvalidOperationException("容器、生物、背包或地图状态无效");
        foreach(var g in Graves)if(g.ItemPositions==null||g.ItemVelocities==null||!double.IsFinite(g.PickupAfter)||g.PickupAfter<0||g.ItemPositions.Any(p=>p.Key<0||p.Key>=g.Items.Slots.Length||!PositionOk(p.Value))||g.ItemVelocities.Any(p=>p.Key<0||p.Key>=g.Items.Slots.Length||!PositionOk(p.Value)))throw new InvalidOperationException("掉落物状态无效");
        foreach(var c in Creatures){if(!double.IsFinite(c.AttackWindup)||c.AttackWindup< -1||c.AttackWindup>5||!double.IsFinite(c.AttackCooldown)||c.AttackCooldown<0||c.AttackCooldown>10||!double.IsFinite(c.ChargeRemaining)||c.ChargeRemaining<0||c.ChargeRemaining>4||c.AttackAim!=null&&!PositionOk(c.AttackAim)||(c.AttackWindup>=0||c.ChargeRemaining>0||c.StrikeRemaining>0)&&c.AttackAim==null||!double.IsFinite(c.StrikeRemaining)||c.StrikeRemaining<0||c.StrikeRemaining>1||!double.IsFinite(c.RecoveryRemaining)||c.RecoveryRemaining<0||c.RecoveryRemaining>2||!double.IsFinite(c.AttackWindupDuration)||c.AttackWindupDuration<0||c.AttackWindupDuration>5||c.FacingYaw is {} yaw&&!double.IsFinite(yaw)||c.AttackTargetId==null||c.AttackTargetId!=""&&(c.AttackTargetId.Length!=32||!c.AttackTargetId.All(Uri.IsHexDigit)))throw new InvalidOperationException("生物战斗状态无效");LifeRules.Validate(c);}
        if(Creatures.Select(c=>c.Id).Distinct().Count()!=Creatures.Count)throw new InvalidOperationException("生物标识重复");
        if(Projectiles.Count>ProjectileFlight.Limit||Projectiles.Any(p=>p==null))throw new InvalidOperationException("投射物数量无效");foreach(var p in Projectiles)p.Validate();
    }
    public WorldSnapshot Copy()
    {
        ValidateShape();
        var copy=(WorldSnapshot)MemberwiseClone();copy.Position=(double[])Position.Clone();copy.Respawn=(double[])Respawn.Clone();copy.Changes=new(Changes);copy.Inventory=Inventory.Copy();copy.Crafting=Crafting.Copy();copy.Equipment=Equipment.Copy();copy.Cursor=Cursor?.Copy();
        copy.Entities=Entities.ToDictionary(p=>p.Key,p=>new BlockEntity{Kind=p.Value.Kind,LootTheme=p.Value.LootTheme,Storage=p.Value.Storage.Copy(),LastTime=p.Value.LastTime,Stage=p.Value.Stage,Rotation=p.Value.Rotation,Open=p.Value.Open,RecipeId=p.Value.RecipeId,FinishTime=p.Value.FinishTime,CropId=p.Value.CropId,GrowthStepSeconds=p.Value.GrowthStepSeconds});
        copy.Players=Players.ToDictionary(p=>p.Key,p=>p.Value.Copy());
        copy.Graves=Graves.Select(g=>new Grave{OwnerId=g.OwnerId,Position=(double[])g.Position.Clone(),Items=g.Items.Copy(),PickupAfter=g.PickupAfter,LegacyContainer=g.LegacyContainer,Death=g.Death,ItemPositions=g.ItemPositions.ToDictionary(p=>p.Key,p=>(double[])p.Value.Clone()),ItemVelocities=g.ItemVelocities.ToDictionary(p=>p.Key,p=>(double[])p.Value.Clone())}).ToList();copy.Beacons=new(Beacons);copy.Discovered=new(Discovered);copy.ExploredMap=new(ExploredMap);copy.FavoriteRecipes=new(FavoriteRecipes);copy.Markers=Markers.Select(p=>(double[])p.Clone()).ToList();
        copy.Effects=Effects.Copy();copy.Mainline=Mainline.Copy();copy.Creatures=Creatures.Select(c=>c.Copy()).ToList();copy.Projectiles=Projectiles.Select(p=>p.Copy()).ToList();copy.Population=new(){Initialized=Population.Initialized,NextSpawnTime=Population.NextSpawnTime,Sequence=Population.Sequence};copy.Weather=new(){Phase=Weather.Phase,Cycle=Weather.Cycle,Remaining=Weather.Remaining,Blend=Weather.Blend};return copy;
    }
}

public sealed partial class WorldState
{
    public Catalog Catalog {get;}
    public Terrain Terrain {get;}
    public WorldSnapshot State {get;}
    public bool MultiplayerActive {get;set;}
    public string ActivePlayerId {get;set;}="host";
    public Dictionary<Cell,ushort> Changes {get;}=[];
    private readonly Dictionary<Cell,Dictionary<Cell,ushort>> changesByChunk=[];
    public long Session {get;}=DateTime.UtcNow.Ticks;
    public Dictionary<Cell,int> Revisions {get;}=[];
    private readonly Dictionary<Cell,int> collisionRevisions=[];
    public int CollisionRevision(Cell chunk)=>collisionRevisions.GetValueOrDefault(chunk);
    public long EditVersion {get;private set;}
    public long ExplorationVersion {get;private set;}
    private double lastMapX=double.NaN,lastMapZ=double.NaN;
    public void RevealMap(double x,double z)
    {
        if(!double.IsFinite(x)||!double.IsFinite(z))return;
        if((x-lastMapX)*(x-lastMapX)+(z-lastMapZ)*(z-lastMapZ)<4)return;
        lastMapX=x;lastMapZ=z;if(MapExploration.Reveal(State.ExploredMap,x,z))ExplorationVersion++;
    }
    public bool MapKnown(int x,int z)=>MapExploration.Known(State.ExploredMap,x,z);
    public WorldState(Catalog catalog,WorldSnapshot snapshot)
    {
        Catalog=catalog;State=snapshot;
        snapshot.ValidateShape();
        foreach(var player in snapshot.Players.Values)player.Validate(catalog);
        if(!Terrain.Supports(snapshot.GeneratorVersion)||snapshot.FormatVersion!=WorldSnapshot.CurrentFormat)throw new InvalidOperationException("不支持的世界版本，请保留原档");
        snapshot.Weather.Validate();
        snapshot.Inventory.Validate(catalog);
        if(snapshot.Inventory.Slots.Length!=36)throw new InvalidOperationException("玩家背包格数无效");
        snapshot.Crafting.Validate(catalog);
        Items.Equipment.Validate(snapshot.Equipment,catalog);
        if(snapshot.Crafting.Slots.Length!=4)throw new InvalidOperationException("合成格数量无效");
        if(snapshot.Cursor!=null)new Inventory(1){Slots=[snapshot.Cursor]}.Validate(catalog);
        foreach(var e in snapshot.Entities.Values){
            if(e.Kind=="torch"&&(e.Stage<0||e.Stage>4))throw new InvalidOperationException("火把附着方向无效");
            e.Storage.Validate(catalog);
            if(!double.IsFinite(e.LastTime)||!double.IsFinite(e.FinishTime)||e.LastTime<0||e.FinishTime<0||e.Rotation<0||e.Rotation>3||e.RecipeId!=null&&(!catalog.Recipes.TryGetValue(e.RecipeId,out var recipe)||recipe.Station!=e.Kind))throw new InvalidOperationException("设施计时、朝向或配方无效");
            if(e.Kind=="crop"&&(e.Stage<0||e.Stage>3||e.CropId==null||!catalog.Items.TryGetValue(e.CropId,out var seed)||seed.Use is not("seed" or "cane")))throw new InvalidOperationException("作物状态无效");
            if(e.Kind=="crop"&&(!double.IsFinite(e.GrowthStepSeconds)||e.GrowthStepSeconds<30||e.GrowthStepSeconds>300))throw new InvalidOperationException("作物生长周期无效");
        }
        foreach(var g in snapshot.Graves)g.Items.Validate(catalog);
        foreach(var c in snapshot.Creatures)if(c.Armor!=null){new Inventory(1){Slots=[c.Armor]}.Validate(catalog);if(!LifeRules.ArmorFits(c.Kind,c.Armor.Id))throw new InvalidOperationException("生物护甲无效");}
        Terrain=new(snapshot.Seed,catalog,snapshot.GeneratorVersion);
        foreach(var p in snapshot.Changes){var c=Cell.Parse(p.Key);if(!Coordinates.InWorld(c))throw new InvalidOperationException("存档坐标越界");Changes[c]=p.Value=="air"?(ushort)0:catalog.Number(p.Value);IndexChange(c,Changes[c]);}
    }
    public ushort NaturalBlock(Cell p)=>NaturalRules.Correct(Terrain,Catalog,p,Terrain.Get(p),State.Entities.ContainsKey(p.ToString()));
    public ushort Get(Cell p)=>Changes.TryGetValue(p,out var id)?id:NaturalBlock(p);
    public bool Solid(Cell p)=>Catalog.Block(Get(p))?.Solid==true && !(Terrain.IsLooseWood(p)&&!Changes.ContainsKey(p)&&!State.Entities.ContainsKey(p.ToString())) && !(State.Entities.TryGetValue(p.ToString(),out var e)&&e.Kind=="door"&&e.Open);
    public void Set(Cell p,ushort id)
    {
        if(!Coordinates.InWorld(p))throw new ArgumentOutOfRangeException(nameof(p));
        ushort previous=Get(p);var shape=Catalog.Block(id)?.Shape;
        bool geometryChanged=previous!=id||shape is "door" or "stairs" or "window" or "fence" or "brewing";
        bool skyChanged=Catalog.Block(previous)?.Solid==true||Catalog.Block(id)?.Solid==true;
        EditVersion++;
        if(NaturalBlock(p)==id){Changes.Remove(p);State.Changes.Remove(p.ToString());}else{Changes[p]=id;State.Changes[p.ToString()]=Catalog.Id(id);}
        if(Changes.ContainsKey(p))IndexChange(p,id);else if(changesByChunk.TryGetValue(Coordinates.Chunk(p),out var edits)){edits.Remove(p);if(edits.Count==0)changesByChunk.Remove(Coordinates.Chunk(p));}
        // Lighting reaches neighboring chunks, even when the edited face is
        // away from a chunk boundary.
        var c=Coordinates.Chunk(p);for(int x=-1;x<=1;x++)for(int y=0;y<6;y++)for(int z=-1;z<=1;z++)if(skyChanged||Math.Abs(y-c.Y)<=1)Bump(new(c.X+x,y,c.Z+z));
        if(geometryChanged){var affected=new HashSet<Cell>{c};foreach(var n in Coordinates.Neighbors)affected.Add(Coordinates.Chunk(p+n));foreach(var chunk in affected)if(chunk.Y>=0&&chunk.Y<6)collisionRevisions[chunk]=CollisionRevision(chunk)+1;}
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
        var copy=new Dictionary<Cell,ushort>();for(int x=Coordinates.FloorDiv(centerX-radius,16);x<=Coordinates.FloorDiv(centerX+radius,16);x++)for(int z=Coordinates.FloorDiv(centerZ-radius,16);z<=Coordinates.FloorDiv(centerZ+radius,16);z++)for(int y=0;y<6;y++)if(changesByChunk.TryGetValue(new(x,y,z),out var edits))foreach(var edit in edits)if(Math.Abs(edit.Key.X-centerX)<=radius&&Math.Abs(edit.Key.Z-centerZ)<=radius)copy.Add(edit.Key,edit.Value);
        foreach(var pair in State.Entities){var p=Cell.Parse(pair.Key);if(Math.Abs(p.X-centerX)<=radius&&Math.Abs(p.Z-centerZ)<=radius)copy.TryAdd(p,Get(p));}return copy;
    }
    public int Revision(Cell c)=>Revisions.GetValueOrDefault(c);
    public bool Place(Cell p,string id,bool playerOverlaps,out string error,int rotation=0,bool upperPlayerOverlaps=false,Inventory? supply=null,int supplySlot=-1,Cell? support=null)
    {
        if(!Coordinates.InWorld(p)){error="世界边界";return false;}
        if(Get(p)!=0&&Catalog.Id(Get(p))!="water"){error="空间已被占用";return false;}
        if(playerOverlaps&&Catalog.Items[id].Solid){error="不能在角色身上放置";return false;}
        if(!Catalog.Items[id].Block){error="这不是建筑材料";return false;}
        int attachment=id=="torch"?TorchMount.Find(this,p,support):0;if(attachment<0){error="火把需要坚实地面或墙面支撑";return false;}
        var upper=p+new Cell(0,1,0);
        if(id=="door"&&(!Coordinates.InWorld(upper)||Get(upper)!=0||upperPlayerOverlaps)){error="门需要世界边界内的两格净空";return false;}
        if(!State.Creative&&!Consume(id,supply,supplySlot)){error="物品不足";return false;}
        Set(p,Catalog.Number(id));var def=Catalog.Items[id];
        if(def.Use!=""||def.Shape is "door" or "stairs" or "window" or "fence"||id=="wood"&&Terrain.IsLooseWood(p))State.Entities[p.ToString()]=new(){Kind=id,Rotation=rotation,LastTime=State.Time};
        if(id=="torch")State.Entities[p.ToString()].Stage=attachment;
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
        if(!global::EmberLighthouse.World.Mainline.RecipeKnown(State,recipe)){error="需要先修复对应灯塔解锁此配方";return false;}
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
        State.Inventory=input;facility.RecipeId=recipe.Id;facility.FinishTime=State.Time+recipe.Seconds;if(facility.Kind=="furnace"&&station is Cell lit)Set(lit,Get(lit));State.Discovered.Add(recipe.Output);error="";return true;
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
        string seed=crop.CropId!,produce=CropGrowth.Produce(seed);var items=new Inventory(2);items.Add(produce,3,Catalog);items.Add(seed,2,Catalog);
        Drop(items,[p.X+.5,p.Y+.3,p.Z+.5]);Set(p,0);State.Entities.Remove(p.ToString());State.Discovered.Add(produce);error="";return true;
    }
    public bool Break(Cell p,int tier,out string error)
    {
        var d=Catalog.Block(Get(p));
        if(d==null){error="没有可挖掘的方块";return false;}
        if(d.Id is "quest_record" or "power_socket" or "reflector" or "core_console" or "energy_anchor"){error="古代设备不能拆除；按 Q 操作";return false;}
        if(d.Id=="bedrock"||d.Id=="beacon"){error="无法拆除";return false;}
        if(!State.Creative&&tier<d.Tier){error="工具等级不足";return false;}
        if(State.Entities.TryGetValue(p.ToString(),out var entity)&&Array.Exists(entity.Storage.Slots,s=>s!=null)){error="请先取出容器内物品";return false;}
        if(entity?.RecipeId!=null){error="设施仍在制作或等待产物空间，请先完成并取出物品";return false;}
        string drop=d.Id switch{"birch_log" or "pine_log" or "acacia_log"=>"wood","grass"=>"dirt","iron_ore"=>"raw_iron","copper_vein"=>"copper_ore","coal_ore"=>"coal","crystal_ore"=>"crystal","berry_bush"=>"berry","loose_stone"=>"stone",_=>d.Id};
        if(entity?.Kind=="crop")drop=entity.CropId??"wheat_seed";
        var items=new Inventory(3);if(!State.Creative){items.Add(drop,1,Catalog);if(d.Id=="leaves"&&RandomField.Hash(State.Seed,p.X,p.Y,p.Z,271)%5==0)items.Add("apple",1,Catalog);if(State.GeneratorVersion<6&&d.Id=="crystal_ore"&&RandomField.Hash(State.Seed,p.X/2,p.Y/2,p.Z/2,272)%5==0)items.Add("gold_ore",1,Catalog);Drop(items,[p.X+.5,p.Y+.5,p.Z+.5]);}
        var doorPartner=DoorPartner(p);
        Set(p,0);State.Entities.Remove(p.ToString());State.Discovered.Add(drop);
        if(doorPartner is Cell partner){Set(partner,0);State.Entities.Remove(partner.ToString());}
        RepairTorchMounts();
        if(!State.Creative&&d.Id is "coal_ore" or "iron_ore" or "crystal_ore")State.Experience+=d.Id=="crystal_ore"?4:1;
        error="";return true;
    }
    public void Die()
    {
        bool finale=global::EmberLighthouse.World.Mainline.Enabled(State)&&State.Mainline.FinaleActive;
        if(!State.Casual&&!State.Creative){var dropped=new Inventory(41);for(int i=0;i<36;i++)dropped.Slots[i]=State.Inventory.Slots[i]?.Copy();for(int i=0;i<5;i++)dropped.Slots[36+i]=State.Equipment.Slots[i]?.Copy();Drop(dropped,State.Position,.5,true);State.Inventory=new();State.Equipment=new(5);State.Experience=0;}
        if(finale){var entry=Terrain.Adventure!.Site("vault").Entry;if(!State.Casual&&!State.Creative)State.Graves.Last().Position=[entry.X+.5,entry.Y,entry.Z+.5];if(!MultiplayerActive)global::EmberLighthouse.World.Mainline.ResetFinale(this);}
        State.Position=(double[])State.Respawn.Clone();State.Health=100;State.Hunger=80;State.Air=15;State.Effects=new();
    }
    public void Advance(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0||!double.IsFinite(State.Time+seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
        State.DayTime=DayCycle.Advance(State.SkyTime,seconds);State.Time+=seconds;
        State.Weather.Advance(seconds,State.Seed);
        LifeRules.Advance(this);
        if(State.Effects.ShieldUntil<=State.Time)State.Effects.Shield=0;
        foreach(var pair in State.Entities){var e=pair.Value;
            if(e.Kind=="crop"){var p=Cell.Parse(pair.Key);if(NearWater(p+new Cell(0,-1,0)))e.LastTime=Math.Max(0,e.LastTime-seconds*.2);int stage=CropGrowth.Stage(e,State.Time);if(stage!=e.Stage){e.Stage=stage;Set(p,Get(p));}}
            if(e.RecipeId!=null&&State.Time>=e.FinishTime){var r=Catalog.Recipes[e.RecipeId];var next=e.Storage.Copy();if(next.Add(r.Output,r.Count,Catalog)==0){e.Storage=next;e.RecipeId=null;if(e.Kind=="furnace"){var p=Cell.Parse(pair.Key);Set(p,Get(p));}}}
        }
    }
}

