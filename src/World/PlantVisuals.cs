namespace EmberLighthouse.World;
public static class PlantVisuals
{
    public const int Count=16;
    public static int Style(string id)=>id switch{"tall_grass"=>5,"dry_grass"=>6,"fern"=>7,"reed"=>8,"red_flower"=>9,"blue_flower"=>10,"brown_mushroom"=>11,"red_mushroom"=>12,"sugar_cane"=>13,_=>0};
    public static float Height(string id)=>id switch{"brown_mushroom" or "red_mushroom"=>.38f,"fern"=>.65f,"red_flower" or "blue_flower"=>.75f,"reed"=>1.3f,_=>.95f};
}
