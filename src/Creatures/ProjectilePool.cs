using System;
using System.Linq;
using EmberLighthouse.Player;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.Creatures;

public partial class ProjectilePool : Node3D
{
    public WorldState World {get;set;}=null!;
    public Explorer Player {get;set;}=null!;
    public Action<double,Vector3> HitPlayer {get;set;}=null!;
    public Action<CreatureState,double> HitCreature {get;set;}=null!;
    public Func<Vector3,bool> ReadyTerrain {get;set;}=null!;
    private readonly MeshInstance3D[] views=new MeshInstance3D[ProjectileFlight.Limit];
    private readonly ProjectileState?[] shots=new ProjectileState?[ProjectileFlight.Limit];
    private Mesh arrow=null!,bolt=null!;
    private StandardMaterial3D arrowMaterial=null!,boltMaterial=null!;
    public int Active=>shots.Count(s=>s!=null);
    public bool CanFire=>Active<shots.Length&&World.State.Projectiles.Count<shots.Length;
    public int BlockedCount {get;private set;}
    public int PlayerHits {get;private set;}
    public int CreatureHits {get;private set;}
    public void Clear(){for(int i=0;i<shots.Length;i++){shots[i]=null;views[i].Visible=false;}World.State.Projectiles.Clear();}
    public override void _Ready()
    {
        arrow=new CapsuleMesh{Radius=.035f,Height=.6f};bolt=new SphereMesh{Radius=.12f,Height=.24f};
        arrowMaterial=new(){AlbedoColor=new("c6a779")};boltMaterial=new(){AlbedoColor=new("8ce7e2"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
        for(int i=0;i<views.Length;i++){views[i]=new(){Visible=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(views[i]);}
        foreach(var shot in World.State.Projectiles)Bind(shot);
    }
    public bool Fire(ProjectileState shot)
    {
        if(!CanFire||World.State.Projectiles.Contains(shot))return false;shot.Validate();
        World.State.Projectiles.Add(shot);Bind(shot);return true;
    }
    private void Bind(ProjectileState shot)
    {
        int index=Array.FindIndex(shots,s=>s==null);if(index<0)throw new InvalidOperationException("投射物池容量不足");
        shots[index]=shot;var view=views[index];view.Mesh=shot.Hostile?bolt:arrow;view.MaterialOverride=shot.Hostile?boltMaterial:arrowMaterial;view.Visible=true;UpdateView(index);
    }
    private void UpdateView(int index)
    {
        var shot=shots[index]!;var point=shot.Point;var dir=shot.Direction;views[index].GlobalPosition=new(point.X,point.Y,point.Z);
        if(!shot.Hostile){var direction=new Vector3(dir.X,dir.Y,dir.Z).Normalized();views[index].GlobalBasis=Basis.LookingAt(direction,Math.Abs(direction.Y)>.98f?Vector3.Right:Vector3.Up);views[index].RotateObjectLocal(Vector3.Right,Mathf.Pi/2);}
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!Player.Enabled||!ReadyTerrain(Player.GlobalPosition))return;
        for(int i=0;i<shots.Length;i++){
            var shot=shots[i];if(shot==null)continue;
            var current=shot.Point;if(!ReadyTerrain(new(current.X,current.Y,current.Z)))continue;
            var hit=ProjectileFlight.Step(World,shot,delta,p=>ReadyTerrain(new(p.X,p.Y,p.Z)),Player.Crouching?1.1f:1.8f);UpdateView(i);
            if(hit.Outcome==ProjectileOutcome.Flying)continue;
            // Retire before applying damage; death/save callbacks see no spent shot.
            shots[i]=null;views[i].Visible=false;World.State.Projectiles.Remove(shot);
            if(hit.Outcome==ProjectileOutcome.Blocked)BlockedCount++;
            else if(hit.Outcome==ProjectileOutcome.Player)PlayerHits++;
            else if(hit.Outcome==ProjectileOutcome.Creature)CreatureHits++;
            if(hit.Outcome==ProjectileOutcome.Player){var direction=shot.Direction;var point=shot.Point-System.Numerics.Vector3.Normalize(direction)*2;HitPlayer(shot.Damage,new(point.X,point.Y,point.Z));}
            else if(hit.Outcome==ProjectileOutcome.Creature&&hit.Target!=null)HitCreature(hit.Target,shot.Damage);
        }
    }
    public override void _ExitTree(){arrow.Dispose();bolt.Dispose();arrowMaterial.Dispose();boltMaterial.Dispose();}
}
