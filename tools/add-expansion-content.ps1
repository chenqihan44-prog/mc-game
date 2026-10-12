$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$items=[Collections.Generic.List[object]]::new(); (Get-Content (Join-Path $root 'data/items.json') -Raw | ConvertFrom-Json) | ForEach-Object {$items.Add($_)}
$recipes=[Collections.Generic.List[object]]::new(); (Get-Content (Join-Path $root 'data/recipes.json') -Raw | ConvertFrom-Json) | ForEach-Object {$recipes.Add($_)}
function Item($id,$name,$color,$use='',$power=0,$description='',$extra=@{}) {
    if($items.Id -contains $id){return}
    $d=@{Id=$id;Name=$name;Color=$color;Use=$use;Power=$power;Description=$description};foreach($k in $extra.Keys){$d[$k]=$extra[$k]};$items.Add([pscustomobject]$d)
}
function Recipe($id,$inputs,$station='workbench',$count=1,$seconds=0,$output=$id){
    if($recipes.Id -contains $id){return};$recipes.Add([pscustomobject]@{Id=$id;Name=($items | Where-Object Id -eq $output).Name;Inputs=$inputs;Output=$output;Station=$station;Count=$count;Seconds=$seconds})
}
Item 'shears' '剪刀' '#d8dde0' 'shears' 0 '右键剪羊毛或采集树叶、植物纤维。' @{Stack=1;Durability=180}
Item 'iron_hoe' '铁锄' '#b7c1c8' 'hoe' 0 '右键草地或泥土翻成耕地；上方需要净空。' @{Stack=1;Durability=220}
Item 'fishing_rod' '钓鱼竿' '#aa793e' 'fish' 0 '对水面右键抛竿；出现鱼讯后右键收线。' @{Stack=1;Durability=160}
Item 'bucket' '铁桶' '#b9c4cd' 'bucket' 0 '取水，或对成年牛挤奶。' @{Stack=1}
Item 'water_bucket' '水桶' '#77bde1' 'bucket' 0 '右键放置静态水源，浇灌附近耕地；留下铁桶。' @{Stack=1}
Item 'milk_bucket' '牛奶桶' '#f7e9cb' 'drink' 0 '补充饥饿并清除短时药水效果；饮后返还铁桶。' @{Stack=1}
Item 'bone' '骨头' '#dfdfc9' '' 0 '骷髅掉落；用于驯服狼或磨骨粉。'
Item 'bone_meal' '骨粉' '#ebe4d3' 'fertilizer' 0 '右键作物推进一个生长阶段。'
Item 'sugar_cane' '甘蔗' '#89ad4f' 'cane' 0 '岸边采集，水边沙土栽种，成熟后Q收获；加工成纸。' @{Block=$true;Solid=$false;Shape='bush';Hardness=.3}
Item 'paper' '纸' '#e8dcc1' '' 0 '制作地图、附魔台和书籍材料。'
Item 'pumpkin' '南瓜' '#d49b3f' '' 0 '成熟作物，建筑装饰；分解种子或烹饪南瓜派。' @{Block=$true}
Item 'pumpkin_seed' '南瓜种子' '#d5bd85' 'seed' 0 '右键耕地种植南瓜。'
Item 'melon' '西瓜' '#77a541' 'food' 15 '食用补充饥饿；可分解种子再次种植。'
Item 'melon_seed' '西瓜种子' '#61503b' 'seed' 0 '右键耕地种植西瓜。'
Item 'apple' '苹果' '#d85649' 'food' 12 '橡树叶少量掉落；食用或制作金苹果。'
Item 'raw_cod' '生鳕鱼' '#b4bec1' 'food' 8 '钓鱼或捕捉鳕鱼获得；喂猫或烹饪。'
Item 'cooked_cod' '熟鳕鱼' '#ca9d58' 'food' 28 '营火烤熟的远征口粮。'
Item 'bowl' '碗' '#987342' '' 0 '汤食容器，食用后可重复使用。'
Item 'mushroom_stew' '蘑菇煲' '#bd9362' 'food' 36 '两种蘑菇与碗制作，食后返还碗。' @{Stack=1}
Item 'golden_apple' '金苹果' '#f5cc55' 'tonic' 0 '回复12生命，提供16点吸收护盾90秒；共享药水冷却，不叠加。'
Item 'gold_ore' '金矿石' '#d6b657' '' 0 '新世界深层金矿，铁镐采集后熔炉冶炼。' @{Block=$true;Tier=3;Hardness=2.5}
Item 'gold_ingot' '金锭' '#e5ba44' '' 0 '金苹果、金甲与附魔材料。'
Item 'glass_bottle' '玻璃瓶' '#a5cad6' 'bottle' 0 '对水面右键装水；制作药水，饮后返还。'
Item 'water_bottle' '水瓶' '#7eafda' 'drink' 0 '酿造用水；饮用返还玻璃瓶，不作为食物。'
Item 'brewing_stand' '酿造台' '#d7ad59' 'brewing_stand' 0 'Q打开炼金配方；植物、晶矿与水瓶制作补给。' @{Block=$true;Hardness=2;Shape='brewing'}
Item 'healing_potion' '治疗药水' '#d45767' 'tonic' 0 '回复30生命，共享药水8秒冷却，返还玻璃瓶。' @{Stack=8}
Item 'fire_resistance_potion' '抗火药水' '#edaa4c' 'tonic' 0 '120秒内降低75%喷口与灼烧伤害，返还玻璃瓶；不抵消重击。' @{Stack=8}
Item 'egg' '鸡蛋' '#ead8ac' '' 0 '成年鸡定时产蛋；用于南瓜派与繁殖补给。'
Item 'feather' '羽毛' '#ede6d9' '' 0 '鸡掉落；制作箭与滑翔装备。'
Item 'string' '蛛丝' '#cecec4' '' 0 '蜘蛛掉落；制作绳索、钓鱼竿。'
Item 'gunpowder' '火药' '#57545a' '' 0 '苦力怕掉落；制作烟花，滑翔时可短时加速。'
Item 'ink_sac' '墨囊' '#41475b' '' 0 '鱿鱼掉落；制作染料和探索地图。'
Item 'armadillo_scute' '犰狳鳞甲' '#a68874' '' 0 '对成年犰狳用剪刀梳理收集；制作与修理狼铠。'
Item 'turtle_scute' '海龟鳞甲' '#7a9d42' '' 0 '海龟幼体成年时留下；制作海龟壳。'
Item 'saddle' '鞍' '#aa774c' '' 0 '对驯服的马右键装鞍；Q骑乘，Shift下马。' @{Stack=1}
Item 'red_dye' '红染料' '#d5554c' 'dye' 0 '工作台装备维护中给皮革甲、狼铠染色。'
Item 'blue_dye' '蓝染料' '#5f83ba' 'dye' 0 '工作台装备维护中给皮革甲、狼铠染色。'
Item 'enchanting_table' '附魔台' '#805294' 'enchanting_table' 0 'Q消耗经验与晶矿，给护甲附加保护或耐久；金甲收益较高。' @{Block=$true;Hardness=2}
Item 'firework' '烟花火箭' '#da785e' 'firework' 0 '滑翔时右键使用：短时推进，不能从地面起飞。'
Item 'pumpkin_pie' '南瓜派' '#c69959' 'food' 38 '南瓜、鸡蛋和小麦烹饪的饱腹食品。'
Item 'red_wool' '红色羊毛' '#bb594c' '' 0 '羊毛染色后用于建筑装饰。' @{Block=$true}
Item 'blue_wool' '蓝色羊毛' '#6084b4' '' 0 '羊毛染色后用于建筑装饰。' @{Block=$true}
$materials=@(
    @('leather','皮革','#a97649','leather',@(1,3,2,1),@(65,96,90,78)),
    @('copper','铜','#cc8859','copper_ingot',@(2,5,4,1),@(110,160,150,130)),
    @('chain','锁链','#9a9da6','iron_ingot',@(2,5,4,1),@(140,200,185,165)),
    @('gold','金','#dfb743','gold_ingot',@(2,5,3,1),@(77,112,105,91)),
    @('crystal','晶矿','#82e9d0','crystal',@(3,7,5,3),@(300,440,410,360)),
    @('ember','余烬合金','#e7a267','ember_alloy',@(3,8,6,3),@(410,600,560,490)))
