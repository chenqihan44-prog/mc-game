"""Replace approved game GLBs, retaining exact sources and a reversible backup."""
import hashlib, importlib.util, json, pathlib, shutil

ROOT=pathlib.Path(__file__).resolve().parents[1]
SOURCE=ROOT/'docs/previews/block-creature-collection-2026-10-13'
WORK=ROOT/'.tools/block-model-replacement-20261013'
BACKUP=WORK/'previous';BACKUP.mkdir(parents=True,exist_ok=True)
profiles_path=ROOT/'data/creature_models.json'
previous=BACKUP/'creature_models.json'
if not previous.exists():shutil.copy2(profiles_path,previous)
old={p['Id']:p for p in json.loads(previous.read_text(encoding='utf-8'))}
records=json.loads((SOURCE/'manifest.json').read_text(encoding='utf-8'))
assert {p['id'] for p in records}==set(old) and len(records)==26
spec=importlib.util.spec_from_file_location('model_inspector',ROOT/'tools/validate-block-creature-collection.py')
inspector=importlib.util.module_from_spec(spec);spec.loader.exec_module(inspector)
profiles=[];changes=[]
for item in records:
 ident=item['id'];source=SOURCE/ident/(ident+'.glb');dest=ROOT/'assets/creatures'/(ident+'.glb')
 checked=inspector.inspect_glb(source);assert checked['sha256']==item['sha256'] and checked['triangles']==item['triangles']
 backup=BACKUP/(ident+'.glb')
 if not backup.exists():shutil.copy2(dest,backup)
 target=old[ident];height=target['Height'];source_height=checked['height'];scale=height/source_height
 assert .4<scale<1.4
 shutil.copy2(source,dest)
 profile=dict(Id=ident,Family=item['family'],Height=height,BodyHeight=target['BodyHeight'],LabelHeight=height+.25,
  Stride=target['Stride'],SourceHeight=source_height,Style='block-v2',Source=str(source.relative_to(ROOT)).replace('\\','/'),
  SourceSha256=checked['sha256'],Sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),
  Triangles=item['triangles'],Bones=item['bones'],Clips=item['clips'],TextureMaxDimension=item['textureSize'][0],
  EmissionMask=item['emissionMask'],AuthoredLocally=True)
 assert profile['Sha256']==profile['SourceSha256'];profiles.append(profile)
 changes.append(dict(id=ident,oldBytes=backup.stat().st_size,newBytes=dest.stat().st_size,sourceHeight=source_height,
                     gameplayHeight=height,runtimeScale=scale,triangles=item['triangles'],sha256=profile['Sha256']))
profiles_path.write_text(json.dumps(profiles,ensure_ascii=False,indent=2),encoding='utf-8')
report=dict(models=26,oldAssetBytes=sum(r['oldBytes'] for r in changes),newAssetBytes=sum(r['newBytes'] for r in changes),
            backup=str(BACKUP),originalPreviewsPreserved=True,approvedGlbsCopiedExactly=True,changes=changes)
(WORK/'installation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='changes'},ensure_ascii=False))
