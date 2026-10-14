"""Freeze the verified playable release; omit validation saves and private workspaces."""
import datetime,hashlib,json,pathlib,shutil,zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
name='EmberLighthouse-creatures-20261013';BUILD=ROOT/'builds'/name
OUT=ROOT/'docs/previews/creature-game-integration-2026-10-13'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
core=read(ROOT/'test-results/core.json');assert core['failed']==0
reports={key:read(ROOT/'test-results'/key/'validation.json') for key in ['creatures','creature-path','expansion','combat']}
for report in reports.values():assert report['passed']
package_motion=read(BUILD/'test-results/creature-motion/validation.json');assert package_motion['passed'] and package_motion['models']==26
package_combat=read(BUILD/'test-results/combat/validation.json');assert package_combat['passed']
multiplayer=read(ROOT/'test-results/multiplayer.verification.json');assert multiplayer['passed'] and multiplayer['packaged'] and multiplayer['actualProcesses']==4 and pathlib.Path(multiplayer['engine']).resolve()==(BUILD/'EmberLighthouse.exe').resolve()
for check in ['creature-motion-check','combat-check','smoke']:
 output=(ROOT/'.tools/creature-rigging'/f'accepted-package-{check}.stdout.log').read_text(encoding='utf-8')
 assert ('CREATURE_MOTION_PASS' if check=='creature-motion-check' else 'COMBAT_PASS' if check=='combat-check' else 'SMOKE_PASS') in output
 errors=(ROOT/'.tools/creature-rigging'/f'accepted-package-{check}.stderr.log').read_text(encoding='utf-8')
 assert not any(term in errors for term in ['ERROR:','leaked','SEHException','FATAL:']),errors
notes=(ROOT/'docs/生物模型与动作接入-2026-10-13.md').read_text(encoding='utf-8')
notes=notes.replace('[查看游戏模型与动作示例](previews/creature-game-integration-2026-10-13/index.html)。','开发资料中的动作预览位于`docs/previews/creature-game-integration-2026-10-13/index.html`；本试玩包只包含游戏与运行说明。')
notes+='\n本文中builds、src、data与test-results等路径指开发目录。当前试玩包直接运行同目录EmberLighthouse.exe。\n'
(BUILD/'生物模型与动作更新说明.md').write_text(notes,encoding='utf-8')
(BUILD/'运行说明.md').write_text('# 运行游戏\n\n完整解压本包后双击 EmberLighthouse.exe。保留同目录 EmberLighthouse.pck 和 data_EmberLighthouse_windows_x86_64；无需另装Godot或.NET。\n\n本版包含26种新生物模型、骨骼步态和怪物攻击动作，并保留已有界面与联机修复。房主和朋友应使用同一新版完整包。存档与身份位置沿用游戏已有配置。\n\n模型源网格与自动绑定仍有局部精修空间；模型详情与检查范围见生物模型与动作更新说明。\n',encoding='utf-8')
files=sorted(path for path in BUILD.iterdir() if path.is_file() and path.suffix in ['.exe','.pck','.md','.txt'])
files+=sorted(path for path in (BUILD/'data_EmberLighthouse_windows_x86_64').rglob('*') if path.is_file())
digests={path.relative_to(BUILD).as_posix():hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
archive_path=ROOT/'builds'/f'{name}试玩包.zip'
with zipfile.ZipFile(archive_path,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=1) as archive:
 for path in files:archive.write(path,path.relative_to(BUILD).as_posix())
with zipfile.ZipFile(archive_path) as archive:
 assert set(archive.namelist())==set(digests) and archive.testzip() is None
 for path,digest in digests.items():assert hashlib.sha256(archive.read(path)).hexdigest()==digest
record={'passed':True,'createdAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'gameIntegrated':True,'models':26,'release':str(BUILD),'archive':str(archive_path),'archiveBytes':archive_path.stat().st_size,'archiveSha256':hashlib.sha256(archive_path.read_bytes()).hexdigest(),'archiveFiles':len(files),'binaryFileHashes':digests,'coreTests':len(core['results']),'sourceRegressionChecks':{key:report['passed'] for key,report in reports.items()},'packagedMotionVerification':package_motion,'packagedCombatVerification':package_combat,'packagedSmokeVerified':True,'packagedMultiplayerVerification':{'passed':True,'processes':4,'actualEnet':True,'runDirectory':multiplayer['runDirectory']},'validationSavesExcluded':True}
(ROOT/'test-results/creature-release-verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
integration=read(OUT/'integration-record.json');integration['releaseVerification']=record;integration['galleryVerification']=read(OUT/'gallery-verification.json')
(OUT/'integration-record.json').write_text(json.dumps(integration,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'passed':True,'models':26,'archiveFiles':len(files),'archiveBytes':archive_path.stat().st_size,'coreTests':len(core['results']),'packagedMultiplayerProcesses':4}))
