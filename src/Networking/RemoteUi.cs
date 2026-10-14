using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Items;
using EmberLighthouse.Rendering;
using EmberLighthouse.UI;
using Godot;
using ItemStack = EmberLighthouse.Items.Stack;

namespace EmberLighthouse.Networking;

public sealed class RemoteUiDocument
{
    public RemoteUiNode Root { get; set; } = new();
    public float[] SourceViewport {get;set;}=[];
}

public sealed class RemoteUiNode
{
    public string Kind { get; set; } = "Control";
    public int Token { get; set; }
    public string Text { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string IconKey { get; set; } = "";
    public string TextureKey { get; set; } = "";
    public string Ornament { get; set; } = "";
    public bool HeadingFont { get; set; }
    public bool Visible { get; set; } = true;
    public bool Disabled { get; set; }
    public bool Pressed { get; set; }
    public bool Editable { get; set; } = true;
    public bool ToggleMode { get; set; }
    public bool Slot { get; set; }
    public int SlotIndex { get; set; }
    public int SurfaceIndex { get; set; }
    public bool DragAllowed { get; set; }
    public bool FollowPointer { get; set; }
    public int ZIndex { get; set; }
    public string AppearanceId { get; set; } = "adventurer";
    public int FontSize { get; set; } = 17;
    public int Horizontal { get; set; }
    public int Vertical { get; set; }
    public int Wrap { get; set; }
    public int Columns { get; set; } = 1;
    public int MouseFilter { get; set; }
    public int Focus { get; set; }
    public int HorizontalFlags { get; set; } = 1;
    public int VerticalFlags { get; set; } = 1;
    public float Stretch { get; set; } = 1;
    // Position, size, minimum size, anchors, offsets, pivot, scale.
    public float[] Geometry { get; set; } = [];
    public float[] Modulate { get; set; } = [1, 1, 1, 1];
    public float[]? Color { get; set; }
    public Dictionary<string, int> Constants { get; set; } = [];
    public Dictionary<string, float[]> Colors { get; set; } = [];
    public Dictionary<string, RemoteUiStyle> Styles { get; set; } = [];
    public List<RemoteUiNode> Children { get; set; } = [];
    public ItemStack? Item { get; set; }
    public Inventory? Equipment { get; set; }
    public float PixelScale { get; set; } = 2.5f;
    public int HorizontalScroll { get; set; }
    public int VerticalScroll { get; set; }
    public int HorizontalScrollMode { get; set; }
    public int VerticalScrollMode { get; set; }
}

public sealed class RemoteUiStyle
{
    public float[] Shadow { get; set; } = [0,0,0,0];
    public int ShadowSize { get; set; }
    public float[] Background { get; set; } = [0, 0, 0, 1];
    public float[] Border { get; set; } = [0, 0, 0, 1];
    public int[] Widths { get; set; } = [0, 0, 0, 0];
    public int[] Corners { get; set; } = [0, 0, 0, 0];
    public float[] Margins { get; set; } = [-1, -1, -1, -1];
    public bool DrawCenter { get; set; } = true;
}

/// <summary>
/// Captures host-created guest menus into bounded data. The client draws controls
/// and submits tokens; existing authoritative callbacks remain on the host.
/// </summary>
public static class RemoteUi
{
    public const int MaximumNodes = 2048;
    public const int MaximumDepth = 32;
    public const int MaximumString = 2048;
    private static readonly string[] ConstantNames = ["separation", "h_separation", "v_separation", "margin_left", "margin_top", "margin_right", "margin_bottom", "shadow_offset_x", "shadow_offset_y", "outline_size"];
    private static readonly string[] ColorNames = ["font_color", "font_shadow_color", "font_outline_color", "font_hover_color", "font_pressed_color", "font_disabled_color"];
    private static readonly string[] StyleNames = ["panel", "normal", "hover", "pressed", "disabled", "focus"];

