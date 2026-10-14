"""Freeze the new feedback release after native and packaged checks pass."""
import datetime,hashlib,json,pathlib,shutil,zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
BUILD=ROOT/'builds/EmberLighthouse-nature-feedback-20261014'
WORK=ROOT/'.tools/block-model-replacement-20261013'
OUT=ROOT/'docs/previews/nature-feedback-2026-10-14'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def rows(value):return value if isinstance(value,list) else [value]
core=read(ROOT/'test-results/core.json');assert core['failed']==0
packaged=rows(read(WORK/'nature-final-package-checks.json'))
assert {r['check'] for r in packaged}=={'nature-check','lighting-check','creature-motion-check','creature-path-check','combat-check','smoke'}
graphics=rows(read(WORK/'nature-final-graphics-checks.json'));assert {r['check'] for r in graphics}=={'nature-check','lighting-check'}
for r in packaged+graphics:assert r['passed'] and r['exitCode']==0 and r['packaged'] and pathlib.Path(r['engine']).resolve()==(BUILD/'EmberLighthouse.exe').resolve()
assert all(r['capture'] for r in graphics)
lighting=read(BUILD/'test-results/lighting/validation.json');nature=read(BUILD/'test-results/nature-feedback/validation.json')
assert lighting['passed'] and lighting['rendered'] and lighting['mainAndOffhand'] and lighting['heldMainLuminance']>.03 and lighting['heldOffhandLuminance']>.03
assert nature['passed'] and nature['rendered'] and nature['backupUpgradeButton'] and nature['projectileImpact'] and nature['replicaFlash']
multiplayer=read(ROOT/'test-results/multiplayer.verification.json');assert multiplayer['passed'] and multiplayer['packaged'] and multiplayer['actualProcesses']==4
assert pathlib.Path(multiplayer['engine']).resolve()==(BUILD/'EmberLighthouse.exe').resolve()
assert read(pathlib.Path(multiplayer['runDirectory'])/'combat-feedback-verified.json')['passed']
export=read(ROOT/'test-results/export-verification.json');assert export['exitCode']==0 and not export['otherEngineErrors'] and pathlib.Path(export['output']).resolve()==BUILD.resolve()
density_paths=list((ROOT/'test-results').glob('saves-*/nature-density/validation.json'));density=read(max(density_paths,key=lambda p:p.stat().st_mtime));assert density['passed']
OUT.mkdir(parents=True,exist_ok=True)
images=[('分层树冠与四种树木','nature-feedback/layered-trees.png'),('近战命中与剩余生命','nature-feedback/melee-hit.png'),('箭矢实际命中','nature-feedback/arrow-hit.png'),('矿洞 · 空手','lighting/cave-dark.png'),('矿洞 · 主手火把','lighting/cave-held-main.png'),('矿洞 · 副手火把','lighting/cave-held-offhand.png')]
for title,path in images:shutil.copy2(BUILD/'test-results'/path,OUT/pathlib.Path(path).name)
figures=''.join(f'<figure><img src="{pathlib.Path(path).name}" alt="{title}"><figcaption>{title}</figcaption></figure>' for title,path in images)
(OUT/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>火把、树木与命中反馈 · 实际游戏渲染</title><style>body{background:#171c21;color:#ede4d5;max-width:1440px;margin:auto;padding:28px;font:17px/1.7 system-ui}a{color:#e4c17c}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,500px),1fr));gap:24px}figure{margin:0}img{width:100%;display:block;border-radius:10px}figcaption{padding:8px 0}p{max-width:1000px}</style><h1>火把、树木与命中反馈</h1><p>以下截图来自最终 Windows 试玩包的实际 Godot 渲染。四种树木在隔离场景中排列展示，矿洞对比使用相同相机与封闭环境；命中截图来自真实攻击和投射物碰撞。</p><p>旧世界：世界管理 → 更新树木与矿藏 → 备份并更新。手持照明与命中反馈直接生效。</p><p><a href="../../../builds/EmberLighthouse-nature-feedback-20261014试玩包.zip">完整试玩包</a> · <a href="../../火把树木与命中反馈-2026-10-14.md">更新说明</a></p><main>'''+figures+'</main></html>',encoding='utf-8')
(BUILD/'火把树木与命中反馈更新说明.md').write_text((ROOT/'docs/火把树木与命中反馈-2026-10-14.md').read_text(encoding='utf-8')+'\n开发资料与验证路径指源项目目录。\n',encoding='utf-8')
(BUILD/'运行说明.md').write_text('''# 方野：余烬灯塔 · 运行新版

完整解压后双击 EmberLighthouse.exe。保留同目录 EmberLighthouse.pck 与 data_EmberLighthouse_windows_x86_64，无需另外安装编辑器或 .NET。

主手或副手拿火把、提灯会照亮附近。实际攻击命中显示伤害与剩余生命。新世界有更少的煤矿和铜矿，以及分层树冠和粗细不同的树干。

旧世界的照明和命中反馈直接生效。应用新树木与矿藏：世界管理 → 更新树木与矿藏 → 备份并更新。原档自动备份，玩家建筑和挖掘记录保留；建筑和已砍树木附近保留原自然外观。

WASD 移动，Space 跳跃，Shift 蹲伏，Ctrl 疾跑；左键挖掘或攻击，右键使用或放置，数字 1–9 选择快捷栏，F 交换主副手，E 背包，Q 使用设施，M 地图，Esc 暂停。

联机时房主与朋友均使用本新版完整包，原存档和玩家身份沿用现有位置。本机四进程联机验证通过，真实异地联机仍取决于网络连通情况。
''',encoding='utf-8')
files=sorted(p for p in BUILD.iterdir() if p.is_file() and p.suffix in {'.exe','.pck','.md','.txt'})+sorted(p for p in (BUILD/'data_EmberLighthouse_windows_x86_64').rglob('*') if p.is_file())
digests={p.relative_to(BUILD).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
archive=ROOT/'builds/EmberLighthouse-nature-feedback-20261014试玩包.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as out:
 for p in files:out.write(p,p.relative_to(BUILD).as_posix())
with zipfile.ZipFile(archive) as out:
 assert out.testzip() is None and set(out.namelist())==set(digests)
 for path,digest in digests.items():assert hashlib.sha256(out.read(path)).hexdigest()==digest
assert all(not p.startswith('test-results/') and '.tools' not in p for p in digests)
record=dict(passed=True,createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),release=str(BUILD),archive=str(archive),archiveBytes=archive.stat().st_size,archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),files=digests,coreTests=len(core['results']),oreSampling=density,packagedChecks=packaged,nativeGraphicsChecks=graphics,lighting=lighting,nature=nature,multiplayerDirectory=multiplayer['runDirectory'],localHostAndThreeClients=True,guestHitFeedback=True,export=export,images=[p for _,p in images],testSavesExcluded=True)
(ROOT/'test-results/nature-feedback-release-verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=True,archiveBytes=archive.stat().st_size,coreTests=len(core['results']),nativeImages=len(images),files=len(files))))
