using System;
using System.Collections.Generic;
using System.Linq;
namespace EmberLighthouse.Items;

public sealed record GridIngredient(string Id,int Count=1);
public static class WorkbenchGrid
{
    public static GridIngredient?[] Layout(Recipe recipe)
    {
        var cells=new GridIngredient?[9];
        void Fill(string id,params int[] indexes){int left=recipe.Inputs.GetValueOrDefault(id);foreach(int i in indexes){if(left<=0)break;cells[i]=new(id,1);left--;}if(left>0&&indexes.Length>0){int first=indexes[0];cells[first]=new(id,(cells[first]?.Count??0)+left);}}
        var keys=recipe.Inputs.Keys.ToArray();string material=keys.FirstOrDefault(k=>k!="stick"&&k!="coal"&&k!="wood")??keys[0];
        if(recipe.Id.EndsWith("_pick")){Fill(material,0,1,2);Fill("stick",4,7);}
        else if(recipe.Id.EndsWith("_axe")){Fill(material,0,1,3);Fill("stick",4,7);}
        else if(recipe.Id.EndsWith("_sword")){Fill(material,1,4);Fill("stick",7);}
        else if(recipe.Id.EndsWith("_shovel")){Fill(material,1);Fill("stick",4,7);}
        else switch(recipe.Id){
            case "workbench":Fill("plank",0,1,3,4);break;
            case "furnace":Fill("stone",0,1,2,3,5,6,7,8);break;
            case "chest":Fill("plank",0,1,2,3,5,6,7,8);break;
            case "bed":Fill("cloth",0,1,2);Fill("plank",3,4,5);break;
            case "door":Fill("plank",0,1,3,4,6,7);Fill("stick",2,8);break;
            case "stairs":Fill("plank",0,3,4);break;
            case "roof":Fill("stone",0,3,4);break;
            case "slab":Fill("plank",3,4,5);break;
            case "fence":Fill("plank",3,5);Fill("stick",4,7);break;
            case "bow":Fill("stick",1,3,7);Fill("rope",2,8);break;
            case "shield":Fill("plank",0,2,3,4,5);Fill("iron_ingot",1);break;
            case "iron_helmet":Fill("iron_ingot",0,1,2,3,5);break;
            case "iron_chestplate":Fill("iron_ingot",0,2,3,4,5,6,7,8);break;
            case "iron_leggings":Fill("iron_ingot",0,1,2,3,5,6,8);break;
            case "iron_boots":Fill("iron_ingot",3,5,6,8);break;
            case "campfire":Fill("wood",6,7,8);Fill("stone",3,4,5);break;
            default:
                if(keys.Length==1){int n=Math.Min(9,recipe.Inputs[keys[0]]);int[] positions=n switch{1=>[4],2=>[1,4],3=>[3,4,5],4=>[0,1,3,4],_=>Enumerable.Range(0,n).ToArray()};Fill(keys[0],positions);}
                else{int slot=0;foreach(var key in keys){Fill(key,slot);slot++;}}
                break;
        }
        return cells;
    }
    public static bool Matches(Inventory grid,Recipe recipe)=>MatchedLayout(grid,recipe)!=null;
    private static GridIngredient?[]? MatchedLayout(Inventory grid,Recipe recipe)
    {
        if(grid.Slots.Length!=9||recipe.Seconds>0)return null;var pattern=Layout(recipe);var occupied=Enumerable.Range(0,9).Where(i=>pattern[i]!=null).ToArray();if(occupied.Length==0)return null;
        int left=occupied.Min(i=>i%3),right=occupied.Max(i=>i%3),top=occupied.Min(i=>i/3),bottom=occupied.Max(i=>i/3),width=right-left+1,height=bottom-top+1;
        for(int y=0;y<=3-height;y++)for(int x=0;x<=3-width;x++)foreach(bool mirror in new[]{false,true}){
            var aligned=new GridIngredient?[9];bool match=true;for(int i=0;i<9;i++){int gx=i%3-x,gy=i/3-y;GridIngredient? need=gx>=0&&gx<width&&gy>=0&&gy<height?pattern[(gy+top)*3+left+(mirror?width-gx-1:gx)]:null;aligned[i]=need;var item=grid.Slots[i];if(need==null?item!=null:item?.Id!=need.Id||item.Count<need.Count){match=false;break;}}if(match)return aligned;
        }
        return null;
    }
    public static bool CraftTo(Inventory grid,Inventory target,Recipe recipe,Catalog catalog,out string error)
    {
        var pattern=MatchedLayout(grid,recipe);if(pattern==null||ReferenceEquals(grid,target)){error="材料摆放不符合配方";return false;}
        var output=target.Copy();if(output.Add(recipe.Output,recipe.Count,catalog)>0){error="物品栏或光标空间不足";return false;}
        var input=grid.Copy();for(int i=0;i<9;i++)if(pattern[i] is { } need){input.Slots[i]!.Count-=need.Count;if(input.Slots[i]!.Count==0)input.Slots[i]=null;}
        grid.Slots=input.Slots;target.Slots=output.Slots;error="";return true;
    }
    public static bool Arrange(Inventory inventory,Inventory grid,Recipe recipe,Catalog catalog,out string error)
    {
        if(inventory.Slots.Length!=36||grid.Slots.Length!=9||recipe.Seconds>0||recipe.Station is not("hand" or "workbench")){error="此配方不能在工作台摆放";return false;}
        var source=new Inventory(45);for(int i=0;i<36;i++)source.Slots[i]=inventory.Slots[i]?.Copy();for(int i=0;i<9;i++)source.Slots[i+36]=grid.Slots[i]?.Copy();
        foreach(var input in recipe.Inputs)if(source.Count(input.Key)<input.Value){error="缺少 "+catalog.Items[input.Key].Name;return false;}
        foreach(var input in recipe.Inputs)source.Remove(input.Key,input.Value);
        var remaining=new Inventory();for(int i=0;i<36;i++)remaining.Slots[i]=source.Slots[i];foreach(var item in source.Slots.Skip(36))if(item!=null&&remaining.Add(item,catalog)>0){error="背包空间不足，请先腾出空格";return false;}
        var pattern=Layout(recipe);inventory.Slots=remaining.Slots;grid.Slots=pattern.Select(p=>p==null?null:new Stack(p.Id,p.Count)).ToArray();error="";return true;
    }
}
