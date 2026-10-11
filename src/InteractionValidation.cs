using System;
using EmberLighthouse.Core;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private async void RunInteractionCheck()
    {
        try{
            StartWorld(new(){Name="输入与挖掘回归检查",Seed=42,Creative=true});
            foreach(var c in creatures)c.QueueFree();creatures.Clear();
            void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
            foreach(string action in new[]{"mine","use","forward","jump"})Check(InputMap.ActionGetEvents(action).Count==0,"validator accepts hardware: "+action);
            var mouseMode=Input.MouseMode;SetScreen("menu");SetScreen("play");Check(Input.MouseMode==mouseMode,"validator changed mouse capture");
            var rotation=player!.Rotation;
            player._UnhandledInput(new InputEventMouseMotion{Relative=new(200,100)});
            Check(player.Rotation==rotation&&!player.AcceptUserInput,"validator accepted real mouse motion");
            Input.ActionPress("mine");SetScreen("play");RearmActions();Check(!actionsArmed,"menu click armed mining");
            Input.ActionRelease("mine");RearmActions();Check(actionsArmed,"released mouse did not rearm mining");
            foreach(int fps in new[]{30,60,240}){
                int Simulate(double seconds){
                    var cells=new[]{new Cell(400,80,400),new Cell(401,80,400),new Cell(402,80,400),new Cell(403,80,400),new Cell(404,80,400)};
                    foreach(var cell in cells)world!.Set(cell,catalog.Number("plank"));
                    attackCooldown=0;miningCell=null;mineProgress=0;int next=0;
                    for(int frame=0;frame<(int)Math.Ceiling(seconds*fps);frame++){
                        attackCooldown-=1.0/fps;if(next>=cells.Length)break;
                        var cell=cells[next];Mine(1.0/fps,(cell,cell+new Cell(0,0,1),3));if(world!.Get(cell)==0)next++;
                    }
                    return next;
                }
                Check(Simulate(.12)==1,"brief press broke multiple blocks at "+fps+" FPS");
                int held=Simulate(.8);Check(held>=3&&held<=4,"held mining repeat cadence at "+fps+" FPS");
                var p=new Cell(400,80,400);world!.Set(p,catalog.Number("plank"));attackCooldown=.1;miningCell=p;mineProgress=.5;
                Mine(.01,(p,p,3));Check(world.Get(p)!=0&&mineProgress==.5,"cooldown advanced mining progress");
            }
            // Let deferred removals and physics resource changes settle before exit.
            SetScreen("saving");
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("INTERACTION_PASS: hardware input isolated, mouse capture unchanged, menu click rearmed, brief/held mining checked at 30/60/240 FPS");QuitCleanly();
        }catch(Exception e){GD.PushError("INTERACTION_FAILED: "+e);GetTree().Quit(8);}
    }
}
