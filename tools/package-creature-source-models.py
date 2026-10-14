"""Package verified source GLBs without touching game assets."""
import datetime, hashlib, json, pathlib, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/previews/blender-creature-renders-2026-10-13'
groups=[ROOT/'docs/previews/land-animal-models-2026-10-13',ROOT/'docs/previews/remaining-creature-models-2026-10-13']
records=[]
archive_path=OUT/'creature-source-models-26.zip'
with zipfile.ZipFile(archive_path,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=1) as archive:
 for group in groups:
  inspection=json.loads((group/'model-inspection.json').read_text(encoding='utf-8'))
  for item in inspection['models']:
   ident=item['id'];model=group/'models'/f'{ident}.glb';original=group/'originals'/f'{ident}-source.zip'
   assert hashlib.sha256(model.read_bytes()).hexdigest()==item['sha256']
   with zipfile.ZipFile(original) as source:assert source.testzip() is None
   archive.write(model,f'models/{ident}.glb');archive.write(original,f'originals/{ident}-source.zip')
   records.append({**item,'sourceDelivery':group.name,'modelPath':f'models/{ident}.glb','originalPath':f'originals/{ident}-source.zip'})
  for filename in ['lux3d-delivery.json','model-inspection.json']:
   archive.write(group/filename,f'inspection/{group.name}/{filename}')
 assert len(records)==26 and len({item['id'] for item in records})==26
 manifest={'models':26,'gameIntegrated':False,'staticModels':True,'records':records}
 (OUT/'source-model-library.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 archive.write(OUT/'source-model-library.json','source-model-library.json')
 archive.writestr('README.md','# 生物源模型库\n\n26个独立GLB，26份服务原始ZIP及原始材质通道贴图。文件按游戏生物ID命名；完整来源、任务ID、实测三角面、UV和SHA256见 source-model-library.json。\n\n模型未接入游戏，未制作骨骼动画。AI网格和材质与概念图存在差异；游戏接入之前仍需形体与材质修整、统一原点/朝向/尺寸、制作骨骼动画和碰撞及性能验证。\n\nBlender渲染图与工作文件另见 blender-renders-26.zip / blender-workfiles-26.zip。\n')
with zipfile.ZipFile(archive_path) as archive:
 assert archive.testzip() is None
 assert sum(name.startswith('models/') for name in archive.namelist())==26
 assert sum(name.startswith('originals/') for name in archive.namelist())==26
base=ROOT/'.tools/lux3d/remaining-creatures-20261013'
approval_path=base/'user-batch-approval.json';approval=json.loads(approval_path.read_text(encoding='utf-8'))
ledgers={profile:json.loads((base/profile/'approved-batch-ledger.json').read_text(encoding='utf-8')) for profile in ['remaining-creatures','land-animals']}
for item in approval['items']:
 entry=ledgers[item['profile']]['entries'][item['id']]
 assert entry['state']=='committed'
 item.update(state=entry['state'],taskId=entry['taskId'])
approval.update(status='completed',committedQuotedCredits=sum(item['estimatedCredits'] for item in approval['items']),reservedQuotedCredits=0,finalizedAt=datetime.datetime.now(datetime.timezone.utc).isoformat())
assert approval['committedQuotedCredits']==360
approval_path.write_text(json.dumps(approval,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'models':26,'archiveBytes':archive_path.stat().st_size,'committedQuotedCredits':360,'gameIntegrated':False}))
