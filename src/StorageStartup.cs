using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.UI;
using Godot;
using static EmberLighthouse.UI.UiFactory;

namespace EmberLighthouse;

public partial class Game
{
    private bool startupReady;
    private int startupAttempts;
    private string startupUserRoot="";

    private bool TryInitializeStorage()
    {
        if(startupReady)return true;
        startupAttempts++;
        string attemptedPath=startupUserRoot;
        GameSettings loadedSettings=new();
        try{
            VerifyWritableDirectory(attemptedPath);
            attemptedPath=Path.Combine(startupUserRoot,"worlds");VerifyWritableDirectory(attemptedPath);
            attemptedPath=Path.Combine(startupUserRoot,"settings.json");
            if(Directory.Exists(attemptedPath))throw new IOException("设置文件路径被目录占用");
            if(File.Exists(attemptedPath)){
                // Check existing settings without truncating or rewriting them.
                using var stream=new FileStream(attemptedPath,FileMode.Open,System.IO.FileAccess.ReadWrite,FileShare.Read);
                using var reader=new StreamReader(stream);
                string json=reader.ReadToEnd();
                try{loadedSettings=JsonSerializer.Deserialize<GameSettings>(json,Catalog.JsonOptions)??new();}
                catch(JsonException error){GD.PushWarning("设置文件格式无效，使用默认设置："+error.Message);}
            }
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException){ShowStorageStartupError(attemptedPath,error.Message);return false;}
        saves=new(Path.Combine(startupUserRoot,"worlds"),catalog);
        settingsPath=Path.Combine(startupUserRoot,"settings.json");settings=loadedSettings;
        saveQueue=new(snapshot=>saves.Save(snapshot));
        defaultKeys["inventory_transfer"]=(Key.Shift,"背包快捷移动");
        settings.Keys??=[];if(!settings.Keys.ContainsKey("swap_hands")&&settings.Keys.GetValueOrDefault("fly")== (long)Key.F)settings.Keys["swap_hands"]=(long)Key.H;
        RegisterInput();ShowMenu();ApplySettings(true);startupReady=true;return true;
    }
    private static void VerifyWritableDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        // A unique temporary probe never overwrites a user's file. The OS removes
        // it on close, including an exception during the flush.
        using var probe=new FileStream(Path.Combine(directory,".startup-probe-"+Guid.NewGuid().ToString("N")+".tmp"),FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.None,4096,FileOptions.DeleteOnClose);
        probe.WriteByte(0);probe.Flush(true);
    }
    private void ShowStorageStartupError(string path,string reason)
    {
        SetScreen("startup-error");var column=Screen(overlay,"无法访问存档目录");
        void Explanation(string text,int size=17){var label=Label(text,size);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;column.AddChild(label);}
        Explanation("请确认目录及设置文件可读写、磁盘空间充足，并关闭正在占用文件的程序，然后点击“重试”。");
        var scroll=Scroll(column,180);scroll.HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled;
        var details=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(details);
        foreach(string text in new[]{"访问路径："+path,"原因："+reason}){var label=Label(text,15);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;details.AddChild(label);}
        column.AddChild(Button("重试",()=>TryInitializeStorage()));column.AddChild(Button("退出",()=>QuitCleanly()));
    }
}