    public static RemoteUiDocument Capture(Control source, Dictionary<int, Control> controls)
    {
        // The authoritative guest menu lives under a hidden Control. Give that
        // root an actual viewport rect and settle adaptive minimum sizes now;
        // sending a freshly built menu cannot wait for its next _Process.
        source.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(var frame in Descendants(source).OfType<LanternScreenFrame>())frame.Refresh();
        controls.Clear();
        int count = 0, token = 0;
        var extent=source.GetViewportRect().Size;
        return new() { Root = CaptureNode(source, controls, ref count, ref token, 0) ?? new(), SourceViewport=[extent.X,extent.Y] };
    }

    private static IEnumerable<Node> Descendants(Node source){foreach(var child in source.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}

    private static RemoteUiNode? CaptureNode(Control source, Dictionary<int, Control> controls, ref int count, ref int token, int depth, bool interactive = true)
    {
        if (count >= MaximumNodes || depth > MaximumDepth || !GodotObject.IsInstanceValid(source)) return null;
        count++;
        // Guest UiRoot is hidden intentionally; hidden descendants still cannot
        // acquire executable tokens in the host's control map.
        interactive &= depth == 0 || source.Visible;
        var node = new RemoteUiNode
        {
            Kind = Kind(source), Text = "", Tooltip = Clip(source.TooltipText),
            Visible = source.Visible, MouseFilter = (int)source.MouseFilter, Focus = (int)source.FocusMode,
            HorizontalFlags = (int)source.SizeFlagsHorizontal, VerticalFlags = (int)source.SizeFlagsVertical,
            Stretch = source.SizeFlagsStretchRatio, Modulate = ColorData(source.Modulate), ZIndex = source.ZIndex,
            FontSize = source.GetThemeFontSize("font_size"),
            Geometry = [source.Position.X, source.Position.Y, source.Size.X, source.Size.Y,
                source.CustomMinimumSize.X, source.CustomMinimumSize.Y,
                source.AnchorLeft, source.AnchorTop, source.AnchorRight, source.AnchorBottom,
                source.OffsetLeft, source.OffsetTop, source.OffsetRight, source.OffsetBottom,
                source.PivotOffset.X, source.PivotOffset.Y, source.Scale.X, source.Scale.Y]
        };
        foreach (string name in ConstantNames)
            if (source.HasThemeConstantOverride(name)) node.Constants[name] = source.GetThemeConstant(name);
        // Effective inherited colors matter: inventory panels supply a local dark
        // label theme, which is different from the outer menu theme.
        foreach (string name in ColorNames)
            if (source.HasThemeColorOverride(name) || source is Label && source.HasThemeColor(name))
                node.Colors[name] = ColorData(source.GetThemeColor(name));
        foreach (string name in StyleNames)
            if (source.HasThemeStyleboxOverride(name) && source.GetThemeStylebox(name) is StyleBoxFlat style)
                node.Styles[name] = CaptureStyle(style);
        if (source is Label label)
        {
            node.HeadingFont=label.HasThemeFontOverride("font");
            node.Text = Clip(label.Text); node.Horizontal = (int)label.HorizontalAlignment;
            node.Vertical = (int)label.VerticalAlignment; node.Wrap = (int)label.AutowrapMode;
        }
        if (source is Button button)
        {
            node.Token = ++token;
            if (interactive && !button.Disabled) controls[node.Token] = source;
            node.Text = Clip(button.Text); node.Disabled = button.Disabled;
            node.Pressed = button.ButtonPressed; node.ToggleMode = button.ToggleMode;
            node.Horizontal = (int)button.Alignment; node.Slot = button is SlotButton;
            if(button.HasMeta("ui_icon"))node.IconKey=button.GetMeta("ui_icon").AsString();
            if (button is SlotButton slot)
            {
                node.SlotIndex = slot.SlotIndex; node.SurfaceIndex = slot.SurfaceIndex;
                node.Item = slot.Item?.Copy();
                node.DragAllowed = interactive && !button.Disabled && slot.DragAllowed?.Invoke() != false;
            }
        }
        if (source is LineEdit line)
        {
            node.Token = ++token;
            if (interactive && line.Editable) controls[node.Token] = source;
            node.Text = Clip(line.Text); node.Placeholder = Clip(line.PlaceholderText);
            node.Editable = line.Editable; node.Horizontal = (int)line.Alignment;
        }
        if (source is GridContainer grid) node.Columns = grid.Columns;
        if (source is ColorRect rectangle) node.Color = ColorData(rectangle.Color);
        if(source is TextureRect image&&image.HasMeta("ui_backdrop"))node.TextureKey=image.GetMeta("ui_backdrop").AsString();
        if(source is UiOrnament ornament)node.Ornament=ornament.Kind;
        if (source is ScrollContainer scroll)
        {
            node.HorizontalScroll = scroll.ScrollHorizontal; node.VerticalScroll = scroll.ScrollVertical;
            node.HorizontalScrollMode = (int)scroll.HorizontalScrollMode;
            node.VerticalScrollMode = (int)scroll.VerticalScrollMode;
        }
        if (source is ItemGlyph glyph) { node.Item = glyph.Item?.Copy(); node.PixelScale = glyph.PixelScale;
            node.FollowPointer = glyph.ZIndex == 20; return node; }
        // Do not capture the live subviewport or its TextureRect; the receiver creates its own.
        if (source is AvatarPreview avatar) { node.Equipment = avatar.Equipment?.Copy(); node.AppearanceId = avatar.AppearanceId; return node; }
        foreach (Node child in source.GetChildren())
            if (child is Control control && CaptureNode(control, controls, ref count, ref token, depth + 1, interactive) is { } captured)
                node.Children.Add(captured);
        return node;
    }

    public static void Render(Control target, RemoteUiDocument document, Catalog catalog,
        Action<int, bool, bool> click, Action<int, string> submit, Action<int, bool> toggle,
        Action<int, int>? drag = null)
    {
        foreach (Node child in target.GetChildren()) { target.RemoveChild(child); child.QueueFree(); }
        var resources = new RemoteUiResources { Name = "RemoteUiResources" };
        target.AddChild(resources);
        var theme = UiFactory.Theme();
        resources.OwnTheme(theme);
        target.Theme = theme;
        // The host source may be deliberately hidden while computing a guest menu.
        target.Visible = true;
        int count = 0;
        string dragScope = Guid.NewGuid().ToString("N");
        foreach (RemoteUiNode child in document.Root.Children)
        {
            Control? control = Create(child, catalog, click, submit, toggle, drag, dragScope, resources, ref count, 0);
            if (control is null) continue;
            target.AddChild(control);
            ApplyGeometry(control, child);
        }
        // Keep the screen's visible controls in their original child order.
        target.MoveChild(resources, target.GetChildCount() - 1);
        foreach(var frame in Descendants(target).OfType<LanternScreenFrame>())frame.BindContent();
    }

    private static Control? Create(RemoteUiNode data, Catalog catalog,
        Action<int, bool, bool> click, Action<int, string> submit, Action<int, bool> toggle,
        Action<int, int>? drag, string dragScope, RemoteUiResources resources, ref int count, int depth)
    {
        if (count >= MaximumNodes || depth > MaximumDepth) return null;
        count++;
        Control control = data.Kind switch
        {
            "TextureRect" => new TextureRect{Texture=data.TextureKey is "day" or "night"?UiFactory.BackdropTexture(data.TextureKey=="night"):null,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered},
            "UiOrnament" => new UiOrnament{Kind=data.Ornament},
            "Label" => new Label { Text = Clip(data.Text), HorizontalAlignment = Alignment(data.Horizontal),
                VerticalAlignment = (VerticalAlignment)Math.Clamp(data.Vertical, 0, 3),
                AutowrapMode = (TextServer.AutowrapMode)Math.Clamp(data.Wrap, 0, 3) },
            "CheckButton" => new CheckButton { Text = Clip(data.Text), Disabled = data.Disabled,
                ButtonPressed = data.Pressed },
            "Button" when data.Slot => new RemoteSlotButton { Catalog = catalog, Item = ValidItem(data.Item, catalog),
                Token = data.Token, DragScope = dragScope, DragAllowed = data.DragAllowed, Transfer = drag,
                Text = Clip(data.Text), Disabled = data.Disabled, Alignment = Alignment(data.Horizontal) },
            "Button" => new Button { Text = Clip(data.Text), Disabled = data.Disabled,
                ToggleMode = data.ToggleMode, ButtonPressed = data.Pressed, Alignment = Alignment(data.Horizontal) },
            "LineEdit" => new LineEdit { Text = Clip(data.Text), PlaceholderText = Clip(data.Placeholder),
                Editable = data.Editable, Alignment = Alignment(data.Horizontal), MaxLength = MaximumString },
            "PanelContainer" => new PanelContainer(),
            "VBoxContainer" => new VBoxContainer(),
            "HBoxContainer" => new HBoxContainer(),
            "GridContainer" => new GridContainer { Columns = Math.Clamp(data.Columns, 1, 64) },
            "LanternScreenFrame" => new LanternScreenFrame{FitRemoteContent=true},
            // Older documents serialized the adaptive frame as its base type.
            "CenterContainer" when data.Children.Any(c=>c.Kind=="ScrollContainer"&&c.Children.Any(p=>p.Kind=="PanelContainer"))
                =>new LanternScreenFrame{FitRemoteContent=true},
            "CenterContainer" => new CenterContainer(),
            "MarginContainer" => new MarginContainer(),
            "ScrollContainer" => new ScrollContainer {
                HorizontalScrollMode = (ScrollContainer.ScrollMode)Math.Clamp(data.HorizontalScrollMode, 0, 4),
                VerticalScrollMode = (ScrollContainer.ScrollMode)Math.Clamp(data.VerticalScrollMode, 0, 4) },
            "ColorRect" => new ColorRect { Color = ReadColor(data.Color, Colors.Transparent) },
            "ItemGlyph" when data.FollowPointer => new RemoteCursorGlyph { Catalog = catalog,
                Item = ValidItem(data.Item, catalog), PixelScale = Finite(data.PixelScale, 2.5f, .1f, 16) },
            "ItemGlyph" => new ItemGlyph { Catalog = catalog,
                Item = ValidItem(data.Item, catalog), PixelScale = Finite(data.PixelScale, 2.5f, .1f, 16) },
            "AvatarPreview" => new AvatarPreview { Catalog = catalog, Equipment = ValidEquipment(data.Equipment, catalog),
                AppearanceId = CharacterAppearances.Normalize(data.AppearanceId) },
            _ => new Control()
        };
        control.Visible = data.Visible;
        control.TooltipText = Clip(data.Tooltip);
        control.MouseFilter = (Control.MouseFilterEnum)Math.Clamp(data.MouseFilter, 0, 2);
        control.FocusMode = (Control.FocusModeEnum)Math.Clamp(data.Focus, 0, 2);
        control.SizeFlagsHorizontal = (Control.SizeFlags)(data.HorizontalFlags & 31);
        control.SizeFlagsVertical = (Control.SizeFlags)(data.VerticalFlags & 31);
        control.SizeFlagsStretchRatio = Finite(data.Stretch, 1, .01f, 100);
        control.Modulate = ReadColor(data.Modulate, Colors.White);
        control.ZIndex = Math.Clamp(data.ZIndex, -64, 64);
        control.AddThemeFontSizeOverride("font_size", Math.Clamp(data.FontSize, 8, 96));
        if(data.HeadingFont&&control is Label){var font=new SystemFont{FontNames=["SimSun","Microsoft YaHei"],FontWeight=700};resources.Own(font);control.AddThemeFontOverride("font",font);}
        if(control is Button decorated&&data.IconKey.Length>0){decorated.Icon=UiSymbols.Texture(data.IconKey);decorated.ExpandIcon=true;decorated.AddThemeConstantOverride("icon_max_width",26);}
        foreach (string name in ConstantNames)
            if (data.Constants.TryGetValue(name, out int value)) control.AddThemeConstantOverride(name, Math.Clamp(value, -100, 500));
        foreach (string name in ColorNames)
            if (data.Colors.TryGetValue(name, out var value)) control.AddThemeColorOverride(name, ReadColor(value, Colors.White));
        foreach (string name in StyleNames)
            if (data.Styles.TryGetValue(name, out var value))
            {
                var style = RenderStyle(value);
                resources.Own(style);
                control.AddThemeStyleboxOverride(name, style);
            }
        if (control is CheckButton check)
            check.Toggled += value => toggle(data.Token, value);
        else if (control is Button button)
        {
            button.Pressed += () => click(data.Token, false, Input.IsActionPressed("inventory_transfer"));
            if (data.Slot)
                button.GuiInput += input => {
                    if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                    { click(data.Token, true, Input.IsActionPressed("inventory_transfer")); button.AcceptEvent(); }
                };
        }
        if (control is LineEdit line) line.TextSubmitted += text => submit(data.Token, Clip(text));
        ApplyGeometry(control, data);
        if (data.Kind is not "ItemGlyph" and not "AvatarPreview")
            foreach (RemoteUiNode child in data.Children)
            {
                Control? rendered = Create(child, catalog, click, submit, toggle, drag, dragScope, resources, ref count, depth + 1);
                if (rendered is null) continue;
                control.AddChild(rendered); ApplyGeometry(rendered, child);
            }
        if (control is ScrollContainer scroller)
            scroller.Ready += () => { scroller.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, Math.Max(0, data.HorizontalScroll));
                scroller.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, Math.Max(0, data.VerticalScroll)); };
        if(control is LanternScreenFrame frame)frame.BindContent();
        return control;
    }

