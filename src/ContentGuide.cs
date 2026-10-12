using System;
using System.Linq;
using EmberLighthouse.Items;
using Godot;
using static EmberLighthouse.UI.UiFactory;
namespace EmberLighthouse;
public partial class Game
{
    private void ShowContentGuide()
    {
        SetScreen("guide");var col=Screen(overlay,"物品与生物图鉴");var search=new LineEdit{PlaceholderText="搜索物品名称、用途或生物名称"};col.AddChild(search);var tabs=new OptionButton();tabs.AddItem("物品与装备");tabs.AddItem("常见生物");col.AddChild(tabs);var scroll=Scroll(col,350);scroll.HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled;var list=Column(scroll);list.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        void Draw(){foreach(var child in list.GetChildren()){list.RemoveChild(child);child.QueueFree();}string query=search.Text.Trim();if(tabs.Selected==0){foreach(var d in catalog.Items.Values.Where(d=>query==""||d.Name.Contains(query)||d.Description.Contains(query)).OrderBy(d=>d.Name)){
            var card=new HBoxContainer();list.AddChild(card);card.AddChild(new UI.ItemGlyph{Catalog=catalog,Item=new(d.Id,1),CustomMinimumSize=new(52,52),Size=new(52,52)});var body=Column(card);body.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;body.AddChild(Label(d.Name,18));string usage=d.Use=="armor"?$"护甲 {d.ArmorPoints} · 耐久 {d.Durability}\n"+ArmorTraits.Describe(d):d.Description!=""?d.Description:d.Use switch{"food"=>$"食用恢复饥饿 {d.Power}","seed"=>"对耕地右键播种，成熟后Q收获","pick"=>"采矿工具，工具等级决定可采矿物","axe"=>"砍伐木材的工具","shovel"=>"挖掘土砂更快","sword"=>"近战武器","bow"=>"消耗箭，远程攻击","shield"=>"按住右键，朝敌人方向格挡",_=>d.Block?"建筑或自然材料；右键放置":"合成与探索材料"};var hint=Label(usage,14);hint.AutowrapMode=TextServer.AutowrapMode.WordSmart;body.AddChild(hint);
        }}else{foreach(var d in creatureDefs.Where(d=>query==""||d.Name.Contains(query)).OrderBy(d=>d.Hostile).ThenBy(d=>d.Name)){var name=Label(d.Name+" · "+(d.Hostile?"敌对生物":"野生动物"),18);list.AddChild(name);string food=World.LifeRules.Food(d.Id);var hint=Label((food!=""?"引诱与喂食："+catalog.Items[food].Name+"；Q查看养殖、驯服或装备操作。":"")+"\n掉落："+catalog.Items[d.Drop].Name+" · "+(d.Hostile?"观察攻击前摇与地形掩体":"按地貌稀疏分布"),14);hint.AutowrapMode=TextServer.AutowrapMode.WordSmart;list.AddChild(hint);}}}
        search.TextChanged+=_=>Draw();tabs.ItemSelected+=_=>Draw();Draw();col.AddChild(Button("返回",ShowPause));
    }
}
