using System;
namespace EmberLighthouse.World;

public static class DayCycle
{
    public const double Duration=1200;
    public const double NightSpeed=2;
    public const double RealCycleSeconds=Duration/2+Duration/2/NightSpeed;
    public static double Phase(double time)=>time%Duration;
    public static bool Night(double time)=>Phase(time)>=Duration/2;
    public static double Elevation(double time)=>Math.Sin(Phase(time)/Duration*Math.Tau);
    public static double Advance(double time,double seconds)
    {
        double phase=Phase(time),elapsed=Math.Floor(time/Duration)*RealCycleSeconds+(phase<Duration/2?phase:Duration/2+(phase-Duration/2)/NightSpeed)+seconds;
        double cycle=Math.Floor(elapsed/RealCycleSeconds),within=elapsed%RealCycleSeconds;
        return cycle*Duration+(within<Duration/2?within:Duration/2+(within-Duration/2)*NightSpeed);
    }
    public static double SecondsUntilBoundary(double time)=>Night(time)?(Duration-Phase(time))/NightSpeed:Duration/2-Phase(time);
    public static int ClockMinutes(double time)=>((int)(Phase(time)/Duration*1440)+360)%1440;
    public static double Daylight(double time)
    {
        double blend=Math.Clamp((Elevation(time)+.16)/.46,0,1);return blend*blend*(3-2*blend);
    }
    public static double Twilight(double time)=>Math.Pow(1-Math.Clamp(Math.Abs(Elevation(time))/.38,0,1),2);
}
