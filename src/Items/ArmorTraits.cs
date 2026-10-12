using System;
using System.Linq;
using EmberLighthouse.World;
namespace EmberLighthouse.Items;

public static class ArmorTraits
{
    public static string Material(string id)=>id=="turtle_shell"?"turtle":id=="elytra"?"elytra":id.Split('_')[0];
    public static int Pieces(WorldSnapshot s,string material)=>s.Equipment.Slots.Take(4).Count(i=>i!=null&&Material(i.Id)==material);
    public static double Mobility(WorldSnapshot s)=>1+Pieces(s,"leather")*.025+Pieces(s,"chain")*.02;
    public static double Warmth(WorldSnapshot s)=>Pieces(s,"leather")*.15;
    public static double Toughness(WorldSnapshot s)=>Pieces(s,"crystal")*.5+Pieces(s,"ember")*.75;
    public static double Knockback(WorldSnapshot s)=>Math.Min(.6,Pieces(s,"ember")*.15);
    public static double HeatDamage(WorldSnapshot s,double damage)=>damage*Math.Max(.4,1-Pieces(s,"ember")*.15)*(s.Effects.FireUntil>s.Time?.25:1);
    public static string Describe(ItemDef d)=>Material(d.Id) switch{
        "leather"=>"每件：移动速度 +2.5%，寒地饥饿增耗降低15%；可染色",
        "copper"=>"矿物过渡护甲；较高耐久，重击磨损更少",
        "chain"=>"每件：移动速度 +2%，投射物伤害降低4%；遗迹获取",
        "iron"=>"稳定防护；胸甲可安装现有耐热衬片",
        "gold"=>"低耐久，附魔每次提升2级（最高3级）",
        "crystal"=>"每件：韧性 +0.5，减少强力攻击造成的护甲穿透",
        "ember"=>"每件：韧性 +0.75，火焰伤害与击退降低15%",
        "turtle"=>"潜水耗气降低65%",
        "elytra"=>"胸部无防护；空中Space滑翔，消耗耐久",
        _=>""
    };
    public static string Color(Stack item,Catalog catalog)=>item.Dye switch{"red"=>"#c6534e","blue"=>"#5c80b0",_=>catalog.Items[item.Id].Color};
    public static bool Enchant(WorldState world,Inventory supply,int slot,bool protection,out string error)
    {
        error="请选择护甲";if(slot<0||slot>=supply.Slots.Length||supply.Slots[slot] is not {} item||world.Catalog.Items[item.Id].Use!="armor")return false;
        int current=protection?item.Protection:item.Unbreaking;if(current>=3){error="已达到三级";return false;}
        int cost=6*(current+1);var inventory=world.State.Inventory.Copy();if(!world.State.Creative&&(world.State.Experience<cost||!inventory.Remove("crystal",1))){error=$"需要经验 {cost}、晶矿 ×1";return false;}
        if(!world.State.Creative){if(ReferenceEquals(supply,world.State.Inventory))item=inventory.Slots[slot]!;world.State.Inventory.Slots=inventory.Slots;world.State.Experience-=cost;}
        int level=Math.Min(3,current+(Material(item.Id)=="gold"?2:1));if(protection)item.Protection=level;else item.Unbreaking=level;error="附魔完成";return true;
    }
}

public sealed class ActiveEffects
{
    public double FireUntil {get;set;}
    public double ShieldUntil {get;set;}
    public double Shield {get;set;}
    public double DrinkReady {get;set;}
    public void Validate(){if(new[]{FireUntil,ShieldUntil,Shield,DrinkReady}.Any(v=>!double.IsFinite(v)||v<0)||Shield>16)throw new InvalidOperationException("补给效果无效");}
    public ActiveEffects Copy()=>(ActiveEffects)MemberwiseClone();
}
