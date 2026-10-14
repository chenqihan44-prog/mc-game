"""Validate real Blender renders and build an offline image gallery."""
import hashlib, html, json, pathlib, re, struct, zipfile, zlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/"docs/previews/blender-creature-renders-2026-10-13"
catalog=json.loads((ROOT/"data/creatures.json").read_text(encoding="utf-8-sig"))
blend_verification=json.loads((OUT/"blend-file-verification.json").read_text(encoding="utf-8"))
assert blend_verification["passed"] and blend_verification["models"]==26
def check_png(path):
    data=path.read_bytes()
    assert data[:8]==b"\x89PNG\r\n\x1a\n"
    offset=8;idat=[];dims=None;ended=False
    while offset<len(data):
        size=struct.unpack_from(">I",data,offset)[0]
        kind=data[offset+4:offset+8];payload=data[offset+8:offset+8+size]
        crc=struct.unpack_from(">I",data,offset+8+size)[0]
        assert zlib.crc32(kind+payload)&0xffffffff==crc
        if kind==b"IHDR":dims=struct.unpack(">IIBBBBB",payload)
        if kind==b"IDAT":idat.append(payload)
        if kind==b"IEND":ended=True
        offset+=size+12
    assert ended and dims[:2]==(1536,1536)
    width,height,depth,color,_,_,interlace=dims
    assert depth==8 and color in (2,6) and interlace==0
    channels=4 if color==6 else 3
    assert len(zlib.decompress(b"".join(idat)))==height*(width*channels+1)
    return {"path":str(path.relative_to(OUT)),"width":width,"height":height,"sha256":hashlib.sha256(data).hexdigest()}
records=[];checks=[]
for item in catalog:
    ident=item["Id"];assert re.fullmatch(r"[a-z_]+",ident)
    record=json.loads((OUT/ident/"render-record.json").read_text(encoding="utf-8"))
    assert record["id"]==ident and record["sourceMeshCount"]>0 and record["engine"]=="Cycles" and record["sourceMaterialsPreserved"]
    assert len(record["renders"])==2
    for label in ["three-quarter","side"]:checks.append(check_png(OUT/ident/(ident+"-"+label+".png")))
    work=OUT/ident/(ident+".blend")
    with work.open("rb") as handle:header=handle.read(8)
    assert header.startswith(b"BLENDER") or header.startswith(b"\x28\xb5\x2f\xfd")
    records.append(record)
report={"models":len(records),"images":len(checks),"resolution":[1536,1536],"blender":"5.2.2 LTS","engine":"Cycles","gameIntegrated":False,"allImagesDecode":True,"imageChecks":checks,"records":records}
(OUT/"render-manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
cards=[]
for item in catalog:
    ident=item["Id"];name=html.escape(item["Name"])
    cards.append(f'<article id="card-{ident}"><h2>{name}<small>{ident}</small></h2><img loading="lazy" width="1536" height="1536" src="{ident}/{ident}-three-quarter.png" alt="{name} Blender实际渲染" data-id="{ident}"><div class="tools"><button data-id="{ident}" data-angle="three-quarter" aria-pressed="true">主视角</button><button data-id="{ident}" data-angle="side" aria-pressed="false">侧视角</button><a href="{ident}/{ident}.blend" download>Blender工作文件</a></div></article>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>26种生物 · Blender渲染</title><style>*{box-sizing:border-box}body{margin:0;background:#141922;color:#eee;font:16px system-ui,"Microsoft YaHei",sans-serif}header,main,footer{max-width:1500px;margin:auto;padding:28px}h1{font-size:32px;margin:8px 0}header p,footer{color:#bfc7d4;line-height:1.65}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:22px}article{background:#242c39;border:1px solid #394457;border-radius:14px;overflow:hidden}h2{font-size:20px;margin:18px}small{float:right;font-size:12px;color:#a8b9ce}img{display:block;width:100%;height:auto;aspect-ratio:1}.tools{display:flex;gap:8px;align-items:center;padding:14px;flex-wrap:wrap}button,a{font:inherit;border:1px solid #64778c;border-radius:7px;padding:7px 10px;background:transparent;color:#d7e7f5;cursor:pointer;text-decoration:none}button[aria-pressed=true]{background:#b3925c;color:#111;border-color:#b3925c}.tools a{font-size:13px;margin-left:auto}@media(max-width:1000px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:600px){.grid{grid-template-columns:1fr}header,main,footer{padding:16px}}</style><header><p>方野：余烬灯塔 · 独立模型展示</p><h1>26种生物 · Blender双角度渲染</h1><p>52张1536×1536图片，由实际生成的GLB在Blender 5.2.2 LTS / Cycles中渲染。每只可切换主视角与侧视角。</p><p>本页为静态渲染图。模型尚未加入游戏，骨骼、动画及局部形体／材质整理仍待后续处理。</p></header><main class="grid">'''+''.join(cards)+'''</main><footer>保留源模型材质。模型只在渲染场景中统一展示尺寸，因此图片不表示游戏中的相对体型。Blender工作文件包含模型、摄影棚灯光、地面及相机；游戏源GLB未改动。</footer><script>document.querySelectorAll('button[data-angle]').forEach(b=>b.addEventListener('click',()=>{const card=document.getElementById('card-'+b.dataset.id);card.querySelector('img').src=b.dataset.id+'/'+b.dataset.id+'-'+b.dataset.angle+'.png';card.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));}));</script></html>'''
(OUT/"index.html").write_text(page,encoding="utf-8")
(OUT/"README.md").write_text("# Blender生物渲染交付\n\n26个源模型，52张1536×1536 PNG，Blender 5.2.2 LTS / Cycles GPU，32采样与降噪。index.html是静态图浏览页；可切换两种相机角度。render-manifest.json包含实际来源、时间、图片校验及每个工作文件记录。\n\n源GLB及其材质保持不变，仅渲染场景统一尺寸、添加地面、灯光和相机。每个 .blend 工作文件使用已打包贴图。模型未接入游戏，未制作骨骼动画，AI网格的细节与概念图存在差异。\n",encoding="utf-8")
image_archive=OUT/"blender-renders-26.zip"
with zipfile.ZipFile(image_archive,"x",compression=zipfile.ZIP_DEFLATED,compresslevel=1) as archive:
    for item in catalog:
        ident=item["Id"]
        for name in [ident+"-three-quarter.png",ident+"-side.png","render-record.json"]:
            path=OUT/ident/name;archive.write(path,path.relative_to(OUT).as_posix())
    for name in ["index.html","README.md","render-manifest.json","blend-file-verification.json"]:archive.write(OUT/name,name)
work_archive=OUT/"blender-workfiles-26.zip"
with zipfile.ZipFile(work_archive,"x",compression=zipfile.ZIP_DEFLATED,compresslevel=1) as archive:
    for item in catalog:
        ident=item["Id"];path=OUT/ident/(ident+".blend")
        archive.write(path,path.relative_to(OUT).as_posix())
    archive.write(ROOT/"tools/blender-render-creatures.py","blender-render-creatures.py")
    archive.write(OUT/"README.md","README.md")
for path in [image_archive,work_archive]:
    with zipfile.ZipFile(path) as archive:assert archive.testzip() is None
print(json.dumps({"models":len(records),"images":len(checks),"gallery":str(OUT/"index.html"),"pngArchiveBytes":image_archive.stat().st_size,"blendArchiveBytes":work_archive.stat().st_size,"gameIntegrated":False}))

