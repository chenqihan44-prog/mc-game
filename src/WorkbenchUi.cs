using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using Godot;
using static EmberLighthouse.UI.UiFactory;
namespace EmberLighthouse;

public partial class Game
{
    private Inventory workbenchCrafting=new(9);
    private string selectedWorkbenchRecipe="";
    private int recipePage;
    private bool craftableOnly;
    private static void ApplyInventoryTheme(Control panel)
    {
        var theme=UiFactory.Theme();theme.SetColor("font_color","Label",new("373737"));theme.SetColor("font_shadow_color","Label",Colors.Transparent);theme.SetConstant("shadow_offset_x","Label",0);theme.SetConstant("shadow_offset_y","Label",0);panel.Theme=theme;
    }
    private Recipe? WorkbenchRecipe()=>catalog.Recipes.Values.Where(r=>r.Station is "hand" or "workbench"&&WorkbenchGrid.Matches(workbenchCrafting,r)).OrderByDescending(r=>r.Inputs.Values.Sum()).FirstOrDefault();
    private bool WorkbenchAvailable()=>world!=null&&player!=null&&currentContainer is { } p&&catalog.Id(world.Get(p))=="workbench"&&world.State.Entities.TryGetValue(p.ToString(),out var entity)&&entity.Kind=="workbench"&&player.GlobalPosition.DistanceSquaredTo(new(p.X+.5f,p.Y+.5f,p.Z+.5f))<=36;
    private void ShowWorkbench()
    {
        if(world==null)return;SetScreen("workbench");var col=Screen(overlay,"工作台");((ColorRect)overlay.GetChild(0)).Color=new(0,0,0,.55f);
        var panel=(PanelContainer)col.GetParent();panel.CustomMinimumSize=Vector2.Zero;panel.AddThemeStyleboxOverride("panel",MinecraftPanel());ApplyInventoryTheme(panel);col.AddThemeConstantOverride("separation",8);
        float scale=Math.Min(3*settings.UiScale,Math.Max(1.3f,Math.Min((overlay.GetViewportRect().Size.Y-180)/176,(overlay.GetViewportRect().Size.X-80)/328)));
        var layout=new Control{CustomMinimumSize=new(328*scale,176*scale),MouseFilter=Control.MouseFilterEnum.Ignore};col.AddChild(layout);
        void Text(string value,Vector2 position,int font=6){var label=Label(value,(int)(font*scale));label.Position=position*scale;layout.AddChild(label);}
        void ActionButton(string label,Vector2 position,Vector2 size,Action action){var b=Button(label,action);b.Position=position*scale;b.Size=size*scale;b.CustomMinimumSize=Vector2.Zero;b.AddThemeFontSizeOverride("font_size",(int)(6*scale));b.AddThemeStyleboxOverride("normal",Style("686868",2));b.AddThemeStyleboxOverride("hover",Style("818181",2));b.AddThemeStyleboxOverride("pressed",Style("565656",2));layout.AddChild(b);}
        Text("配方册",new(3,0),7);
        var search=new LineEdit{Text=recipeQuery,PlaceholderText="搜索物品 / 材料",Position=new(2*scale,12*scale),Size=new(142*scale,14*scale)};search.AddThemeFontSizeOverride("font_size",(int)(6*scale));layout.AddChild(search);search.TextSubmitted+=text=>{recipeQuery=text;recipePage=0;ShowWorkbench();};
        ActionButton(craftableOnly?"显示全部":"仅可合成",new(2,29),new(68,13),()=>{craftableOnly=!craftableOnly;recipePage=0;ShowWorkbench();});
        bool CanMake(Recipe r)=>r.Inputs.All(p=>world.State.Inventory.Count(p.Key)+workbenchCrafting.Count(p.Key)>=p.Value);
        var recipes=catalog.Recipes.Values.Where(r=>r.Station is "hand" or "workbench"&&r.Seconds==0&&(recipeQuery==""||r.Name.Contains(recipeQuery)||r.Inputs.Keys.Any(id=>catalog.Items[id].Name.Contains(recipeQuery)))&&(!craftableOnly||CanMake(r))).OrderByDescending(r=>world.State.FavoriteRecipes.Contains(r.Id)).ThenBy(r=>r.Name).ToArray();
        const int perPage=25;int pages=Math.Max(1,(recipes.Length+perPage-1)/perPage);recipePage=Math.Clamp(recipePage,0,pages-1);
        foreach(var pair in recipes.Skip(recipePage*perPage).Take(perPage).Select((r,i)=>(Recipe:r,Index:i))){
            var r=pair.Recipe;bool available=CanMake(r);var b=new Button{Name="Recipe_"+r.Id,Position=new((2+pair.Index%5*28)*scale,(46+pair.Index/5*23)*scale),Size=new(26*scale,21*scale),TooltipText=r.Name+" ×"+r.Count+"\n"+string.Join("\n",r.Inputs.Select(p=>catalog.Items[p.Key].Name+" "+(world.State.Inventory.Count(p.Key)+workbenchCrafting.Count(p.Key))+" / "+p.Value))};StyleSlot(b);layout.AddChild(b);
            b.AddChild(new ItemGlyph{Catalog=catalog,Item=new(r.Output,r.Count),Size=b.Size,PixelScale=1.15f*scale,Modulate=available?Colors.White:new(1,1,1,.45f)});
            if(selectedWorkbenchRecipe==r.Id)b.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new("a7b29b"),BorderColor=new("f4f4df"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2});
            b.Pressed+=()=>SelectWorkbenchRecipe(r);
        }
        ActionButton("‹",new(2,165),new(20,11),()=>{recipePage=Math.Max(0,recipePage-1);ShowWorkbench();});Text($"{recipePage+1} / {pages}",new(47,165));ActionButton("›",new(120,165),new(20,11),()=>{recipePage=Math.Min(pages-1,recipePage+1);ShowWorkbench();});
        const float start=152;Text("合成",new(start+28,3),7);
        DrawSlotsAt(layout,workbenchCrafting,4,0,9,3,new(start+28,18),scale);
        var recipe=WorkbenchRecipe();
        if(recipe==null&&catalog.Recipes.TryGetValue(selectedWorkbenchRecipe,out var preview)){
            var pattern=WorkbenchGrid.Layout(preview);for(int i=0;i<9;i++)if(workbenchCrafting.Slots[i]==null&&pattern[i] is { } ingredient)layout.AddChild(new ItemGlyph{Catalog=catalog,Item=new(ingredient.Id,ingredient.Count),Position=new((start+28+i%3*18)*scale,(18+i/3*18)*scale),Size=new(18*scale,18*scale),PixelScale=.9f*scale,Modulate=new(1,1,1,.3f)});
        }
        Text("→",new(start+87,34),10);
        var output=new Button{Name="WorkbenchOutput",Position=new((start+116)*scale,36*scale),Size=new(24*scale,24*scale),TooltipText=recipe==null?"按配方摆放材料，或点击配方册自动填入":catalog.Items[recipe.Output].Name+" · 点击取出 · Shift 批量合成",Disabled=recipe==null};StyleSlot(output);layout.AddChild(output);
        if(recipe!=null){output.AddChild(new ItemGlyph{Catalog=catalog,Item=new(recipe.Output,recipe.Count),Size=output.Size,PixelScale=1.2f*scale});output.Pressed+=()=>CraftWorkbenchOutput(recipe);}
        Text(recipe?.Name??(catalog.Recipes.TryGetValue(selectedWorkbenchRecipe,out var selectedRecipe)?selectedRecipe.Name:"选择左侧配方"),new(start+8,74),5);
        Text("物品栏",new(start+8,83));DrawSlotsAt(layout,world.State.Inventory,0,9,27,9,new(start+8,94),scale);DrawSlotsAt(layout,world.State.Inventory,0,0,9,9,new(start+8,152),scale);
        var buttons=Row(col);buttons.AddChild(Button("收回合成材料",()=>{ReturnWorkbenchItems();ShowWorkbench();}));buttons.AddChild(Button("返回游戏",()=>{SetScreen("play");currentContainer=null;}));
        col.AddChild(Label("点击配方填入材料 · 左右键取放 · 拖动移动 · Shift 点击结果批量合成",13));
        cursorGlyph=null;if(world.State.Cursor!=null){cursorGlyph=new(){Catalog=catalog,Item=world.State.Cursor,Size=new(54,54),PixelScale=2.7f,Position=GetViewport().GetMousePosition()-Vector2.One*24,ZIndex=20};overlay.AddChild(cursorGlyph);}
    }
    private void SelectWorkbenchRecipe(Recipe recipe)
    {
        selectedWorkbenchRecipe=recipe.Id;if(!WorkbenchAvailable()){Toast("工作台已不可用或距离过远");return;}
        if(!WorkbenchGrid.Arrange(world!.State.Inventory,workbenchCrafting,recipe,catalog,out string error))Toast(error);ShowWorkbench();
    }
    private void CraftWorkbenchOutput(Recipe recipe,bool? batch=null)
    {
        if(!WorkbenchAvailable()||!ReferenceEquals(catalog.Recipes.GetValueOrDefault(recipe.Id),recipe)||!WorkbenchGrid.Matches(workbenchCrafting,recipe)){Toast("工作台或合成材料已变化");return;}
        bool shortcut=batch??Input.IsActionPressed("inventory_transfer");var target=shortcut?world!.State.Inventory:new Inventory(1){Slots=[world!.State.Cursor?.Copy()]};
        if(!WorkbenchGrid.CraftTo(workbenchCrafting,target,recipe,catalog,out string error)){Toast(error);return;}
        if(shortcut)for(int i=0;i<255&&WorkbenchGrid.Matches(workbenchCrafting,recipe);i++)if(!WorkbenchGrid.CraftTo(workbenchCrafting,target,recipe,catalog,out _))break;
        if(!shortcut)world.State.Cursor=target.Slots[0];world.State.Discovered.Add(recipe.Output);audio.Play("craft");ShowWorkbench();
    }
    private void ReturnWorkbenchItems()
    {
        if(world==null){workbenchCrafting=new(9);return;}var remaining=new Inventory(9);
        for(int i=0;i<9;i++)if(workbenchCrafting.Slots[i] is { } item){int count=world.State.Inventory.Add(item,catalog);if(count>0)remaining.Slots[i]=new(item.Id,count,item.Durability);}
        workbenchCrafting=new(9);if(remaining.Slots.Any(s=>s!=null))world.State.Graves.Add(new(){Position=(double[])world.State.Position.Clone(),Items=remaining});
    }
}
