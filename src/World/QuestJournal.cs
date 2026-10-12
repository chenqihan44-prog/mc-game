using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
namespace EmberLighthouse.World;

public enum QuestStatus {Locked,Available,Completed}
public sealed record QuestNode(string Id,string Title,string Icon,float X,float Y,string[] Parents,bool Done,string Description,string Supplies,string Reward,Cell? Target)
{
    public QuestStatus Status {get;set;}
}
public static class QuestJournal
{
    public static List<QuestNode> Build(WorldState world)
    {
        if(!Mainline.Enabled(world.State))return Legacy(world);
        var s=world.State;var q=s.Mainline;var a=world.Terrain.Adventure!;
        Cell Tower(int i){var t=a.Towers[i];return new(t.X,world.Terrain.Surface(t.X,t.Z)+1,t.Z);}
        var v=a.Site("vault");bool camp=q.Proofs.Contains("camp")||s.Beacons.Contains(0)||s.Inventory.Count("iron_pick")>0&&s.Entities.Values.Any(e=>e.Kind=="campfire");
        List<QuestNode> nodes=[
            new("camp","安营与铁器","campfire",0,210,[],camp,"建立补给营地，准备工作台、熔炉和铁器。此节点是准备建议，可自行选择何时远征。","床、箱子、铁镐、熟食与照明","可靠的复活点和补给",new((int)s.Respawn[0],(int)s.Respawn[1],(int)s.Respawn[2])),
            new("mine","废矿场哨长","stone_pick",250,210,[],q.Proofs.Contains("mine"),"探索废矿场，挖通坍塌砾石，击败深处的矿场哨长。检修证明会永久记录。","铁器、盾牌、熟食、火把","寻路塔检修证明",a.Site("mine").Entry),
            new("tower0","寻路塔","copper_coil",500,210,["mine"],s.Beacons.Contains(0),"使用检修证明与修复材料点亮第一座灯塔。","铜线圈 ×1、铁锭 ×4","勘测镜与双域远征线索",Tower(0)),
            new("forge","旱地熔炉堡","heat_lining",750,70,["tower0"],q.Proofs.Contains("forge"),"双域远征之一。喷口先发光再喷焰，可走外侧绕行；躲开炉卫冲锋与散射。两处区域可以任意顺序完成。","盾牌、熟食、方块；耐热衬片可降低喷焰伤害","炉心棱镜（永久藏品）",a.Site("forge").Entry),
            new("snow","雪岭观测所","bow",750,350,["tower0"],q.Proofs.Contains("snow"),"双域远征之一。沿旧阶梯登上观测台，或搭建另一条路径。利用掩体躲开连续光弹，离开重击预警圈。","弓箭、照明、搭建材料与熟食","霜辉棱镜（永久藏品）",a.Site("snow").Entry),
            new("tower1","校准塔","calibration_bench",1000,210,["forge","snow"],s.Beacons.Contains(1),"取得两枚棱镜后修复校准塔。两条远征分支在此汇合。","铜线圈 ×2、铁锭 ×4","灯塔旅行、校准台与余烬装备",Tower(1)),
            new("cave","深层晶穴","ember_sword",1250,210,["tower1"],q.Proofs.Contains("guardian"),"深入晶穴挑战余烬守护者。观察范围重击圈，侧移或撤出后反击。","铁甲、盾牌、照明与熟食；可选余烬武器","共鸣晶核（永久藏品）",a.Site("cave").Entry),
            new("tower2","镇封塔","beacon_sigil",1500,210,["cave"],s.Beacons.Contains(2),"在校准台制作符印，使用守护者晶核修复第三座灯塔。","铜线圈 ×2、铁锭 ×4、灯塔符印 ×1","地心遗址信号",Tower(2)),
            new("vault","地心遗址","core_console",1750,210,["tower2"],q.Nodes.Count==3,"探索三个供能区域。三个分支全部完成后，可以启动核心控制台。","坚实方块、弓箭、装备与补给","终局挑战入口",v.Entry),
            new("bridge","修复电桥","plank",2000,40,["tower2"],q.Nodes.Contains("bridge"),"在西北高台补齐连续六格缺口，然后 Q 使用节点连接供能。","至少六个坚实方块","电桥供能",v.At(-22,5,-16)),
            new("reflector","校准反射器","reflector",2000,210,["tower2"],q.Nodes.Contains("reflector"),"读取旁边碑文。旋转反射器，使其对准记录的接收方向，再确认校准。","无需额外材料，注意碑文方向","反射器供能",v.At(20,1,-16)),
            new("guard","供能室守卫","shield",2000,380,["tower2"],q.Nodes.Contains("guard"),"击败遗址东南供能室的守卫，线路会自动恢复。营火无法驱散此守卫。","盾牌、武器与熟食","供能室线路",v.At(18,1,18)),
            new("colossus","熔心巨像","energy_anchor",2250,210,["bridge","reflector","guard"],q.Completed,"在控制台开战。先关闭四个供能锚点，高台锚点可射箭。随后避开光弹、冲锋与重击，在重击后的核心窗口输出；低于 35% 生命时节奏加快。","弓箭、盾牌、修理包、方块、熟食；入口可设床与箱子","黎明之心与主线完成",v.At(0,1,6)),
            new("dawn","三塔归火","dawn_heart",2500,210,["colossus"],q.Completed,"主线已完成。灯塔网络已经稳定，可以继续探索、建造与收集。","自由选择后续目标","校准台可制作黎明之心纪念灯",Tower(1))
        ];
        foreach(var n in nodes)n.Status=n.Done?QuestStatus.Completed:n.Parents.All(p=>nodes.First(x=>x.Id==p).Done)?QuestStatus.Available:QuestStatus.Locked;
        // The vault is a grouping node; its three children unlock when it is reached.
        return nodes;
    }
    public static IEnumerable<(string From,string To)> Edges(List<QuestNode> nodes)
    {
        if(nodes.Any(n=>n.Id=="mine"))yield return("camp","mine");else {yield return("camp","crystal");yield return("camp","guardian");}foreach(var node in nodes)foreach(string parent in node.Parents)yield return(parent=="tower2"&&(node.Id is "bridge" or "reflector" or "guard")?"vault":parent,node.Id);
    }
    private static List<QuestNode> Legacy(WorldState world)
    {
        var s=world.State;Cell Tower(int i){var t=world.Terrain.TowerLocations[i];return new(t.X,world.Terrain.Surface(t.X,t.Z)+1,t.Z);}
        bool prepared=s.TutorialStep>=3||s.Inventory.Count("iron_pick")>0||s.Beacons.Count>0,crystal=s.Beacons.Count>0||s.Inventory.Count("beacon_sigil")>0,done=s.Beacons.Count==3&&s.GuardianDefeated;
        var guardian=Tower(2)+new Cell(2,0,1);
        List<QuestNode> nodes=[
            new("camp","安营与铁器","campfire",0,230,[],prepared,"此世界保留原版通关路线。建立营地，准备工作台、熔炉、铁镐与补给。","工作台、熔炉、床、铁镐与熟食","稳定的采矿与远征补给",new((int)s.Respawn[0],(int)s.Respawn[1],(int)s.Respawn[2])),
            new("crystal","晶矿与符印","crystal",250,100,[],crystal,"探索地下，使用足够等级的镐采集晶矿，在工作台制作灯塔符印。灯塔可以按任意顺序修复。","铁镐、照明；配方材料以工作台显示为准","三座灯塔的修复材料",new(20,world.Terrain.Surface(20,0)+1,0)),
            new("tower0","第一座灯塔","beacon",500,0,["crystal"],s.Beacons.Contains(0),"Q 使用第一座灯塔，提交材料修复。三座灯塔顺序自选。","灯塔符印 ×1、铁锭 ×2","灯塔网络节点一",Tower(0)),
            new("tower1","第二座灯塔","beacon",500,150,["crystal"],s.Beacons.Contains(1),"Q 使用第二座灯塔，提交材料修复。三座灯塔顺序自选。","灯塔符印 ×1、铁锭 ×2","灯塔网络节点二",Tower(1)),
            new("tower2","第三座灯塔","beacon",500,300,["crystal"],s.Beacons.Contains(2),"Q 使用第三座灯塔，提交材料修复。附近有守护者，先备好装备与食物。","灯塔符印 ×1、铁锭 ×2","灯塔网络节点三",Tower(2)),
            new("guardian","遗迹守护者","iron_sword",500,450,[],s.GuardianDefeated,"前往第三座灯塔附近挑战守护者。可以与修塔并行，避开范围重击与远程光弹。","铁甲、武器、盾牌与熟食","解除守护者威胁",guardian),
            new("dawn","网络复苏","dawn_heart",750,210,["tower0","tower1","tower2","guardian"],done,"三座灯塔全部修复，守护者已被击败。可继续自由探索与建造。","后续目标自选","原版主线完成",Tower(2))
        ];
        foreach(var n in nodes)n.Status=n.Done?QuestStatus.Completed:n.Parents.All(p=>nodes.First(x=>x.Id==p).Done)?QuestStatus.Available:QuestStatus.Locked;return nodes;
    }
    public static QuestNode Current(List<QuestNode> nodes)=>nodes.FirstOrDefault(n=>n.Status==QuestStatus.Available&&n.Id!="camp"&&n.Id!="vault")??nodes.Last();
}
