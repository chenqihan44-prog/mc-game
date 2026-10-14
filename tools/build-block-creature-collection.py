"""Author the remaining 23 creatures in the approved revision-2 visual style.

No external generation, imported source mesh, game replacement, or service charge.
Each species has authored geometry, rigid per-part skin and family-specific clips.
"""
import argparse, importlib.util, json, math, pathlib, random, shutil, sys
import bpy
import numpy as np
from mathutils import Vector, Matrix

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('reference_v2',ROOT/'tools/build-block-creature-prototypes-v2.py')
v2=importlib.util.module_from_spec(spec);spec.loader.exec_module(v2)
OUT=ROOT/'docs/previews/block-creature-collection-2026-10-13'
OUT.mkdir(parents=True,exist_ok=True);v2.OUT=OUT;v2.base.OUT=OUT
parser=argparse.ArgumentParser();parser.add_argument('--ids');parser.add_argument('--force',action='store_true');parser.add_argument('--still-only',action='store_true')
ARGS=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
CATALOG=json.loads((ROOT/'data/creatures.json').read_text(encoding='utf-8-sig'))
NAMES={r['Id']:r['Name'] for r in CATALOG}
GROUPS={
 'land':['deer','boar','sheep','chicken','horse','wolf'],
 'small_aquatic':['cat','fox','armadillo','cod','squid','turtle'],
 'enemies':['skitter','husk','sentinel','zombie','skeleton','creeper','spider'],
 'bosses':['mine_warden','furnace_warden','observatory_warden','colossus'],
}
P=v2.PALETTE
P.update({
 'tan':(.64,.42,.22),'pale':(.91,.83,.65),'wool':(.95,.91,.79),'woolshade':(.84,.78,.65),
 'boarfur':(.31,.25,.20),'bristle':(.19,.16,.13),'tusk':(.89,.83,.65),
 'chestnut':(.51,.27,.12),'mane':(.15,.105,.08),'silverfur':(.57,.62,.64),'wolfpale':(.84,.86,.82),
 'orangefur':(.80,.39,.12),'furblack':(.13,.12,.13),'furwhite':(.94,.90,.79),'greeneye':(.35,.73,.26),
 'goldbeak':(.93,.65,.14),'comb':(.72,.12,.09),'shellbrown':(.52,.37,.25),'shelllight':(.68,.51,.32),
 'turtlegreen':(.32,.46,.20),'turtleshell':(.23,.34,.16),'turtletrim':(.67,.64,.35),
 'fishskin':(.60,.52,.35),'fishpale':(.84,.79,.58),'fin':(.53,.43,.27),
 'squidblue':(.20,.27,.43),'squidlight':(.35,.43,.62),'sucker':(.72,.61,.53),
 'chitin':(.20,.28,.12),'moss':(.40,.49,.17),'spiderblack':(.18,.15,.18),'redeye':(.98,.12,.04),
 'deadskin':(.40,.53,.60),'rags':(.24,.23,.25),'raglight':(.35,.30,.26),'bandage':(.63,.60,.51),
 'zombiegreen':(.39,.52,.24),'shirt':(.18,.45,.49),'pants':(.27,.23,.38),
 'bone':(.85,.82,.70),'bonedark':(.61,.57,.47),'bow':(.37,.23,.11),'string':(.83,.80,.67),
 'creepgreen':(.32,.55,.22),'creepdark':(.16,.31,.10),'copper':(.61,.40,.20),'patina':(.20,.43,.41),
 'coldstone':(.28,.39,.51),'silvermetal':(.64,.69,.72),'cyan':(.21,.85,1),'iron':(.19,.19,.18),
 'brass':(.65,.49,.25),'molten':(1,.39,.025),
})
EMISSIVE={'amber','glyph','cyan','molten','redeye','greeneye'}

