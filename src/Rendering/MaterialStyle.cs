namespace EmberLighthouse.Rendering;

// Semantic shader tags are independent of the catalog's numeric block IDs.
public static class MaterialStyle
{
    public static byte For(string id)=>id switch{
        "grass"=>1,"wood"=>2,"plank" or "stairs" or "slab" or "fence"=>3,
        "brick" or "ruin_brick"=>4,"farmland"=>5,"door"=>6,
        "coal_ore"=>8,"iron_ore"=>9,"crystal_ore"=>10,"workbench"=>11,
        "chest"=>12,"furnace"=>13,"bookshelf"=>14,"bed"=>15,
        "lantern" or "campfire"=>16,"beacon"=>17,"cobble" or "loose_stone"=>20,
        "sand" or "gravel"=>21,"snow"=>22,"leaves" or "moss"=>23,
        "cactus"=>24,"marble" or "pillar"=>25,"copper_block"=>26,
        "tile"=>27,"wool" or "rug"=>28,"sandstone"=>29,"ice"=>31,"roof"=>32,_=>0
    };
}
