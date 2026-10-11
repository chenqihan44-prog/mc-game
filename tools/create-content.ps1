$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$items=[System.Collections.Generic.List[object]]::new()
$recipes=[System.Collections.Generic.List[object]]::new()
function Item($id,$name,$color,$block=$false,$solid=$true,$hardness=1,$tier=0,$shape='cube',$use='',$power=0,$stack=64){
    $items.Add(@{Id=$id;Name=$name;Color=$color;Block=$block;Solid=$solid;Hardness=$hardness;Tier=$tier;Shape=$shape;Use=$use;Power=$power;Stack=$stack})
}
function Recipe($id,$name,$inputs,$output,$count=1,$station='hand',$seconds=0){$recipes.Add(@{Id=$id;Name=$name;Inputs=$inputs;Output=$output;Count=$count;Station=$station;Seconds=$seconds})}
Item grass 草地 '#729752' $true
Item dirt 泥土 '#80604c' $true
Item stone 岩石 '#879090' $true $true 1.6 1
Item sand 沙子 '#d3bd80' $true
Item snow 雪 '#dfebee' $true
Item basalt 玄武岩 '#444b55' $true $true 2 1
Item wood 原木 '#8c6942' $true $true 1.5
Item leaves 树叶 '#467449' $true $false 0.3
Item water 静态水 '#498faa' $true $false 0
Item coal_ore 煤矿 '#596265' $true $true 2 1
Item iron_ore 铁矿 '#a88770' $true $true 2.5 1
Item crystal_ore 晶矿 '#67c7cf' $true $true 3 2
Item bedrock 基岩 '#252c34' $true $true 999 3
Item loose_stone 碎石 '#9b9d8b' $true $false 0.2 0 slab
Item berry_bush 莓果灌木 '#507b42' $true $false 0.3 0 bush
Item clay 黏土 '#b5ada1' $true
Item gravel 砾石 '#9f9586' $true
Item ice 冰 '#a1d7e3' $true
Item cactus 仙人掌 '#728f42' $true
Item moss 苔藓 '#576d47' $true
Item plank 木板 '#c79a61' $true
Item brick 红砖 '#a36049' $true
Item cobble 圆石 '#777b77' $true
Item ruin_brick 遗迹石砖 '#727f83' $true $true 2 1
Item sandstone 砂岩 '#c7ad79' $true
Item glass 玻璃 '#bddde3' $true
Item stairs 木楼梯 '#be955f' $true $true 1 0 stairs
Item slab 木台阶 '#be955f' $true $true 1 0 slab
Item door 木门 '#8f693e' $true $true 1 0 door
Item window 窗框 '#d0b286' $true $true 1 0 window
Item fence 栅栏 '#a27b4c' $true $true 1 0 fence
Item pillar 石柱 '#aeb6ad' $true
Item roof 屋顶 '#6c7883' $true $true 1 0 stairs
Item tile 地砖 '#c0a18a' $true
Item wool 羊毛块 '#ede4d5' $true
Item rug 地毯 '#b46848' $true $false 0.3 0 rug
Item lantern 提灯 '#f9cc71' $true $false 0.6 0 cube light
Item marble 大理石 '#cdd1c8' $true $true 2 1
Item copper_block 铜块 '#b27653' $true
Item polished_basalt 磨制玄武岩 '#565762' $true
Item workbench 工作台 '#b28045' $true $true 1 0 cube workbench
Item campfire 营火 '#e5a053' $true $false 0.5 0 slab campfire
Item furnace 熔炉 '#58616a' $true $true 2 1 cube furnace
Item chest 木箱 '#a57643' $true $true 1 0 cube chest
Item bed 床 '#b76952' $true $true 1 0 slab bed
Item beacon 古代灯塔核心 '#76d7db' $true $true 999 3 cube beacon
Item farmland 耕地 '#664d33' $true
Item bookshelf 书架 '#8b774f' $true
$tools=@('pick','axe','sword','shovel');$toolNames=@('镐','斧','剑','铲');$tiers=@('stone','iron','crystal');$tierNames=@('石','铁','晶矿')
for($t=0;$t -lt 3;$t++){for($i=0;$i -lt 4;$i++){Item ($tiers[$t]+'_'+$tools[$i]) ($tierNames[$t]+$toolNames[$i]) '#c4c7b9' $false $true 1 ($t+1) cube $tools[$i] (4+5*$t) 1}}
Item bow 弓 '#bd8f58' $false $true 1 0 cube bow 8 1
Item arrow 箭 '#d3cfb9'
Item shield 盾牌 '#b69a71' $false $true 1 0 cube shield 0 1
$materials=@(
@('raw_iron','粗铁','#b69279'),@('iron_ingot','铁锭','#bdc3c7'),@('coal','煤炭','#3b4147'),@('crystal','晶矿','#73ced5'),
@('stick','木棍','#b28957'),@('fiber','植物纤维','#94a46a'),@('rope','绳索','#c8ba82'),@('cloth','布料','#e7dcc7'),@('leather','皮革','#a07c52'),
@('berry','莓果','#d68575'),@('cooked_berry','莓果汤','#dd9576'),@('grain','麦粒','#d7b263'),@('bread','面包','#d19e57'),
@('carrot','胡萝卜','#df934f'),@('cooked_carrot','炖胡萝卜','#d9a467'),@('potato','马铃薯','#baa87a'),@('baked_potato','烤马铃薯','#d1b47d'),
@('wheat_seed','小麦种子','#b7a05a'),@('carrot_seed','胡萝卜种子','#bc915e'),@('potato_seed','马铃薯种薯','#baa17c'),
@('meat','生肉','#d58979'),@('cooked_meat','熟肉','#c3966b'),@('copper_ore','铜矿石','#b78560'),@('copper_ingot','铜锭','#cd946f'),
@('map','地图','#e6d7b2'),@('beacon_sigil','灯塔符印','#85dcdf'),@('charcoal','木炭','#44413c'))
foreach($m in $materials){$use='';$power=0;if($m[0] -in @('berry','cooked_berry','bread','carrot','cooked_carrot','potato','baked_potato','meat','cooked_meat')){$use='food';$power=12;if($m[0] -like '*cooked*' -or $m[0] -eq 'bread' -or $m[0] -eq 'baked_potato'){$power=30}};if($m[0] -like '*_seed'){$use='seed'};Item $m[0] $m[1] $m[2] $false $true 1 0 cube $use $power}
Recipe plank 木板 @{wood=1} plank 4
Recipe stick 木棍 @{plank=1} stick 4
for($t=0;$t -lt 3;$t++){for($i=0;$i -lt 4;$i++){$mat=@('stone','iron_ingot','crystal')[$t];$ingredients=@{stick=2};$ingredients[$mat]=3;$station='workbench';if($t -eq 0){$station='hand'};Recipe ($tiers[$t]+'_'+$tools[$i]) ($tierNames[$t]+$toolNames[$i]) $ingredients ($tiers[$t]+'_'+$tools[$i]) 1 $station}}
Recipe workbench 工作台 @{plank=4} workbench
Recipe campfire 营火 @{wood=3;stone=3} campfire
Recipe furnace 熔炉 @{stone=8} furnace 1 workbench
Recipe chest 木箱 @{plank=8} chest 1 workbench
Recipe bed 床 @{plank=3;cloth=3} bed 1 workbench
Recipe farmland 耕地 @{dirt=2;stick=1} farmland 2
Recipe bookshelf 书架 @{plank=4;cloth=2} bookshelf 1 workbench
Recipe cobble 圆石 @{stone=1} cobble
Recipe ruin_brick 遗迹石砖 @{stone=4} ruin_brick 4 workbench
Recipe sandstone 砂岩 @{sand=4} sandstone 4 workbench
Recipe stairs 木楼梯 @{plank=3} stairs 4 workbench
Recipe slab 木台阶 @{plank=3} slab 6
Recipe door 木门 @{plank=6;stick=2} door 1 workbench
Recipe window 窗框 @{plank=2;glass=2} window 2 workbench
Recipe fence 栅栏 @{plank=2;stick=2} fence 3
Recipe pillar 石柱 @{stone=2} pillar 2 workbench
Recipe roof 屋顶 @{stone=3} roof 4 workbench
Recipe tile 地砖 @{clay=2} tile 4 workbench
Recipe wool 羊毛块 @{cloth=4} wool 1 workbench
Recipe rug 地毯 @{cloth=2} rug 3
Recipe lantern 提灯 @{coal=1;iron_ingot=2;glass=1} lantern 1 workbench
Recipe marble 大理石 @{stone=4;crystal=1} marble 4 workbench
Recipe copper_block 铜块 @{copper_ingot=4} copper_block 1 workbench
Recipe polished_basalt 磨制玄武岩 @{basalt=2} polished_basalt 2 workbench
Recipe brick 红砖 @{clay=2;coal=1} brick 4 furnace 8
Recipe glass 玻璃 @{sand=2;coal=1} glass 2 furnace 6
Recipe iron_ingot 铁锭 @{raw_iron=2;coal=1} iron_ingot 2 furnace 8
Recipe copper_ingot 铜锭 @{copper_ore=2;coal=1} copper_ingot 2 furnace 8
Recipe charcoal 木炭 @{wood=2} charcoal 2 furnace 6
Recipe fiber 植物纤维 @{leaves=2} fiber 4
Recipe rope 绳索 @{fiber=3} rope 1
Recipe cloth 布料 @{fiber=4} cloth 1 workbench
Recipe bow 弓 @{stick=3;rope=2} bow 1 workbench
Recipe arrow 箭 @{stick=1;stone=1} arrow 8
Recipe shield 盾牌 @{plank=5;iron_ingot=1} shield 1 workbench
Recipe cooked_berry 莓果汤 @{berry=3;wood=1} cooked_berry 1 campfire 5
Recipe bread 面包 @{grain=3;wood=1} bread 1 campfire 6
Recipe cooked_carrot 炖胡萝卜 @{carrot=2;wood=1} cooked_carrot 1 campfire 5
Recipe baked_potato 烤马铃薯 @{potato=2;wood=1} baked_potato 1 campfire 5
Recipe cooked_meat 熟肉 @{meat=1;wood=1} cooked_meat 1 campfire 5
Recipe wheat_seed 小麦种子 @{grain=1} wheat_seed 2
Recipe carrot_seed 胡萝卜种子 @{carrot=1} carrot_seed 2
Recipe potato_seed 马铃薯种薯 @{potato=1} potato_seed 2
Recipe map 地图 @{cloth=2;coal=1} map 1 workbench
Recipe beacon_sigil 灯塔符印 @{crystal=3;iron_ingot=4} beacon_sigil 1 workbench
Recipe clay 黏土 @{dirt=2;sand=1} clay 3
if($items.Count -ne 90 -or $recipes.Count -ne 60){throw "Content budget mismatch: $($items.Count) items, $($recipes.Count) recipes"}
$items | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'data/items.json') -Encoding utf8
$recipes | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'data/recipes.json') -Encoding utf8
