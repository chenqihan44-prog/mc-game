using System;
using System.Collections.Generic;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;
public static class StarterSupplies
{
    public static Dictionary<(int X,int Z),string> Generate(int seed,Catalog catalog)
    {
        var result=new Dictionary<(int X,int Z),string>();var ground=new Terrain(seed,catalog,4);
        string[] items=["wood","loose_stone","berry_bush","wood","loose_stone","wood","berry_bush","loose_stone","wood","loose_stone","wood","berry_bush","loose_stone","wood","loose_stone"];
        for(int attempt=0;attempt<4096&&result.Count<items.Length;attempt++){
            uint n=RandomField.Hash(seed,attempt,0,0,220);int x=(int)(n%41)-20,z=(int)((n>>12)%41)-20,r=x*x+z*z;
            if(r<36||r>400||ground.Surface(x,z)<=Terrain.SeaLevel)continue;
            var cell=new Cell(x,ground.Surface(x,z)+1,z);string occupying=catalog.Id(ground.Get(cell));if(occupying is "birch_log" or "pine_log" or "acacia_log" or "leaves" or "birch_leaves" or "pine_leaves" or "acacia_leaves"||occupying=="wood"&&!ground.IsLooseWood(cell))continue;
            bool near=false;foreach(var p in result.Keys)if((p.X-x)*(p.X-x)+(p.Z-z)*(p.Z-z)<9){near=true;break;}if(near)continue;
            result.Add((x,z),items[result.Count]);
        }
        if(result.Count!=items.Length)throw new InvalidOperationException("无法生成可采集的起始物资");return result;
    }
    public static WorldSnapshot Refresh(WorldSnapshot original,Catalog catalog)
    {
        if(original.GeneratorVersion!=4)throw new InvalidOperationException("此更新适用于地形版本 4 的世界");
        var copy=original.Copy();var old=new WorldState(catalog,original.Copy());
        // Some placed blocks match the old natural material and only have an entity.
        foreach(string key in copy.Entities.Keys)if(!copy.Changes.ContainsKey(key)){ushort id=old.Get(Cell.Parse(key));if(id!=0)copy.Changes[key]=catalog.Id(id);}
        copy.GeneratorVersion=5;return copy;
    }
}
