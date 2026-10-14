"""Assemble offline 26-model gallery, provenance and source delivery archive."""
import argparse, importlib.util, json, pathlib, shutil, zipfile

ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/previews/block-creature-collection-2026-10-13'
parser=argparse.ArgumentParser();parser.add_argument('--package',action='store_true');args=parser.parse_args()
CATALOG=json.loads((ROOT/'data/creatures.json').read_text(encoding='utf-8-sig'))
NAMES={r['Id']:r['Name'] for r in CATALOG}
GROUPS={'land':['cow','pig','deer','boar','sheep','chicken','horse','wolf'],
 'small_aquatic':['cat','fox','armadillo','cod','squid','turtle'],
 'enemies':['skitter','husk','sentinel','zombie','skeleton','creeper','spider'],
 'bosses':['mine_warden','furnace_warden','observatory_warden','guardian','colossus']}
LABELS={'land':'陆地动物','small_aquatic':'小型与水生','enemies':'普通怪物','bosses':'精英与首领'}
DESCRIPTIONS={
 'cow':'正面眼睛与鼻孔、弯折牛角、连贯花纹、蹄部和乳房。',
 'pig':'大头比例、翻折耳朵、分层口鼻、卷曲尾巴和分蹄。',
 'deer':'分支鹿角、白斑与浅色腹部、直立耳朵和细长四肢。',
 'boar':'强壮肩背、鬃毛脊、两段獠牙、方形口鼻和冲锋蓄力。',
 'sheep':'分层羊毛块、露出的棕色面部、鼻头、耳朵和低头动作。',
 'chicken':'双足和前后趾、层叠羽片、红色鸡冠与肉垂、双层鸡喙。',
 'horse':'抬起的长颈、长口鼻、额前白纹、深色鬃毛与尾巴。',
 'wolf':'灰白毛色、浅色颌部与胸毛、独立下颌、厚尾和咬击动作。',
 'cat':'三花斑块、绿眼、立耳、几何胡须和抬起的分段尾巴。',
 'fox':'橙色身体、白色口鼻、黑色足部、宽大白尖尾巴。',
 'armadillo':'六道甲壳环带、长鼻、立耳、短爪、渐细尾巴和蜷缩蓄势。',
 'cod':'浅色腹部、侧眼与鳃、胸鳍、背鳍、双叶尾鳍与摆尾游动。',
 'squid':'分层外套膜、侧鳍、八条分节触腕、吸盘排和脉动游动。',
 'turtle':'九块背甲、浅色甲缘与腹甲、四片鳍状肢和头部。',
 'skitter':'八条分节腿、苔绿甲壳、五枚绿眼、成对口器和咬击。',
 'husk':'破损兜帽与披风、浅色脸、冷色眼睛、绷带手臂和分节手指。',
 'sentinel':'铜绿装甲、古铜边框、单眼光学装置和前臂内置能量炮。',
 'zombie':'绿色头部与手臂、破损青色衬衣、紫色裤子和近战出手。',
 'skeleton':'独立肋骨和脊柱、眼窝与牙齿、分离骨骼、弓与弓弦。',
 'creeper':'无手臂的四足结构、斑驳绿纹、方形面部和爆炸前颤动。',
 'spider':'八条分节腿、低矮黑色身体、红色复眼和成对口器。',
 'mine_warden':'矿工头盔与灯、不对称肩甲、黄铜护板、前臂发射器和活塞。',
 'furnace_warden':'宽体炉膛、实体竖向栅栏、四根烟囱、铜色热盾和发光通风口。',
 'observatory_warden':'狭长侦察轮廓、分层肩披、青色单眼、U 形天线和长发射器。',
 'guardian':'分层石甲、发光眼睛与内凹菱形核心、手指、膝盖和分节足部。',
 'colossus':'十一格尺度、双肩阶梯高塔、内凹十字熔心、分层活塞前臂和巨手。',
}

