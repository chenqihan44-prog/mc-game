namespace EmberLighthouse.Rendering;

// Semantic shader tags are independent of the catalog's numeric block IDs.
public static class MaterialStyle
{
    public static byte For(string id)=>id switch{
        "grass"=>1,"wood" or "pine_log" or "acacia_log"=>2,"birch_log"=>34,"plank" or "stairs" or "slab" or "fence"=>3,
        "brick" or "ruin_brick"=>4,"farmland"=>5,"door"=>6,
        "coal_ore"=>8,"iron_ore" or "copper_vein" or "gold_ore"=>9,"crystal_ore"=>10,"workbench" or "calibration_bench" or "brewing_stand" or "enchanting_table"=>11,
        "chest"=>12,"furnace"=>13,"bookshelf"=>14,"bed"=>15,
        "lantern" or "campfire" or "dawn_heart"=>16,"torch"=>33,"beacon" or "core_console" or "energy_anchor" or "power_socket" or "reflector"=>17,"cobble"=>20,
        "sand" or "gravel"=>21,"snow"=>22,"leaves" or "birch_leaves" or "pine_leaves" or "acacia_leaves" or "moss"=>23,
        "cactus"=>24,"marble" or "pillar"=>25,"copper_block"=>26,
        "tile"=>27,"wool" or "red_wool" or "blue_wool" or "rug"=>28,"sandstone"=>29,"ice"=>31,"roof"=>32,_=>0
    };
}
