using System;
using System.Collections.Generic;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public static class ChestLoot
{
    private readonly record struct Drop(string Id,int Min,int Max,int Weight=1);
    private static readonly Drop[] Camp=[new("grain",2,5,4),new("carrot",1,4,3),new("potato",1,4,3),new("coal",1,4,2),new("copper_ore",1,3,2),new("plank",3,9,4),new("rope",1,3,2),new("bread",1,2,2),new("torch",2,5,3)];
    private static readonly Drop[] Mine=[new("raw_iron",2,6,5),new("copper_ore",3,8,5),new("coal",3,7,4),new("torch",3,7,4),new("plank",4,10,2),new("stone_pick",1,1),new("repair_kit",1,1),new("bread",1,3,2)];
    private static readonly Drop[] Forge=[new("coal",4,9,5),new("iron_ingot",1,3,3),new("raw_iron",2,5,3),new("copper_ingot",1,4,3),new("heat_lining",1,1),new("repair_kit",1,2,2),new("brick",5,12,3),new("cooked_meat",1,3,2)];
    private static readonly Drop[] Snow=[new("cloth",2,5,4),new("leather",1,3,3),new("arrow",5,14,5),new("rope",2,5,4),new("torch",3,7,3),new("bread",2,4,3),new("glass",2,5,2),new("bow",1,1)];
    private static readonly Drop[] Cave=[new("crystal",1,4,2),new("coal",3,8,4),new("torch",4,8,5),new("repair_kit",1,2,3),new("raw_iron",2,6,4),new("cooked_meat",2,4,2),new("arrow",5,12,3)];
    private static readonly Drop[] Vault=[new("repair_kit",1,3,4),new("arrow",8,18,5),new("cooked_meat",2,5,4),new("plank",8,16,4),new("torch",4,8,3),new("iron_ingot",2,4,2),new("coal",3,6,2)];
    public static (string Theme,Inventory Items) Generate(WorldState world,Cell cell)
    {
        var adventure=world.Terrain.Adventure;string site=adventure?.Nearby(cell)?.Id??"";if(site.Length>0&&!adventure!.Inside(cell,site))site="";bool starter=Math.Abs(cell.X-34)<2&&Math.Abs(cell.Z-26)<2;
        string theme=site switch{"mine"=>"矿工补给","forge"=>"熔炉维修","snow"=>"雪岭远征","cave"=>"晶穴勘探","vault"=>"遗址军需",_=>starter?"废营地补给":world.Terrain.Biome(cell.X,cell.Z) switch{2=>"旱地物资",3=>"寒地物资",_=>"野外行囊"}};
        var table=site switch{"mine"=>Mine,"forge"=>Forge,"snow"=>Snow,"cave"=>Cave,"vault"=>Vault,_=>world.Terrain.Biome(cell.X,cell.Z) switch{2=>Forge,3=>Snow,_=>Camp}};
        var pool=new List<Drop>(table);pool.AddRange([new("apple",1,3,2),new("pumpkin_seed",1,3),new("melon_seed",1,3),new("sugar_cane",1,3),new("gold_ore",1,3)]);
        if(site is "mine" or "forge" or "vault")foreach(string armor in new[]{"chain_helmet","chain_chestplate","chain_leggings","chain_boots"})pool.Add(new(armor,1,1));
        if(!Mainline.Enabled(world.State))pool.RemoveAll(d=>d.Id is "repair_kit" or "heat_lining");var items=new Inventory();int rolls=4+(int)(RandomField.Hash(world.State.Seed,cell.X,cell.Y,cell.Z,101)%3);
        for(int i=0;i<rolls&&pool.Count>0;i++){
            int total=0;foreach(var d in pool)total+=d.Weight;int draw=(int)(RandomField.Hash(world.State.Seed,cell.X,cell.Y,cell.Z,102+i*2)%(uint)total),index=0;
            while(draw>=pool[index].Weight){draw-=pool[index].Weight;index++;}var drop=pool[index];pool.RemoveAt(index);int count=drop.Min+(int)(RandomField.Hash(world.State.Seed,cell.X,cell.Y,cell.Z,103+i*2)%(uint)(drop.Max-drop.Min+1));items.Add(drop.Id,count,world.Catalog);
        }
        if(starter){items.Add("grain",2,world.Catalog);items.Add("carrot",2,world.Catalog);items.Add("potato",2,world.Catalog);items.Add("map",1,world.Catalog);}return(theme,items);
    }
    public static BlockEntity Open(WorldState world,Cell cell)
    {
        if(world.Catalog.Id(world.Get(cell))!="chest")throw new InvalidOperationException("这里没有箱子");
        if(world.State.Entities.TryGetValue(cell.ToString(),out var existing))return existing;
        var generated=Generate(world,cell);var chest=new BlockEntity{Kind="chest",Storage=generated.Items,LootTheme=generated.Theme};world.State.Entities[cell.ToString()]=chest;world.State.Discovered.Add("ruins");return chest;
    }
}
