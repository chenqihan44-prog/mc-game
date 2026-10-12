using System;
using System.Collections.Generic;
using EmberLighthouse.Core;

namespace EmberLighthouse.World;

public static class MapExploration
{
    public const int CellSize=8,Radius=40,Width=Coordinates.Limit*2/CellSize,Count=Width*Width;
    private static int Index(int x,int z)=>(x+Coordinates.Limit/CellSize)+(z+Coordinates.Limit/CellSize)*Width;
    public static bool Known(IReadOnlySet<int> explored,int x,int z)=>x>=-Coordinates.Limit&&x<Coordinates.Limit&&z>=-Coordinates.Limit&&z<Coordinates.Limit&&explored.Contains(Index(Coordinates.FloorDiv(x,CellSize),Coordinates.FloorDiv(z,CellSize)));
    public static bool Reveal(HashSet<int> explored,double x,double z)
    {
        if(!double.IsFinite(x)||!double.IsFinite(z)||x<-Coordinates.Limit||x>=Coordinates.Limit||z<-Coordinates.Limit||z>=Coordinates.Limit)return false;
        bool changed=false;int min=-Coordinates.Limit/CellSize,max=Coordinates.Limit/CellSize-1;
        for(int gz=Math.Max(min,(int)Math.Floor((z-Radius)/CellSize));gz<=Math.Min(max,(int)Math.Floor((z+Radius)/CellSize));gz++)
            for(int gx=Math.Max(min,(int)Math.Floor((x-Radius)/CellSize));gx<=Math.Min(max,(int)Math.Floor((x+Radius)/CellSize));gx++){
                double dx=gx*CellSize+CellSize/2d-x,dz=gz*CellSize+CellSize/2d-z;
                if(dx*dx+dz*dz<=Radius*Radius)changed|=explored.Add(Index(gx,gz));
            }
        return changed;
    }
}
