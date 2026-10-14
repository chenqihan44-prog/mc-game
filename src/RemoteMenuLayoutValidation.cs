using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.Networking;
using EmberLighthouse.Player;
using EmberLighthouse.UI;
using EmberLighthouse.World;
using Godot;

namespace EmberLighthouse;

public partial class Game
{
    // Called by the isolated native UI harness. Uses the production hidden
    // authoritative guest tree and JSON codec, not a hand-built imitation UI.
    private async Task ValidateRemoteMenuLayouts(WorldSnapshot fixture)
    {
        if(!UiStyleCheck||DisplayServer.GetName()=="headless")throw new InvalidOperationException("remote menu validation requires isolated native GUI harness");
        string run=Path.GetFullPath(uiStyleDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(saves.Root).StartsWith(run,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("remote UI fixture is outside isolated run");
        StartWorld(saves.Load(fixture.WorldId).Snapshot);player!.SetPhysicsProcess(false);
        var actor=new Explorer{NetworkReplica=true,AcceptUserInput=false,Enabled=true,Model=world!,TerrainReady=renderer!.IsTerrainReady,Hurt=_=>{}};
        worldRoot!.AddChild(actor);actor.SetReplicaPose(player.GlobalPosition,0,0,false);actor.AddCollisionExceptionWith(player);player.AddCollisionExceptionWith(actor);
        var hidden=new Control{Visible=false};AddChild(hidden);
        var guest=new RoomPlayer{Peer=200000001,Actor=actor,UiRoot=hidden,State=new MultiplayerPlayerState{
            Id="ddd33333333333333333333333333333",Nickname="布局验证朋友",AppearanceId="artisan",Position=[actor.GlobalPosition.X,actor.GlobalPosition.Y,actor.GlobalPosition.Z]}};
        Cell station=Explorer.ToCell(actor.GlobalPosition)+new Cell(2,0,0),chest=station+new Cell(0,0,1);
        world!.Set(station,catalog.Number("workbench"));world.State.Entities[station.ToString()]=new(){Kind="workbench"};
        world.Set(chest,catalog.Number("chest"));world.State.Entities[chest.ToString()]=new(){Kind="chest",Storage=new Inventory(27)};
        var samples=new List<object>();
        var originalLogicalSize=GetWindow().ContentScaleSize;
        int callbackCount=0;
        var scenarios=new[]{(1920,1080,1280,720,false),(1280,720,1920,1080,false),(1600,900,1545,642,false),(1545,642,1600,900,false),(1920,1080,1366,640,false),
            (1600,900,1545,642,true),(1545,642,1600,900,true)};
        try{
            foreach(var scenario in scenarios){
                var hostSize=new Vector2I(scenario.Item1,scenario.Item2);var clientSize=new Vector2I(scenario.Item3,scenario.Item4);
                bool variableLogical=scenario.Item5;
                uiStyleVariant=$"remote-host{hostSize.X}x{hostSize.Y}-client{clientSize.X}x{clientSize.Y}-font1.4"+(variableLogical?"-logical":"-letterbox");
                foreach(string menu in new[]{"inventory","container","workbench"}){
                    guest.State.Inventory=new();guest.State.Inventory.Slots[0]=new("plank",8);guest.State.Inventory.Slots[2]=new("wood",12);
                    guest.State.Cursor=null;guest.Workbench=new(9);guest.Screen="play";guest.Container=null;
                    world.State.Entities[chest.ToString()].Storage=new Inventory(27);
                    world.State.Entities[chest.ToString()].Storage!.Slots[0]=new("stone",16);
                    async Task Present(string actualMenu= ""){
                        await RemoteMenuWindow(hostSize,variableLogical?hostSize:new Vector2I(1280,720));
                        RemoteUiDocument? document=null;
                        WithGuest(guest,()=>{
                            if(actualMenu.Length>0){guest.Container=actualMenu=="workbench"?station.ToString():actualMenu=="container"?chest.ToString():null;
                                currentContainer=guest.Container is null?null:Cell.Parse(guest.Container);
                                if(actualMenu=="workbench"){currentStation="workbench";ShowWorkbench();}else ShowInventory();}
                            document=RemoteUi.Capture(hidden,guest.UiControls);
                        });
                        if(document is null)throw new InvalidOperationException("guest scope did not capture menu");
                        var sourceFrame=UiDescendants(hidden).OfType<LanternScreenFrame>().Single();
                        var sourceMinimum=sourceFrame.Panel!.GetCombinedMinimumSize();
                        var sourceScrollerMinimum=sourceFrame.Scroller!.CustomMinimumSize;
                        samples.Add(new{menu=guest.Screen,sourceHidden=!hidden.Visible,zeroFrameCapture=true,hostPhysical=new[]{hostSize.X,hostSize.Y},
                            hostLogical=document.SourceViewport,sourcePanelMinimum=new[]{sourceMinimum.X,sourceMinimum.Y},sourceScrollerMinimum=new[]{sourceScrollerMinimum.X,sourceScrollerMinimum.Y}});
                        string json=JsonSerializer.Serialize(document,NetJson);
                        var decoded=JsonSerializer.Deserialize<RemoteUiDocument>(json,Catalog.JsonOptions)!;
                        await RemoteMenuWindow(clientSize,variableLogical?clientSize:new Vector2I(1280,720));SetScreen("remote-ui");
                        RemoteUi.Render(overlay,decoded,catalog,(token,half,shift)=>{
                            callbackCount++;WithGuest(guest,()=>{
                                if(!guest.UiControls.TryGetValue(token,out var control)||control is not Button button||button.Disabled)throw new InvalidOperationException("client submitted invalid source control");
                                bool previous=Input.IsActionPressed("inventory_transfer");
                                try{if(shift)Input.ActionPress("inventory_transfer");else Input.ActionRelease("inventory_transfer");
                                    if(half)button.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Right,Pressed=true});else button.EmitSignal(BaseButton.SignalName.Pressed);
                                }finally{if(previous)Input.ActionPress("inventory_transfer");else Input.ActionRelease("inventory_transfer");}
                            });if(guest.Screen=="play")SetScreen("play");
                        },(_,_)=>{},(_,_)=>{},(source,target)=>{
                            callbackCount++;WithGuest(guest,()=>{
                                var from=(SlotButton)guest.UiControls[source];var to=(SlotButton)guest.UiControls[target];
                                if(world!.State.Cursor!=null||from.DragAllowed?.Invoke()==false)throw new InvalidOperationException("drag accepted while cursor held");
                                TransferSlots(from.SurfaceIndex,from.SlotIndex,to.SurfaceIndex,to.SlotIndex);
                            });
                        });
                        await UiStyleFrames(8);
                    }
                    // No frame is yielded between constructing the hidden source
                    // menu and Capture: this is the production failure trigger.
                    await Present(menu);
                    string back=menu=="workbench"?"返回游戏":"关闭";
                    await RemoteMenuCheck(menu+"-initial",back,samples);
                    int SlotToken(int surface,int index)=>guest.UiControls.Single(p=>p.Value is SlotButton b&&b.SurfaceIndex==surface&&b.SlotIndex==index).Key;
                    RemoteSlotButton Slot(int surface,int index)=>UiStyleControls<RemoteSlotButton>().Single(b=>b.Token==SlotToken(surface,index));
                    int before=callbackCount;
                    Slot(0,0).EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Right,Pressed=true});
                    UiStyleRequire(callbackCount==before+1&&guest.State.Cursor?.Count==4&&guest.State.Inventory.Slots[0]?.Count==4,"remote right-click splits guest stack through source callback: "+menu);
                    await Present();
                    Slot(0,1).EmitSignal(BaseButton.SignalName.Pressed);
                    UiStyleRequire(guest.State.Cursor==null&&guest.State.Inventory.Slots[1]?.Count==4,"remote left-click puts held guest stack through source callback: "+menu);
                    await Present();
                    var from=Slot(0,0);var to=Slot(0,3);Vector2 start=from.GetGlobalRect().GetCenter(),finish=to.GetGlobalRect().GetCenter();
                    GetViewport().GuiReleaseFocus();
                    GetViewport().NotifyMouseEntered();
                    // These points are canvas/viewport coordinates. PushInput's
                    // local-coordinate mode executes the native GUI pipeline
                    // without mixing desktop/window pixels into letterboxing.
                    GetViewport().PushInput(new InputEventMouseMotion{Position=start,GlobalPosition=start,ButtonMask=0},true);
                    GetViewport().PushInput(new InputEventMouseButton{Position=start,GlobalPosition=start,ButtonIndex=MouseButton.Left,ButtonMask=MouseButtonMask.Left,Pressed=true},true);await UiStyleFrames(2);
                    bool sourceHovered=from.IsHovered();var mouseAfterPress=GetViewport().GetMousePosition();
                    Vector2 threshold=start+new Vector2(18,0);
                    // Input keeps a global pressed-button state, but synthetic
                    // motion events must also carry their own held-button mask.
                    GetViewport().PushInput(new InputEventMouseMotion{Position=threshold,GlobalPosition=threshold,Relative=threshold-start,ButtonMask=MouseButtonMask.Left},true);await UiStyleFrames(2);
                    bool dragStarted=GetViewport().GuiIsDragging();
                    GetViewport().PushInput(new InputEventMouseMotion{Position=finish,GlobalPosition=finish,Relative=finish-threshold,ButtonMask=MouseButtonMask.Left},true);await UiStyleFrames(2);
                    bool targetHovered=to.IsHovered();string dragDataType=GetViewport().GuiGetDragData().VariantType.ToString();
                    GetViewport().PushInput(new InputEventMouseButton{Position=finish,GlobalPosition=finish,ButtonIndex=MouseButton.Left,ButtonMask=0,Pressed=false},true);await UiStyleFrames(3);
                    samples.Add(new{menu,variant=uiStyleVariant,stage="native-drag",sourceHovered,targetHovered,dragStarted,dragDataType,
                        sourceDragAllowed=from.DragAllowed,sourceItem=from.Item?.Id,sourceCount=from.Item?.Count,
                        from.DragAttempts,to.DropChecks,to.CompletedDrops,successful=GetViewport().GuiIsDragSuccessful(),
                        mouseAfterPress=new[]{mouseAfterPress.X,mouseAfterPress.Y},start=new[]{start.X,start.Y},finish=new[]{finish.X,finish.Y},callbacks=callbackCount});
                    UiStyleRequire(guest.State.Inventory.Slots[0]==null&&guest.State.Inventory.Slots[3]?.Count==4&&guest.State.Cursor==null,"native remote drag moves guest stack and conserves cursor: "+menu);
                    await Present();
                    Input.ActionPress("inventory_transfer");try{Slot(0,2).EmitSignal(BaseButton.SignalName.Pressed);}finally{Input.ActionRelease("inventory_transfer");}
                    bool shiftMoved=menu=="container"?world.State.Entities[chest.ToString()].Storage!.Count("wood")==12:
                        menu=="workbench"?guest.Workbench.Count("wood")==12:guest.State.Inventory.Slots.Take(9).All(s=>s?.Id!="wood")&&guest.State.Inventory.Slots.Skip(9).Any(s=>s?.Id=="wood");
                    UiStyleRequire(shiftMoved&&guest.State.Cursor==null,"remote Shift callback moves guest items to correct destination: "+menu);
                    await Present();
                    if(menu=="workbench"){
                        var next=UiStyleButton("›");var previous=UiStyleButton("‹");
                        foreach(string footer in new[]{"收回合成材料","返回游戏","装备维护"}){
                            var button=UiStyleButton(footer);UiStyleRequire(!next.GetGlobalRect().Intersects(button.GetGlobalRect())&&!previous.GetGlobalRect().Intersects(button.GetGlobalRect()),"remote workbench pagination does not overlap "+footer);}
                        int oldPage=guest.RecipePage;await UiStyleClick("›");UiStyleRequire(guest.RecipePage>oldPage,"remote workbench next-page callback changes source page");await Present();
                        await UiStyleClick("‹");UiStyleRequire(guest.RecipePage==oldPage,"remote workbench previous-page callback restores source page");await Present();
                        await UiStyleClick("装备维护");UiStyleRequire(guest.Screen=="maintenance","remote workbench maintenance callback opens actual maintenance menu");await Present();
                        await RemoteMenuCheck("maintenance", "返回制作设施", samples);
                        await UiStyleClick("返回制作设施");UiStyleRequire(guest.Screen=="workbench","remote maintenance back callback restores workbench");await Present();
                    }
                    // Resize a live reconstructed menu without replacing its
                    // document. Adaptive layout must respond on the client.
                    GetWindow().Size=new(1366,640);
                    if(variableLogical)GetWindow().ContentScaleSize=new(1366,640);
                    await UiStyleFrames(10);
                    await RemoteMenuCheck(menu+"-resized",back,samples);
                    await UiStyleClick(back);UiStyleRequire(guest.Screen=="play"&&screen=="play","remote return callback remains accessible after client resize: "+menu);
                }
            }
            File.WriteAllText(Path.Combine(uiStyleDirectory,"remote-menu-layout.json"),JsonSerializer.Serialize(new{passed=uiStyleFailures.Count==0,actualNativeRendering=true,
                actualHiddenGuestTree=true,zeroFrameCapture=true,jsonRoundTrip=true,fontScale=1.4,callbacks=callbackCount,samples},Catalog.JsonOptions));
        }finally{WithGuest(guest,ReturnCraftingItems);actor.QueueFree();hidden.QueueFree();GetWindow().ContentScaleSize=originalLogicalSize;ClearWorldView();ShowMenu();}
    }

    private async Task RemoteMenuWindow(Vector2I physical,Vector2I logical)
    {
        settings.UiScale=1.4f;settings.WindowWidth=1280;settings.WindowHeight=720;ApplySettings(true);
        GetWindow().ContentScaleSize=logical;GetWindow().Size=physical;await UiStyleFrames(4);
        var actual=GetViewport().GetVisibleRect().Size;
        UiStyleRequire(Math.Abs(actual.X-logical.X)<1&&Math.Abs(actual.Y-logical.Y)<1,"requested logical viewport applied: "+logical);
    }

    private async Task RemoteMenuCheck(string name,string back,List<object> samples)
    {
        await UiStyleCapture("remote-"+name,back);
        var frame=UiStyleControls<LanternScreenFrame>().Single();
        UiStyleRequire(frame.Panel!=null&&frame.Scroller!=null&&frame.Scroller.Size.Y>280,"remote adaptive frame lays out body instead of a top strip: "+name);
        var bounds=new Rect2(Vector2.Zero,GetViewport().GetVisibleRect().Size);
        foreach(var slot in UiStyleControls<RemoteSlotButton>()){
            Rect2 visible=slot.GetGlobalRect().Intersection(bounds);
            for(Node? parent=slot.GetParent();parent!=null&&parent!=overlay;parent=parent.GetParent())if(parent is ScrollContainer scroll)visible=visible.Intersection(scroll.GetGlobalRect());
            UiStyleRequire(visible.Size.X>=slot.GetGlobalRect().Size.X-2&&visible.Size.Y>=slot.GetGlobalRect().Size.Y-2,"remote slot fully visible: "+name+" token"+slot.Token);
        }
        var extent=GetViewport().GetVisibleRect().Size;var physical=GetWindow().Size;
        var panelMinimum=frame.Panel!.GetCombinedMinimumSize();var returnRect=UiStyleButton(back).GetGlobalRect();
        samples.Add(new{name,clientPhysical=new[]{physical.X,physical.Y},clientLogical=new[]{extent.X,extent.Y},frameHeight=frame.Scroller!.Size.Y,
            panelMinimum=new[]{panelMinimum.X,panelMinimum.Y},returnRect=new[]{returnRect.Position.X,returnRect.Position.Y,returnRect.Size.X,returnRect.Size.Y},slotCount=UiStyleControls<RemoteSlotButton>().Count()});
    }
}
