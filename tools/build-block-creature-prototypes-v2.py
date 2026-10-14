"""Reference-led second revision: articulated armor, faces, horns, ears and hooves.

This authors new meshes locally. It does not decimate the old assets, modify
the first revision, spend service credits, or install anything in the game.
"""
import argparse
import importlib.util
import json
import math
import pathlib
import random
import sys
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix, Euler

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('prototype_base',ROOT/'tools/build-block-creature-prototypes.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
OUT=ROOT/'docs/previews/block-creature-prototypes-v2-2026-10-13'
OUT.mkdir(parents=True,exist_ok=True);base.OUT=OUT
parser=argparse.ArgumentParser();parser.add_argument('--ids',default='cow,pig,guardian');parser.add_argument('--still-only',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []) if __name__=='__main__' else argparse.Namespace(ids='cow,pig,guardian',still_only=False)

PALETTE={
 'cream':(.94,.88,.75),'brown':(.40,.23,.10),'horn':(.83,.72,.46),
 'pig':(.94,.60,.60),'nose':(.93,.52,.52),'earpink':(.81,.43,.43),
 'white':(.98,.97,.90),'black':(.04,.045,.047),'hoof':(.20,.17,.15),'pighoof':(.35,.24,.22),
 'stone':(.25,.27,.27),'stone_light':(.33,.34,.34),'dark':(.11,.13,.14),
 'bronze':(.61,.43,.20),'amber':(1,.50,.04),'glyph':(1,.89,.37),
 'udder':(.89,.49,.49),
}

class DetailedCreature(base.Creature):
 def __init__(self,ident):
  super().__init__(ident,1536 if ident=='guardian' else 768)
  self.pixels=np.zeros((self.size,self.size,4),dtype=np.float32);self.pixels[:,:,3]=1
  self.glow=np.zeros_like(self.pixels);self.glow[:,:,3]=1
  self.features=[]

 def paint(self,style,p,name):
  x,y,z=p;rgb=np.array(PALETTE[style])
  # Coherent spots projected in model space, not a repeated checker per face.
  if style=='cream':
   if name=='Head':
    stripe=.065+.055*max(0,(z-1.1)/.6)
    brown=abs(x)>stripe
   elif name.startswith('Tail'):
    brown=.80<z<1.09
   else:
    spot1=((y-.48)/.36)**2+((z-1.16)/.43)**2
    spot2=((y+.72)/.31)**2+((z-1.27)/.39)**2
    spot3=((y-.38)/.31)**2+((z-.55)/.31)**2
    edge=.08*math.sin(y*24)+.06*math.sin(z*21)
    brown=min(spot1,spot2,spot3)<1+edge
   if brown:rgb=np.array(PALETTE['brown'])
  ix,iy,iz=(math.floor(v*25) for v in (x,y,z))
  rng=random.Random(f'{self.ident}:{style}:{ix}:{iy}:{iz}')
  shade=rng.choice([.93,.96,1,1,1,1.03,1.07])
  if style in {'stone','stone_light'}:
   shade=rng.choice([.76,.87,.94,1,1,1.08,1.17])
  if style=='pig':
   # Sparse broad pink patches leave the silhouette clean.
   patch=random.Random(f'pigpatch:{ix//4}:{iy//4}:{iz//4}').random()
   shade=rng.choice([.98,1,1,1,1.02])*(.95 if patch<.22 else 1)
  return tuple(np.clip(rgb*shade,0,1))

 def tile_face(self,style,name,normal,local_vertices,center,rotation):
  index=self.tiles;self.tiles+=1
  size=16;columns=self.size//size
  assert self.tiles<=columns*columns,'Texture atlas overflow'
  tx,ty=(index%columns)*size,(index//columns)*size
  axis=max(range(3),key=lambda a:abs(normal[a]))
  a,b=([1,2] if axis==0 else [0,2] if axis==1 else [0,1])
  lo=[min(v[k] for v in local_vertices) for k in range(3)]
  hi=[max(v[k] for v in local_vertices) for k in range(3)]
  fixed=sum(v[axis] for v in local_vertices)/len(local_vertices)
  for py in range(14):
   for px in range(14):
    p=Vector((0,0,0));p[axis]=fixed
    p[a]=lo[a]+(hi[a]-lo[a])*(px+.5)/14
    p[b]=lo[b]+(hi[b]-lo[b])*(py+.5)/14
    world=rotation@p+Vector(center)
    color=self.paint(style,world,name)
    self.pixels[ty+py+1,tx+px+1,:3]=color
    if style in {'amber','glyph'}:self.glow[ty+py+1,tx+px+1,:3]=color
  for arr in [self.pixels,self.glow]:
   arr[ty,tx+1:tx+15]=arr[ty+1,tx+1:tx+15]
   arr[ty+15,tx+1:tx+15]=arr[ty+14,tx+1:tx+15]
   arr[ty:ty+16,tx]=arr[ty:ty+16,tx+1]
   arr[ty:ty+16,tx+15]=arr[ty:ty+16,tx+14]
  uv=[]
  for v in local_vertices:
   u=(v[a]-lo[a])/max(hi[a]-lo[a],1e-6)
   vv=(v[b]-lo[b])/max(hi[b]-lo[b],1e-6)
   uv.append(((tx+1+u*14)/self.size,(ty+1+vv*14)/self.size))
  return uv

 def box(self,name,center,dimensions,bone,style,rotation=(0,0,0),bevel=None,segments=1):
  if isinstance(rotation,(int,float)):rotation=(0,rotation,0)
  rotation=Euler(rotation,'XYZ').to_matrix()
  dx,dy,dz=(d/2 for d in dimensions)
  corners=[(-dx,-dy,-dz),(dx,-dy,-dz),(dx,dy,-dz),(-dx,dy,-dz),(-dx,-dy,dz),(dx,-dy,dz),(dx,dy,dz),(-dx,dy,dz)]
  faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
  data=bpy.data.meshes.new('_part');data.from_pydata(corners,[],faces);data.update()
  bm=bmesh.new();bm.from_mesh(data)
  width=min(dimensions)*(.075 if self.ident=='guardian' else .032) if bevel is None else bevel
  if width>0:
   bmesh.ops.bevel(bm,geom=list(bm.edges),offset=width,segments=segments,affect='EDGES',clamp_overlap=True)
  bm.normal_update();bm.to_mesh(data);bm.free();data.update()
  for p in data.polygons:
   local=[data.vertices[i].co.copy() for i in p.vertices]
   first=len(self.vertices)
   self.vertices.extend([tuple(rotation@v+Vector(center)) for v in local])
   self.weights.extend([bone]*len(local))
   self.faces.append(tuple(first+i for i in range(len(local))))
   self.uvs.extend(self.tile_face(style,name,p.normal,local,center,rotation))
  bpy.data.meshes.remove(data)
  self.blocks.append(dict(name=name,bone=bone,center=center,dimensions=dimensions,edgeBevel=width,segments=segments))

 def decorate_material(self,material,shader,texture):
  if self.ident!='guardian':return
  img=bpy.data.images.new('guardian_emissive_mask',width=self.size,height=self.size,alpha=False)
  img.pixels.foreach_set(self.glow.ravel())
  img.filepath_raw=str(self.folder/'guardian-emission.png');img.file_format='PNG';img.save();img.pack()
  node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;node.interpolation='Closest'
  material.node_tree.links.new(node.outputs['Color'],shader.inputs['Emission Color'])
  shader.inputs['Emission Strength'].default_value=1.8

 def finish(self):
  self.pixels=self.pixels.ravel()
  record=super().finish()
  record['revision']=2;record['designFeatures']=self.features
  record['height']=max(v[2] for v in self.vertices)
  record['textureSize']=[self.size,self.size]
  (self.folder/'model-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
  return record

 def stage(self):
  super().stage()
  scene=bpy.context.scene
  world=scene.world
  nodes=world.node_tree.nodes;nodes.clear();links=world.node_tree.links
  output=nodes.new('ShaderNodeOutputWorld');mix=nodes.new('ShaderNodeMixShader');lightpath=nodes.new('ShaderNodeLightPath')
  ambient=nodes.new('ShaderNodeBackground');ambient.inputs['Color'].default_value=(.75,.75,.75,1);ambient.inputs['Strength'].default_value=.70
  sky=nodes.new('ShaderNodeBackground');sky.inputs['Color'].default_value=(.22,.51,.92,1);sky.inputs['Strength'].default_value=1
  links.new(lightpath.outputs['Is Camera Ray'],mix.inputs[0]);links.new(ambient.outputs[0],mix.inputs[1]);links.new(sky.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs[0])
  sun=bpy.data.objects['Daylight'];sun.rotation_euler=(.65,-.35,-.45);sun.data.energy=2.1
  # Hand-authored procedural context texture, independent of creature export.
  img=bpy.data.images.new('Preview block grass pixels',width=32,height=32,alpha=False)
  pixels=[]
  for y in range(32):
   for x in range(32):
    shade=random.Random(f'grass:{x//2}:{y//2}').choice([.83,.94,1,1.07,1.14])
    pixels.extend([.30*shade,.49*shade,.13*shade,1])
  img.pixels.foreach_set(pixels);img.pack()
  mat=bpy.data.materials['Block grass'];node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;node.interpolation='Closest'
  mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
  # Low terraces make the environment visibly block based, rather than a studio plane.
  dirt=bpy.data.materials['Block soil']
  for x in range(-5,6):
   for y in range(-5,-2):
    bpy.ops.mesh.primitive_cube_add(size=2,location=(x,y,-.26+.5))
    o=bpy.context.object;o.name='PreviewTerrace';o.scale=(.5,.5,.25)
    o.data.materials.append(dirt);o.data.materials.append(mat)
    for p in o.data.polygons:p.material_index=1 if p.normal.z>.5 else 0
  # A few box trees, kept well behind the subject.
  def flat(name,color):
   m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
   m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
   m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=1
   return m
  trunk=flat('Preview tree bark',(.30,.20,.09));leaf=flat('Preview box leaves',(.15,.31,.12))
  for x,y in [(-3.3,-3.2),(3.5,-4.2)]:
   for center,dims,m in [((x,y,.9),(.28,.28,1.6),trunk),((x,y,1.85),(1.0,.95,.75),leaf)]:
    bpy.ops.mesh.primitive_cube_add(size=1,location=center);o=bpy.context.object;o.name='PreviewTree';o.dimensions=dims;o.data.materials.append(m)
  cam=scene.camera;cam.data.ortho_scale=3.1 if self.ident=='cow' else 2.3 if self.ident=='pig' else 3.9
  cam.data.type='PERSP';cam.data.sensor_fit='HORIZONTAL';cam.data.sensor_width=36
  self.target=Vector((0,.06,max(v[2] for v in self.vertices)*.48))
  scene.cycles.samples=24

 def render(self):
  scene=bpy.context.scene
  angles=[('three-quarter',(3.8,6.5,1.0)),('side',(7,0,.7)),('front',(0,7,.5))]
  for label,offset in angles:
   scene.camera.data.lens=Vector(offset).length*36/scene.camera.data.ortho_scale
   scene.camera.location=self.target+Vector(offset);base.point_camera(scene.camera,self.target)
   scene.render.filepath=str(self.folder/(self.ident+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
  scene.camera.location=self.target+Vector(angles[0][1]);base.point_camera(scene.camera,self.target)
  scene.camera.data.lens=Vector(angles[0][1]).length*36/scene.camera.data.ortho_scale
  # Save the source at the default camera and a clean neutral pose.
  bpy.ops.wm.save_as_mainfile(filepath=str(self.folder/(self.ident+'.blend')),compress=True)
  if args.still_only:return
  scene.render.resolution_x=420;scene.render.resolution_y=420;scene.cycles.samples=8
  motion=self.folder/'motion';motion.mkdir(exist_ok=True)
  self.rig.animation_data.action=bpy.data.actions['slam' if self.ident=='guardian' else 'walk']
  for i in range(24):
   frame=1+round(i*(32 if self.ident=='guardian' else 24)/23);scene.frame_set(frame)
   scene.render.filepath=str(motion/f'{i:02d}.png');bpy.ops.render.render(write_still=True)
  self.rig.animation_data.action=None;scene.frame_set(1)

def animal(ident):
 c=DetailedCreature(ident);pig=ident=='pig'
 c.bone('Root',(0,0,0));c.bone('Body',(0,0,.67 if pig else 1.02),'Root')
 c.bone('Head',(0,.43,.79 if pig else 1.27),'Body');c.bone('Tail',(0,-.59,.74 if pig else 1.32),'Body')
 hip,knee,xoff,fy,ry=(.44,.22,.28,.36,-.35) if pig else (.78,.38,.31,.40,-.48)
 skin='pig' if pig else 'cream'
 for suffix,x in [('L',-xoff),('R',xoff)]:
  for place,y in [('Front',fy),('Rear',ry)]:
   upper='Leg'+place+suffix;lower='Knee'+place+suffix
   c.bone(upper,(x,y,hip),'Body');c.bone(lower,(x,y,knee),upper)
   c.box(upper,(x,y,(hip+knee)/2),(.215,.24,hip-knee+.035),upper,skin)
   c.box('Joint'+place+suffix,(x,y,knee),(.197,.222,.065),lower,skin)
   c.box(lower,(x,y,(knee+.12)/2),(.198,.218,knee-.10),lower,skin)
   for n,ox in enumerate([-.056,.056]):
    c.box('ClovenHoof'+place+suffix+str(n),(x+ox,y+.012,.06),(.104,.255,.12),lower,'pighoof' if pig else 'hoof',bevel=.004)
 c.box('Torso',(0,-.1,.69) if pig else (0,-.13,1.08),(.76,1.18,.56) if pig else (.86,1.47,.72),'Body',skin,segments=2)
 if not pig:c.box('Chest',(0,.47,1.03),(.79,.31,.62),'Body',skin,segments=2)
 headcenter=(0,.62,.81) if pig else (0,.68,1.33)
 headsize=(.66,.57,.62) if pig else (.72,.58,.70)
 c.box('Head',headcenter,headsize,'Head',skin,segments=2)
 facey=headcenter[1]+headsize[1]/2
 eyex,eyez,eyew,eyeh=(.225,.865,.14,.13) if pig else (.225,1.385,.15,.17)
 for suffix,s in [('L',-1),('R',1)]:
  c.box('EyeWhite'+suffix,(s*eyex,facey+.008,eyez),(eyew,.018,eyeh),'Head','white',bevel=.001)
  c.box('Pupil'+suffix,(s*(eyex-.035),facey+.019,eyez-.012),(.071,.009,eyeh*.90),'Head','black',bevel=0)
  if not pig:c.box('UpperLid'+suffix,(s*eyex,facey+.006,eyez+eyeh/2+.011),(.157,.027,.023),'Head','brown',bevel=.002)
 snoutcenter=(0,.975,.69) if pig else (0,1.018,1.11)
 snoutsize=(.38,.15,.245) if pig else (.50,.21,.265)
 c.box('Snout',snoutcenter,snoutsize,'Head','nose',segments=2)
 c.box('LowerLip',(0,snoutcenter[1]-.015,snoutcenter[2]-snoutsize[2]/2-.018),(snoutsize[0]*.86,snoutsize[1]*.78,.055),'Head','earpink')
 nosey=snoutcenter[1]+snoutsize[1]/2
 for s in [-1,1]:
  c.box('Nostril'+str(s),(s*snoutsize[0]*.27,nosey+.002,snoutcenter[2]),(.064,.006,.068),'Head','black',bevel=.001)
  c.box('NostrilRim'+str(s),(s*snoutsize[0]*.27,nosey+.007,snoutcenter[2]+.038),(.071,.012,.012),'Head','nose',bevel=.001)
  if pig:
   earcenter=(s*.365,.46,1.035);rot=(.17,s*.32,s*.08)
   c.box('EarOuter'+str(s),earcenter,(.20,.16,.29),'Head','pig',rotation=rot)
   c.box('EarInner'+str(s),(s*.365,.551,1.02),(.137,.018,.177),'Head','earpink',rotation=rot,bevel=.003)
   c.box('EarFold'+str(s),(s*.365,.53,1.16),(.209,.11,.075),'Head','pig',rotation=(.4,s*.32,s*.08))
  else:
   c.box('EarOuter'+str(s),(s*.51,.60,1.49),(.30,.19,.25),'Head','brown',rotation=(0,s*.10,0))
   c.box('EarInner'+str(s),(s*.51,.703,1.478),(.235,.025,.165),'Head','earpink',rotation=(0,s*.10,0))
   c.box('HornElbow'+str(s),(s*.34,.56,1.70),(.14,.19,.19),'Head','horn',rotation=(0,s*.55,0),segments=2)
   c.box('HornTip'+str(s),(s*.40,.56,1.842),(.135,.175,.225),'Head','horn',bevel=.01,segments=2)
 if pig:
  for n,(pos,dim,rot) in enumerate([((0,-.77,.78),(.08,.23,.09),(0,0,0)),((.08,-.9,.85),(.20,.08,.08),(0,-.42,0)),((.15,-.92,.95),(.08,.09,.18),(0,.3,0)),((.09,-.92,1.02),(.15,.08,.08),(0,.12,0))]):
   c.box('CurledTail'+str(n),pos,dim,'Tail','pig',rotation=rot,bevel=.004)
  c.features=['larger reference head','front white eyes and black pupils','folded upright ears','layered snout and nostrils','four-segment curled tail','split hooves']
 else:
  c.box('TailUpper',(0,-.96,1.12),(.09,.10,.43),'Tail','cream',rotation=(.27,0,0))
  c.box('TailLower',(0,-1.05,.83),(.09,.10,.30),'Tail','cream',rotation=(.23,0,0))
  c.box('TailTassel',(0,-1.09,.65),(.13,.14,.17),'Tail','hoof')
  c.box('Udder',(0,-.43,.674),(.31,.32,.12),'Body','udder')
  for x in [-.085,.085]:
   for y in [-.51,-.35]:c.box('Teat'+str((x,y)),(x,y,.593),(.055,.055,.064),'Body','udder',bevel=.007,segments=2)
  c.features=['longer reference legs','front white eyes and black pupils','coherent brown/cream patches and forehead stripe','elbowed two-part horns','pink inner ears','layered snout','udder and teats','split hooves']
 return c

def guardian():
 c=DetailedCreature('guardian')
 c.bone('Root',(0,0,0));c.bone('Body',(0,0,1.58),'Root');c.bone('Head',(0,0,2.37),'Body');c.bone('Tail',(0,-.2,1.3),'Body')
 for suffix,s in [('L',-1),('R',1)]:
  x=s*.365;armx=s*.91
  c.bone('Leg'+suffix,(x,0,1.35),'Body');c.bone('Knee'+suffix,(x,.005,.70),'Leg'+suffix)
  c.bone('Arm'+suffix,(armx,0,2.24),'Body');c.bone('Hand'+suffix,(armx,.01,1.67),'Arm'+suffix)
  c.box('ThighCore'+suffix,(x,0,1.10),(.37,.41,.47),'Leg'+suffix,'dark')
  c.box('ThighArmor'+suffix,(x,.055,1.08),(.47,.53,.54),'Leg'+suffix,'stone',segments=2)
  c.box('ThighTrim'+suffix,(x,.06,.86),(.485,.55,.075),'Leg'+suffix,'bronze')
  c.box('KneeJoint'+suffix,(x,0,.71),(.31,.35,.17),'Knee'+suffix,'dark',segments=2)
  c.box('KneeCap'+suffix,(x,.25,.71),(.32,.19,.19),'Knee'+suffix,'stone_light',segments=2)
  c.box('ShinCore'+suffix,(x,0,.43),(.34,.38,.39),'Knee'+suffix,'dark')
  c.box('ShinArmor'+suffix,(x,.035,.43),(.41,.46,.44),'Knee'+suffix,'stone',segments=2)
  c.box('ShinTrim'+suffix,(x,.04,.28),(.43,.48,.073),'Knee'+suffix,'bronze')
  for n,ox in enumerate([-.16,0,.16]):
   c.box('Toe'+suffix+str(n),(x+ox,.17,.095),(.153,.70,.19),'Knee'+suffix,'stone',bevel=.012,segments=2)
  c.box('FootBand'+suffix,(x,.14,.20),(.09,.58,.025),'Knee'+suffix,'bronze')
  c.box('ShoulderCore'+suffix,(armx,0,2.23),(.60,.63,.47),'Arm'+suffix,'dark')
  c.box('ShoulderArmor'+suffix,(armx,.02,2.29),(.70,.72,.60),'Arm'+suffix,'stone',segments=2)
  c.box('ShoulderTop'+suffix,(armx,.025,2.60),(.66,.68,.05),'Arm'+suffix,'stone_light')
  # Inset bronze markings on the frontal shoulder plate resemble the reference.
  c.box('ShoulderBorder'+suffix,(armx+s*.25,.388,2.30),(.075,.025,.55),'Arm'+suffix,'bronze',bevel=.002)
  for n,(ox,z,width,height) in enumerate([(-.11,2.48,.24,.07),(.01,2.37,.07,.22),(-.07,2.25,.20,.07),(-.15,2.17,.07,.22)]):
   c.box('ShoulderGlyph'+suffix+str(n),(armx+s*ox,.389,z),(width,.026,height),'Arm'+suffix,'bronze',bevel=.002)
  c.box('UpperArm'+suffix,(armx,0,1.86),(.39,.44,.43),'Arm'+suffix,'stone',segments=2)
  c.box('ElbowJoint'+suffix,(armx,0,1.60),(.30,.35,.14),'Hand'+suffix,'dark')
  c.box('ForearmCore'+suffix,(armx,0,1.37),(.37,.40,.40),'Hand'+suffix,'dark')
  c.box('ForearmArmor'+suffix,(armx,.02,1.37),(.52,.55,.46),'Hand'+suffix,'stone',segments=2)
  c.box('WristTrim'+suffix,(armx,.02,1.17),(.54,.57,.077),'Hand'+suffix,'bronze')
  c.box('ForearmStripe'+suffix,(armx+s*.20,.311,1.42),(.065,.025,.30),'Hand'+suffix,'bronze',bevel=.002)
  c.box('Palm'+suffix,(armx,.08,1.01),(.36,.34,.27),'Hand'+suffix,'dark',segments=2)
  # Three folded fingers and thumb: actual separated geometry, no painted fist.
  for n,ox in enumerate([-.125,0,.125]):
   c.box('Knuckle'+suffix+str(n),(armx+ox,.26,1.04),(.112,.18,.20),'Hand'+suffix,'stone',segments=2)
   c.box('Finger'+suffix+str(n),(armx+ox,.18,.935),(.11,.24,.11),'Hand'+suffix,'stone',segments=2)
  c.box('ThumbBase'+suffix,(armx-s*.22,.065,1.03),(.13,.19,.20),'Hand'+suffix,'stone',rotation=(0,s*.28,0),segments=2)
  c.box('ThumbTip'+suffix,(armx-s*.19,.19,.975),(.125,.14,.13),'Hand'+suffix,'stone',segments=2)
  c.box('Horn'+suffix,(s*.255,-.015,2.90),(.13,.19,.33),'Head','bronze',segments=2)
 c.box('WaistCore',(0,0,1.50),(.72,.43,.25),'Body','dark')
 c.box('Pelvis',(0,0,1.33),(.85,.48,.24),'Body','stone')
 c.box('Belt',(0,.025,1.43),(.92,.55,.12),'Body','bronze')
 c.box('BeltBuckle',(0,.32,1.41),(.23,.095,.24),'Body','bronze',segments=2)
 c.box('BuckleInset',(0,.374,1.41),(.13,.022,.13),'Body','dark')
 c.box('PelvisFront',(0,.29,1.23),(.32,.08,.28),'Body','stone')
 c.box('PelvisRune',(0,.335,1.23),(.065,.025,.27),'Body','bronze',bevel=.002)
 c.box('TorsoCore',(0,0,1.96),(.96,.55,.68),'Body','dark')
 c.box('BackPlate',(0,-.285,1.99),(1.03,.14,.71),'Body','stone',segments=2)
 c.box('ChestTop',(0,.05,2.27),(1.035,.63,.15),'Body','stone',segments=2)
 c.box('ChestBottom',(0,.05,1.71),(1.035,.63,.15),'Body','stone',segments=2)
 for s in [-1,1]:
  c.box('ChestSide'+str(s),(s*.425,.04,1.99),(.19,.62,.51),'Body','stone',segments=2)
  c.box('ChestTrim'+str(s),(s*.40,.366,2.20),(.115,.037,.29),'Body','bronze')
 c.box('LowerChestTrim',(0,.035,1.62),(.98,.60,.073),'Body','bronze')
 # Recessed core, layered frame, bright panel and a geometric diamond rune.
 for n,(pos,dims) in enumerate([((-.25,.363,1.99),(.07,.085,.48)),((.25,.363,1.99),(.07,.085,.48)),((0,.363,2.23),(.56,.085,.07)),((0,.363,1.75),(.56,.085,.07))]):
  c.box('CoreFrame'+str(n),pos,dims,'Body','stone_light',bevel=.008,segments=2)
 c.box('CoreRecess',(0,.31,1.99),(.45,.045,.43),'Body','dark')
 c.box('AmberPanel',(0,.34,1.99),(.34,.025,.33),'Body','amber',bevel=.008)
 for n,(x,z,rot) in enumerate([(-.063,2.053,.785),(.063,2.053,-.785),(-.063,1.927,-.785),(.063,1.927,.785)]):
  dims=(.035,.016,.18)
  c.box('CoreGlyph'+str(n),(x,.360,z),dims,'Body','glyph',rotation=(0,rot,0),bevel=.002)
 c.box('CoreCenter',(0,.373,1.99),(.07,.016,.07),'Body','bronze',bevel=.002)
 c.box('Neck',(0,0,2.38),(.31,.35,.17),'Head','dark')
 c.box('HeadCore',(0,.015,2.59),(.57,.51,.39),'Head','dark',segments=2)
 c.box('HelmetTop',(0,.015,2.775),(.61,.55,.075),'Head','stone',segments=2)
 c.box('Forehead',(0,.283,2.69),(.59,.075,.14),'Head','stone',segments=2)
 c.box('Jaw',(0,.292,2.48),(.54,.075,.16),'Head','stone',segments=2)
 for s in [-1,1]:
  c.box('Cheek'+str(s),(s*.24,.294,2.57),(.085,.075,.15),'Head','stone')
  c.box('EyeSocket'+str(s),(s*.157,.279,2.604),(.14,.025,.087),'Head','bronze')
  c.box('GlowingEye'+str(s),(s*.157,.295,2.604),(.094,.019,.057),'Head','amber',bevel=.003)
 c.box('NoseBridge',(0,.308,2.598),(.10,.085,.15),'Head','stone')
 # Set the rest silhouette closer to the reference: arms slope outward and
 # legs have a small broad stance. Transform rigid pieces AND joint rests.
 transforms={}
 for suffix,s in [('L',-1),('R',1)]:
  arm=Vector(c.bones['Arm'+suffix][0]);hand=Vector(c.bones['Hand'+suffix][0])
  ar=Matrix.Rotation(-s*.13,3,'Y');hr=Matrix.Rotation(-s*.05,3,'Y')
  transforms['Arm'+suffix]=lambda p,a=arm,r=ar:r@(p-a)+a
  transforms['Hand'+suffix]=lambda p,a=arm,h=hand,r=ar,q=hr:r@(q@(p-h)+h-a)+a
  c.bones['Hand'+suffix]=(tuple(ar@(hand-arm)+arm),'Arm'+suffix)
  hip=Vector(c.bones['Leg'+suffix][0]);leg=Matrix.Rotation(-s*.09,3,'Y')
  transforms['Leg'+suffix]=transforms['Knee'+suffix]=lambda p,h=hip,r=leg:r@(p-h)+h
  knee=Vector(c.bones['Knee'+suffix][0]);c.bones['Knee'+suffix]=(tuple(leg@(knee-hip)+hip),'Leg'+suffix)
 c.vertices=[tuple(transforms[bone](Vector(v))) if bone in transforms else v for v,bone in zip(c.vertices,c.weights)]
 lift=-min(v[2] for v in c.vertices)
 c.vertices=[(x,y,z+lift) for x,y,z in c.vertices]
 c.bones={name:((x,y,z+(lift if name!='Root' else 0)),parent) for name,((x,y,z),parent) in c.bones.items()}
 c.features=['long articulated legs','layered shoulder armor with bronze markings','three-section chest armor','recessed emissive core and raised rune','belt and buckle','separate kneecaps and segmented toes','folded finger geometry and thumb','helmet, recessed glowing eyes and jaw']
 return c

if __name__=='__main__':
 records=[]
 for ident in args.ids.split(','):
  records.append((guardian() if ident=='guardian' else animal(ident)).finish())
 existing=json.loads((OUT/'manifest.json').read_text(encoding='utf-8')) if (OUT/'manifest.json').exists() else []
 mapping={r['id']:r for r in existing};mapping.update({r['id']:r for r in records})
 (OUT/'manifest.json').write_text(json.dumps(list(mapping.values()),ensure_ascii=False,indent=2),encoding='utf-8')
 print('REFERENCE_REVISION_READY',flush=True)
