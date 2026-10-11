using System;
namespace EmberLighthouse.Audio;

public static class ProceduralAudio
{
    public const int Rate=22050;
    public static byte[] Create(string cue,int variant=0)
    {
        bool surface=cue.StartsWith("step_")||cue.StartsWith("break_")||cue.StartsWith("hit_")||cue.StartsWith("place_");
        string material=surface?cue[(cue.IndexOf('_')+1)..]:"";
        bool loop=cue is "wind" or "fire";bool music=cue.StartsWith("music_");bool voice=cue.StartsWith("warn_")||cue.StartsWith("idle_");
        double duration=surface?(cue.StartsWith("break_")?.24:cue.StartsWith("step_")?.13:.15):loop?3:music?18:voice?.55:cue=="death"?.7:cue=="craft"?.24:cue=="birds"?1.5:.16;
        int samples=(int)(duration*Rate);var bytes=new byte[samples*2];int seed=19;foreach(char c in cue)seed=unchecked(seed*31+c);seed=unchecked(seed+Math.Clamp(variant,0,3)*7919);var random=new Random(seed);double low=0,body=0,previousNoise=0;
        int[] progression=cue=="music_night"?[0,7,10,14,7,3,10,0,7]:[0,7,12,4,9,7,4,12,0];
        for(int i=0;i<samples;i++){
            double t=i/(double)Rate,n=random.NextDouble()*2-1;low=low*.9+n*.1;double value=0;
            if(loop){double seam=Math.Sin(Math.PI*i/(samples-1.0));value=cue=="wind"?low*.26:(low*.22+(random.NextDouble()>.994?n*.3:0));value*=seam*seam;}
            else if(music){int note=(int)(t/2);double local=t%2,attack=Math.Min(1,local/.12),decay=Math.Exp(-local*1.9);double hz=130.81*Math.Pow(2,progression[note%progression.Length]/12.0);value=(Math.Sin(t*Math.Tau*hz)+Math.Sin(t*Math.Tau*hz*2)*.25)*attack*decay*.11;value*=Math.Min(1,(duration-t)/.1);}
            else if(cue=="birds"){double local=t%.3,hz=2200+Math.Sin(local*25)*650;value=local<.1?Math.Sin(t*Math.Tau*hz)*Math.Sin(local/.1*Math.PI)*.18:0;}
            else if(voice){double hz=cue.Contains("guardian")?65:cue.Contains("skitter")?390:cue.Contains("sheep")?180:cue.Contains("boar")?100:130;double pitch=hz*(1+.16*Math.Sin(t*28));value=(Math.Sin(t*Math.Tau*pitch)*.23+Math.Sin(t*Math.Tau*pitch*2)*.11+low*.25)*Math.Sin(Math.PI*t/duration);}
            else{
                double env=Math.Min(1,t/.004)*Math.Pow(1-t/duration,2);double hz=cue=="ui"?800:cue=="pickup"?650:cue=="eat"?160:cue=="door"?190:cue=="craft"?330:cue=="hurt"?82:cue=="death"?90:cue=="shield"?245:cue=="attack"?145:120;
                if(surface){
                    // Filtered impact and small irregular grains, without a sustained
                    // oscillator. Each material has a different transient and decay.
                    body=body*.965+n*.035;
                    double crisp=n-previousNoise;previousNoise=n;
                    double attack=Math.Min(1,t/.002),decay=Math.Exp(-t/(material=="wood"?.022:material=="stone"?.012:.037));
                    double tail=Math.Pow(Math.Max(0,1-t/duration),2);
                    double grain=(.45+.55*Math.Pow(Math.Sin(t*(variant+3)*137),2))*Math.Exp(-t/.07);
                    value=material switch{
                        "wood"=>(body*1.5+low*.65)*decay+crisp*.075*grain,
                        "stone"=>low*.7*decay+crisp*.16*grain,
                        "grass"=>body*.65*decay+crisp*.10*grain+low*.25*tail,
                        "sand"=>body*.65*decay+n*.16*grain,
                        "snow"=>body*.8*decay+n*.07*tail,
                        "water"=>low*.8*tail+n*.035*grain,
                        _=>low*.7*decay+crisp*.16*grain
                    };
                    if(cue.StartsWith("break_"))value=value*1.35+crisp*.09*grain;
                    else if(cue.StartsWith("hit_"))value*=.65;
                    else if(cue.StartsWith("place_"))value*=.85;
                    value*=attack*tail;
                }else if(cue=="attack")value=(n*.23+low*.38)*env;
                else if(cue=="eat")value=(n*.27+Math.Sin(t*Math.Tau*hz)*.12)*env;
                else if(cue=="craft")value=(Math.Sin(t*Math.Tau*hz)+n*.4)*env*.26*(Math.Sin(t*90)>.1?1:.25);
                else value=(Math.Sin(t*Math.Tau*(cue=="death"?hz*(1-t/duration*.6):hz))*.3+low*.12)*env;
            }
            short pcm=(short)(Math.Clamp(value,-.8,.8)*32767);bytes[i*2]=(byte)(pcm&255);bytes[i*2+1]=(byte)(pcm>>8);
        }
        return bytes;
    }
    public static string Material(string id)=>id switch{
        "wood" or "plank" or "stairs" or "slab" or "door" or "fence" or "chest" or "workbench" or "bookshelf"=>"wood",
        "grass" or "dirt" or "leaves" or "berry_bush" or "farmland" or "moss"=>"grass",
        "sand" or "gravel" or "sandstone"=>"sand","snow" or "ice"=>"snow","water"=>"water",_=>"stone"
    };
}
