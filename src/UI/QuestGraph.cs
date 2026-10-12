using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.UI;

public partial class QuestGraph : Control
{
    public Catalog Catalog {get;set;}=null!;
    public List<QuestNode> Nodes {get;set;}=[];
    public Action<QuestNode>? Selected {get;set;}
    public string SelectedId {get;private set;}="";
    public Vector2 Center {get;private set;}=new(400,265);
    public float Zoom {get;private set;}=.9f;
    private bool dragging;
    private Vector2 cursor;
    private readonly Vector2 nodeSize=new(206,104);
    private StyleBoxFlat completed=null!,available=null!,locked=null!;
    public override void _Ready()
    {
        FocusMode=FocusModeEnum.All;ClipContents=true;MouseDefaultCursorShape=CursorShape.Drag;
        StyleBoxFlat Style(string bg,string border)=>new(){BgColor=new(bg),BorderColor=new(border),BorderWidthBottom=2,BorderWidthTop=2,BorderWidthLeft=2,BorderWidthRight=2,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8,CornerRadiusTopLeft=8,CornerRadiusTopRight=8};
        completed=Style("183f36","65c99c");available=Style("3a3425","e1ad55");locked=Style("202832","4c5765");SelectNode(QuestJournal.Current(Nodes).Id,true);
    }
    public Vector2 ScreenPoint(Vector2 world)=>(world-Center)*Zoom+Size/2;
    public Vector2 WorldPoint(Vector2 screen)=>(screen-Size/2)/Zoom+Center;
    public Rect2 NodeRect(QuestNode node)=>new(ScreenPoint(new(node.X,node.Y)),nodeSize*Zoom);
    public void SelectNode(string id,bool focus=false)
    {
        var node=Nodes.FirstOrDefault(n=>n.Id==id);if(node==null)return;SelectedId=id;if(focus)Center=new(node.X+nodeSize.X*.5f,node.Y+nodeSize.Y*.5f);Selected?.Invoke(node);QueueRedraw();
    }
    public void SetView(Vector2 center,float zoom){Center=new(Math.Clamp(center.X,-500,3200),Math.Clamp(center.Y,-450,1000));Zoom=Math.Clamp(zoom,.23f,1.6f);QueueRedraw();}
    public void FocusCurrent(){Zoom=.9f;SelectNode(QuestJournal.Current(Nodes).Id,true);}
    public void Fit(){float left=Nodes.Min(n=>n.X),top=Nodes.Min(n=>n.Y),width=Nodes.Max(n=>n.X)+nodeSize.X-left,height=Nodes.Max(n=>n.Y)+nodeSize.Y-top;Center=new(left+width/2,top+height/2);Zoom=Math.Clamp(Math.Min((Size.X-70)/width,(Size.Y-70)/height),.23f,1.6f);QueueRedraw();}
    public void ZoomAt(Vector2 anchor,float factor){var point=WorldPoint(anchor);float zoom=Math.Clamp(Zoom*factor,.23f,1.6f);SetView(point-(anchor-Size/2)/zoom,zoom);}
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton button){
            cursor=button.Position;
            if(button.ButtonIndex is MouseButton.Left or MouseButton.Middle){
                if(button.Pressed){GrabFocus();var node=Nodes.LastOrDefault(n=>NodeRect(n).HasPoint(button.Position));if(node!=null&&button.ButtonIndex==MouseButton.Left){SelectNode(node.Id);dragging=false;}else dragging=true;}else dragging=false;AcceptEvent();
            }else if(button.Pressed&&button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){ZoomAt(button.Position,button.ButtonIndex==MouseButton.WheelUp?1.2f:1/1.2f);AcceptEvent();}
        }else if(e is InputEventMouseMotion motion){cursor=motion.Position;if(dragging)SetView(Center-motion.Relative/Zoom,Zoom);var node=Nodes.LastOrDefault(n=>NodeRect(n).HasPoint(cursor));TooltipText=node==null?"":node.Title+" · "+StatusName(node.Status);}
        else if(e is InputEventPanGesture pan){SetView(Center+pan.Delta*25/Zoom,Zoom);AcceptEvent();}
        else if(e is InputEventKey{Pressed:true} key&&key.Keycode is Key.Left or Key.Right){int i=Nodes.FindIndex(n=>n.Id==SelectedId);SelectNode(Nodes[Math.Clamp(i+(key.Keycode==Key.Right?1:-1),0,Nodes.Count-1)].Id,true);AcceptEvent();}
    }
    public static string StatusName(QuestStatus status)=>status switch{QuestStatus.Completed=>"已完成",QuestStatus.Available=>"可进行",_=>"待解锁"};
    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero,Size),new("101922"));
        for(int x=0;x<Size.X;x+=32)for(int y=0;y<Size.Y;y+=32)DrawCircle(new(x,y),1,new Color(1,1,1,.06f));
        foreach(var (from,to) in QuestJournal.Edges(Nodes)){
            var a=Nodes.First(n=>n.Id==from);var b=Nodes.First(n=>n.Id==to);var start=ScreenPoint(new(a.X+nodeSize.X,a.Y+nodeSize.Y/2));var end=ScreenPoint(new(b.X,b.Y+nodeSize.Y/2));var points=new Vector2[17];
            for(int i=0;i<points.Length;i++){float t=i/16f;points[i]=start.BezierInterpolate(start+new Vector2(70*Zoom,0),end-new Vector2(70*Zoom,0),end,t);}DrawPolyline(points,b.Status==QuestStatus.Locked?new("465363"):b.Done?new("65c99c"):new("dcaa58"),Math.Max(1,3*Zoom),true);
            DrawColoredPolygon([end,end+new Vector2(-9,-5)*Zoom,end+new Vector2(-9,5)*Zoom],b.Status==QuestStatus.Locked?new("465363"):new("dcaa58"));
        }
        var font=GetThemeFont("font");foreach(var node in Nodes){
            var rect=NodeRect(node);DrawStyleBox(node.Status==QuestStatus.Completed?completed:node.Status==QuestStatus.Available?available:locked,rect);if(node.Id==SelectedId)DrawRect(rect.Grow(4),new("e9f5ff"),false,2);
            if(Catalog.Items.TryGetValue(node.Icon,out var icon))DrawTextureRect(ItemSprites.Texture(icon),new(rect.Position+new Vector2(10,20)*Zoom,Vector2.One*42*Zoom),false,node.Status==QuestStatus.Locked?new(.55f,.55f,.55f):Colors.White);
            int size=Math.Max(10,(int)(18*Zoom));var color=node.Status==QuestStatus.Locked?new Color("91a0b0"):Colors.White;DrawString(font,rect.Position+new Vector2(61,39)*Zoom,node.Title,width:140*Zoom,fontSize:size,modulate:color);
            if(Zoom>=.48f){DrawString(font,rect.Position+new Vector2(61,66)*Zoom,StatusName(node.Status),fontSize:Math.Max(10,(int)(14*Zoom)),modulate:node.Done?new("83d9b8"):node.Status==QuestStatus.Available?new("f0c77c"):new("7f8c9d"));DrawString(font,rect.Position+new Vector2(12,91)*Zoom,node.Id is "forge" or "snow"?"双域分支 · 顺序自选":node.Id is "bridge" or "reflector" or "guard"?"三节点分支 · 全部完成":"点击查看目标与准备",fontSize:Math.Max(9,(int)(11*Zoom)),modulate:new("9dabb6"));}
        }
    }
    public override void _ExitTree(){completed?.Dispose();available?.Dispose();locked?.Dispose();}
}
