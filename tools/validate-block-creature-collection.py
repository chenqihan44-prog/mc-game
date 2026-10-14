"""Check delivery GLBs as binaries, including skin, texture and animation data."""
import hashlib, json, math, pathlib, struct

ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/previews/block-creature-collection-2026-10-13'

def inspect_glb(path):
 raw=path.read_bytes();magic,version,size=struct.unpack_from('<III',raw)
 assert magic==0x46546C67 and version==2 and size==len(raw),str(path)
 length,kind=struct.unpack_from('<II',raw,12);assert kind==0x4E4F534A
 doc=json.loads(raw[20:20+length]);binary=raw[28+length:]
 def accessor(index):
  a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
  fmt={5121:'B',5123:'H',5125:'I',5126:'f'}[a['componentType']]
  count={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
  n=struct.calcsize(fmt)*count;offset=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',n)
  assert offset+(a['count']-1)*stride+n<=len(binary)
  return [struct.unpack_from('<'+fmt*count,binary,offset+i*stride) for i in range(a['count'])]
 assert len(doc['meshes'])==len(doc['skins'])==len(doc['materials'])==1
 triangles=0;verts=0;positions=[]
 for mesh in doc['meshes']:
  for primitive in mesh['primitives']:
   assert primitive.get('mode',4)==4
   indices=accessor(primitive['indices']);p=accessor(primitive['attributes']['POSITION']);positions+=p;verts+=len(p)
   assert len(indices)%3==0 and max(i[0] for i in indices)<len(p);triangles+=len(indices)//3
   assert all(all(math.isfinite(v) for v in row) for row in p)
   normals=accessor(primitive['attributes']['NORMAL']);assert all(.97<sum(v*v for v in n)<1.03 for n in normals)
   weights=accessor(primitive['attributes']['WEIGHTS_0']);joints=accessor(primitive['attributes']['JOINTS_0'])
   assert all(abs(sum(w)-1)<1e-5 and sum(v>1e-5 for v in w)==1 for w in weights)
   assert all(j<len(doc['skins'][0]['joints']) for row in joints for j in row)
   uv=accessor(primitive['attributes']['TEXCOORD_0']);assert all(all(0<=v<=1 for v in row) for row in uv)
 assert abs(min(p[1] for p in positions))<1e-4
 assert all(s['magFilter']==9728 and s['minFilter'] in {9728,9984} for s in doc['samplers'])
 textures=[]
 for image in doc['images']:
  view=doc['bufferViews'][image['bufferView']];offset=view.get('byteOffset',0);png=binary[offset:offset+view['byteLength']]
  assert png.startswith(b'\x89PNG\r\n\x1a\n')
  textures.append(list(struct.unpack_from('>II',png,16)))
 clips=[]
 for animation in doc['animations']:
  changed=False
  for sampler in animation['samplers']:
   times=accessor(sampler['input']);output=accessor(sampler['output'])
   assert len(times)>1 and all(a[0]<=b[0] for a,b in zip(times,times[1:]))
   assert all(all(math.isfinite(v) for v in row) for row in output)
   changed=changed or any(any(abs(a-b)>1e-5 for a,b in zip(output[0],row)) for row in output[1:])
  assert changed,animation['name'];clips.append(animation['name'])
 emission='emissiveTexture' in doc['materials'][0]
 return dict(triangles=triangles,exportedVertices=verts,height=max(p[1] for p in positions),bones=len(doc['skins'][0]['joints']),
             clips=clips,textures=textures,emissionMask=emission,sha256=hashlib.sha256(raw).hexdigest())

def validate():
 catalog=json.loads((ROOT/'data/creatures.json').read_text(encoding='utf-8-sig'));checks=[]
 for item in catalog:
  ident=item['Id'];folder=OUT/ident;record=json.loads((folder/'model-record.json').read_text(encoding='utf-8'))
  check=inspect_glb(folder/(ident+'.glb'));check['id']=ident
  assert check['triangles']==record['triangles'] and check['sha256']==record['sha256'],ident
  assert set(check['clips'])==set(record['clips']),ident
  assert record['textureSize']==check['textures'][0],ident
  if 'emissionMask' in record:assert check['emissionMask']==record['emissionMask'],ident
  if not item['Hostile'] and ident!='guardian':assert not check['emissionMask'],'Unexpected luminous animal: '+ident
  assert len(list((folder/'motion').glob('*.png')))==24,ident
  assert (folder/(ident+'.blend')).stat().st_size>10000
  assert all((folder/(ident+'-'+view+'.png')).exists() for view in ['three-quarter','side','front'])
  checks.append(check)
 report=dict(passed=True,models=len(checks),newModels=23,acceptedModels=3,checks=checks,
             scope='binary geometry, single material/skin, normalized rigid weights, nearest sampler, embedded PNGs, animation channels, rendered-frame completeness')
 (OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(dict(passed=True,models=len(checks),triangles={c['id']:c['triangles'] for c in checks}),ensure_ascii=False))
 return report

if __name__=='__main__':validate()
