using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.Items;

namespace EmberLighthouse.World;

public sealed class MainlineProgress
{
    public HashSet<string> Proofs {get;set;}=[];
    public HashSet<string> Nodes {get;set;}=[];
    public HashSet<int> Anchors {get;set;}=[];
    public bool FinaleActive {get;set;}
    public bool Completed {get;set;}
    public double BattleTime {get;set;}
    public int ReflectorRotation {get;set;}
    public bool StrongHints {get;set;}
    public MainlineProgress Copy()=>new(){Proofs=new(Proofs),Nodes=new(Nodes),Anchors=new(Anchors),FinaleActive=FinaleActive,Completed=Completed,BattleTime=BattleTime,ReflectorRotation=ReflectorRotation,StrongHints=StrongHints};
    public void Validate()
    {
        if(Proofs==null||Nodes==null||Anchors==null||Proofs.Any(p=>p is not("mine" or "forge" or "snow" or "guardian" or "dawn" or "camp"))||Nodes.Any(p=>p is not("bridge" or "reflector" or "guard"))||Anchors.Any(i=>i<0||i>3)||ReflectorRotation<0||ReflectorRotation>3||!double.IsFinite(BattleTime)||BattleTime<0||Completed&&FinaleActive)throw new InvalidOperationException("三塔归火进度无效");
    }
}

public sealed record AdventureSite(string Id,string Name,Cell Center,int Floor,int Radius,int Height)
{
    public Cell At(int x,int y,int z)=>new(Center.X+x,Floor+y,Center.Z+z);
    public Cell Entry=>At(0,41-Floor,Radius+40-Floor+3);
}

