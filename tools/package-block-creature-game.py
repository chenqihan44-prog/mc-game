"""Freeze the verified replacement release without test saves or developer files."""
import datetime, hashlib, json, pathlib, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
BUILD=ROOT/'builds/EmberLighthouse-block-creatures-20261013'
OUT=ROOT/'docs/previews/block-creature-game-2026-10-13'
WORK=ROOT/'.tools/block-model-replacement-20261013'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def rows(value):return value if isinstance(value,list) else [value]
core=read(ROOT/'test-results/core.json');assert core['failed']==0
source={key:read(ROOT/'test-results'/key/'validation.json') for key in ['creature-path','expansion','combat','creatures']}
assert all(value['passed'] for value in source.values())
capture=read(ROOT/'test-results/creature-motion/validation.json');assert capture['passed'] and capture['renderedCapture'] and capture['models']==26
graphics=rows(read(WORK/'final-capture-checks.json'));assert all(r['passed'] and r['exitCode']==0 and r['capture'] for r in graphics)
packaged=rows(read(WORK/'final-package-checks.json'));assert {r['check'] for r in packaged}=={'creature-motion-check','combat-check','smoke'}
assert all(r['passed'] and r['exitCode']==0 and pathlib.Path(r['engine']).resolve()==(BUILD/'EmberLighthouse.exe').resolve() for r in packaged)
motion=read(BUILD/'test-results/creature-motion/validation.json');assert motion['passed'] and motion['models']==26 and all(row['pixelSampling'] and row['footError']<.001 for row in motion['checks'])
combat=read(BUILD/'test-results/combat/validation.json');assert combat['passed']
multiplayer=read(ROOT/'test-results/multiplayer.verification.json');assert multiplayer['passed'] and multiplayer['packaged'] and multiplayer['actualProcesses']==4
assert pathlib.Path(multiplayer['engine']).resolve()==(BUILD/'EmberLighthouse.exe').resolve()
gallery=read(OUT/'gallery-verification.json');assert gallery['passed']
notes=(ROOT/'docs/方块生物模型替换-2026-10-13.md').read_text(encoding='utf-8')
(BUILD/'方块生物模型更新说明.md').write_text(notes+'\n开发资料与验证路径指源项目目录。试玩包完整解压后运行同目录 EmberLighthouse.exe。\n',encoding='utf-8')
(BUILD/'运行说明.md').write_text('''# 运行新版游戏

完整解压后双击 EmberLighthouse.exe。保留同目录 EmberLighthouse.pck 与 data_EmberLighthouse_windows_x86_64；无需另外安装 Godot 或 .NET。

本版将全部 26 种生物替换为已确认的细化方块模型，启用像素贴图、分段骨骼、按新腿长计算的步态和脚底接地补偿，保留已有攻击流程、界面和联机功能。

房主与朋友应一起使用此新版完整包。正常游戏沿用原有存档与玩家身份位置。
''',encoding='utf-8')
files=sorted(p for p in BUILD.iterdir() if p.is_file() and p.suffix in {'.exe','.pck','.md','.txt'})
files+=sorted(p for p in (BUILD/'data_EmberLighthouse_windows_x86_64').rglob('*') if p.is_file())
digests={p.relative_to(BUILD).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
archive=ROOT/'builds/EmberLighthouse-block-creatures-20261013试玩包.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as output:
 for path in files:output.write(path,path.relative_to(BUILD).as_posix())
with zipfile.ZipFile(archive) as output:
 assert output.testzip() is None and set(output.namelist())==set(digests)
 assert all(not p.startswith('test-results/') and '.tools' not in p for p in output.namelist())
 for p,digest in digests.items():assert hashlib.sha256(output.read(p)).hexdigest()==digest
record=dict(passed=True,createdAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),models=26,style='block-v2',
 release=str(BUILD),archive=str(archive),archiveBytes=archive.stat().st_size,archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
 files=digests,coreTests=len(core['results']),sourceChecks={key:value['passed'] for key,value in source.items()},
 nativeGraphicsChecks=graphics,packagedChecks=packaged,packagedMotion=motion,packagedCombat=True,
 localEnetHostAndThreeClients=True,multiplayerRunDirectory=multiplayer['runDirectory'],gallery=gallery,testSavesExcluded=True,
 installation=read(WORK/'installation.json'))
(ROOT/'test-results/block-model-release-verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
integration=read(OUT/'integration-record.json');integration['releaseVerification']=record
(OUT/'integration-record.json').write_text(json.dumps(integration,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(passed=True,models=26,archiveBytes=archive.stat().st_size,files=len(files),coreTests=len(core['results']))))
