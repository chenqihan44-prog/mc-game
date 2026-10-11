using System;
using EmberLighthouse.Core;

namespace EmberLighthouse.World;

public sealed class WeatherState
{
    // 0: clear, 1: cloudy, 2: precipitation. Climate determines rain/snow locally.
    public int Phase {get;set;}
    public int Cycle {get;set;}
    public double Remaining {get;set;}=180;
    public double Blend {get;set;}
    public void Advance(double seconds,int seed)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        while(seconds>0){
            double step=Math.Min(seconds,Remaining);Remaining-=step;seconds-=step;
            double target=Phase==2?1:Phase==1?.45:0;
            Blend=Math.Clamp(Blend+Math.Clamp(target-Blend,-step/12,step/12),0,1);
            if(Remaining<=0){Cycle++;Phase=Phase==0?1:Phase==1?2:0;Remaining=Phase==0?180+RandomField.Hash(seed,Cycle,0,0,31)%181:Phase==1?30:120+RandomField.Hash(seed,Cycle,0,0,32)%121;}
        }
    }
    public void Validate()
    {
        if(Phase<0||Phase>2||Cycle<0||!double.IsFinite(Remaining)||Remaining<=0||Remaining>600||!double.IsFinite(Blend)||Blend<0||Blend>1)throw new InvalidOperationException("天气存档状态无效");
    }
    public string Describe(int biome)=>Phase==0?"晴":Phase==1?"多云":biome==3?"雪":biome==2?"阴": "雨";
}
