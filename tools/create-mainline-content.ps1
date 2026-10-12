$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$itemPath=Join-Path $root 'data/items.json'
$recipePath=Join-Path $root 'data/recipes.json'
$creaturePath=Join-Path $root 'data/creatures.json'
$items=@(Get-Content -LiteralPath $itemPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable)
$recipes=@(Get-Content -LiteralPath $recipePath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable)
$creatures=@(Get-Content -LiteralPath $creaturePath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable)
$newItems=@(
 @{Id='copper_coil';Name='铜线圈';Color='#d99557'},
 @{Id='survey_lens';Name='勘测镜';Color='#87d7df';Stack=1;Use='survey'},
 @{Id='heat_lining';Name='耐热衬片';Color='#c59362';Stack=1;Use='lining'},
 @{Id='furnace_prism';Name='炉心棱镜';Color='#ffac51';Stack=1;Use='relic'},
 @{Id='frost_prism';Name='霜辉棱镜';Color='#94ddff';Stack=1;Use='relic'},
 @{Id='calibration_bench';Name='校准台';Color='#687f87';Block=$true;Use='calibration_bench';Hardness=2;Tier=1},
 @{Id='ember_alloy';Name='余烬合金';Color='#e7a267'},
 @{Id='ember_sword';Name='余烬剑';Color='#edb176';Stack=1;Tier=3;Use='sword';Power=17;Durability=600},
 @{Id='ember_bow';Name='余烬弓';Color='#edb176';Stack=1;Tier=3;Use='bow';Power=13;Durability=600},
 @{Id='resonant_core';Name='共鸣晶核';Color='#7cf5d9';Stack=1;Use='relic'},
 @{Id='repair_kit';Name='修理包';Color='#b7b6a1';Use='repair'},
 @{Id='dawn_heart';Name='黎明之心纪念灯';Color='#ffda8b';Block=$true;Use='light';Hardness=1},
 @{Id='copper_vein';Name='铜矿';Color='#a67d60';Block=$true;Hardness=2;Tier=1},
 @{Id='quest_record';Name='古代碑文';Color='#c6b782';Block=$true;Use='mainline';Hardness=100},
 @{Id='power_socket';Name='供能节点';Color='#688f82';Block=$true;Use='mainline';Hardness=100},
 @{Id='reflector';Name='反射器';Color='#96c4ce';Block=$true;Use='mainline';Hardness=100},
 @{Id='core_console';Name='核心控制台';Color='#bf9964';Block=$true;Use='mainline';Hardness=100},
 @{Id='energy_anchor';Name='供能锚点';Color='#f1ba61';Block=$true;Use='mainline';Hardness=100}
)
foreach($item in $newItems){if(!($items | Where-Object Id -eq $item.Id)){$items+=$item}}
function New-Recipe($id,$name,$station,$inputs,$count=1){@{Id=$id;Name=$name;Station=$station;Inputs=$inputs;Output=$id;Count=$count;Seconds=0}}
$newRecipes=@(
 (New-Recipe 'copper_coil' '铜线圈' 'workbench' @{copper_ingot=2;iron_ingot=1}),
 (New-Recipe 'survey_lens' '勘测镜' 'workbench' @{copper_coil=1;glass=2}),
 (New-Recipe 'heat_lining' '耐热衬片' 'workbench' @{leather=2;cloth=2;iron_ingot=1}),
 (New-Recipe 'calibration_bench' '校准台' 'workbench' @{copper_coil=2;plank=4;iron_ingot=4}),
 (New-Recipe 'ember_alloy' '余烬合金' 'calibration_bench' @{iron_ingot=2;crystal=1;coal=1}),
 (New-Recipe 'ember_sword' '余烬剑' 'calibration_bench' @{ember_alloy=3;stick=2}),
 (New-Recipe 'ember_bow' '余烬弓' 'calibration_bench' @{ember_alloy=2;stick=3;rope=2}),
 (New-Recipe 'repair_kit' '修理包' 'workbench' @{iron_ingot=2;cloth=1}),
 (New-Recipe 'dawn_heart' '黎明之心纪念灯' 'calibration_bench' @{iron_ingot=2;glass=2;coal=1})
)
if(!($recipes | Where-Object Id -eq 'beacon_sigil_legacy')){$legacy=($recipes | Where-Object Id -eq 'beacon_sigil').Clone();$legacy.Id='beacon_sigil_legacy';$legacy.Name='灯塔符印（旧世界）';$recipes+=$legacy}
($recipes | Where-Object Id -eq 'beacon_sigil').Inputs=@{iron_ingot=2;crystal=2}
($recipes | Where-Object Id -eq 'beacon_sigil').Station='calibration_bench'
foreach($recipe in $newRecipes){if(!($recipes | Where-Object Id -eq $recipe.Id)){$recipes+=$recipe}}
$newCreatures=@(
 @{Id='mine_warden';Name='矿场哨长';Color='#a88c63';Health=120;Speed=2.2;Damage=22;Range=12;Hostile=$true;Drop='copper_ingot';Attack='bolt';Windup=1.2;Cooldown=2.5;AttackSpeed=10;AttackDuration=3;AttackRadius=0.12},
 @{Id='furnace_warden';Name='炉卫';Color='#d98b46';Health=200;Speed=2.1;Damage=40;Range=9;Hostile=$true;Drop='furnace_prism';Attack='charge';Windup=1.2;Cooldown=3.4;AttackSpeed=9;AttackDuration=0.8;AttackRadius=0.8},
 @{Id='observatory_warden';Name='观测哨长';Color='#8fbad3';Health=180;Speed=2.8;Damage=28;Range=22;Hostile=$true;Drop='frost_prism';Attack='bolt';Windup=1.15;Cooldown=2.2;AttackSpeed=13;AttackDuration=3;AttackRadius=0.12},
 @{Id='colossus';Name='熔心巨像';Color='#e6a15c';Health=1500;Speed=1.8;Damage=65;Range=27;Hostile=$true;Drop='dawn_heart';Attack='slam';Windup=1.4;Cooldown=3.4;AttackRadius=4.5}
)
foreach($creature in $newCreatures){if(!($creatures | Where-Object Id -eq $creature.Id)){$creatures+=$creature}}
$items | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $itemPath -Encoding UTF8
$recipes | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $recipePath -Encoding UTF8
$creatures | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $creaturePath -Encoding UTF8
Write-Output "Mainline content: $($items.Count) items, $($recipes.Count) recipes, $($creatures.Count) creatures"
