using System;
using System.Linq;
using EmberLighthouse.Core;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse.Rendering;

public partial class NearLightPool : Node3D
{
    private readonly OmniLight3D[] lights=new OmniLight3D[6];
    private double timer;
    private Cell[] selected=[];
    public int ActiveCount=>lights.Count(light=>light.Visible);
    public override void _Ready()
    {
        for(int i=0;i<lights.Length;i++){lights[i]=new(){Visible=false,ShadowEnabled=false,OmniRange=9,LightColor=new(1,.76f,.44f),LightEnergy=1.3f};AddChild(lights[i]);}
    }
    public void Tick(double delta,WorldState world,Vector3 position,Func<Vector3,bool> terrainReady)
    {
        timer-=delta;
        if(timer<=0){timer=.35;
            var sources=world.State.Entities.Where(p=>p.Value.Kind is "lantern" or "campfire" || p.Value.Kind=="furnace"&&p.Value.RecipeId!=null).Select(p=>Cell.Parse(p.Key));
            sources=sources.Concat(world.CaptureRegion((int)position.X,(int)position.Z,18).Where(p=>world.Catalog.Id(p.Value)=="lantern").Select(p=>p.Key));
            sources=sources.Concat(world.State.Beacons.Select(index=>{var p=Terrain.Towers[index];return p with{Y=world.Terrain.Surface(p.X,p.Z)+1};}));
            selected=sources.Distinct().Where(p=>Center(p).DistanceSquaredTo(position)<18*18).OrderBy(p=>Center(p).DistanceSquaredTo(position)).Where(p=>terrainReady(Center(p))).Take(lights.Length).ToArray();
        }
        for(int i=0;i<lights.Length;i++){
            var light=lights[i];light.Visible=i<selected.Length;if(!light.Visible)continue;
            var p=selected[i];light.Position=Center(p);string id=world.Catalog.Id(world.Get(p));bool beacon=id=="beacon";
            light.LightColor=beacon?new(.36f,.9f,1):new(1,.76f,.44f);
            light.OmniRange=beacon?12:9;
            light.LightEnergy=beacon?1.8f:1.3f+(float)Math.Sin(world.State.Time*9+p.X*3+p.Z)*.08f;
        }
    }
    private static Vector3 Center(Cell p)=>new(p.X+.5f,p.Y+.7f,p.Z+.5f);
}
