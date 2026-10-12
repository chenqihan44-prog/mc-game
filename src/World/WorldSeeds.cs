using System.Security.Cryptography;
namespace EmberLighthouse.World;

public static class WorldSeeds
{
    public static int Random(int? previous=null)
    {
        int seed;do{seed=RandomNumberGenerator.GetInt32(int.MaxValue);}while(seed==previous);return seed;
    }
}
