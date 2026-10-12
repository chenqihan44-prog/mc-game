using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmberLighthouse.Items;

public sealed record ItemDef(string Id,string Name,string Color,int Stack=64,bool Block=false,bool Solid=true,double Hardness=1,int Tier=0,string Shape="cube",string Use="",int Power=0,int ArmorSlot=-1,int ArmorPoints=0,int Durability=0,string Description="");
public sealed record Recipe(string Id,string Name,Dictionary<string,int> Inputs,string Output,int Count=1,string Station="hand",double Seconds=0);

public sealed class Catalog
{
    public Dictionary<string,ItemDef> Items { get; }
    public Dictionary<string,Recipe> Recipes { get; }
    public string[] BlockIds { get; }
    private readonly Dictionary<string,ushort> numbers;
    public Catalog(IEnumerable<ItemDef> items,IEnumerable<Recipe> recipes)
    {
        Items=items.ToDictionary(i=>i.Id); Recipes=recipes.ToDictionary(r=>r.Id);
        BlockIds=["air",..Items.Values.Where(i=>i.Block).Select(i=>i.Id).Order(StringComparer.Ordinal)];
        numbers=BlockIds.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>(ushort)p.i);
        Validate();
    }
    public static Catalog Load(string directory) => new(
        JsonSerializer.Deserialize<ItemDef[]>(File.ReadAllText(Path.Combine(directory,"items.json")),JsonOptions)!,
        JsonSerializer.Deserialize<Recipe[]>(File.ReadAllText(Path.Combine(directory,"recipes.json")),JsonOptions)!);
    public static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public ushort Number(string id) => numbers[id];
    public string Id(ushort n) => BlockIds[n];
    public ItemDef? Block(ushort n) => n==0?null:Items[Id(n)];
    public void Validate()
    {
        foreach(var i in Items.Values) if(i.Id=="air"||i.Stack<1||i.Hardness<0||i.Tier<0||i.Tier>3||i.ArmorSlot< -1||i.ArmorSlot>3||i.ArmorPoints<0||i.ArmorPoints>20||i.Durability<0||i.ArmorSlot>=0&&(i.Block||i.Stack!=1||i.ArmorPoints==0&&i.Id!="elytra"||i.Durability==0||i.Use!="armor")) throw new InvalidDataException($"Invalid item {i.Id}");
        foreach(var r in Recipes.Values) {
            if(!Items.ContainsKey(r.Output)||r.Count<1||r.Inputs.Count==0||r.Seconds<0) throw new InvalidDataException($"Invalid recipe {r.Id}");
            foreach(var p in r.Inputs) if(!Items.ContainsKey(p.Key)||p.Value<1) throw new InvalidDataException($"Invalid ingredient {r.Id}: {p.Key}");
        }
    }
}
