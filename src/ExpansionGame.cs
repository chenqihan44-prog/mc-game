using System;
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
    private Cell? fishingWater;
    private double fishBite,fishExpires;
    private Inventory? fishingSupply;
    private int fishingSlot;
    private MeshInstance3D? floatView;
    private void TickExpansion(double delta)
    {
        foreach(var c in creatures)c.LureId=Held()?.Id??"";
        if(fishingWater is {} water){
            var position=new Vector3(water.X+.5f,water.Y+1,water.Z+.5f);
            if(screen!="play"||player!.GlobalPosition.DistanceTo(position)>12||world!.Catalog.Id(world.Get(water))!="water"||world.State.Time>fishExpires||fishingSupply?.Slots[fishingSlot]?.Id!="fishing_rod"){EndFishing();Toast("收回浮漂");}
            else if(world.State.Time>=fishBite){if(floatView!=null)floatView.Position=position+Vector3.Up*(float)(Math.Sin(world.State.Time*16)*.07);prompt.Text="鱼讯！右键收线";}
        }
        foreach(var state in world!.State.Creatures.Where(s=>s.Health>0&&!creatures.Any(c=>ReferenceEquals(c.State,s))).ToArray())SpawnCreature(state);
    }
    private void EndFishing(){fishingWater=null;fishingSupply=null;if(floatView!=null){floatView.QueueFree();floatView=null;}}
    private Creature? AimedCreature(float range=5)
    {
        var start=player!.Camera.GlobalPosition;var dir=-player.Camera.GlobalBasis.Z;var target=creatures.Where(c=>c.State.Health>0&&c!=player.Mount).Select(c=>{var aim=c.GlobalPosition+Vector3.Up*c.GetChild<EmberLighthouse.Rendering.CreatureModel>(0).BodyHeight*.5f;return(c,Projection:(aim-start).Dot(dir),Side:(aim-start).Cross(dir).Length());}).Where(p=>p.Projection>0&&p.Projection<range&&p.Side<.7f).OrderBy(p=>p.Projection).FirstOrDefault();return target.c!=null&&target.c.HasLineOfSight(start)?target.c:null;
    }
    private bool UseExpansionHand(Inventory supply,int slot,ItemDef def)
    {
        var animal=AimedCreature();if(animal!=null&&LifeRules.Animal(animal.Definition.Id)){
            bool success=LifeRules.Interact(world!,animal.State,supply,slot,out var info);if(success||def.Use is "animal_armor" or "shears"||def.Id is "bone" or "saddle"||def.Id==LifeRules.Food(animal.Definition.Id)){Toast(info);if(success){audio.Play("pickup");RequestSave();}return true;}
        }
        if(def.Use=="fish"){
            if(fishingWater!=null){if(world!.State.Time>=fishBite&&world.State.Time<=fishExpires&&ReferenceEquals(supply,fishingSupply)&&slot==fishingSlot){world.CatchFish(supply,slot,out var caught);Toast(caught);}else Toast("鱼尚未咬钩");EndFishing();return true;}
            var target=player!.Target(9,true);if(target==null||catalog.Id(world!.Get(target.Value.Hit))!="water"){Toast("请瞄准水面抛竿");return true;}
            fishingWater=target.Value.Hit;fishingSupply=supply;fishingSlot=slot;fishBite=world.State.Time+3+RandomField.Hash(world.State.Seed,target.Value.Hit.X,(int)world.State.Time,target.Value.Hit.Z,278)%5;fishExpires=fishBite+3;
            floatView=new(){Mesh=new BoxMesh{Size=new(.12f,.13f,.12f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new("dd6550")},Position=new(target.Value.Hit.X+.5f,target.Value.Hit.Y+1,target.Value.Hit.Z+.5f)};worldRoot!.AddChild(floatView);Toast("已抛竿，等待浮漂下沉的鱼讯");return true;
        }
        if(def.Use=="firework"){
            if(!player!.Gliding){Toast("烟花只能在滑翔时使用");return true;}if(!world!.State.Creative&&!supply.Remove(def.Id,1))return true;player.GlideBoost=2;Toast("滑翔推进");return true;
        }
        if(def.Use is "tonic" or "drink" or "bucket" or "bottle" or "hoe" or "fertilizer" or "shears" or "cane"||def.Id=="mushroom_stew"){
            var target=player!.Target(6,def.Use is "bucket" or "bottle");bool success=world!.UseExpansion(supply,slot,target?.Hit,target?.Before,out var info);Toast(string.IsNullOrEmpty(info)?"背包空间不足或此处无法使用":info);if(success){if(target!=null){renderer!.RefreshEdit(target.Value.Hit);renderer.RefreshEdit(target.Value.Before);renderer.RefreshEdit(target.Value.Hit+new Cell(0,1,0));}audio.Play("pickup");}return true;
        }
        return false;
    }
    private bool InteractExpansion()
    {
        if(player!.Mount!=null){if(!player.Dismount())Toast("周围没有安全下马位置");else Toast("已下马");return true;}
        var c=AimedCreature();if(c==null||!LifeRules.Animal(c.Definition.Id))return false;
        SetScreen("animal");var col=Screen(overlay,c.Definition.Name+(c.State.BabyUntil>world!.State.Time?" · 幼体":""));
        col.AddChild(Label($"生命 {c.State.Health:0}/{c.Definition.Health:0} · {(c.State.Tamed?"已驯服":"野生")}\n喂食：{(LifeRules.Food(c.Definition.Id) is {} food&&food!=""?catalog.Items[food].Name:"无")}",17));
        string use=c.Definition.Id switch{"cow"=>"空铁桶右键挤奶，间隔120秒；皮革可制作装备。","sheep"=>"剪刀右键剪羊毛，180秒重新长出；羊毛可织布。","chicken"=>"每180秒产蛋，落在附近；羽毛可制箭。","wolf"=>"骨头右键驯服；跟随协战；犰狳鳞甲制作与修理狼铠。","cat"=>"生鳕鱼右键驯服；附近的苦力怕会避开它。","horse"=>"苹果右键驯服，鞍右键安装，装备马铠后提高防护。","armadillo"=>"用剪刀梳理，120秒恢复一次鳞甲；制作狼铠。","turtle"=>"芦苇喂养繁殖，幼体成长后留下海龟鳞甲。","fox"=>"浆果引诱和繁殖；受惊逃离玩家。",_=>"成对喂食可繁殖；繁殖后休息300秒，幼体360秒长大。"};var detail=Label(use,17);detail.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(detail);
        if(c.State.Tamed){col.AddChild(Button(c.State.Sitting?"跟随":"原地停留",()=>{c.State.Sitting=!c.State.Sitting;RequestSave();SetScreen("play");}));
            if(c.Definition.Id=="horse"&&c.State.Saddled&&c.State.BabyUntil<=world!.State.Time)col.AddChild(Button("骑乘（Shift下马）",()=>{if(c.GlobalPosition.DistanceTo(player.GlobalPosition)>5){Toast("坐骑太远");SetScreen("play");return;}player.Mount=c;c.Mounted=true;player.AddCollisionExceptionWith(c);c.AddCollisionExceptionWith(player);SetScreen("play");}));
            if(c.State.Armor!=null)col.AddChild(Button("卸下 "+catalog.Items[c.State.Armor.Id].Name,()=>{if(world!.State.Inventory.Add(c.State.Armor,catalog)>0){Toast("背包空间不足");return;}c.State.Armor=null;RequestSave();SetScreen("play");}));
        }
        col.AddChild(Button("返回游戏",()=>SetScreen("play")));return true;
    }
    private void CreeperExplode(Creature c)
    {
        c.Exploded=true;audio.Play("break_stone",c.GlobalPosition);miningFeedback?.Emit(Explorer.ToCell(c.GlobalPosition),catalog.Items["stone"]);
        // Explosions damage living targets; buildings and quest devices are preserved.
        foreach(var other in creatures.Where(a=>a!=c&&a.State.Health>0&&a.GlobalPosition.DistanceTo(c.GlobalPosition)<3.5f).ToArray())if(c.HasLineOfSight(other.GlobalPosition+Vector3.Up*.4f))other.Damage(20);
        c.State.Health=0;CreatureDied(c);
    }
    private void ShowEnchantments()
    {
        SetScreen("enchant");var col=Screen(overlay,"护甲附魔");col.AddChild(Label("保护：每级减伤1.5%；耐久：降低磨损。每件最高三级。\n每次消耗晶矿 ×1、经验 6×下一等级；金甲每次提升两级。",16));var list=Column(Scroll(col,310));
        void Item(Inventory inventory,int slot){if(inventory.Slots[slot] is not {} item||catalog.Items[item.Id].Use!="armor")return;var row=Row(list);row.AddChild(Label(catalog.Items[item.Id].Name+$" · 保护{item.Protection} / 耐久{item.Unbreaking}",15));foreach(bool protection in new[]{true,false}){bool p=protection;row.AddChild(Button(p?"保护":"耐久",()=>{if(!FacilityNear("enchanting_table")){Toast("附魔台不可用");return;}ArmorTraits.Enchant(world!,inventory,slot,p,out var info);Toast(info);ShowEnchantments();}));}}
        for(int i=0;i<36;i++)Item(world!.State.Inventory,i);for(int i=0;i<4;i++)Item(world!.State.Equipment,i);col.AddChild(Button("返回游戏",()=>SetScreen("play")));
    }
    private bool FacilityNear(string id)=>currentContainer is {} p&&world!.Catalog.Id(world.Get(p))==id&&player!.GlobalPosition.DistanceTo(new(p.X+.5f,p.Y+.5f,p.Z+.5f))<6;
    private void DyeArmor(Inventory inventory,int slot,string dye,Action back)
    {
        if(!MaintenanceAvailable()||inventory.Slots[slot] is not {} item||!item.Id.StartsWith("leather_")&&item.Id!="wolf_armor"){Toast("需要工作台旁的皮革装备或狼铠");return;}if(!world!.State.Creative&&!world.State.Inventory.Remove(dye+"_dye",1)){Toast("缺少染料");return;}item.Dye=dye;ShowWorkshopMaintenance(back);
    }
}
