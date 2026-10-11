using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Audio;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
using EmberLighthouse.Persistence;
using EmberLighthouse.Player;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;

namespace EmberLighthouse;

public partial class Game : Node3D
{
    private Catalog catalog=null!;
    private SaveStore saves=null!;
    private SaveQueue saveQueue=null!;
    private readonly Dictionary<Task<SaveReceipt>,List<Action<bool>>> saveCallbacks=[];
    private bool saveFailed;
    private bool buildingCheck;
    private Task<object>? worldIo;
    private Action<object>? worldIoCompleted;
    private Action? worldIoFailed;
    private bool closeAfterIo;
    private WorldState? world;
    private Explorer? player;
    private ChunkRenderer? renderer;
    private Node3D? worldRoot;
    private readonly List<Creature> creatures=[];
    private CreatureDef[] creatureDefs=[];
    private Control overlay=null!,hudRoot=null!;
    private Label status=null!,hotbar=null!,prompt=null!,notice=null!,crosshair=null!;
    private MeshInstance3D outline=null!;
    private DirectionalLight3D sun=null!;
    private WorldEnvironment environment=null!;
    private WeatherView? weatherView;
    private NearLightPool? nearLights;
    private ProjectilePool? projectiles;
    private GameSettings settings=new();
    private string settingsPath="",screen="menu",message="",lastSaved="尚未保存";
    private int selected,rotation,tutorial;
    private double noticeTime,saveClock,survivalClock,attackCooldown,mineProgress,smokeClock;
    private Cell? miningCell;
    private bool smoke,visualCheck;
    private bool interactionCheck,creatureCheck,combatCheck,actionsArmed;
    private bool quitting;
    private double populationClock;
    private bool IsValidation=>smoke||visualCheck||soak||saveUiCheck||buildingCheck||audioCheck||interactionCheck||creatureCheck||combatCheck||routeCheck||boundaryCheck||creaturePathCheck||feedbackCheck||equipmentCheck||estateCheck||storageStartupCheck||presentationCheck;
    private Cell? currentContainer;
    private string recipeQuery="";
    private string currentStation="hand";
    private GameAudio audio=null!;
    private string? binding;
    private MinecraftHud minecraftHud=null!;
    private DamageFeedback damageFeedback=null!;
    private MiniMap miniMap=null!;
    private HeldItemView heldView=null!;
    private MiningProgress miningBar=null!;
    private MiningFeedback? miningFeedback;
    private bool debugHud;
    private readonly Dictionary<Grave,MeshInstance3D> graveMarkers=[];
    private readonly Dictionary<Grave,float> graveFallSpeed=[];
    private readonly Dictionary<string,(Key Key,string Name)> defaultKeys=new(){
        ["forward"]=(Key.W,"前进"),["back"]=(Key.S,"后退"),["left"]=(Key.A,"左移"),["right"]=(Key.D,"右移"),["jump"]=(Key.Space,"跳跃 / 上浮"),["crouch"]=(Key.Shift,"蹲伏 / 下降"),["sprint"]=(Key.Ctrl,"疾跑"),["inventory"]=(Key.E,"背包"),["interact"]=(Key.Q,"使用 / 回收背包"),["craft"]=(Key.C,"随身制作"),["pause"]=(Key.Escape,"暂停"),["rotate"]=(Key.R,"旋转建筑"),["map"]=(Key.M,"地图"),["fly"]=(Key.V,"创造飞行"),["swap_hands"]=(Key.F,"交换主副手"),["drop"]=(Key.G,"放入拾取背包")};
    public override void _Ready()
    {
        // Native startup regression runs before UI, input capture or user saves.
        if(OS.GetCmdlineUserArgs().Contains("--startup-settings-check")){
            SetProcess(false);SetPhysicsProcess(false);SetProcessInput(false);SetProcessUnhandledInput(false);
            bool logging=ProjectSettings.GetSettingWithOverride("debug/file_logging/enable_file_logging").AsBool();
            GD.Print("STARTUP_SETTINGS: "+JsonSerializer.Serialize(new{effectiveFileLogging=logging,baseFileLogging=ProjectSettings.GetSetting("debug/file_logging/enable_file_logging").AsBool(),pcFileLogging=ProjectSettings.GetSetting("debug/file_logging/enable_file_logging.pc").AsBool(),pcFeature=OS.HasFeature("pc"),headless=DisplayServer.GetName()=="headless",userDataDir=OS.GetUserDataDir()},Catalog.JsonOptions));
            GetTree().Quit(logging?10:0);return;
        }
        smoke=OS.GetCmdlineUserArgs().Contains("--smoke");visualCheck=OS.GetCmdlineUserArgs().Contains("--visual-check");soak=OS.GetCmdlineUserArgs().Contains("--soak");
        saveUiCheck=OS.GetCmdlineUserArgs().Contains("--save-ui-check");buildingCheck=OS.GetCmdlineUserArgs().Contains("--building-check");
        audioCheck=OS.GetCmdlineUserArgs().Contains("--audio-check");
        interactionCheck=OS.GetCmdlineUserArgs().Contains("--interaction-check");
        creatureCheck=OS.GetCmdlineUserArgs().Contains("--creature-check");
        combatCheck=OS.GetCmdlineUserArgs().Contains("--combat-check");
        routeCheck=OS.GetCmdlineUserArgs().Contains("--route-check");
        boundaryCheck=OS.GetCmdlineUserArgs().Contains("--boundary-check");
        creaturePathCheck=OS.GetCmdlineUserArgs().Contains("--creature-path-check");
        feedbackCheck=OS.GetCmdlineUserArgs().Contains("--feedback-check");
        equipmentCheck=OS.GetCmdlineUserArgs().Contains("--equipment-check");
        estateCheck=OS.GetCmdlineUserArgs().Contains("--estate-check");
        storageStartupCheck=OS.GetCmdlineUserArgs().Contains("--storage-startup-check");
        presentationCheck=OS.GetCmdlineUserArgs().Contains("--presentation-check");
        gcStress=IsValidation&&OS.GetCmdlineUserArgs().Contains("--gc-stress");
        if(IsValidation&&DisplayServer.GetName()!="headless")GetWindow().Unfocusable=true;
        catalog=new(JsonSerializer.Deserialize<ItemDef[]>(FileAccessText("res://data/items.json"),Catalog.JsonOptions)!,JsonSerializer.Deserialize<Recipe[]>(FileAccessText("res://data/recipes.json"),Catalog.JsonOptions)!);
        creatureDefs=JsonSerializer.Deserialize<CreatureDef[]>(FileAccessText("res://data/creatures.json"),Catalog.JsonOptions)!;
        CreatureRules.Validate(creatureDefs,catalog);
        string userRoot=IsValidation?ProjectSettings.GlobalizePath("res://test-results/runtime"):OS.GetUserDataDir();
        if(storageStartupCheck){userRoot=PrepareStorageStartupFixture();RunStorageStartupCheck();}
        var canvas=new CanvasLayer();AddChild(canvas);var root=new Control{Theme=UiFactory.Theme(),MouseFilter=Control.MouseFilterEnum.Ignore};root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);canvas.AddChild(root);
        hudRoot=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};hudRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(hudRoot);
        overlay=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.AddChild(overlay);
        audio=new();AddChild(audio);
        BuildHud();GetTree().AutoAcceptQuit=false;startupUserRoot=userRoot;
        if(!TryInitializeStorage())return;
        if(smoke||visualCheck)StartWorld(new(){Name="自动场景检查",Seed=42,Creative=!visualCheck,Casual=visualCheck});
        if(soak){settings.ViewDistance=8;StartWorld(new(){Name="自动耐久检查",Seed=42,Creative=true});InitializeValidation();}
        if(saveUiCheck)StartSaveUiCheck();
        if(buildingCheck)StartBuildingCheck();
        if(audioCheck)StartAudioCheck();
        if(interactionCheck)RunInteractionCheck();
        if(creatureCheck)RunCreatureCheck();
        if(combatCheck)RunCombatCheck();
        if(routeCheck)RunSurvivalRouteCheck();
        if(boundaryCheck)RunBoundaryCheck();
        if(creaturePathCheck)RunCreaturePathCheck();
        if(feedbackCheck)RunFeedbackCheck();
        if(equipmentCheck)RunEquipmentCheck();
        if(estateCheck)RunEstateCheck();
        if(presentationCheck)RunPresentationCheck();
        GetTree().AutoAcceptQuit=false;
    }
    private static string FileAccessText(string path){using var f=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Read);return f.GetAsText();}
    private void RegisterInput()
    {
        foreach(var p in defaultKeys){if(!InputMap.HasAction(p.Key))InputMap.AddAction(p.Key);InputMap.ActionEraseEvents(p.Key);if(!IsValidation)InputMap.ActionAddEvent(p.Key,new InputEventKey{PhysicalKeycode=(Key)settings.Keys.GetValueOrDefault(p.Key,(long)p.Value.Key)});}
        foreach(var p in new[]{("mine",MouseButton.Left),("use",MouseButton.Right)}){if(!InputMap.HasAction(p.Item1))InputMap.AddAction(p.Item1);InputMap.ActionEraseEvents(p.Item1);if(!IsValidation)InputMap.ActionAddEvent(p.Item1,new InputEventMouseButton{ButtonIndex=p.Item2});}
    }
    private void BuildHud()
    {
        heldView=new(){Catalog=catalog};hudRoot.AddChild(heldView);
        damageFeedback=new();hudRoot.AddChild(damageFeedback);
        miningBar=new();hudRoot.AddChild(miningBar);
        minecraftHud=new(){Catalog=catalog};minecraftHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);hudRoot.AddChild(minecraftHud);
        miniMap=new();hudRoot.AddChild(miniMap);
        status=Label("");status.Position=new(222,18);hudRoot.AddChild(status);
        hotbar=Label("",18);hotbar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);hotbar.OffsetTop=-152;hotbar.OffsetBottom=-130;hotbar.HorizontalAlignment=HorizontalAlignment.Center;hudRoot.AddChild(hotbar);
        prompt=Label("",18);prompt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);prompt.OffsetLeft=-330;prompt.OffsetRight=330;prompt.OffsetTop=36;prompt.OffsetBottom=90;prompt.HorizontalAlignment=HorizontalAlignment.Center;hudRoot.AddChild(prompt);
        crosshair=Label("+",24);crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);crosshair.Position-=new Vector2(8,16);crosshair.Visible=false;hudRoot.AddChild(crosshair);
        notice=Label("",18);notice.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);notice.OffsetTop=25;notice.HorizontalAlignment=HorizontalAlignment.Center;notice.MouseFilter=Control.MouseFilterEnum.Ignore;hudRoot.GetParent().AddChild(notice);
    }
    private void SetScreen(string id)
    {
        if(id!="play")damageFeedback?.Reset();
        if((screen=="inventory"&&id!="inventory")||(screen=="workbench"&&id!="workbench"))ReturnCraftingItems();
        if(id!="play"){miningFeedback?.HideCracks();miningBar.Progress=0;miningBar.QueueRedraw();heldView.SetActive(false);}
        screen=id;if(player!=null)player.Enabled=id=="play";
        weatherView?.PauseAudio(id!="play");
        audio?.PauseWorld(id!="play");
        if(!IsValidation)Input.MouseMode=id=="play"?Input.MouseModeEnum.Captured:Input.MouseModeEnum.Visible;
        actionsArmed=false;
        hudRoot.Visible=world!=null;overlay.Visible=id!="play";miningCell=null;mineProgress=0;
        if(id=="play"){foreach(var c in overlay.GetChildren()){overlay.RemoveChild(c);c.QueueFree();}}
    }
    private void ShowMenu()
    {
        SetScreen("menu");hudRoot.Visible=false;var col=Screen(overlay,"方野：余烬灯塔");
        col.AddChild(Label("在荒野筑起营地，让沉寂的灯塔再次亮起。",18));
        col.AddChild(Button("新建世界",ShowNewWorld));col.AddChild(Button("读取与备份世界",ShowWorlds));col.AddChild(Button("设置",()=>ShowSettings("menu")));col.AddChild(Button("操作与帮助",()=>ShowHelp("menu")));col.AddChild(Button("制作人员与素材",()=>ShowCredits("menu")));col.AddChild(Button("退出",()=>QuitCleanly()));
        col.AddChild(Label("开发版本 0.1 · 单人离线 · Windows / 键鼠",14));
    }
    private void ShowNewWorld()
    {
        SetScreen("new");var col=Screen(overlay,"新建世界");var name=new LineEdit{Text="我的营地",PlaceholderText="世界名称"};var seed=new LineEdit{Text="42",PlaceholderText="整数种子"};col.AddChild(name);col.AddChild(seed);
        var creative=new CheckButton{Text="创造模式：无限建筑材料，飞行，无生存损耗"};var casual=new CheckButton{Text="休闲难度：死亡保留物品，饥饿减缓"};col.AddChild(creative);col.AddChild(casual);col.AddChild(Label("标准难度：死亡留下可取回背包；多次死亡仍保留旧背包。",15));
        var error=Label("");col.AddChild(error);
        col.AddChild(Button("进入荒野",()=>{if(!int.TryParse(seed.Text,out int value)){error.Text="种子必须是 32 位整数";return;}var s=new WorldSnapshot{Name=string.IsNullOrWhiteSpace(name.Text)?"新世界":name.Text[..Math.Min(40,name.Text.Length)],Seed=value,Creative=creative.ButtonPressed,Casual=casual.ButtonPressed};StartWorld(s);}));col.AddChild(Button("返回",ShowMenu));
    }
    private void ShowWorlds()=>RunWorldIo("读取世界列表",()=>saves.List().ToArray(),DrawWorlds,ShowMenu);
    private void DrawWorlds((string Id,string Name,bool Damaged)[] worlds)
    {
        SetScreen("worlds");var col=Screen(overlay,"世界管理");var scroll=Scroll(col,340);var entries=Column(scroll);
        foreach(var entry in worlds){var row=Row(entries);row.AddChild(Label(entry.Name+(entry.Damaged?"（存档需恢复）":"")));row.AddChild(Button("读取",()=>RunWorldIo("读取世界",()=>saves.Load(entry.Id),loaded=>{StartWorld(loaded.Snapshot);if(loaded.Recovered)Toast("已从最近完整代际恢复，原文件已保留");else if(loaded.Migrated)Toast("旧存档已迁移；首次保存前会自动保留原格式备份");},ShowWorlds)));
            row.AddChild(Button("备份",()=>RunWorldIo("备份世界",()=>saves.Backup(entry.Id),path=>{Toast("已备份至 "+path);ShowWorlds();},ShowWorlds)));}
        if(worlds.Length==0)entries.AddChild(Label("尚无世界。先创建一个新世界。"));col.AddChild(Button("从备份恢复",ShowBackups));col.AddChild(Button("返回",ShowMenu));
    }
    private void StartWorld(WorldSnapshot snapshot)
    {
        try{
            damageFeedback.Reset();
            ReturnWorkbenchItems();
            if(worldRoot!=null){RemoveChild(worldRoot);worldRoot.QueueFree();}creatures.Clear();graveMarkers.Clear();graveFallSpeed.Clear();
            world=new(catalog,snapshot);miniMap.Initialize(world);ReturnCraftingItems();worldRoot=new();AddChild(worldRoot);renderer=new();worldRoot.AddChild(renderer);renderer.Initialize(world);renderer.Radius=settings.ViewDistance;
            player=new(){Model=world,TerrainReady=renderer.IsTerrainReady,Hurt=Hurt,BoundaryHit=()=>Toast("已到达世界边界"),AcceptUserInput=!IsValidation,LookMoved=heldView.Look};worldRoot.AddChild(player);
            miningFeedback=new();worldRoot.AddChild(miningFeedback);
            player.GlobalPosition=Explorer.KeepInsideWorld(new((float)snapshot.Position[0],(float)snapshot.Position[1],(float)snapshot.Position[2]));
            snapshot.Position=[player.GlobalPosition.X,player.GlobalPosition.Y,player.GlobalPosition.Z];
            var respawn=Explorer.KeepInsideWorld(new((float)snapshot.Respawn[0],(float)snapshot.Respawn[1],(float)snapshot.Respawn[2]));snapshot.Respawn=[respawn.X,respawn.Y,respawn.Z];
            audio.ResetWorld(player.GlobalPosition);
            environment=new(){Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("8aaeb1"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new("b1c4bb"),AmbientLightEnergy=.65f,TonemapMode=Godot.Environment.ToneMapper.Filmic}};worldRoot.AddChild(environment);
            sun=new(){RotationDegrees=new(-40,-28,0),LightColor=new("fff2d5"),LightEnergy=1.1f,ShadowEnabled=settings.Shadows};worldRoot.AddChild(sun);
            weatherView=new();worldRoot.AddChild(weatherView);weatherView.Initialize(world);
            nearLights=new();worldRoot.AddChild(nearLights);
            projectiles=new(){World=world,Player=player,ReadyTerrain=renderer.IsTerrainReady,HitPlayer=HurtFrom,HitCreature=(state,damage)=>creatures.FirstOrDefault(c=>ReferenceEquals(c.State,state))?.Damage(damage)};worldRoot.AddChild(projectiles);
            outline=new(){Mesh=SelectionEdges(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("252525"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded},Visible=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};worldRoot.AddChild(outline);
            CreaturePopulation.Initialize(world,creatureDefs);foreach(var c in snapshot.Creatures.ToArray())SpawnCreature(c);populationClock=0;
            tutorial=snapshot.TutorialStep;selected=0;saveClock=0;survivalClock=0;lastSaved="尚未保存";saveFailed=false;SetScreen("play");ApplySettings();Toast("WASD 移动 · 左键挖掘 · 右键放置 · E 背包 · Q 使用 · Esc 暂停");
        }catch(Exception e){GD.PushError(e);Toast("世界加载失败："+e.Message);ShowMenu();}
    }
    private void TickPopulation(double delta)
    {
        populationClock-=delta;if(populationClock>0)return;populationClock=1;
        var changes=CreaturePopulation.Tick(world!,creatureDefs,Explorer.ToCell(player!.GlobalPosition),p=>renderer!.IsTerrainReady(new(p.X+.5f,p.Y,p.Z+.5f)));
        foreach(var c in creatures.Where(c=>changes.Removed.Contains(c.State)).ToArray()){creatures.Remove(c);c.QueueFree();}
        foreach(var state in changes.Added)SpawnCreature(state);
    }
    private void SpawnCreature(CreatureState state)
    {
        var d=creatureDefs.FirstOrDefault(d=>d.Id==state.Kind);if(d==null||state.Health<=0)return;
        var c=new Creature{State=state,Definition=d,World=world!,Player=player!,ReadyTerrain=renderer!.IsTerrainReady,AttackPlayer=Hurt,Died=CreatureDied};worldRoot!.AddChild(c);creatures.Add(c);
        c.AttackPlayer=damage=>HurtFrom(damage,c.GlobalPosition);
        c.SoundCue=(cue,position)=>audio.Play(cue,position);
        c.FireProjectile=shot=>projectiles!.Fire(shot);
    }
    private void CreatureDied(Creature c)
    {
        int count=c.Definition.Id=="guardian"?1:2;var leftovers=new Inventory();leftovers.Add(c.Definition.Drop,count,catalog);world!.State.Graves.Add(new(){Position=[c.GlobalPosition.X,c.GlobalPosition.Y,c.GlobalPosition.Z],Items=leftovers});
        if(c.Definition.Id=="guardian"){world.State.GuardianDefeated=true;Toast("守护者已击败。激活三座灯塔后，网络将恢复。");}
        audio.Play("death",c.GlobalPosition);world.State.Creatures.Remove(c.State);creatures.Remove(c);c.QueueFree();
        if(!world.State.Creative)world.State.Experience+=c.Definition.Id=="guardian"?30:3;
        if(c.Definition.Id=="guardian")RequestSave();
    }
    public override void _Process(double delta)
    {
        if(!startupReady)return;
        PollSaves();
        PollWorldIo();
        if(saveUiCheck)RunSaveUiCheck();
        if(buildingCheck)RunBuildingCheck();
        if(audioCheck)RunAudioCheck();
        noticeTime-=delta;notice.Text=noticeTime>0?message:"";
        if(screen is "inventory" or "workbench"&&cursorGlyph!=null)cursorGlyph.Position=GetViewport().GetMousePosition()-Vector2.One*24;
        if(world==null||player==null||renderer==null)return;
        renderer.Stream(player.GlobalPosition);
        if(soak)RunSoak();
        if(screen!="play"){heldView.Tick(delta,null,null,Vector3.Zero,false,false);return;}
        world.Advance(delta);TickPopulation(delta);saveClock+=delta;survivalClock+=delta;attackCooldown-=delta;
        bool submerged=catalog.Id(world.Get(Explorer.ToCell(player.Camera.GlobalPosition)))=="water";
        world.State.Air=submerged?Math.Max(0,world.State.Air-delta):Math.Min(15,world.State.Air+delta*5);minecraftHud.Underwater=submerged;minecraftHud.Air=world.State.Air;
        if(submerged&&world.State.Air<=0)Hurt(delta*4);
        if(saveClock>=60&&!smoke&&!soak){saveClock=0;RequestSave();}
        if(survivalClock>=1){if(!world.State.Creative){double rate=world.State.Casual?.035:.12;world.State.Hunger=Math.Max(0,world.State.Hunger-rate*survivalClock*(Input.IsActionPressed("sprint")?2:1));if(world.State.Hunger<=0)Hurt(survivalClock*.8);else if(world.State.Hunger>65)world.State.Health=Math.Min(100,world.State.Health+survivalClock*.6);}survivalClock=0;world.Terrain.TrimCaches();}
        float daylight=(float)Math.Max(.12,Math.Sin(world.State.Time/1200*Math.Tau));float overcast=(float)world.State.Weather.Blend;
        sun.LightEnergy=daylight*(1-overcast*.5f);sun.RotationDegrees=new((float)(world.State.Time/1200*360-90),-28,0);
        environment.Environment.AmbientLightEnergy=.23f+daylight*.5f;environment.Environment.BackgroundColor=new Color("28404c").Lerp(new("8aaeb1"),daylight).Lerp(new("657581"),overcast*.65f);
        weatherView!.Tick(delta,player.Camera.GlobalPosition,daylight,1,settings.WeatherEffects);
        audio.Tick(delta,player,world,daylight);
        nearLights!.Tick(delta,world,player.GlobalPosition,renderer!.IsTerrainReady);
        UpdateHud();UpdateGraveMarkers(delta);var target=player.Target();outline.Visible=target.HasValue;if(target.HasValue)outline.Position=new(target.Value.Hit.X+.5f,target.Value.Hit.Y+.5f,target.Value.Hit.Z+.5f);
        RearmActions();
        if(actionsArmed&&Input.IsActionPressed("mine"))Mine(delta,target);else{miningCell=null;mineProgress=0;}
        miningFeedback!.Tick(delta,world,miningCell,mineProgress);miningBar.Progress=mineProgress;miningBar.QueueRedraw();
        heldView.Tick(delta,world.State.Inventory.Slots[selected],world.State.Equipment.Slots[Equipment.Offhand],player.Velocity,mineProgress>0,true);
        if(smoke||visualCheck)RunSmoke(delta);
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(!startupReady||IsValidation)return;
        if(screen is "saving" or "world-io"){GetViewport().SetInputAsHandled();return;}
        if(binding!=null&&e is InputEventKey k&&k.Pressed&&!k.Echo){settings.Keys[binding]=(long)k.PhysicalKeycode;binding=null;RegisterInput();PersistSettings();ShowSettings(world==null?"menu":"pause");GetViewport().SetInputAsHandled();return;}
        if(e.IsActionPressed("pause")){if(world!=null){if(screen=="play")ShowPause();else SetScreen("play");}return;}
        if(screen!="play"||world==null)return;
        if(e is InputEventKey debugKey&&debugKey.PhysicalKeycode==Key.F3&&debugKey.Pressed&&!debugKey.Echo)debugHud=!debugHud;
        if(e.IsActionPressed("inventory")){currentContainer=null;ShowInventory();}
        else if(e.IsActionPressed("craft")){currentStation="hand";ShowRecipes();}
        else if(e.IsActionPressed("interact"))Interact();
        else if(e.IsActionPressed("map"))ShowMap();
        else if(e.IsActionPressed("rotate")){rotation=(rotation+1)%4;Toast("放置朝向："+rotation*90+"°");}
        else if(e.IsActionPressed("drop"))DropSelected();
        else if(e.IsActionPressed("swap_hands"))SwapHands();
        else if(actionsArmed&&e.IsActionPressed("use"))Use();
        if(e is InputEventKey key&&key.Pressed&&!key.Echo&&key.Keycode>=Key.Key1&&key.Keycode<=Key.Key9)selected=(int)(key.Keycode-Key.Key1);
        if(e is InputEventMouseButton wheel&&wheel.Pressed){if(wheel.ButtonIndex==MouseButton.WheelUp)selected=(selected+8)%9;if(wheel.ButtonIndex==MouseButton.WheelDown)selected=(selected+1)%9;}
    }
    public override void _Notification(int what)
    {
        if(what==NotificationWMCloseRequest){if(worldIo!=null)closeAfterIo=true;else if(world==null)QuitCleanly();else if(screen!="saving")SaveAndLeave(()=>QuitCleanly());}
        if(what==NotificationApplicationFocusOut&&world!=null&&screen=="play"&&!IsValidation)ShowPause();
    }
    private void UpdateHud()
    {
        var s=world!.State;string mode=s.Creative?"创造":s.Casual?"休闲":"生存";int minutes=(int)(s.Time%1200/1200*1440);
        status.Visible=debugHud;status.Text=$"余烬灯塔 · {mode}   {Engine.GetFramesPerSecond()} FPS\nXYZ: {player!.GlobalPosition.X:0.00} / {player.GlobalPosition.Y:0.00} / {player.GlobalPosition.Z:0.00}\n区块 {renderer!.Loaded}  待处理 {renderer.Pending}\n时间 {minutes/60:00}:{minutes%60:00}  保存 {(saveCallbacks.Count>0?"保存中":saveFailed?"失败":lastSaved)}";
        minecraftHud.State=s;minecraftHud.Selected=selected;minecraftHud.GuiScale=3*settings.UiScale;minecraftHud.QueueRedraw();
        miniMap.Scale=Vector2.One*settings.UiScale;miniMap.UpdatePlayer(player.GlobalPosition,player.GlobalRotation.Y);status.Position=new(24+192*settings.UiScale,18);
        miniMap.WeatherText=s.Weather.Describe(world.Terrain.Biome((int)Math.Floor(player.GlobalPosition.X),(int)Math.Floor(player.GlobalPosition.Z)));
        hotbar.Text=Held()?.Name??"";
        var target=player.Target();prompt.Text="";
        if(target.HasValue){var d=catalog.Block(world.Get(target.Value.Hit))!;prompt.Text=d.Name+(d.Use!=""?" · Q 使用":"")+(mineProgress>0?$"  挖掘 {mineProgress:0%}":"");}
        string[] guide=["收集地面的原木和碎石；C 制作石镐", "制作工作台与营火，右键放置", "采掘煤与铁；熔炉冶炼后制作铁镐", "制作床与木箱，建造营地", "M 查看三座灯塔；晶矿用于灯塔符印", "击败守护者，激活所有灯塔"];
        if(!s.TutorialHidden&&tutorial<guide.Length)prompt.Text+="\n"+guide[tutorial];
        if(s.Inventory.Count("stone_pick")>0)tutorial=Math.Max(tutorial,1);if(s.Entities.Values.Any(e=>e.Kind=="campfire"))tutorial=Math.Max(tutorial,2);if(s.Inventory.Count("iron_pick")>0)tutorial=Math.Max(tutorial,3);if(s.Entities.Values.Any(e=>e.Kind=="bed"))tutorial=Math.Max(tutorial,4);if(s.Beacons.Count>0)tutorial=Math.Max(tutorial,5);if(s.Beacons.Count==3&&s.GuardianDefeated)tutorial=6;s.TutorialStep=tutorial;
    }
    private ItemDef? Held()=>world!.State.Inventory.Slots[selected] is { } s?catalog.Items[s.Id]:null;
    private ItemDef? OffhandHeld()=>world!.State.Equipment.Slots[Equipment.Offhand] is { } s?catalog.Items[s.Id]:null;
    private void SwapHands(){if(world==null||screen!="play")return;Equipment.SwapHands(world.State,selected);miningCell=null;mineProgress=0;audio.Play("ui");}
    private void Mine(double delta,(Cell Hit,Cell Before,float Distance)? target)
    {
        if(attackCooldown>0)return;
        if(Attack()){miningCell=null;mineProgress=0;return;}
        if(!target.HasValue){miningCell=null;mineProgress=0;heldView.Swing();attackCooldown=.3;return;}var p=target.Value.Hit;if(miningCell!=p){miningCell=p;mineProgress=0;}
        var def=catalog.Block(world!.Get(p))!;var tool=Held();int tier=tool?.Use is "pick" or "axe" or "shovel"?tool.Tier:0;
        float speed=tool?.Use=="pick"?1+tool.Tier*2:tool?.Use=="axe"&&def.Id=="wood"?4:1;
        mineProgress+=world.State.Creative?1:delta*speed/Math.Max(.15,def.Hardness);
        if(mineProgress>=1){var partner=world.DoorPartner(p);if(world.Break(p,tier,out var error)){miningFeedback?.Emit(p,def);heldView.Swing();renderer!.RefreshEdit(p);if(partner is Cell other)renderer.RefreshEdit(other);audio.Play("break_"+ProceduralAudio.Material(def.Id),new(p.X+.5f,p.Y+.5f,p.Z+.5f));}else Toast(error);mineProgress=0;attackCooldown=world.State.Creative?.25:.18;}
        else audio.Play("hit_"+ProceduralAudio.Material(def.Id),new(p.X+.5f,p.Y+.5f,p.Z+.5f));
    }
    private bool Attack()
    {
        var held=Held();float range=held?.Use=="bow"?25:3;Vector3 start=player!.Camera.GlobalPosition,dir=-player.Camera.GlobalTransform.Basis.Z;
        if(held?.Use=="bow"){
            if(!projectiles!.CanFire){Toast("飞行中的投射物较多，请稍候");attackCooldown=.2;return true;}
            var inventory=world!.State.Inventory.Copy();if(!world.State.Creative&&!inventory.Remove("arrow",1)){Toast("缺少箭");attackCooldown=.5;return true;}
            var origin=start+dir*.35f;var velocity=dir*22;
            if(projectiles.Fire(new(){Position=[origin.X,origin.Y,origin.Z],Velocity=[velocity.X,velocity.Y,velocity.Z],Damage=held.Power*2,Remaining=1.5,Radius=.06})){world.State.Inventory=inventory;heldView.Swing();attackCooldown=.8;audio.Play("attack");}
            return true;
        }
        var target=creatures.Where(c=>c.State.Health>0).Select(c=>(Creature:c,Projection:(c.GlobalPosition+Vector3.Up-start).Dot(dir),Distance:(c.GlobalPosition+Vector3.Up-start).Cross(dir).Length())).Where(p=>p.Projection>0&&p.Projection<range&&p.Distance<1).OrderBy(p=>p.Projection).FirstOrDefault();
        if(target.Creature==null||!target.Creature.HasLineOfSight(start))return false;
        target.Creature.Damage(held?.Use is "sword" or "bow"?held.Power*2:4);heldView.Swing();attackCooldown=held?.Use=="bow"?.8:.45;audio.Play("attack");return true;
    }
    private void Use()
    {
        if(TryUseHand(world!.State.Inventory,selected)){heldView.Swing();return;}if(TryUseHand(world.State.Equipment,Equipment.Offhand))heldView.Swing(true);
    }
    private bool TryUseHand(Inventory supply,int slot)
    {
        if(supply.Slots[slot] is not { } stack)return false;var held=catalog.Items[stack.Id];
        if(held.Use=="armor"){int armorSlot=held.ArmorSlot;if(world!.State.Equipment.Slots[armorSlot]!=null||ReferenceEquals(supply,world.State.Equipment))return false;return Inventory.Move(supply,slot,world.State.Equipment,armorSlot,1,catalog,targetAccepts:(i,s)=>Equipment.Accepts(catalog,i,s));}
        if(held.Use=="shield"){Toast("按住右键，面向敌人使用盾牌");return true;}
        if(held.Use=="food"){if(world!.State.Hunger>=100)return false;if(--stack.Count==0)supply.Slots[slot]=null;world.State.Hunger=Math.Min(100,world.State.Hunger+held.Power);audio.Play("eat");return true;}
        var target=player!.Target();if(!target.HasValue)return false;
        if(held.Use=="seed"){if(!world!.Plant(target.Value.Hit,held.Id,out var plantError,supply,slot)){Toast(plantError);return false;}renderer!.RefreshEdit(target.Value.Hit+new Cell(0,1,0));audio.Play("place_grass");return true;}
        if(!held.Block)return false;
        var p=target.Value.Before;var feet=player.GlobalPosition;bool overlap=feet.X+.3f>p.X&&feet.X-.3f<p.X+1&&feet.Z+.3f>p.Z&&feet.Z-.3f<p.Z+1&&feet.Y+(player.Crouching?1.1:1.8)>p.Y&&feet.Y<p.Y+1;
        bool upperOverlap=feet.X+.3f>p.X&&feet.X-.3f<p.X+1&&feet.Z+.3f>p.Z&&feet.Z-.3f<p.Z+1&&feet.Y+(player.Crouching?1.1:1.8)>p.Y+1&&feet.Y<p.Y+2;
        if(!world!.Place(p,held.Id,overlap,out var error,rotation,upperOverlap,supply,slot)){Toast(error);return false;}
        if(held.Id=="door")renderer!.RefreshEdit(p+new Cell(0,1,0));
        renderer!.RefreshEdit(p);audio.Play("place_"+ProceduralAudio.Material(held.Id),new(p.X+.5f,p.Y+.5f,p.Z+.5f));return true;
    }
    private void Interact()
    {
        var grave=world!.State.Graves.FirstOrDefault(g=>new Vector3((float)g.Position[0],(float)g.Position[1],(float)g.Position[2]).DistanceTo(player!.GlobalPosition)<3);
        if(grave!=null){int before=world.State.Inventory.Slots.Where(s=>s!=null).Sum(s=>s!.Count);foreach(var slot in grave.Items.Slots)if(slot!=null){int remaining=world.State.Inventory.Add(slot,catalog);slot.Count=remaining;}for(int i=0;i<grave.Items.Slots.Length;i++)if(grave.Items.Slots[i]?.Count==0)grave.Items.Slots[i]=null;if(world.State.Inventory.Slots.Where(s=>s!=null).Sum(s=>s!.Count)>before)audio.Play("pickup");if(grave.Items.Slots.All(s=>s==null)){world.State.Graves.Remove(grave);Toast("已取回背包");}else Toast("背包已满，剩余物品仍留在原处");return;}
        var target=player!.Target();if(!target.HasValue)return;var p=target.Value.Hit;string id=catalog.Id(world.Get(p));
        if(id=="door"){if(world.State.Entities.TryGetValue(p.ToString(),out var e)){e.Open=!e.Open;world.Set(p,world.Get(p));renderer!.RefreshEdit(p);var other=p+new Cell(0,e.Stage==1?-1:1,0);if(world.State.Entities.TryGetValue(other.ToString(),out var top)){top.Open=e.Open;world.Set(other,world.Get(other));renderer.RefreshEdit(other);}audio.Play("door",new(p.X+.5f,p.Y+.5f,p.Z+.5f));}return;}
        if(id=="bed"){world.State.Respawn=[p.X+.5,p.Y+.7,p.Z+.5];double day=world.State.Time%1200;if(day<600){Toast("已设置复活点；白天无需睡觉");RequestSave();return;}world.Advance(1200-day+240);world.State.Health=100;Toast("已安全过夜");RequestSave();return;}
        if(id=="beacon"){ShowBeacon(p);return;}
        if(id=="lantern"){Toast("提灯会自动照亮附近");return;}
        if(world.State.Entities.TryGetValue(p.ToString(),out var entity)&&entity.Kind=="crop"){Harvest(p,entity);return;}
        if(id is "chest" or "workbench" or "campfire" or "furnace" && !world.State.Entities.ContainsKey(p.ToString())){
            var facility=new BlockEntity{Kind=id,LastTime=world.State.Time};world.State.Entities[p.ToString()]=facility;
            if(id=="chest"){foreach(string item in new[]{"grain","carrot","potato","copper_ore","coal"})facility.Storage.Add(item,3,catalog);facility.Storage.Add("map",1,catalog);world.State.Discovered.Add("ruins");}
        }
        if(id is "workbench" or "campfire" or "furnace"){currentStation=id;currentContainer=p;ShowRecipes();return;}
        if(id=="chest"){currentContainer=p;ShowInventory();return;}
        Toast("此处没有可使用的设施");
    }
    private void Plant(Cell ground,string seed)
    {
        if(!world!.Plant(ground,seed,out var error)){Toast(error);return;}renderer!.RefreshEdit(ground+new Cell(0,1,0));audio.Play("place_grass",new(ground.X+.5f,ground.Y+1,ground.Z+.5f));
    }
    private void Harvest(Cell p,BlockEntity e)
    {
        string produce=CropGrowth.Produce(e.CropId??"wheat_seed");
        if(!world!.Harvest(p,out var error)){Toast(error);return;}renderer!.RefreshEdit(p);audio.Play("pickup");Toast("收获："+catalog.Items[produce].Name);
    }
    private void DropSelected()
    {
        var slot=world!.State.Inventory.Slots[selected];if(slot==null)return;var items=new Inventory();items.Slots[0]=slot;world.State.Inventory.Slots[selected]=null;world.State.Graves.Add(new(){Position=(double[])world.State.Position.Clone(),Items=items});Toast("物品已放在脚下，Q 可回收");
    }
    private void Hurt(double damage)
    {
        if(world==null||world.State.Creative||screen!="play"||!double.IsFinite(damage)||damage<=0)return;
        RecordRouteDamage(damage);
        world.State.Health-=damage;damageFeedback.Trigger();Toast($"受伤 −{damage:0.#}");audio.Play("hurt");
        if(world.State.Health<=0)audio.Play("death");
        if(world.State.Health<=0){world.Die();player!.GlobalPosition=new((float)world.State.Position[0],(float)world.State.Position[1],(float)world.State.Position[2]);player.Velocity=Vector3.Zero;SetScreen("death");var col=Screen(overlay,"你在荒野中倒下了");col.AddChild(Label(world.State.Casual?"物品已保留。":"物品保留在地图标记的背包中；再次死亡仍可取回。"));col.AddChild(Button("从营地复活",()=>SetScreen("play")));col.AddChild(Button("保存并返回主菜单",ReturnMenu));RequestSave();}
    }
    private void HurtFrom(double damage,Vector3 attacker)
    {
        if(world!=null&&player!=null&&CreaturePopulation.SafeCamp(world,Explorer.ToCell(player.GlobalPosition)))return;
        if(world==null||world.State.Creative||screen!="play"||!double.IsFinite(damage)||damage<=0)return;
        bool shield=Held()?.Use=="shield"||OffhandHeld()?.Use=="shield"&&Held()?.Block!=true&&Held()?.Use is not("food" or "seed" or "bow");
        if(player!=null&&shield&&Input.IsActionPressed("use")&&(-player.Camera.GlobalBasis.Z).Dot((attacker-player.GlobalPosition).Normalized())>.2f){damage*=.35;audio.Play("shield");}
        damage=Equipment.Absorb(world.State,catalog,damage);
        Hurt(damage);
    }
    private bool SaveNow()
    {
        if(world==null)return true;
        ReturnWorkbenchItems();
        try{saves.Save(world.State);lastSaved=DateTime.Now.ToString("HH:mm:ss");return true;}catch(Exception e){GD.PushError(e);Toast("保存失败，尚未写入："+e.Message);return false;}
    }
    private void RequestSave(Action<bool>? completed=null)
    {
        if(world==null){completed?.Invoke(true);return;}
        ReturnWorkbenchItems();
        try{var task=saveQueue.Enqueue(world.State);if(!saveCallbacks.TryGetValue(task,out var callbacks)){callbacks=[];saveCallbacks.Add(task,callbacks);}if(completed!=null)callbacks.Add(completed);}
        catch(Exception error){saveFailed=true;Toast("保存失败，原档已保留："+error.Message);completed?.Invoke(false);}
    }
    private void PollSaves()
    {
        foreach(var task in saveCallbacks.Keys.ToArray())if(task.IsCompleted){
            var callbacks=saveCallbacks[task];saveCallbacks.Remove(task);bool success=task.IsCompletedSuccessfully;
            if(success){var receipt=task.Result;lastCaptureMs=receipt.CaptureMs;lastWriteMs=receipt.WriteMs;if(receipt.WorldId==world?.State.WorldId){lastSaved=receipt.SavedUtc.ToLocalTime().ToString("HH:mm:ss");saveFailed=false;}}
            else{saveFailed=true;Toast("保存失败，原档已保留："+task.Exception?.GetBaseException().Message);}
            foreach(var callback in callbacks)callback(success);
        }
    }
    private void SaveAndLeave(Action leave)
    {
        ReturnCraftingItems();SetScreen("saving");var col=Screen(overlay,"正在保存世界");col.AddChild(Label("保存完成后返回；请保留游戏窗口。",16));RequestSave(success=>{if(success)leave();else ShowPause();});
    }
    private void RunWorldIo<T>(string label,Func<T> work,Action<T> completed,Action failed)
    {
        if(worldIo!=null)return;SetScreen("world-io");var col=Screen(overlay,label);col.AddChild(Label("正在处理世界文件…",16));
        worldIoCompleted=result=>completed((T)result);worldIoFailed=failed;worldIo=Task.Run<object>(()=>work()!);
    }
    private void PollWorldIo()
    {
        if(worldIo?.IsCompleted!=true)return;var task=worldIo;var completed=worldIoCompleted;var failed=worldIoFailed;worldIo=null;worldIoCompleted=null;worldIoFailed=null;
        if(closeAfterIo){closeAfterIo=false;if(task.IsFaulted)_=task.Exception;if(world==null)QuitCleanly();else SaveAndLeave(()=>QuitCleanly());return;}
        if(task.IsCompletedSuccessfully)completed?.Invoke(task.Result);else{Toast("世界文件操作失败："+task.Exception?.GetBaseException().Message);failed?.Invoke();}
    }
    private void BackupCurrentWorld()
    {
        string id=world!.State.WorldId;SetScreen("saving");var col=Screen(overlay,"正在保存并备份世界");col.AddChild(Label("备份完成后返回。",16));
        RequestSave(success=>{if(!success){ShowPause();return;}RunWorldIo("备份世界",()=>saves.Backup(id),path=>{Toast("备份完成："+path);ShowPause();},ShowPause);});
    }
    private void ClearWorldView()
    {
        damageFeedback?.Reset();
        heldView?.SetActive(false);
        if(worldRoot!=null){RemoveChild(worldRoot);worldRoot.QueueFree();worldRoot=null;}
        weatherView=null;nearLights=null;projectiles=null;miningFeedback=null;sun=null!;environment=null!;outline=null!;
        world=null;player=null;renderer=null;creatures.Clear();graveMarkers.Clear();graveFallSpeed.Clear();
    }
    private async void QuitCleanly(int exitCode=0)
    {
        if(quitting)return;quitting=true;ClearWorldView();
        // Stop submitting new collision shapes before draining deferred deletion
        // and physics-server frees. A delay while streaming still creates new RIDs.
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
    private void ReturnMenu()=>SaveAndLeave(()=>{ClearWorldView();ShowMenu();});
    private void Toast(string text){message=text;noticeTime=7;}
    private static ImmediateMesh SelectionEdges()
    {
        var mesh=new ImmediateMesh();mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for(int axis=0;axis<3;axis++)for(int a=0;a<2;a++)for(int b=0;b<2;b++){
            var from=axis==0?new Vector3(-.506f,a-.5f,b-.5f):axis==1?new Vector3(a-.5f,-.506f,b-.5f):new Vector3(a-.5f,b-.5f,-.506f);
            var to=from;to[axis]=.506f;mesh.SurfaceAddVertex(from);mesh.SurfaceAddVertex(to);
        }mesh.SurfaceEnd();return mesh;
    }
    private void UpdateGraveMarkers(double delta)
    {
        foreach(var g in graveMarkers.Keys.ToArray())if(!world!.State.Graves.Contains(g)){graveMarkers[g].QueueFree();graveMarkers.Remove(g);graveFallSpeed.Remove(g);}
        foreach(var g in world!.State.Graves)if(!graveMarkers.ContainsKey(g)){
            var marker=new MeshInstance3D{Mesh=new BoxMesh{Size=new(.45f,.4f,.35f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new("8b6746")}};
            for(int i=-1;i<=1;i+=2)marker.AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=new(.055f,.43f,.37f)},Position=new(i*.13f,0,0),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("433426")}});
            var label=new Label3D{Text="Q 取回背包",Position=Vector3.Up*.65f,FontSize=26,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,PixelSize=.006f};marker.AddChild(label);worldRoot!.AddChild(marker);graveMarkers[g]=marker;
        }
        foreach(var pair in graveMarkers){
            var g=pair.Key;float dt=(float)Math.Min(delta,.1),speed=Math.Min(25,graveFallSpeed.GetValueOrDefault(g)+20*dt),y=(float)g.Position[1];
            int x=(int)Math.Floor(g.Position[0]),z=(int)Math.Floor(g.Position[2]);float target=Math.Max(1,y-speed*dt);
            for(float sample=y-.02f;sample>=target-.02f;sample-=.1f){var cell=new Cell(x,(int)Math.Floor(sample),z);if(world.Solid(cell)){target=cell.Y+1.02f;speed=0;break;}}
            g.Position[1]=target;graveFallSpeed[g]=speed;pair.Value.Position=new((float)g.Position[0],target+.2f,(float)g.Position[2]);
            pair.Value.GetChild<Label3D>(2).Visible=pair.Value.Position.DistanceTo(player!.GlobalPosition)<10;
        }
    }
    public override void _Input(InputEvent e)
    {
        if(IsValidation){GetViewport().SetInputAsHandled();return;}
        if(screen!="play"&&(e is InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left}||e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Enter}))audio?.Play("ui");
    }
    private void RearmActions()
    {
        if(!Input.IsActionPressed("mine")&&!Input.IsActionPressed("use"))actionsArmed=true;
    }
    public override void _ExitTree()=>ItemSprites.Release();
}