    private static string Kind(Control control) => control switch
    {
        TextureRect => "TextureRect", UiOrnament => "UiOrnament",
        ItemGlyph => "ItemGlyph", AvatarPreview => "AvatarPreview", Label => "Label",
        CheckButton => "CheckButton", Button => "Button", LineEdit => "LineEdit",
        PanelContainer => "PanelContainer", VBoxContainer => "VBoxContainer", HBoxContainer => "HBoxContainer",
        GridContainer => "GridContainer", ScrollContainer => "ScrollContainer", LanternScreenFrame => "LanternScreenFrame", CenterContainer => "CenterContainer",
        MarginContainer => "MarginContainer", ColorRect => "ColorRect", _ => "Control"
    };

    private static void ApplyGeometry(Control control, RemoteUiNode data)
    {
        if (data.Geometry.Length != 18) return;
        var g = data.Geometry;
        control.CustomMinimumSize = new(Finite(g[4], 0, 0, 8192), Finite(g[5], 0, 0, 8192));
        control.AnchorLeft = Finite(g[6], 0, -2, 2); control.AnchorTop = Finite(g[7], 0, -2, 2);
        control.AnchorRight = Finite(g[8], 0, -2, 2); control.AnchorBottom = Finite(g[9], 0, -2, 2);
        // Anchor-plus-offset data already defines the captured rect. Setting
        // Size again on stretched controls fights anchor layout after _Ready.
        control.OffsetLeft = Finite(g[10], 0); control.OffsetTop = Finite(g[11], 0);
        control.OffsetRight = Finite(g[12], 0); control.OffsetBottom = Finite(g[13], 0);
        control.PivotOffset = new(Finite(g[14], 0), Finite(g[15], 0));
        control.Scale = new(Finite(g[16], 1, .01f, 16), Finite(g[17], 1, .01f, 16));
    }

