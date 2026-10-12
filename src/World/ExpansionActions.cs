using System;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public sealed partial class WorldState
{
    public bool UseExpansion(Inventory supply,int slot,Cell? hit,Cell? before,out string message)
    {
        message="背包空间不足";if(slot<0||slot>=supply.Slots.Length||supply.Slots[slot] is not {} item||!ReferenceEquals(supply,State.Inventory)&&!ReferenceEquals(supply,State.Equipment))return false;
        string id=item.Id;var def=Catalog.Items[id];var next=supply.Copy();
        bool Commit(string? result=null,int count=1,bool wear=false){if(!State.Creative){if(wear)ToolUse.Wear(next,slot,Catalog,false);else if(--next.Slots[slot]!.Count==0)next.Slots[slot]=null;}if(result!=null&&next.Add(result,count,Catalog)>0)return false;supply.Slots=next.Slots;return true;}
        if(def.Use is "tonic" or "drink"||id=="mushroom_stew"){
            if(def.Use=="tonic"&&State.Time<State.Effects.DrinkReady){message="补给冷却中";return false;}
            string? container=id switch{"golden_apple"=>null,"milk_bucket"=>"bucket","mushroom_stew"=>"bowl",_=>"glass_bottle"};if(!Commit(container))return false;
            switch(id){
                case "golden_apple":State.Health=Math.Min(100,State.Health+12);State.Effects.Shield=16;State.Effects.ShieldUntil=State.Time+90;break;
                case "healing_potion":State.Health=Math.Min(100,State.Health+30);break;
                case "fire_resistance_potion":State.Effects.FireUntil=State.Time+120;break;
                case "milk_bucket":State.Effects=new();State.Hunger=Math.Min(100,State.Hunger+12);break;
                case "mushroom_stew":State.Hunger=Math.Min(100,State.Hunger+36);break;
                default:break;
            }
            if(def.Use=="tonic")State.Effects.DrinkReady=State.Time+8;message="已使用 "+def.Name;return true;
        }
        if(hit is not Cell p||!Coordinates.InWorld(p)){message="请瞄准地面、作物或水面";return false;}
        if(id=="bucket"||id=="glass_bottle"){
            if(Catalog.Id(Get(p))!="water"){message="请瞄准水源";return false;}if(!Commit(id=="bucket"?"water_bucket":"water_bottle"))return false;message="已装水";return true;
        }
        if(id=="water_bucket"){
            if(before is not Cell cell||!Coordinates.InWorld(cell)||Get(cell)!=0){message="水源需要一格空位";return false;}if(!Commit("bucket"))return false;Set(cell,Catalog.Number("water"));var below=cell+new Cell(0,-1,0);if(Catalog.Id(Get(below)) is "grass" or "moss")Set(below,Catalog.Number("dirt"));message="放置静态水源；附近耕地保持湿润";return true;
        }
        if(id=="iron_hoe"){
            if(Catalog.Id(Get(p)) is not("grass" or "dirt")||Get(p+new Cell(0,1,0))!=0){message="上方空出的草地或泥土才能开垦";return false;}if(!Commit(wear:true))return false;Set(p,Catalog.Number("farmland"));message="已开垦耕地";return true;
        }
        if(id=="bone_meal"){
            if(!State.Entities.TryGetValue(p.ToString(),out var crop)||crop.Kind!="crop"||crop.Stage>=3){message="请对未成熟作物使用";return false;}if(!Commit())return false;crop.LastTime=Math.Max(0,crop.LastTime-crop.GrowthStepSeconds);crop.Stage++;Set(p,Get(p));message="作物生长加快";return true;
        }
        if(id=="shears"){
            var block=Catalog.Block(Get(p));if(block==null||block.Id is not("leaves" or "birch_leaves" or "pine_leaves" or "acacia_leaves" or "tall_grass" or "fern")){message="剪刀可剪羊毛、梳理犰狳，也可采树叶和野草";return false;}
            if(!Commit(wear:true))return false;Drop(new Stack(block.Id.EndsWith("leaves",StringComparison.Ordinal)?block.Id:"fiber",2),[p.X+.5,p.Y+.5,p.Z+.5]);Set(p,0);State.Entities.Remove(p.ToString());RepairTorchMounts();message="剪取成功，接近掉落物拾取";return true;
        }
        if(id=="sugar_cane"){
            string ground=Catalog.Id(Get(p));if(ground is not("sand" or "dirt" or "grass")||Get(p+new Cell(0,1,0))!=0||!NearWater(p)){message="甘蔗需要水边沙土和上方净空";return false;}if(!Commit())return false;var cell=p+new Cell(0,1,0);Set(cell,Catalog.Number("sugar_cane"));State.Entities[cell.ToString()]=new(){Kind="crop",CropId="sugar_cane",Stage=0,LastTime=State.Time,GrowthStepSeconds=100};message="已种下甘蔗";return true;
        }
        return false;
    }
    public bool NearWater(Cell p)=>new[]{new Cell(1,0,0),new Cell(-1,0,0),new Cell(0,0,1),new Cell(0,0,-1)}.Any(n=>Catalog.Id(Get(p+n))=="water"||Catalog.Id(Get(p+n+new Cell(0,1,0)))=="water");
    public bool CatchFish(Inventory supply,int slot,out string message)
    {
        if(slot<0||slot>=supply.Slots.Length){message="需要钓鱼竿";return false;}var next=supply.Copy();if(next.Slots[slot]?.Id!="fishing_rod"){message="需要钓鱼竿";return false;}ToolUse.Wear(next,slot,Catalog,State.Creative);supply.Slots=next.Slots;Drop(new Stack("raw_cod",1),[State.Position[0],State.Position[1]+.7,State.Position[2]]);State.Discovered.Add("raw_cod");message="钓到一条鳕鱼，接近拾取";return true;
    }
}
