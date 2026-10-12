namespace EmberLighthouse.World;

public static class CropGrowth
{
    public static double StepSeconds(string seed)=>seed switch{
        "carrot_seed"=>60,"potato_seed"=>120,"pumpkin_seed"=>100,"melon_seed"=>80,"sugar_cane_seed"=>100,_=>90
    };
    public static string Produce(string seed)=>seed switch{
        "wheat_seed"=>"grain","carrot_seed"=>"carrot","pumpkin_seed"=>"pumpkin","melon_seed"=>"melon","sugar_cane"=>"sugar_cane",_=>"potato"
    };
    public static int Stage(BlockEntity crop,double worldTime)
    {
        // Compare the same absolute deadline used when advancing the clock.
        // Subtracting fractional timestamps can round an exact deadline below a stage.
        if(worldTime<=crop.LastTime)return crop.Stage;
        for(int stage=3;stage>crop.Stage;stage--)if(worldTime>=crop.LastTime+crop.GrowthStepSeconds*stage)return stage;
        return crop.Stage;
    }
}