$suffix=@('helmet','chestplate','leggings','boots');$names=@('头盔','胸甲','护腿','靴子');$cost=@(5,8,7,4)
foreach($m in $materials){for($s=0;$s -lt 4;$s++){
    $id=$m[0]+'_'+$suffix[$s];Item $id ($m[1]+$names[$s]) $m[2] 'armor' 0 '' @{Stack=1;ArmorSlot=$s;ArmorPoints=$m[4][$s];Durability=$m[5][$s]}
    if($m[0] -eq 'chain'){continue}
    if($m[0] -eq 'ember'){Recipe $id @{($m[3])=3;('crystal_'+$suffix[$s])=1} 'calibration_bench'}else{Recipe $id @{($m[3])=$cost[$s]}}
}}
Item 'turtle_shell' '海龟壳' '#629f51' 'armor' 0 '头部装备，潜水耗气降低65%。' @{Stack=1;ArmorSlot=0;ArmorPoints=2;Durability=240}
Item 'elytra' '鞘翅' '#8d84b2' 'armor' 0 '占用胸部防护；空中按Space开始滑翔。主线完成后校准台制作。' @{Stack=1;ArmorSlot=1;ArmorPoints=0;Durability=360}
Item 'wolf_armor' '狼铠' '#bd7e69' 'animal_armor' 10 '右键为已驯服狼装备，吸收伤害并损耗耐久；Q管理与修理。' @{Stack=1;Durability=220}
foreach($m in @(@('leather','皮革','#a97649','leather',3),@('copper','铜','#cc8859','copper_ingot',5),@('iron','铁','#b9c4c8','iron_ingot',7),@('gold','金','#dfb743','gold_ingot',9),@('crystal','晶矿','#82e9d0','crystal',11),@('ember','余烬合金','#e7a267','ember_alloy',13))){
    $id=$m[0]+'_horse_armor';Item $id ($m[1]+'马铠') $m[2] 'animal_armor' $m[4] '对已驯服马右键装备；Q管理坐骑装备。' @{Stack=1;Durability=300+$m[4]*20}
    if($m[0] -eq 'ember'){Recipe $id @{ember_alloy=4;crystal_horse_armor=1} 'calibration_bench'}else{Recipe $id @{($m[3])=7;cloth=1}}
}
Recipe 'shears' @{iron_ingot=2};Recipe 'iron_hoe' @{iron_ingot=2;stick=2};Recipe 'fishing_rod' @{stick=3;rope=2};Recipe 'bucket' @{iron_ingot=3}
Recipe 'bone_meal' @{bone=1} 'hand' 3;Recipe 'paper' @{sugar_cane=3} 'hand' 3
Recipe 'pumpkin_seed' @{pumpkin=1} 'hand' 4;Recipe 'melon_seed' @{melon=1} 'hand' 4
Recipe 'bowl' @{plank=3} 'hand' 4;Recipe 'mushroom_stew' @{bowl=1;brown_mushroom=1;red_mushroom=1} 'hand'
Recipe 'golden_apple' @{apple=1;gold_ingot=8};Recipe 'gold_ingot' @{gold_ore=1;coal=1} 'furnace' 1 14
Recipe 'glass_bottle' @{glass=3} 'hand' 3;Recipe 'brewing_stand' @{iron_ingot=2;copper_ingot=2;cobble=3}
Recipe 'cooked_cod' @{raw_cod=1} 'campfire' 1 6
Recipe 'healing_potion' @{water_bottle=1;red_mushroom=1;red_flower=1;coal=1} 'brewing_stand' 1 12
Recipe 'fire_resistance_potion' @{water_bottle=1;cactus=1;crystal=1;coal=1} 'brewing_stand' 1 15
Recipe 'saddle' @{leather=5;iron_ingot=2};Recipe 'turtle_shell' @{turtle_scute=5};Recipe 'wolf_armor' @{armadillo_scute=6}
Recipe 'elytra' @{cloth=8;feather=12;crystal=4} 'calibration_bench'
Recipe 'enchanting_table' @{paper=6;crystal=4;stone=4};Recipe 'red_dye' @{red_flower=1} 'hand' 2;Recipe 'blue_dye' @{ink_sac=1;blue_flower=1} 'hand' 3
Recipe 'firework' @{gunpowder=1;paper=1} 'hand' 3;Recipe 'pumpkin_pie' @{pumpkin=1;egg=1;grain=2} 'campfire' 1 8
Recipe 'string_rope' @{string=3} 'hand' 1 0 'rope';Recipe 'feather_arrow' @{feather=1;stick=1;stone=1} 'hand' 4 0 'arrow';Recipe 'wool_cloth' @{wool=1} 'hand' 2 0 'cloth'
Recipe 'paper_map' @{paper=4;ink_sac=1;copper_ingot=1} 'hand' 1 0 'map';Recipe 'red_wool' @{wool=1;red_dye=1} 'hand';Recipe 'blue_wool' @{wool=1;blue_dye=1} 'hand'
$items | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $root 'data/items.json')
$recipes | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $root 'data/recipes.json')
$creatures=[Collections.Generic.List[object]]::new();(Get-Content (Join-Path $root 'data/creatures.json') -Raw | ConvertFrom-Json) | ForEach-Object {$creatures.Add($_)}
foreach($m in @(@('cow','牛','#a88767',40,1.5,'leather'),@('pig','猪','#dda3a0',28,1.7,'meat'),@('sheep','羊','#e1ded0',28,1.5,'wool'),@('chicken','鸡','#ece5cc',14,1.5,'feather'),@('wolf','狼','#a6abb4',35,3.4,'bone'),@('cat','猫','#d4a861',20,3.2,'fiber'),@('horse','马','#966344',65,3,'leather'),@('fox','狐狸','#cc823b',24,3.4,'berry'),@('armadillo','犰狳','#aa8b79',28,1.2,'armadillo_scute'),@('cod','鳕鱼','#b9ad85',10,1.7,'raw_cod'),@('squid','鱿鱼','#475978',25,1.6,'ink_sac'),@('turtle','海龟','#638e44',35,.8,'fiber'),@('zombie','僵尸','#739b50',48,2.2,'meat'),@('skeleton','骷髅','#cccabd',35,2.4,'bone'),@('creeper','苦力怕','#599a48',40,2.1,'gunpowder'),@('spider','蜘蛛','#383039',30,3.2,'string'))){
    if($creatures.Id -contains $m[0]){continue};$hostile=$m[0] -in @('zombie','skeleton','creeper','spider');$attack=if($m[0] -eq 'skeleton'){'bolt'}elseif($m[0] -eq 'creeper'){'explode'}elseif($m[0] -eq 'wolf' -or $hostile){'melee'}else{'none'}
    $creatures.Add([pscustomobject]@{Id=$m[0];Name=$m[1];Color=$m[2];Health=$m[3];Speed=$m[4];Drop=$m[5];Hostile=$hostile;Attack=$attack;Damage=$(if($hostile){if($m[0] -eq 'creeper'){32}else{10}}elseif($m[0] -eq 'wolf'){8}else{0});Range=$(if($m[0] -eq 'skeleton'){14}else{2.5});Windup=$(if($m[0] -eq 'creeper'){1.6}else{.7});Cooldown=2;AttackSpeed=$(if($m[0] -eq 'skeleton'){14}else{0});AttackDuration=$(if($m[0] -eq 'skeleton'){2}else{0});AttackRadius=$(if($m[0] -eq 'creeper'){3.5}else{.08})})
}
$creatures | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $root 'data/creatures.json')
Write-Output "Expansion catalog: $($items.Count) items, $($recipes.Count) recipes, $($creatures.Count) creatures"
