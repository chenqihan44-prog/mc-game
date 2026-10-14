"""Publish local native game renders and an auditable installation record."""
import hashlib, html, json, pathlib, struct
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/previews/block-creature-game-2026-10-13'
profiles=json.loads((ROOT/'data/creature_models.json').read_text(encoding='utf-8'))
definitions=json.loads((ROOT/'data/creatures.json').read_text(encoding='utf-8-sig'))
checks=json.loads((ROOT/'test-results/creature-motion/validation.json').read_text(encoding='utf-8'))
assert checks['passed'] and checks['models']==26 and checks['renderedCapture']
assert all(c['pixelSampling'] and c['footError']<.001 for c in checks['checks'])
for profile in profiles:
 ident=profile['Id'];source=ROOT/profile['Source'];asset=ROOT/'assets/creatures'/(ident+'.glb')
 assert hashlib.sha256(source.read_bytes()).hexdigest()==profile['SourceSha256']==profile['Sha256']==hashlib.sha256(asset.read_bytes()).hexdigest()
 assert profile['Style']=='block-v2'
 data=asset.read_bytes();size,kind=struct.unpack_from('<II',data,12);doc=json.loads(data[20:20+size])
 assert len(doc['meshes'])==len(doc['skins'])==len(doc['materials'])==1
 assert (OUT/(ident+'-game.png')).read_bytes().startswith(b'\x89PNG\r\n\x1a\n')
for ident in ['horse','guardian']:assert len(list((OUT/(ident+'-motion')).glob('frame-*.png')))==48
assert (OUT/'world-cow-pig-guardian.png').exists()
by_id={p['Id']:p for p in profiles}
cards=[]
for item in definitions:
 ident=item['Id'];profile=by_id[ident]
 cards.append(f'<article><img loading="lazy" src="{ident}-game.png" alt="{html.escape(item["Name"])}实际游戏模型"><h2>{html.escape(item["Name"])}<small>{profile["Triangles"]:,} 三角面</small></h2></article>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>细化方块模型 · 游戏替换完成</title><style>*{box-sizing:border-box}body{margin:0;background:#17251e;color:#ecf1e5;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:30px}p{color:#becdb9}a{color:#e6c277}section{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin:26px 0}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}article{background:#293c2e;border-radius:9px;overflow:hidden;border:1px solid #4a6246}img{display:block;width:100%;height:auto}h2{font-size:18px;padding:0 14px}small{float:right;color:#e0ba66;font-size:12px}.hero{border-radius:12px;margin-top:18px}button{background:#9f7b38;color:white;border:0;border-radius:6px;font:inherit;padding:7px 14px;cursor:pointer;margin:0 14px 14px}@media(max-width:900px){.grid{grid-template-columns:1fr 1fr}}@media(max-width:550px){section,.grid{grid-template-columns:1fr}}</style><main><h1>26 种细化方块模型已替换进游戏</h1><p>以下为实际 Godot 游戏渲染，使用游戏内材质、体型和运行时骨骼动作。草地截图来自独立验证世界。</p><p><a href="../../../../builds/EmberLighthouse-block-creatures-20261013试玩包.zip">下载新版完整试玩包</a> · <a href="../block-creature-collection-2026-10-13/index.html">查看模型源文件与 Blender 渲染</a> · <a href="integration-record.json">替换与验证记录</a></p><img class="hero" src="world-cow-pig-guardian.png" alt="牛、猪、守护者在实际草地世界中的截图"><section><article><img id="horse-demo" src="horse-motion/frame-00.png" alt="马的行走奔跑动作"><h2>马 · 行走与奔跑</h2><button data-demo="horse">播放</button></article><article><img id="guardian-demo" src="guardian-motion/frame-00.png" alt="守护者的蓄力、出手与恢复"><h2>守护者 · 重击动作</h2><button data-demo="guardian">播放</button></article></section><p>模型按游戏中的实际尺寸展示，独立图片采用统一取景。动物使用平滑步态、头尾动作与脚底接地补偿，怪物继续使用预备、出手和恢复阶段。</p><div class="grid">'''+''.join(cards)+'''</div><p>本批 26 个 GLB 合计约 10.7 MB，单模型为一份网格和一份材质。像素贴图采用 Nearest 采样，能量部件保留发光遮罩。</p></main><script>for(const id of ['horse','guardian']){let n=0,playing=false;const img=document.getElementById(id+'-demo'),button=document.querySelector('[data-demo='+id+']');const frames=Array.from({length:48},(_,i)=>{const frame=new Image;frame.src=id+'-motion/frame-'+String(i).padStart(2,'0')+'.png';return frame;});button.onclick=()=>{playing=!playing;button.textContent=playing?'暂停':'播放';};setInterval(()=>{if(playing){n=(n+1)%48;if(frames[n].complete)img.src=frames[n].src;}},1000/24);}</script></html>'''
# Three directory levels separate this page from the workspace root.
page=page.replace('../../../../builds/','../../../builds/')
(OUT/'index.html').write_text(page,encoding='utf-8')
installation=json.loads((ROOT/'.tools/block-model-replacement-20261013/installation.json').read_text(encoding='utf-8'))
record=dict(gameIntegrated=True,models=26,style='approved block-v2',modelsCopiedExactly=True,
            runtimeChecks=checks,installation=installation,
            profiles=profiles,capture='Godot game viewport and isolated staged game world',
            captureCount=26+96+1,animationRoute='blended runtime skeletal poses with actual bound sole correction')
(OUT/'integration-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(gameIntegrated=True,models=26,captureImages=123)))