OUT.mkdir(parents=True,exist_ok=True)
for ident in ['cow','pig','guardian']:
 source=ROOT/'docs/previews/block-creature-prototypes-v2-2026-10-13'/ident
 dest=OUT/ident
 if source.exists():shutil.copytree(source,dest,dirs_exist_ok=True)
 elif not (dest/(ident+'.glb')).exists():raise FileNotFoundError(f'Generate accepted prototype or restore collection source: {ident}')
 record=json.loads((dest/'model-record.json').read_text(encoding='utf-8'))
 record.update(name=NAMES[ident],family='biped' if ident=='guardian' else 'quadruped',
  group='bosses' if ident=='guardian' else 'land',previewClip='slam' if ident=='guardian' else 'walk',
  acceptedPrototype=True,emissionMask=ident=='guardian',scope='accepted revision-2 sample copied without mesh changes')
 (dest/'model-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')

records=[]
for group,ids in GROUPS.items():
 for ident in ids:
  folder=OUT/ident
  record=json.loads((folder/'model-record.json').read_text(encoding='utf-8'))
  assert record.get('family'), 'Incomplete model: '+ident
  if ident in {'cow','pig','guardian'}:reference=folder/(ident+'-concept.png')
  else:
   sub='lux3d-inputs' if ident in {'deer','boar','sheep','chicken','horse','wolf'} else 'lux3d-remaining-inputs'
   reference=ROOT/'docs/previews/creature-redesign-2026-10-13'/sub/(ident+'.png')
  reference_copy=folder/(ident+'-reference.png')
  if not reference.exists():reference=reference_copy
  if reference.resolve()!=reference_copy.resolve():shutil.copy2(reference,reference_copy)
  record.update(name=NAMES[ident],group=group,description=DESCRIPTIONS[ident],acceptedPrototype=ident in {'cow','pig','guardian'},
                referenceKind='已确认的方块造型预览' if ident in {'cow','pig','guardian'} else '先前物种参考稿；按已确认方块风格重制')
  records.append(record)