public sealed class AdventureLayout
{
    public AdventureSite[] Sites {get;}
    public Cell[] Towers {get;}
    public Dictionary<Cell,string> Devices {get;}=[];
    public int ReflectorAnswer {get;}
    private readonly Dictionary<(int,int),AdventureSite> regions=[];
    public AdventureSite Site(string id)=>Sites.First(s=>s.Id==id);
    public AdventureLayout(int seed)
    {
        Cell Location(int x,int z,int salt)=>new(x+(int)(RandomField.Hash(seed,x,0,z,salt)%65)-32,0,z+(int)(RandomField.Hash(seed,z,0,x,salt+1)%65)-32);
        Towers=[Location(240,180,61),Location(-380,580,63),Location(740,-840,65)];
        Sites=[new("mine","废矿场",Towers[0]+new Cell(45,0,0),32,20,8),new("forge","旱地熔炉堡",Location(700,470,67),40,25,13),new("snow","雪岭观测所",Location(-680,-440,69),40,25,22),new("cave","深层晶穴",Towers[2]+new Cell(52,0,0),14,23,10),new("vault","地心遗址",Location(-1020,760,71),8,30,18)];
        foreach(var site in Sites)for(int x=Coordinates.FloorDiv(site.Center.X-site.Radius-3,16);x<=Coordinates.FloorDiv(site.Center.X+site.Radius+3,16);x++)for(int z=Coordinates.FloorDiv(site.Center.Z-site.Radius-3,16);z<=Coordinates.FloorDiv(site.Center.Z+site.Radius+43,16);z++)regions[(x,z)]=site;
        Devices[Site("mine").At(-12,1,12)]="quest_record";
        Devices[Site("forge").At(-16,1,16)]="quest_record";
        Devices[Site("snow").At(-16,1,16)]="quest_record";
        var v=Site("vault");Devices[v.At(-22,5,-16)]="power_socket";Devices[v.At(20,1,-16)]="reflector";Devices[v.At(18,1,18)]="power_socket";Devices[v.At(0,1,6)]="core_console";Devices[v.At(22,1,-16)]="quest_record";
        for(int i=0;i<4;i++)Devices[Anchor(i)]="energy_anchor";
        ReflectorAnswer=(int)(RandomField.Hash(seed,0,0,0,73)%4);
    }
    public Cell Anchor(int index)=>Site("vault").At(index%2==0?-12:12,index<2?1:9,index<2?-10:10);
    public bool Inside(Cell p,string id,int margin=0){var s=Site(id);return Math.Abs(p.X-s.Center.X)<=s.Radius+margin&&Math.Abs(p.Z-s.Center.Z)<=s.Radius+margin&&p.Y>=s.Floor-1&&p.Y<=s.Floor+s.Height+3;}
    public AdventureSite? Nearby(Cell p)=>regions.GetValueOrDefault((Coordinates.FloorDiv(p.X,16),Coordinates.FloorDiv(p.Z,16)));
    // The templates carve actual traversable rooms and one-block ramps. All
    // required rooms and access tunnels exist independently of random terrain.
    public string? BlockAt(Cell p)
    {
        if(Devices.TryGetValue(p,out var device))return device;
        var s=Nearby(p);if(s==null)return null;int x=p.X-s.Center.X,z=p.Z-s.Center.Z,y=p.Y-s.Floor,r=s.Radius;
        if(Math.Abs(x)<=2&&z>=r&&z<=r+40-s.Floor+3){int floor=s.Floor+Math.Min(z-r,40-s.Floor);if(p.Y==floor)return "ruin_brick";if(p.Y>floor&&p.Y<=Math.Max(44,s.Floor+s.Height))return "air";}
        if(Math.Abs(x)>r||Math.Abs(z)>r||y<0||y>s.Height+1)return null;
        if(y==0)return s.Id=="forge"?"basalt":"ruin_brick";
        if(y==s.Height+1)return s.Id is "mine" or "cave" or "vault"?"ruin_brick":"air";
        if(Math.Abs(x)==r||Math.Abs(z)==r){if(z==r&&Math.Abs(x)<=2)return "air";return y<4||s.Id is "mine" or "cave" or "vault"?"ruin_brick":"air";}
        if(x==r-4&&z==r-4&&y==1)return "chest";
        if(y==4&&Math.Abs(x)==r-3&&z%8==0)return "lantern";
        switch(s.Id){
            case "mine":
                if(z==2&&y<=3&&Math.Abs(x)>3)return "cobble";
                if(z==-6&&y<=4&&x>=-4&&x<=4)return "gravel";
                if(Math.Abs(x)==15&&y<=5&&z%8==0)return "wood";
                if(y==1&&x<-9&&z<-6)return (x+z)%3==0?"copper_vein":"iron_ore";
                break;
            case "forge":
                if(y==1&&Math.Abs(x)==6&&z%6==0)return "lantern";
                if(Math.Abs(x)==10&&y<=3&&z<12&&z>-12&&z!=0)return "basalt";
                if(z==-18&&y<=3&&Math.Abs(x)<15)return "copper_vein";
                if(y==5&&z==12&&Math.Abs(x)>=7)return "plank";
                break;
            case "snow":
                // An ascending mountain gallery with recoverable lower floor.
                if(x>=-3&&x<=3&&z>=-12&&z<=12){int step=Math.Min(16,12-z);if(y==step)return "ruin_brick";}
                if(y==16&&z<=-12&&Math.Abs(x)<=13)return "ruin_brick";
                if(y==16&&z>=-7&&z<=7&&Math.Abs(x)>=8&&Math.Abs(x)<=12)return "plank";
                if(y==1&&Math.Abs(x)>20)return "snow";
                break;
            case "cave":
                if(Math.Abs(x)==15&&z%7==0&&y<6)return "basalt";
                if(y==1&&Math.Abs(x)>=18&&z<0)return (x+z)%2==0?"crystal_ore":"coal_ore";
                break;
            case "vault":
                if(z==-16&&x>=-28&&x<=-16){if(y==4&&(x<-22||x>-17))return "ruin_brick";if(x==-27&&y<=4)return "ruin_brick";}
                if(x==-27&&z>=-12&&z<=-8&&y==-8-z)return "ruin_brick";
                for(int i=2;i<4;i++){var a=Anchor(i);int ax=a.X-s.Center.X,az=a.Z-s.Center.Z;if(x==ax&&z==az&&y<9)return "pillar";if(z==az-2&&x>=ax-8&&x<=ax&&y==x-ax+8)return "ruin_brick";if(x==ax&&z==az-1&&y==8)return "ruin_brick";}
                if(Math.Abs(x)==22&&Math.Abs(z)<=10&&y<=4&&Math.Abs(z)>2)return "ruin_brick";
                if(x==24&&z>=-22&&z<=-12&&y==1)return "glass";
                break;
        }
        return "air";
    }
}

