import bpy, json, pathlib
ROOT=pathlib.Path(r"F:\我的世界")
OUT=ROOT/"docs/previews/blender-creature-renders-2026-10-13"
items=json.loads((ROOT/"data/creatures.json").read_text(encoding="utf-8-sig"))
checks=[]
for item in items:
    ident=item["Id"];file=OUT/ident/(ident+".blend")
    bpy.ops.wm.open_mainfile(filepath=str(file))
    meshes=[o for o in bpy.context.scene.objects if o.type=="MESH" and o.name!="Studio Floor"]
    source_images=[i for i in bpy.data.images if i.type not in {"RENDER_RESULT","COMPOSITING"}]
    row={"id":ident,"file":str(file),"meshes":len(meshes),"sourceImages":len(source_images),"allSourceImagesPacked":all(i.packed_file is not None or bool(i.packed_files) for i in source_images),"cameraPresent":bpy.context.scene.camera is not None,"renderEngine":bpy.context.scene.render.engine}
    assert row["meshes"]>0 and row["cameraPresent"] and row["allSourceImagesPacked"] and row["renderEngine"]=="CYCLES"
    checks.append(row);print("WORKFILE_CHECK "+json.dumps({"id":ident,"passed":True}),flush=True)
(OUT/"blend-file-verification.json").write_text(json.dumps({"models":len(checks),"passed":True,"checks":checks},ensure_ascii=False,indent=2),encoding="utf-8")
print("ALL_BLEND_FILES_VERIFIED",flush=True)

