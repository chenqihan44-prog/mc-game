using System;
using System.Collections.Generic;
using System.Linq;

namespace EmberLighthouse.Items;

public sealed class Stack
{
    public string Id {get;set;}="";
    public int Count {get;set;}
    public int? Durability {get;set;}
    public bool HeatLining {get;set;}
    public string Dye {get;set;}="";
    public int Protection {get;set;}
    public int Unbreaking {get;set;}
    public Stack(){}
    public Stack(string id,int count,int? durability=null){Id=id;Count=count;Durability=durability;}
    public Stack Copy()=>new(Id,Count,Durability){HeatLining=HeatLining,Dye=Dye,Protection=Protection,Unbreaking=Unbreaking};
    public Stack WithCount(int count){var copy=Copy();copy.Count=count;return copy;}
}

public sealed class Inventory
{
    public Stack?[] Slots {get;set;}=new Stack?[36];
    public Inventory(){}
    public Inventory(int size){Slots=new Stack?[size];}
    public Inventory Copy()=>new(){Slots=Slots.Select(s=>s?.Copy()).ToArray()};
    public int Count(string id)=>Slots.Where(s=>s?.Id==id).Sum(s=>s!.Count);
    // Returns the remaining amount; never silently discards overflow.
    public int Add(string id,int count,Catalog catalog)
    {
        return Add(new Stack(id,count),catalog);
    }
    public int Add(Stack item,Catalog catalog)
    {
        int count=item.Count;if(count<0)throw new ArgumentOutOfRangeException(nameof(item));int max=catalog.Items[item.Id].Stack;
        for(int i=0;i<Slots.Length&&count>0;i++)if(SameItem(Slots[i],item)){int n=Math.Min(count,max-Slots[i]!.Count);Slots[i]!.Count+=n;count-=n;}
        for(int i=0;i<Slots.Length&&count>0;i++)if(Slots[i]==null){int n=Math.Min(count,max);Slots[i]=item.WithCount(n);count-=n;}
        return count;
    }
    private static bool SameItem(Stack? a,Stack b)=>a?.Id==b.Id&&a.Durability==b.Durability&&a.HeatLining==b.HeatLining&&a.Dye==b.Dye&&a.Protection==b.Protection&&a.Unbreaking==b.Unbreaking;
    public bool Remove(string id,int count)
    {
        if(count<0||Count(id)<count)return false;
        for(int i=0;i<Slots.Length&&count>0;i++)if(Slots[i]?.Id==id){int n=Math.Min(count,Slots[i]!.Count);Slots[i]!.Count-=n;count-=n;if(Slots[i]!.Count==0)Slots[i]=null;}
        return true;
    }
    public static bool Move(Inventory source,int from,Inventory target,int to,int count,Catalog catalog,Func<int,Stack?,bool>? sourceAccepts=null,Func<int,Stack?,bool>? targetAccepts=null)
    {
        if(from<0||from>=source.Slots.Length||to<0||to>=target.Slots.Length||count<1)return false;
        if(ReferenceEquals(source,target)&&from==to)return false;
        var a=source.Slots[from];var b=target.Slots[to];
        if(a==null||targetAccepts?.Invoke(to,a)==false)return false;
        if(b!=null&&!SameItem(b,a)){if(count<a.Count||sourceAccepts?.Invoke(from,b)==false)return false;source.Slots[from]=b;target.Slots[to]=a;return true;}
        int n=Math.Min(Math.Min(count,a.Count),catalog.Items[a.Id].Stack-(b?.Count??0));
        if(n==0)return false;
        target.Slots[to]=a.WithCount((b?.Count??0)+n);a.Count-=n;if(a.Count==0)source.Slots[from]=null;return true;
    }
    public static bool Click(Inventory inventory,int index,Stack? cursor,bool right,Catalog catalog,out Stack? nextCursor,Func<int,Stack?,bool>? accepts=null)
    {
        nextCursor=cursor;if(index<0||index>=inventory.Slots.Length)return false;var slot=inventory.Slots[index];
        if(cursor==null){if(slot==null)return false;int amount=right?(slot.Count+1)/2:slot.Count;nextCursor=slot.WithCount(amount);slot.Count-=amount;if(slot.Count==0)inventory.Slots[index]=null;return true;}
        if(accepts?.Invoke(index,cursor)==false)return false;
        if(slot!=null&&!SameItem(slot,cursor)){if(right)return false;inventory.Slots[index]=cursor.Copy();nextCursor=slot;return true;}
        int move=Math.Min(right?1:cursor.Count,catalog.Items[cursor.Id].Stack-(slot?.Count??0));if(move<=0)return false;
        inventory.Slots[index]=cursor.WithCount((slot?.Count??0)+move);nextCursor=cursor.Count==move?null:cursor.WithCount(cursor.Count-move);return true;
    }
    public bool Craft(Recipe recipe,Catalog catalog,out string error)
    {
        var next=Copy();
        Stack product=new(recipe.Output,recipe.Count);if(recipe.Output.StartsWith("ember_")&&catalog.Items[recipe.Output].Use is "armor" or "animal_armor"){var source=Slots.FirstOrDefault(i=>i!=null&&recipe.Inputs.ContainsKey(i.Id)&&catalog.Items[i.Id].Use==catalog.Items[recipe.Output].Use);if(source!=null){product=source.WithCount(recipe.Count);product.Id=recipe.Output;product.Durability=catalog.Items[recipe.Output].Durability;product.HeatLining=false;}}
        foreach(var pair in recipe.Inputs)if(!next.Remove(pair.Key,pair.Value)){error="材料不足";return false;}
        if(next.Add(product,catalog)>0){error="背包空间不足";return false;}
        Slots=next.Slots;error="";return true;
    }
    public static bool CraftTo(Inventory source,Inventory target,Recipe recipe,Catalog catalog,out string error)
    {
        if(ReferenceEquals(source,target))return source.Craft(recipe,catalog,out error);
        var input=source.Copy();var output=target.Copy();
        foreach(var p in recipe.Inputs)if(!input.Remove(p.Key,p.Value)){error="材料不足";return false;}
        if(output.Add(recipe.Output,recipe.Count,catalog)>0){error="背包空间不足";return false;}
        source.Slots=input.Slots;target.Slots=output.Slots;error="";return true;
    }
    public void Sort(Catalog catalog)
    {
        var items=Slots.Where(s=>s!=null).GroupBy(s=>(s!.Id,s.Durability,s.HeatLining,s.Dye,s.Protection,s.Unbreaking)).OrderBy(g=>g.Key.Id).ThenBy(g=>g.Key.Durability).Select(g=>g.First()!.WithCount(g.Sum(s=>s!.Count))).ToArray();
        var next=new Inventory(Slots.Length);
        foreach(var item in items)if(next.Add(item,catalog)>0)throw new InvalidOperationException("Inventory invariant failed");
        Slots=next.Slots;
    }
    public void Validate(Catalog catalog)
    {
        if(Slots==null||Slots.Length<1||Slots.Length>100)throw new InvalidOperationException("Invalid inventory size");
        foreach(var s in Slots)if(s!=null&&(!catalog.Items.TryGetValue(s.Id,out var d)||s.Count<1||s.Count>d.Stack||s.HeatLining&&s.Id!="iron_chestplate"||s.Dye is not("" or "red" or "blue")||s.Dye!=""&&!s.Id.StartsWith("leather_")&&s.Id!="wolf_armor"||s.Protection<0||s.Protection>3||s.Unbreaking<0||s.Unbreaking>3||(s.Protection+s.Unbreaking>0&&d.Use!="armor")||s.Durability is { } durability&&(durability<1||durability>d.Durability)))throw new InvalidOperationException("Invalid inventory stack");
    }
}
