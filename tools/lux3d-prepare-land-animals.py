import concurrent.futures, importlib, json, os, pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
SKILL = pathlib.Path(r"C:\Users\lenovo\.codex\plugins\cache\openai-curated-remote\aholo-lux3d\0.1.1\skills\lux3d")
sys.path.insert(0, str(SKILL / "core" / "runtime"))
import asset_uploader, lux3d_client
config = json.loads((pathlib.Path.home() / ".config/lux3d/credentials.json").read_text(encoding="utf-8"))
os.environ["LUX3D_CN_API_KEY"] = config["apiKeys"]["cn"]
plan = json.loads((ROOT / "docs/previews/creature-redesign-2026-10-13/lux3d-land-animal-plan.json").read_text(encoding="utf-8"))
work = ROOT / ".tools/lux3d/land-animals-20261013"
work.mkdir(parents=True, exist_ok=True)
def prepare(item):
    record = work / (item["id"] + "-upload.json")
    lux3d_client.validate_face_count(item["faceCount"])
    lux3d_client.validate_generation_options(item["version"], item["outputFormat"])
    if record.exists():
        data = json.loads(record.read_text(encoding="utf-8"))
    else:
        data = asset_uploader.upload_file(ROOT / item["reference"], region="cn", timeout=30)
        asset_uploader.write_json_atomic(record, data)
    if data.get("status") != 5 or not data.get("url"):
        raise RuntimeError("Input asset upload incomplete")
    return item, data["url"]
items = {}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    futures = {pool.submit(prepare, item): seq for seq, item in enumerate(plan["items"], 1)}
    for future in concurrent.futures.as_completed(futures):
        seq = futures[future]
        try:
            item, url = future.result()
        except Exception as error:
            print(json.dumps({"sequence": seq, "errorType": type(error).__name__, "status": "input-preparation-failed"}), flush=True)
            raise SystemExit(1)
        body = {"img": url, "version": item["version"], "faceCount": item["faceCount"], "outputFormat": item["outputFormat"]}
        items[str(seq)] = {"endpoint": {"method": "POST", "path": "/lux3d/v1/generate/img-to-3d/task/create"}, "parameters": {"body": body}}
        print(json.dumps({"id": item["id"], "status": "reference-uploaded", "expectedFormats": lux3d_client.generation_result_formats(item["version"], item["outputFormat"])}), flush=True)
asset_uploader.write_json_atomic(work / "quote-items.json", dict(sorted(items.items(), key=lambda pair: int(pair[0]))))
print(json.dumps({"status": "ready-for-quote", "count": len(items)}))