    private static RemoteUiStyle CaptureStyle(StyleBoxFlat style) => new()
    {
        Shadow=ColorData(style.ShadowColor),ShadowSize=style.ShadowSize,
        Background = ColorData(style.BgColor), Border = ColorData(style.BorderColor), DrawCenter = style.DrawCenter,
        Widths = [style.BorderWidthLeft, style.BorderWidthTop, style.BorderWidthRight, style.BorderWidthBottom],
        Corners = [style.CornerRadiusTopLeft, style.CornerRadiusTopRight, style.CornerRadiusBottomRight, style.CornerRadiusBottomLeft],
        Margins = [style.ContentMarginLeft, style.ContentMarginTop, style.ContentMarginRight, style.ContentMarginBottom]
    };

    private static StyleBoxFlat RenderStyle(RemoteUiStyle data)
    {
        var style = new StyleBoxFlat { BgColor = ReadColor(data.Background, Colors.Transparent),
            BorderColor = ReadColor(data.Border, Colors.Black), DrawCenter = data.DrawCenter };
        style.ShadowColor=ReadColor(data.Shadow,Colors.Transparent);style.ShadowSize=Math.Clamp(data.ShadowSize,0,32);
        if (data.Widths.Length == 4)
        { style.BorderWidthLeft = Math.Clamp(data.Widths[0], 0, 64); style.BorderWidthTop = Math.Clamp(data.Widths[1], 0, 64);
            style.BorderWidthRight = Math.Clamp(data.Widths[2], 0, 64); style.BorderWidthBottom = Math.Clamp(data.Widths[3], 0, 64); }
        if (data.Corners.Length == 4)
        { style.CornerRadiusTopLeft = Math.Clamp(data.Corners[0], 0, 64); style.CornerRadiusTopRight = Math.Clamp(data.Corners[1], 0, 64);
            style.CornerRadiusBottomRight = Math.Clamp(data.Corners[2], 0, 64); style.CornerRadiusBottomLeft = Math.Clamp(data.Corners[3], 0, 64); }
        if (data.Margins.Length == 4)
        { style.ContentMarginLeft = Finite(data.Margins[0], -1, -1, 500); style.ContentMarginTop = Finite(data.Margins[1], -1, -1, 500);
            style.ContentMarginRight = Finite(data.Margins[2], -1, -1, 500); style.ContentMarginBottom = Finite(data.Margins[3], -1, -1, 500); }
        return style;
    }