public static class Mainline
{
    public static bool Enabled(WorldSnapshot s)=>s.GeneratorVersion>=3;
    public static string TowerName(int index)=>new[]{"寻路塔","校准塔","镇封塔"}[index];
    public static bool RecipeKnown(WorldSnapshot s,Recipe r)
    {
        if(!s.Creative&&r.Id=="elytra")return Enabled(s)?s.Mainline.Completed:s.GuardianDefeated&&s.Beacons.Count==3;
        if(!s.Creative&&r.Station=="calibration_bench")return Enabled(s)&&s.Beacons.Contains(1);
        if(!Enabled(s))return r.Id is not("beacon_sigil" or "copper_coil" or "survey_lens" or "heat_lining" or "calibration_bench" or "ember_alloy" or "ember_sword" or "ember_bow" or "repair_kit" or "dawn_heart");
        if(s.Creative)return true;
        return r.Id switch{"survey_lens"=>s.Beacons.Contains(0),"calibration_bench" or "ember_alloy" or "ember_sword" or "ember_bow" or "beacon_sigil"=>s.Beacons.Contains(1),"dawn_heart"=>s.Mainline.Completed,"beacon_sigil_legacy"=>false,_=>true};
    }
    public static Dictionary<string,int> RepairCost(int index){var cost=new Dictionary<string,int>{{"copper_coil",index==0?1:2},{"iron_ingot",4}};if(index==2)cost.Add("beacon_sigil",1);return cost;}
    public static bool Repair(WorldState w,int index,out string error)
    {
        var s=w.State;var q=s.Mainline;error="";
        if(index<0||index>2){error="灯塔编号无效";return false;}if(s.Beacons.Contains(index)){error="这座灯塔已修复";return false;}
        if(!s.Creative){
            if(index==0&&!q.Proofs.Contains("mine")){error="先击败废矿场的矿场哨长";return false;}
            if(index==1&&(!s.Beacons.Contains(0)||!q.Proofs.Contains("forge")||!q.Proofs.Contains("snow"))){error="需要寻路塔与炉心、霜辉两枚棱镜";return false;}
            if(index==2&&(!s.Beacons.Contains(1)||!q.Proofs.Contains("guardian"))){error="需要校准塔与守护者的共鸣晶核";return false;}
            var inventory=s.Inventory.Copy();foreach(var cost in RepairCost(index))if(!inventory.Remove(cost.Key,cost.Value)){error="缺少 "+w.Catalog.Items[cost.Key].Name+" ×"+cost.Value;return false;}s.Inventory=inventory;
        }
        s.Beacons.Add(index);return true;
    }
    public static bool RecordVictory(WorldSnapshot s,string kind,string home="")
    {
        if(!Enabled(s))return false;
        string? proof=kind switch{"mine_warden"=>"mine","furnace_warden"=>"forge","observatory_warden"=>"snow","guardian"=>"guardian","colossus"=>"dawn",_=>null};
        if(home=="vault_guard")return s.Mainline.Nodes.Add("guard");
        if(proof==null)return false;bool first=s.Mainline.Proofs.Add(proof);
        if(kind=="guardian")s.GuardianDefeated=true;
        if(kind=="colossus"){s.Mainline.Completed=true;s.Mainline.FinaleActive=false;}return first;
    }
    public static bool StartFinale(WorldState w,out string error)
    {
        var q=w.State.Mainline;error="";if(q.Completed){error="黎明之心已经稳定";return false;}if(q.FinaleActive){error="核心战正在进行";return false;}
        if(!w.State.Creative&&(w.State.Beacons.Count!=3||q.Nodes.Count!=3)){error="需要三座灯塔与电桥、反射器、供能室三个节点";return false;}
        q.FinaleActive=true;q.BattleTime=0;q.Anchors.Clear();return true;
    }
    public static bool CloseAnchor(WorldState w,Cell p)
    {
        if(!Enabled(w.State)||!w.State.Mainline.FinaleActive)return false;
        for(int i=0;i<4;i++)if(w.Terrain.Adventure!.Anchor(i)==p)return w.State.Mainline.Anchors.Add(i);return false;
    }
    public static bool BridgeReady(WorldState w)
    {
        var s=w.Terrain.Adventure!.Site("vault");return Enumerable.Range(-22,6).All(x=>w.Solid(s.At(x,4,-16)));
    }
    public static void ResetFinale(WorldState w)
    {
        var q=w.State.Mainline;q.FinaleActive=false;q.BattleTime=0;q.Anchors.Clear();w.State.Creatures.RemoveAll(c=>c.Kind=="colossus");w.State.Projectiles.RemoveAll(p=>p.Hostile);
    }
    public static string Objective(WorldSnapshot s)
    {
        var q=s.Mainline;
        if(q.Completed)return "三塔归火已完成\n继续探索与建造；校准台可制作黎明之心纪念灯。";
        if(q.FinaleActive)return q.Anchors.Count<4?$"断流：关闭供能锚点 {q.Anchors.Count}/4\n高台锚点可以射箭或搭阶梯。":"攻击熔心巨像暴露的核心；避开重击圈。";
        if(!s.Beacons.Contains(0))return q.Proofs.Contains("mine")?"修复寻路塔\n铜线圈 ×1、铁锭 ×4。":"建立补给营地，探索废矿场并击败矿场哨长。\nJ 查看主线日志与路线提示。";
        if(!q.Proofs.Contains("forge")||!q.Proofs.Contains("snow"))return $"双域远征（顺序自选）\n炉心棱镜 {(q.Proofs.Contains("forge")?"✓":"未取得")} · 霜辉棱镜 {(q.Proofs.Contains("snow")?"✓":"未取得")}\n勘测镜右键定位；J 查看准备建议。";
        if(!s.Beacons.Contains(1))return "修复校准塔\n安装两枚棱镜；铜线圈 ×2、铁锭 ×4。";
        if(!q.Proofs.Contains("guardian"))return "深入晶穴挑战余烬守护者\n校准台可制作余烬装备；准备盾牌与熟食。";
        if(!s.Beacons.Contains(2))return "修复镇封塔\n共鸣晶核、灯塔符印 ×1、铜线圈 ×2、铁锭 ×4。";
        return $"探索地心遗址：供能节点 {q.Nodes.Count}/3\n修复电桥、校准反射器、击败供能室守卫，然后启动核心。";
    }
    public static bool RepairItem(WorldState w,Inventory supply,int slot,out string error)
    {
        error="";if(slot<0||slot>=supply.Slots.Length||supply.Slots[slot] is not {} item||w.Catalog.Items[item.Id].Durability<=0){error="请选择有耐久的工具或装备";return false;}
        int max=w.Catalog.Items[item.Id].Durability;if((item.Durability??max)>=max){error="物品不需要修理";return false;}
        if(!w.State.Creative&&!w.State.Inventory.Remove("repair_kit",1)){error="缺少修理包";return false;}item.Durability=Math.Min(max,(item.Durability??max)+(int)Math.Ceiling(max*.4));return true;
    }
}
