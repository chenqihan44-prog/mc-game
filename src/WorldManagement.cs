using EmberLighthouse.Persistence;
using EmberLighthouse.World;
using Godot;
using static EmberLighthouse.UI.UiFactory;
namespace EmberLighthouse;

public partial class Game
{
    private void ShowDeleteWorld(WorldEntry entry)
    {
        SetScreen("delete-world");var col=Screen(overlay,"删除世界");var description=Label($"{entry.Name}\n编号：{entry.Id}\n普通删除移到「已删除世界」，可以恢复。永久删除直接清除文件，无法恢复。",17);description.AutowrapMode=TextServer.AutowrapMode.WordSmart;description.CustomMinimumSize=new(580,0);col.AddChild(description);
        var backups=new CheckButton{Text="永久删除时一并清除该世界的备份",ButtonPressed=true};col.AddChild(backups);
        col.AddChild(Button("确认删除",()=>RunWorldIo("删除世界",()=>saves.DeleteWorld(entry.Id,entry.Name),_=>{Toast("世界已删除，可在已删除世界中恢复");ShowWorlds();},ShowWorlds)));col.AddChild(Button("取消",ShowWorlds));
        col.AddChild(Button("永久删除世界",()=>{bool include=backups.ButtonPressed;RunWorldIo("永久删除世界",()=>saves.PermanentlyDeleteWorld(entry.Id,include),_=>{Toast("世界文件已永久删除");ShowWorlds();},ShowWorlds);}));
    }
    private void ShowDeletedWorlds()=>RunWorldIo("读取已删除世界",saves.ListDeletedWorlds,DrawDeletedWorlds,ShowWorlds);
    private void DrawDeletedWorlds(DeletedWorldEntry[] deleted)
    {
        SetScreen("deleted-worlds");var col=Screen(overlay,"已删除世界");var scroll=Scroll(col,330);scroll.HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled;var entries=Column(scroll);entries.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        foreach(var entry in deleted){var row=Column(entries);var text=Label(entry.Name+"\n删除于 "+entry.DeletedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),15);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;text.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;row.AddChild(text);var actions=Row(row);actions.AddChild(Button("恢复世界",()=>RunWorldIo("恢复已删除世界",()=>saves.RestoreDeletedWorld(entry.Key),_=>{Toast("世界已恢复");ShowWorlds();},ShowDeletedWorlds)));actions.AddChild(Button("永久删除",()=>ShowPurgeDeletedWorld(entry)));}
        if(deleted.Length==0)entries.AddChild(Label("没有已删除的世界。"));col.AddChild(Button("返回世界列表",ShowWorlds));
    }
    private void ShowPurgeDeletedWorld(DeletedWorldEntry entry)
    {
        SetScreen("purge-deleted-world");var col=Screen(overlay,"永久删除世界");var text=Label(entry.Name+"\n编号："+entry.Id+"\n将清除该条已删除世界的文件，无法恢复。",17);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(text);
        var backups=new CheckButton{Text="一并永久删除该世界的备份",ButtonPressed=true};col.AddChild(backups);
        col.AddChild(Button("确认永久删除",()=>{bool include=backups.ButtonPressed;RunWorldIo("永久删除已删除世界",()=>saves.PermanentlyDeleteDeletedWorld(entry.Key,include),_=>{Toast("已清除世界文件");ShowDeletedWorlds();},ShowDeletedWorlds);}));col.AddChild(Button("取消",ShowDeletedWorlds));
    }
    private void ShowDeleteBackup(BackupEntry entry)
    {
        SetScreen("delete-backup");var col=Screen(overlay,"永久删除备份");var text=Label(entry.Name+"\n备份："+entry.Key+"\n只清除此份备份，活动世界不受影响。此操作无法恢复。",17);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(text);
        col.AddChild(Button("确认永久删除备份",()=>RunWorldIo("永久删除备份",()=>{saves.DeleteBackup(entry.Key);return true;},_=>{Toast("备份文件已永久删除");ShowBackups();},ShowBackups)));col.AddChild(Button("取消",ShowBackups));
    }
    private void ShowSurfaceRefresh(WorldEntry entry)
    {
        SetScreen("surface-refresh");var col=Screen(overlay,"更新自然地表");var text=Label(entry.Name+"\n减少自然灌木和草花，移除固定的出生物资排列，按种子分散物资。山体、湖泊、建筑、容器、作物与主线进度保留。",17);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;col.AddChild(text);
        var backup=new CheckButton{Text="更新前备份原存档",ButtonPressed=true};col.AddChild(backup);
        col.AddChild(Button("确认更新地表",()=>{bool keepBackup=backup.ButtonPressed;RunWorldIo("更新自然地表",()=>{var saved=saves.Load(entry.Id).Snapshot;if(keepBackup)saves.Backup(entry.Id);var updated=StarterSupplies.Refresh(saved,catalog);saves.Save(updated);return true;},_=>{Toast("自然地表已更新，建筑和主线进度保留");ShowWorlds();},ShowWorlds);}));col.AddChild(Button("取消",ShowWorlds));
    }
}
