"""Author three new cuboid creatures; no source meshes or game assets are changed.

Run with local Blender --background --python this_file. +Y is forward in Blender
and becomes -Z in glTF/Godot. All vertices have exactly one bone influence.
"""
import hashlib
import json
import math
import pathlib
import random
import bpy
from mathutils import Vector, Matrix

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/previews/block-creature-prototypes-2026-10-13'
OUT.mkdir(parents=True, exist_ok=True)

PALETTE = {
    'cream': (0.82, 0.78, 0.64), 'brown': (0.32, 0.16, 0.07),
    'hoof': (0.12, 0.10, 0.09), 'pink': (0.83, 0.43, 0.43),
    'pig': (0.87, 0.56, 0.56), 'stone': (0.17, 0.20, 0.20),
    'bronze': (0.40, 0.29, 0.15), 'dark': (0.065, 0.08, 0.085),
    'amber': (0.94, 0.49, 0.07), 'ivory': (0.70, 0.66, 0.50),
}

def point_camera(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()

class Creature:
    def __init__(self, ident, atlas_size):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.ident = ident
        self.folder = OUT / ident
        self.folder.mkdir(exist_ok=True)
        self.size = atlas_size
        self.pixels = [0.0, 0.0, 0.0, 1.0] * (atlas_size*atlas_size)
        self.vertices, self.faces, self.uvs, self.weights = [], [], [], []
        self.bones, self.blocks, self.tiles = {}, [], 0
        self.image = bpy.data.images.new(ident+'_pixel_atlas', width=atlas_size, height=atlas_size, alpha=False)

    def bone(self, name, location, parent=None):
        self.bones[name] = (location, parent)

    def tile(self, style, face):
        index = self.tiles
        self.tiles += 1
        columns = self.size//8
        if self.tiles > columns*columns:
            raise RuntimeError('Atlas overflow')
        tx, ty = (index % columns)*8, (index//columns)*8
        rng = random.Random(self.ident+str(index))
        base = PALETTE[style]
        # Each tile has a one-pixel duplicated edge gutter. Variation is paint,
        # never extra geometry, baked shadows, normals, or metallic detail.
        colors = {}
        for y in range(6):
            for x in range(6):
                shade = rng.choice([0.95, 1, 1, 1, 1.04])
                color = tuple(min(1, c*shade) for c in base)
                if style == 'cream' and ((x//2+y//2+index)%5 in {0, 1}):
                    color = PALETTE['brown']
                if style in {'stone', 'bronze'} and y == 0:
                    color = tuple(c*.84 for c in base)
                colors[x,y] = color
        if face == 'eye':
            colors[4,4] = (0.025, .025, .025)
            colors[4,3] = (0.025, .025, .025)
            colors[4,4] = (.06, .055, .045)
        elif face == 'snout':
            for x in [1,4]:
                colors[x,2] = colors[x,3] = (.29,.13,.13)
        elif face == 'golem_face':
            for x in [1,4]:
                colors[x,3] = PALETTE['amber']
            colors[2,1] = colors[3,1] = PALETTE['dark']
        elif face == 'rune':
            for x,y in [(2,1),(3,1),(1,2),(4,2),(1,3),(4,3),(2,4),(3,4),(2,2),(3,3)]:
                colors[x,y] = PALETTE['amber']
        for y in range(8):
            for x in range(8):
                c = colors[min(5,max(0,x-1)),min(5,max(0,y-1))]
                offset = ((ty+y)*self.size+tx+x)*4
                self.pixels[offset:offset+4] = [*c,1]
        # Pixel boundaries with a gutter prevent adjacent tiles bleeding.
        u0,v0,u1,v1 = (tx+1)/self.size,(ty+1)/self.size,(tx+7)/self.size,(ty+7)/self.size
        return [(u0,v0),(u1,v0),(u1,v1),(u0,v1)]

    def box(self, name, center, dimensions, bone, style, front=None, eye=False, rotation=0):
        cx,cy,cz = center
        dx,dy,dz = [d/2 for d in dimensions]
        corners = [(-dx,-dy,-dz),(dx,-dy,-dz),(dx,dy,-dz),(-dx,dy,-dz),
                   (-dx,-dy,dz),(dx,-dy,dz),(dx,dy,dz),(-dx,dy,dz)]
        rotate = Matrix.Rotation(rotation,3,'Y')
        corners = [rotate@Vector(v)+Vector(center) for v in corners]
        # Outward winding. Front is +Y; both side eye marks sit toward the nose.
        sides = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
        for n, indices in enumerate(sides):
            first = len(self.vertices)
            self.vertices.extend([tuple(corners[i]) for i in indices])
            self.weights.extend([bone]*4)
            self.faces.append(tuple(first+i for i in range(4)))
            feature = front if n == 3 else 'eye' if eye and n in {4,5} else None
            uv = self.tile(style, feature)
            # Side UV horizontal axis points toward +Y on either side.
            if n == 5:
                uv = [uv[1],uv[2],uv[3],uv[0]]
            elif n == 4:
                uv = [uv[0],uv[3],uv[2],uv[1]]
            elif n == 3:
                uv = [uv[0],uv[3],uv[2],uv[1]]
            self.uvs.extend(uv)
        self.blocks.append({'name':name,'bone':bone,'center':center,'dimensions':dimensions})

    def finish(self):
        self.image.pixels.foreach_set(self.pixels)
        self.image.filepath_raw = str(self.folder/(self.ident+'-atlas.png'))
        self.image.file_format = 'PNG'
        self.image.save()
        self.image.pack()
        mat = bpy.data.materials.new(self.ident+'_matte_pixel')
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value = 1
        shader.inputs['Metallic'].default_value = 0
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = self.image
        tex.interpolation = 'Closest'
        mat.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
        self.decorate_material(mat, shader, tex)
        data = bpy.data.meshes.new(self.ident+'_cuboids')
        data.from_pydata(self.vertices,[],self.faces)
        data.update()
        layer = data.uv_layers.new(name='PixelAtlas')
        for i,uv in enumerate(self.uvs): layer.data[i].uv = uv
        mesh = bpy.data.objects.new(self.ident+'_mesh',data)
        bpy.context.collection.objects.link(mesh)
        data.materials.append(mat)
        for p in data.polygons: p.use_smooth = False
        armature = bpy.data.armatures.new(self.ident+'_skeleton')
        rig = bpy.data.objects.new('CreatureRig',armature)
        bpy.context.collection.objects.link(rig)
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for name,(loc,parent) in self.bones.items():
            b = armature.edit_bones.new(name)
            b.head = loc
            b.tail = Vector(loc)+Vector((0,0,.1))
            if parent: b.parent = armature.edit_bones[parent]
        bpy.ops.object.mode_set(mode='OBJECT')
        for name in self.bones:
            group = mesh.vertex_groups.new(name=name)
            indices = [i for i,b in enumerate(self.weights) if b==name]
            if indices: group.add(indices,1,'REPLACE')
        mesh.parent = rig
        modifier = mesh.modifiers.new('Rigid cuboid skin','ARMATURE')
        modifier.object = rig
        self.mesh, self.rig = mesh, rig
        self.animate()
        bpy.ops.object.select_all(action='DESELECT')
        mesh.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        props = bpy.ops.export_scene.gltf.get_rna_type().properties
        kwargs = dict(filepath=str(self.folder/(self.ident+'.glb')),export_format='GLB',
                      use_selection=True,export_yup=True,export_skins=True,
                      export_animations=True,export_force_sampling=True)
        if 'export_animation_mode' in props: kwargs['export_animation_mode'] = 'NLA_TRACKS'
        bpy.ops.export_scene.gltf(**kwargs)
        data.calc_loop_triangles()
        record = dict(id=self.ident,blocks=len(self.blocks),triangles=len(data.loop_triangles),
                      vertices=len(data.vertices),bones=list(self.bones),textureSize=[self.size,self.size],
                      materialCount=1,skin='rigid: one bone per vertex',clips=self.clips,
                      byteSize=(self.folder/(self.ident+'.glb')).stat().st_size,
                      sha256=hashlib.sha256((self.folder/(self.ident+'.glb')).read_bytes()).hexdigest(),
                      gameIntegrated=False,authoredLocally=True)
        (self.folder/'model-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
        self.stage()
        bpy.ops.wm.save_as_mainfile(filepath=str(self.folder/(self.ident+'.blend')),compress=True)
        self.render()
        print('PROTOTYPE_COMPLETE '+json.dumps(record),flush=True)
        return record

    def decorate_material(self, material, shader, texture):
        pass

    def animate(self):
        scene = bpy.context.scene
        scene.render.fps = 24
        self.rig.animation_data_create()
        self.clips = ['idle','walk','run'] + (['slam'] if self.ident=='guardian' else ['graze'])
        for clip in self.clips:
            self.rig.animation_data.action = None
            frames = 49 if clip in {'idle','graze'} else 33 if clip=='slam' else 25
            action = None
            for f in range(1,frames+1):
                t = (f-1)/(frames-1)
                for b in self.rig.pose.bones:
                    b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
                poses = self.rig.pose.bones
                if clip in {'walk','run'}:
                    amplitude = .31 if clip=='walk' else .49
                    phase = t*math.tau
                    for name in self.bones:
                        offset = math.pi if name.endswith('R') else 0
                        if 'Rear' in name: offset += math.pi
                        wave = math.sin(phase+offset)
                        if name.startswith('Leg'): poses[name].rotation_euler.x = wave*amplitude
                        if name.startswith('Knee'): poses[name].rotation_euler.x = max(0,-wave)*amplitude*.60
                        if name.startswith('Arm'): poses[name].rotation_euler.x = -wave*amplitude*.6
                    poses['Body'].location.z = abs(math.sin(phase))*.012
                    poses['Head'].rotation_euler.x = math.sin(phase*2)*.018
                elif clip=='slam':
                    def ease(a,b,x):
                        p=max(0,min(1,(x-a)/(b-a)));return p*p*(3-2*p)
                    # Visible anticipation, short contact, then a slower recovery.
                    lift = ease(0,.38,t)
                    hit = ease(.38,.55,t)
                    recover = ease(.62,1,t)
                    arms = (-1.10*lift+1.35*hit)*(1-recover)
                    for suffix in ['L','R']:
                        poses['Arm'+suffix].rotation_euler.x = arms
                        poses['Hand'+suffix].rotation_euler.x = -.18*lift*(1-recover)
                    poses['Body'].rotation_euler.x = (-.10*lift+.24*hit)*(1-recover)
                    poses['Head'].rotation_euler.x = -.08*lift*(1-recover)
                else:
                    poses['Body'].location.z=math.sin(t*math.tau)*.004
                    poses['Head'].rotation_euler.y=math.sin(t*math.tau)*.025
                    if clip=='graze': poses['Head'].rotation_euler.x=math.sin(t*math.pi)**2*.38
                poses['Tail'].rotation_euler.y=math.sin(t*math.tau)*.12
                for b in poses:
                    b.keyframe_insert('rotation_euler',frame=f,group=b.name)
                    b.keyframe_insert('location',frame=f,group=b.name)
                if action is None:
                    action=self.rig.animation_data.action
                    action.name=clip
            track=self.rig.animation_data.nla_tracks.new();track.name=clip
            track.strips.new(clip,1,action)
            track.mute=True
        self.rig.animation_data.action=None
        scene.frame_set(1)

    def stage(self):
        scene=bpy.context.scene
        scene.render.engine='CYCLES';scene.cycles.samples=16
        scene.cycles.use_denoising=True
        scene.render.threads_mode='FIXED';scene.render.threads=8
        scene.render.resolution_x=900;scene.render.resolution_y=900
        scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG'
        scene.view_settings.view_transform='Standard'
        world=bpy.data.worlds.new('Blue daytime');scene.world=world;world.use_nodes=True
        world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.65,.86,1)
        world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
        def matte(name,color):
            m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
            s=m.node_tree.nodes.get('Principled BSDF');s.inputs['Base Color'].default_value=(*color,1);s.inputs['Roughness'].default_value=1
            return m
        grass=matte('Block grass',(.28,.47,.12));dirt=matte('Block soil',(.30,.19,.10))
        # A small voxel context; rendering context objects are never exported.
        for x in range(-4,5):
            for y in range(-4,5):
                bpy.ops.mesh.primitive_cube_add(size=2,location=(x,y,-.26))
                cube=bpy.context.object;cube.name='PreviewTerrain';cube.scale=(.5,.5,.25)
                cube.data.materials.append(dirt);cube.data.materials.append(grass)
                for p in cube.data.polygons:p.material_index=1 if p.normal.z>.5 else 0
        light=bpy.data.lights.new('Daylight','SUN');light.energy=2.0;light.angle=math.radians(8)
        obj=bpy.data.objects.new('Daylight',light);scene.collection.objects.link(obj);obj.rotation_euler=(.45,-.5,-.45)
        camdata=bpy.data.cameras.new('Camera');camdata.type='ORTHO'
        cam=bpy.data.objects.new('Camera',camdata);scene.collection.objects.link(cam);scene.camera=cam
        height=max(v[2] for v in self.vertices)
        target=Vector((0,.08,height*.46))
        cam.location=target+Vector((4,6,3.0));point_camera(cam,target)
        camdata.ortho_scale=3.55 if self.ident=='guardian' else 2.65 if self.ident=='cow' else 2.1
        self.target=target

    def render(self):
        scene=bpy.context.scene
        for label,offset in [('three-quarter',(4,6,3)),('side',(7,0,2)),('front',(0,7,1.7))]:
            scene.camera.location=self.target+Vector(offset);point_camera(scene.camera,self.target)
            scene.render.filepath=str(self.folder/(self.ident+'-'+label+'.png'))
            bpy.ops.render.render(write_still=True)
        scene.camera.location=self.target+Vector((4,6,3));point_camera(scene.camera,self.target)
        scene.render.resolution_x=420;scene.render.resolution_y=420;scene.cycles.samples=8
        motion=self.folder/'motion';motion.mkdir(exist_ok=True)
        self.rig.animation_data.action=bpy.data.actions['slam' if self.ident=='guardian' else 'walk']
        for i in range(24):
            frame=1+round(i*(32 if self.ident=='guardian' else 24)/23)
            scene.frame_set(frame)
            scene.render.filepath=str(motion/f'{i:02d}.png')
            bpy.ops.render.render(write_still=True)
        self.rig.animation_data.action=None;scene.frame_set(1)

def animal(ident):
    c=Creature(ident,128)
    pig=ident=='pig'
    c.bone('Root',(0,0,0));c.bone('Body',(0,0,.63 if pig else .91),'Root')
    c.bone('Head',(0,.42,.72) if pig else (0,.48,1.04),'Body')
    c.bone('Tail',(0,-.57,.7) if pig else (0,-.63,1.08),'Body')
    skin='pig' if pig else 'cream'
    hip,knee,legx,fronty,reary=(.39,.22,.28,.37,-.35) if pig else (.62,.31,.29,.40,-.43)
    for suffix,x in [('L',-legx),('R',legx)]:
        for place,y in [('Front',fronty),('Rear',reary)]:
            upper='Leg'+place+suffix;lower='Knee'+place+suffix
            c.bone(upper,(x,y,hip),'Body');c.bone(lower,(x,y,knee),upper)
            c.box(upper,(x,y,(hip+knee)/2),(.19,.21,hip-knee),upper,skin)
            c.box(lower,(x,y,knee/2+.03),(.17,.19,knee-.05),lower,skin)
            c.box('Hoof'+place+suffix,(x,y,.055),(.19,.22,.11),lower,'hoof')
    c.box('Torso',(0,-.05,.64) if pig else (0,-.08,.91),(.74,1.16,.48) if pig else (.82,1.30,.60),'Body',skin)
    c.box('Head',(0,.54,.74) if pig else (0,.62,1.10),(.51,.49,.43) if pig else (.54,.50,.53),'Head',skin,eye=True)
    c.box('Muzzle',(0,.81,.65) if pig else (0,.94,.98),(.34,.13,.22) if pig else (.43,.21,.25),'Head','pink',front='snout')
    for suffix,s in [('L',-1),('R',1)]:
        c.box('Ear'+suffix,(s*.30,.48,.97) if pig else (s*.40,.59,1.27),(.19,.17,.08) if pig else (.28,.15,.10),'Head',skin,rotation=s*(.28 if pig else .08))
        if not pig:
            c.box('Horn'+suffix,(s*.24,.49,1.43),(.10,.13,.23),'Head','ivory',rotation=s*.26)
    if pig:
        c.box('TailStem',(0,-.69,.74),(.08,.19,.08),'Tail','pig')
        c.box('TailTip',(.07,-.79,.78),(.20,.07,.08),'Tail','pig')
    else:
        c.box('TailStem',(0,-.77,.91),(.07,.07,.39),'Tail','brown')
        c.box('TailTip',(0,-.77,.66),(.12,.10,.15),'Tail','hoof')
    return c

def guardian():
    c=Creature('guardian',256)
    c.bone('Root',(0,0,0));c.bone('Body',(0,0,1.30),'Root')
    c.bone('Head',(0,0,2.14),'Body');c.bone('Tail',(0,-.2,1.2),'Body')
    for suffix,s in [('L',-1),('R',1)]:
        x=s*.33
        c.bone('Leg'+suffix,(x,0,1.06),'Body');c.bone('Knee'+suffix,(x,0,.55),'Leg'+suffix)
        c.bone('Arm'+suffix,(s*.80,0,1.98),'Body');c.bone('Hand'+suffix,(s*.82,.02,1.33),'Arm'+suffix)
        c.box('Thigh'+suffix,(x,0,.84),(.43,.46,.45),'Leg'+suffix,'stone')
        c.box('Knee'+suffix,(x,.04,.54),(.46,.48,.18),'Knee'+suffix,'bronze')
        c.box('Shin'+suffix,(x,0,.34),(.41,.43,.34),'Knee'+suffix,'stone')
        c.box('Foot'+suffix,(x,.14,.10),(.51,.70,.20),'Knee'+suffix,'dark')
        c.box('Shoulder'+suffix,(s*.82,0,2.03),(.53,.64,.43),'Arm'+suffix,'stone')
        c.box('ShoulderBand'+suffix,(s*.82,.04,2.16),(.55,.65,.09),'Arm'+suffix,'bronze')
        c.box('UpperArm'+suffix,(s*.83,0,1.67),(.32,.37,.43),'Arm'+suffix,'dark')
        c.box('Elbow'+suffix,(s*.83,0,1.39),(.35,.40,.16),'Hand'+suffix,'bronze')
        c.box('Forearm'+suffix,(s*.83,.02,1.13),(.45,.50,.43),'Hand'+suffix,'stone')
        c.box('Fist'+suffix,(s*.83,.10,.85),(.47,.55,.22),'Hand'+suffix,'dark')
        c.box('Horn'+suffix,(s*.28,-.02,2.62),(.13,.18,.27),'Head','bronze')
    c.box('Pelvis',(0,0,1.10),(.83,.46,.25),'Body','dark')
    c.box('Torso',(0,0,1.63),(1.03,.57,.78),'Body','stone')
    c.box('ChestBand',(0,.30,1.85),(1.05,.08,.16),'Body','bronze')
    c.box('CoreFrame',(0,.34,1.56),(.39,.12,.43),'Body','bronze')
    c.box('Core',(0,.41,1.56),(.25,.04,.29),'Body','dark',front='rune')
    c.box('Head',(0,.03,2.31),(.61,.54,.39),'Head','stone',front='golem_face')
    c.box('Brow',(0,.31,2.45),(.66,.10,.09),'Head','bronze')
    return c

if __name__ == '__main__':
    records=[]
    for ident in ['cow','pig','guardian']:
        records.append((guardian() if ident=='guardian' else animal(ident)).finish())
    (OUT/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    print('ALL_PROTOTYPES_READY',flush=True)
