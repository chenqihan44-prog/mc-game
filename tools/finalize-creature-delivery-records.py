"""Reconcile actual preview, render and generation evidence."""
import hashlib,json,pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
models=ROOT/'docs/previews/remaining-creature-models-2026-10-13'
renders=ROOT/'docs/previews/blender-creature-renders-2026-10-13'
parts=[]
for first in ['cat','skitter','spider']:
 part=json.loads((models/'inspection'/f'browser-verification-{first}.json').read_text(encoding='utf-8'))
 assert part['passed'] and not part['pageErrors'];parts.append(part)
observations=[item for part in parts for item in part['observations']]
assert len(observations)==18 and len({item['id'] for item in observations})==18
report={'browser':'Chrome headless, offline, SwiftShader','modelCount':18,'samePreviewPage':True,'verificationStrategy':'Three independent browser sessions, six models per session, to bound renderer memory','observations':observations,'pageErrors':[],'passed':True}
(models/'inspection/browser-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
render_manifest=json.loads((renders/'render-manifest.json').read_text(encoding='utf-8'))
assert render_manifest['models']==26 and render_manifest['images']==52
assert json.loads((renders/'inspection/gallery-verification.json').read_text(encoding='utf-8'))['passed']
assert json.loads((renders/'blend-file-verification.json').read_text(encoding='utf-8'))['passed']
script=ROOT/'tools/blender-render-creatures.py'
evidence={'executionRoute':'Local Blender Python, hidden background process','hostInvocation':{'batchToolChunk':'93a4d6','batchProcessId':26076,'blendVerificationToolChunk':'c183fb','blendVerificationProcessId':15932},'executable':'E:/steam/steamapps/common/Blender/blender.exe','script':str(script),'scriptSha256':hashlib.sha256(script.read_bytes()).hexdigest(),'batchStdout':str(ROOT/'.tools/lux3d/blender-runtime/batch.stdout.txt'),'batchStderr':str(ROOT/'.tools/lux3d/blender-runtime/batch.stderr.txt'),'recordedCompletionTimes':{item['id']:item['completedAt'] for item in render_manifest['records']},'resultManifest':'render-manifest.json','workfileVerification':'blend-file-verification.json','galleryVerification':'inspection/gallery-verification.json','visualInspection':{'reviewed':'All 26 main views via full gallery screenshot; enlarged elite, aquatic and common-enemy gallery sections; native cat and colossus side views','findings':['Subjects are visible and framed with studio lighting.','Generated meshes retain fragmented surfaces, some smoothed voxel edges, and texture differences from the concept sheets.','This is source-model and render delivery; rigging, animation and game-readiness acceptance are pending.']},'gameIntegrated':False}
(renders/'local-blender-execution.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
plan_path=ROOT/'docs/previews/creature-redesign-2026-10-13/lux3d-remaining-model-plan.json'
plan=json.loads(plan_path.read_text(encoding='utf-8'));plan['status']='delivered-source-models-and-blender-renders';plan['quotation']['generationTasksSubmitted']=True
plan['blenderRendering'].update(status='completed',models=26,images=52,workfiles=26,gallery='docs/previews/blender-creature-renders-2026-10-13/index.html')
for item in plan['items']:item['status']='delivered';item['blenderRenderStatus']='completed'
plan_path.write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
readme=models/'README.md';body=readme.read_text(encoding='utf-8')
addition='\n## Blender渲染\n\n包括原有8种生物在内，共26个模型已在Blender 5.2.2 LTS / Cycles中完成渲染，每个有主视角和侧视角，合计52张1536×1536 PNG。26份贴图打包的 .blend 文件已逐一实际打开检查。\n\n[查看全部Blender渲染](../blender-creature-renders-2026-10-13/index.html) · [26个源模型合集](../blender-creature-renders-2026-10-13/creature-source-models-26.zip) · [渲染图合集](../blender-creature-renders-2026-10-13/blender-renders-26.zip) · [Blender工作文件合集](../blender-creature-renders-2026-10-13/blender-workfiles-26.zip)\n\n18个模型在同一离线预览页中，分三个独立浏览器会话验证了加载、旋转和缩放。浏览器检查采用分组以控制内存；模型预览HTML约442MiB，首次打开可能较慢。全部文件仍未接入游戏。\n'
if '## Blender渲染' not in body:readme.write_text(body+addition,encoding='utf-8')
print(json.dumps({'models':26,'remainingModelPreviewVerified':18,'renderImages':52,'blendFilesVerified':26,'gameIntegrated':False}))
