using System;
using System.Numerics;
using EmberLighthouse.Core;
using EmberLighthouse.World;

namespace EmberLighthouse.Creatures;

public static class CombatGeometry
{
    public static Cell CellAt(Vector3 p)=>new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Floor(p.Z));
    public static bool Blocks(WorldState world,Vector3 point)
    {
        var p=CellAt(point);var def=world.Catalog.Block(world.Get(p));if(def==null||!def.Solid)return false;
        if(def.Id=="wood"&&world.Terrain.IsLooseWood(p)&&!world.Changes.ContainsKey(p)&&!world.State.Entities.ContainsKey(p.ToString()))return false;
        Vector3 local=point-new Vector3(p.X,p.Y,p.Z);world.State.Entities.TryGetValue(p.ToString(),out var entity);
        int rotation=(entity?.Rotation??0)+(def.Shape=="door"&&entity?.Open==true?1:0);
        for(int i=0;i<rotation%4;i++){float x=local.X;local.X=local.Z;local.Z=1-x;}
        return def.Shape switch{
            "slab"=>local.Y<.5f,"stairs"=>local.Y<.5f||local.Z>=.5f,
            "door" or "window"=>local.Z<=.15f,
            "fence"=>(local.X>=.35f&&local.X<=.65f&&local.Z>=.35f&&local.Z<=.65f)||(local.Z>=.43f&&local.Z<=.57f&&(local.Y>=.3f&&local.Y<=.5f||local.Y>=.75f&&local.Y<=.9f)),
            _=>true
        };
    }
    public static bool Clear(WorldState world,Vector3 from,Vector3 to,float radius=0)
    {
        int steps=Math.Max(1,(int)Math.Ceiling(Vector3.Distance(from,to)/.075f));
        for(int i=0;i<=steps;i++)if(Blocked(world,Vector3.Lerp(from,to,i/(float)steps),radius))return false;
        return true;
    }
    public static bool Blocked(WorldState world,Vector3 point,float radius)
    {
        if(Blocks(world,point))return true;if(radius<=0)return false;
        return Blocks(world,point+Vector3.UnitX*radius)||Blocks(world,point-Vector3.UnitX*radius)||Blocks(world,point+Vector3.UnitY*radius)||Blocks(world,point-Vector3.UnitY*radius)||Blocks(world,point+Vector3.UnitZ*radius)||Blocks(world,point-Vector3.UnitZ*radius);
    }
    public static bool BodyHit(Vector3 point,Vector3 feet,float height,float bodyRadius,float projectileRadius)
    {
        float dx=point.X-feet.X,dz=point.Z-feet.Z,r=bodyRadius+projectileRadius;
        return dx*dx+dz*dz<=r*r&&point.Y>=feet.Y-projectileRadius&&point.Y<=feet.Y+height+projectileRadius;
    }
    public static bool InArea(Vector3 feet,Vector3 aim,float radius)=>Math.Abs(feet.Y-aim.Y)<1.5f&&new Vector2(feet.X-aim.X,feet.Z-aim.Z).LengthSquared()<=radius*radius;
}
