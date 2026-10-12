using System;
using System.Linq;
using EmberLighthouse.World;

namespace EmberLighthouse.Items;

public static class Equipment
{
    public const int Offhand=4;
    public static bool Accepts(Catalog catalog,int slot,Stack? item)=>slot>=0&&slot<=Offhand&&(item==null||catalog.Items.TryGetValue(item.Id,out var def)&&(slot==Offhand||def.ArmorSlot==slot&&item.Count==1));
    public static void Validate(Inventory equipment,Catalog catalog)
    {
        equipment.Validate(catalog);if(equipment.Slots.Length!=5||equipment.Slots.Where((item,i)=>!Accepts(catalog,i,item)).Any())throw new InvalidOperationException("装备槽内容无效");
    }
    public static int ArmorPoints(WorldSnapshot state,Catalog catalog){int points=0;for(int i=0;i<4;i++)if(state.Equipment.Slots[i] is { } item)points+=catalog.Items[item.Id].ArmorPoints;return Math.Min(20,points);}
    public static double Absorb(WorldSnapshot state,Catalog catalog,double damage)
    {
        if(state.Creative||!double.IsFinite(damage)||damage<=0)return 0;
        double effective=ArmorPoints(state,catalog);if(damage>20)effective=Math.Max(effective*.6,effective-(damage-20)/(5+ArmorTraits.Toughness(state)*2));
        double protection=state.Equipment.Slots.Take(4).Where(i=>i!=null).Sum(i=>i!.Protection)*.015;
        double result=damage*Math.Max(.15,1-effective*.04)*(1-Math.Min(.18,protection));int wear=(int)Math.Min(int.MaxValue,Math.Max(1,Math.Ceiling(damage/4)));
        for(int i=0;i<4;i++)if(state.Equipment.Slots[i] is { } item){double factor=ArmorTraits.Material(item.Id)=="copper"?.8:1;int loss=Math.Max(1,(int)Math.Ceiling(wear*factor/(1+item.Unbreaking*.5)));int remaining=(item.Durability??catalog.Items[item.Id].Durability)-loss;item.Durability=remaining;if(remaining<=0)state.Equipment.Slots[i]=null;}
        return result;
    }
    public static bool SwapHands(WorldSnapshot state,int selected)
    {
        if(selected<0||selected>=9)return false;(state.Inventory.Slots[selected],state.Equipment.Slots[Offhand])=(state.Equipment.Slots[Offhand],state.Inventory.Slots[selected]);return true;
    }
}
