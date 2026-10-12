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
    private OmniLight3D heldLight=null!;
    public int ActiveCount=>lights.Count(light=>light.Visible);
    public override void _Ready()
    {
        for(int i=0;i<lights.Length;i++){lights[i]=new(){Visible=false,ShadowEnabled=true,ShadowBlur=2,OmniRange=15,OmniAttenuation=1.5f,LightColor=new(1,.76f,.44f),LightEnergy=1.8f};AddChild(lights[i]);}
        heldLight=new(){Visible=false,ShadowEnabled=false,OmniRange=8,OmniAttenuation=.6f,LightColor=new(1,.72f,.35f),LightEnergy=3.2f};AddChild(heldLight);
    }
    public void Tick(double delta,WorldState world,Vector3 position,Func<Vector3,bool> terrainReady)
    {
        timer-=delta;
        if(timer<=0){timer=.35;
            var sources=world.State.Entities.Where(p=>p.Value.Kind is "torch" or "lantern" or "campfire" or "dawn_heart" || p.Value.Kind=="furnace"&&p.Value.RecipeId!=null).Select(p=>Cell.Parse(p.Key));
            sources=sources.Concat(world.CaptureRegion((int)position.X,(int)position.Z,18).Where(p=>world.Catalog.Id(p.Value) is "torch" or "lantern" or "dawn_heart").Select(p=>p.Key));
            if(world.Terrain.Adventure is {} a){var site=a.Nearby(new((int)position.X,(int)position.Y,(int)position.Z));if(site!=null){var generated=Enumerable.Range(-site.Radius,site.Radius*2+1).Where(z=>z%8==0).SelectMany(z=>new[]{site.At(-site.Radius+3,4,z),site.At(site.Radius-3,4,z)});sources=sources.Concat(generated.Where(p=>world.Catalog.Id(world.Get(p))=="lantern"));}sources=sources.Concat(a.Devices.Where(d=>d.Value is "energy_anchor" or "core_console" or "reflector").Select(d=>d.Key));}
            sources=sources.Concat(world.State.Beacons.Select(index=>{var p=world.Terrain.TowerLocations[index];return p with{Y=world.Terrain.Surface(p.X,p.Z)+1};}));
            selected=sources.Distinct().Where(p=>Center(p).DistanceSquaredTo(position)<18*18).OrderBy(p=>Center(p).DistanceSquaredTo(position)).Where(p=>terrainReady(Center(p))).Take(lights.Length).ToArray();
        }
        for(int i=0;i<lights.Length;i++){
            var light=lights[i];light.Visible=i<selected.Length;if(!light.Visible)continue;
            var p=selected[i];var flame=world.State.Entities.TryGetValue(p.ToString(),out var entity)&&entity.Kind=="torch"?TorchMount.Flame(p,entity.Stage):new System.Numerics.Vector3(p.X+.5f,p.Y+.7f,p.Z+.5f);light.Position=new(flame.X,flame.Y,flame.Z);string id=world.Catalog.Id(world.Get(p));bool beacon=id=="beacon";
            light.LightColor=beacon||id=="reflector"?new(.36f,.9f,1):new(1,.76f,.44f);
            light.OmniRange=beacon?16:15;
            light.LightEnergy=beacon?2.8f:1.8f+(float)Math.Sin(world.State.Time*9+p.X*3+p.Z)*.04f;
        }
    }
    public void Hold(Vector3 eye,bool lit){heldLight.Position=eye+new Vector3(.25f,-.25f,0);heldLight.Visible=lit;}
    private static Vector3 Center(Cell p)=>new(p.X+.5f,p.Y+.7f,p.Z+.5f);
}
