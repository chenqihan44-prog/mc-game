using System.Collections.Generic;

namespace EmberLighthouse.Rendering;

public static class CharacterAppearances
{
    public static readonly string[] Ids=["ranger","artisan","frost"];
    public static readonly IReadOnlyDictionary<string,string> Names=new Dictionary<string,string>{["adventurer"]="原版冒险者",["ranger"]="森野游侠",["artisan"]="铜炉工匠",["frost"]="霜境旅人"};
    public static bool IsKnown(string? id)=>id!=null&&Names.ContainsKey(id);
    public static string Normalize(string? id)=>IsKnown(id)?id!:"adventurer";
    public static string Name(string? id)=>Names[Normalize(id)];
}
