using System;
using System.Collections.Generic;
namespace EmberLighthouse.Items;

public static class MiningRules
{
    private static readonly HashSet<string> rock=["stone","basalt","cobble","coal_ore","iron_ore","crystal_ore","gold_ore","copper_vein","calibration_bench","ruin_brick","brick","sandstone","marble","polished_basalt","pillar","roof","tile","furnace","copper_block"];
    private static readonly HashSet<string> timber=["wood","birch_log","pine_log","acacia_log","plank","workbench","chest","bookshelf","stairs","slab","door","fence","window","bed"];
    public static string RequiredTool(ItemDef block)=>rock.Contains(block.Id)?"pick":timber.Contains(block.Id)?"axe":block.Id is "grass" or "dirt" or "sand" or "snow" or "gravel" or "clay" or "farmland"?"shovel":"";
    public static int HarvestTier(ItemDef? tool)=>tool?.Use=="pick"?Math.Min(3,tool.Tier+1):0;
    public static bool CanHarvest(ItemDef block,ItemDef? tool)=>RequiredTool(block)!="pick"&&block.Tier==0||tool?.Use=="pick"&&HarvestTier(tool)>=Math.Max(1,block.Tier);
    public static double Seconds(ItemDef block,ItemDef? tool,bool looseWood=false)
    {
        if(looseWood||block.Id=="loose_stone")return .45;
        string required=RequiredTool(block);bool matched=required!=""&&tool?.Use==required;
        double efficiency=matched?tool!.Tier switch{0=>2,1=>4,2=>6,_=>9}:1;
        double effort=matched?1.5:required=="pick"?5:required=="axe"?2.2:1.8;
        return Math.Max(.2,block.Hardness*effort/efficiency);
    }
}

public static class ToolUse
{
    public static bool Wear(Inventory inventory,int slot,Catalog catalog,bool creative,int amount=1)
    {
        if(creative||amount<=0||slot<0||slot>=inventory.Slots.Length||inventory.Slots[slot] is not { } item)return false;
        var def=catalog.Items[item.Id];if(def.Durability<=0||def.Use is not("pick" or "axe" or "shovel" or "sword" or "bow" or "shield"))return false;
        int remaining=(item.Durability??def.Durability)-amount;if(remaining<=0){inventory.Slots[slot]=null;return true;}item.Durability=remaining;return false;
    }
}
