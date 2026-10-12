using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse;

public partial class Game
{
    private DirectionalLight3D moon=null!;
    private float UpdateDaylight()
    {
        double time=world!.State.SkyTime;float day=(float)DayCycle.Daylight(time),twilight=(float)DayCycle.Twilight(time),overcast=(float)world.State.Weather.Blend;
        sun.RotationDegrees=new((float)(-DayCycle.Phase(time)/DayCycle.Duration*360),-28,0);
        float skyExposure=renderer?.SkyAt(player!.Camera.GlobalPosition)??0;
        renderer?.SetDaylight(day*(1-overcast*.35f));
        sun.LightEnergy=day*(.18f+.95f*Mathf.Max(0,(float)DayCycle.Elevation(time)))*(1-overcast*.5f)*skyExposure;
        sun.LightColor=new Color("fff3db").Lerp(new("ffa06a"),twilight*.85f);
        moon.RotationDegrees=sun.RotationDegrees+new Vector3(180,0,0);moon.LightEnergy=.022f*(1-day)*(1-overcast*.65f)*skyExposure;
        var sky=new Color("030611").Lerp(new("8bbce2"),day).Lerp(new("bc7976"),twilight*.6f);
        environment.Environment.AmbientLightEnergy=.0025f+(.007f+day*.638f)*Mathf.Pow(skyExposure,1.6f);environment.Environment.AmbientLightColor=new Color("758abc").Lerp(new("c4d1df"),day);
        environment.Environment.BackgroundColor=sky.Lerp(new Color("060a17").Lerp(new("667587"),day),overcast*.65f);
        return day;
    }
}
