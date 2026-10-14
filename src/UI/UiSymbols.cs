using System.Collections.Generic;
using Godot;
namespace EmberLighthouse.UI;
public static class UiSymbols
{
    private static readonly Dictionary<string,Texture2D> cache=[];
    public static Texture2D Texture(string key){if(cache.TryGetValue(key,out var texture))return texture;string body=key switch{
        "compass" or "world"=>"<circle cx='16' cy='16' r='12'/><path d='M16 1v5M16 26v5M1 16h5M26 16h5M21 9l-3 9-9 5 4-10z'/>",
        "folder" or "play"=>"<path d='M3 9V6h10l3 4h13v17H3zM3 12h26l-3 15H3'/>",
        "settings"=>"<path d='M13 2h6l1 5 4-1 4 5-3 4 3 4-4 5-4-1-1 5h-6l-1-5-4 1-4-5 3-4-3-4 4-5 4 1z'/><circle cx='16' cy='15' r='5'/>",
        "book"=>"<path d='M16 27V6C11 3 6 3 2 5v21c5-2 9-2 14 1zM16 27V6c5-3 10-3 14-1v21c-5-2-9-2-14 1z'/>",
        "people"=>"<circle cx='16' cy='9' r='5'/><circle cx='5' cy='12' r='3'/><circle cx='27' cy='12' r='3'/><path d='M8 29v-6c0-11 16-11 16 0v6zM1 28v-7c0-4 5-6 8-3M31 28v-7c0-4-5-6-8-3'/>",
        "back"=>"<path d='M13 7L4 15l9 8M5 15h16c10 0 10 12 0 12h-8'/>",
        "exit"=>"<path d='M4 3h15v26H4zM19 16h11l-5-5m5 5-5 5'/>",
        "trash" or "delete"=>"<path d='M4 8h24M12 8V4h8v4M7 8l1 20h16l1-20M13 12v12M19 12v12'/>",
        "backup" or "save"=>"<ellipse cx='16' cy='6' rx='11' ry='4'/><path d='M5 6v18c0 6 22 6 22 0V6M5 14c0 6 22 6 22 0M5 21c0 6 22 6 22 0'/>",
        "sound"=>"<path d='M3 12h6l8-7v22l-8-7H3zM22 10q7 6 0 12M25 5q12 11 0 22'/>",
        "mouse"=>"<rect x='7' y='5' width='18' height='25' rx='9'/><path d='M16 1v13M7 14h18'/>",
        "eye"=>"<path d='M2 16q14-18 28 0-14 18-28 0z'/><circle cx='16' cy='16' r='5'/>",
        "font"=>"<path d='M3 7V3h26v4M16 3v26M10 29h12M23 14h8M27 14v15'/>",
        "display"=>"<rect x='2' y='3' width='28' height='20' rx='2'/><path d='M16 23v6M10 29h12'/>",
        "cube"=>"<path d='M16 2L3 9v14l13 7 13-7V9zM3 9l13 7 13-7M16 16v14'/>",
        "music"=>"<path d='M11 25V7l16-4v18M11 12l16-4'/><ellipse cx='7' cy='25' rx='4' ry='3'/><ellipse cx='23' cy='21' rx='4' ry='3'/>",
        "leaf"=>"<path d='M4 28c2-12 7-17 24-25 0 18-9 27-20 21M4 28L22 10'/>",
        "keyboard"=>"<rect x='1' y='7' width='30' height='19' rx='3'/><path d='M6 12h2m3 0h2m3 0h2m3 0h2m3 0h2M6 17h2m3 0h2m3 0h2m3 0h2M7 22h18'/>",
        "down"=>"<path d='M7 12l9 9 9-9'/>",
        "knob"=>"<circle cx='16' cy='16' r='12' fill='#fff1ce'/>",
        "knob-off"=>"<circle cx='16' cy='16' r='10' fill='#918675'/>",
        "check-on"=>"<rect x='5' y='5' width='22' height='22' rx='4' fill='#b98332'/><path d='M9 16l5 5 9-11'/>",
        "check-off"=>"<rect x='5' y='5' width='22' height='22' rx='4'/>",
        "switch-on"=>"<rect x='1' y='7' width='30' height='18' rx='9' fill='#edaf42'/><circle cx='22' cy='16' r='7' fill='#fff1ce'/>",
        "switch-off"=>"<rect x='1' y='7' width='30' height='18' rx='9' fill='#24313a'/><circle cx='10' cy='16' r='7' fill='#b5a88c'/>",
        "connection"=>"<circle cx='16' cy='5' r='4'/><circle cx='5' cy='26' r='4'/><circle cx='27' cy='26' r='4'/><path d='M16 9v8M5 22v-5h22v5'/>",
        _=>"<path d='M16 2l5 10 10 4-10 4-5 10-5-10-10-4 10-4z'/>"};
        using var image=new Image();image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32' viewBox='0 0 32 32'><g fill='none' stroke='#ffe6ac' stroke-width='2' stroke-linejoin='round' stroke-linecap='round'>"+body+"</g></svg>");texture=ImageTexture.CreateFromImage(image);cache[key]=texture;return texture;
    }
}