    private static ItemStack? ValidItem(ItemStack? item, Catalog catalog)
    {
        if (item is null || !catalog.Items.ContainsKey(item.Id)) return null;
        var copy = item.Copy(); copy.Count = Math.Clamp(copy.Count, 1, 9999); return copy;
    }

    private static Inventory? ValidEquipment(Inventory? equipment, Catalog catalog)
    {
        if (equipment is null) return null;
        var result = new Inventory(5);
        for (int i = 0; i < Math.Min(result.Slots.Length, equipment.Slots.Length); i++) result.Slots[i] = ValidItem(equipment.Slots[i], catalog);
        return result;
    }

    private static string Clip(string? value) => string.IsNullOrEmpty(value) ? "" : value.Length <= MaximumString ? value : value[..MaximumString];
    private static float[] ColorData(Color color) => [color.R, color.G, color.B, color.A];
    private static Color ReadColor(float[]? values, Color fallback) => values is { Length: 4 }
        ? new(Finite(values[0], fallback.R, 0, 4), Finite(values[1], fallback.G, 0, 4), Finite(values[2], fallback.B, 0, 4), Finite(values[3], fallback.A, 0, 1)) : fallback;
    private static float Finite(float value, float fallback, float minimum = -16384, float maximum = 16384) => float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    private static HorizontalAlignment Alignment(int value) => (HorizontalAlignment)Math.Clamp(value, 0, 3);
}

