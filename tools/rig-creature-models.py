"""Bind local Lux3D meshes for Godot. Original source deliveries stay intact."""
import argparse, datetime, hashlib, json, math, pathlib, sys
import bpy
from mathutils import Vector
ROOT=pathlib.Path(r'F:\我的世界')
OUT=ROOT/'assets/creatures';OUT.mkdir(parents=True,exist_ok=True)
WORK=ROOT/'.tools/creature-rigging';WORK.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('--ids');parser.add_argument('--force',action='store_true');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
heights={'deer':(1.95,1.55),'boar':(1.10,1.03),'cow':(1.45,1.35),'pig':(.95,.9),'sheep':(1.25,1.15),'chicken':(.92,.92),'horse':(1.95,1.9),'wolf':(.95,.92),'cat':(.78,.72),'fox':(.92,.82),'armadillo':(.55,.5),'turtle':(.55,.55),'cod':(.45,.45),'squid':(.95,.95),'spider':(.65,.65),'skitter':(.65,.65),'husk':(1.83,1.83),'zombie':(1.8,1.8),'skeleton':(1.8,1.8),'creeper':(1.7,1.7),'sentinel':(1.8,1.8),'guardian':(2.7,2.7),'mine_warden':(2.2,2.2),'furnace_warden':(2.9,2.9),'observatory_warden':(2.78,2.35),'colossus':(10.9,10.9)}
humans={'husk','zombie','skeleton','sentinel','guardian','mine_warden','furnace_warden','observatory_warden','colossus'}
old={'deer','boar','cow','pig','sheep','chicken','horse','wolf'}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def rig(ident):
 source=ROOT/'docs/previews'/('land-animal-models-2026-10-13' if ident in old else 'remaining-creature-models-2026-10-13')/'models'/f'{ident}.glb'
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
 mesh=meshes[0];mesh.data.transform(mesh.matrix_world);mesh.parent=None;mesh.matrix_world.identity()
 # Blender source faces -Y; Godot gameplay faces -Z. Bake a half turn before Y-up export.
 from mathutils import Matrix
 mesh.data.transform(Matrix.Rotation(math.pi,4,'Z'))
 points=[v.co for v in mesh.data.vertices];lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)))
 dims=hi-lo;height,bodyheight=heights[ident];scale=height/dims.z
 # Aquatic species keep their original proportions; their dimensions fit existing spawn clearances.
 if ident=='cod':scale=min(scale,1.25/dims.y)
 if ident=='squid':scale=min(scale,1.5/max(dims.x,dims.y))
 center=(lo+hi)*.5
 for v in mesh.data.vertices:v.co=(v.co-Vector((center.x,center.y,lo.z)))*scale
 mesh.data.update();w,l,h=dims*scale
 family='biped' if ident in humans else 'arthropod' if ident in {'spider','skitter'} else 'fish' if ident=='cod' else 'squid' if ident=='squid' else 'bird' if ident=='chicken' else 'quadruped'
 legtop={'deer':.46,'horse':.48,'cow':.39,'sheep':.30,'pig':.30,'armadillo':.24,'turtle':.28,'creeper':.27}.get(ident,.38)
 armature=bpy.data.armatures.new(ident+'_skeleton');rigobj=bpy.data.objects.new('CreatureRig',armature);bpy.context.collection.objects.link(rigobj)
 bpy.context.view_layer.objects.active=rigobj;rigobj.select_set(True);mesh.select_set(False);bpy.ops.object.mode_set(mode='EDIT')
 bones={}
 def bone(name,position,parent='Body'):
  b=armature.edit_bones.new(name);b.head=position;b.tail=Vector(position)+Vector((0,0,max(h*.07,.025)));b.roll=0
  if parent:b.parent=bones[parent]
  bones[name]=b
 bone('Root',(0,0,0),None);bone('Body',(0,0,h*.50),'Root')
 bone('Head',(0,l*.20,h*(.76 if family=='biped' else .57)))
 bone('Tail',(0,-l*.30,h*.46))
 if family=='biped':
  for s in [-1,1]:
   suffix='L' if s<0 else 'R'
   bone('Leg'+suffix,(s*w*.13,0,h*.40));bone('Knee'+suffix,(s*w*.13,0,h*.20),'Leg'+suffix)
   bone('Arm'+suffix,(s*w*.29,0,h*.69));bone('Hand'+suffix,(s*w*.31,l*.03,h*.43),'Arm'+suffix)
 elif family in {'quadruped','bird'}:
  for s in [-1,1]:
   for front in ([True,False] if family=='quadruped' else [True]):
    suffix=('Front' if front else 'Rear')+('L' if s<0 else 'R');y=l*(.23 if front else -.24)
    bone('Leg'+suffix,(s*w*.27,y,h*legtop));bone('Knee'+suffix,(s*w*.27,y,h*legtop*.45),'Leg'+suffix)
  if family=='bird':
   for s in [-1,1]:bone('Wing'+('L' if s<0 else 'R'),(s*w*.25,0,h*.48))
 elif family=='arthropod':
  for s in [-1,1]:
   for n in range(4):
    name=f'Leg{n}'+('L' if s<0 else 'R');bone(name,(s*w*.20,l*(n/3-.5)*.64,h*.48));bone('Tip'+name[3:],(s*w*.34,l*(n/3-.5)*.75,h*.24),name)
 elif family=='squid':
  for n in range(8):
   a=n*math.tau/8;bone('Tentacle'+str(n),(math.cos(a)*w*.17,math.sin(a)*l*.17,h*.43))
 else:
  for s in [-1,1]:bone('Fin'+('L' if s<0 else 'R'),(s*w*.30,0,h*.45))
 bpy.ops.object.mode_set(mode='OBJECT')
 groups={name:mesh.vertex_groups.new(name=name) for name in bones}
 usage={name:0 for name in bones}
 for v in mesh.data.vertices:
  x,y,z=v.co;nx=x/w;ny=y/l+.5;nz=z/h;suffix='L' if x<0 else 'R';weights={'Body':1.0}
  def use(name,amount):
   amount=max(0,min(1,amount));weights['Body']*=1-amount;weights[name]=weights.get(name,0)+amount
  if family=='biped':
   if abs(nx)>.24 and .18<nz<.79:
    amount=smooth(.22,.27,abs(nx))*(1-smooth(.70,.81,nz));hand=1-smooth(.38,.49,nz)
    use('Arm'+suffix,amount*(1-hand));use('Hand'+suffix,amount*hand)
   elif nz<.43:
    amount=1-smooth(.35,.44,nz);knee=1-smooth(.16,.25,nz)
    use('Leg'+suffix,amount*(1-knee));use('Knee'+suffix,amount*knee)
   else:use('Head',smooth(.75,.83,nz)*(1-smooth(.24,.34,abs(nx))))
  elif family in {'quadruped','bird'}:
   if nz<legtop+.04:
    front=ny>.5 or family=='bird';name=('Front' if front else 'Rear')+suffix
    amount=1-smooth(legtop-.06,legtop+.04,nz)
    if ident=='turtle':amount*=smooth(.20,.34,abs(nx))
    knee=1-smooth(legtop*.37,legtop*.61,nz);use('Leg'+name,amount*(1-knee));use('Knee'+name,amount*knee)
   elif family=='bird' and abs(nx)>.28 and nz<.65:use('Wing'+suffix,smooth(.25,.4,abs(nx)))
   elif ident=='creeper':use('Head',smooth(.65,.73,nz))
   else:
    use('Head',smooth(.58,.73,ny)*smooth(legtop+.02,legtop+.12,nz))
    use('Tail',(1-smooth(.08,.19,ny))*(1-smooth(.12,.25,abs(nx))))
  elif family=='arthropod':
   amount=smooth(.19,.32,abs(nx))*(1-smooth(.60,.78,nz));n=min(3,max(0,round((ny-.18)/.64*3)));tip=smooth(.30,.41,abs(nx))
   use(f'Leg{n}'+suffix,amount*(1-tip));use(f'Tip{n}'+suffix,amount*tip)
   if amount<.1:use('Head',smooth(.64,.78,ny))
  elif family=='squid':
   a=math.atan2(y/max(l,.01),x/max(w,.01))%math.tau;n=int(round(a*8/math.tau))%8
   use('Tentacle'+str(n),1-smooth(.38,.51,nz))
  else:
   use('Tail',1-smooth(.18,.40,ny));use('Head',smooth(.70,.90,ny));use('Fin'+suffix,smooth(.28,.42,abs(nx))*(1-smooth(.60,.78,ny)))
  # Normalize and keep only supported, nonzero influences; every vertex has a valid skin.
  weights={name:weight for name,weight in weights.items() if weight>.001};total=sum(weights.values())
  for name,weight in weights.items():groups[name].add([v.index],weight/total,'REPLACE');usage[name]+=1
 if family=='biped':
  # Armor blocks must bend at their joins, never stretch triangles from a fist to a foot.
  # Split shared vertices at anatomical seams and rigidly bind each original face.
  oldmesh=mesh.data;vertices=[];faces=[];uvs=[];normals=[];weights_by_vertex=[];materials=[]
  lookup={}
  group_names={g.index:g.name for g in mesh.vertex_groups}
  source_uv=oldmesh.uv_layers.active
  for poly in oldmesh.polygons:
   votes={}
   for index in poly.vertices:
    for group in oldmesh.vertices[index].groups:votes[group_names[group.group]]=votes.get(group_names[group.group],0)+group.weight
   joint=max(votes,key=votes.get);face=[]
   for loop_index in poly.loop_indices:
    loop=oldmesh.loops[loop_index];key=(loop.vertex_index,joint)
    if key not in lookup:
     lookup[key]=len(vertices);vertices.append(oldmesh.vertices[loop.vertex_index].co.copy());weights_by_vertex.append(joint)
    face.append(lookup[key]);uvs.append(source_uv.data[loop_index].uv.copy());normals.append(oldmesh.corner_normals[loop_index].vector.copy())
   faces.append(face);materials.append(poly.material_index)
  segmented=bpy.data.meshes.new(ident+'_articulated_armor');segmented.from_pydata(vertices,[],faces);segmented.update()
  layer=segmented.uv_layers.new(name='UVMap')
  for index,uv in enumerate(uvs):layer.data[index].uv=uv
  for material in oldmesh.materials:segmented.materials.append(material)
  for index,poly in enumerate(segmented.polygons):poly.material_index=materials[index];poly.use_smooth=True
  segmented.normals_split_custom_set(normals);mesh.data=segmented
  mesh.vertex_groups.clear();groups={name:mesh.vertex_groups.new(name=name) for name in bones};usage={name:0 for name in bones}
  for index,joint in enumerate(weights_by_vertex):groups[joint].add([index],1,'REPLACE');usage[joint]+=1
 modifier=mesh.modifiers.new('CreatureSkin','ARMATURE');modifier.object=rigobj;mesh.parent=rigobj
 mesh.name=ident+'_source_mesh';rigobj['source_sha256']=hashlib.sha256(source.read_bytes()).hexdigest()
 for image in bpy.data.images:
  if image.type=='IMAGE' and image.size[0]>1024 and ident!='colossus':image.scale(1024,1024)
  if image.type=='IMAGE':image.pack()
 bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rigobj.select_set(True);bpy.context.view_layer.objects.active=rigobj
 bpy.ops.wm.save_as_mainfile(filepath=str(WORK/f'{ident}.blend'),compress=True)
 output=OUT/f'{ident}.glb'
 bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,export_animations=False,export_skins=True,export_yup=True,export_apply=False,export_extras=False)
 record={'Id':ident,'Family':family,'Height':h,'BodyHeight':bodyheight,'LabelHeight':h+.25,'Stride':max(.35,min(2.4,l*.70 if family!='biped' else h*.48)),'Source':str(source.relative_to(ROOT)).replace('\\','/'),'SourceSha256':rigobj['source_sha256'],'Sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'Bones':list(bones),'WeightedVertices':usage,'Vertices':len(mesh.data.vertices),'TextureMaxDimension':2048 if ident=='colossus' else 1024,'CompletedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 (WORK/f'{ident}.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print('RIG_COMPLETE '+json.dumps({'id':ident,'bones':len(bones),'vertices':len(mesh.data.vertices)}),flush=True)
 return record
ids=list(heights) if not args.ids else args.ids.split(',');records=[]
for ident in ids:
 path=WORK/f'{ident}.json'
 if not args.force and path.exists() and (OUT/f'{ident}.glb').exists():records.append(json.loads(path.read_text(encoding='utf-8')))
 else:records.append(rig(ident))
if all((WORK/f'{ident}.json').exists() for ident in heights):
 records=[json.loads((WORK/f'{ident}.json').read_text(encoding='utf-8')) for ident in heights]
 (ROOT/'data/creature_models.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('RIG_BATCH_COMPLETE',len(records),flush=True)
