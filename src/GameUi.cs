using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.Player;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;

namespace EmberLighthouse;

public partial class Game
{
    private void ShowBackups()=>RunWorldIo("读取备份列表",()=>saves.ListBackups().ToArray(),DrawBackups,ShowWorlds);
    private void DrawBackups(BackupEntry[] backups)
    {
        SetScreen("backups");var col=Screen(overlay,"备份管理");col.AddChild(Label("恢复会创建一个世界副本；备份也可以永久删除。",16));var scroll=Scroll(col,340);scroll.HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled;var entries=Column(scroll);entries.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        foreach(var backup in backups){
            var card=Column(entries);var name=Label(backup.Name+" · "+backup.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")+(backup.Damaged?" · 校验失败":""),14);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;card.AddChild(name);var row=Row(card);
            var restore=Button("恢复为新世界",()=>RunWorldIo("恢复备份",()=>saves.RestoreBackup(backup.Key),_=>{Toast("备份已恢复为新世界；原世界保留");ShowWorlds();},ShowBackups));restore.Disabled=backup.Damaged;row.AddChild(restore);row.AddChild(Button("永久删除备份",()=>ShowDeleteBackup(backup)));
        }
        if(backups.Length==0)entries.AddChild(Label("尚无备份。可在暂停菜单或世界管理中创建。"));col.AddChild(Button("返回",ShowWorlds));
    }
    private void ShowPause()
    {
        SetScreen("pause");var col=Screen(overlay,"营地休息");col.AddChild(Label(InRoom?"联机世界继续运行；作物、制作与敌人不会暂停。":"单人世界已暂停；作物、制作与敌人停止计时。",16));
        if(NetClient){col.AddChild(Button("继续探索",()=>SetScreen("play")));col.AddChild(Button("设置",()=>ShowSettings("pause")));col.AddChild(Button("离开联机世界",ReturnMenu));return;}
        var resume=Button("继续探索",()=>SetScreen("play"));UiFactory.DecorateAction(resume,"compass",true);col.AddChild(resume);var grid=new GridContainer{Columns=2};grid.AddThemeConstantOverride("h_separation",12);grid.AddThemeConstantOverride("v_separation",10);col.AddChild(grid);
        void Add(Button b,string icon){b.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;UiFactory.DecorateAction(b,icon);grid.AddChild(b);}
        Add(Button(NetHost?"联机房间":"开放联机",ShowHostRoom),"connection");Button? saveButton=null;saveButton=Button("保存世界",()=>{saveButton!.Disabled=true;RequestSave(success=>{if(GodotObject.IsInstanceValid(saveButton))saveButton.Disabled=false;if(success)Toast("保存完成");});});Add(saveButton,"save");
        Add(Button("备份世界",BackupCurrentWorld),"backup");Add(Button("主线日志（J）",ShowMainlineJournal),"book");Add(Button("物品与生物图鉴",ShowContentGuide),"cube");Add(Button("世界规则",ShowWorldRules),"settings");Add(Button("设置",()=>ShowSettings("pause")),"settings");Add(Button("帮助与引导",()=>ShowHelp("pause")),"book");var leave=Button("保存并返回主菜单",ReturnMenu);UiFactory.DecorateAction(leave,"back");col.AddChild(leave);
    }
    private void ShowWorldRules()
    {
        SetScreen("rules");var col=Screen(overlay,"世界规则");col.AddChild(Label("创造模式可飞行并获取无限材料；切换不删除建筑或物品。",16));
        var creative=new CheckButton{Text="创造模式",ButtonPressed=world!.State.Creative};var casual=new CheckButton{Text="休闲难度 / 死亡保留物品",ButtonPressed=!world.State.Creative&&world.State.Casual};ExclusiveModes(creative,casual);col.AddChild(creative);col.AddChild(casual);
        col.AddChild(Button("应用并保存",()=>{world.State.Creative=creative.ButtonPressed;world.State.Casual=casual.ButtonPressed;if(!creative.ButtonPressed)player!.Flying=false;RequestSave();ShowPause();}));col.AddChild(Button("返回",ShowPause));
    }
    private Inventory? ContainerInventory()=>currentContainer.HasValue&&world!.State.Entities.TryGetValue(currentContainer.Value.ToString(),out var e)?e.Storage:null;
    private void ShowInventory()
    {
        if(world==null)return;
        SetScreen("inventory");var col=Screen(overlay,currentContainer.HasValue?"容器":"物品栏");
        ((ColorRect)overlay.GetChild(0)).Color=new(0,0,0,.22f);
        var panel=(PanelContainer)col.GetParent();panel.CustomMinimumSize=new(528,0);panel.AddThemeStyleboxOverride("panel",MinecraftPanel());ApplyInventoryTheme(panel);
        CompactInventoryHeading(col);
        col.AddThemeConstantOverride("separation",12);col.GetChild<Label>(0).AddThemeColorOverride("font_color",Gold);
        if(ContainerInventory() is { } container){DrawSlots(col,container,1,0,container.Slots.Length);}
        else{
            float scale=InventoryLayoutScale(176,176);
            var layout=new Control{CustomMinimumSize=new(176*scale,176*scale),MouseFilter=Control.MouseFilterEnum.Ignore};col.AddChild(layout);
            var preview=new AvatarPreview{Equipment=world!.State.Equipment,Catalog=catalog,AppearanceId=RemoteContext?executingGuest!.State.AppearanceId:networkAppearance,Position=new(26*scale,8*scale),Size=new(52*scale,70*scale)};layout.AddChild(preview);
            DrawSlotsAt(layout,world.State.Equipment,3,0,4,1,new(8,8),scale);DrawSlotsAt(layout,world.State.Equipment,3,4,1,1,new(77,62),scale);
            DrawSlotsAt(layout,world.State.Crafting,2,0,4,2,new(98,16),scale);
            var craftTitle=Label("合成",(int)(6*scale));craftTitle.Position=new(98*scale,6*scale);craftTitle.AddThemeColorOverride("font_color",Gold);layout.AddChild(craftTitle);
            var arrow=Label("→",(int)(10*scale));arrow.Position=new(137*scale,24*scale);arrow.AddThemeColorOverride("font_color",Gold);layout.AddChild(arrow);
            var recipe=GridRecipe();var output=new Button{Position=new(154*scale,26*scale),Size=new(18*scale,18*scale),TooltipText=recipe==null?"将材料放入 2×2 合成格":catalog.Items[recipe.Output].Name};StyleSlot(output);layout.AddChild(output);
            if(recipe!=null){output.AddChild(new ItemGlyph{Catalog=catalog,Item=new(recipe.Output,recipe.Count),Size=output.Size,PixelScale=.9f*scale});output.Pressed+=()=>CraftGridOutput(recipe);}else output.Disabled=true;
            var inventoryTitle=Label("物品栏",(int)(6*scale));inventoryTitle.Position=new(8*scale,83*scale);inventoryTitle.AddThemeColorOverride("font_color",Gold);layout.AddChild(inventoryTitle);
            DrawSlotsAt(layout,world.State.Inventory,0,9,27,9,new(8,94),scale);DrawSlotsAt(layout,world.State.Inventory,0,0,9,9,new(8,152),scale);
        }
        if(ContainerInventory()!=null){var inventoryLabel=Label("物品栏",17);inventoryLabel.AddThemeColorOverride("font_color",Gold);col.AddChild(inventoryLabel);DrawSlots(col,world!.State.Inventory,0,9,27);DrawSlots(col,world.State.Inventory,0,0,9);}
        var rowButtons=Row(col);rowButtons.AddChild(Button("整理",()=>{world.State.Inventory.Sort(catalog);ShowInventory();}));rowButtons.AddChild(Button("配方",()=>{currentStation="hand";ShowRecipes();}));if(world.State.Creative)rowButtons.AddChild(Button("创造目录",ShowCreative));rowButtons.AddChild(Button("关闭",()=>{currentContainer=null;SetScreen("play");}));
        var hint=Label("左键取放 · 右键取半 / 放一个 · Shift 快捷移动",13);hint.AddThemeColorOverride("font_color",Muted);hint.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(hint);
        DrawCursorItem();
    }
    private static void CompactInventoryHeading(Control column)
    {
        if(column.GetChildCount()>1&&column.GetChild(1) is UiOrnament ornament)ornament.Hide();
    }
    private float InventoryLayoutScale(float width,float height)
    {
        var viewport=overlay.GetViewportRect().Size;
        float fit=Math.Min((viewport.X-112)/width,(viewport.Y-190-70*UiFactory.FontScale)/height);
        return Math.Max(1.1f,Math.Min(3*settings.UiScale,fit));
    }
    private static StyleBoxFlat MinecraftPanel()=>new(){BgColor=new("21190eed"),BorderColor=Gold.Darkened(.22f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=12,ContentMarginBottom=12,ShadowColor=new(0,0,0,.42f),ShadowSize=12};
    private static void StyleSlot(Button button)
    {
        StyleBoxFlat Slot(string background,Color border)=>new(){BgColor=new(background),BorderColor=border,BorderWidthTop=1,BorderWidthLeft=1,BorderWidthBottom=1,BorderWidthRight=1,CornerRadiusTopLeft=2,CornerRadiusTopRight=2,CornerRadiusBottomLeft=2,CornerRadiusBottomRight=2};
        button.AddThemeStyleboxOverride("normal",Slot("21190f",new("79603b")));
        button.AddThemeStyleboxOverride("hover",Slot("493522",Gold));
        button.AddThemeStyleboxOverride("pressed",Slot("63472b",Parchment));button.AddThemeStyleboxOverride("disabled",Slot("18140f",new("56452e")));
        button.AddThemeColorOverride("font_color",Parchment);button.AddThemeColorOverride("font_hover_color",Gold);
        button.FocusMode=Control.FocusModeEnum.None;
    }
    private void DrawSlots(Control parent,Inventory inventory,int surface,int start,int count,int columns=9)
    {
        int rows=(int)Math.Ceiling((ContainerInventory()?.Slots.Length??27)/(double)columns)+4;
        float slotSize=Math.Clamp((overlay.GetViewportRect().Size.Y-302-105*UiFactory.FontScale)/rows,28,54);
        var grid=new GridContainer{Columns=columns};grid.AddThemeConstantOverride("h_separation",0);grid.AddThemeConstantOverride("v_separation",0);parent.AddChild(grid);
        for(int i=start;i<start+count;i++){
            int index=i;var stack=inventory.Slots[i];var b=new SlotButton{SlotIndex=i,SurfaceIndex=surface,Transfer=TransferSlots,Catalog=catalog,Item=stack,DragAllowed=()=>world?.State.Cursor==null,CustomMinimumSize=new(slotSize,slotSize),TooltipText=stack==null?"":catalog.Items[stack.Id].Name+(catalog.Items[stack.Id].Durability>0?$" · 耐久 {stack.Durability??catalog.Items[stack.Id].Durability}/{catalog.Items[stack.Id].Durability}":"")};StyleSlot(b);grid.AddChild(b);
            if(stack!=null)b.AddChild(new ItemGlyph{Catalog=catalog,Item=stack,Size=new(slotSize,slotSize),PixelScale=slotSize/20});
            b.Pressed+=()=>SlotClick(surface,index,false);b.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.ButtonIndex==MouseButton.Right&&m.Pressed){SlotClick(surface,index,true);b.AcceptEvent();}};
        }
    }
    private void DrawSlotsAt(Control parent,Inventory inventory,int surface,int start,int count,int columns,Vector2 origin,float scale)
    {
        string[] emptyArmor={"iron_helmet","iron_chestplate","iron_leggings","iron_boots","shield"};
        for(int n=0;n<count;n++){
            int index=start+n;var item=inventory.Slots[index];var def=item==null?null:catalog.Items[item.Id];
            var b=new SlotButton{SlotIndex=index,SurfaceIndex=surface,Transfer=TransferSlots,Catalog=catalog,Item=item,DragAllowed=()=>world?.State.Cursor==null,Position=(origin+new Vector2(n%columns*18,n/columns*18))*scale,Size=new(18*scale,18*scale),TooltipText=def?.Name??(surface==3?index==4?"副手":"护甲":"")};StyleSlot(b);parent.AddChild(b);
            if(def?.Durability>0)b.TooltipText+=$" · 耐久 {item!.Durability??def.Durability}/{def.Durability}";
            if(item!=null)b.AddChild(new ItemGlyph{Catalog=catalog,Item=item,Size=b.Size,PixelScale=.9f*scale});
            else if(surface==3)b.AddChild(new ItemGlyph{Catalog=catalog,Item=new(emptyArmor[index],1),Size=b.Size,PixelScale=.9f*scale,Modulate=new(1,1,1,.25f)});
            b.Pressed+=()=>SlotClick(surface,index,false);b.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.ButtonIndex==MouseButton.Right&&m.Pressed){SlotClick(surface,index,true);b.AcceptEvent();}};
        }
    }
    private ItemGlyph? cursorGlyph;
    private Inventory? Surface(int surface)=>surface switch{0=>world?.State.Inventory,1=>ContainerInventory(),2=>world?.State.Crafting,3=>world?.State.Equipment,4=>workbenchCrafting,_=>null};
    private Func<int,Stack?,bool>? SlotRule(int surface)=>surface==3?(i,s)=>Equipment.Accepts(catalog,i,s):null;
    private Recipe? GridRecipe()
    {
        var input=world!.State.Crafting;return catalog.Recipes.Values.Where(r=>r.Station=="hand"&&r.Seconds==0&&r.Inputs.All(p=>input.Count(p.Key)>=p.Value)&&input.Slots.All(s=>s==null||r.Inputs.ContainsKey(s.Id))).OrderByDescending(r=>r.Inputs.Values.Sum()).FirstOrDefault();
    }
    private void ReturnCraftingItems()
    {
        ReturnWorkbenchItems();
        cursorGlyph=null;if(world==null)return;var remaining=new Inventory(5);
        if(world.State.Cursor is { } cursor){int count=world.State.Inventory.Add(cursor,catalog);if(count>0)remaining.Slots[4]=new(cursor.Id,count,cursor.Durability){HeatLining=cursor.HeatLining};world.State.Cursor=null;}
        for(int i=0;i<4;i++)if(world.State.Crafting.Slots[i] is { } slot){int count=world.State.Inventory.Add(slot,catalog);if(count>0)remaining.Slots[i]=new(slot.Id,count,slot.Durability){HeatLining=slot.HeatLining};}
        world.State.Crafting=new(4);if(remaining.Slots.Any(s=>s!=null))world.Drop(remaining,world.State.Position);
    }
    private void SlotClick(int surface,int index,bool half)
    {
        var inventory=Surface(surface);if(inventory==null||index<0||index>=inventory.Slots.Length)return;
        if(Input.IsActionPressed("inventory_transfer")){
            if(surface==0&&ContainerInventory()==null&&inventory.Slots[index] is { } wearable){var def=catalog.Items[wearable.Id];int equipSlot=def.ArmorSlot>=0?def.ArmorSlot:def.Use=="shield"?Equipment.Offhand:-1;if(equipSlot>=0&&world!.State.Equipment.Slots[equipSlot]==null){Inventory.Move(inventory,index,world.State.Equipment,equipSlot,64,catalog,targetAccepts:SlotRule(3));RefreshInventory();return;}}
            var target=surface!=0?world!.State.Inventory:screen=="workbench"?workbenchCrafting:ContainerInventory();bool internalMove=target==null&&surface==0;target??=world!.State.Inventory;
            var destinations=internalMove?(index<9?Enumerable.Range(9,27):Enumerable.Range(0,9)):Enumerable.Range(0,target.Slots.Length);
            var stack=inventory.Slots[index];if(stack==null)return;
            foreach(int i in destinations)if(inventory.Slots[index]!=null&&target.Slots[i]?.Id==stack.Id)Inventory.Move(inventory,index,target,i,64,catalog);
            foreach(int i in destinations)if(inventory.Slots[index]!=null&&target.Slots[i]==null)Inventory.Move(inventory,index,target,i,64,catalog);
            RefreshInventory();return;
        }
        if(Inventory.Click(inventory,index,world!.State.Cursor,half,catalog,out var nextCursor,SlotRule(surface))){world.State.Cursor=nextCursor;RefreshInventory();}
    }
    private void CraftGridOutput(Recipe recipe)
    {
        bool shortcut=Input.IsActionPressed("inventory_transfer");var target=shortcut?world!.State.Inventory:new Inventory(1){Slots=[world!.State.Cursor?.Copy()]};
        if(!Inventory.CraftTo(world.State.Crafting,target,recipe,catalog,out string error)){Toast(error);return;}
        if(shortcut)for(int i=0;i<255;i++)if(!Inventory.CraftTo(world.State.Crafting,target,recipe,catalog,out _))break;
        if(!shortcut)world.State.Cursor=target.Slots[0];world.State.Discovered.Add(recipe.Output);audio.Play("craft");ShowInventory();
    }
    private void TransferSlots(int sourceSurface,int sourceIndex,int targetSurface,int targetIndex)
    {
        var a=Surface(sourceSurface);var b=Surface(targetSurface);if(a==null||b==null)return;
        if(Inventory.Move(a,sourceIndex,b,targetIndex,64,catalog,SlotRule(sourceSurface),SlotRule(targetSurface))){if(RemoteContext)RefreshInventory();else{AnimateSlotArrival(targetSurface,targetIndex);CallDeferred(nameof(RefreshInventory));}}
    }
    private void RefreshInventory(){if(screen=="workbench")ShowWorkbench();else ShowInventory();}
    private string BoundKey(string action)=>OS.GetKeycodeString((Key)settings.Keys.GetValueOrDefault(action,(long)defaultKeys[action].Key));
    private void ShowCreative()
    {
        SetScreen("creative");var col=Screen(overlay,"创造物品目录");col.AddChild(Label($"点击物品加入背包；右键放置。{BoundKey("swap_hands")} 交换主副手，{BoundKey("fly")} 开关飞行。",15));var scroll=Scroll(col,400);var grid=new GridContainer{Columns=4};scroll.AddChild(grid);
        foreach(var d in catalog.Items.Values.Where(d=>d.Id!="beacon"&&d.Id!="bedrock"&&d.Id!="water").OrderBy(d=>d.Name)){
            var card=Button("",()=>{if(world!.State.Inventory.Add(d.Id,d.Stack,catalog)>0)Toast("背包空间不足");else Toast("已添加 "+d.Name);});card.CustomMinimumSize=new(145,84);grid.AddChild(card);
            card.AddChild(new ItemGlyph{Catalog=catalog,Item=new(d.Id,1),Position=new(44,1),Size=new(56,56),PixelScale=3});var name=Label(d.Name,15);name.Position=new(5,59);name.Size=new(135,20);name.HorizontalAlignment=HorizontalAlignment.Center;name.MouseFilter=Control.MouseFilterEnum.Ignore;card.AddChild(name);
        }col.AddChild(Button("返回背包",ShowInventory));
    }
    private void ShowRecipes()
    {
        if(currentStation=="workbench"){ShowWorkbench();return;}
        SetScreen("recipes");var col=Screen(overlay,currentStation=="hand"?"随身制作":"制作设施："+catalog.Items[currentStation].Name);
        var search=new LineEdit{Text=recipeQuery,PlaceholderText="输入名称或材料，回车检索"};col.AddChild(search);search.TextChanged+=text=>recipeQuery=text;search.TextSubmitted+=text=>{recipeQuery=text;ShowRecipes();};
        var scroll=Scroll(col,380);var list=Column(scroll);
        foreach(var r in catalog.Recipes.Values.Where(r=>Mainline.RecipeKnown(world!.State,r)&&(r.Station=="hand"||r.Station==currentStation)&&(recipeQuery==""||r.Name.Contains(recipeQuery)||r.Inputs.Keys.Any(id=>catalog.Items[id].Name.Contains(recipeQuery)))).OrderByDescending(r=>world!.State.FavoriteRecipes.Contains(r.Id)).ThenBy(r=>r.Name)){
            var card=new PanelContainer();list.AddChild(card);var content=Column(card);content.AddChild(Label(r.Name+" ×"+r.Count+(r.Seconds>0?$" · {r.Seconds:0}秒":""),16));var row=Row(content);
            foreach(var ingredient in r.Inputs){var stack=Column(row);var glyph=new ItemGlyph{Catalog=catalog,Item=new(ingredient.Key,ingredient.Value),CustomMinimumSize=new(48,48),TooltipText=catalog.Items[ingredient.Key].Name};stack.AddChild(glyph);var count=Label(world!.State.Inventory.Count(ingredient.Key)+" / "+ingredient.Value,13);count.AddThemeColorOverride("font_color",world.State.Inventory.Count(ingredient.Key)>=ingredient.Value?new("c8dab0"):new("e9a294"));stack.AddChild(count);}
            row.AddChild(Label("→",24));row.AddChild(new ItemGlyph{Catalog=catalog,Item=new(r.Output,r.Count),CustomMinimumSize=new(58,58),TooltipText=catalog.Items[r.Output].Name});
            var make=Button("制作",()=>Craft(r));make.Disabled=!r.Inputs.All(p=>world!.State.Inventory.Count(p.Key)>=p.Value);row.AddChild(make);
            row.AddChild(Button(world!.State.FavoriteRecipes.Contains(r.Id)?"★":"☆",()=>{if(!world.State.FavoriteRecipes.Add(r.Id))world.State.FavoriteRecipes.Remove(r.Id);ShowRecipes();}));
        }
        if(currentStation is "furnace" or "campfire"&&currentContainer.HasValue){var e=world!.State.Entities[currentContainer.Value.ToString()];col.AddChild(Label(e.RecipeId==null?"设施空闲；完成的产物放入设施容器。":"正在制作 "+catalog.Recipes[e.RecipeId].Name+"，剩余 "+Math.Max(0,e.FinishTime-world.State.Time).ToString("0")+" 秒（世界暂停时停止）",14));col.AddChild(Button("取出设施物品",ShowInventory));}
        var buttons=Row(col);buttons.AddChild(Button("背包",ShowInventory));buttons.AddChild(Button("返回游戏",()=>{currentContainer=null;SetScreen("play");}));
        if(currentStation=="calibration_bench")buttons.AddChild(Button("装备维护",()=>ShowWorkshopMaintenance(ShowRecipes)));
    }
    private void Craft(Recipe r)
    {
        var feet=player!.GlobalPosition;
        if(!world!.Craft(r,currentContainer,[feet.X,feet.Y,feet.Z],out string error)){Toast(error);return;}
        ShowRecipes();audio.Play("craft");Toast(r.Seconds>0?"制作已开始；返回游戏后计时":"已制作 "+r.Name);
    }
    private bool HandleMapInput(InputEvent e)
    {
        if(world==null||!e.IsActionPressed("map")||screen is not ("play" or "map"))return false;
        if(screen=="map")SetScreen("play");else ShowMap();GetViewport().SetInputAsHandled();return true;
    }
    private void ShowMap()
    {
        SetScreen("map");var col=Screen(overlay,"荒野地图");CompactInventoryHeading(col);((PanelContainer)col.GetParent()).AddThemeStyleboxOverride("panel",Style("21190ede",18));var instructions=Label(InRoom?"拖动平移 · 滚轮缩放 · 联机世界继续运行":"拖动平移 · 滚轮缩放 · 单人世界暂停",16);instructions.AddThemeColorOverride("font_color",Muted);instructions.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(instructions);
        var details=Label("",14);details.AddThemeColorOverride("font_color",Gold);details.AutowrapMode=TextServer.AutowrapMode.WordSmart;var viewport=overlay.GetViewportRect().Size;var explorer=player!;var position=explorer.GlobalPosition;
        string mapSelfId=RemoteContext?executingGuest!.State.Id:NetHost?"host":NetClient?clientPlayerId:"local";
        var map=new WorldMap{ Name="InteractiveWorldMap",World=world!,PlayerPosition=position,PlayerNickname=networkNickname,PlayerMarkersSource=()=>CaptureMapRoster(mapSelfId,explorer),PositionSource=()=>GodotObject.IsInstanceValid(explorer)?explorer.GlobalPosition:position,FacingSource=()=>GodotObject.IsInstanceValid(explorer)?explorer.GlobalRotation.Y:0,CustomMinimumSize=new(Math.Clamp(viewport.X-116,500,1400),Math.Clamp(viewport.Y-350-95*UiFactory.FontScale,160,720)),StatusChanged=text=>details.Text=text };
        var buttons=Row(col);buttons.AddChild(Button("＋",()=>map.ZoomAt(map.Size/2,1.25f)));buttons.AddChild(Button("－",()=>map.ZoomAt(map.Size/2,.8f)));buttons.AddChild(Button("定位玩家",map.FocusPlayer));buttons.AddChild(Button("全图",map.FitWorld));
        col.AddChild(map);col.AddChild(details);var legend=Label("暖白箭头是我 · 彩色箭头是在线朋友 · 点击查看姓名坐标 · 边缘箭头提示视野外玩家\n暗区未探索 · 金/青灯塔 · 橙背包 · 蓝标记",14);legend.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(legend);
        var actions=Row(col);actions.AddChild(Button("标记当前位置",()=>{world!.State.Markers.Add([explorer.GlobalPosition.X,explorer.GlobalPosition.Z]);map.QueueRedraw();}));actions.AddChild(Button("主线日志与路线提示（J）",ShowMainlineJournal));var back=Button("返回游戏（M）",()=>SetScreen("play"));Primary(back);actions.AddChild(back);
    }
    private void ShowBeacon(Cell cell)
    {
        if(Mainline.Enabled(world!.State)){ShowMainlineBeacon(cell);return;}
        int index=Array.FindIndex(world.Terrain.TowerLocations,t=>Math.Abs(t.X-cell.X)<5&&Math.Abs(t.Z-cell.Z)<5);if(index<0)return;
        SetScreen("beacon");var col=Screen(overlay,$"古代灯塔 {index+1}");
        if(!world!.State.Beacons.Contains(index)){
            col.AddChild(Label("修复需要：灯塔符印 ×1、铁锭 ×2。",17));
            col.AddChild(Button("修复并点亮",()=>{var next=world.State.Inventory.Copy();if(!world.State.Creative&&(!next.Remove("beacon_sigil",1)||!next.Remove("iron_ingot",2))){Toast("修复材料不足");return;}world.State.Inventory=next;world.State.Beacons.Add(index);RequestSave();ShowBeacon(cell);audio.Play("pickup");}));
        }else{
            col.AddChild(Label("区域线索：附近可找到遗迹和矿物；前往已激活灯塔消耗煤 ×1。",16));
            foreach(int destination in world.State.Beacons.Order())if(destination!=index){int target=destination;col.AddChild(Button("旅行至灯塔 "+(target+1),()=>{
                if(creatures.Any(c=>c.IsThreat&&c.GlobalPosition.DistanceTo(player!.GlobalPosition)<12)){Toast("附近仍有敌人，无法旅行");return;}
                if(!world.State.Creative&&!world.State.Inventory.Remove("coal",1)){Toast("缺少旅行燃料：煤 ×1");return;}var t=world.Terrain.TowerLocations[target];player!.GlobalPosition=new(t.X+.5f,world.Terrain.Surface(t.X,t.Z)+1.1f,t.Z+2.5f);player.Velocity=Vector3.Zero;world.State.Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];SetScreen("play");RequestSave();}));}
        }
        if(world.State.Beacons.Count==3&&world.State.GuardianDefeated)col.AddChild(Label("灯塔网络已恢复。你仍可自由探索与建造。",21));col.AddChild(Button("返回游戏",()=>SetScreen("play")));
    }
    private void ShowSettings(string back)
    {
        SetScreen("settings");var col=Screen(overlay,"设置");var viewport=overlay.GetViewportRect().Size;
        float panelWidth=Math.Clamp(viewport.X-64,340,800);((PanelContainer)col.GetParent()).CustomMinimumSize=new(panelWidth,0);
        var scroll=new ScrollContainer{CustomMinimumSize=new(0,Math.Clamp(viewport.Y-250*settings.UiScale,140,520)),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};col.AddChild(scroll);
        var list=Column(scroll);list.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;list.AddThemeConstantOverride("separation",14);
        float labelWidth=Math.Clamp(panelWidth*.25f,120,190);
        void Slider(string title,double min,double max,double step,double value,Action<double> change,Func<double,string> format){
            var row=Row(list);row.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
            var label=Label(title,16);label.CustomMinimumSize=new(labelWidth,34);label.VerticalAlignment=VerticalAlignment.Center;row.AddChild(label);
            var number=Label(format(value),16);number.CustomMinimumSize=new(80,34);number.HorizontalAlignment=HorizontalAlignment.Center;number.VerticalAlignment=VerticalAlignment.Center;number.AddThemeColorOverride("font_color",UiFactory.Gold);row.AddChild(number);
            var slider=new HSlider{MinValue=min,MaxValue=max,Step=step,Value=value,CustomMinimumSize=new(100,34),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,TooltipText=title};row.AddChild(slider);
            slider.ValueChanged+=v=>{change(v);number.Text=format(v);ApplySettings();};
        }
        void Option(string title,OptionButton option){var row=Row(list);var label=Label(title,16);label.CustomMinimumSize=new(labelWidth,38);label.VerticalAlignment=VerticalAlignment.Center;row.AddChild(label);option.CustomMinimumSize=new(120,38);option.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(option);}
        UiFactory.Heading(list,"♫  声音与视角");
        Slider("主音量",0,1,.05,settings.Volume,v=>settings.Volume=(float)v,v=>$"{v*100:0}%");
        Slider("音效音量",0,1,.05,settings.EffectsVolume,v=>settings.EffectsVolume=(float)v,v=>$"{v*100:0}%");
        Slider("环境音量",0,1,.05,settings.AmbientVolume,v=>settings.AmbientVolume=(float)v,v=>$"{v*100:0}%");
        Slider("音乐音量",0,1,.05,settings.MusicVolume,v=>settings.MusicVolume=(float)v,v=>$"{v*100:0}%");
        Slider("鼠标灵敏度",.0005,.006,.0001,settings.Sensitivity,v=>settings.Sensitivity=(float)v,v=>v.ToString("0.0000"));
        Slider("视野",55,110,1,settings.Fov,v=>settings.Fov=(float)v,v=>$"{v:0}°");
        UiFactory.Heading(list,"▧  图像显示");
        Slider("界面字体比例",.8,1.4,.1,settings.UiScale,v=>settings.UiScale=(float)v,v=>$"{v:0.0}×");
        var view=new OptionButton();foreach(int r in new[]{4,6,8,12})view.AddItem($"{r} 个地形柱",r);view.Select(Math.Max(0,Array.IndexOf(new[]{4,6,8,12},settings.ViewDistance)));view.ItemSelected+=i=>{settings.ViewDistance=view.GetItemId((int)i);ApplySettings();};Option("渲染视距",view);
        var limit=new OptionButton();foreach(int fps in new[]{30,60,120,0})limit.AddItem(fps==0?"无限制":$"{fps} FPS",fps);limit.Select(Math.Max(0,Array.IndexOf(new[]{30,60,120,0},settings.FpsLimit)));limit.ItemSelected+=i=>{settings.FpsLimit=limit.GetItemId((int)i);ApplySettings();};Option("帧率上限",limit);
        var resolution=new OptionButton();var sizes=new[]{new Vector2I(1280,720),new(1920,1080),new(2560,1440)};foreach(var r in sizes)resolution.AddItem($"{r.X} × {r.Y}");resolution.Select(Math.Max(0,Array.IndexOf(sizes,new Vector2I(settings.WindowWidth,settings.WindowHeight))));resolution.ItemSelected+=i=>{var size=sizes[(int)i];settings.WindowWidth=size.X;settings.WindowHeight=size.Y;ApplySettings(true);};Option("窗口分辨率",resolution);
        var full=new CheckButton{Text="全屏",ButtonPressed=settings.Fullscreen};full.Toggled+=v=>{settings.Fullscreen=v;ApplySettings(true);};list.AddChild(full);
        var shadow=new CheckButton{Text="实时阴影",ButtonPressed=settings.Shadows};shadow.Toggled+=v=>{settings.Shadows=v;ApplySettings();};list.AddChild(shadow);
        var flash=new CheckButton{Text="受伤画面闪烁",ButtonPressed=settings.DamageFlash};flash.Toggled+=v=>{settings.DamageFlash=v;ApplySettings();};list.AddChild(flash);
        var weatherEffects=new CheckButton{Text="雨雪粒子",ButtonPressed=settings.WeatherEffects};weatherEffects.Toggled+=v=>settings.WeatherEffects=v;list.AddChild(weatherEffects);
        UiFactory.Heading(list,"⌨  按键绑定");var note=Label("点击右侧按键，再按下新的键位。",14);note.AddThemeColorOverride("font_color",UiFactory.Muted);list.AddChild(note);
        foreach(var p in defaultKeys){string action=p.Key;string name=p.Value.Name;var row=Row(list);var label=Label(name,16);label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;label.VerticalAlignment=VerticalAlignment.Center;row.AddChild(label);
            var key=Button(OS.GetKeycodeString((Key)settings.Keys.GetValueOrDefault(action,(long)p.Value.Key)),()=>{binding=action;Toast("请按下用于「"+name+"」的新按键");});key.CustomMinimumSize=new(138,38);key.TooltipText="重新绑定："+name;row.AddChild(key);}
        var save=Button("保存并返回",()=>{PersistSettings();if(back=="pause")ShowPause();else ShowMenu();});UiFactory.DecorateAction(save,"save",true);col.AddChild(save);
    }
    private void ApplySettings(bool resizeWindow=false)
    {
        if(damageFeedback!=null)damageFeedback.FlashEnabled=settings.DamageFlash;
        settings.EffectsVolume=float.IsFinite(settings.EffectsVolume)?Math.Clamp(settings.EffectsVolume,0,1):1;settings.AmbientVolume=float.IsFinite(settings.AmbientVolume)?Math.Clamp(settings.AmbientVolume,0,1):.7f;settings.MusicVolume=float.IsFinite(settings.MusicVolume)?Math.Clamp(settings.MusicVolume,0,1):.3f;
        if(!float.IsFinite(settings.Sensitivity))settings.Sensitivity=.0023f;if(!float.IsFinite(settings.Fov))settings.Fov=78;if(!float.IsFinite(settings.UiScale))settings.UiScale=1;if(!float.IsFinite(settings.Volume))settings.Volume=.4f;
        if(!new[]{0,30,60,120}.Contains(settings.FpsLimit))settings.FpsLimit=60;
        settings.ViewDistance=new[]{4,6,8,12}.Contains(settings.ViewDistance)?settings.ViewDistance:4;
        settings.Sensitivity=Math.Clamp(settings.Sensitivity,.0005f,.006f);settings.Fov=Math.Clamp(settings.Fov,55,110);settings.UiScale=Math.Clamp(settings.UiScale,.8f,1.4f);settings.Volume=Math.Clamp(settings.Volume,0,1);
        Engine.MaxFps=settings.FpsLimit;var mode=settings.Fullscreen&&!IsValidation?DisplayServer.WindowMode.Fullscreen:DisplayServer.WindowMode.Windowed;
        if(DisplayServer.WindowGetMode()!=mode)DisplayServer.WindowSetMode(mode);
        if(!new[]{new Vector2I(1280,720),new(1920,1080),new(2560,1440)}.Contains(new Vector2I(settings.WindowWidth,settings.WindowHeight))){settings.WindowWidth=1280;settings.WindowHeight=720;}
        if(resizeWindow&&mode==DisplayServer.WindowMode.Windowed)GetWindow().Size=new(settings.WindowWidth,settings.WindowHeight);
        if(IsValidation&&DisplayServer.GetName()!="headless")GetWindow().Position=new(-10000,-10000);
        audio?.ApplyVolumes(settings.Volume,settings.EffectsVolume,settings.AmbientVolume,settings.MusicVolume);
        if(player!=null){player.Sensitivity=settings.Sensitivity;player.Camera.Fov=settings.Fov;}if(renderer!=null)renderer.Radius=settings.ViewDistance;if(sun!=null)sun.ShadowEnabled=settings.Shadows;
        if(overlay!=null){UiFactory.ApplyFontScale(overlay,settings.UiScale);overlay.Theme=UiFactory.Theme();}if(hudRoot!=null)UiFactory.ApplyFontScale(hudRoot,settings.UiScale);
    }
    private void PersistSettings(){try{File.WriteAllText(settingsPath,JsonSerializer.Serialize(settings,Catalog.JsonOptions));}catch(Exception e){Toast("设置保存失败："+e.Message);}}
    private void ShowHelp(string back)
    {
        SetScreen("help");var col=Screen(overlay,"生存手册");var viewport=overlay.GetViewportRect().Size;((PanelContainer)col.GetParent()).CustomMinimumSize=new(Math.Clamp(viewport.X-64,340,820),0);
        var scroll=new ScrollContainer{CustomMinimumSize=new(0,Math.Clamp(viewport.Y-(world==null?235:330)*settings.UiScale,140,520)),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};col.AddChild(scroll);var list=Column(scroll);list.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;list.AddThemeConstantOverride("separation",14);
        void Section(string title,params string[] paragraphs){UiFactory.Heading(list,title);foreach(string paragraph in paragraphs){var label=Label(paragraph,16);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;list.AddChild(label);}}
        Section("移动与交互","WASD 移动，Space 跳跃；Shift 蹲伏防坠，Ctrl 疾跑。","鼠标左键挖掘或攻击；右键放置、进食或播种。1–9 / 滚轮选择快捷栏。","E 打开背包，C 随身制作，Q 使用设施，R 旋转建筑；靠近掉落物会自动拾取。",$"{BoundKey("swap_hands")} 交换主副手；Shift 点击铁盔甲穿入空槽。按住右键，面向敌人使用盾牌。",$"创造模式在背包中打开物品目录；{BoundKey("fly")} 开关飞行，Space 上升，Shift 下降。");
        Section("从第一把工具开始","收集地面原木与碎石，制作石镐。原木可徒手获取，硬矿石需要对应等级的工具。","木板用于工作台、木箱与营地。营火消耗木材烹饪，并提供普通敌人的 7 格安全区；遗址精英与 Boss 仍会攻击。","熔炉用煤冶炼铁。设施的制作计时随世界推进，完成的物品放在设施容器中。","火把需放在地面或墙上才能照明；进入深矿洞前携带足够火把。");
        Section("营地与农田","制作床设置复活点，夜间按 Q 使用床可过夜。门需要两格高度，按 Q 开关。","耕地可播种：胡萝卜 180 秒、小麦 270 秒、土豆 360 秒成熟。按 Q 收获，接近掉落物取得作物与种子。","水桶可以搬运静态水源；水体暂不流动，世界有明确边界。");
        Section("三塔归火与远征","M 查看已探索地图；J 打开三塔归火日志，查看遗址线索与准备建议。","依次修复寻路、校准、镇封三塔；旱地与雪岭的远征顺序可以自选。击败晶穴守护者后进入地心遗址。","地心遗址有电桥、反射器、供能室三个节点。熔心巨像开战后，先关闭四个锚点；高处锚点可用弓箭。重击后的核心最脆弱。","校准台制作余烬合金与武器。工作台与校准台的装备维护可安装耐热衬片，或使用修理包恢复耐久。");
        Section("与朋友一起探索","所有玩家先加入同一个 ZeroTier 虚拟网络。房主在自己的存档中开放联机，朋友填写房主的虚拟 IP 与端口加入。","加入前选择角色外观；房主退出后房间关闭。联机时打开背包、地图或菜单，世界会继续运行。");
        Section("保存与恢复","单人模式按 Esc 暂停。每 60 秒自动保存；手动保存、正常退出与重要进度也会保存。联机世界由房主保存。","物品不足、空间被占用、工具等级不足会显示具体原因；保存失败不会提示成功。");
        if(world!=null){var actions=Row(col);var skip=Button("跳过新手引导",()=>{world.State.TutorialHidden=true;SetScreen("play");});skip.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;actions.AddChild(skip);var restart=Button("重新显示新手引导",()=>{tutorial=0;world.State.TutorialStep=0;world.State.TutorialHidden=false;SetScreen("play");});restart.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;actions.AddChild(restart);}
        var backButton=Button("返回",()=>{if(back=="pause")ShowPause();else ShowMenu();});UiFactory.DecorateAction(backButton,"back");col.AddChild(backButton);
    }
    private void ShowCredits(string back)
    {
        SetScreen("credits");var col=Screen(overlay,"制作人员与素材");var viewport=overlay.GetViewportRect().Size;((PanelContainer)col.GetParent()).CustomMinimumSize=new(Math.Clamp(viewport.X-64,340,760),0);
        var scroll=new ScrollContainer{CustomMinimumSize=new(0,Math.Clamp(viewport.Y-235*settings.UiScale,140,500)),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};col.AddChild(scroll);var list=Column(scroll);list.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        UiFactory.Heading(list,"方野：余烬灯塔");var subtitle=Label("荒野求生 · 三塔归火",17);subtitle.AddThemeColorOverride("font_color",UiFactory.Muted);list.AddChild(subtitle);
        void Credit(string role,string detail){var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",UiFactory.Style("201c18",14));list.AddChild(panel);var content=Column(panel);var heading=Label(role,15);heading.AddThemeColorOverride("font_color",UiFactory.Gold);content.AddChild(heading);var body=Label(detail,16);body.AutowrapMode=TextServer.AutowrapMode.WordSmart;body.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;content.AddChild(body);}
        Credit("游戏引擎","Godot 4.6 .NET · MIT 许可");Credit("世界与角色","方块颜色、程序化生物与体素角色：项目原创");Credit("界面与声音","界面控件、图标与合成音效：项目原创；灯塔背景依据用户参考图生成。");Credit("字体","使用 Windows 系统字体，不随发布包分发");
        UiFactory.Heading(list,"许可说明");var license=Label("第三方许可详见发布包内的 LICENSES.md、GODOT_LICENSE.txt 与 GODOT_COPYRIGHT.txt。",15);license.AutowrapMode=TextServer.AutowrapMode.WordSmart;license.AddThemeColorOverride("font_color",UiFactory.Muted);list.AddChild(license);
        var backButton=Button("返回",()=>{if(back=="pause")ShowPause();else ShowMenu();});UiFactory.DecorateAction(backButton,"back");col.AddChild(backButton);
    }
    private bool smokeEdited;
    private bool visualCapturePending;
    private async void FinishVisualCheck()
    {
        string path=ProjectSettings.GlobalizePath("res://test-results");
        world!.State.Inventory.Add("stone_pick",1,catalog);world.State.Inventory.Add("plank",32,catalog);world.State.Inventory.Add("berry",9,catalog);world.State.Inventory.Add("iron_sword",1,catalog);
        world.State.Health=73;world.State.Hunger=62;world.State.Experience=17;
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(path,"scene.png"));
        var origin=player!.GlobalPosition;
        world.State.Weather.Phase=2;world.State.Weather.Blend=1;world.State.Weather.Remaining=120;
        for(int i=0;i<60;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(path,"rain.png"));
        bool winter=false;
        for(int x=-512;x<=512&&!winter;x+=64)for(int z=-512;z<=512&&!winter;z+=64)if(world.Terrain.Biome(x,z)==3&&world.Terrain.Surface(x,z)>Terrain.SeaLevel){player.GlobalPosition=new(x+.5f,world.Terrain.Surface(x,z)+1.05f,z+.5f);player.Velocity=Vector3.Zero;winter=true;}
        if(!winter){GD.PushError("VISUAL_FAILED: no winter sample");GetTree().Quit(6);return;}
        for(int i=0;i<1800&&!renderer!.IsTerrainReady(player.GlobalPosition);i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(!renderer!.IsTerrainReady(player.GlobalPosition)){GD.PushError("VISUAL_FAILED: winter terrain timeout");GetTree().Quit(6);return;}
        for(int i=0;i<60;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(path,"snow.png"));
        player.GlobalPosition=origin;player.Velocity=Vector3.Zero;
        currentContainer=null;ShowInventory();
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(path,"inventory.png"));
        GD.Print("SMOKE_PASS: terrain, collision readiness, edit, save, reload, minimap, HUD, inventory, rain and snow render");QuitCleanly();
    }
    private void RunSmoke(double delta)
    {
        if(visualCapturePending)return;
        smokeClock+=delta;if(smokeClock>45&&!renderer!.IsTerrainReady(player!.GlobalPosition)){GD.PushError("SMOKE_FAILED: terrain never became ready");GetTree().Quit(2);return;}
        if(!renderer!.IsTerrainReady(player!.GlobalPosition)||smokeClock<4)return;
        if(!smokeEdited){smokeEdited=true;var p=new Cell(2,37,2);world!.Set(p,catalog.Number("plank"));renderer.RefreshEdit(p);world.State.Inventory.Add("stone",10,catalog);if(!SaveNow()){GetTree().Quit(3);return;}var loaded=saves.Load(world.State.WorldId);if(loaded.Snapshot.Changes[p.ToString()]!="plank"||loaded.Snapshot.Inventory.Count("stone")!=10){GetTree().Quit(4);return;}if(visualCheck){world.Set(p,world.Terrain.Get(p));renderer.RefreshEdit(p);}}
        if(smokeClock>7){string path=ProjectSettings.GlobalizePath("res://test-results");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"runtime.json"),JsonSerializer.Serialize(new{passed=true,seconds=smokeClock,loaded=renderer.Loaded,pending=renderer.Pending,editMs=renderer.LastEditMs,renderer=RenderingServer.GetCurrentRenderingMethod(),gpu=RenderingServer.GetVideoAdapterName(),godot=Engine.GetVersionInfo()["string"].AsString()},Catalog.JsonOptions));
            if(visualCheck){visualCapturePending=true;FinishVisualCheck();return;}
            GD.Print("SMOKE_PASS: terrain, collision readiness, edit, save and reload");QuitCleanly();
        }
    }
}


