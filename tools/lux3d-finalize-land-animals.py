"""Package the eight generated source models without integrating them into the game."""
import hashlib, json, pathlib, shutil, struct, sys, zipfile
ROOT = pathlib.Path(__file__).resolve().parents[1]
SKILL = pathlib.Path(r"C:\Users\lenovo\.codex\plugins\cache\openai-curated-remote\aholo-lux3d\0.1.1\skills\lux3d")
sys.path.insert(0, str(SKILL / "core/runtime"))
import artifact_delivery, delivery_bundle
WORK = ROOT / ".tools/lux3d/land-animals-20261013"
TARGET = ROOT / "docs/previews/land-animal-models-2026-10-13"
PLAN_PATH = ROOT / "docs/previews/creature-redesign-2026-10-13/lux3d-land-animal-plan.json"
plan = json.loads(PLAN_PATH.read_text(encoding="utf-8-sig"))
states = {}
for item in plan["items"]:
    state = json.loads((WORK / "attempts" / (item["id"] + ".json")).read_text(encoding="utf-8"))
    if state.get("status") != "succeeded":
        raise SystemExit("Not all eight source models are ready")
    states[item["id"]] = state
manifest = delivery_bundle.prepare_bundle(str(WORK / "delivery-spec.json"), str(TARGET), locale="zh-CN")
assert manifest["assetStatus"] == "complete"
(TARGET / "models").mkdir()
(TARGET / "originals").mkdir()
rows = []
for item in plan["items"]:
    ident = item["id"]
    glb = WORK / "artifacts" / ident / (ident + "_glb.glb")
    original = WORK / "artifacts" / ident / (ident + "_zip.zip")
    artifact_delivery.inspect_artifact(glb, "glb", require_embedded_glb=True)
    blob = glb.read_bytes()
    chunk_length, _ = struct.unpack_from("<II", blob, 12)
    document = json.loads(blob[20:20+chunk_length])
    primitives = [p for m in document.get("meshes", []) for p in m["primitives"]]
    triangles = sum(document["accessors"][p["indices"]]["count"] // 3 if "indices" in p else document["accessors"][p["attributes"]["POSITION"]]["count"] // 3 for p in primitives)
    with zipfile.ZipFile(original) as archive:
        original_files = archive.namelist()
    shutil.copyfile(glb, TARGET / "models" / (ident + ".glb"))
    shutil.copyfile(original, TARGET / "originals" / (ident + "-source.zip"))
    rows.append({"id": ident, "name": item["name"], "taskId": states[ident]["taskId"], "targetFaces": item["faceCount"], "actualTriangles": triangles, "meshes": len(document.get("meshes", [])), "materials": len(document.get("materials", [])), "embeddedImages": sum("bufferView" in image for image in document.get("images", [])), "skins": len(document.get("skins", [])), "animations": len(document.get("animations", [])), "allPrimitivesHaveUV": all("TEXCOORD_0" in p["attributes"] for p in primitives), "originalArchiveContents": original_files, "glbBytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()})
    item.update(status="generated", taskId=states[ident]["taskId"], actualTriangles=triangles)
artifact_delivery.write_json_atomic(TARGET / "model-inspection.json", {"models": rows, "sourceFidelity": "Approximate AI reconstruction: some voxel steps are smoothed or represented by textures; not deterministic per-cube geometry.", "integration": "Deferred by user; game code and models have not been replaced."})
godot_report = ROOT / ".tools/lux3d/godot-check/godot-import-report.json"
if godot_report.exists():
    shutil.copyfile(godot_report, TARGET / "godot-import-report.json")
table = "\n".join("| " + str(row["name"]) + " | " + row["id"] + " | " + str(row["targetFaces"]) + " | " + str(row["actualTriangles"]) + " |" for row in rows)
readme = """# 八种陆地动物模型 · 第一版
\n生成服务：Aholo Lux3D，中国站标准版；基于已确认的第一张精细体素预览。8 个任务已实际成功并完成下载。
\n## 使用文件
\n- preview.html：包含 8 个独立模型的离线交互预览和原始文件下载。
- models/：按游戏 ID 命名的独立 GLB，包含贴图和 UV。
- originals/：服务原始 ZIP，每个包含 MODEL.glb 和 6 张原始材质通道贴图。
- lux3d-delivery.json：插件生成的来源清单。
- model-inspection.json：实测三角面、材质、UV 和文件哈希。
- godot-import-report.json：独立 Godot 4.6 项目的真实导入结果。
- inspection/browser-verification.json：离线预览的加载、旋转、缩放验证；inspection/ 中保留实际模型截图。
\n## 实测面数
\n| 动物 | 文件 ID | 生成目标面数 | GLB 实测三角面 |
| --- | --- | ---: | ---: |
""" + table + """
\n## 当前限制
\n本批是静态源网格：没有骨骼和动画，未拆分出独立关节。AI 重建与概念图并非逐方块对应，部分阶梯形边缘被平滑或以纹理表现；材质也存在差异，例如鸡尾部出现红色斑块，需要后续整理。保留所有原始版本，没有为这些差异再次付费生成，也未把源模型标记为已达到最终美术质量。
\n导入检查仅确认 Godot 能读取并生成网格和材质，不表示已完成游戏动画、碰撞、尺寸朝向、LOD 或性能验收。后续需要统一脚底原点、Y 轴向上和 -Z 朝向，制作关节、动画与碰撞并验证实际游戏表现。
\n用户要求：先不加入游戏，等全部完成后再接入。本批文件保存在文档预览交付目录，没有替换游戏模型、修改游戏逻辑或发布游戏包。
\n插件原始清单的顶层 status 是 incomplete，这是打包器保留的状态；模型是否齐全以 assetStatus 和实际文件为准。预览是否通过检查以独立 browser-verification.json 为准，不修改原始清单制造验收结果。
"""
(TARGET / "README.md").write_text(readme, encoding="utf-8")
plan["status"] = "source-models-generated-awaiting-final-verification"
plan["integrationStatus"] = "deferred-by-user"
artifact_delivery.write_json_atomic(PLAN_PATH, plan)
artifact_delivery.write_json_atomic(WORK / "finalization-record.json", {"delivery": str(TARGET), "modelCount": len(rows), "gameIntegration": False})
print(json.dumps({"delivery": str(TARGET), "modelCount": len(rows), "actualTriangles": {row["id"]: row["actualTriangles"] for row in rows}}))

