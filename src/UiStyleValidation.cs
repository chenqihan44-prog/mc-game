using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    private bool UiStyleCheck;
    private bool uiStyleStarted, uiStyleFinished;
    private string uiStyleDirectory = "", uiStyleStage = "startup", uiStyleVariant = "";
    private readonly Stopwatch uiStyleWatch = new();
    private readonly List<string> uiStyleFailures = [];
    private readonly List<string> uiStyleAssertions = [];
    private readonly List<object> uiStyleScreens = [];

    private void InitializeUiStyleValidation()
    {
        UiStyleCheck = OS.GetCmdlineUserArgs().Contains("--ui-style-check");
        if (!UiStyleCheck) return;
        var arguments = OS.GetCmdlineUserArgs();
        int index = Array.IndexOf(arguments, "--ui-style-report-dir");
        uiStyleDirectory = index >= 0 && index + 1 < arguments.Length ? Path.GetFullPath(arguments[index + 1])
            : ProjectSettings.GlobalizePath("res://test-results/ui-style/runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uiStyleDirectory);
        uiStyleWatch.Start();
    }

    private void TickUiStyleValidation(double delta)
    {
        if (!UiStyleCheck || uiStyleFinished || !startupReady) return;
        if (uiStyleWatch.Elapsed.TotalSeconds > 240)
        { FinishUiStyleValidation(false, "native GUI scenario timeout at " + uiStyleStage); return; }
        if (!uiStyleStarted) { uiStyleStarted = true; RunUiStyleValidation(); }
    }

    private async Task UiStyleFrames(int count = 4)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (uiStyleFinished) throw new OperationCanceledException("UI validation stopped");
    }

    private async Task UiStyleUntil(Func<bool> condition, double seconds = 25)
    {
        double end = uiStyleWatch.Elapsed.TotalSeconds + seconds;
        while (!condition())
        {
            if (uiStyleWatch.Elapsed.TotalSeconds > end) throw new TimeoutException(uiStyleStage);
            await UiStyleFrames(1);
        }
        await UiStyleFrames();
    }

    private void UiStyleRequire(bool condition, string assertion)
    {
        if (condition) { if (!uiStyleAssertions.Contains(assertion)) uiStyleAssertions.Add(assertion); }
        else uiStyleFailures.Add(uiStyleVariant + "/" + uiStyleStage + ": " + assertion);
    }

    private IEnumerable<T> UiStyleControls<T>() where T : Node => UiDescendants(overlay).OfType<T>();
    private Button UiStyleButton(string text) => UiStyleControls<Button>().FirstOrDefault(b => b.Text == text)
        ?? throw new InvalidOperationException("required action missing: " + text + " (" + screen + ")");

    private async Task UiStyleClick(string text)
    {
        var button = UiStyleButton(text);
        UiStyleRequire(!button.Disabled, "action enabled: " + text);
        for (Node? parent = button.GetParent(); parent is not null && parent != overlay; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(button);
        await UiStyleFrames(2);
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await UiStyleFrames();
    }

    private async Task UiStyleAction(string action, string expected)
    {
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await UiStyleFrames(2);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await UiStyleFrames(2);
        UiStyleRequire(screen == expected, "native shortcut " + action + " opens/closes " + expected);
    }

    private static ScrollContainer? UiStyleNearestScroll(Control control, Control root)
    {
        for (Node? parent = control.GetParent(); parent is not null && parent != root; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) return scroll;
        return null;
    }

    private async Task UiStyleCapture(string name, params string[] primaryActions)
    {
        uiStyleStage = name;
        await UiStyleFrames(6);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var viewportRect = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
        var problems = new List<string>();
        int buttons = 0, scrollable = 0, disabled = 0;
        foreach (Control control in UiStyleControls<Control>().Where(c => c.IsVisibleInTree()))
        {
            if (control is not (Button or LineEdit or Slider or OptionButton or PanelContainer)) continue;
            if (control is Button button)
            {
                buttons++;
                if (button is not SlotButton && button.Text.Length > 0 && !button.Disabled && button.FocusMode == Control.FocusModeEnum.None)
                    problems.Add("nonfocusable action: " + button.Text);
                if (button.Disabled) disabled++;
            }
            Rect2 rect = control.GetGlobalRect();
            if (rect.Size.X <= 0 || rect.Size.Y <= 0) { problems.Add("zero-size " + control.GetType().Name); continue; }
            var scroll = UiStyleNearestScroll(control, overlay);
            bool outsideX = rect.Position.X < -2 || rect.End.X > viewportRect.End.X + 2;
            bool outsideY = rect.Position.Y < -2 || rect.End.Y > viewportRect.End.Y + 2;
            if (scroll is not null)
            {
                scrollable++; outsideY = false;
                if (scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled) outsideX = false;
            }
            if (outsideX || outsideY) problems.Add("outside viewport: " + control.GetType().Name + " " + (control is Button b ? b.Text : control.Name.ToString()) + " " + rect);
        }
        foreach (string text in primaryActions)
        {
            Button? action = UiStyleControls<Button>().FirstOrDefault(b => b.Text == text);
            if (action is null) { problems.Add("missing primary action: " + text); continue; }
            Rect2 visible = action.GetGlobalRect().Intersection(viewportRect);
            for (Node? parent = action.GetParent(); parent is not null && parent != overlay; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) visible = visible.Intersection(scroll.GetGlobalRect());
            if (visible.Size.X < action.Size.X - 2 || visible.Size.Y < action.Size.Y - 2)
                problems.Add("primary action clipped: " + text);
        }
        string directory = Path.Combine(uiStyleDirectory, uiStyleVariant);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".png");
        using var image = GetViewport().GetTexture().GetImage();
        UiStyleRequire(image.SavePng(path) == Error.Ok && image.GetWidth() > 0 && image.GetHeight() > 0, "native screenshot recorded: " + name);
        UiStyleRequire(problems.Count == 0, "usable bounds and keyboard focus: " + name + (problems.Count == 0 ? "" : " [" + string.Join("; ", problems.Take(12)) + "]"));
        uiStyleScreens.Add(new { variant = uiStyleVariant, name, screen, viewport = new[] { viewportRect.Size.X, viewportRect.Size.Y },
            imageWidth = image.GetWidth(), imageHeight = image.GetHeight(), fontScale = settings.UiScale,
            buttons, scrollable, disabled, problems, screenshot = path });
    }

    private async void RunUiStyleValidation()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("native rendering is required; omit --headless");
            string isolated = Path.GetFullPath(uiStyleDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(saves.Root).StartsWith(isolated, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(settingsPath).StartsWith(isolated, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("fixture and settings paths must be inside this isolated report run; user saves are never used");
            UiStyleRequire(true, "fixture and settings writes isolated from user profiles");
            var fixture = new WorldSnapshot { Name = "UI验证营地·只用于自动测试", Seed = 42, Creative = true,
                Population = new() { Initialized = true, NextSpawnTime = 100000 } };
            fixture.Inventory.Add("wood", 12, catalog); fixture.Inventory.Add("stone", 7, catalog);
            saves.Save(fixture);
            if (OS.GetCmdlineUserArgs().Contains("--coop-feedback-only"))
            {
                uiStyleStage = "remote menu layout and workbench callbacks";
                await ValidateRemoteMenuLayouts(fixture);
                uiStyleStage = "multiplayer map and articulated crouch";
                await ValidateMultiplayerMapAndCrouch();
                UiStyleRequire(true, "live player map markers and rigid crouch pose validated in native engine");
                FinishUiStyleValidation(uiStyleFailures.Count == 0, "");
                return;
            }
            foreach (var variant in new[] { (1280, 720, 1f), (1920, 1080, 1f), (1280, 720, 1.4f), (1920, 1080, 1.4f) })
            {
                uiStyleVariant = variant.Item1 + "x" + variant.Item2 + "-font" + variant.Item3.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                settings.WindowWidth = variant.Item1; settings.WindowHeight = variant.Item2; settings.UiScale = variant.Item3;
                settings.Fullscreen = false; settings.FpsLimit = 60; ApplySettings(true);
                ClearWorldView(); ShowMenu(); await UiStyleCapture("01-main-menu", "新建世界", "设置", "退出");
                await UiStyleClick("新建世界"); await UiStyleCapture("02-create-world", "进入荒野", "返回");
                var seed = UiStyleControls<LineEdit>().First(e => e.Name == "WorldSeed");
                string previous = seed.Text; await UiStyleClick("随机种子");
                UiStyleRequire(seed.Text != previous && int.TryParse(seed.Text, out _), "new-world reroll changes valid seed");
                var modes = UiStyleControls<CheckButton>().Where(b => b.Name != "BonusChest").ToArray();
                if (modes.Length >= 2) { modes[0].ButtonPressed = true; modes[1].ButtonPressed = true;
                    UiStyleRequire(!modes[0].ButtonPressed && modes[1].ButtonPressed, "new-world modes stay exclusive"); }
                var bonus = UiStyleControls<CheckButton>().FirstOrDefault(b => b.Name == "BonusChest");
                if (bonus is not null && modes.Length >= 1) { modes[0].ButtonPressed = true;
                    UiStyleRequire(bonus.Disabled && !bonus.ButtonPressed, "creative mode disables bonus chest"); }
                await UiStyleClick("返回"); UiStyleRequire(screen == "menu", "new-world return reaches main menu");
                await UiStyleClick("加入联机世界"); await UiStyleCapture("03-join-room", "加入世界", "返回");
                await UiStyleClick("返回"); UiStyleRequire(screen == "menu", "join-room return reaches main menu");
                await UiStyleClick("设置"); await UiStyleCapture("04-settings", "保存并返回");
                var slider = UiStyleControls<HSlider>().First(s => s.TooltipText == "鼠标灵敏度"); double oldValue = slider.Value;
                slider.Value = Math.Min(slider.MaxValue, oldValue + slider.Step);
                UiStyleRequire(Math.Abs(settings.Sensitivity - slider.Value) < .00001, "settings slider invokes actual sensitivity callback");
                var fps = UiStyleControls<OptionButton>().First(b => new[] { 30, 60, 120, 0 }
                    .All(value => Enumerable.Range(0, b.ItemCount).Any(i => b.GetItemId(i) == value)));
                int lower = Enumerable.Range(0, fps.ItemCount).First(i => fps.GetItemId(i) == 30);
                fps.Select(lower); fps.EmitSignal(OptionButton.SignalName.ItemSelected, lower);
                UiStyleRequire(settings.FpsLimit == 30 && Engine.MaxFps == 30, "settings option changes actual engine FPS cap");
                int option = Enumerable.Range(0, fps.ItemCount).First(i => fps.GetItemId(i) == 60);
                fps.Select(option); fps.EmitSignal(OptionButton.SignalName.ItemSelected, option);
                UiStyleRequire(settings.FpsLimit == 60 && Engine.MaxFps == 60, "settings option updates real engine FPS cap");
                await UiStyleClick("保存并返回");
                UiStyleRequire(screen == "menu" && File.Exists(settingsPath), "settings persist and return to menu");
                await UiStyleClick("操作与帮助"); await UiStyleCapture("05-help", "返回");
                var helpScroll = UiStyleControls<ScrollContainer>().OrderByDescending(s => s.GetVScrollBar().MaxValue).First();
                helpScroll.ScrollVertical = (int)helpScroll.GetVScrollBar().MaxValue; await UiStyleFrames();
                UiStyleRequire(helpScroll.ScrollVertical > 0, "help content can scroll to later instructions");
                await UiStyleCapture("06-help-scrolled", "返回"); await UiStyleClick("返回");
                UiStyleRequire(screen == "menu", "help return reaches main menu");
                await UiStyleClick("制作人员与素材"); await UiStyleCapture("07-credits", "返回"); await UiStyleClick("返回");
                UiStyleRequire(screen == "menu", "credits return reaches main menu");
                await UiStyleClick("读取与备份世界"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
                await UiStyleCapture("08-world-management", "返回", "从备份恢复");
                await UiStyleClick("读取"); await UiStyleUntil(() => world is not null && screen == "play");
                player!.SetPhysicsProcess(false); await UiStyleUntil(() => renderer!.IsTerrainReady(player.GlobalPosition));
                UiStyleRequire(world!.State.Name == fixture.Name && world.State.Inventory.Count("wood") == 12, "actual world-load button restores isolated fixture");
                await UiStyleAction("pause", "pause"); await UiStyleCapture("09-pause", "继续探索", "保存并返回主菜单");
                await UiStyleClick("继续探索"); UiStyleRequire(screen == "play", "pause continue callback resumes gameplay");
                await UiStyleAction("inventory", "inventory"); await UiStyleCapture("10-inventory", "关闭");
                UiStyleRequire(UiStyleControls<SlotButton>().Count() >= 40 && UiStyleControls<AvatarPreview>().Any(), "inventory retains interactive slots and actual avatar preview");
                await UiStyleClick("关闭"); UiStyleRequire(screen == "play", "inventory close callback resumes gameplay");
                await UiStyleAction("map", "map"); await UiStyleCapture("11-map", "返回游戏（M）");
                UiStyleRequire(UiStyleControls<WorldMap>().Any(), "map remains a real interactive world map");
                await UiStyleAction("map", "play"); await UiStyleAction("journal", "journal");
                await UiStyleCapture("12-exploration-log", "返回游戏");
                var graph = UiStyleControls<QuestGraph>().First(); float zoom = graph.Zoom;
                await UiStyleClick("＋"); UiStyleRequire(graph.Zoom > zoom, "exploration-log zoom action updates real graph");
                await UiStyleAction("journal", "play");
                ShowPause(); await UiStyleClick(UiStyleControls<Button>().First(b => b.Text is "操作与帮助" or "帮助与引导").Text);
                await UiStyleClick("返回"); UiStyleRequire(screen == "pause", "in-game help return preserves pause context");
                await UiStyleClick("保存并返回主菜单"); await UiStyleUntil(() => screen == "menu" && world is null && saveCallbacks.Count == 0);
            }
            await UiStyleValidateExtendedInteractions(fixture);
            uiStyleVariant = "fixture-operations";
            await UiStyleValidateFixtureManagement(fixture);
            uiStyleStage = "remote menu layout and workbench callbacks";
            await ValidateRemoteMenuLayouts(fixture);
            uiStyleStage = "multiplayer map and articulated crouch";
            await ValidateMultiplayerMapAndCrouch();
            UiStyleRequire(true, "live player map markers and rigid crouch pose validated in native engine");
            FinishUiStyleValidation(uiStyleFailures.Count == 0, "");
        }
        catch (Exception error) { if (!uiStyleFinished) FinishUiStyleValidation(false, error.ToString()); }
    }

    private async Task UiStyleKey(Key key)
    {
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await UiStyleFrames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await UiStyleFrames(2);
    }

    private async Task UiStyleValidateExtendedInteractions(WorldSnapshot fixture)
    {
        uiStyleVariant = "extended-1280x720-font1.0";
        uiStyleStage = "native escape navigation and settings binding";
        settings.WindowWidth = 1280; settings.WindowHeight = 720; settings.UiScale = 1;
        ApplySettings(true); ShowMenu(); await UiStyleFrames();
        foreach (string entry in new[] { "新建世界", "加入联机世界", "操作与帮助", "制作人员与素材", "设置" })
        {
            await UiStyleClick(entry); await UiStyleKey(Key.Escape);
            UiStyleRequire(screen == "menu", "native Escape returns " + entry + " to main menu");
        }
        await UiStyleClick("设置");
        const string action = "forward";
        bool hadBinding = settings.Keys.ContainsKey(action);
        long original = settings.Keys.GetValueOrDefault(action, (long)defaultKeys[action].Key);
        var bindingButton = UiStyleControls<Button>().First(b => b.TooltipText == "重新绑定：" + defaultKeys[action].Name);
        bindingButton.EmitSignal(BaseButton.SignalName.Pressed); await UiStyleFrames();
        UiStyleRequire(binding == action, "settings key button enters real rebinding mode");
        await UiStyleKey(Key.Up);
        UiStyleRequire(binding is null && settings.Keys[action] == (long)Key.Up &&
            InputMap.ActionGetEvents(action).OfType<InputEventKey>().Any(k => k.PhysicalKeycode == Key.Up),
            "native key rebinding updates settings and live InputMap");
        UiStyleControls<Button>().First(b => b.TooltipText == "重新绑定：" + defaultKeys[action].Name)
            .EmitSignal(BaseButton.SignalName.Pressed); await UiStyleFrames();
        await UiStyleKey(Key.Escape);
        UiStyleRequire(binding is null && screen == "settings" && settings.Keys[action] == (long)Key.Up,
            "Escape cancels rebinding without changing assigned key or leaving settings");
        UiStyleControls<Button>().First(b => b.TooltipText == "重新绑定：" + defaultKeys[action].Name)
            .EmitSignal(BaseButton.SignalName.Pressed); await UiStyleFrames();
        await UiStyleKey((Key)original);
        UiStyleRequire(settings.Keys[action] == original && binding is null, "native rebind restores fixture key preference");
        if (!hadBinding) { settings.Keys.Remove(action); RegisterInput(); PersistSettings(); }
        await UiStyleKey(Key.Escape); UiStyleRequire(screen == "menu", "Escape exits settings after cancelling key capture");
        await UiStyleClick("读取与备份世界"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        await UiStyleKey(Key.Escape); UiStyleRequire(screen == "menu", "Escape returns world list to menu");
        await UiStyleClick("读取与备份世界"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        await UiStyleClick("读取"); await UiStyleUntil(() => world is not null && screen == "play");
        if (world!.State.WorldId != fixture.WorldId) throw new InvalidOperationException("extended actions must use isolated fixture");
        player!.SetPhysicsProcess(false); await UiStyleUntil(() => renderer!.IsTerrainReady(player.GlobalPosition));
        await UiStyleCapture("19-gameplay-hud");
        UiStyleRequire(hudRoot.Visible && !overlay.Visible && minecraftHud.IsVisibleInTree() && miniMap.IsVisibleInTree(),
            "gameplay shows native HUD and minimap while menu overlay is hidden");
        uiStyleStage = "repeat shortcuts and hand crafting";
        await UiStyleAction("inventory", "inventory"); await UiStyleAction("inventory", "play");
        await UiStyleAction("craft", "recipes");
        await UiStyleCapture("18-hand-crafting", "返回游戏", "背包");
        UiStyleRequire(UiStyleControls<LineEdit>().Any() && UiStyleControls<Button>().Any(b => b.Text == "制作"),
            "C opens real searchable hand crafting with recipe actions");
        await UiStyleAction("craft", "play");
        uiStyleStage = "map zoom focus fit and marker callbacks";
        await UiStyleAction("map", "map");
        var map = UiStyleControls<WorldMap>().First(); float initialZoom = map.Zoom;
        await UiStyleClick("＋"); UiStyleRequire(map.Zoom > initialZoom, "map zoom-in button changes actual map zoom");
        await UiStyleClick("－"); UiStyleRequire(Math.Abs(map.Zoom - initialZoom) < .0001, "map zoom-out reverses zoom-in");
        await UiStyleClick("全图");
        UiStyleRequire(map.MapCenter.DistanceTo(Vector2.Zero) < .01f && map.Zoom < initialZoom,
            "map full-view button fits world rather than leaving current local view");
        map.SetView(new Vector2(120, 90), map.Zoom);
        await UiStyleClick("定位玩家");
        UiStyleRequire(map.MapCenter.DistanceTo(new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z)) < .01f,
            "map player-focus button centers actual player coordinates");
        int markers = world.State.Markers.Count; await UiStyleClick("标记当前位置");
        UiStyleRequire(world.State.Markers.Count == markers + 1 &&
            Math.Abs(world.State.Markers[^1][0] - player.GlobalPosition.X) < .001 &&
            Math.Abs(world.State.Markers[^1][1] - player.GlobalPosition.Z) < .001,
            "map marker button records actual current position exactly once");
        await UiStyleKey(Key.Escape); UiStyleRequire(screen == "play", "Escape closes map to gameplay");
        uiStyleStage = "journal hint preference and real node tracking";
        await UiStyleAction("journal", "journal");
        bool hints = world.State.Mainline.StrongHints;
        UiStyleControls<CheckButton>().First(b => b.Text == "显示目标准确位置").ButtonPressed = !hints;
        await UiStyleFrames();
        UiStyleRequire(world.State.Mainline.StrongHints == !hints && screen == "journal", "journal accurate-position toggle updates real preference and preserves journal");
        UiStyleControls<CheckButton>().First(b => b.Text == "显示目标准确位置").ButtonPressed = hints;
        await UiStyleFrames(); UiStyleRequire(world.State.Mainline.StrongHints == hints, "journal accurate-position preference restored");
        var graph = UiStyleControls<QuestGraph>().First();
        var node = graph.Nodes.First(n => n.Target is not null && n.Status != QuestStatus.Locked);
        // Recenter the presentation, then select through the native GUI pipeline.
        var otherNode = graph.Nodes.FirstOrDefault(n => n.Id != node.Id);
        if (otherNode is not null) graph.SelectNode(otherNode.Id);
        graph.SetView(new Vector2(node.X + 103, node.Y + 52), graph.Zoom); await UiStyleFrames();
        Vector2 point = graph.GetGlobalTransform() * graph.NodeRect(node).GetCenter();
        GetViewport().GuiReleaseFocus();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        await UiStyleFrames(2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false });
        await UiStyleFrames(2);
        UiStyleRequire(graph.SelectedId == node.Id, "native journal node click selects displayed quest details");
        await UiStyleClick(node.Done ? "追踪此地点" : "追踪此目标");
        UiStyleRequire(screen == "play" && trackedQuest?.Id == node.Id && trackedMainlineTarget == node.Id,
            "journal track button activates selected node and resumes gameplay");
        uiStyleStage = "pause save backup and reversible fixture rules";
        await UiStyleAction("pause", "pause");
        int stone = world.State.Inventory.Count("stone"); world.State.Inventory.Add("stone", 1, catalog);
        var saveButton = UiStyleButton("保存世界"); saveButton.EmitSignal(BaseButton.SignalName.Pressed);
        UiStyleRequire(saveButton.Disabled, "pause save button disables while authoritative save is pending");
        await UiStyleUntil(() => saveCallbacks.Count == 0 && !saveButton.Disabled);
        UiStyleRequire(saves.Load(fixture.WorldId).Snapshot.Inventory.Count("stone") == stone + 1,
            "pause save callback writes actual modified fixture and reenables action");
        world.State.Inventory.Remove("stone", 1);
        await UiStyleClick("保存世界"); await UiStyleUntil(() => saveCallbacks.Count == 0);
        int backups = saves.ListBackups().Count(b => b.Name == fixture.Name);
        await UiStyleClick("备份世界"); await UiStyleUntil(() => screen == "pause" && worldIo is null && saveCallbacks.Count == 0);
        UiStyleRequire(saves.ListBackups().Count(b => b.Name == fixture.Name && !b.Damaged) == backups + 1,
            "pause backup callback creates one readable isolated backup and returns to pause");
        bool creative = world.State.Creative, casual = world.State.Casual;
        await UiStyleClick("世界规则");
        UiStyleControls<CheckButton>().First(b => b.Text.StartsWith("休闲难度", StringComparison.Ordinal)).ButtonPressed = true;
        await UiStyleClick("应用并保存"); await UiStyleUntil(() => saveCallbacks.Count == 0);
        UiStyleRequire(screen == "pause" && world.State.Casual && !world.State.Creative,
            "world-rule apply callback changes only fixture to exclusive casual mode");
        await UiStyleClick("世界规则");
        UiStyleControls<CheckButton>().First(b => b.Text == "创造模式").ButtonPressed = creative;
        UiStyleControls<CheckButton>().First(b => b.Text.StartsWith("休闲难度", StringComparison.Ordinal)).ButtonPressed = casual;
        await UiStyleClick("应用并保存"); await UiStyleUntil(() => saveCallbacks.Count == 0);
        var restoredRules = saves.Load(fixture.WorldId).Snapshot;
        UiStyleRequire(world.State.Creative == creative && world.State.Casual == casual &&
            restoredRules.Creative == creative && restoredRules.Casual == casual, "world rules restored in live fixture and durable save");
        await UiStyleKey(Key.Escape); UiStyleRequire(screen == "play", "Escape resumes gameplay from pause");
        await UiStyleAction("pause", "pause"); await UiStyleClick("设置"); await UiStyleKey(Key.Escape);
        UiStyleRequire(screen == "pause", "in-game settings Escape returns to pause context");
        await UiStyleClick("保存并返回主菜单"); await UiStyleUntil(() => screen == "menu" && world is null && saveCallbacks.Count == 0);
    }

    private async Task UiStyleValidateFixtureManagement(WorldSnapshot fixture)
    {
        uiStyleStage = "backup/delete/restore callbacks";
        ShowWorlds(); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        await UiStyleClick("备份"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        UiStyleRequire(saves.ListBackups().Any(b => b.Name == fixture.Name && !b.Damaged), "actual backup callback creates readable fixture backup");
        await UiStyleClick("删除"); await UiStyleCapture("13-delete-confirmation", "确认删除", "取消");
        await UiStyleClick("确认删除"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        UiStyleRequire(!saves.ListDetails().Any(w => w.Id == fixture.WorldId), "delete callback removes fixture from active world list");
        await UiStyleClick("已删除世界"); await UiStyleUntil(() => screen == "deleted-worlds" && worldIo is null);
        await UiStyleCapture("14-deleted-worlds", "返回世界列表"); await UiStyleClick("恢复世界");
        await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        UiStyleRequire(saves.ListDetails().Any(w => w.Id == fixture.WorldId), "deleted-world restore callback restores original fixture identity");
        await UiStyleClick("从备份恢复"); await UiStyleUntil(() => screen == "backups" && worldIo is null);
        await UiStyleCapture("15-backup-management", "返回"); await UiStyleClick("恢复为新世界");
        await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        UiStyleRequire(saves.ListDetails().Any(w => w.Id != fixture.WorldId && w.Name.StartsWith(fixture.Name, StringComparison.Ordinal)), "backup restore callback creates independent world copy");
        await UiStyleClick("从备份恢复"); await UiStyleUntil(() => screen == "backups" && worldIo is null);
        var beforeBackupDeletion = saves.ListBackups().Select(b => b.Key).ToHashSet();
        await UiStyleClick("永久删除备份"); await UiStyleCapture("16-delete-backup", "确认永久删除备份", "取消");
        await UiStyleClick("确认永久删除备份"); await UiStyleUntil(() => screen == "backups" && worldIo is null);
        var afterBackupDeletion = saves.ListBackups().Select(b => b.Key).ToHashSet();
        UiStyleRequire(afterBackupDeletion.Count == beforeBackupDeletion.Count - 1 && afterBackupDeletion.IsSubsetOf(beforeBackupDeletion),
            "backup deletion removes only one isolated backup and preserves every other backup");
        UiStyleRequire(saves.ListDetails().Count(w => w.Name.StartsWith(fixture.Name, StringComparison.Ordinal)) >= 2, "backup deletion preserves both active fixture worlds");
        await UiStyleClick("返回"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        var restored = saves.ListDetails().First(w => w.Id != fixture.WorldId && w.Name.StartsWith(fixture.Name, StringComparison.Ordinal));
        ShowDeleteWorld(restored); await UiStyleCapture("17-permanent-fixture-deletion", "永久删除世界", "取消");
        await UiStyleClick("永久删除世界"); await UiStyleUntil(() => screen == "worlds" && worldIo is null);
        UiStyleRequire(!saves.ListDetails().Any(w => w.Id == restored.Id) && saves.ListDetails().Any(w => w.Id == fixture.WorldId), "permanent deletion callback removes copied fixture and preserves original");
        await UiStyleClick("返回"); UiStyleRequire(screen == "menu", "world management return chain reaches menu");
    }

    private void FinishUiStyleValidation(bool passed, string error)
    {
        if (uiStyleFinished) return;
        uiStyleFinished = true;
        if (error.Length > 0) uiStyleFailures.Add(error);
        File.WriteAllText(Path.Combine(uiStyleDirectory, "ui-style-validation.json"), JsonSerializer.Serialize(new {
            passed = passed && uiStyleFailures.Count == 0, stage = uiStyleStage, error,
            seconds = uiStyleWatch.Elapsed.TotalSeconds, nativeRendering = DisplayServer.GetName() != "headless",
            userProfilesTouched = false, saveRoot = saves?.Root, settingsPath,
            assertions = uiStyleAssertions, failures = uiStyleFailures, screenshots = uiStyleScreens }, Catalog.JsonOptions));
        GD.Print(passed && uiStyleFailures.Count == 0 ? "UI_STYLE_PASS: native menus, viewport bounds, callbacks, shortcuts and isolated saves" : "UI_STYLE_FAILED: " + string.Join("; ", uiStyleFailures.Take(8)));
        QuitCleanly(passed && uiStyleFailures.Count == 0 ? 0 : 19);
    }
}
