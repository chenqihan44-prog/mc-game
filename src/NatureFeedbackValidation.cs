using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;
public partial class Game
{
    private bool natureCheck;
    private async void RunNatureCheck()
    {
        string folder=ProjectSettings.GlobalizePath("res://test-results/nature-feedback");Directory.CreateDirectory(folder);string stage="tree fixture";
        void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(stage+": "+reason);}
        async Task Frames(int n=4){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> done){var end=Time.GetTicksMsec()+45000;while(!done()){if(Time.GetTicksMsec()>end)throw new TimeoutException(stage);await Frames(1);}}
        async Task Shot(string name){if(DisplayServer.GetName()=="headless")return;await Frames(5);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(folder,name+".png"));}
        try{
            settings.ViewDistance=3;StartWorld(new(){Name="树林与命中反馈验证",Seed=42,Creative=true,DayTime=300,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);foreach(var c in creatures)c.QueueFree();creatures.Clear();world!.State.Creatures.Clear();
            for(int x=316;x<=360;x++)for(int z=316;z<=353;z++)world.Set(new(x,109,z),catalog.Number("grass"));
            var examples=new[]{new TreeShape("wood","leaves",9,2,1),new("birch_log","birch_leaves",7,1,1),new("pine_log","pine_leaves",13,2,1),new("acacia_log","acacia_leaves",6,1,1)};
            for(int i=0;i<examples.Length;i++)for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++)for(int y=1;y<=17;y++)if(NaturalTrees.Block(examples[i],x,y,z) is {} block)world.Set(new(323+i*10+x,109+y,325+z),catalog.Number(block));
            player.GlobalPosition=new(338,110,350);world.State.Position=[338,110,350];player.Camera.LookAt(new Vector3(338,116,325));await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition)&&renderer.IsVisualReady(new(338,115,325)));await Shot("layered-trees");
            stage="melee confirmation";player.GlobalPosition=new(338,110,345);world.State.Position=[338,110,345];var pigState=new CreatureState{Kind="pig",Position=[338,110,342.5],Health=creatureDefs.Single(d=>d.Id=="pig").Health};world.State.Creatures.Add(pigState);SpawnCreature(pigState);var pig=creatures.Single();pig.SetPhysicsProcess(false);player.Camera.LookAt(pig.GlobalPosition+Vector3.Up*.5f);selected=0;world.State.Inventory.Slots[0]=null;await Until(()=>renderer!.IsTerrainReady(pig.GlobalPosition));double before=pig.State.Health;int hits=combatFeedback.ConfirmedHits;Check(Attack(),"native melee failed to select pig");Check(pig.State.Health==before-4&&combatFeedback.ConfirmedHits==hits+1&&combatFeedback.Damage==4&&pig.HitFeedbackVisible,"actual melee lacks damage/health feedback");await Shot("melee-hit");
            stage="ranged confirmation after impact";world.State.Inventory.Slots[0]=new("bow",1);hits=combatFeedback.ConfirmedHits;before=pig.State.Health;Check(Attack(),"bow did not fire");Check(combatFeedback.ConfirmedHits==hits,"bow shows a hit before projectile impact");await Until(()=>combatFeedback.ConfirmedHits>hits);Check(pig.State.Health<before&&combatFeedback.Damage==before-pig.State.Health,"projectile feedback shows wrong damage");await Shot("arrow-hit");
            stage="replicated flash";pig.NetworkReplica=true;var replica=pig.State.Copy();replica.Health-=1;pig.ApplyReplicaState(replica);Check(pig.HitFeedbackVisible,"replica misses hit flash and floating damage");
            stage="shield and pause";var bossState=new CreatureState{Kind="colossus",Position=[355,110,345],Health=creatureDefs.Single(d=>d.Id=="colossus").Health};SpawnCreature(bossState);var boss=creatures.Last();boss.SetPhysicsProcess(false);before=boss.State.Health;DealCreatureDamage(boss,12,"host");Check(boss.State.Health==before&&combatFeedback.Damage==0&&combatFeedback.Remaining>0,"shield is reported as damage");SetScreen("journal");Check(combatFeedback.Remaining==0,"menu retains hit marker");
            stage="old-world refresh control";var old=new WorldSnapshot{Name="树木升级验证",GeneratorVersion=7};saves.Save(old);var entry=saves.ListDetails().Single(w=>w.Id==old.WorldId);ShowNatureRefresh(entry);
            System.Collections.Generic.IEnumerable<Node> Nodes(Node root){foreach(var c in root.GetChildren()){yield return c;foreach(var n in Nodes(c))yield return n;}}
            int backupCount=saves.ListBackups().Count();Nodes(overlay).OfType<Button>().Single(b=>b.Text=="备份并更新").EmitSignal(Button.SignalName.Pressed);await Until(()=>worldIo==null&&screen=="worlds");Check(saves.Load(old.WorldId).Snapshot.GeneratorVersion==8&&saves.ListBackups().Count()>backupCount,"actual update button misses backup or new generator");
            File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=true,rendered=DisplayServer.GetName()!="headless",melee=true,projectileImpact=true,replicaFlash=true,blockedHit=true,menuReset=true,backupUpgradeButton=true,treeExamples=4},Catalog.JsonOptions));GD.Print("NATURE_FEEDBACK_PASS: layered trees, melee and projectile damage/health, replica flash, shield, menu reset and backed-up world update");QuitCleanly();
        }catch(Exception error){File.WriteAllText(Path.Combine(folder,"validation.json"),JsonSerializer.Serialize(new{passed=false,stage,error=error.ToString()},Catalog.JsonOptions));GD.PushError("NATURE_FEEDBACK_FAILED: "+error);QuitCleanly(17);}
    }
}
