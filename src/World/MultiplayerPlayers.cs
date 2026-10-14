using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Items;

namespace EmberLighthouse.World;

// Only personal state belongs here: world edits, containers and quest milestones
// remain on WorldSnapshot and are never duplicated for an individual player.
public sealed class MultiplayerPlayerState
{
    public string Id {get;set;}=MultiplayerPlayers.LegacyHostId;
    public string Nickname {get;set;}="房主";
    public string AppearanceId {get;set;}="adventurer";
    public string ReconnectToken {get;set;}=Guid.NewGuid().ToString("N");
    public int SelectedHotbar {get;set;}
    public Inventory Inventory {get;set;}=new();
    public Inventory Equipment {get;set;}=new(5);
    public Inventory Crafting {get;set;}=new(4);
    public Stack? Cursor {get;set;}
    public double Health {get;set;}=100;
    public double Hunger {get;set;}=100;
    public double Air {get;set;}=15;
    public double[] Position {get;set;}=[0,39,0];
    public double[] Respawn {get;set;}=[0,39,0];
    public int Experience {get;set;}
    public HashSet<string> Discovered {get;set;}=[];
    public HashSet<int> ExploredMap {get;set;}=[];
    public HashSet<string> FavoriteRecipes {get;set;}=[];
    public List<double[]> Markers {get;set;}=[];
    public int TutorialStep {get;set;}
    public bool TutorialHidden {get;set;}
    public ActiveEffects Effects {get;set;}=new();

    public static MultiplayerPlayerState Capture(WorldSnapshot snapshot)
    {
        var player=new MultiplayerPlayerState();player.CaptureFrom(snapshot);return player;
    }

    // Runtime scopes retain references so UI callbacks referring to a player's
    // inventory still target that same inventory on a later network request.
    // Save snapshots and packets must use Copy(), never these live projections.
    public static MultiplayerPlayerState CaptureLive(WorldSnapshot snapshot)
    {
        var player=new MultiplayerPlayerState();player.CaptureLiveFrom(snapshot);return player;
    }
    public void CaptureLiveFrom(WorldSnapshot snapshot)
    {
        Health=snapshot.Health;Hunger=snapshot.Hunger;Air=snapshot.Air;Experience=snapshot.Experience;SelectedHotbar=snapshot.SelectedHotbar;
        Inventory=snapshot.Inventory;Equipment=snapshot.Equipment;Crafting=snapshot.Crafting;Cursor=snapshot.Cursor;
        Position=snapshot.Position;Respawn=snapshot.Respawn;Discovered=snapshot.Discovered;ExploredMap=snapshot.ExploredMap;
        FavoriteRecipes=snapshot.FavoriteRecipes;Markers=snapshot.Markers;TutorialStep=snapshot.TutorialStep;TutorialHidden=snapshot.TutorialHidden;Effects=snapshot.Effects;
    }
    public void ApplyLive(WorldSnapshot snapshot)
    {
        snapshot.Health=Health;snapshot.Hunger=Hunger;snapshot.Air=Air;snapshot.Experience=Experience;snapshot.SelectedHotbar=SelectedHotbar;
        snapshot.Inventory=Inventory;snapshot.Equipment=Equipment;snapshot.Crafting=Crafting;snapshot.Cursor=Cursor;
        snapshot.Position=Position;snapshot.Respawn=Respawn;snapshot.Discovered=Discovered;snapshot.ExploredMap=ExploredMap;
        snapshot.FavoriteRecipes=FavoriteRecipes;snapshot.Markers=Markers;snapshot.TutorialStep=TutorialStep;snapshot.TutorialHidden=TutorialHidden;snapshot.Effects=Effects;
    }

    // Preserve identity and the reconnect credential when committing a live player.
    public void CaptureFrom(WorldSnapshot snapshot)
    {
        Inventory=snapshot.Inventory.Copy();Equipment=snapshot.Equipment.Copy();Crafting=snapshot.Crafting.Copy();Cursor=snapshot.Cursor?.Copy();
        Health=snapshot.Health;Hunger=snapshot.Hunger;Air=snapshot.Air;
        Position=(double[])snapshot.Position.Clone();Respawn=(double[])snapshot.Respawn.Clone();Experience=snapshot.Experience;
        Discovered=new(snapshot.Discovered);ExploredMap=new(snapshot.ExploredMap);FavoriteRecipes=new(snapshot.FavoriteRecipes);
        Markers=snapshot.Markers.Select(p=>(double[])p.Clone()).ToList();TutorialStep=snapshot.TutorialStep;TutorialHidden=snapshot.TutorialHidden;
        Effects=snapshot.Effects.Copy();SelectedHotbar=snapshot.SelectedHotbar;
    }

