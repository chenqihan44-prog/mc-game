"""Execute one preallocated account partition of the approved 18-model batch."""
import argparse, datetime, json, os, pathlib, subprocess, sys, time
ROOT = pathlib.Path(__file__).resolve().parents[1]
SKILL = pathlib.Path(r"C:\Users\lenovo\.codex\plugins\cache\openai-curated-remote\aholo-lux3d\0.1.1\skills\lux3d")
sys.path.insert(0, str(SKILL / "core/runtime"))
import lux3d_client as client
import artifact_delivery as delivery
parser = argparse.ArgumentParser()
parser.add_argument("--profile", choices=["remaining-creatures", "land-animals"], required=True)
args = parser.parse_args()
BASE = ROOT / ".tools/lux3d/remaining-creatures-20261013"
APPROVAL = json.loads((BASE / "user-batch-approval.json").read_text(encoding="utf-8"))
assert APPROVAL["status"] == "approved" and APPROVAL["authorizedQuotedCredits"] == 360
PROFILE = args.profile
LIMIT = APPROVAL["accountAllocation"][PROFILE]["quotedCredits"]
WORK = BASE / PROFILE
WORK.mkdir(exist_ok=True)
ATTEMPTS = WORK / "attempts"
ATTEMPTS.mkdir(exist_ok=True)
full_plan = json.loads((ROOT / "docs/previews/creature-redesign-2026-10-13/lux3d-remaining-model-plan.json").read_text(encoding="utf-8-sig"))
PLAN = {**full_plan, "items": [item for item in full_plan["items"] if item["proposedAccountProfile"] == PROFILE]}
assert len(PLAN["items"]) == APPROVAL["accountAllocation"][PROFILE]["models"]
ITEMS = json.loads((BASE / (PROFILE + "-prepared-quote-items.json")).read_text(encoding="utf-8-sig"))
TOTAL = len(PLAN["items"])
def now():
    return datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat()
def save(path, value):
    delivery.write_json_atomic(path, value)
def emit(**data):
    print(json.dumps(data, ensure_ascii=True), flush=True)
config = json.loads((pathlib.Path.home() / ".config/lux3d/credentials.json").read_text(encoding="utf-8"))
os.environ["LUX3D_CN_API_KEY"] = config["profiles"][PROFILE]["apiKeys"]["cn"]
ledger_path = WORK / "approved-batch-ledger.json"
quote = json.loads((BASE / (PROFILE + "-prepared-quote-response.json")).read_text(encoding="utf-8-sig"))
if not ledger_path.exists():
    assert quote["quote"]["estimatedCreditsTotal"] == LIMIT
    save(ledger_path, {"mode": "batch", "authorization": APPROVAL["authorization"], "profile": PROFILE, "authorizedQuotedCredits": LIMIT, "region": "cn", "createdAt": now(), "entries": {i["id"]: {"estimatedCredits": 20, "state": "reserved"} for i in PLAN["items"]}})
ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
def refresh_quote_if_needed():
    global quote
    expiry = datetime.datetime.fromisoformat(quote["quote"]["expiresAt"])
    if expiry <= datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=40):
        completed = subprocess.run([sys.executable, str(SKILL / "scripts/commerce.py"), "quote", "--region", "cn", "--items", str(BASE / (PROFILE + "-prepared-quote-items.json"))], capture_output=True, text=True)
        if completed.returncode:
            raise RuntimeError("Quote refresh failed; no new submission")
        renewed = json.loads(completed.stdout)
        if renewed["quote"]["estimatedCreditsTotal"] != LIMIT or any(v != 20 for v in renewed["quote"]["details"].values()):
            raise RuntimeError("Quote changed; new spending approval required")
        if renewed["account"]["accountRef"] != quote["account"]["accountRef"]:
            raise RuntimeError("Account changed; new quote review required")
        quote = renewed
        save(WORK / "approved-quote-response.json", quote)
        emit(event="quote-refreshed", total=LIMIT)
    assert quote["quote"]["estimatedCreditsTotal"] == LIMIT
def read_state(item):
    path = ATTEMPTS / (item["id"] + ".json")
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None
def submit(item, seq):
    previous = read_state(item)
    if previous is not None:
        if not previous.get("taskId"):
            raise RuntimeError("Prior submission outcome requires reconciliation")
        return previous
    refresh_quote_if_needed()
    body = ITEMS[str(seq)]["parameters"]["body"]
    assert body["version"] == item["version"] and body["faceCount"] == item["faceCount"] and body["outputFormat"] == item["outputFormat"]
    state = {"itemId": item["id"], "attemptId": item["id"] + "-generation-v1", "region": "cn", "accountRef": quote["account"]["accountRef"], "operation": "image-to-3d", "parameters": {k: v for k, v in body.items() if k != "img"}, "reference": item["reference"], "quoteId": quote["quote"]["quoteId"], "estimatedCredits": 20, "retryCount": 0, "createdAt": now(), "submissionState": "started", "status": "submitting"}
    state_path = ATTEMPTS / (item["id"] + ".json")
    with state_path.open("x", encoding="utf-8") as handle:
        json.dump(state, handle, indent=2)
    ledger["entries"][item["id"]]["state"] = "committed"
    ledger["updatedAt"] = now()
    save(ledger_path, ledger)
    try:
        task_id = client.create_image_to_3d_task(img=body["img"], version=body["version"], faceCount=body["faceCount"], outputFormat=body["outputFormat"], region="cn")
    except Exception as error:
        state.update(status="submission_rejected" if isinstance(error, client.ApiBusinessRejection) else "submission_unknown", errorType=type(error).__name__, errorCode=getattr(error, "code", None), updatedAt=now())
        save(state_path, state)
        emit(event="submission-stopped", item=item["id"], status=state["status"], errorType=state["errorType"], errorCode=state["errorCode"])
        raise RuntimeError("Submission requires review; task was not blindly retried") from None
    state.update(taskId=task_id, submissionState="submitted", status="running", updatedAt=now())
    save(state_path, state)
    ledger["entries"][item["id"]]["taskId"] = task_id
    save(ledger_path, ledger)
    emit(event="submitted", item=item["id"], taskId=task_id, targetFaces=item["faceCount"])
    return state
