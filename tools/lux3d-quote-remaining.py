"""Prepare real itemized quotes without submitting any generation task."""
import argparse, concurrent.futures, json, os, pathlib, subprocess, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
SKILL = pathlib.Path(r"C:\Users\lenovo\.codex\plugins\cache\openai-curated-remote\aholo-lux3d\0.1.1\skills\lux3d")
sys.path.insert(0, str(SKILL / "core/runtime"))
import asset_uploader, lux3d_client
parser = argparse.ArgumentParser()
parser.add_argument("--use-sheet-inputs", action="store_true")
args = parser.parse_args()
config = json.loads((pathlib.Path.home() / ".config/lux3d/credentials.json").read_text(encoding="utf-8"))
plan_path = ROOT / "docs/previews/creature-redesign-2026-10-13/lux3d-remaining-model-plan.json"
plan = json.loads(plan_path.read_text(encoding="utf-8-sig"))
work = ROOT / ".tools/lux3d/remaining-creatures-20261013"
work.mkdir(parents=True, exist_ok=True)
combined = {"preliminarySheetInputs": args.use_sheet_inputs, "generationTasksSubmitted": False, "accounts": {}, "items": {}, "estimatedCreditsTotal": 0}
for profile in ["remaining-creatures", "land-animals"]:
    os.environ["LUX3D_CN_API_KEY"] = config["profiles"][profile]["apiKeys"]["cn"]
    group = [item for item in plan["items"] if item["proposedAccountProfile"] == profile]
    def prepare(item):
        lux3d_client.validate_face_count(item["faceCount"])
        lux3d_client.validate_generation_options(item["version"], item["outputFormat"])
        source = ROOT / ("docs/previews/creature-redesign-2026-10-13/" + item["sheet"] if args.use_sheet_inputs else item["reference"])
        cache = work / (profile + "-" + source.stem + ("-sheet" if args.use_sheet_inputs else "-isolated") + "-upload.json")
        if cache.exists():
            upload = json.loads(cache.read_text(encoding="utf-8"))
        else:
            upload = asset_uploader.upload_file(source, region="cn")
            asset_uploader.write_json_atomic(cache, upload)
        if upload.get("status") != 5:
            raise RuntimeError("Input asset upload incomplete")
        return item, upload["url"]
    if args.use_sheet_inputs:
        uploaded = {}
        prepared = []
        for item in group:
            if item["sheet"] not in uploaded:
                _, url = prepare(item)
                uploaded[item["sheet"]] = url
            prepared.append((item, uploaded[item["sheet"]]))
    else:
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            prepared = list(pool.map(prepare, group))
    quote_items = {}
    for sequence, (item, url) in enumerate(prepared, 1):
        quote_items[str(sequence)] = {"endpoint": {"method":"POST","path":"/lux3d/v1/generate/img-to-3d/task/create"}, "parameters":{"body":{"img":url,"version":item["version"],"faceCount":item["faceCount"],"outputFormat":item["outputFormat"]}}}
    suffix = "preliminary" if args.use_sheet_inputs else "prepared"
    items_path = work / (profile + "-" + suffix + "-quote-items.json")
    asset_uploader.write_json_atomic(items_path, quote_items)
    call = subprocess.run([sys.executable,str(SKILL / "scripts/commerce.py"),"quote","--region","cn","--items",str(items_path)],capture_output=True,text=True,encoding="utf-8")
    if call.returncode:
        print(json.dumps({"profile":profile,"status":"quote-failed","noGenerationSubmitted":True}))
        raise SystemExit(1)
    response = json.loads(call.stdout)
    asset_uploader.write_json_atomic(work / (profile + "-" + suffix + "-quote-response.json"), response)
    combined["accounts"][profile] = {"accountRef":response["account"]["accountRef"],"availableCredits":response["account"]["availableCredits"],"quotedSubtotal":response["quote"]["estimatedCreditsTotal"],"quoteId":response["quote"]["quoteId"],"expiresAt":response["quote"]["expiresAt"]}
    for sequence, (item, _) in enumerate(prepared, 1):
        cost = response["quote"]["details"][str(sequence)]
        combined["items"][item["id"]] = {"profile":profile,"sequence":str(sequence),"estimatedCredits":cost}
    combined["estimatedCreditsTotal"] += response["quote"]["estimatedCreditsTotal"]
    print(json.dumps({"profile":profile,"items":len(group),"availableCredits":response["account"]["availableCredits"],"estimatedCredits":response["quote"]["estimatedCreditsTotal"]}),flush=True)
asset_uploader.write_json_atomic(work / ("combined-" + suffix + "-quote.json"), combined)
print(json.dumps({"totalItems":len(combined["items"]),"estimatedCreditsTotal":combined["estimatedCreditsTotal"],"generationTasksSubmitted":False}))

