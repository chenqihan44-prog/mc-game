using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberLighthouse.Core;
using EmberLighthouse.Creatures;
using EmberLighthouse.Items;
namespace EmberLighthouse.World;

public sealed partial class WorldState
{
    public Grave Drop(Inventory items,double[] position,double delay=.4,bool death=false)
    {
        var g=new Grave{Items=items.Copy(),Position=(double[])position.Clone(),PickupAfter=State.Time+delay,Death=death};State.Graves.Add(g);return g;
    }
    public Grave Drop(Stack item,double[] position,double delay=.4){var items=new Inventory(1);items.Slots[0]=item.Copy();return Drop(items,position,delay);}
    public static double[] ItemPosition(Grave grave,int index)
    {
        if(grave.ItemPositions.TryGetValue(index,out var p))return p;
        int total=grave.Items.Slots.Count(i=>i!=null);double angle=index*2.399963,spread=total>1?.28+.025*Math.Sqrt(index%16):0;
        p=[grave.Position[0]+Math.Cos(angle)*spread,grave.Position[1]+.22,grave.Position[2]+Math.Sin(angle)*spread];grave.ItemPositions[index]=p;return p;
    }
    public int PickTouchingItems(double[] feet,double height=1.8)
    {
        int picked=0;foreach(var g in State.Graves.ToArray()){
            if(State.Time<g.PickupAfter)continue;
            for(int i=0;i<g.Items.Slots.Length;i++)if(g.Items.Slots[i] is {} item){var p=ItemPosition(g,i);double dx=p[0]-feet[0],dz=p[2]-feet[2];if(dx*dx+dz*dz>.52*.52||p[1]<feet[1]-.18||p[1]>feet[1]+height)continue;
                if(!CombatGeometry.Clear(this,new((float)p[0],(float)p[1],(float)p[2]),new((float)feet[0],(float)feet[1]+.4f,(float)feet[2])))continue;
                int remaining=State.Inventory.Add(item,Catalog);picked+=item.Count-remaining;if(remaining==0){g.Items.Slots[i]=null;g.ItemPositions.Remove(i);g.ItemVelocities.Remove(i);}else item.Count=remaining;
            }
            if(g.Items.Slots.All(i=>i==null))State.Graves.Remove(g);
        }return picked;
    }
    public void TickDroppedItems(double delta,Func<Vector3,bool> ready)
    {
        float dt=(float)Math.Clamp(delta,0,.05);if(dt==0)return;
        foreach(var g in State.Graves){for(int i=0;i<g.Items.Slots.Length;i++)if(g.Items.Slots[i]!=null){var point=ItemPosition(g,i);var p=new Vector3((float)point[0],(float)point[1],(float)point[2]);if(!ready(p))continue;
            var stored=g.ItemVelocities.GetValueOrDefault(i)??[0,0,0];var velocity=new Vector3((float)stored[0],(float)stored[1],(float)stored[2]);velocity.Y=Math.Max(-18,velocity.Y-18*dt);
            if(Catalog.Id(Get(CombatGeometry.CellAt(p)))=="water")velocity.Y=Math.Min(1,velocity.Y+24*dt);
            Vector3 next=p+velocity*dt;bool blocked=false;
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Abs(next.Y-p.Y)/.04));for(int step=1;step<=steps;step++){var sample=Vector3.Lerp(p,next,step/(float)steps);if(CombatGeometry.Blocks(this,sample-Vector3.UnitY*.13f)){blocked=true;break;}p=sample;}
            if(blocked)velocity.Y=0;
            p.X=Math.Clamp(p.X,-Coordinates.Limit+.2f,Coordinates.Limit-.2f);p.Z=Math.Clamp(p.Z,-Coordinates.Limit+.2f,Coordinates.Limit-.2f);p.Y=Math.Max(.2f,p.Y);
            g.ItemPositions[i]=[p.X,p.Y,p.Z];g.ItemVelocities[i]=[velocity.X,velocity.Y,velocity.Z];
        }}
    }
    public void InitializeRewards()
    {
        if(State.BonusChestInitialized)return;State.BonusChestInitialized=true;if(!State.BonusChest||State.Creative)return;
        for(int attempt=0;attempt<100;attempt++){uint n=RandomField.Hash(State.Seed,attempt,0,0,333);int x=(int)(n%13)-6,z=(int)((n>>8)%13)-6;if(x*x+z*z<9)continue;var p=new Cell(x,Terrain.Surface(x,z)+1,z);if(Get(p)!=0||!TorchMount.Sturdy(this,p+new Cell(0,-1,0)))continue;
            Set(p,Catalog.Number("chest"));var storage=new Inventory(27);foreach(var item in new[]{new Stack("wood",6),new("apple",3),new("torch",3),new("wood_pick",1)})storage.Add(item,Catalog);State.Entities[p.ToString()]=new(){Kind="chest",LootTheme="出生奖励宝箱",Storage=storage,LastTime=State.Time};break;
        }
    }
    public void RepairTorchMounts()
    {
        foreach(var entry in State.Entities.Where(e=>e.Value.Kind=="torch").ToArray()){var p=Cell.Parse(entry.Key);if(TorchMount.Sturdy(this,TorchMount.Support(p,entry.Value.Stage)))continue;int found=TorchMount.Find(this,p,null);if(found>=0){entry.Value.Stage=found;Set(p,Get(p));}else{Set(p,0);State.Entities.Remove(entry.Key);Drop(new Stack("torch",1),[p.X+.5,p.Y+.3,p.Z+.5]);}}
    }
}
