using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool feedbackCheck;
    private async void RunFeedbackCheck()
    {
        bool reload=OS.GetCmdlineUserArgs().Contains("--feedback-reload");
        var timer=Stopwatch.StartNew();var samples=new List<object>();string stage="startup";
        static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
        static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var n in Descendants(child))yield return n;}}
        async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        async Task Until(Func<bool> done){double end=timer.Elapsed.TotalSeconds+10;while(!done()){if(timer.Elapsed.TotalSeconds>end)throw new TimeoutException(stage);await Frames(1);}}
        void Record(bool passed,string error){
            string path=ProjectSettings.GlobalizePath("res://test-results/feedback");Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,reload?"reload.json":"validation.json"),JsonSerializer.Serialize(new{passed,error,stage,seconds=timer.Elapsed.TotalSeconds,headless=DisplayServer.GetName()=="headless",renderedImage=false,controlledDamage=true,coldSettingsReload=reload,samples},Catalog.JsonOptions));
        }
        try{
            if(reload)Check(!settings.DamageFlash&&!damageFeedback.FlashEnabled,"cold startup did not restore the disabled flash setting");
            StartWorld(new(){Name="受伤反馈与设置回归",Seed=42,Creative=false,Hunger=50,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});
            player!.SetPhysicsProcess(false);await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));await Frames();
            Check(damageFeedback.MouseFilter==Control.MouseFilterEnum.Ignore&&damageFeedback.FocusMode==Control.FocusModeEnum.None,"feedback captures UI input");
            Check(damageFeedback.GetIndex()<minecraftHud.GetIndex(),"feedback covers the hotbar/health HUD");
            if(reload){
                stage="cold startup damage with flash disabled";double health=world!.State.Health;Hurt(10);await Frames();
                Check(Math.Abs(world.State.Health-(health-10))<.001&&!damageFeedback.Visible&&message.StartsWith("受伤"),"disabled setting lost health/notice or flashed after cold startup");
                samples.Add(new{stage,setting=settings.DamageFlash,opacity=damageFeedback.Color.A});Record(true,"");GD.Print("FEEDBACK_RELOAD_PASS: disabled flash persisted across independent EXE startup, damage and text remain");QuitCleanly();return;
            }
            settings.DamageFlash=true;ApplySettings();
            stage="real damage fade and repeated-hit cap";double before=world!.State.Health;Hurt(10);
            Check(world.State.Health==before-10&&damageFeedback.Visible&&Math.Abs(damageFeedback.Color.A-DamageFeedback.PeakOpacity)<.00001,"damage did not trigger a bounded red overlay");
            float last=damageFeedback.Color.A;double fadeStart=timer.Elapsed.TotalSeconds;int frames=0;
            while(damageFeedback.Visible){
                Hurt(.01);await Frames(1);float alpha=damageFeedback.Color.A;Check(alpha<=last+.00001&&alpha>=0,"repeated damage restarted or accumulated the fading overlay");last=alpha;frames++;
                if(timer.Elapsed.TotalSeconds-fadeStart>1.5)throw new TimeoutException("fade did not finish");
            }
            Hurt(.01);Check(!damageFeedback.Visible,"damage bypassed the minimum pulse interval");
            samples.Add(new{stage,frames,fadeSeconds=timer.Elapsed.TotalSeconds-fadeStart,peakOpacity=DamageFeedback.PeakOpacity,minimumInterval=DamageFeedback.MinimumInterval});
            stage="ignored damage";before=world.State.Health;world.State.Creative=true;Hurt(10);Check(world.State.Health==before&&!damageFeedback.Visible,"creative damage flashed");world.State.Creative=false;
            foreach(double damage in new[]{0,-1,double.NaN,double.PositiveInfinity})Hurt(damage);Check(world.State.Health==before&&!damageFeedback.Visible,"invalid damage changed health or feedback");
            SetScreen("pause");Hurt(10);Check(world.State.Health==before&&!damageFeedback.Visible,"paused damage flashed");SetScreen("play");
            stage="actual hostile projectile";int hits=projectiles!.PlayerHits;before=world.State.Health;var origin=player.GlobalPosition+new Vector3(0,1,-2);
            Check(projectiles.Fire(new(){Position=[origin.X,origin.Y,origin.Z],Velocity=[0,0,10],Damage=7,Remaining=1,Hostile=true}),"projectile fixture refused");await Until(()=>projectiles.PlayerHits>hits);
            Check(Math.Abs(world.State.Health-(before-7))<.001&&damageFeedback.Visible,"real projectile did not trigger damage feedback");samples.Add(new{stage,damage=before-world.State.Health,opacity=damageFeedback.Color.A});
            settings.DamageFlash=false;ApplySettings();Check(!damageFeedback.Visible&&damageFeedback.Color.A==0,"disabling an active flash did not immediately clear it");
            stage="settings checkbox and persistence";ShowSettings("pause");var flash=Descendants(overlay).OfType<CheckButton>().Single(c=>c.Text=="受伤画面闪烁");flash.ButtonPressed=true;
            Check(settings.DamageFlash&&damageFeedback.FlashEnabled,"checkbox did not enable real feedback");flash.ButtonPressed=false;Check(!settings.DamageFlash&&!damageFeedback.FlashEnabled,"checkbox did not disable real feedback");
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="保存并返回").EmitSignal(Button.SignalName.Pressed);Check(screen=="pause","settings return did not reach pause");
            Check(JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(settingsPath),Catalog.JsonOptions)?.DamageFlash==false,"disabled setting was not written");
            SetScreen("play");before=world.State.Health;Hurt(10);Check(world.State.Health==before-10&&!damageFeedback.Visible&&message.StartsWith("受伤"),"disabled flash suppressed damage/notice");samples.Add(new{stage,disabledDamage=10,persisted=false});
            settings.DamageFlash=true;ApplySettings();
            stage="HUD sizing";foreach(float scale in new[]{.8f,1f,1.4f}){settings.UiScale=scale;ApplySettings();await Frames();var size=damageFeedback.Size;
                Check(size.DistanceTo(hudRoot.Size)<.01&&size.X>0&&size.Y>0,"feedback does not cover the native HUD bounds");samples.Add(new{stage,scale,width=size.X,height=size.Y});}
            stage="screen transitions";foreach(string id in new[]{"pause","inventory","map","help","settings"}){SetScreen("play");Hurt(.1);Check(damageFeedback.Visible,"new pulse missing before "+id);SetScreen(id);Check(!damageFeedback.Visible&&damageFeedback.Color.A==0,"stale flash on "+id);samples.Add(new{stage,screen=id,cleared=true});}
            SetScreen("play");Hurt(.1);StartWorld(new(){Name="反馈重建检查",Seed=42,Hunger=50,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});player!.SetPhysicsProcess(false);Check(!damageFeedback.Visible,"new world inherited an old flash");await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));
            stage="death and respawn";Hurt(200);Check(screen=="death"&&!damageFeedback.Visible,"death screen inherited a flash");
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="从营地复活").EmitSignal(Button.SignalName.Pressed);Check(screen=="play"&&!damageFeedback.Visible,"respawn inherited a flash");samples.Add(new{stage,cleared=true});
            await Until(()=>saveCallbacks.Count==0);stage="save and menu exit";Hurt(.1);Check(damageFeedback.Visible,"exit fixture not visible");ReturnMenu();Check(!damageFeedback.Visible,"save screen inherited a flash");await Until(()=>screen=="menu");Check(world==null&&!damageFeedback.Visible,"menu inherited a flash");
            ShowSettings("menu");Descendants(overlay).OfType<CheckButton>().Single(c=>c.Text=="受伤画面闪烁").ButtonPressed=false;
            Descendants(overlay).OfType<Button>().Single(b=>b.Text=="保存并返回").EmitSignal(Button.SignalName.Pressed);
            Record(true,"");GD.Print("FEEDBACK_PASS: real damage/projectile, bounded fade, ignored damage, actual checkbox/save, HUD bounds, pause/death/respawn/world/menu cleanup");QuitCleanly();
        }catch(Exception error){Record(false,error.Message);GD.PushError("FEEDBACK_FAILED: "+error);QuitCleanly(9);}
    }
}