def download(item, state, provider):
    source = BASE / "artifacts" / item["id"]
    source.mkdir(parents=True, exist_ok=True)
    expected = client.generation_result_formats(item["version"], item["outputFormat"])
    outputs = client.parse_task_outputs(provider)
    paths = client.download_requested_models(outputs, str(source / item["id"]), expected)
    artifacts, inspected = [], []
    for fmt, (path, byte_count) in zip(expected, paths):
        result = delivery.inspect_artifact(path, fmt)
        inspected.append(result)
        artifacts.append({"path": path, "format": fmt})
    save(source / "structural-inspection.json", inspected)
    state.update(status="succeeded", lastKnownProviderStatus=3, downloadedArtifacts=artifacts, expectedFormats=expected, localCompletedAt=state.get("localCompletedAt", now()), updatedAt=now())
    save(ATTEMPTS / (item["id"] + ".json"), state)
    snapshot = {"schema": "lux3d.workflow-state/v2", "taskId": state["taskId"], "status": "succeeded", "lastKnownProviderStatus": 3, "request": {"operation": "image-to-3d", "region": "cn", "version": item["version"], "faceCount": item["faceCount"], "outputFormat": item["outputFormat"]}, "expectedFormats": expected, "downloadedArtifacts": artifacts, "createdAt": state["createdAt"], "updatedAt": state["updatedAt"], "localCompletedAt": state["localCompletedAt"]}
    save(ATTEMPTS / (item["id"] + "-delivery.json"), snapshot)
    emit(event="downloaded-and-structurally-inspected", item=item["id"], files=len(artifacts), bytes=sum(size for _, size in paths))
lock = WORK / "generation-run.lock"
try:
    with lock.open("x", encoding="utf-8") as handle:
        json.dump({"pid": os.getpid(), "startedAt": now()}, handle)
except FileExistsError:
    raise SystemExit("Another batch coordinator or a stale lock requires review")
try:
    pending = [(seq, item) for seq, item in enumerate(PLAN["items"], 1) if not (read_state(item) or {}).get("status") == "succeeded"]
    active = {}
    last_heartbeat = 0
    while pending or active:
        while pending and len(active) < 2:
            seq, item = pending.pop(0)
            active[item["id"]] = (item, submit(item, seq))
        for ident, (item, state) in list(active.items()):
            try:
                provider = client.get_task(state["taskId"], region="cn")
            except Exception as error:
                emit(event="query-retry-later", item=ident, errorType=type(error).__name__)
                continue
            save(ATTEMPTS / (ident + "-provider-private.json"), provider)
            old_status = state.get("lastKnownProviderStatus")
            state.update(lastKnownProviderStatus=provider["status"], updatedAt=now())
            save(ATTEMPTS / (ident + ".json"), state)
            if provider["status"] != old_status:
                emit(event="provider-status", item=ident, status=provider["status"])
            if provider["status"] == 3:
                try:
                    download(item, state, provider)
                except Exception as error:
                    state.update(status="delivery_error", errorType=type(error).__name__, updatedAt=now())
                    save(ATTEMPTS / (ident + ".json"), state)
                    emit(event="local-delivery-recovery-needed", item=ident, errorType=type(error).__name__)
                    raise RuntimeError("Successful generation retained; resume download without regeneration") from None
                del active[ident]
            elif provider["status"] in (4, 6):
                state["status"] = "failed" if provider["status"] == 4 else "cancelled"
                save(ATTEMPTS / (ident + ".json"), state)
                emit(event="provider-terminal", item=ident, status=state["status"])
                del active[ident]
        if time.monotonic() - last_heartbeat >= 40:
            complete = sum((read_state(i) or {}).get("status") == "succeeded" for i in PLAN["items"])
            emit(event="progress", completed=complete, total=TOTAL, active=list(active), pending=len(pending))
            last_heartbeat = time.monotonic()
        if pending or active:
            time.sleep(13)
    success = sum((read_state(i) or {}).get("status") == "succeeded" for i in PLAN["items"])
    emit(event="batch-generation-finished", successful=success, total=TOTAL, committedQuotedCredits=sum(e["estimatedCredits"] for e in ledger["entries"].values() if e["state"] == "committed"))
finally:
    lock.unlink(missing_ok=True)

