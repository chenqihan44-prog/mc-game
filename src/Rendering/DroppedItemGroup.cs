using System;
using System.Collections.Generic;
using System.Linq;
using EmberLighthouse.Items;
using EmberLighthouse.World;
using Godot;
namespace EmberLighthouse.Rendering;

public partial class DroppedItemGroup : Node3D
{
    private readonly Dictionary<int,(PresentationModel Model,Label3D Label,string Appearance)> views=[];
    public void Update(Grave grave,Catalog catalog,Vector3 player,double time,Func<Vector3,Color> lightAt)
    {
        foreach(int slot in views.Keys.ToArray())if(slot>=grave.Items.Slots.Length||grave.Items.Slots[slot]==null){var old=views[slot];RemoveChild(old.Model);old.Model.QueueFree();old.Label.QueueFree();views.Remove(slot);}
        int budget=0;for(int i=0;i<grave.Items.Slots.Length&&budget<64;i++)if(grave.Items.Slots[i] is {} item){budget++;
            string key=item.Id+item.Dye;if(!views.TryGetValue(i,out var view)||view.Appearance!=key){if(view.Model!=null){RemoveChild(view.Model);view.Model.QueueFree();view.Label.QueueFree();}var def=catalog.Items[item.Id];if(item.Dye!="")def=def with{Color=ArmorTraits.Color(item,catalog)};var model=PresentationModel.Item(def);model.Scale=Vector3.One*.55f;AddChild(model);var label=new Label3D{FontSize=20,PixelSize=.004f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=false};AddChild(label);view=(model,label,key);views[i]=view;}
            var p=WorldState.ItemPosition(grave,i);var at=new Vector3((float)p[0],(float)p[1],(float)p[2]);view.Model.SetWorldLight(lightAt(at));view.Model.Position=at+Vector3.Up*(float)(.025*Math.Sin(time*2+i));view.Model.Rotation=new(0,(float)(time*.7+i),0);view.Label.Position=at+Vector3.Up*.42f;view.Label.Text=catalog.Items[item.Id].Name+(item.Count>1?" ×"+item.Count:"");view.Label.Visible=at.DistanceSquaredTo(player)<2.5f*2.5f;
        }
    }
}