class Creature(v2.DetailedCreature):
 def __init__(self,ident,family,atlas=1024):
  super().__init__(ident)
  self.family=family;self.preview_clip='walk';self.emissive_used=False;self.gpu=False
  if atlas!=self.size:
   self.size=atlas;self.image.scale(atlas,atlas)
   self.pixels=np.zeros((atlas,atlas,4),dtype=np.float32);self.pixels[:,:,3]=1
   self.glow=np.zeros_like(self.pixels);self.glow[:,:,3]=1
  self.bone('Root',(0,0,0));self.bone('Body',(0,0,.5),'Root')

 def paint(self,style,p,name):
  x,y,z=p;rgb=np.array(P[style]);ix,iy,iz=(math.floor(a*22) for a in p)
  rng=random.Random(f'{self.ident}:{style}:{ix//2}:{iy//2}:{iz//2}')
  shade=rng.choice([.94,.98,1,1,1.04])
  if style in {'stone','stone_light','patina','iron','coldstone','chitin','spiderblack'}:
   shade=rng.choice([.84,.93,.97,1,1.08,1.14])
  if self.ident=='deer' and style=='tan' and name=='Torso':
   spot=random.Random(f'deer:{ix//3}:{iy//3}:{iz//3}').random()
   if z>.95 and spot<.24:rgb=np.array(P['pale'])
  if self.ident=='cat' and style=='orangefur':
   patch=random.Random(f'calico:{ix//6}:{iy//6}:{iz//6}').random()
   if patch<.36:rgb=np.array(P['furblack'])
   elif patch>.76:rgb=np.array(P['furwhite'])
  if self.ident=='wolf' and style=='silverfur' and z<.39:rgb=np.array(P['wolfpale'])
  if self.ident=='creeper' and style=='creepgreen':
   shade=rng.choice([.55,.72,.88,1,1.17,1.33])
  return tuple(np.clip(rgb*shade,0,1))

 def tile_face(self,style,name,normal,local_vertices,center,rotation):
  index=self.tiles;uv=super().tile_face(style,name,normal,local_vertices,center,rotation)
  if style in EMISSIVE and self.family in {'biped','arthropod'}:
   columns=self.size//16;tx,ty=(index%columns)*16,(index//columns)*16
   self.glow[ty:ty+16,tx:tx+16,:3]=self.pixels[ty:ty+16,tx:tx+16,:3];self.emissive_used=True
  return uv

 def box(self,*args,**kwargs):
  if 'bevel' not in kwargs:
   dims=args[2];kwargs['bevel']=min(dims)*(.055 if self.family=='biped' else .032)
  return super().box(*args,**kwargs)

 def bar(self,name,a,b,width,bone,style,depth=None,segments=1):
  a,b=Vector(a),Vector(b);d=b-a
  rotation=Vector((0,0,1)).rotation_difference(d.normalized()).to_euler()
  self.box(name,tuple((a+b)/2),(width,depth or width,d.length),bone,style,rotation=tuple(rotation),segments=segments)

 def eyes_front(self,y,z,x=.12,width=.10,height=.11,pupil='black',bone='Head',white=True):
  for s in [-1,1]:
   if white:self.box('EyeWhite'+str(s),(s*x,y,z),(width,.018,height),bone,'white',bevel=.001)
   self.box('Eye'+str(s),(s*(x-.017),y+.013,z-.006),(width*(.48 if white else 1),.014,height*.82),bone,pupil,bevel=.001)

 def decorate_material(self,material,shader,texture):
  if not self.emissive_used:return
  img=bpy.data.images.new(self.ident+'_emission',width=self.size,height=self.size,alpha=False)
  img.pixels.foreach_set(self.glow.ravel());img.filepath_raw=str(self.folder/(self.ident+'-emission.png'));img.file_format='PNG';img.save();img.pack()
  node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;node.interpolation='Closest'
  material.node_tree.links.new(node.outputs['Color'],shader.inputs['Emission Color']);shader.inputs['Emission Strength'].default_value=1.5

 def finish(self):
  # Normalize bind geometry, all joints and the ground plane together.
  minimum=min(p[2] for p in self.vertices)
  self.vertices=[(x,y,z-minimum) for x,y,z in self.vertices]
  self.bones={n:((x,y,z-(minimum if n!='Root' else 0)),parent) for n,((x,y,z),parent) in self.bones.items()}
  height=max(v[2] for v in self.vertices)
  record=super().finish();record.update(name=NAMES[self.ident],family=self.family,height=height,
   group=next(g for g,ids in GROUPS.items() if self.ident in ids),previewClip=self.preview_clip,
   emissionMask=self.emissive_used,gpuRender=self.gpu,scope='authored asset and preview; no production replacement')
  (self.folder/'model-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
  return record

 def animate(self):
  scene=bpy.context.scene;scene.render.fps=24;self.rig.animation_data_create()
  attack={'boar':'charge','wolf':'bite','skitter':'bite','spider':'bite','husk':'melee','zombie':'melee',
   'skeleton':'bolt','sentinel':'bolt','mine_warden':'bolt','observatory_warden':'bolt','furnace_warden':'charge','colossus':'slam','creeper':'explode'}.get(self.ident)
  self.clips=['idle','swim','dart'] if self.family in {'fish','squid'} else ['idle','walk','run']
  extra=attack or ('peck' if self.family=='bird' else 'curl' if self.ident=='armadillo' else 'graze' if self.ident in {'deer','sheep','horse'} else 'look')
  if self.family not in {'fish','squid'}:self.clips.append(extra)
  elif self.family=='squid':self.clips.append('pulse')
  self.preview_clip=attack or ('swim' if self.family in {'fish','squid'} else 'walk')
  for clip in self.clips:
   self.rig.animation_data.action=None;frames=33 if clip in {'slam','bolt','melee','bite','charge','explode'} else 25 if clip in {'walk','run','swim','dart','pulse'} else 49
   action=None
   for f in range(1,frames+1):
    t=(f-1)/(frames-1);phase=t*math.tau;poses=self.rig.pose.bones
    for b in poses:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
    def setpose(n,x=0,y=0,z=0):
     if n in poses:poses[n].rotation_euler=(x,y,z)
    def ease(a,b):
     p=max(0,min(1,(t-a)/(b-a)));return p*p*(3-2*p)
    if clip in {'walk','run'}:
     amp=.31 if clip=='walk' else .49
     for name in self.bones:
      offset=math.pi if name.endswith('R') else 0
      if 'Rear' in name:offset+=math.pi
      wave=math.sin(phase+offset)
      if name.startswith('Leg') and self.family!='arthropod':setpose(name,x=wave*amp)
      if name.startswith('Knee'):setpose(name,x=max(0,-wave)*amp*.62)
      if name.startswith('Arm'):setpose(name,x=-wave*amp*.55)
     if self.family=='arthropod':
      for n in range(4):
       for side,s in [('L',-1),('R',1)]:
        p=phase+n*math.pi+(math.pi if s>0 else 0)
        setpose(f'Leg{n}{side}',y=math.cos(p)*amp*.48*s,z=math.sin(p)*amp*.13*s)
        setpose(f'Tip{n}{side}',z=max(0,math.sin(p))*amp*.24*s)
     elif self.ident=='turtle':
      for name in self.bones:
       if name.startswith('Leg'):setpose(name,y=math.sin(phase+(0 if 'FrontL' in name or 'RearR' in name else math.pi))*.2)
     setpose('Head',x=math.sin(phase*2)*.018);poses['Body'].location.z=abs(math.sin(phase))*.006
     setpose('WingL',z=-.03-math.sin(phase)*.035);setpose('WingR',z=.03+math.sin(phase)*.035)
    elif clip in {'swim','dart','pulse'}:
     amp=.18 if clip=='swim' else .33
     if self.family=='fish':
      setpose('Body',z=math.sin(phase)*.025);setpose('Tail',y=math.sin(phase)*amp)
      setpose('FinL',z=math.sin(phase)*.16);setpose('FinR',z=-math.sin(phase)*.16)
     else:
      for n in range(8):
       a=n*math.tau/8;p=math.sin(phase+n*.30)*amp
       setpose('Tentacle'+str(n),x=math.sin(a)*p,z=math.cos(a)*p)
       setpose('TipTentacle'+str(n),x=math.sin(a)*p*.6,z=math.cos(a)*p*.6)
      setpose('Body',x=math.sin(phase)*.035)
    elif clip in {'melee','bite','slam','bolt','charge','explode'}:
     anticipation=ease(0,.36);strike=ease(.36,.55);recover=1-ease(.64,1)
     if clip in {'melee','slam'}:
      setpose('Body',x=(-.08*anticipation+.23*strike)*recover)
      for side in ['L','R']:
       amount=(-1.08*anticipation+1.36*strike)*recover
       setpose('Arm'+side,x=amount if clip=='slam' or side=='R' else -.18*anticipation*recover)
       setpose('Hand'+side,x=-.13*anticipation*recover)
     elif clip=='bolt':
      for side in ['L','R']:setpose('Arm'+side,x=-1.18*anticipation*recover)
      setpose('HandR',x=-.12*anticipation*recover);setpose('Body',x=-.035*strike*recover)
     elif clip=='bite':
      setpose('Head',x=(-.18*anticipation+.35*strike)*recover);setpose('Jaw',x=.38*anticipation*(1-strike)*recover)
      setpose('Body',x=(-.10*anticipation+.13*strike)*recover)
     elif clip=='charge':
      setpose('Body',x=.13*anticipation*recover);setpose('Head',x=.18*anticipation*recover)
      for name in self.bones:
       if name.startswith('Leg'):setpose(name,x=math.sin(phase*2+(math.pi if name.endswith('R') else 0))*.25*anticipation*recover)
     else:setpose('Body',z=math.sin(phase*7)*.035*anticipation*recover);setpose('Head',y=math.sin(phase*6)*.045*anticipation*recover)
    else:
     poses['Body'].location.z=math.sin(phase)*.003
     setpose('Head',y=math.sin(phase)*.04)
     if clip in {'graze','peck'}:setpose('Head',x=math.sin(t*math.pi)**2*(.32 if clip=='graze' else .5))
     if clip=='curl':
      setpose('Head',x=math.sin(t*math.pi)**2*.4)
      for n in self.bones:
       if n.startswith('Leg'):setpose(n,x=math.sin(t*math.pi)**2*.45)
    if self.family not in {'fish','squid'}:setpose('Tail',y=math.sin(phase)*.11);setpose('TailTip',y=math.sin(phase-.4)*.07)
    for b in poses:b.keyframe_insert('rotation_euler',frame=f,group=b.name);b.keyframe_insert('location',frame=f,group=b.name)
    if action is None:action=self.rig.animation_data.action;action.name=clip
   track=self.rig.animation_data.nla_tracks.new();track.name=clip;track.strips.new(clip,1,action);track.mute=True
  self.rig.animation_data.action=None;scene.frame_set(1)

 def stage(self):
  super().stage();scene=bpy.context.scene
  width=max(p[0] for p in self.vertices)-min(p[0] for p in self.vertices)
  length=max(p[1] for p in self.vertices)-min(p[1] for p in self.vertices)
  height=max(p[2] for p in self.vertices)
  scene.camera.data.ortho_scale=max(height,width*.85,length*.78)*1.38
  self.target=Vector((0,0,height*.47))
  prefs=bpy.context.preferences.addons.get('cycles')
  if prefs:
   for backend in ['OPTIX','CUDA','HIP','ONEAPI']:
    try:
     settings=prefs.preferences;settings.compute_device_type=backend;settings.get_devices()
     available=[d for d in settings.devices if d.type==backend]
     if available:
      for d in settings.devices:d.use=d.type==backend
      scene.cycles.device='GPU';self.gpu=True;break
    except Exception:pass
  scene.cycles.samples=24
  if self.family in {'fish','squid'}:
   for obj in list(scene.objects):
    if obj.name.startswith('PreviewTerrain') or obj.name.startswith('PreviewTree'):bpy.data.objects.remove(obj,do_unlink=True)
   m=bpy.data.materials.new('Preview water');m.use_nodes=True
   shader=m.node_tree.nodes['Principled BSDF'];shader.inputs['Base Color'].default_value=(.10,.35,.53,1);shader.inputs['Roughness'].default_value=.5
   bpy.ops.mesh.primitive_cube_add(size=2,location=(0,0,-.3));water=bpy.context.object;water.name='PreviewWater';water.scale=(5,5,.25);water.data.materials.append(m)
   self.rig.location.z=.20;self.target.z+=.20

 def render(self):
  scene=bpy.context.scene;scale=scene.camera.data.ortho_scale
  height=max(p[2] for p in self.vertices)
  if self.ident=='colossus':
   # Maintain studio framing around the large asset; production scale is real.
   for obj in list(scene.objects):
    if obj.name.startswith('PreviewTree') or obj.name.startswith('PreviewTerrace'):bpy.data.objects.remove(obj,do_unlink=True)
   for obj in scene.objects:
    if obj.name.startswith('PreviewTerrain'):obj.scale.x*=3;obj.scale.y*=3
  radius=max(7.5,scale*2)
  def camera(offset):
   off=Vector(offset).normalized()*radius;scene.camera.location=self.target+off
   scene.camera.data.lens=off.length*36/scale;v2.base.point_camera(scene.camera,self.target)
  for label,offset in [('three-quarter',(3.8,6.5,1.25)),('side',(7,0,.85)),('front',(0,7,.7))]:
   camera(offset);scene.render.filepath=str(self.folder/(self.ident+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
  camera((3.8,6.5,1.25));bpy.ops.wm.save_as_mainfile(filepath=str(self.folder/(self.ident+'.blend')),compress=True)
  if ARGS.still_only:return
  scene.render.resolution_x=420;scene.render.resolution_y=420;scene.cycles.samples=8
  folder=self.folder/'motion';folder.mkdir(exist_ok=True)
  action=bpy.data.actions[self.preview_clip];self.rig.animation_data.action=action
  last=round(action.frame_range[1])
  for n in range(24):
   scene.frame_set(1+round(n*(last-1)/23));scene.render.filepath=str(folder/f'{n:02d}.png');bpy.ops.render.render(write_still=True)
  self.rig.animation_data.action=None;scene.frame_set(1)

def quad(ident,width,length,hip,bodyz,bodyh,skin,legwidth=.16):
 c=Creature(ident,'quadruped');c.bones['Body']=((0,0,bodyz),'Root')
 c.bone('Head',(0,length*.33,bodyz+.1),'Body');c.bone('Tail',(0,-length*.48,bodyz+.06),'Body')
 for s,side in [(-1,'L'),(1,'R')]:
  for front,y in [('Front',length*.32),('Rear',-length*.32)]:
   upper='Leg'+front+side;lower='Knee'+front+side;x=s*width*.34;knee=hip*.46
   c.bone(upper,(x,y,hip),'Body');c.bone(lower,(x,y,knee),upper)
   c.box(upper,(x,y,(hip+knee)/2),(legwidth,legwidth*1.15,hip-knee+.025),upper,skin)
   c.box('Joint'+front+side,(x,y,knee),(legwidth*.96,legwidth*1.13,.055),lower,skin)
   c.box(lower,(x,y,(knee+.08)/2),(legwidth*.92,legwidth*1.08,knee-.04),lower,skin)
   c.box('Foot'+front+side,(x,y+.026,.045),(legwidth*1.13,legwidth*1.45,.09),lower,'hoof' if ident in {'deer','boar','sheep','horse'} else skin)
 c.box('Torso',(0,-.035,bodyz),(width,length,bodyh),'Body',skin,segments=2)
 return c

def mammal(ident):
 if ident=='deer':
  c=quad(ident,.52,1.10,.85,1.09,.48,'tan',.13)
  c.box('Belly',(0,-.05,.90),(.47,.94,.13),'Body','pale')
  c.box('Neck',(0,.45,1.30),(.28,.34,.55),'Head','tan',rotation=(-.17,0,0),segments=2)
  c.box('Head',(0,.60,1.54),(.35,.48,.29),'Head','tan',segments=2)
  c.box('Muzzle',(0,.865,1.455),(.27,.19,.17),'Head','pale');c.box('Nose',(0,.97,1.48),(.13,.025,.077),'Head','black')
  c.eyes_front(.847,1.58,.13,.065,.08,white=False)
  for s in [-1,1]:
   c.box('Ear'+str(s),(s*.25,.50,1.70),(.18,.13,.33),'Head','tan',rotation=(.10,s*.58,0))
   c.box('EarInner'+str(s),(s*.25,.578,1.70),(.12,.018,.22),'Head','pale',rotation=(.10,s*.58,0))
   # Six purposeful stepped antler branches, not tiny voxel rubble.
   points=[(s*.13,.49,1.68),(s*.21,.44,1.91),(s*.35,.36,2.12),(s*.39,.30,2.33)]
   for n in range(3):c.bar('AntlerBeam'+str((s,n)),points[n],points[n+1],.067,'Head','horn')
   for n,(a,b) in enumerate([(points[1],(s*.39,.51,2.02)),(points[2],(s*.54,.33,2.24)),(points[2],(s*.30,.20,2.38))]):c.bar('AntlerTine'+str((s,n)),a,b,.057,'Head','horn')
  c.box('Tail',(0,-.68,1.12),(.16,.29,.20),'Tail','tan',rotation=(-.4,0,0));c.features=['branching antlers','white-spotted back','pale belly and muzzle','upright ears','slender jointed legs']
 elif ident=='boar':
  c=quad(ident,.76,1.18,.49,.78,.58,'boarfur',.215)
  c.box('Shoulder',(0,.30,.88),(.81,.40,.59),'Body','boarfur',segments=2)
  c.box('Head',(0,.58,.79),(.58,.49,.42),'Head','boarfur',segments=2)
  c.box('Snout',(0,.865,.72),(.39,.21,.23),'Head','nose',segments=2)
  c.eyes_front(.83,.89,.215,.075,.065,pupil='amber',white=False)
  for s in [-1,1]:
   c.box('Nostril'+str(s),(s*.10,.978,.73),(.06,.009,.075),'Head','black',bevel=.001)
   c.box('Ear'+str(s),(s*.27,.48,1.035),(.17,.17,.25),'Head','boarfur',rotation=(.15,s*.35,0))
   c.box('EarInner'+str(s),(s*.27,.571,1.03),(.11,.018,.14),'Head','earpink',rotation=(.15,s*.35,0))
   c.bar('TuskRoot'+str(s),(s*.235,.82,.68),(s*.29,.94,.75),.088,'Head','tusk')
   c.bar('TuskTip'+str(s),(s*.29,.94,.75),(s*.32,.97,.88),.064,'Head','tusk')
  for n in range(7):
   c.box('BackBristle'+str(n),(0,.27-n*.13,1.11),(.12,.12,.19 if n<4 else .13),'Body','bristle',rotation=(-.35,0,0))
  c.box('TailStem',(0,-.74,.88),(.08,.23,.09),'Tail','boarfur');c.box('TailTassel',(0,-.85,.80),(.11,.10,.16),'Tail','bristle')
  c.features=['raised bristle ridge','broad shoulder','square snout and nostrils','two-part ivory tusks','short heavy legs','charge anticipation']
 elif ident=='sheep':
  c=quad(ident,.74,1.08,.46,.76,.56,'wool',.17)
  # Large layered wool blocks create a readable coat at gameplay distance.
  for s in [-1,1]:
   for n in range(4):c.box('WoolSide'+str((s,n)),(s*.38,.39-n*.24,.73),(.17,.27,.40),'Body','woolshade' if n%3==0 else 'wool',bevel=.024,segments=2)
  for x in [-.24,0,.24]:
   for y in [-.36,0,.36]:c.box('WoolTop'+str((x,y)),(x,y,1.035),(.28,.33,.15),'Body','wool',bevel=.025,segments=2)
  c.box('Head',(0,.61,.91),(.39,.42,.36),'Head','tan',segments=2)
  c.box('Muzzle',(0,.84,.81),(.30,.16,.17),'Head','pale');c.box('Nose',(0,.932,.85),(.14,.016,.067),'Head','black')
  c.eyes_front(.83,1.00,.13,.07,.065,white=False)
  for s in [-1,1]:
   c.box('Ear'+str(s),(s*.29,.54,.995),(.24,.14,.12),'Head','tan',rotation=(0,s*.18,0))
   c.box('EarInner'+str(s),(s*.29,.615,.995),(.17,.014,.064),'Head','pale')
  c.box('ForeheadWool',(0,.51,1.125),(.46,.35,.11),'Head','wool',segments=2);c.box('Tail',(0,-.65,.78),(.16,.25,.18),'Tail','wool')
  c.features=['layered broad wool clumps','exposed tan face and ears','dark nose and hooves','separate forehead wool','grazing motion']
 elif ident=='horse':
  c=quad(ident,.73,1.43,1.05,1.32,.63,'chestnut',.19)
  c.box('Haunch',(0,-.51,1.36),(.77,.51,.62),'Body','chestnut',segments=2)
  c.box('Chest',(0,.47,1.41),(.65,.39,.65),'Body','chestnut',segments=2)
  c.box('Neck',(0,.54,1.71),(.34,.39,.75),'Head','chestnut',rotation=(-.16,0,0),segments=2)
  c.box('Head',(0,.75,1.99),(.36,.59,.32),'Head','chestnut',rotation=(-.10,0,0),segments=2)
  c.box('Muzzle',(0,1.08,1.92),(.31,.22,.24),'Head','mane',segments=2)
  c.eyes_front(1.037,2.04,.13,.058,.068,white=False)
  c.box('Blaze',(0,1.051,2.01),(.087,.015,.25),'Head','pale',bevel=.001)
  for s in [-1,1]:c.box('Ear'+str(s),(s*.145,.64,2.28),(.13,.16,.31),'Head','chestnut',rotation=(.08,s*.08,0))
  for n in range(6):c.box('Mane'+str(n),(0,.31+n*.027,1.42+n*.145),(.15,.18,.19),'Head','mane')
  c.bone('TailTip',(0,-.98,.85),'Tail')
  c.box('TailTop',(0,-.92,1.20),(.20,.25,.55),'Tail','mane',rotation=(.16,0,0));c.box('TailBottom',(0,-1.01,.80),(.21,.22,.35),'TailTip','mane')
  c.features=['raised elongated neck','long muzzle and forehead blaze','layered dark mane','strong haunches','long jointed legs','segmented hanging tail']
 else:
  cfg={'wolf':(.55,1.07,.45,.72,.42,'silverfur'),'fox':(.43,.96,.38,.59,.35,'orangefur'),'cat':(.34,.76,.35,.53,.31,'orangefur')}
  width,length,hip,z,h,skin=cfg[ident];c=quad(ident,width,length,hip,z,h,skin,.13 if ident!='wolf' else .16)
  c.bones['Head']=((0,length*.34,z+.09),'Body');c.bone('Jaw',(0,length*.65,z+.015),'Head')
  if ident=='wolf':
   c.box('Ruff',(0,.36,.80),(.59,.37,.48),'Body','silverfur',segments=2)
   for n in range(3):c.box('ChestTuft'+str(n),(0,.45,.67-n*.095),(.42-n*.07,.13,.13),'Body','wolfpale')
  headw=.43 if ident=='wolf' else .34 if ident=='fox' else .33
  heady=length*.55;headz=z+.16
  c.box('Head',(0,heady,headz),(headw,.36,.34 if ident=='wolf' else .29),'Head',skin,segments=2)
  fronty=heady+.19
  c.eyes_front(fronty,headz+.045,headw*.30,.09,.087,pupil='amber' if ident in {'wolf','fox'} else 'greeneye')
  for s in [-1,1]:
   c.box('EarBase'+str(s),(s*headw*.38,heady-.06,headz+.21),(.15,.14,.19),'Head',skin,rotation=(0,s*.11,0))
   c.box('EarTip'+str(s),(s*headw*.40,heady-.07,headz+.32),(.095,.12,.12),'Head','furblack' if ident!='wolf' else 'silverfur')
   c.box('EarInner'+str(s),(s*headw*.38,heady+.018,headz+.23),(.075,.012,.12),'Head','earpink' if ident=='cat' else 'pale',bevel=.001)
  muzzlew=.27 if ident=='wolf' else .23 if ident=='fox' else .22;depth=.27 if ident!='cat' else .13
  c.box('Muzzle',(0,fronty+.08,headz-.065),(muzzlew,depth,.16),'Head','wolfpale' if ident=='wolf' else 'furwhite',segments=2)
  c.box('LowerJaw',(0,fronty+.06,headz-.16),(muzzlew*.84,depth*.87,.065),'Jaw','wolfpale' if ident=='wolf' else 'furwhite')
  c.box('Nose',(0,fronty+.08+depth/2+.004,headz-.024),(.10,.024,.065),'Head','black')
  if ident=='cat':
   for s in [-1,1]:
    for n in [-1,1]:c.bar('Whisker'+str((s,n)),(s*.10,fronty+.15,headz-.085),(s*.28,fronty+.17,headz-.07+n*.034),.008,'Head','white')
   c.bone('TailTip',(0,-.66,.86),'Tail');c.box('TailBase',(0,-.53,.72),(.085,.15,.34),'Tail','orangefur',rotation=(.22,0,0));c.box('TailUpright',(0,-.66,.96),(.084,.10,.42),'TailTip','furblack');c.box('TailTip',(0,-.66,1.19),(.10,.10,.13),'TailTip','furwhite')
  else:
   c.bone('TailTip',(0,-length*.65,z-.08),'Tail')
   c.box('TailBase',(0,-length*.62,z),(.24 if ident=='wolf' else .31,.46,.24),'Tail',skin,rotation=(.25,0,0),segments=2)
   c.box('TailTaper',(0,-length*.84,z-.12),(.17 if ident=='wolf' else .24,.32,.19),'TailTip',skin,rotation=(.35,0,0))
   c.box('TailTip',(0,-length*.97,z-.19),(.13,.20,.14),'TailTip','furwhite' if ident=='fox' else 'silverfur')
  c.features=['layered face and jaw','white eyes with species eye color','stepped upright ears','jointed paws','segmented '+('upright calico tail' if ident=='cat' else 'bushy white-tipped tail' if ident=='fox' else 'heavy tail and pale chest ruff')]
 return c

def chicken():
 c=Creature('chicken','bird');c.bones['Body']=((0,0,.45),'Root');c.bone('Head',(0,.24,.63),'Body');c.bone('Tail',(0,-.29,.50),'Body')
 c.box('Torso',(0,0,.47),(.41,.51,.37),'Body','wool',segments=2)
 c.box('Breast',(0,.22,.51),(.36,.20,.34),'Body','wool')
 c.box('Head',(0,.31,.76),(.29,.29,.29),'Head','wool',segments=2);c.eyes_front(.463,.80,.094,.06,.073,white=False)
 c.box('UpperBeak',(0,.50,.74),(.20,.20,.075),'Head','goldbeak');c.box('LowerBeak',(0,.49,.68),(.16,.17,.046),'Head','goldbeak')
 c.box('Wattle',(0,.417,.605),(.11,.10,.15),'Head','comb')
 for n in range(3):c.box('Comb'+str(n),(0,.24+n*.078,.957),(.086,.08,.12 if n==1 else .09),'Head','comb')
 for s,side in [(-1,'L'),(1,'R')]:
  c.bone('Wing'+side,(s*.19,0,.55),'Body');c.bone('LegFront'+side,(s*.11,.04,.30),'Body');c.bone('KneeFront'+side,(s*.11,.04,.16),'LegFront'+side)
  for n in range(3):c.box('WingFeather'+side+str(n),(s*(.215+n*.023),-.035-n*.07,.47-n*.035),(.095,.31-n*.04,.11),'Wing'+side,'woolshade' if n==2 else 'wool')
  c.box('Shank'+side,(s*.11,.04,.16),(.055,.065,.26),'LegFront'+side,'goldbeak')
  for toe in [-1,0,1]:c.bar('Toe'+side+str(toe),(s*.11,.04,.035),(s*.11+toe*.05,.20,.035),.035,'KneeFront'+side,'goldbeak')
  c.bar('BackToe'+side,(s*.11,.04,.035),(s*.11,-.045,.035),.029,'KneeFront'+side,'goldbeak')
 for n in range(5):c.box('TailFan'+str(n),((n-2)*.04,-.38,.58+(.12-abs(n-2)*.035)),(.08,.21,.23),'Tail','wool',rotation=(-.62,(n-2)*.10,0))
 c.features=['two legs with three forward toes and back toe','layered folded wing feathers','red stepped comb and wattle','two-part yellow beak','fan tail','peck motion'];return c

def armadillo():
 c=quad('armadillo',.48,.89,.23,.43,.36,'shellbrown',.11)
 c.box('Belly',(0,0,.28),(.43,.77,.10),'Body','shelllight')
 for n in range(6):
  y=.34-n*.135
  c.box('ShellBandTop'+str(n),(0,y,.637),(.47,.095,.10),'Body','shelllight',bevel=.012)
  for s in [-1,1]:c.box('ShellBandSide'+str((s,n)),(s*.241,y,.47),(.10,.095,.28),'Body','shellbrown' if n%2 else 'shelllight',rotation=(0,s*.24,0))
 c.box('Head',(0,.53,.36),(.28,.31,.23),'Head','shellbrown',segments=2);c.box('Snout',(0,.735,.315),(.17,.18,.11),'Head','shelllight');c.box('Nose',(0,.83,.32),(.095,.019,.055),'Head','nose')
 c.eyes_front(.696,.405,.098,.06,.052,white=False)
 for s in [-1,1]:c.box('Ear'+str(s),(s*.11,.43,.56),(.075,.095,.19),'Head','shellbrown',rotation=(.2,s*.2,0))
 for n in range(4):c.box('TailRing'+str(n),(0,-.54-n*.12,.32-n*.025),(.12-n*.018,.14,.105-n*.014),'Tail','shelllight' if n%2 else 'shellbrown')
 for name,(pos,parent) in list(c.bones.items()):
  if name.startswith('Knee'):
   x,y,z=pos
   for n in [-1,0,1]:c.bar('Claw'+name+str(n),(x+n*.028,y+.06,.06),(x+n*.028,y+.12,.027),.02,name,'tusk')
 c.features=['six articulated shell bands','distinct light band edges','elongated nose','small upright ears','short clawed feet','ringed tapering tail','curl anticipation'];return c

def aquatic(ident):
 if ident=='cod':
  c=Creature(ident,'fish');c.bones['Body']=((0,0,.28),'Root');c.bone('Head',(0,.40,.28),'Body');c.bone('Tail',(0,-.38,.27),'Body');c.bone('FinL',(-.13,.05,.24),'Body');c.bone('FinR',(.13,.05,.24),'Body')
  c.box('Body',(0,0,.28),(.26,.72,.33),'Body','fishskin',segments=2);c.box('Belly',(0,.01,.13),(.24,.66,.09),'Body','fishpale')
  c.box('Head',(0,.43,.28),(.28,.25,.30),'Head','fishskin',segments=2)
  for s in [-1,1]:
   c.box('EyeWhite'+str(s),(s*.145,.478,.35),(.018,.098,.105),'Head','white',bevel=.001);c.box('Eye'+str(s),(s*.157,.49,.35),(.010,.053,.069),'Head','black',bevel=.001)
   c.box('Gills'+str(s),(s*.139,.26,.28),(.014,.029,.18),'Head','fin',bevel=.001)
   c.box('PectoralFin'+str(s),(s*.22,.11,.21),(.22,.18,.035),'FinL' if s<0 else 'FinR','fin',rotation=(0,s*.3,s*.22))
  c.box('MouthTop',(0,.574,.245),(.23,.026,.029),'Head','fishpale');c.box('MouthGap',(0,.576,.218),(.20,.019,.017),'Head','black',bevel=0)
  for n in range(4):c.box('DorsalFin'+str(n),(0,.18-n*.10,.488),(.025,.11,.11 if n<2 else .075),'Body','fin',rotation=(.17,0,0))
  c.box('TailStem',(0,-.48,.27),(.12,.23,.16),'Tail','fishskin')
  for z,ang in [(.37,.30),(.17,-.30)]:c.box('TailFan'+str(z),(0,-.65,z),(.048,.26,.18),'Tail','fin',rotation=(ang,0,0))
  c.features=['pale belly stripe','side eyes and gill slits','paired pectoral fins','stepped dorsal fin','two-lobed tail fan','swim and dart clips'];return c
 if ident=='squid':
  c=Creature(ident,'squid',1536);c.bones['Body']=((0,0,.88),'Root');c.bone('Head',(0,0,.80),'Body');c.bone('Tail',(0,0,1.25),'Body')
  c.box('Mantle',(0,0,1.03),(.43,.54,.65),'Body','squidblue',segments=2);c.box('MantleTip',(0,-.05,1.39),(.30,.38,.19),'Body','squidblue',segments=2)
  c.box('Head',(0,.10,.71),(.41,.36,.25),'Head','squidlight',segments=2)
  for s in [-1,1]:
   c.box('EyeWhite'+str(s),(s*.225,.185,.76),(.022,.14,.15),'Head','white',bevel=.002);c.box('Eye'+str(s),(s*.241,.19,.76),(.012,.075,.10),'Head','black',bevel=.001)
   c.box('MantleFin'+str(s),(s*.28,-.07,1.17),(.22,.33,.035),'Body','squidlight',rotation=(0,s*.18,s*.24))
  for n in range(8):
   a=n*math.tau/8;x,y=math.cos(a)*.20,math.sin(a)*.20
   mid=(math.cos(a)*.36,math.sin(a)*.36,.32);tip=(math.cos(a)*.49,math.sin(a)*.49,.06)
   c.bone('Tentacle'+str(n),(x,y,.60),'Body');c.bone('TipTentacle'+str(n),mid,'Tentacle'+str(n))
   c.bar('TentacleUpper'+str(n),(x,y,.62),mid,.076,'Tentacle'+str(n),'squidblue')
   c.bar('TentacleLower'+str(n),mid,tip,.058,'TipTentacle'+str(n),'squidlight')
   for j in range(3):
    pos=Vector(mid).lerp(Vector(tip),(j+.35)/3);pos.x*=.90;pos.y*=.90
    c.box('Sucker'+str((n,j)),tuple(pos),(.047,.042,.024),'TipTentacle'+str(n),'sucker',bevel=.003)
  c.features=['tapered mantle and lateral fins','prominent side eyes','eight separately jointed tentacles','painted/raised sucker rows','swim and pulse clips'];return c
 c=quad('turtle',.68,.95,.17,.31,.30,'turtlegreen',.12)
 c.box('Plastron',(0,0,.19),(.67,.91,.12),'Body','turtletrim',segments=2)
 c.box('Shell',(0,-.015,.39),(.74,.93,.22),'Body','turtleshell',segments=2)
 for x in [-.25,0,.25]:
  for y in [-.30,0,.30]:c.box('Scute'+str((x,y)),(x,y,.525-abs(x)*.2),(.235,.28,.09),'Body','turtleshell',bevel=.019,segments=2)
 for s in [-1,1]:
  c.box('ShellRim'+str(s),(s*.385,0,.32),(.09,.93,.09),'Body','turtletrim')
  for front,y in [('Front',.29),('Rear',-.28)]:
   c.box('Flipper'+front+str(s),(s*.48,y,.125),(.40,.25,.065),'Leg'+front+('L' if s<0 else 'R'),'turtlegreen',rotation=(0,s*.10,s*(.28 if front=='Front' else -.25)))
 c.box('Head',(0,.63,.29),(.32,.31,.24),'Head','turtlegreen',segments=2);c.box('Muzzle',(0,.805,.25),(.29,.08,.13),'Head','turtletrim')
 c.eyes_front(.793,.34,.11,.069,.068);c.box('Tail',(0,-.59,.22),(.10,.23,.075),'Tail','turtlegreen')
 c.features=['layered nine-scute shell','contrasting shell rim and pale plastron','four wide flippers','recognizable turtle head','walk/run flipper motion'];return c

def arthropod(ident):
 skitter=ident=='skitter';c=Creature(ident,'arthropod',1536);skin='chitin' if skitter else 'spiderblack';trim='moss' if skitter else 'bronze'
 c.bones['Body']=((0,-.12,.46),'Root');c.bone('Head',(0,.42,.39),'Body');c.bone('Tail',(0,-.54,.48),'Body');c.bone('Jaw',(0,.66,.31),'Head')
 c.box('Abdomen',(0,-.31,.47),(.67,.77,.36),'Body',skin,segments=2);c.box('Thorax',(0,.14,.41),(.54,.43,.26),'Body',skin,segments=2)
 c.box('Head',(0,.47,.38),(.47,.35,.23),'Head',skin,segments=2)
 for n in range(5):c.box('AbdomenBand'+str(n),(0,-.02-n*.16,.668),(.58,.065,.055),'Body',trim)
 for s in [-1,1]:
  for n in range(4):
   y=.34-n*.22;spread=(n-1.5)*-.19
   start=(s*.25,y,.41);knee=(s*(.65+.045*abs(n-1.5)),y+spread,.39);foot=(s*(.91+.05*abs(n-1.5)),y+spread*1.60,.06)
   side='L' if s<0 else 'R';upper=f'Leg{n}{side}';lower=f'Tip{n}{side}'
   c.bone(upper,start,'Body');c.bone(lower,knee,upper)
   c.bar('Femur'+upper,start,knee,.107,upper,skin,segments=2);c.box('Knee'+upper,knee,(.14,.14,.14),lower,skin)
   c.bar('Tibia'+upper,knee,foot,.086,lower,skin,segments=2)
   for j in [0,1]:
    a=Vector(knee).lerp(Vector(foot),.23+j*.43);b=Vector(knee).lerp(Vector(foot),.31+j*.43)
    c.bar('LegBand'+str((s,n,j)),a,b,.095,lower,trim)
  c.box('FangRoot'+str(s),(s*.15,.67,.31),(.12,.20,.13),'Jaw','horn' if skitter else 'bone')
  c.bar('FangTip'+str(s),(s*.15,.74,.27),(s*.10,.80,.14),.07,'Jaw','horn' if skitter else 'bone')
 for n,(x,z) in enumerate([(-.14,.39),(0,.39),(.14,.39),(-.08,.47),(.08,.47)]):
  c.box('Eye'+str(n),(x,.659,z),(.075,.022,.066),'Head','greeneye' if skitter else 'redeye',bevel=.003)
 c.features=['eight fully segmented legs','raised shell bands','five clustered glowing eyes','two-part mandibles','alternating diagonal gait','bite anticipation'];return c

def humanoid_rig(c,height,hip,shoulder,armx,legx):
 c.bones['Body']=((0,0,height*.52),'Root');c.bone('Head',(0,0,height*.80),'Body');c.bone('Tail',(0,-.1,height*.5),'Body')
 for s,side in [(-1,'L'),(1,'R')]:
  c.bone('Leg'+side,(s*legx,0,hip),'Body');c.bone('Knee'+side,(s*legx,0,hip*.48),'Leg'+side)
  c.bone('Arm'+side,(s*armx,0,shoulder),'Body');c.bone('Hand'+side,(s*armx,0,shoulder*.68),'Arm'+side)

def undead(ident):
 c=Creature(ident,'biped',1536);humanoid_rig(c,1.83,.77,1.38,.34,.15)
 bone=ident=='skeleton';husk=ident=='husk';skin='bone' if bone else 'deadskin' if husk else 'zombiegreen'
 c.box('Head',(0,.02,1.59),(.43,.42,.44),'Head',skin,segments=2)
 if bone:
  for s in [-1,1]:c.box('EyeSocket'+str(s),(s*.12,.237,1.63),(.12,.015,.10),'Head','black',bevel=.002)
  c.box('NoseSocket',(0,.244,1.53),(.07,.018,.067),'Head','black');c.box('Jaw',(0,.07,1.375),(.33,.35,.12),'Head','bone')
  for n in range(5):c.box('Tooth'+str(n),((n-2)*.06,.255,1.40),(.036,.02,.05),'Head','bonedark',bevel=.001)
  c.box('Spine',(0,-.025,1.085),(.095,.10,.56),'Body','bonedark')
  for n in range(4):
   for s in [-1,1]:c.box('Rib'+str((n,s)),(s*.125,.053,1.31-n*.105),(.20,.18,.055),'Body','bone')
  c.box('Pelvis',(0,0,.81),(.32,.22,.17),'Body','bone');c.box('Sternum',(0,.16,1.13),(.055,.037,.43),'Body','bone')
 else:
  c.box('Torso',(0,0,1.11),(.49,.29,.61),'Body','rags' if husk else 'shirt',segments=2)
  c.eyes_front(.243,1.64,.125,.083,.065,pupil='cyan' if husk else 'black',white=False)
  c.box('Mouth',(0,.244,1.50),(.17,.018,.041),'Head','black')
  c.box('Waist',(0,0,.82),(.43,.27,.17),'Body','raglight' if husk else 'pants')
  if husk:
   for s in [-1,1]:
    c.box('HoodSide'+str(s),(s*.25,-.055,1.61),(.13,.49,.48),'Head','rags')
    c.box('CloakShoulder'+str(s),(s*.30,-.05,1.38),(.25,.34,.20),'Body','rags')
   c.box('HoodTop',(0,-.06,1.86),(.59,.51,.11),'Head','rags')
   for n in range(5):c.box('TornCloak'+str(n),((n-2)*.09,-.20,.98-n%2*.08),(.077,.10,.53-n%3*.08),'Body','rags')
   for n in range(3):c.box('RagHem'+str(n),((n-1)*.125,.13,.77),(.10,.055,.24+n%2*.10),'Body','raglight',rotation=(0,(n-1)*.18,0))
  else:
   for s in [-1,1]:c.box('Sleeve'+str(s),(s*.335,0,1.34),(.19,.30,.20),'ArmL' if s<0 else 'ArmR','shirt')
   for n in range(3):c.box('ShirtHem'+str(n),((n-1)*.135,.16,.825),(.13,.055,.095+n%2*.055),'Body','shirt')
 for s,side in [(-1,'L'),(1,'R')]:
  upper,lower,arm,hand='Leg'+side,'Knee'+side,'Arm'+side,'Hand'+side
  legskin=skin if bone else 'rags' if husk else 'pants';lw=.09 if bone else .19
  c.box('Thigh'+side,(s*.15,0,.58),(lw,.17,.35),upper,legskin)
  c.box('Knee'+side,(s*.15,.01,.365),(lw*1.16,.18,.10),lower,'bonedark' if bone else skin)
  c.box('Shin'+side,(s*.15,0,.205),(lw,.16,.25),lower,legskin)
  c.box('Foot'+side,(s*.15,.075,.052),(lw*1.6,.32,.105),lower,skin)
  c.box('UpperArm'+side,(s*.34,0,1.235),(.09 if bone else .14,.17,.29),arm,skin)
  c.box('Elbow'+side,(s*.34,0,.995),(.11,.18,.10),hand,'bonedark' if bone else skin)
  c.box('Forearm'+side,(s*.34,.03,.857),(.085 if bone else .14,.16,.20),hand,skin)
  c.box('Palm'+side,(s*.34,.06,.718),(.14,.15,.13),hand,skin)
  for n in range(3):c.box('Finger'+side+str(n),(s*.34+(n-1)*.045,.11,.655),(.035,.10,.095),hand,skin)
  if husk:
   for n in range(3):c.box('Wrap'+side+str(n),(s*.34,.029,1.00-n*.09),(.153,.19,.042),hand,'bandage')
 if bone:
  # Bow is bound to the left hand; the right hand draws during the bolt clip.
  x=-.46;y=.24
  points=[(x,y,.38),(x-.14,y,.63),(x-.19,y,.92),(x-.14,y,1.18),(x,y,1.43)]
  for n in range(4):c.bar('BowLimb'+str(n),points[n],points[n+1],.065,'HandL','bow')
  c.bar('BowString',points[0],points[-1],.009,'HandL','string')
  c.box('ArrowGrip',(x,y,.91),(.10,.08,.15),'HandL','bandage')
 c.features=['exposed skull sockets and teeth','four paired ribs and spine','thin separated bone limbs','articulated bow and string','bolt anticipation'] if bone else ['hood and torn cloak','exposed pale face and cold eyes','bandaged forearms','jointed fingers','melee anticipation'] if husk else ['green square head and hands','torn teal shirt','purple trousers','separate fingers','melee windup/strike/recovery']
 return c

def creeper():
 c=Creature('creeper','quadruped');c.bones['Body']=((0,0,.84),'Root');c.bone('Head',(0,0,1.33),'Body');c.bone('Tail',(0,-.1,.7),'Body')
 c.box('Torso',(0,0,.91),(.38,.36,.89),'Body','creepgreen',segments=2);c.box('Head',(0,0,1.56),(.57,.52,.52),'Head','creepgreen',segments=2)
 for s,side in [(-1,'L'),(1,'R')]:
  for front,y in [('Front',.18),('Rear',-.18)]:
   leg='Leg'+front+side;knee='Knee'+front+side;c.bone(leg,(s*.18,y,.45),'Body');c.bone(knee,(s*.18,y,.20),leg)
   c.box(leg,(s*.18,y,.30),(.22,.23,.30),leg,'creepgreen');c.box(knee,(s*.18,y,.14),(.23,.29,.26),knee,'creepgreen');c.box('Foot'+knee,(s*.18,y,.04),(.235,.30,.08),knee,'creepdark')
  c.box('Eye'+str(s),(s*.16,.27,1.64),(.125,.018,.115),'Head','black',bevel=.001)
 c.box('Nose',(0,.27,1.535),(.11,.018,.105),'Head','black',bevel=.001)
 c.box('MouthTop',(0,.27,1.465),(.22,.018,.070),'Head','black',bevel=.001)
 for s in [-1,1]:c.box('MouthDrop'+str(s),(s*.089,.27,1.406),(.061,.018,.11),'Head','black',bevel=.001)
 c.features=['four feet and no arms','mottled green pixel skin','recognizable recessed square face','body tremor during explosion anticipation'];return c

def frame(c,name,center,width,height,thick,depth,bone,style):
 x,y,z=center
 for n,(pos,dims) in enumerate([((x-width/2,y,z),(thick,depth,height)),((x+width/2,y,z),(thick,depth,height)),((x,y,z-height/2),(width,depth,thick)),((x,y,z+height/2),(width,depth,thick))]):
  c.box(name+str(n),pos,dims,bone,style)

def optic(c,name,center,radius,bone,color):
 x,y,z=center
 for n in range(8):
  a=n*math.tau/8
  c.box(name+'Rim'+str(n),(x+math.sin(a)*radius,y,z+math.cos(a)*radius),(radius*.77,.07,radius*.25),bone,'silvermetal',rotation=(0,a,0),bevel=.007)
 c.box(name+'Lens',center,(radius*1.5,.043,radius*1.5),bone,color,rotation=(0,math.pi/4,0),bevel=.012)
 c.box(name+'Center',(x,y+.032,z),(radius*.61,.023,radius*.61),bone,'glyph' if color=='amber' else 'cyan',bevel=.009)

def robotic_limbs(c,scale,hip,shoulder,legx,armx,skin,trim,thin=False):
 for s,side in [(-1,'L'),(1,'R')]:
  x=s*legx;ax=s*armx;upper='Leg'+side;lower='Knee'+side;arm='Arm'+side;hand='Hand'+side;knee=hip*.48
  lw=scale*(.14 if thin else .25);depth=scale*(.16 if thin else .28)
  c.box('ThighCore'+side,(x,0,hip*.76),(lw*.74,depth*.7,hip*.37),upper,'dark')
  c.box('ThighPlate'+side,(x,.04*scale,hip*.76),(lw,depth,hip*.37),upper,skin,segments=2)
  c.box('ThighBorder'+side,(x,.055*scale,hip*.60),(lw*1.08,depth*1.04,.046*scale),upper,trim)
  c.box('KneeJoint'+side,(x,0,knee),(lw*.80,depth*.75,.13*scale),lower,'dark',segments=2)
  c.box('Kneecap'+side,(x,depth*.55,knee),(lw*.72,.11*scale,.12*scale),lower,skin)
  c.box('ShinCore'+side,(x,0,hip*.25),(lw*.65,depth*.64,hip*.34),lower,'dark')
  c.box('ShinPlate'+side,(x,.035*scale,hip*.25),(lw*.88,depth*.90,hip*.33),lower,skin,segments=2)
  c.box('ShinTrim'+side,(x,.04*scale,hip*.13),(lw*.94,depth,.043*scale),lower,trim)
  for n in range(3 if not thin else 2):
   c.box('Toe'+side+str(n),(x+(n-(1 if not thin else .5))*lw*.34,.08*scale,.053*scale),(lw*.32,depth*1.5,.106*scale),lower,skin,segments=2)
  # Visible paired piston rods belong to the moving limb segment.
  c.bar('ThighPiston'+side,(x+s*lw*.45,-depth*.35,hip*.91),(x+s*lw*.45,-depth*.35,hip*.59),.037*scale,upper,'silvermetal')
  c.box('UpperArm'+side,(ax,0,shoulder*.83),(.17*scale if thin else .24*scale,.22*scale,shoulder*.23),arm,skin,segments=2)
  c.box('ElbowJoint'+side,(ax,0,shoulder*.675),(.13*scale,.17*scale,.08*scale),hand,'dark')
  c.box('Forearm'+side,(ax,.012*scale,shoulder*.58),(.19*scale if thin else .29*scale,.24*scale,shoulder*.17),hand,skin,segments=2)
  c.box('Wrist'+side,(ax,.013*scale,shoulder*.49),(.20*scale if thin else .31*scale,.26*scale,.055*scale),hand,trim)
  c.box('Palm'+side,(ax,.035*scale,shoulder*.44),(.18*scale,.19*scale,.13*scale),hand,'dark')
  for n in range(3):
   c.box('Knuckle'+side+str(n),(ax+(n-1)*.065*scale,.115*scale,shoulder*.445),(.055*scale,.11*scale,.12*scale),hand,skin)
   c.box('Finger'+side+str(n),(ax+(n-1)*.065*scale,.075*scale,shoulder*.40),(.054*scale,.15*scale,.065*scale),hand,skin)

def cannon(c,name,x,y,z,scale,bone,skin,trim,color,long=False):
 # Stepped square/octagonal barrel integrated into the forearm. It points down
 # in the bind pose and rotates forward in the bolt anticipation clip.
 for n in range(4 if long else 3):
  radius=(.16 if long else .19)*scale;centerz=z-n*.12*scale
  c.box(name+'Sleeve'+str(n),(x,y,centerz),(radius*2,radius*2,.14*scale),bone,skin,segments=2)
  c.box(name+'Ring'+str(n),(x,y,centerz+.062*scale),(radius*2.08,radius*2.08,.035*scale),bone,trim)
 c.box(name+'Muzzle',(x,y,z-(.45 if long else .34)*scale),(.26*scale,.26*scale,.085*scale),bone,'dark',segments=2)
 c.box(name+'Bore',(x,y,z-(.50 if long else .385)*scale),(.15*scale,.15*scale,.014*scale),bone,color)
 for s in [-1,1]:c.box(name+'EnergyRail'+str(s),(x+s*.17*scale,y+.12*scale,z-.15*scale),(.032*scale,.04*scale,.38*scale if long else .28*scale),bone,color)

def machine(ident):
 scale={'sentinel':1,'mine_warden':1.2,'furnace_warden':1.45,'observatory_warden':1.30}[ident]
 thin=ident=='observatory_warden';furnace=ident=='furnace_warden';mine=ident=='mine_warden'
 height=scale*1.95;hip=scale*(1.0 if thin else .83);shoulder=scale*(1.58 if thin else 1.50)
 armx=scale*(.37 if thin else .52 if furnace else .43);legx=scale*(.135 if thin else .19)
 c=Creature(ident,'biped',1536);humanoid_rig(c,height,hip,shoulder,armx,legx)
 skin='coldstone' if thin else 'iron' if furnace else 'stone' if mine else 'patina'
 trim='silvermetal' if thin else 'copper' if furnace else 'brass' if mine else 'bronze';glow='cyan' if thin else 'molten' if furnace else 'amber'
 robotic_limbs(c,scale,hip,shoulder,legx,armx,skin,trim,thin)
 c.box('Waist',(0,0,hip+.035*scale),(.45*scale,.28*scale,.19*scale),'Body','dark')
 c.box('Belt',(0,.01*scale,hip+.015*scale),(.51*scale,.34*scale,.08*scale),'Body',trim)
 c.box('Buckle',(0,.205*scale,hip),(.11*scale,.045*scale,.13*scale),'Body',trim)
 c.box('PelvisPlate',(0,.20*scale,hip-.09*scale),(.20*scale,.055*scale,.16*scale),'Body',skin)
 chestz=(hip+shoulder)*.5+.06*scale;chesth=shoulder-hip-.04*scale
 width=(.45 if thin else .83 if furnace else .62)*scale;depth=(.30 if thin else .49 if furnace else .36)*scale
 c.box('TorsoCore',(0,0,chestz),(width*.87,depth*.89,chesth),'Body','dark')
 c.box('BackPlate',(0,-depth*.46,chestz),(width,.12*scale,chesth*1.04),'Body',skin,segments=2)
 if furnace:
  # Furnace body: orange interior BEHIND a real vertical metal grille.
  c.box('FurnaceInterior',(0,depth*.42,chestz),(.59*scale,.045*scale,chesth*.78),'Body','molten')
  frame(c,'FurnaceFrame',(0,depth*.53,chestz),.66*scale,chesth*.87,.075*scale,.11*scale,'Body',trim)
  for n in range(5):c.box('VerticalGrille'+str(n),((n-2)*.105*scale,depth*.60,chestz),(.04*scale,.065*scale,chesth*.88),'Body','iron')
  for s in [-1,1]:
   c.box('HeatShield'+str(s),(s*width*.52,0,chestz),(.10*scale,depth,chesth*1.10),'Body','iron',segments=2)
   for n in range(2):
    x=s*(.26+n*.20)*scale;z=shoulder+.15*scale+n*.035*scale
    c.box('Chimney'+str((s,n)),(x,-.16*scale,z),(.13*scale,.16*scale,.39*scale),'Body','iron')
    c.box('ChimneyCap'+str((s,n)),(x,-.16*scale,z+.20*scale),(.17*scale,.20*scale,.035*scale),'Body',trim)
    c.box('ChimneyVent'+str((s,n)),(x,-.07*scale,z),(.057*scale,.018*scale,.24*scale),'Body','molten')
   c.box('HeavyShoulder'+str(s),(s*armx,.012*scale,shoulder),(.39*scale,.48*scale,.33*scale),'ArmL' if s<0 else 'ArmR','iron',segments=2)
   c.box('ShoulderShield'+str(s),(s*armx,.26*scale,shoulder),(.39*scale,.085*scale,.26*scale),'ArmL' if s<0 else 'ArmR',trim)
   c.box('ForearmHeatShield'+str(s),(s*armx,.17*scale,shoulder*.575),(.30*scale,.13*scale,.34*scale),'HandL' if s<0 else 'HandR',trim)
   c.box('ForearmVent'+str(s),(s*armx,.24*scale,shoulder*.575),(.072*scale,.024*scale,.25*scale),'HandL' if s<0 else 'HandR','molten')
  c.box('Hood',(0,.005*scale,shoulder+.19*scale),(.33*scale,.35*scale,.23*scale),'Head','iron',segments=2)
  c.eyes_front(.19*scale,shoulder+.16*scale,.075*scale,.077*scale,.038*scale,pupil='molten',white=False)
  c.box('HoodBrow',(0,.198*scale,shoulder+.24*scale),(.37*scale,.08*scale,.059*scale),'Head','iron')
  c.features=['wide squat iron furnace body','orange core behind five physical grille bars','four chimney stacks','heavy copper heat shields','glowing forearm vents','hood-like slit-eye head','charge clip']
 elif thin:
  # Scout body is narrow with plate capes and a U-shaped antenna, not a golem.
  c.box('ChestSpine',(0,.19*scale,chestz),(.095*scale,.07*scale,chesth*.92),'Body','silvermetal')
  for s in [-1,1]:
   for n in range(3):c.box('ChestBlade'+str((s,n)),(s*(.10+n*.032)*scale,.18*scale,chestz+.14*scale-n*.11*scale),(.10*scale,.06*scale,.14*scale),'Body','coldstone',rotation=(0,s*.16,0))
   for n in range(3):c.box('ShoulderCape'+str((s,n)),(s*(armx+.075*scale+n*.09*scale),-.02*scale,shoulder+.09*scale-n*.12*scale),(.31*scale,.44*scale,.10*scale),'ArmL' if s<0 else 'ArmR','coldstone',rotation=(0,-s*.35,0))
   c.bar('AntennaSide'+str(s),(s*.34*scale,-.13*scale,shoulder+.13*scale),(s*.37*scale,-.13*scale,height+.40*scale),.055*scale,'Head','silvermetal')
   c.bar('AntennaTip'+str(s),(s*.37*scale,-.13*scale,height+.40*scale),(s*.21*scale,-.13*scale,height+.40*scale),.055*scale,'Head','coldstone')
   c.box('AntennaLight'+str(s),(s*.21*scale,-.13*scale,height+.40*scale),(.065*scale,.064*scale,.061*scale),'Head','cyan')
   c.box('ShinEnergy'+str(s),(s*legx,.165*scale,hip*.25),(.021*scale,.017*scale,hip*.25),'KneeL' if s<0 else 'KneeR','cyan',bevel=.001)
  c.box('OpticHead',(0,.015*scale,shoulder+.24*scale),(.29*scale,.28*scale,.31*scale),'Head','coldstone',segments=2)
  optic(c,'Cyclops',(0,.175*scale,shoulder+.24*scale),.13*scale,'Head','cyan')
  c.box('HeadTopSensor',(0,0,shoulder+.46*scale),(.17*scale,.19*scale,.074*scale),'Head','silvermetal')
  cannon(c,'TelescopeEmitter',armx,.018*scale,shoulder*.60,scale,'HandR','coldstone','silvermetal','cyan',long=True)
  c.features=['tall narrow scout proportions','layered angular shoulder capes','single framed cyan optic','open U-shaped sensing antenna','long telescopic integrated emitter','slender piston legs with energy seams','bolt aim clip']
 else:
  c.box('ChestPlate',(0,.20*scale,chestz),(width,.105*scale,chesth*.85),'Body','stone_light' if not mine else 'stone',segments=2)
  for s in [-1,1]:
   c.box('ChestTrim'+str(s),(s*width*.40,.264*scale,chestz),(.065*scale,.023*scale,chesth*.76),'Body',trim)
   c.box('Shoulder'+str(s),(s*armx,0,shoulder),(.34*scale,.42*scale,.31*scale),'ArmL' if s<0 else 'ArmR',skin,segments=2)
   frame(c,'ShoulderRim'+str(s),(s*armx,.231*scale,shoulder),.30*scale,.27*scale,.038*scale,.025*scale,'ArmL' if s<0 else 'ArmR',trim)
   c.box('ForearmBadge'+str(s),(s*armx,.17*scale,shoulder*.58),(.14*scale,.05*scale,.20*scale),'HandL' if s<0 else 'HandR',trim)
  c.box('Head',(0,0,shoulder+.26*scale),(.33*scale,.31*scale,.34*scale),'Head',skin,segments=2)
  if mine:
   c.box('Helmet',(0,-.005*scale,shoulder+.45*scale),(.46*scale,.43*scale,.11*scale),'Head','brass',segments=2)
   c.box('HelmetCrest',(0,-.04*scale,shoulder+.52*scale),(.12*scale,.34*scale,.07*scale),'Head','stone')
   optic(c,'Headlamp',(0,.235*scale,shoulder+.42*scale),.085*scale,'Head','amber')
   c.eyes_front(.167*scale,shoulder+.27*scale,.08*scale,.055*scale,.045*scale,pupil='cyan',white=False)
   c.box('AsymmetricShoulder',(-armx-.07*scale,.016*scale,shoulder+.03*scale),(.43*scale,.49*scale,.37*scale),'ArmL','stone',segments=2)
   frame(c,'LeftGuardRim',(-armx-.07*scale,.275*scale,shoulder+.03*scale),.36*scale,.31*scale,.046*scale,.026*scale,'ArmL','brass')
   cannon(c,'MiningBoltLauncher',armx,.018*scale,shoulder*.60,scale,'HandR','iron','brass','amber')
   c.features=['mining helmet and amber lamp','asymmetric broad left shoulder guard','weathered stone and brass plates','integrated stepped right-forearm bolt launcher','visible piston links','belt clamps','bolt clip']
  else:
   optic(c,'AmberEye',(0,.185*scale,shoulder+.26*scale),.095*scale,'Head','amber')
   c.box('HeadCopperBand',(0,0,shoulder+.43*scale),(.37*scale,.35*scale,.045*scale),'Head','bronze')
   cannon(c,'SentinelBoltArm',armx,.016*scale,shoulder*.58,scale,'HandR','stone','bronze','amber')
   c.features=['turquoise patina armor and bronze rims','single amber optic','stone chest plate','integrated forearm bolt weapon','segmented fingers and piston legs','bolt windup/strike/recovery']
 return c

def colossus():
 c=Creature('colossus','biped',2048);humanoid_rig(c,10.9,4.7,8.20,2.85,1.08)
 robotic_limbs(c,5.05,4.7,8.20,1.08,2.85,'stone','brass')
 c.box('WaistSpine',(0,0,5.10),(2.3,1.3,.92),'Body','dark',segments=2)
 c.box('Belt',(0,.03,4.90),(2.85,1.62,.37),'Body','brass')
 for s in [-1,1]:
  c.box('HipShield'+str(s),(s*.90,.79,4.47),(.76,.28,.84),'Body','stone',segments=2)
  frame(c,'HipShieldRim'+str(s),(s*.90,.95,4.47),.68,.73,.11,.07,'Body','brass')
  # Tiered shoulder pylons are the giant's defining architectural silhouette.
  for n in range(4):
   width=1.75-n*.28;z=8.42+n*.70
   c.box('PylonTier'+str((s,n)),(s*2.6,-.13,z),(width,1.82-n*.20,.87),'ArmL' if s<0 else 'ArmR','stone',segments=2)
   c.box('PylonCap'+str((s,n)),(s*2.6,-.13,z+.43),(width+.07,1.85-n*.20,.13),'ArmL' if s<0 else 'ArmR','brass')
   for side in [-1,1]:c.box('PylonRail'+str((s,n,side)),(s*2.6+side*(width/2-.12),.76-n*.10,z),(.12,.07,.65),'ArmL' if s<0 else 'ArmR','brass')
  c.box('PylonCrown'+str(s),(s*2.6,-.13,11.0),(.39,.89,.60),'ArmL' if s<0 else 'ArmR','brass')
  c.box('ShoulderFoundation'+str(s),(s*2.5,.03,7.86),(2.1,2.0,.69),'ArmL' if s<0 else 'ArmR','stone',segments=2)
  c.box('ForearmShell'+str(s),(s*2.85,.08,4.92),(1.80,1.78,1.66),'HandL' if s<0 else 'HandR','stone',segments=2)
  for n in range(4):
   z=4.27+n*.43
   c.box('ForearmBrace'+str((s,n)),(s*2.85,.08,z),(1.86,1.84,.11),'HandL' if s<0 else 'HandR','brass')
  c.box('ForearmCenterRail'+str(s),(s*2.85,.995,4.94),(.19,.09,1.60),'HandL' if s<0 else 'HandR','brass')
  for n in range(3):
   x=s*2.85+(n-1)*.43
   c.box('MassiveKnuckle'+str((s,n)),(x,.74,3.69),(.37,.59,.69),'HandL' if s<0 else 'HandR','stone',segments=2)
   c.box('FoldedFinger'+str((s,n)),(x,.44,3.27),(.36,.88,.34),'HandL' if s<0 else 'HandR','stone',segments=2)
  c.box('Thumb'+str(s),(s*2.85-s*.78,.23,3.63),(.37,.73,.58),'HandL' if s<0 else 'HandR','stone',rotation=(0,s*.24,0),segments=2)
  c.box('LegTowerPlate'+str(s),(s*1.08,.74,2.95),(.74,.26,1.36),'LegL' if s<0 else 'LegR','stone',segments=2)
  frame(c,'ThighFrame'+str(s),(s*1.08,.90,2.95),.68,1.21,.10,.07,'LegL' if s<0 else 'LegR','brass')
  c.box('LowerLegPlate'+str(s),(s*1.08,.76,1.22),(.67,.26,1.0),'KneeL' if s<0 else 'KneeR','stone',segments=2)
  c.box('ShinBrace'+str(s),(s*1.08,.918,1.22),(.16,.06,.96),'KneeL' if s<0 else 'KneeR','brass')
 c.box('BackSpine',(0,-.64,6.87),(2.89,.45,2.58),'Body','stone',segments=2)
 for s in [-1,1]:c.box('ChestPillar'+str(s),(s*1.35,.02,6.83),(.70,1.81,2.29),'Body','stone',segments=2)
 c.box('ChestLintel',(0,.04,8.02),(3.39,1.86,.46),'Body','stone',segments=2)
 c.box('ChestBase',(0,.04,5.73),(3.25,1.84,.43),'Body','stone',segments=2)
 # Two concentric framed layers around an open, deep cross-shaped molten core.
 frame(c,'OuterCoreFrame',(0,1.015,6.85),2.43,2.12,.23,.16,'Body','brass')
 frame(c,'InnerCoreFrame',(0,.96,6.85),1.96,1.72,.13,.18,'Body','stone_light')
 c.box('CoreCavity',(0,.57,6.85),(1.92,.22,1.66),'Body','dark')
 c.box('MoltenCrossVertical',(0,.765,6.85),(.32,.28,1.37),'Body','molten',segments=2)
 c.box('MoltenCrossHorizontal',(0,.785,6.85),(1.50,.26,.26),'Body','molten',segments=2)
 c.box('CoreCrystal',(0,.96,6.85),(.45,.31,.45),'Body','glyph',rotation=(0,math.pi/4,0),segments=2)
 for s in [-1,1]:
  c.bar('CoreRadialBrace'+str(s),(s*.76,.80,6.30),(s*.20,.93,6.73),.12,'Body','brass')
  c.bar('CoreUpperBrace'+str(s),(s*.76,.80,7.42),(s*.20,.93,6.97),.12,'Body','brass')
 c.box('Neck',(0,-.03,8.25),(.83,.92,.34),'Head','dark')
 c.box('Helmet',(0,0,8.73),(1.05,1.02,.76),'Head','stone',segments=2)
 c.box('FacePlate',(0,.57,8.71),(1.06,.13,.40),'Head','dark')
 c.eyes_front(.655,8.79,.30,.19,.14,pupil='molten',white=False)
 c.box('Brow',(0,.59,8.99),(1.17,.17,.16),'Head','brass')
 c.box('CrownCenter',(0,-.10,9.60),(.35,.45,1.02),'Head','brass')
 for s in [-1,1]:c.box('CrownProng'+str(s),(s*.41,-.08,9.27),(.23,.40,.53),'Head','brass')
 c.features=['true eleven-block architectural scale','four-tier twin shoulder pylons','deep concentric chest opening','raised molten cross and diamond crystal','stacked piston forearms','large folded fingers and thumbs','tiered plated legs and feet','slam anticipation/contact/recovery'];return c

def make(ident):
 if ident in {'deer','boar','sheep','horse','wolf','cat','fox'}:return mammal(ident)
 if ident=='chicken':return chicken()
 if ident=='armadillo':return armadillo()
 if ident in {'cod','squid','turtle'}:return aquatic(ident)
 if ident in {'skitter','spider'}:return arthropod(ident)
 if ident in {'husk','zombie','skeleton'}:return undead(ident)
 if ident=='creeper':return creeper()
 if ident in {'sentinel','mine_warden','furnace_warden','observatory_warden'}:return machine(ident)
 if ident=='colossus':return colossus()
 raise ValueError(ident)

def write_manifest():
 records=[json.loads(p.read_text(encoding='utf-8')) for p in sorted(OUT.glob('*/model-record.json'))]
 (OUT/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
 return records

if __name__=='__main__':
 ids=ARGS.ids.split(',') if ARGS.ids else sum(GROUPS.values(),[])
 for ident in ids:
  path=OUT/ident/'model-record.json'
  if not ARGS.force and path.exists() and json.loads(path.read_text(encoding='utf-8')).get('family') and (OUT/ident/ident).with_suffix('.glb').exists() and (ARGS.still_only or len(list((OUT/ident/'motion').glob('*.png')))==24):
   print('CREATURE_REUSED '+ident,flush=True);continue
  print('CREATURE_START '+ident,flush=True)
  make(ident).finish();write_manifest()
 print('COLLECTION_BATCH_COMPLETE '+json.dumps({'ids':ids,'records':len(write_manifest())}),flush=True)
