"""Render actual generated GLBs in local Blender; never modify the game."""
import argparse, datetime, json, math, pathlib, sys, time
import bpy
from mathutils import Vector
ROOT = pathlib.Path(r"F:\我的世界")
OUTPUT = ROOT / "docs/previews/blender-creature-renders-2026-10-13"
OUTPUT.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument("--probe", action="store_true")
parser.add_argument("--id", default=None)
parser.add_argument("--watch", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
catalog = json.loads((ROOT/"data/creatures.json").read_text(encoding="utf-8-sig"))
old_ids = {"deer","boar","cow","pig","sheep","chicken","horse","wolf"}
new_plan = json.loads((ROOT/"docs/previews/creature-redesign-2026-10-13/lux3d-remaining-model-plan.json").read_text(encoding="utf-8-sig"))
new_items = {i["id"]:i for i in new_plan["items"]}
ordered = [i for i in catalog if i["Id"] in old_ids] + [i for i in catalog if i["Id"] not in old_ids]
if args.id:
    ordered = [i for i in ordered if i["Id"] == args.id]
def source_path(ident):
    if ident in old_ids:
        p = ROOT / "docs/previews/land-animal-models-2026-10-13/models" / (ident+".glb")
        return p if p.exists() else None
    item = new_items[ident]
    state_path = ROOT / ".tools/lux3d/remaining-creatures-20261013" / item["proposedAccountProfile"] / "attempts" / (ident+".json")
    if not state_path.exists():
        return None
    if json.loads(state_path.read_text(encoding="utf-8")).get("status") != "succeeded":
        return None
    p = ROOT / ".tools/lux3d/remaining-creatures-20261013/artifacts" / ident / (ident+"_glb.glb")
    return p if p.exists() else None
def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
def area(name, position, energy, size, color, target):
    data=bpy.data.lights.new(name, "AREA")
    data.energy=energy;data.shape="DISK";data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    obj.location=position;aim(obj,target)
def render_item(item, source):
    ident=item["Id"]
    dest=OUTPUT/("probe-"+ident if args.probe else ident)
    dest.mkdir(exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.samples=12 if args.probe else 32
    scene.cycles.use_denoising=True
    scene.cycles.device="CPU"
    gpu=False
    prefs=bpy.context.preferences.addons.get("cycles")
    if prefs:
        prefs=prefs.preferences
        for backend in ["OPTIX","CUDA","HIP","ONEAPI"]:
            try:
                prefs.compute_device_type=backend
                prefs.get_devices()
                devices=[d for d in prefs.devices if d.type==backend]
                if devices:
                    for d in prefs.devices:d.use=d.type==backend
                    scene.cycles.device="GPU";gpu=True;break
            except Exception:
                continue
    scene.render.resolution_x=768 if args.probe else 1536
    scene.render.resolution_y=768 if args.probe else 1536
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.film_transparent=False
    scene.view_settings.view_transform="AgX"
    scene.render.threads_mode="FIXED";scene.render.threads=8
    world=bpy.data.worlds.new("Studio World");scene.world=world;world.use_nodes=True
    world.node_tree.nodes["Background"].inputs["Color"].default_value=(0.22,0.24,0.28,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value=0.45
    bpy.ops.import_scene.gltf(filepath=str(source))
    imported=[o for o in scene.objects if o.type=="MESH"]
    if not imported:raise RuntimeError("No source mesh imported")
    coords=[o.matrix_world@Vector(c) for o in imported for c in o.bound_box]
    minimum=Vector(tuple(min(v[k] for v in coords) for k in range(3)))
    maximum=Vector(tuple(max(v[k] for v in coords) for k in range(3)))
    native_size=maximum-minimum
    scale=3.0/max(native_size)
    center=(minimum+maximum)/2
    origin=Vector((center.x,center.y,minimum.z))
    root=bpy.data.objects.new("Render placement only - source GLB unchanged",None)
    scene.collection.objects.link(root)
    for obj in list(scene.objects):
        if obj!=root and obj.parent is None:obj.parent=root
    root.scale=(scale,scale,scale);root.location=-origin*scale
    bpy.context.view_layer.update()
    height=native_size.z*scale
    target=Vector((0,0,height*.5))
    ground_mat=bpy.data.materials.new("Warm matte studio floor");ground_mat.use_nodes=True
    principled=ground_mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value=(0.33,0.34,0.36,1)
    principled.inputs["Roughness"].default_value=0.9
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025))
    ground=bpy.context.object;ground.name="Studio Floor";ground.data.materials.append(ground_mat)
    area("Key softbox",(4,-5,7),850,5,(1,.92,.82),target)
    area("Fill softbox",(-4,-2,4),500,5,(.82,.9,1),target)
    area("Rim softbox",(2,5,6),1000,4,(1,.94,.84),target)
    camera_data=bpy.data.cameras.new("Model Camera");camera_data.type="ORTHO";camera_data.ortho_scale=4.7
    camera=bpy.data.objects.new("Model Camera",camera_data);scene.collection.objects.link(camera);scene.camera=camera
    radius=(native_size*scale).length*.5
    camera.location=target+Vector((1,-1.6,.85)).normalized()*max(7,radius*3)
    aim(camera,target)
    normalized_corners=[(point-origin)*scale for point in coords]
    def fit_frame():
        rotation=camera.rotation_euler.to_quaternion()
        right=rotation@Vector((1,0,0));up=rotation@Vector((0,1,0))
        xs=[(point-target).dot(right) for point in normalized_corners]
        ys=[(point-target).dot(up) for point in normalized_corners]
        camera_data.ortho_scale=max(max(xs)-min(xs),max(ys)-min(ys))*1.18
    fit_frame()
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/(ident+".blend")))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/(ident+".blend")))
    angles=[("three-quarter",camera.location.copy())]
    if not args.probe:angles.append(("side",target+Vector((1,.1,.28)).normalized()*max(7,radius*3)))
    files=[]
    for label,position in angles:
        camera.location=position;aim(camera,target);fit_frame()
        scene.render.filepath=str(dest/(ident+"-"+label+".png"))
        bpy.ops.render.render(write_still=True)
        files.append({"path":scene.render.filepath,"width":scene.render.resolution_x,"height":scene.render.resolution_y})
    report={"id":ident,"name":item["Name"],"blenderVersion":bpy.app.version_string,"engine":"Cycles","gpu":gpu,"samples":scene.cycles.samples,"source":str(source),"sourceMeshCount":len(imported),"sourceNativeDimensions":list(native_size),"renderOnlyUniformScale":scale,"sourceMaterialsPreserved":True,"workingFile":str(dest/(ident+".blend")),"renders":files,"completedAt":datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),"gameIntegrated":False}
    (dest/"render-record.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print("CREATURE_RENDER_READY "+json.dumps({"id":ident,"images":len(files),"gpu":gpu}),flush=True)
pending=list(ordered)
while pending:
    made_progress=False
    for item in list(pending):
        ident=item["Id"]
        previous=OUTPUT/ident/"render-record.json"
        if not args.probe and previous.exists():
            pending.remove(item);continue
        source=source_path(ident)
        if source is None:continue
        render_item(item,source);pending.remove(item);made_progress=True
        if args.probe:break
    if args.probe or not args.watch:break
    if pending:
        print("RENDER_WAIT "+json.dumps({"remaining":[i["Id"] for i in pending]}),flush=True)
        time.sleep(20)
print("RENDER_BATCH_EXIT "+json.dumps({"pending":[i["Id"] for i in pending]}),flush=True)

