using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Player;
using EmberLighthouse.Rendering;
using EmberLighthouse.World;
using EmberLighthouse.Items;
using Godot;

namespace EmberLighthouse;
public partial class Game
{
    private static IEnumerable<Node> MotionDescendants(Node parent)
    {
        foreach(var child in parent.GetChildren()){yield return child;foreach(var nested in MotionDescendants(child))yield return nested;}
    }
    private async void RunCreatureMotionCheck()
    {
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Until(Func<bool> predicate){var clock=Stopwatch.StartNew();while(!predicate()){if(clock.Elapsed.TotalSeconds>45)throw new TimeoutException("creature motion runtime");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
        var checks=new List<object>();var timer=Stopwatch.StartNew();
        string reportRoot=ProjectSettings.GlobalizePath("res://test-results/creature-motion");Directory.CreateDirectory(reportRoot);
        bool capture=OS.GetCmdlineUserArgs().Contains("--creature-motion-capture")&&DisplayServer.GetName()!="headless";
        try{
            var holder=new Node3D();AddChild(holder);
            foreach(var def in creatureDefs){
                var model=CreatureModel.Create(def.Id);holder.AddChild(model);Check(model.Generated,"procedural fallback: "+def.Id);
                Check(model.PixelModel,"old source model still active: "+def.Id);
                var rig=model.GeneratedSkeleton!;Check(rig.GetBoneCount()>=6,"missing articulated rig: "+def.Id);
                var meshes=MotionDescendants(model).OfType<MeshInstance3D>().ToArray();Check(meshes.Length==1&&meshes[0].Skin!=null&&meshes[0].Skin.GetBindCount()>=6,"unbound source mesh: "+def.Id);
                var material=(StandardMaterial3D)meshes[0].GetActiveMaterial(0);Check(material.TextureFilter==BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,"blurred pixel atlas: "+def.Id);
                var before=Enumerable.Range(0,rig.GetBoneCount()).Select(rig.GetBonePoseRotation).ToArray();
                for(int i=0;i<30;i++)model.Animate(1d/60,new(0,0,-2.4f),true);
                float deformation=Enumerable.Range(0,rig.GetBoneCount()).Max(n=>before[n].AngleTo(rig.GetBonePoseRotation(n)));
                Check(deformation>.03f,"rig stayed rigid during locomotion: "+def.Id);
                float footError=Mathf.Abs(model.LowestFootHeight);
                if(def.Id is not("cod" or "squid"))Check(model.FootSampleCount>0&&footError<.001f,"sole contact drift: "+def.Id+" / "+footError);
                var frozen=rig.GetBonePoseRotation(1);model.Animate(1,Vector3.Zero,false);Check(frozen.IsEqualApprox(rig.GetBonePoseRotation(1)),"pause moved bones: "+def.Id);
                model.Animate(1d/60,Vector3.Zero,true,.75f,def.Attack);model.Animate(1d/60,Vector3.Zero,true,0,def.Attack,.5f);model.Animate(1d/60,Vector3.Zero,true,0,def.Attack,-1,.5f);
                Check(Enumerable.Range(0,rig.GetBoneCount()).All(n=>rig.GetBonePoseRotation(n).IsFinite()),"nonfinite pose: "+def.Id);
                checks.Add(new{kind=def.Id,bones=rig.GetBoneCount(),sourceMeshes=meshes.Length,skinBinds=meshes[0].Skin.GetBindCount(),deformation,pauseFreeze=true,pixelSampling=true,footSamples=model.FootSampleCount,footError});
                holder.RemoveChild(model);model.QueueFree();await Frames(1);
            }
            holder.QueueFree();
            if(capture)await CaptureCreatureMotion();
            StartWorld(new(){Name="生物攻击与骨骼动作验证",Seed=42,Creative=true,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});
            await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition));await Frames(8);
            foreach(var creature in creatures){creature.SetPhysicsProcess(false);creature.QueueFree();}creatures.Clear();world!.State.Creatures.Clear();
            foreach(var def in creatureDefs){
                var specimen=new CreatureState{Kind=def.Id,Health=def.Health,Position=[player!.GlobalPosition.X+4,player.GlobalPosition.Y,player.GlobalPosition.Z+4]};
                world.State.Creatures.Add(specimen);SpawnCreature(specimen);var actor=creatures.Last();actor.SetPhysicsProcess(false);
                Check(actor.GetChild<CreatureModel>(0).Generated,"world spawn did not use generated asset: "+def.Id);
            }
            Check(creatures.Count==26,"world spawn coverage");
            foreach(var creature in creatures){creature.SetPhysicsProcess(false);creature.QueueFree();}creatures.Clear();world.State.Creatures.Clear();await Frames(2);
            player!.SetPhysicsProcess(false);player.GlobalPosition=new(0,37.02f,0);world.State.Position=[0,37.02,0];world.State.Creative=false;
            var state=new CreatureState{Kind="husk",Health=40,Position=[0,37.02,-1.2]};world.State.Creatures.Add(state);SpawnCreature(state);
            var enemy=creatures.Single();int hits=0;enemy.AttackPlayer=_=>hits++;
            await Until(()=>enemy.IsPreparingAttack);Check(hits==0,"hit during windup");
            await Until(()=>enemy.IsStriking);Check(hits==0,"hit before strike contact");
            var pending=world.State.Copy();Check(pending.Creatures[0].StrikeRemaining>0&&pending.Creatures[0].AttackAim!=null,"strike omitted from snapshot");
            var encoded=JsonSerializer.Serialize(pending,Catalog.JsonOptions);var restored=JsonSerializer.Deserialize<WorldSnapshot>(encoded,Catalog.JsonOptions)!;restored.ValidateShape();Check(restored.Creatures[0].StrikeRemaining==state.StrikeRemaining,"strike lost in JSON roundtrip");
            enemy.SetPhysicsProcess(false);state.Position=[enemy.GlobalPosition.X,enemy.GlobalPosition.Y,enemy.GlobalPosition.Z];StartWorld(restored);
            enemy=creatures.Single();enemy.AttackPlayer=_=>hits++;player!.SetPhysicsProcess(false);
            await Until(()=>renderer!.IsTerrainReady(player.GlobalPosition));await Until(()=>enemy.IsRecovering);Check(hits==1,"strike restore duplicated or missed contact");
            await Frames(10);Check(hits==1,"recovery dealt repeated damage");
            enemy.State.AttackCooldown=0;enemy.State.RecoveryRemaining=0;await Until(()=>enemy.IsPreparingAttack);await Until(()=>enemy.IsStriking);world.State.Creative=true;await Frames(20);Check(hits==1&&!enemy.IsStriking,"creative mode did not cancel strike");
            world.State.Creative=false;enemy.State.AttackCooldown=0;enemy.State.RecoveryRemaining=0;await Until(()=>enemy.IsPreparingAttack);await Until(()=>enemy.IsStriking);
            player.GlobalPosition=enemy.GlobalPosition-(-enemy.GlobalBasis.Z)*1.1f;await Until(()=>enemy.IsRecovering);Check(hits==1,"melee hit player behind attacker");
            if(capture){
                StartWorld(new(){Name="方块生物场景展示",Seed=42,Creative=true,GuardianDefeated=true,Population=new(){Initialized=true,NextSpawnTime=100000}});
                await Until(()=>renderer!.IsTerrainReady(player!.GlobalPosition));await Frames(8);
                foreach(var creature in creatures){creature.SetPhysicsProcess(false);creature.QueueFree();}creatures.Clear();world!.State.Creatures.Clear();
                foreach(string kind in new[]{"cow","pig","guardian"}){
                    var specimen=new CreatureState{Kind=kind,Health=creatureDefs.Single(d=>d.Id==kind).Health,Position=[player!.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z]};
                    world.State.Creatures.Add(specimen);SpawnCreature(specimen);
                }
                await CaptureBlockCreatureWorld();
            }
            SetScreen("saving");
            File.WriteAllText(Path.Combine(reportRoot,"validation.json"),JsonSerializer.Serialize(new{passed=true,models=checks.Count,checks,strikeContactOnce=true,strikeSnapshotRestore=true,creativeStrikeCancel=true,frontalMeleeArc=true,renderedCapture=capture,seconds=timer.Elapsed.TotalSeconds},Catalog.JsonOptions));
            GD.Print("CREATURE_MOTION_PASS: 26 skinned source meshes, locomotion deformation, pause, persisted strike, contact once, recovery, creative cancellation, frontal melee");QuitCleanly();
        }catch(Exception e){GD.PushError("CREATURE_MOTION_FAILED: "+e);GetTree().Quit(17);}
    }
    private async Task CaptureCreatureMotion()
    {
        string folder=ProjectSettings.GlobalizePath("res://docs/previews/block-creature-game-2026-10-13");Directory.CreateDirectory(folder);
        var viewport=new SubViewport{OwnWorld3D=true,Size=new(512,416),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        viewport.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("c8ccd4"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.65f}});
        viewport.AddChild(new DirectionalLight3D{RotationDegrees=new(-42,-28,0),LightEnergy=1.2f});
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Current=true};viewport.AddChild(camera);var holder=new Node3D();viewport.AddChild(holder);
        using var sheet=Image.CreateEmpty(2048,7*416,false,Image.Format.Rgba8);int index=0;
        foreach(var def in creatureDefs){
            var model=CreatureModel.Create(def.Id);holder.AddChild(model);
            float extent=Mathf.Max(model.BoundSize.X,Mathf.Max(model.BoundSize.Y,model.BoundSize.Z));camera.Size=extent*1.5f;
            var center=new Vector3(0,model.LabelHeight*.43f,0);camera.Position=center+new Vector3(2.7f,1.5f,-4.2f)*extent;camera.LookAt(center);
            for(int n=0;n<24;n++)model.Animate(1d/60,new(0,0,-2.3f),true);
            for(int n=0;n<3;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var image=viewport.GetTexture().GetImage()){image.SavePng(Path.Combine(folder,def.Id+"-game.png"));sheet.BlitRect(image,new(0,0,512,416),new(index%4*512,index/4*416));}index++;
            if(def.Id is "horse" or "guardian"){
                string demo=Path.Combine(folder,def.Id+"-motion");Directory.CreateDirectory(demo);
                for(int n=0;n<48;n++){
                    float t=n/48f;
                    if(def.Id=="horse")model.Animate(1d/24,new(0,0,-4.5f),true);
                    else if(t<.55f)model.Animate(1d/24,Vector3.Zero,true,t/.55f,"slam");
                    else if(t<.70f)model.Animate(1d/24,Vector3.Zero,true,0,"slam",(t-.55f)/.15f);
                    else model.Animate(1d/24,Vector3.Zero,true,0,"slam",-1,(t-.70f)/.30f);
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var frame=viewport.GetTexture().GetImage();frame.SavePng(Path.Combine(demo,$"frame-{n:D2}.png"));
                }
            }
            holder.RemoveChild(model);model.QueueFree();
        }
        sheet.SavePng(Path.Combine(folder,"all-26-game-models.png"));viewport.QueueFree();
    }
    private async Task CaptureBlockCreatureWorld()
    {
        // A disposable validation world shows the actual terrain, materials and HUD.
        var cell=Explorer.ToCell(player!.GlobalPosition);int floor=cell.Y;
        world!.State.DayTime=300;
        for(int x=cell.X-6;x<=cell.X+6;x++)for(int z=cell.Z-5;z<=cell.Z+5;z++){
            world.Set(new(x,floor-1,z),catalog.Number("grass"));
            for(int y=floor;y<=floor+4;y++)world.Set(new(x,y,z),0);
        }
        foreach(int x in new[]{cell.X-6,cell.X,cell.X+6})foreach(int z in new[]{cell.Z-5,cell.Z,cell.Z+5})renderer!.RefreshEdit(new(x,floor-1,z));
        var center=new Vector3(cell.X+.5f,floor+.02f,cell.Z+.5f);
        foreach(var actor in creatures){
            actor.SetPhysicsProcess(false);actor.SetProcess(false);
            actor.Visible=actor.Definition.Id is "cow" or "pig" or "guardian";
            if(!actor.Visible)continue;
            actor.GlobalPosition=center+(actor.Definition.Id switch{"cow"=>new Vector3(-1.7f,0,0),"pig"=>new Vector3(0,0,-.55f),_=>new Vector3(2.2f,0,.4f)});
            actor.Rotation=Vector3.Zero;
            actor.GetChildren().OfType<Label3D>().Single().Visible=true;
            for(int n=0;n<45;n++)actor.GetChild<CreatureModel>(0).Animate(1d/60,Vector3.Zero,true);
        }
        player.SetPhysicsProcess(false);player.GlobalPosition=center+Vector3.Forward*6;
        SetScreen("play");player.SetPhysicsProcess(false);player.Visible=false;
        var sceneCamera=new Camera3D{Current=true,Fov=60,Position=center+new Vector3(3.6f,1.85f,-6.2f)};worldRoot!.AddChild(sceneCamera);sceneCamera.LookAt(center+Vector3.Up*1.1f);
        for(int n=0;n<90;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        string path=ProjectSettings.GlobalizePath("res://docs/previews/block-creature-game-2026-10-13/world-cow-pig-guardian.png");
        using var image=GetViewport().GetTexture().GetImage();image.SavePng(path);
    }
}
