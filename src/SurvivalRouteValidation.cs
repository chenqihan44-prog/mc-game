using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool routeCheck;
    private readonly Stopwatch routeWatch=new();
    private readonly List<object> routeMilestones=[];
    private readonly List<object> routeNavigationStarts=[];
    private readonly List<object> routeSearchRecords=[];
    private readonly List<object> routeDamageEvents=[];
    private double routeDamageTotal;
    private bool routeReturning;
    private string routeAction="start";
    private int routeMined,routePlaced;
    private double routeMeters;
    private sealed class RouteBodyObstacle(string message):Exception(message);
    private async Task RouteWait(Func<bool> done,double timeout)
    {
        double until=routeWatch.Elapsed.TotalSeconds+timeout;
        while(!done()){
            if(screen=="death")throw new InvalidOperationException("路线中死亡："+routeAction);
            if(routeWatch.Elapsed.TotalSeconds>until)throw new InvalidOperationException("路线超时："+routeAction+"，位置 "+player?.GlobalPosition);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
    }
    private void RouteMark(string stage)
    {
        routeMilestones.Add(new{stage,seconds=routeWatch.Elapsed.TotalSeconds,health=world!.State.Health,hunger=world.State.Hunger,position=world.State.Position.ToArray()});
        GD.Print("ROUTE_STAGE: "+stage+" "+routeWatch.Elapsed.TotalSeconds.ToString("0.0")+"s");
    }
    private async Task RouteWalk(Vector3 destination)
    {
        routeAction="步行至 "+destination;var start=player!.GlobalPosition;
        double started=routeWatch.Elapsed.TotalSeconds,until=started+20,nextJump=0;
        try{
            while(new Vector2(player.GlobalPosition.X-destination.X,player.GlobalPosition.Z-destination.Z).Length()>.16f||Math.Abs(player.GlobalPosition.Y-destination.Y)>.3f||!player.IsOnFloor()||!world!.Solid(Explorer.ToCell(player.GlobalPosition+Vector3.Down*.05f))){
                if(screen=="death")throw new InvalidOperationException("步行中死亡");
                if(routeWatch.Elapsed.TotalSeconds-started>.25)for(int i=0;i<player.GetSlideCollisionCount();i++){
                    var hit=player.GetSlideCollision(i);
                    if(hit.GetCollider() is EmberLighthouse.Creatures.Creature c&&Math.Abs(hit.GetNormal().Y)<.5f)throw new RouteBodyObstacle("步行遇到移动生物："+c.Definition.Id);
                }
                if(routeWatch.Elapsed.TotalSeconds>until){string contacts=string.Join(",",Enumerable.Range(0,player.GetSlideCollisionCount()).Select(i=>{var hit=player.GetSlideCollision(i);return (hit.GetCollider() is Node n?n.Name.ToString():hit.GetCollider()?.GetClass())+":"+hit.GetNormal();}));throw new InvalidOperationException(routeAction+"受阻，当前位置 "+player.GlobalPosition+"，接触 "+contacts);}
                var flat=new Vector3(destination.X,player.GlobalPosition.Y,destination.Z);
                if(player.GlobalPosition.DistanceTo(flat)>.01f)player.LookAt(flat,Vector3.Up);
                Input.ActionPress("forward");if(routeReturning)Input.ActionPress("sprint");
                if(destination.Y-player.GlobalPosition.Y>.03f&&player.IsOnFloor()&&routeWatch.Elapsed.TotalSeconds>=nextJump){Input.ActionPress("jump");nextJump=routeWatch.Elapsed.TotalSeconds+.5;}else Input.ActionRelease("jump");
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            }
        }finally{Input.ActionRelease("forward");Input.ActionRelease("jump");Input.ActionRelease("sprint");routeMeters+=start.DistanceTo(player.GlobalPosition);}
    }
    private void RouteAim(Cell cell,bool ground=false)
    {
        float height=ground?-.005f:world!.Terrain.IsLooseWood(cell)&&world.Get(cell)==catalog.Number("wood")&&!world.Changes.ContainsKey(cell)?.15f:.5f;
        player!.Camera.LookAt(new Vector3(cell.X+.5f,cell.Y+height,cell.Z+.5f),Vector3.Up);
    }
    private void RouteSelect(string? id)
    {
        var inventory=world!.State.Inventory;
        int index=Array.FindIndex(inventory.Slots,s=>id==null?s==null:s?.Id==id);
        if(index<0)throw new InvalidOperationException("路线缺少物品："+id);
        if(index>=9){if(!Inventory.Move(inventory,index,inventory,8,64,catalog))throw new InvalidOperationException("无法移到快捷栏");index=8;}
        selected=index;
    }
    private async Task RouteMine(Cell cell)
    {
        if(world!.Get(cell)==0)return;
        routeAction="采掘 "+cell+" "+catalog.Id(world.Get(cell));RouteAim(cell);
        for(int obstruction=0;player!.Target()?.Hit!=cell&&obstruction<8;obstruction++){
            var hit=player.Target()?.Hit;
            int tier=Held()?.Use is "pick" or "axe" or "shovel"?Held()!.Tier:0;
            if(hit is not Cell blocker||blocker.Y<Math.Floor(player.GlobalPosition.Y)||catalog.Block(world.Get(blocker)) is not { } block||block.Tier>tier||block.Id is "bedrock" or "beacon")break;
            await RouteMine(blocker);RouteAim(cell);
        }
        if(player!.Target()?.Hit!=cell)throw new InvalidOperationException("目标被挡住："+routeAction+"，准星命中 "+player.Target()?.Hit);
        try{Input.ActionPress("mine");await RouteWait(()=>world.Get(cell)==0,8);routeMined++;}
        finally{Input.ActionRelease("mine");}
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
    private async Task RoutePlace(Cell cell,string id)
    {
        routeAction="放置 "+id+" "+cell;RouteSelect(id);RouteAim(cell,true);
        if(player!.Target()?.Before!=cell)throw new InvalidOperationException("无法瞄准放置位置："+routeAction+"，实际 "+player.Target()?.Before);
        Use();if(world!.Get(cell)!=catalog.Number(id))throw new InvalidOperationException("放置失败："+message);routePlaced++;
        await RouteWait(()=>renderer!.IsTerrainReady(player.GlobalPosition),10);
    }
    private async Task RouteCraft(string id,Cell? facility=null)
    {
        routeAction="制作 "+id;Input.ActionRelease("mine");Input.ActionRelease("forward");
        var recipe=catalog.Recipes[id];int before=world!.State.Inventory.Count(recipe.Output);
        if(facility is Cell cell){RouteAim(cell);Interact();if(currentContainer!=cell||currentStation!=recipe.Station)throw new InvalidOperationException("无法使用制作设施："+id);}
        else{currentContainer=null;currentStation="hand";ShowRecipes();}
        Craft(recipe);
        if(recipe.Seconds==0&&world.State.Inventory.Count(recipe.Output)!=before+recipe.Count)throw new InvalidOperationException("制作没有产出："+id+" "+message);
        if(recipe.Seconds>0&&world.State.Entities[facility!.Value.ToString()].RecipeId!=recipe.Id)throw new InvalidOperationException("计时制作没有开始："+id+" "+message);
        currentContainer=null;SetScreen("play");
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
    private async Task RouteTake(Cell cell,string id)
    {
        routeAction="取出 "+id;RouteAim(cell);Interact();
        if(currentContainer!=cell)throw new InvalidOperationException("无法打开设施容器");
        ShowInventory();var storage=ContainerInventory()!;int source=Array.FindIndex(storage.Slots,s=>s?.Id==id);
        int target=Array.FindIndex(world!.State.Inventory.Slots,s=>s?.Id==id&&s.Count<catalog.Items[id].Stack);
        if(target<0)target=Array.FindIndex(world.State.Inventory.Slots,s=>s==null);
        if(source<0||target<0)throw new InvalidOperationException("无法转移设施产物");
        int before=world.State.Inventory.Count(id);TransferSlots(1,source,0,target);
        if(world.State.Inventory.Count(id)<=before)throw new InvalidOperationException("设施取物失败");
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        currentContainer=null;SetScreen("play");
    }
    private async Task RouteOre()
    {
        var center=Explorer.ToCell(player!.GlobalPosition);
        for(int pass=0;pass<12;pass++){
            Cell? candidate=null;
            for(int dy=0;dy<=3&&!candidate.HasValue;dy++)for(int x=-4;x<=4&&!candidate.HasValue;x++)for(int z=-4;z<=4&&!candidate.HasValue;z++){
                var cell=center+new Cell(x,dy,z);string id=catalog.Id(world!.Get(cell));
                if(id=="iron_ore"&&world.State.Inventory.Count("raw_iron")<4||id=="coal_ore"&&world.State.Inventory.Count("coal")<2){
                    RouteAim(cell);if(player.Target()?.Hit==cell)candidate=cell;
                }
            }
            if(candidate is not Cell ore)break;await RouteMine(ore);
        }
    }
    private async Task RouteNavigate(Cell goal,int replans=0)
    {
        var arrival=player!.GlobalPosition;
        routeNavigationStarts.Add(new{goal=goal.ToString(),position=new[]{arrival.X,arrival.Y,arrival.Z},onFloor=player.IsOnFloor(),groundSupport=world!.Solid(Explorer.ToCell(arrival+Vector3.Down*.05f))});
        // MoveAndSlide's previous floor flag can survive a respawn while terrain is loading.
        // Wait for actual support at the new location before constructing a walking path.
        await RouteWait(()=>renderer!.IsTerrainReady(player.GlobalPosition)&&player.IsOnFloor()&&world.Solid(Explorer.ToCell(player.GlobalPosition+Vector3.Down*.05f)),15);
        if(world!.State.Inventory.Count("iron_pick")>0)RouteSelect("iron_pick");
        int tier=Held()?.Use is "pick" or "axe" or "shovel"?Held()!.Tier:0;
        var feet=player!.GlobalPosition;var start=new Cell((int)Math.Floor(feet.X),(int)Math.Round(feet.Y),(int)Math.Floor(feet.Z));
        var frontier=new PriorityQueue<Cell,int>();var cost=new Dictionary<Cell,int>{{start,0}};var previous=new Dictionary<Cell,Cell>();frontier.Enqueue(start,0);
        bool CanDig(Cell p)=>catalog.Block(world.Get(p)) is { } block&&block.Tier<=tier&&block.Id is not ("bedrock" or "beacon" or "chest")&&(!world.State.Entities.TryGetValue(p.ToString(),out var entity)||entity.Storage.Slots.All(s=>s==null));
        int Clearance(Cell p){int blocks=0;foreach(var body in new[]{p,p+new Cell(0,1,0)})if(world.Solid(body)){if(!CanDig(body))return -1;blocks++;}return blocks;}
        int ThreatCost(Cell p){
            int risk=0;
            foreach(var c in creatures.Where(c=>c.IsThreat)){
                double range=c.Definition.Range+4,dx=p.X-c.GlobalPosition.X,dz=p.Z-c.GlobalPosition.Z;
                if(dx*dx+dz*dz<range*range)risk+=120;
            }
            // The automated navigator knows terrain; avoid crossing other ruins
            // where sentinels can appear during the walk, while allowing the target ruin.
            int rx=Coordinates.FloorDiv(p.X,96)*96+32,rz=Coordinates.FloorDiv(p.Z,96)*96+24;
            if(Math.Abs(goal.X-rx)>10||Math.Abs(goal.Z-rz)>10){
                if(Math.Abs(p.X-rx)<23&&Math.Abs(p.Z-rz)<23&&world.Terrain.Surface(rx,rz)>Terrain.SeaLevel)risk+=120;
            }
            return risk;
        }
        string captureDirectory=ProjectSettings.GlobalizePath("res://test-results/route/planning/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(captureDirectory);
        File.WriteAllText(Path.Combine(captureDirectory,"world.json"),JsonSerializer.Serialize(world.State.Copy(),Catalog.JsonOptions));
        var capturedCreatures=creatures.Select(c=>new{kind=c.Definition.Id,health=c.State.Health,position=new[]{c.GlobalPosition.X,c.GlobalPosition.Y,c.GlobalPosition.Z},threat=c.IsThreat,range=c.Definition.Range}).ToArray();
        var occupiedGoal=capturedCreatures.Where(c=>c.health>0&&Math.Abs(c.position[1]-goal.Y)<2&&Math.Pow(c.position[0]-goal.X-.5,2)+Math.Pow(c.position[2]-goal.Z-.5,2)<2.25).ToArray();
        bool found=false;int examined=0,rejectedCreatures=0,rejectedClearance=0;
        while(frontier.TryDequeue(out var node,out _)&&examined++<30000){
            if(node==goal){found=true;break;}
            foreach(var offset in new[]{new Cell(1,0,0),new Cell(-1,0,0),new Cell(0,0,1),new Cell(0,0,-1)})foreach(int dy in new[]{0,1,-1}){
                var next=node+offset+new Cell(0,dy,0);
                if(next.X<Math.Min(start.X,goal.X)-20||next.X>Math.Max(start.X,goal.X)+20||next.Z<Math.Min(start.Z,goal.Z)-20||next.Z>Math.Max(start.Z,goal.Z)+20||next.Y<Math.Min(start.Y,goal.Y)-4||next.Y>Math.Max(start.Y,goal.Y)+4||!Coordinates.InWorld(next)||next.Y<=0||!world.Solid(next+new Cell(0,-1,0)))continue;
                if(creatures.Any(c=>c.State.Health>0&&Math.Abs(c.GlobalPosition.Y-next.Y)<2&&Math.Pow(c.GlobalPosition.X-next.X-.5,2)+Math.Pow(c.GlobalPosition.Z-next.Z-.5,2)<2.25)){rejectedCreatures++;continue;}
                int blocks=Clearance(next);if(blocks<0){rejectedClearance++;continue;}
                if(dy>0&&world.Solid(next+new Cell(0,2,0))){if(!CanDig(next+new Cell(0,2,0)))continue;blocks++;}
                int score=cost[node]+10+Math.Abs(dy)*2+blocks*60+ThreatCost(next);if(cost.TryGetValue(next,out int old)&&score>=old)continue;
                cost[next]=score;previous[next]=node;int heuristic=(Math.Abs(next.X-goal.X)+Math.Abs(next.Z-goal.Z)+Math.Abs(next.Y-goal.Y))*11;frontier.Enqueue(next,score+heuristic);
            }
        }
        var searchRecord=new{found,examined,discovered=cost.Count,queued=frontier.Count,rejectedCreatures,rejectedClearance,start=start.ToString(),goal=goal.ToString(),tool=Held()?.Id,tier,selected,replans,worldTime=world.State.Time,occupiedGoal,creatures=capturedCreatures,goalSupport=world.Solid(goal+new Cell(0,-1,0)),goalClearance=Clearance(goal),goalVisited=cost.ContainsKey(goal),captureDirectory,stoppedAfterPlanning=OS.GetCmdlineUserArgs().Contains("--route-stop-after-planning")};
        File.WriteAllText(Path.Combine(captureDirectory,"search.json"),JsonSerializer.Serialize(searchRecord,Catalog.JsonOptions));routeSearchRecords.Add(searchRecord);
        if(!found)throw new InvalidOperationException("实际地形没有找到步行路线："+start+" → "+goal+"，搜索 "+examined+" 节点，快照 "+captureDirectory);
        if(OS.GetCmdlineUserArgs().Contains("--route-stop-after-planning"))return;
        var path=new List<Cell>();for(var p=goal;p!=start;p=previous[p])path.Add(p);path.Reverse();
        foreach(var cell in path){
            if(cell.Y>player.GlobalPosition.Y+.03f){var overhead=Explorer.ToCell(player.GlobalPosition)+new Cell(0,2,0);if(world.Solid(overhead))await RouteMine(overhead);var landingHead=cell+new Cell(0,2,0);if(world.Solid(landingHead))await RouteMine(landingHead);}
            for(int y=cell.Y+1;y>=cell.Y;y--){var body=new Cell(cell.X,y,cell.Z);if(world.Solid(body))await RouteMine(body);}
            try{await RouteWalk(new(cell.X+.5f,cell.Y,cell.Z+.5f));}
            catch(RouteBodyObstacle)when(replans<12){await RouteNavigate(goal,replans+1);return;}
        }
    }
    private Cell RouteWater()
    {
        for(int radius=16;radius<=96;radius++)for(int a=-radius;a<=radius;a++)foreach(var pair in new[]{(a,radius),(a,-radius),(radius,a),(-radius,a)}){
            int h=world!.Terrain.Surface(pair.Item1,pair.Item2);var water=new Cell(pair.Item1,h+1,pair.Item2);
            if(h<=33&&world.Get(water)==catalog.Number("water")&&world.Solid(water+new Cell(0,-1,0)))return water;
        }
        throw new InvalidOperationException("种子42未找到可达的自然浅水死亡测试点");
    }
    private static bool RouteSameItems(Inventory a,Inventory b)=>a.Slots.Where(s=>s!=null).Select(s=>s!.Id).Concat(b.Slots.Where(s=>s!=null).Select(s=>s!.Id)).Distinct().All(id=>a.Count(id)==b.Count(id));
    private async void RunSurvivalRouteCheck()
    {
        string id="";
        try{
            routeWatch.Start();StartWorld(new(){Name="生存路线自动验证",Seed=42});id=world!.State.WorldId;
            if(world.State.Creative||world.State.Casual||world.State.Inventory.Slots.Any(s=>s!=null))throw new InvalidOperationException("路线没有从空背包标准生存开始");
            await RouteWait(()=>renderer!.IsTerrainReady(player!.GlobalPosition)&&player.IsOnFloor(),15);RouteMark("新生存世界就绪");RouteSelect(null);
            for(int x=2;x<=7;x++){await RouteWalk(new(x+.5f,37,2.5f));await RouteMine(new(x,37,4));}
            for(int x=-2;x>=-7;x--){await RouteWalk(new(x+.5f,37,2.5f));await RouteMine(new(x,37,4));}
            RouteMark("徒手取得至少6木材和6碎石");
            await RouteCraft("plank");await RouteCraft("plank");await RouteCraft("stick");await RouteCraft("stone_pick");await RouteCraft("workbench");await RouteCraft("campfire");RouteMark("石镐、工作台与营火制作");
            await RouteWalk(new(0.5f,37,.5f));var bench=new Cell(3,37,2);var camp=new Cell(5,37,2);await RoutePlace(bench,"workbench");await RoutePlace(camp,"campfire");RouteMark("营地设施放置");
            RouteSelect("stone_pick");await RouteWalk(new(8.5f,37,.5f));await RouteWalk(new(13.5f,37,10.5f));
            var path=new List<Vector3>{player!.GlobalPosition};
            for(int step=1;step<=17;step++){
                int x=13+step,y=37-step;
                for(int clear=(int)Math.Ceiling(player.GlobalPosition.Y)+1;clear>=y;clear--)await RouteMine(new(x,clear,10));
                await RouteWalk(new(x+.5f,y,10.5f));path.Add(player.GlobalPosition);await RouteOre();
                if(world.State.Inventory.Count("stone")>=8&&world.State.Inventory.Count("raw_iron")>=4&&world.State.Inventory.Count("coal")>=2)break;
            }
            if(world.State.Inventory.Count("stone")<8||world.State.Inventory.Count("raw_iron")<4||world.State.Inventory.Count("coal")<2)throw new InvalidOperationException("实际矿道未取得所需矿物");RouteMark("阶梯矿道采集石料、4粗铁与2煤");
            for(int index=path.Count-2;index>=0;index--)await RouteWalk(path[index]);
            await RouteWalk(new(8.5f,37,.5f));await RouteWalk(new(.5f,37,.5f));await RouteCraft("furnace",bench);
            var furnace=new Cell(7,37,2);await RouteWalk(new(2.5f,37,.5f));await RoutePlace(furnace,"furnace");
            for(int batch=0;batch<2;batch++){await RouteCraft("iron_ingot",furnace);routeAction="等待真实8秒冶炼";await RouteWait(()=>world.State.Entities[furnace.ToString()].RecipeId==null,12);await RouteTake(furnace,"iron_ingot");}
            await RouteCraft("iron_pick",bench);RouteMark("真实冶炼与铁镐升级");
            var cache=new Cell(34,38,26);await RouteNavigate(new(34,38,25));
            if(OS.GetCmdlineUserArgs().Contains("--route-stop-after-planning")){WriteRouteRecord(true,"");GD.Print("ROUTE_PLANNING_PASS: first walking path planned and captured; full route not executed");QuitCleanly();return;}
            await RouteTake(cache,"grain");await RouteTake(cache,"carrot");
            if(!world.State.Discovered.Contains("ruins")||world.State.Inventory.Count("grain")!=3||world.State.Inventory.Count("carrot")!=3)throw new InvalidOperationException("实际遗迹奖励不一致");RouteMark("步行探索废营地并取出遗迹补给");
            var water=RouteWater();await RouteNavigate(water);routeAction="自然水体受伤";Input.ActionPress("crouch");await RouteWait(()=>world.State.Air<=0&&world.State.Health<90,35);double injured=world.State.Health;
            Input.ActionRelease("crouch");await RouteWait(()=>!player!.Crouching&&world.State.Air>=14,8);RouteSelect("carrot");Use();
            if(world.State.Inventory.Count("carrot")!=2)throw new InvalidOperationException("正常进食没有消耗一份食物");await RouteWait(()=>world.State.Health>=injured+2,10);RouteMark("自然溺水受伤、进食与恢复");
            var carried=world.State.Inventory.Copy();routeAction="标准模式水下真实伤害死亡";Input.ActionPress("crouch");await RouteWait(()=>screen=="death",60);Input.ActionRelease("crouch");
            var deathBag=world.State.Graves.SingleOrDefault(g=>RouteSameItems(g.Items,carried));
            if(deathBag==null||world.State.Inventory.Slots.Any(s=>s!=null))throw new InvalidOperationException("标准死亡没有完整留下背包");RouteMark("真实死亡并留下完整背包");
            SetScreen("play");await RouteWait(()=>renderer!.IsTerrainReady(player!.GlobalPosition)&&player.IsOnFloor(),15);
            // Return directly rather than requiring a second visit to the guarded ruin.
            // The planner only clears blocks that can be mined with the currently held tool.
            routeReturning=true;routeAction="复活后步行返回死亡背包";await RouteNavigate(water);
            var recovered=world.State.Inventory.Copy();
            foreach(var stack in carried.Slots.Where(s=>s!=null))if(recovered.Add(stack!.Id,stack.Count,catalog)>0)throw new InvalidOperationException("返回途中采集后没有空间取回完整背包");
            Interact();routeReturning=false;
            if(world.State.Graves.Contains(deathBag)||!RouteSameItems(world.State.Inventory,recovered))throw new InvalidOperationException("找回死亡背包没有守恒");RouteMark("正常复活、步行返回与Q找回背包");
            await RouteNavigate(new(2,37,0));
            RouteMark("浅水正常跳跃上岸并步行返回营地");
            var expected=world.State.Copy();routeAction="保存退出重读";bool? saved=null;SetScreen("pause");RequestSave(success=>saved=success);await RouteWait(()=>saved.HasValue,30);if(saved!=true)throw new InvalidOperationException("路线保存失败");
            ReturnMenu();await RouteWait(()=>world==null&&screen=="menu",30);WorldSnapshot? reloaded=null;
            RunWorldIo("读取路线存档",()=>saves.Load(id).Snapshot,snapshot=>reloaded=snapshot,()=>throw new InvalidOperationException("路线读取失败"));await RouteWait(()=>reloaded!=null,30);
            StartWorld(reloaded!);await RouteWait(()=>renderer!.IsTerrainReady(player!.GlobalPosition)&&player.IsOnFloor(),15);
            if(world!.State.Creative||!RouteSameItems(world.State.Inventory,expected.Inventory)||!world.State.Discovered.SetEquals(expected.Discovered)||world.State.Inventory.Count("iron_pick")!=1||world.State.Inventory.Count("iron_ingot")!=1||world.State.Changes.Count!=expected.Changes.Count||expected.Changes.Any(p=>world.State.Changes.GetValueOrDefault(p.Key)!=p.Value)||world.State.Entities[bench.ToString()].Kind!="workbench"||world.State.Entities[camp.ToString()].Kind!="campfire"||world.State.Entities[furnace.ToString()].Storage.Count("iron_ingot")!=0)throw new InvalidOperationException("路线保存重读不一致");
            RouteMark("保存重进核对地形、设施与物品");WriteRouteRecord(true,"");GD.Print("ROUTE_PASS: empty survival inventory, physical gather/tool/camp/mine/smelt/upgrade, ruins, natural injury/recovery/death, respawn/walk/bag retrieval and save/reload");QuitCleanly();
        }catch(Exception error){WriteRouteRecord(false,error.Message);GD.PushError("ROUTE_FAILED: "+error);QuitCleanly(9);}
        finally{foreach(string action in new[]{"mine","forward","jump","crouch","sprint"})Input.ActionRelease(action);}
    }
    private void RecordRouteDamage(double damage)
    {
        if(!routeCheck||world==null||player==null)return;
        routeDamageTotal+=damage;
        if(damage<1&&world.State.Health>damage)return;
        var caller=new StackTrace().GetFrame(2)?.GetMethod();
        var p=player.GlobalPosition;
        routeDamageEvents.Add(new{seconds=routeWatch.Elapsed.TotalSeconds,damage,healthBefore=world.State.Health,air=world.State.Air,position=new[]{p.X,p.Y,p.Z},source=caller?.DeclaringType?.Name+"."+caller?.Name,action=routeAction});
        if(routeDamageEvents.Count>32)routeDamageEvents.RemoveAt(0);
    }
    private void WriteRouteRecord(bool passed,string error)
    {
        string directory=ProjectSettings.GlobalizePath("res://test-results/route");Directory.CreateDirectory(directory);
        bool stoppedAfterPlanning=OS.GetCmdlineUserArgs().Contains("--route-stop-after-planning");
        File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{passed,error,worldId=world?.State.WorldId,seconds=routeWatch.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",seed=42,emptyStart=true,creative=false,testTeleports=false,grantedItems=false,clockAcceleration=false,actualWalkMeters=routeMeters,mined=routeMined,placed=routePlaced,action=routeAction,coverage=stoppedAfterPlanning?"gather-tool-camp-mine-smelt-upgrade-first-path-planning":"gather-tool-camp-mine-smelt-upgrade-ruins-injury-recovery-death-bag-save-reload",nativeS2Route=!stoppedAfterPlanning,stoppedAfterPlanning,humanPlaytest=false,knownTerrainNavigation=true,milestones=routeMilestones,navigationStarts=routeNavigationStarts,searches=routeSearchRecords,damageTotal=routeDamageTotal,damageEvents=routeDamageEvents},Catalog.JsonOptions));
    }
}
