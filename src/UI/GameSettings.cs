using System.Collections.Generic;
namespace EmberLighthouse.UI;
public sealed class GameSettings
{
    public float Sensitivity {get;set;}=.0023f;
    public float Fov {get;set;}=78;
    public int ViewDistance {get;set;}=4;
    public int FpsLimit {get;set;}=60;
    public float Volume {get;set;}=.4f;
    public float EffectsVolume {get;set;}=1;
    public float AmbientVolume {get;set;}=.7f;
    public float MusicVolume {get;set;}=.3f;
    public float UiScale {get;set;}=1;
    public bool Fullscreen {get;set;}
    public int WindowWidth {get;set;}=1280;
    public int WindowHeight {get;set;}=720;
    public bool Shadows {get;set;}
    public bool DamageFlash {get;set;}=true;
    public bool WeatherEffects {get;set;}=true;
    public Dictionary<string,long> Keys {get;set;}=[];
}
