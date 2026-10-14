using System;
using EmberLighthouse.Items;
using EmberLighthouse.UI;
using Godot;
using ItemStack = EmberLighthouse.Items.Stack;

namespace EmberLighthouse.Networking;

/// <summary>Client drag presentation sends control tokens, never inventory edits.</summary>
public partial class RemoteSlotButton : Button
{
    public Catalog Catalog { get; set; } = null!;
    public ItemStack? Item { get; set; }
    public int Token { get; set; }
    public string DragScope { get; set; } = "";
    public bool DragAllowed { get; set; }
    public Action<int, int>? Transfer { get; set; }
    public int DragAttempts {get;private set;}
    public int DropChecks {get;private set;}
    public int CompletedDrops {get;private set;}
    private bool dragging;

    public override Variant _GetDragData(Vector2 position)
    {
        DragAttempts++;
        if (Disabled || !DragAllowed || Item is null || Transfer is null || Token <= 0) return default;
        var preview = new Control { MouseFilter = MouseFilterEnum.Ignore };
        var shadow = new ItemGlyph { Catalog = Catalog, Item = Item.Copy(), Size = Size,
            Position = -Size / 2 + new Vector2(3, 5), Modulate = new(0, 0, 0, .35f), PixelScale = Size.X / 20 };
        preview.AddChild(shadow);
        var glyph = new ItemGlyph { Catalog = Catalog, Item = Item.Copy(), Size = Size,
            Position = -Size / 2, PixelScale = Size.X / 20, PivotOffset = Size / 2, Scale = Vector2.One * .85f };
        preview.AddChild(glyph);
        preview.Ready += () => preview.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
            .TweenProperty(glyph, "scale", Vector2.One * 1.08f, .12);
        SetDragPreview(preview);
        dragging = true;
        foreach (Node child in GetChildren()) if (child is ItemGlyph icon) icon.Modulate = new(1, 1, 1, .25f);
        return new Godot.Collections.Dictionary { { "remote_source", Token }, { "remote_scope", DragScope } };
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        DropChecks++;
        if (Disabled || Transfer is null || Token <= 0 || !IsVisibleInTree() || data.VariantType != Variant.Type.Dictionary) return false;
        var values = data.AsGodotDictionary();
        if (!values.ContainsKey("remote_source") || !values.ContainsKey("remote_scope") ||
            values["remote_source"].VariantType != Variant.Type.Int || values["remote_scope"].VariantType != Variant.Type.String) return false;
        long source = values["remote_source"].AsInt64();
        return source > 0 && source <= RemoteUi.MaximumNodes && source != Token && values["remote_scope"].AsString() == DragScope;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (!_CanDropData(position, data)) return;
        CompletedDrops++;
        Transfer?.Invoke(data.AsGodotDictionary()["remote_source"].AsInt32(), Token);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !dragging) return;
        dragging = false;
        foreach (Node child in GetChildren()) if (child is ItemGlyph icon) icon.Modulate = Colors.White;
    }
}