    // Apply a copy so temporary player scopes cannot mutate the saved DTO by alias.
    public void Apply(WorldSnapshot snapshot)
    {
        var copy=Copy();snapshot.Inventory=copy.Inventory;snapshot.Equipment=copy.Equipment;snapshot.Crafting=copy.Crafting;snapshot.Cursor=copy.Cursor;
        snapshot.Health=Health;snapshot.Hunger=Hunger;snapshot.Air=Air;snapshot.Position=copy.Position;snapshot.Respawn=copy.Respawn;snapshot.Experience=Experience;
        snapshot.Discovered=copy.Discovered;snapshot.ExploredMap=copy.ExploredMap;snapshot.FavoriteRecipes=copy.FavoriteRecipes;snapshot.Markers=copy.Markers;
        snapshot.TutorialStep=TutorialStep;snapshot.TutorialHidden=TutorialHidden;snapshot.Effects=copy.Effects;snapshot.SelectedHotbar=SelectedHotbar;
    }

    public MultiplayerPlayerState Copy()
    {
        Validate();var copy=(MultiplayerPlayerState)MemberwiseClone();
        copy.Inventory=Inventory.Copy();copy.Equipment=Equipment.Copy();copy.Crafting=Crafting.Copy();copy.Cursor=Cursor?.Copy();
        copy.Position=(double[])Position.Clone();copy.Respawn=(double[])Respawn.Clone();copy.Discovered=new(Discovered);copy.ExploredMap=new(ExploredMap);
        copy.FavoriteRecipes=new(FavoriteRecipes);copy.Markers=Markers.Select(p=>(double[])p.Clone()).ToList();copy.Effects=Effects.Copy();return copy;
    }

    public void Validate(Catalog? catalog=null)
    {
        static bool PositionOk(double[]? p)=>p is {Length:3}&&p.All(double.IsFinite);
        if(!MultiplayerPlayers.ValidId(Id)||string.IsNullOrWhiteSpace(Nickname)||Nickname.Length>32||Nickname.Any(char.IsControl)||!MultiplayerPlayers.ValidId(AppearanceId)||string.IsNullOrEmpty(ReconnectToken)||ReconnectToken.Length>128||ReconnectToken.Any(char.IsControl))throw new InvalidOperationException("联机玩家身份无效");
        if(!PositionOk(Position)||!PositionOk(Respawn)||!double.IsFinite(Health)||Health<0||Health>100||!double.IsFinite(Hunger)||Hunger<0||Hunger>100||!double.IsFinite(Air)||Air<0||Air>15||Experience<0||TutorialStep<0||SelectedHotbar<0||SelectedHotbar>8)throw new InvalidOperationException("联机玩家状态无效");
        if(Inventory?.Slots is not {Length:36}||Equipment?.Slots is not {Length:5}||Crafting?.Slots is not {Length:4}||Discovered==null||ExploredMap==null||FavoriteRecipes==null||Markers==null||Effects==null||ExploredMap.Any(i=>i<0||i>=MapExploration.Count)||Markers.Any(p=>p is not {Length:2}||!p.All(double.IsFinite)))throw new InvalidOperationException("联机玩家背包或地图无效");
        Effects.Validate();
        if(catalog!=null){Inventory.Validate(catalog);Items.Equipment.Validate(Equipment,catalog);Crafting.Validate(catalog);if(Cursor!=null)new Inventory(1){Slots=[Cursor]}.Validate(catalog);}
    }
}

public static class MultiplayerPlayers
{
    public const string LegacyHostId="host";
    internal static bool ValidId(string? id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=64&&id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');

    public static MultiplayerPlayerState CaptureHost(WorldSnapshot snapshot,string? nickname=null,string? appearanceId=null)
    {
        if(!snapshot.Players.TryGetValue(LegacyHostId,out var host))host=new(){Id=LegacyHostId};
        host.CaptureFrom(snapshot);if(nickname!=null)host.Nickname=nickname;if(appearanceId!=null)host.AppearanceId=appearanceId;
        host.Validate();snapshot.Players[LegacyHostId]=host;return host;
    }

    public static MultiplayerPlayerState Create(WorldSnapshot snapshot,string id,string nickname,string appearanceId="adventurer")
    {
        if(snapshot.Players.ContainsKey(id))throw new InvalidOperationException("联机玩家标识已存在");
        var player=new MultiplayerPlayerState{Id=id,Nickname=nickname,AppearanceId=appearanceId,Position=(double[])snapshot.Respawn.Clone(),Respawn=(double[])snapshot.Respawn.Clone()};
        player.Validate();snapshot.Players.Add(id,player);return player;
    }
}