/// <summary>A presentation-only cursor image; inventory changes stay on the host.</summary>
public partial class RemoteCursorGlyph : ItemGlyph
{
    public override void _Process(double delta)
    {
        Position = Position.Lerp(GetViewport().GetMousePosition() - Size / 2,
            1 - MathF.Exp(-(float)delta * 35));
    }
}

/// <summary>
/// Keeps dynamically reconstructed UI resource wrappers alive until the menu
/// exits the tree, then releases them on the game thread before engine teardown.
/// </summary>
public partial class RemoteUiResources : Node
{
    private readonly List<Resource> owned = [];
    private readonly HashSet<ulong> ids = [];

    public void Own(Resource? resource)
    {
        if (resource is not null && ids.Add(resource.GetInstanceId())) owned.Add(resource);
    }

    public void OwnTheme(Theme theme)
    {
        Own(theme);
        Own(theme.DefaultFont);
        foreach (string type in theme.GetTypeList())
        {
            foreach (string name in theme.GetStyleboxList(type)) Own(theme.GetStylebox(name, type));
            foreach (string name in theme.GetFontList(type)) Own(theme.GetFont(name, type));
        }
    }

    public override void _ExitTree()
    {
        // Controls/Theme hold their own native references. Dispose only our
        // managed wrappers; Godot releases the remaining native refs normally.
        for (int index = owned.Count - 1; index >= 0; index--) owned[index].Dispose();
        owned.Clear();
        ids.Clear();
    }
}
