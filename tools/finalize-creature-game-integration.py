"""Package real game captures and source bindings after runtime verification."""
import hashlib,html,json,pathlib,struct
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/previews/creature-game-integration-2026-10-13'
profiles=json.loads((ROOT/'data/creature_models.json').read_text(encoding='utf-8'))
definitions=json.loads((ROOT/'data/creatures.json').read_text(encoding='utf-8-sig'))
checks=json.loads((ROOT/'test-results/creature-motion/validation.json').read_text(encoding='utf-8'))
assert checks['passed'] and checks['models']==26 and checks['renderedCapture']
assert {p['Id'] for p in profiles}=={d['Id'] for d in definitions}
for profile in profiles:
 ident=profile['Id'];source=ROOT/profile['Source'];model=ROOT/'assets/creatures'/f'{ident}.glb'
 assert hashlib.sha256(source.read_bytes()).hexdigest()==profile['SourceSha256']
 data=model.read_bytes();assert hashlib.sha256(data).hexdigest()==profile['Sha256']
 assert data[:4]==b'glTF';size,kind=struct.unpack_from('<II',data,12);document=json.loads(data[20:20+size])
 assert document['skins'] and len(document['skins'][0]['joints'])>=6
 assert all('JOINTS_0' in primitive['attributes'] and 'WEIGHTS_0' in primitive['attributes'] for mesh in document['meshes'] for primitive in mesh['primitives'])
 assert (OUT/f'{ident}-game.png').read_bytes()[:8]==b'\x89PNG\r\n\x1a\n'
for ident in ['horse','guardian']:
 assert len(list((OUT/f'{ident}-motion').glob('frame-*.png')))==48
 for frame in (OUT/f'{ident}-motion').glob('frame-*.png'):assert frame.read_bytes()[:8]==b'\x89PNG\r\n\x1a\n'
cards=[]
for definition in definitions:
 ident=definition['Id'];name=html.escape(definition['Name'])
 cards.append(f'<article><img loading="lazy" src="{ident}-game.png" alt="{name}游戏骨骼动作"><h2>{name}<small>{ident}</small></h2></article>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>生物模型与动作 · 游戏接入</title><style>*{box-sizing:border-box}body{background:#141922;color:#eee;font:16px system-ui,"Microsoft YaHei",sans-serif;margin:0}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:30px}p{line-height:1.7;color:#bdc8d8}section{display:grid;grid-template-columns:repeat(2,1fr);gap:20px;margin:25px 0}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:18px}article{border:1px solid #3a485c;border-radius:12px;background:#242c39;overflow:hidden}img{display:block;width:100%;height:auto}h2{font-size:18px;padding:0 16px}small{float:right;font-size:11px;color:#aabace}button{background:#b3925c;color:#111;border:0;border-radius:6px;padding:9px 15px;font:inherit;cursor:pointer;margin:0 16px 16px}a{color:#dfb979}@media(max-width:900px){.grid{grid-template-columns:repeat(2,1fr)}}@media(max-width:550px){section,.grid{grid-template-columns:1fr}}</style><main><h1>26种生物已接入游戏</h1><p>以下图片由实际游戏的骨骼模型在Godot中渲染。马的行走／奔跑和守护者的预备／出手／收招示例，可播放和暂停。</p><section><article><img id="horse-demo" src="horse-motion/frame-00.png" alt="马的骨骼步态"><h2>马 · 连续步态</h2><button data-demo="horse">暂停</button></article><article><img id="guardian-demo" src="guardian-motion/frame-00.png" alt="守护者重击动作"><h2>守护者 · 范围重击动作</h2><button data-demo="guardian">暂停</button></article></section><p>独立展示采用统一取景比例。实际游戏保留不同体型、移动速度、碰撞、预警与伤害判定。以下为行走／游动中的模型帧。</p><div class="grid">'''+''.join(cards)+'''</div><p>模型经本地骨骼绑定；动物使用平滑步态、头尾待机与转向动作，铠甲关节采用分段绑定。单图AI重建留下的形体、贴图及部分关节接缝仍可继续精修。</p><p><a href="integration-record.json">接入来源与实际验证记录</a></p></main><script>for(const id of ['horse','guardian']){let n=0,playing=true;const img=document.getElementById(id+'-demo');const frames=Array.from({length:48},(_,i)=>{const image=new Image;image.src=id+'-motion/frame-'+String(i).padStart(2,'0')+'.png';return image;});document.querySelector('[data-demo='+id+']').onclick=e=>{playing=!playing;e.target.textContent=playing?'暂停':'播放';};setInterval(()=>{if(playing){n=(n+1)%48;if(frames[n].complete)img.src=frames[n].src;}},1000/24);}</script></html>'''
OUT.mkdir(parents=True,exist_ok=True);(OUT/'index.html').write_text(page,encoding='utf-8')
record={'sourceModels':26,'boundGameModels':26,'runtimeVerification':checks,'models':profiles,'sourceDeliveriesPreserved':True,'gameIntegrated':True,'animationRoute':'Godot procedural skeletal poses, blended with locomotion and persisted combat phases','blenderInvocation':{'initialRiggingProcess':18924,'initialToolChunk':'ff37ef','armorRefinementProcess':19116,'armorRefinementToolChunk':'1f865c','finalArmorRefinementProcess':22812,'finalArmorRefinementToolChunk':'285da9'},'renderCaptureInvocation':{'processId':44544,'toolChunk':'54c8fe','stdout':'.tools/creature-rigging/motion-accepted.stdout.log','stderr':'.tools/creature-rigging/motion-accepted.stderr.log'},'renderedCaptureFiles':{'catalog':'all-26-game-models.png','horseFrames':48,'guardianFrames':48}}
(OUT/'integration-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'gameIntegrated':True,'models':26,'capturedFrames':96,'originalsUnmodified':True}))
