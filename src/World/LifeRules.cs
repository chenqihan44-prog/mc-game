using System;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public static class LifeRules
{
    public static bool Aquatic(string kind)=>kind is "cod" or "squid";
    public static bool Animal(string kind)=>kind is "cow" or "pig" or "sheep" or "chicken" or "wolf" or "cat" or "horse" or "fox" or "armadillo" or "turtle";
    public static double Health(string kind)=>kind switch{"cow"=>40,"pig" or "sheep" or "armadillo"=>28,"chicken"=>14,"wolf" or "turtle"=>35,"cat"=>20,"horse"=>65,"fox"=>24,_=>30};
    public static string Food(string kind)=>kind switch{"cow" or "sheep" or "horse"=>"grain","pig"=>"carrot","chicken"=>"wheat_seed","wolf"=>"cooked_meat","cat"=>"raw_cod","fox"=>"berry","armadillo"=>"brown_mushroom","turtle"=>"reed",_=>""};
    public static bool ArmorFits(string kind,string id)=>kind=="wolf"?id=="wolf_armor":kind=="horse"&&id.EndsWith("_horse_armor",StringComparison.Ordinal);
    public static void Validate(CreatureState c){if(c.Id.Length!=32||!c.Id.All(Uri.IsHexDigit)||new[]{c.BabyUntil,c.LoveUntil,c.BreedReady,c.ProduceReady}.Any(v=>!double.IsFinite(v)||v<0)||c.Tamed&&c.Kind is not("wolf" or "cat" or "horse")||c.Saddled&&c.Kind!="horse"||c.Sitting&&!c.Tamed)throw new InvalidOperationException("动物状态无效");}
    public static void Drop(WorldState w,CreatureState c,string id,int count)=>w.Drop(new Stack(id,count),c.Position);
    public static void Advance(WorldState w)
    {
        var s=w.State;
        foreach(var c in s.Creatures.ToArray()){
            if(c.Health<=0||!Animal(c.Kind))continue;
            if(c.BabyUntil>0&&s.Time>=c.BabyUntil){c.BabyUntil=0;if(c.Kind=="turtle"&&c.ScutePending){Drop(w,c,"turtle_scute",1);c.ScutePending=false;}}
            if(c.Kind=="chicken"&&c.BabyUntil==0&&s.Time>=c.ProduceReady){if(c.ProduceReady>0)Drop(w,c,"egg",1);c.ProduceReady=s.Time+180;}
            if(c.LoveUntil<=s.Time||c.BabyUntil>s.Time||c.BreedReady>s.Time||s.Creatures.Count>=CreaturePopulation.Limit)continue;
            var partner=s.Creatures.FirstOrDefault(p=>p!=c&&p.Kind==c.Kind&&p.Health>0&&p.BabyUntil<=s.Time&&p.BreedReady<=s.Time&&p.LoveUntil>s.Time&&Distance(c.Position,p.Position)<16);
            if(partner==null)continue;
            c.LoveUntil=partner.LoveUntil=0;c.BreedReady=partner.BreedReady=s.Time+300;
            var baby=new CreatureState{Kind=c.Kind,Health=Math.Max(10,c.Health*.6),Position=(double[])c.Position.Clone(),BabyUntil=s.Time+360,Persistent=true,ScutePending=c.Kind=="turtle"};s.Creatures.Add(baby);
        }
    }
    public static double Distance(double[] a,double[] b)=>Math.Pow(a[0]-b[0],2)+Math.Pow(a[1]-b[1],2)+Math.Pow(a[2]-b[2],2);
    public static bool Interact(WorldState w,CreatureState c,Inventory supply,int slot,out string message)
    {
        message="";if(!w.State.Creatures.Contains(c)||c.Health<=0||slot<0||slot>=supply.Slots.Length||supply.Slots[slot] is not {} item)return false;
        if(LifeRules.Distance(c.Position,w.State.Position)>36){message="动物太远";return false;}var def=w.Catalog.Items[item.Id];bool adult=c.BabyUntil<=w.State.Time;
        var input=supply.Copy();bool Commit(string? give=null,int count=1,bool wear=false){if(!w.State.Creative){if(wear)ToolUse.Wear(input,slot,w.Catalog,false);else if(--input.Slots[slot]!.Count==0)input.Slots[slot]=null;}if(give!=null&&input.Add(give,count,w.Catalog)>0){return false;}supply.Slots=input.Slots;return true;}
        if(item.Id=="bucket"&&c.Kind=="cow"&&adult){if(c.ProduceReady>w.State.Time){message="牛需要休息后再挤奶";return false;}if(!Commit("milk_bucket")){message="背包空间不足";return false;}c.ProduceReady=w.State.Time+120;c.Persistent=true;message="取得牛奶";return true;}
        if(item.Id=="shears"&&adult&&c.Kind is "sheep" or "armadillo"){
            if(c.ProduceReady>w.State.Time){message="毛或鳞甲尚未恢复";return false;}if(!Commit(wear:true))return false;Drop(w,c,c.Kind=="sheep"?"wool":"armadillo_scute",c.Kind=="sheep"?3:1);c.ProduceReady=w.State.Time+(c.Kind=="sheep"?180:120);c.Persistent=true;message=c.Kind=="sheep"?"羊毛落在地上，接近拾取":"鳞甲落在地上，接近拾取";return true;
        }
        if(!c.Tamed&&(c.Kind=="wolf"&&item.Id=="bone"||c.Kind=="cat"&&item.Id=="raw_cod"||c.Kind=="horse"&&item.Id=="apple")){
            if(!Commit())return false;c.Tamed=true;c.Persistent=true;c.Provoked=false;message="驯服成功；Q可管理跟随、停留与装备";return true;
        }
        if(c.Tamed&&item.Id=="saddle"&&c.Kind=="horse"&&!c.Saddled){if(!Commit())return false;c.Saddled=true;message="装鞍完成，Q骑乘";return true;}
        if(c.Tamed&&def.Use=="animal_armor"&&ArmorFits(c.Kind,item.Id)){
            if(c.Armor!=null){message="先在Q管理中卸下原护甲";return false;}var armor=item.WithCount(1);armor.Durability??=def.Durability;if(!Commit())return false;c.Armor=armor;message="同伴已装备护甲";return true;
        }
        if(c.Armor?.Id=="wolf_armor"&&item.Id=="armadillo_scute"){
            int max=w.Catalog.Items["wolf_armor"].Durability;if((c.Armor.Durability??max)>=max){message="狼铠无需修理";return false;}if(!Commit())return false;c.Armor.Durability=Math.Min(max,(c.Armor.Durability??max)+55);message="狼铠已修理";return true;
        }
        if(Animal(c.Kind)&&item.Id==Food(c.Kind)){
            if(!adult){if(!Commit())return false;c.BabyUntil=Math.Max(w.State.Time,c.BabyUntil-45);c.Persistent=true;message="喂养幼体，加快成长";return true;}
            if(c.BreedReady>w.State.Time||c.LoveUntil>w.State.Time){message="需要休息后再繁殖";return false;}if(!Commit())return false;c.Health=Math.Min(Health(c.Kind),c.Health+8);c.LoveUntil=w.State.Time+60;c.Persistent=true;message="已喂食；附近同类也喂食后可繁殖";return true;
        }
        message="手持合适食物可引诱、喂食；Q查看动物用途";return false;
    }
}