(OUT/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'gallery-data.js').write_text('window.CREATURE_DATA='+json.dumps(records,ensure_ascii=False)+';\n',encoding='utf-8')
shutil.copy2(ROOT/'tools/block-creature-gallery.css',OUT/'gallery.css')
shutil.copy2(ROOT/'tools/block-creature-gallery.js',OUT/'gallery.js')
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>方块生物 · 26 种细化模型</title><link rel="stylesheet" href="gallery.css">
<header><p class="eyebrow">方野 · 余烬灯塔 / 生物模型</p><h1>26 种生物，统一的方块造型</h1><p>23 个新模型与 3 个已确认小样。图片和动作均来自实际 Blender 模型，含 GLB、骨骼、基础动画、像素贴图与 Blender 源文件。</p><div class="toplinks"><a href="block-creatures-26.zip" download>下载完整模型包</a><a href="overview.html">全部模型总览</a><a href="validation.json">导出检查</a><a href="godot-validation.json">Godot 检查</a></div></header>
<main><div class="toolbar"><div class="filters" aria-label="模型分类"><button data-group="all" aria-pressed="true">全部 26</button><button data-group="land">陆地动物 8</button><button data-group="small_aquatic">小型与水生 6</button><button data-group="enemies">普通怪物 7</button><button data-group="bosses">精英与首领 5</button><button data-group="new">本次新增 23</button></div><label>查找生物<input id="search" type="search" placeholder="名称或 ID" autocomplete="off"></label></div><p id="count" aria-live="polite"></p><div id="grid" class="grid"></div><p id="empty" hidden>没有匹配的生物。</p>
<footer>当前交付用于造型与动作审阅，尚未替换正式游戏资源。地形、树、水面和灯光仅用于渲染。攻击动画已包含蓄力、出手和恢复；游戏内命中、移动接地与碰撞需要接入时验证。</footer></main>
<dialog id="detail"><button id="close" class="close" aria-label="关闭预览">×</button><div class="detailhead"><div><p id="category" class="eyebrow"></p><h2 id="name"></h2><p id="metrics"></p></div><div class="navigation"><button id="previous" aria-label="上一个模型">←</button><button id="next" aria-label="下一个模型">→</button></div></div><div id="pictures" class="pictures"><figure><img id="model" alt="实际模型渲染"><figcaption>实际模型 · Blender 渲染</figcaption></figure><figure id="referencewrap" hidden><img id="reference" alt="造型参考"><figcaption id="referencecaption"></figcaption></figure></div><div class="controls"><button data-view="three-quarter" aria-pressed="true">斜视</button><button data-view="front">正面</button><button data-view="side">侧面</button><button id="motion"></button><button id="compare">查看造型参考</button></div><div class="framecontrol" id="framecontrol" hidden><label>动作帧<input id="timeline" type="range" min="0" max="23" value="0"></label><span id="frame">1 / 24</span></div><p id="description"></p><p id="clips"></p><div class="downloads"><a id="glb" download>下载 GLB</a><a id="blend" download>Blender 源文件</a><a id="atlas" download>像素贴图</a><a id="emission" download hidden>发光遮罩</a></div></dialog>
<script src="gallery-data.js"></script><script src="gallery.js"></script></html>'''
(OUT/'index.html').write_text(html,encoding='utf-8')
cards=''.join(f'<article><img src="{r["id"]}/{r["id"]}-three-quarter.png" alt="{r["name"]}"><div><b>{r["name"]}</b><span>{r["triangles"]:,} 三角面</span></div></article>' for r in records)
overview='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>26 种模型 · 实际渲染总览</title><style>*{box-sizing:border-box}body{margin:0;padding:28px;background:#172521;color:#eff3e8;font:16px system-ui,"Microsoft YaHei"}h1{margin:0 0 8px;font-size:30px}p{color:#bdd0bd;margin:0 0 22px}.grid{display:grid;grid-template-columns:repeat(5,1fr);gap:12px}article{background:#2b3c31;border-radius:8px;overflow:hidden}img{display:block;width:100%;aspect-ratio:1;object-fit:contain}article div{padding:10px;display:flex;justify-content:space-between;align-items:center;gap:5px}span{font-size:12px;color:#e7c575}a{color:#e7c575}</style><h1>26 种生物 · 细化方块模型</h1><p>实际 Blender 模型渲染 / 23 个新模型 + 3 个已确认小样 / <a href="index.html">打开动作与细节预览</a></p><div class="grid">'''+cards+'</div></html>'
(OUT/'overview.html').write_text(overview,encoding='utf-8')
lines=['方块生物全集（2026-10-13）','',
 '按已确认牛、猪、守护者细化版风格继续制作。新增 23 个模型，连同 3 个已确认小样共 26 个。',
 '打开 index.html 离线查看模型、前/侧/斜视、逐帧动作、造型参考、实际三角面数。overview.html 为总览。',
 '每个目录含带骨骼和基础动画的 GLB、打包贴图的 .blend、像素贴图、渲染图片及 24 张动作帧。',
 '所有新几何与贴图由本地 Blender 脚本重新制作，未复用 Lux3D 生成网格，也未产生新的服务积分费用。',
 '单模型为一份网格、一份材质、一个蒙皮。每个顶点刚性绑定一根骨骼，分段关节不会拉长三角面。',
 'GLB 使用 Y-up，正面 -Z，模型最低点 Y=0；原始尺寸记录于 manifest.json。',
 '仅怪物的光学装置与能量部件有独立发光遮罩；自然动物没有发光材质。',
 'Blender NLA 轨道默认静音，取消对应轨道静音可编辑动画。地形等渲染环境没有导出到 GLB。',
 '当前是模型交付，尚未替换游戏资源；移动接地、攻击命中、碰撞和光照仍需接入游戏后验证。',
 '参考图保留以便审阅：先前高密度物种稿用于识别特征，最终方块画风以已确认的 3 个 v2 小样为准。','',
 '模型清单（三角面 / 动画）']
lines += [f'{r["name"]} ({r["id"]}): {r["triangles"]:,} / {", ".join(r["clips"])}' for r in records]
(OUT/'README.txt').write_text('\n'.join(lines),encoding='utf-8')
spec=importlib.util.spec_from_file_location('delivery_validation',ROOT/'tools/validate-block-creature-collection.py')
validation=importlib.util.module_from_spec(spec);spec.loader.exec_module(validation);validation.validate()
if args.package:
 archive=OUT/'block-creatures-26.zip'
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for path in sorted(OUT.rglob('*')):
   if path.is_file() and path.suffix not in {'.zip','.blend1'}:z.write(path,path.relative_to(OUT).as_posix())
  for name in ['build-block-creature-prototypes.py','build-block-creature-prototypes-v2.py','build-block-creature-collection.py']:
   z.write(ROOT/'tools'/name,'tools/'+name)
 with zipfile.ZipFile(archive) as z:assert z.testzip() is None
 print(json.dumps(dict(models=26,newModels=23,archiveBytes=archive.stat().st_size),ensure_ascii=False))
