using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;

namespace EmberLighthouse;

public partial class Game
{
    private readonly List<(Cell Cell,Label3D Label)> mainlineLabels=[];
    private readonly List<MeshInstance3D> forgeVents=[];
    private double mainlineClock;
    private string trackedMainlineTarget="";
    private void InitializeMainline()
    {
        mainlineLabels.Clear();forgeVents.Clear();mainlineClock=0;trackedMainlineTarget="";trackedQuest=null;journalGraph=null;journalCenter=null;journalSelection="";
        if(world?.Terrain.Adventure is not {} layout)return;
        foreach(var (cell,id) in layout.Devices){var label=new Label3D{Text=catalog.Items[id].Name,Position=new(cell.X+.5f,cell.Y+1.5f,cell.Z+.5f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=28,PixelSize=.008f,Modulate=new("ffdb9e"),Visible=false};worldRoot!.AddChild(label);mainlineLabels.Add((cell,label));}
        for(int i=0;i<3;i++){var t=layout.Towers[i];int y=world.Terrain.Surface(t.X,t.Z);var label=new Label3D{Text=Mainline.TowerName(i),Position=new(t.X+.5f,y+10,t.Z+.5f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=34,PixelSize=.012f,Visible=false};worldRoot!.AddChild(label);mainlineLabels.Add((new(t.X,y+1,t.Z),label));}
        var forge=layout.Site("forge");var flameMaterial=new StandardMaterial3D{AlbedoColor=new(1,.42f,.08f,.48f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};var flameMesh=new BoxMesh{Size=new(4,2.2f,2)};
        foreach(int x in new[]{-6,6})for(int z=-24;z<=24;z+=6){var p=forge.At(x,2,z);var vent=new MeshInstance3D{Mesh=flameMesh,MaterialOverride=flameMaterial,Position=new(p.X+(x<0?1:0),p.Y+.3f,p.Z+1),Visible=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};worldRoot!.AddChild(vent);forgeVents.Add(vent);}
    }
    private IEnumerable<(string Id,string Name,Cell Cell)> MainlineTargets()
    {
        if(world?.Terrain.Adventure is not {} a)yield break;var s=world.State;var q=s.Mainline;
        Cell Tower(int i){var t=a.Towers[i];return new(t.X,world.Terrain.Surface(t.X,t.Z)+1,t.Z);}
        if(!s.Beacons.Contains(0)){if(!q.Proofs.Contains("mine"))yield return("mine","废矿场",a.Site("mine").Entry);yield return("tower0","寻路塔",Tower(0));yield break;}
        if(!q.Proofs.Contains("forge"))yield return("forge","旱地熔炉堡",a.Site("forge").Entry);
        if(!q.Proofs.Contains("snow"))yield return("snow","雪岭观测所",a.Site("snow").Entry);
        if(!q.Proofs.Contains("forge")||!q.Proofs.Contains("snow"))yield break;
        if(!s.Beacons.Contains(1)){yield return("tower1","校准塔",Tower(1));yield break;}
        if(!q.Proofs.Contains("guardian")){yield return("cave","深层晶穴",a.Site("cave").Entry);yield break;}
        if(!s.Beacons.Contains(2)){yield return("tower2","镇封塔",Tower(2));yield break;}
        if(!q.Completed)yield return("vault","地心遗址",a.Site("vault").Entry);
    }
    private string DirectionTo(Cell cell,bool exact=false)
    {
        var p=player!.GlobalPosition;double x=cell.X-p.X,z=cell.Z-p.Z,distance=Math.Sqrt(x*x+z*z);string[] directions=["东","东南","南","西南","西","西北","北","东北"];int i=((int)Math.Round(Math.Atan2(z,x)/Math.PI*4)+8)%8;
        return exact||distance<90?$"{directions[i]} · {distance:0} 格 · X {cell.X} / Y {cell.Y} / Z {cell.Z}":$"{directions[i]} · 约 {(int)(distance/200)*200}～{((int)(distance/200)+1)*200} 格";
    }
    private void ShowMainlineJournal(){if(world!=null)ShowQuestGraph();}
    private void Survey()
    {
        if(trackedQuest?.Target is {} trackedCell){Toast(trackedQuest.Title+" · "+DirectionTo(trackedCell,world!.State.Mainline.StrongHints));return;}
        var targets=MainlineTargets().ToArray();if(targets.Length==0){Toast("灯塔网络已稳定。继续自由探索。");return;}var target=targets.FirstOrDefault(t=>t.Id==trackedMainlineTarget);if(target.Id==null)target=targets[0];trackedMainlineTarget=target.Id;Toast(target.Name+" · "+DirectionTo(target.Cell,world!.State.Mainline.StrongHints));
    }
    private void ShowMainlineBeacon(Cell cell)
    {
        int index=Array.FindIndex(world!.Terrain.TowerLocations,t=>Math.Abs(t.X-cell.X)<5&&Math.Abs(t.Z-cell.Z)<5);if(index<0)return;
        SetScreen("beacon");var col=Screen(overlay,"古代灯塔 · "+Mainline.TowerName(index));var s=world.State;
        if(!s.Beacons.Contains(index)){
            col.AddChild(Label("修复材料："+string.Join("、",Mainline.RepairCost(index).Select(p=>catalog.Items[p.Key].Name+" ×"+p.Value)),17));
            col.AddChild(Label(index switch{0=>"需要矿场哨长的检修证明。点亮后解锁勘测镜与双域线索。",1=>"需要寻路塔、炉心棱镜、霜辉棱镜。点亮后解锁旅行、校准台与余烬装备。",_=>"需要校准塔与守护者的共鸣晶核。点亮后揭示地心遗址。"},16));
            col.AddChild(Button("修复并点亮",()=>{if(!Mainline.Repair(world,index,out var error)){Toast(error);return;}audio.Play("pickup");RequestSave();ShowMainlineBeacon(cell);}));
        }else{
            col.AddChild(Label("已恢复 · "+(index==0?"双域线索已记录":index==1?"校准配方已解锁":"地心遗址信号可追踪"),17));
            if(!s.Beacons.Contains(1))col.AddChild(Label("修复校准塔后开启灯塔旅行。",15));
            else foreach(int destination in s.Beacons.Order())if(destination!=index){int target=destination;col.AddChild(Button("旅行至 "+Mainline.TowerName(target)+"（煤炭 ×1）",()=>{
                if(creatures.Any(c=>c.IsThreat&&c.GlobalPosition.DistanceTo(player!.GlobalPosition)<12)){Toast("附近仍有敌人，无法旅行");return;}
                if(!s.Creative&&!s.Inventory.Remove("coal",1)){Toast("缺少煤炭 ×1");return;}var t=world.Terrain.TowerLocations[target];player!.GlobalPosition=new(t.X+.5f,world.Terrain.Surface(t.X,t.Z)+1.1f,t.Z+2.5f);player.Velocity=Vector3.Zero;s.Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];SetScreen("play");RequestSave();}));}
        }
        if(s.Mainline.Completed)col.AddChild(Label("三塔归火：灯塔网络已稳定。",19));col.AddChild(Button("主线日志",ShowMainlineJournal));col.AddChild(Button("返回游戏",()=>SetScreen("play")));
    }
    private bool InteractMainline(Cell p,string id)
    {
        if(world?.Terrain.Adventure is not {} a||!a.Devices.TryGetValue(p,out var device)||device!=id)return false;
        var q=world.State.Mainline;
        if(id=="energy_anchor"){if(Mainline.CloseAnchor(world,p)){Toast("已关闭供能锚点 "+q.Anchors.Count+"/4");RequestSave();}else Toast(q.FinaleActive?"此锚点已关闭":"启动核心后才能关闭供能锚点");return true;}
        SetScreen("mainline-device");var col=Screen(overlay,catalog.Items[id].Name);
        if(id=="quest_record"){
            string text=a.Nearby(p)?.Id switch{"mine"=>"矿场记录：旧网络失去供能。击败深处哨长取得检修证明，采铜冶炼线圈，修复西侧的寻路塔。坍塌砾石可挖通。","forge"=>"维修说明：内侧喷口先亮后喷；可沿外侧通道绕行。炉卫正面危险，观察冲撞预警后绕侧。营火不能驱散遗迹守卫。","snow"=>"观测日志：中部旧阶梯通往上层观测台；也可以自行搭建。连续射击时换掩体，预警圈出现后离开原位。",_=>"反射器校准记录：接收面朝向「"+new[]{"北","东","南","西"}[a.ReflectorAnswer]+"」。电桥需要补齐高台上连续六格路面；供能室守卫在东南侧。"};col.AddChild(Label(text,17));
        }else if(id=="reflector"){
            col.AddChild(Label("当前朝向："+new[]{"北","东","南","西"}[q.ReflectorRotation]+"\n旁边碑文记录了接收面的方向。",17));
            col.AddChild(Button("旋转 90°",()=>{q.ReflectorRotation=(q.ReflectorRotation+1)%4;InteractMainline(p,id);}));
            col.AddChild(Button("校准供能",()=>{if(q.ReflectorRotation!=a.ReflectorAnswer){Toast("光束没有对准接收面");return;}q.Nodes.Add("reflector");Toast("反射器供能已恢复");RequestSave();InteractMainline(p,id);}));
        }else if(id=="power_socket"){
            bool bridge=p==a.Site("vault").At(-22,5,-16);col.AddChild(Label(bridge?"修复高台电桥的六格缺口，再连接供能。可使用任意坚实方块。":"供能室线路被守卫占用，击败附近守卫后自动恢复。",17));
            if(bridge)col.AddChild(Button("连接电桥",()=>{if(!Mainline.BridgeReady(world)){Toast("电桥六格缺口尚未补齐");return;}q.Nodes.Add("bridge");Toast("电桥供能已恢复");RequestSave();InteractMainline(p,id);}));
            col.AddChild(Label(q.Nodes.Contains(bridge?"bridge":"guard")?"节点已恢复":"节点未恢复",16));
        }else if(id=="core_console"){
            col.AddChild(Label(q.Completed?"黎明之心已经稳定，三座灯塔重获光芒。":$"供能节点 {q.Nodes.Count}/3 · 灯塔 {world.State.Beacons.Count}/3\n启动后关闭四个锚点，再攻击巨像核心。重击后的核心更脆弱。\n门外可放床与箱子；营火不能阻止巨像攻击。",17));
            if(!q.Completed&&!q.FinaleActive)col.AddChild(Button("启动核心 · 挑战熔心巨像",()=>{if(!Mainline.StartFinale(world,out var error)){Toast(error);return;}var v=a.Site("vault");var def=creatureDefs.Single(d=>d.Id=="colossus");var state=new CreatureState{Kind="colossus",Home="vault",Health=def.Health,Position=[v.Center.X+.5,v.Floor+1,v.Center.Z-3.5]};world.State.Creatures.Add(state);SpawnCreature(state);SetScreen("play");Toast("断流：关闭四个供能锚点。高处锚点可以射箭。");RequestSave();}));
            if(q.FinaleActive)col.AddChild(Button("撤退并重置本场战斗",()=>{ResetMainlineBattle();SetScreen("play");Toast("本场战斗已重置；遗址供能与灯塔进度保留。");RequestSave();}));
        }
        col.AddChild(Button("主线日志",ShowMainlineJournal));col.AddChild(Button("返回游戏",()=>SetScreen("play")));return true;
    }
    private void TickMainline(double delta)
    {
        if(world?.Terrain.Adventure is not {} a)return;var q=world.State.Mainline;if(q.FinaleActive)q.BattleTime+=delta;mainlineClock-=delta;if(mainlineClock>0)return;mainlineClock=.25;
        foreach(var (cell,label) in mainlineLabels){label.Visible=player!.GlobalPosition.DistanceSquaredTo(new(cell.X,cell.Y,cell.Z))<55*55&&renderer!.IsTerrainReady(new(cell.X,cell.Y,cell.Z));if(a.Devices.GetValueOrDefault(cell)=="energy_anchor"){int index=Enumerable.Range(0,4).First(i=>a.Anchor(i)==cell);label.Text=q.Anchors.Contains(index)?"锚点已断流":"供能锚点 · Q / 射箭";label.Modulate=q.Anchors.Contains(index)?new("82f3cd"):new("ffcf7b");}}
        foreach(var vent in forgeVents){vent.Visible=world.State.Time%4>2.1&&vent.Position.DistanceSquaredTo(player!.GlobalPosition)<45*45&&renderer!.IsTerrainReady(vent.Position);vent.Scale=new(1,world.State.Time%4>2.8?1:.14f,1);}
        var p=Explorer.ToCell(player!.GlobalPosition);var forge=a.Site("forge");int x=p.X-forge.Center.X,z=p.Z-forge.Center.Z;
        if(a.Inside(p,"forge")&&Math.Abs(x)<=7&&Math.Abs(x)>=4&&z%6 is 0 or 1&&p.Y<forge.Floor+4&&world.State.Time%4>2.8){double heat=world.State.Equipment.Slots[1]?.HeatLining==true?1.5:6;Hurt(ArmorTraits.HeatDamage(world.State,heat));}
        if(q.FinaleActive&&!a.Inside(p,"vault",20)){ResetMainlineBattle();Toast("离开核心区域，本场挑战已重置。");RequestSave();}
    }
    private void ResetMainlineBattle()
    {
        if(world==null)return;Mainline.ResetFinale(world);foreach(var c in creatures.Where(c=>c.Definition.Id=="colossus").ToArray()){creatures.Remove(c);c.QueueFree();}projectiles?.Clear();
    }
    private void ShowWorkshopMaintenance(Action back)
    {
        if(world==null)return;SetScreen("maintenance");var col=Screen(overlay,"装备维护");var list=Column(Scroll(col,320));
        void Item(Inventory supply,int slot){if(supply.Slots[slot] is not {} item||catalog.Items[item.Id].Durability<=0)return;var def=catalog.Items[item.Id];var row=Row(list);row.AddChild(Label(def.Name+$" · {item.Durability??def.Durability}/{def.Durability}"+(item.HeatLining?" · 耐热":""),15));row.AddChild(Button("修理（修理包 ×1）",()=>{if(!MaintenanceAvailable()){Toast("需要附近的工作台或校准台");return;}if(!Mainline.RepairItem(world,supply,slot,out var error))Toast(error);ShowWorkshopMaintenance(back);}));if(item.Id=="iron_chestplate")row.AddChild(Button(item.HeatLining?"拆回耐热衬片":"安装耐热衬片",()=>{if(!MaintenanceAvailable()){Toast("制作设施不可用");return;}if(item.HeatLining){if(world.State.Inventory.Add("heat_lining",1,catalog)>0){Toast("背包空间不足");return;}item.HeatLining=false;}else{if(!world.State.Creative&&!world.State.Inventory.Remove("heat_lining",1)){Toast("缺少耐热衬片");return;}item.HeatLining=true;}ShowWorkshopMaintenance(back);}));}
        void Dye(Inventory inventory,int slot){if(inventory.Slots[slot] is {} item&&(item.Id.StartsWith("leather_")||item.Id=="wolf_armor")){var row=Row(list);row.AddChild(Label(catalog.Items[item.Id].Name+"染色",15));row.AddChild(Button("红色",()=>DyeArmor(inventory,slot,"red",back)));row.AddChild(Button("蓝色",()=>DyeArmor(inventory,slot,"blue",back)));}}
        for(int i=0;i<36;i++){Item(world.State.Inventory,i);Dye(world.State.Inventory,i);}for(int i=0;i<5;i++){Item(world.State.Equipment,i);Dye(world.State.Equipment,i);}col.AddChild(Button("返回制作设施",back));
    }
    private bool MaintenanceAvailable()=>world!=null&&currentContainer is {} p&&catalog.Id(world.Get(p)) is "workbench" or "calibration_bench"&&player!.GlobalPosition.DistanceSquaredTo(new(p.X+.5f,p.Y+.5f,p.Z+.5f))<=36;
}
