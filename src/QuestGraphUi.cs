using System;
using System.Linq;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;
namespace EmberLighthouse;

public partial class Game
{
    private QuestGraph? journalGraph;
    private Vector2? journalCenter;
    private float journalZoom=.9f;
    private string journalSelection="";
    private QuestNode? trackedQuest;
    private void RememberJournalView()
    {
        if(journalGraph==null||!GodotObject.IsInstanceValid(journalGraph))return;journalCenter=journalGraph.Center;journalZoom=journalGraph.Zoom;journalSelection=journalGraph.SelectedId;
    }
    private void ShowQuestGraph()
    {
        SetScreen("journal");foreach(var child in overlay.GetChildren()){overlay.RemoveChild(child);child.QueueFree();}
        Backdrop(overlay,true,.10f);
        var margin=new MarginContainer();margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);foreach(string side in new[]{"left","top","right","bottom"})margin.AddThemeConstantOverride("margin_"+side,28);overlay.AddChild(margin);
        var col=Column(margin);var header=Row(col);var title=Label(Mainline.Enabled(world!.State)?"三塔归火 · 探索日志":"灯塔复苏 · 探索日志",30);title.AddThemeColorOverride("font_color",Gold);title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;header.AddChild(title);
        void Remember()=>RememberJournalView();
        header.AddChild(Button("返回游戏",()=>{Remember();SetScreen("play");}));
        var objectiveLabel=Label(Mainline.Enabled(world!.State)?Mainline.Objective(world.State):$"原版路线 · 已修复灯塔 {world.State.Beacons.Count}/3 · 守护者 {(world.State.GuardianDefeated?"已击败":"未击败")}\n三座灯塔顺序自选，与守护者挑战并行。",17);objectiveLabel.AddThemeColorOverride("font_color",Parchment);objectiveLabel.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(objectiveLabel);
        var body=Row(col);body.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        var graphCol=Column(body);graphCol.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        var toolbar=Row(graphCol);journalGraph=new QuestGraph{Catalog=catalog,Nodes=QuestJournal.Build(world),SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(230,230)};
        toolbar.AddChild(Button("当前目标",()=>journalGraph.FocusCurrent()));toolbar.AddChild(Button("全图",()=>journalGraph.Fit()));toolbar.AddChild(Button("−",()=>journalGraph.ZoomAt(journalGraph.Size/2,1/1.2f)));toolbar.AddChild(Button("＋",()=>journalGraph.ZoomAt(journalGraph.Size/2,1.2f)));
        graphCol.AddChild(journalGraph);var help=Label("拖动空白处平移 · 滚轮缩放 · 点击节点查看 · ← → 切换",13);help.AddThemeColorOverride("font_color",Muted);help.AutowrapMode=TextServer.AutowrapMode.WordSmart;graphCol.AddChild(help);
        var detailPanel=new PanelContainer{CustomMinimumSize=new(340,0),SizeFlagsVertical=Control.SizeFlags.ExpandFill};detailPanel.AddThemeStyleboxOverride("panel",Style("21180fdf",18));body.AddChild(detailPanel);
        var detailFrame=Column(detailPanel);detailFrame.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        var details=new ScrollContainer{CustomMinimumSize=new(300,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=Control.SizeFlags.ExpandFill};detailFrame.AddChild(details);
        var detailCol=Column(details);detailCol.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        var detailActions=Column(detailFrame);
        void ShowDetails(QuestNode node){
            foreach(var child in detailCol.GetChildren()){detailCol.RemoveChild(child);child.QueueFree();}
            foreach(var child in detailActions.GetChildren()){detailActions.RemoveChild(child);child.QueueFree();}
            var heading=Row(detailCol);heading.AddChild(new TextureRect{Texture=ItemSprites.Texture(catalog.Items[node.Icon]),CustomMinimumSize=new(48,48),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered});
            var name=Label(node.Title,23);name.AddThemeColorOverride("font_color",Gold);name.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;name.AutowrapMode=TextServer.AutowrapMode.WordSmart;heading.AddChild(name);
            void Text(string value,int size=16){var l=Label(value,size);l.AutowrapMode=TextServer.AutowrapMode.WordSmart;l.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;detailCol.AddChild(l);}
            Text(QuestGraph.StatusName(node.Status)+" · "+(node.Status==QuestStatus.Available?"可以开始探索":node.Done?"进度永久保留":"可提前查看路线与准备"),14);
            Text(node.Description);Heading(detailCol,"准备物品");
            var supplyIds=catalog.Items.Values.Where(item=>(item.Name.Length>1||item.Id=="bed")&&node.Supplies.Contains(item.Name,StringComparison.Ordinal)).OrderByDescending(item=>item.Name.Length).Select(item=>item.Id).ToList();
            foreach(var (word,id) in new[]{("照明","torch"),("熟食","cooked_meat"),("箱子","chest"),("弓箭","bow"),("弓箭","arrow")})if(node.Supplies.Contains(word,StringComparison.Ordinal)&&catalog.Items.ContainsKey(id)&&!supplyIds.Contains(id))supplyIds.Add(id);
            if(supplyIds.Count>0){var supplies=new GridContainer{Columns=6};detailCol.AddChild(supplies);foreach(var id in supplyIds.Take(6))supplies.AddChild(new ItemGlyph{Catalog=catalog,Item=new(id,1),CustomMinimumSize=new(40,40),TooltipText=catalog.Items[id].Name});}
            Text(node.Supplies);Heading(detailCol,"完成后获得");Text(node.Reward);
            if(node.Target is {} cell){Text("目标位置\n"+DirectionTo(cell,world.State.Mainline.StrongHints),14);var track=Button(node.Done?"追踪此地点":"追踪此目标",()=>{Remember();trackedMainlineTarget=node.Id;trackedQuest=node;SetScreen("play");Toast("已追踪："+node.Title);});track.Disabled=node.Status==QuestStatus.Locked;Primary(track);detailActions.AddChild(track);}
            if(node.Parents.Length>0)Text("前置："+string.Join("、",node.Parents.Select(id=>journalGraph.Nodes.First(n=>n.Id==id).Title)),14);
            Text("绿色：已完成\n金色：可进行\n灰色：待解锁",14);
        }
        journalGraph.Selected=ShowDetails;journalGraph.SelectNode(journalSelection.Length>0?journalSelection:QuestJournal.Current(journalGraph.Nodes).Id,true);
        if(journalCenter is {} center)journalGraph.SetView(center,journalZoom);
        var hints=new CheckButton{Text="显示目标准确位置",ButtonPressed=world.State.Mainline.StrongHints};hints.Toggled+=value=>{world.State.Mainline.StrongHints=value;Remember();ShowQuestGraph();};col.AddChild(hints);
    }
}
