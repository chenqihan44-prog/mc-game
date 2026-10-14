"""Verify exported GLB structure and package the three local prototypes."""
import hashlib
import json
import pathlib
import shutil
import struct
import zipfile
import argparse

ROOT=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--revision',type=int,choices=[1,2],default=1);args=parser.parse_args()
OUT=ROOT/('docs/previews/block-creature-prototypes-v2-2026-10-13' if args.revision==2 else 'docs/previews/block-creature-prototypes-2026-10-13')
names={'cow':'牛','pig':'猪','guardian':'守护者'}
concepts={'cow':'exec-ff5ebf2f-25f5-4a18-a709-c70783d2abb7.png',
          'pig':'exec-d2b2e5cd-fb04-45f1-aa10-106a7cb598af.png',
          'guardian':'exec-528a21cd-9b8d-4946-bdc1-8ca150ee1c02.png'}
checks=[]
cards=[]
for ident,name in names.items():
    folder=OUT/ident
    record=json.loads((folder/'model-record.json').read_text(encoding='utf-8'))
    raw=(folder/(ident+'.glb')).read_bytes()
    magic,version,length=struct.unpack_from('<III',raw)
    assert magic==0x46546c67 and version==2 and length==len(raw)
    jlen,jtype=struct.unpack_from('<II',raw,12)
    assert jtype==0x4e4f534a
    doc=json.loads(raw[20:20+jlen])
    binary=raw[28+jlen:]
    def accessor(index):
        item=doc['accessors'][index];view=doc['bufferViews'][item['bufferView']]
        fmt={5121:'B',5123:'H',5125:'I',5126:'f'}[item['componentType']]
        components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[item['type']]
        size=struct.calcsize(fmt)*components
        offset=view.get('byteOffset',0)+item.get('byteOffset',0)
        stride=view.get('byteStride',size)
        return [struct.unpack_from('<'+fmt*components,binary,offset+n*stride) for n in range(item['count'])]
    triangles=0
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            assert primitive.get('mode',4)==4
            triangles+=doc['accessors'][primitive['indices']]['count']//3
            weights=accessor(primitive['attributes']['WEIGHTS_0'])
            assert all(abs(sum(w)-1)<1e-5 and sum(v>1e-5 for v in w)==1 for w in weights)
            positions=accessor(primitive['attributes']['POSITION'])
            assert abs(min(p[1] for p in positions))<.001, 'Feet must start at Godot Y=0'
    assert triangles==record['triangles']
    assert len(doc['meshes'])==1 and len(doc['skins'])==1 and len(doc['materials'])==1
    clips=[a['name'] for a in doc['animations']]
    assert set(clips)==set(record['clips'])
    assert all(s['magFilter']==9728 and s['minFilter'] in {9728,9984} for s in doc['samplers'])
    changed=[]
    for animation in doc['animations']:
        outputs=[accessor(s['output']) for s in animation['samplers']]
        assert any(any(any(abs(a-b)>1e-5 for a,b in zip(o[0],row)) for row in o[1:]) for o in outputs)
        changed.append(animation['name'])
    assert len(list((folder/'motion').glob('*.png')))==24
    checks.append(dict(id=ident,triangles=triangles,clips=clips,animationDataChanges=changed,
                       rigidWeights=True,nearestTextureSampling=True,glbSha256=hashlib.sha256(raw).hexdigest()))
    source=pathlib.Path('C:/Users/lenovo/.codex/generated_images/01a13309-cb13-7623-a3f1-f42520e0c2a1')/concepts[ident]
    shutil.copy2(source,folder/(ident+'-concept.png'))
    cards.append(f'''<article data-id="{ident}">
      <div class="art"><img class="model" src="{ident}/{ident}-three-quarter.png" alt="{name}实际模型渲染"><img class="concept" src="{ident}/{ident}-concept.png" alt="{name}概念图" hidden></div>
      <h2>{name}<span>{triangles} 三角面</span></h2>
      <p>{record['blocks']} 个方块 · {len(record['bones'])} 根骨骼 · {record['textureSize'][0]}×{record['textureSize'][1]} 像素贴图 · {round(record['byteSize']/1024)} KB GLB</p>
      <div class="controls"><button data-view="three-quarter">斜视</button><button data-view="front">正面</button><button data-view="side">侧面</button><button data-motion>播放{'砸击' if ident=='guardian' else '行走'}</button><button data-concept>对照概念图</button></div>
      <input class="timeline" type="range" min="0" max="23" value="0" aria-label="{name}动作帧" hidden>
      <p class="download"><a href="{ident}/{ident}.glb" download>下载 GLB</a><a href="{ident}/{ident}.blend" download>Blender 源文件</a><a href="{ident}/{ident}-atlas.png" download>像素贴图</a></p>
    </article>''')
    if args.revision==2:
        cards[-1]=cards[-1].replace(f'<div class="art"><img class="model" src="{ident}/{ident}-three-quarter.png" alt="{name}实际模型渲染"><img class="concept" src="{ident}/{ident}-concept.png" alt="{name}概念图" hidden></div>',f'<div class="art"><figure><figcaption>原预览图</figcaption><img class="concept" src="{ident}/{ident}-concept.png" alt="{name}概念图"></figure><figure><figcaption>新版实际模型 · Blender 渲染</figcaption><img class="model" src="{ident}/{ident}-three-quarter.png" alt="{name}实际模型渲染"></figure></div>')
        cards[-1]=cards[-1].replace('对照概念图','显示旧小样')
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>方块生物 · 三个模型小样</title>
<style>*{box-sizing:border-box}body{margin:0;background:#172521;color:#eef1e8;font:16px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1450px;margin:auto;padding:36px 24px}h1{font-size:30px;margin:0 0 8px}.intro{color:#b5c6b8;max-width:920px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px;margin-top:28px}article{background:#25362e;border-radius:12px;overflow:hidden;padding:0 16px 12px}.art{aspect-ratio:1;margin:0 -16px;position:relative;background:#6c9358}.art img{width:100%;height:100%;object-fit:contain}.art img[hidden]{display:none}h2{display:flex;justify-content:space-between;align-items:center;margin:16px 0 6px}h2 span{font-size:16px;color:#e9c36b}p{font-size:14px;color:#b5c6b8}.controls{display:flex;flex-wrap:wrap;gap:7px}button{background:#3b5241;color:#fff;border:1px solid #668068;border-radius:6px;padding:8px;cursor:pointer}button[aria-pressed=true]{background:#9c6d22}.timeline{width:100%;margin-top:14px}.download{display:flex;flex-wrap:wrap;gap:14px}a{color:#efd189}footer{color:#b5c6b8;margin-top:24px}@media(max-width:950px){.grid{grid-template-columns:1fr}main{max-width:650px}}</style>
<main><h1>方块生物 · 三个模型小样</h1><p class="intro">按已确认概念图制作的实际网格，采用少量规整方块、像素贴图与分段骨骼。下方图片和动作逐帧预览均由实际 Blender 模型渲染。尚未替换游戏资源。</p><div class="grid">'''+''.join(cards)+'''</div>
<footer>牛、猪包含 idle / walk / run / graze；守护者包含 idle / walk / run / slam。动作预览是 Blender 渲染，GLB 内含骨骼与动画；地形仅作画风参照，未导出到模型。<br><a href="block-creature-prototypes.zip" download>下载三个小样完整包</a> · <a href="validation.json">导出检查记录</a></footer></main>
<script>
document.querySelectorAll('article').forEach(card=>{
 const id=card.dataset.id,model=card.querySelector('.model'),concept=card.querySelector('.concept'),slider=card.querySelector('.timeline'),motion=card.querySelector('[data-motion]');
 const frames=Array.from({length:24},(_,n)=>{const img=new Image();img.src=`${id}/motion/${String(n).padStart(2,'0')}.png`;return img;});
 const paired=Boolean(card.querySelector('figure'));let frame=0,playing=false,timer;
 function show(n){frame=n;model.src=frames[n].src;slider.value=n;}
 function stop(){playing=false;clearInterval(timer);motion.textContent=id==='guardian'?'播放砸击':'播放行走';motion.setAttribute('aria-pressed','false');}
 card.querySelectorAll('[data-view]').forEach(button=>button.onclick=()=>{stop();concept.hidden=paired?false:true;model.hidden=false;slider.hidden=true;model.src=`${id}/${id}-${button.dataset.view}.png`;});
 motion.onclick=()=>{if(playing){stop();return;}concept.hidden=paired?false:true;model.hidden=false;slider.hidden=false;playing=true;motion.textContent='暂停动作';motion.setAttribute('aria-pressed','true');show(frame);timer=setInterval(()=>show((frame+1)%24),id==='guardian'?57:42);};
 slider.oninput=()=>{stop();show(Number(slider.value));};
 card.querySelector('[data-concept]').onclick=()=>{stop();if(paired){const old=concept.getAttribute('src').includes('v1');concept.src=`${id}/${id}-${old?'concept':'v1'}.png`;card.querySelector('figcaption').textContent=old?'原预览图':'被退回的旧小样';card.querySelector('[data-concept]').textContent=old?'显示旧小样':'显示原预览';return;}concept.hidden=!concept.hidden;model.hidden=!concept.hidden;slider.hidden=true;};
});</script></html>'''
if args.revision==2:
    html=html.replace('方块生物 · 三个模型小样','方块生物 · 细化版模型对照')
    html=html.replace('按已确认概念图制作的实际网格，采用少量规整方块、像素贴图与分段骨骼。下方图片和动作逐帧预览均由实际 Blender 模型渲染。尚未替换游戏资源。','根据原预览图重做比例、面部、耳朵、牛角、花纹与装甲。每组左侧为原预览图，右侧为新版实际模型；可切换到旧小样比较修改。实际模型已含骨骼与基础动画，尚未替换游戏资源。')
    html=html.replace('</style>','figure{margin:0;min-width:0}figcaption{text-align:center;padding:10px;background:#304237;font-size:14px}.grid{grid-template-columns:1fr;gap:24px}.art{display:grid;grid-template-columns:1fr 1fr;aspect-ratio:auto;background:#31422f}.art img{display:block;height:390px;width:100%;object-fit:contain;background:#24382e}.art img[hidden]{display:none}main{max-width:1120px}@media(max-width:650px){.art{grid-template-columns:1fr}.art img{height:auto;aspect-ratio:1;object-fit:contain}}'+'</style>')
    for ident in names:
        shutil.copy2(ROOT/f'docs/previews/block-creature-prototypes-2026-10-13/{ident}/{ident}-three-quarter.png',OUT/ident/(ident+'-v1.png'))
(OUT/'index.html').write_text(html,encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps(dict(passed=True,checks=checks,scope='GLB binary structure, triangle count, rigid skin weights, animated outputs, pixel sampler, rendered frames'),ensure_ascii=False,indent=2),encoding='utf-8')
readme='''三个方块模型小样（2026-10-13）

打开 index.html 查看实际渲染、角度和动作。支持离线浏览。
每个目录含 GLB（骨骼和四个动画）、打包贴图的 Blender 源文件、PNG 像素贴图、参考概念图、实际渲染和动作帧。
全部为本地按方块结构重新制作，没有使用旧生成网格减面，没有额外 Lux3D 积分费用。
GLB 内模型脚底位于 Y=0，正面朝 -Z；贴图使用 Nearest，材质为哑光。没有替换正式游戏资产。
Blender 源文件内预览地形和灯光未包含在 GLB 中。动画位于 CreatureRig 的 NLA tracks，可取消对应轨道静音进行编辑。
各顶点仅绑定一根骨骼，方块不会在关节处变形；关节转动时可能露出分段接口，这是方块模型结构的一部分。
图像为 Blender 渲染；最终的光照、尺寸、脚底接地和攻击命中时间仍需在正式游戏场景验证。
'''
(OUT/'README.txt').write_text(readme,encoding='utf-8')
if args.revision==2:
    readme='''三个细化模型小样 v2（2026-10-13）

打开 index.html 并排查看原概念图、新版模型实际渲染；也可切换到被退回的旧小样，切换角度、播放和拖动动作帧。
新版实测三角面：牛 2,372；猪 1,832；守护者 5,748。
这是按二维参考重新搭建的模型，已修改比例、面部、牛角、耳朵、蹄部、花纹、装甲与核心，视觉还原程度请通过并排图判断。
每个目录含 GLB（骨骼和四个动画）、打包贴图的 Blender 源文件、PNG 像素贴图、参考概念图、实际渲染和动作帧。
守护者另有独立发光遮罩贴图，发光仅用于眼睛和胸口核心。材质仍为一份。
全部在本地 Blender 重新制作，未花费新的 Lux3D 积分；旧版与正式游戏资产均保留。
GLB 脚底位于 Y=0，正面朝 -Z；贴图使用 Nearest。预览地形、树、灯光不包含在 GLB 内。
动画位于 CreatureRig 的 NLA tracks，取消对应轨道静音即可编辑。
各顶点刚性绑定一根骨骼；关节转动时存在分段接口。
导出检查与 Godot 隔离项目的导入/播放检查记录在目录中。
图片与动作帧是 Blender 渲染，正式游戏中的光照、尺寸、接地和攻击命中时间尚待接入后验证。
'''
    (OUT/'README.txt').write_text(readme,encoding='utf-8')
with zipfile.ZipFile(OUT/'block-creature-prototypes.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for path in sorted(OUT.rglob('*')):
        if path.is_file() and path.suffix not in {'.zip','.blend1'} and '.godot' not in path.parts:
            z.write(path,path.relative_to(OUT).as_posix())
    z.write(ROOT/'tools/build-block-creature-prototypes.py','source/build-block-creature-prototypes.py')
    if args.revision==2:z.write(ROOT/'tools/build-block-creature-prototypes-v2.py','source/build-block-creature-prototypes-v2.py')
print(json.dumps({'passed':True,'checks':checks,'archiveBytes':(OUT/'block-creature-prototypes.zip').stat().st_size},ensure_ascii=False))
