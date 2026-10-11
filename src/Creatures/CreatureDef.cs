using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmberLighthouse.Items;

namespace EmberLighthouse.Creatures;

public sealed record CreatureDef(string Id,string Name,string Color,double Health,double Speed,double Damage,double Range,bool Hostile,string Drop,string Attack="melee",double Windup=.7,double Cooldown=2,double AttackSpeed=0,double AttackDuration=0,double AttackRadius=0);

public static class CreatureRules
{
    public static bool Known(string id)=>id is "deer" or "boar" or "skitter" or "husk" or "sentinel" or "guardian";
    public static bool Night(double time)=>time%1200>=600;
    public static void Validate(IReadOnlyList<CreatureDef> definitions,Catalog catalog)
    {
        if(definitions.Count!=6||definitions.Select(d=>d.Id).Distinct().Count()!=6)throw new InvalidDataException("需要六种不同的生物定义");
        foreach(var d in definitions)if(!Known(d.Id)||string.IsNullOrWhiteSpace(d.Name)||!double.IsFinite(d.Health)||d.Health<=0||!double.IsFinite(d.Speed)||d.Speed<=0||!double.IsFinite(d.Damage)||d.Damage<0||!double.IsFinite(d.Range)||d.Range<=0||!catalog.Items.ContainsKey(d.Drop))throw new InvalidDataException("生物参数或掉落引用无效："+d.Id);
        foreach(var d in definitions)if(d.Attack is not ("none" or "bite" or "melee" or "charge" or "bolt" or "slam")||!double.IsFinite(d.Windup)||d.Windup<0||d.Windup>5||!double.IsFinite(d.Cooldown)||d.Cooldown<0||d.Cooldown>10||!double.IsFinite(d.AttackSpeed)||d.AttackSpeed<0||d.AttackSpeed>40||!double.IsFinite(d.AttackDuration)||d.AttackDuration<0||d.AttackDuration>4||!double.IsFinite(d.AttackRadius)||d.AttackRadius<0||d.AttackRadius>6||d.Attack is "charge" or "bolt"&&(d.AttackSpeed<=0||d.AttackDuration<=0)||d.Attack=="slam"&&d.AttackRadius<=0)throw new InvalidDataException("生物攻击参数无效："+d.Id);
    }
}
