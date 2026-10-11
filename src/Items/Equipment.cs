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
        double result=damage*(1-ArmorPoints(state,catalog)*.04);int wear=(int)Math.Min(int.MaxValue,Math.Max(1,Math.Ceiling(damage/4)));
        for(int i=0;i<4;i++)if(state.Equipment.Slots[i] is { } item){int remaining=(item.Durability??catalog.Items[item.Id].Durability)-wear;item.Durability=remaining;if(remaining<=0)state.Equipment.Slots[i]=null;}
        return result;
    }
    public static bool SwapHands(WorldSnapshot state,int selected)
    {
        if(selected<0||selected>=9)return false;(state.Inventory.Slots[selected],state.Equipment.Slots[Offhand])=(state.Equipment.Slots[Offhand],state.Inventory.Slots[selected]);return true;
    }
}
