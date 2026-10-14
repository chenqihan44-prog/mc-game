方块生物全集（2026-10-13）

按已确认牛、猪、守护者细化版风格继续制作。新增 23 个模型，连同 3 个已确认小样共 26 个。
打开 index.html 离线查看模型、前/侧/斜视、逐帧动作、造型参考、实际三角面数。overview.html 为总览。
每个目录含带骨骼和基础动画的 GLB、打包贴图的 .blend、像素贴图、渲染图片及 24 张动作帧。
所有新几何与贴图由本地 Blender 脚本重新制作，未复用 Lux3D 生成网格，也未产生新的服务积分费用。
单模型为一份网格、一份材质、一个蒙皮。每个顶点刚性绑定一根骨骼，分段关节不会拉长三角面。
GLB 使用 Y-up，正面 -Z，模型最低点 Y=0；原始尺寸记录于 manifest.json。
仅怪物的光学装置与能量部件有独立发光遮罩；自然动物没有发光材质。
Blender NLA 轨道默认静音，取消对应轨道静音可编辑动画。地形等渲染环境没有导出到 GLB。
当前是模型交付，尚未替换游戏资源；移动接地、攻击命中、碰撞和光照仍需接入游戏后验证。
参考图保留以便审阅：先前高密度物种稿用于识别特征，最终方块画风以已确认的 3 个 v2 小样为准。

模型清单（三角面 / 动画）
牛 (cow): 2,372 / idle, walk, run, graze
猪 (pig): 1,832 / idle, walk, run, graze
林鹿 (deer): 1,852 / idle, walk, run, graze
野猪 (boar): 1,868 / idle, walk, run, charge
羊 (sheep): 2,300 / idle, walk, run, graze
鸡 (chicken): 1,440 / idle, walk, run, peck
马 (horse): 1,636 / idle, walk, run, graze
狼 (wolf): 1,752 / idle, walk, run, bite
猫 (cat): 1,720 / idle, walk, run, look
狐狸 (fox): 1,560 / idle, walk, run, look
犰狳 (armadillo): 2,628 / idle, walk, run, curl
鳕鱼 (cod): 880 / idle, swim, dart
鱿鱼 (squid): 2,204 / idle, swim, dart, pulse
海龟 (turtle): 2,012 / idle, walk, run, look
洞穴爬行者 (skitter): 2,812 / idle, walk, run, bite
荒野游魂 (husk): 2,100 / idle, walk, run, melee
遗迹哨兵 (sentinel): 4,316 / idle, walk, run, bolt
僵尸 (zombie): 1,484 / idle, walk, run, melee
骷髅 (skeleton): 2,172 / idle, walk, run, bolt
苦力怕 (creeper): 912 / idle, walk, run, explode
蜘蛛 (spider): 2,812 / idle, walk, run, bite
矿场哨长 (mine_warden): 4,700 / idle, walk, run, bolt
炉卫 (furnace_warden): 4,224 / idle, walk, run, charge
观测哨长 (observatory_warden): 4,516 / idle, walk, run, bolt
余烬守护者 (guardian): 5,748 / idle, walk, run, slam
熔心巨像 (colossus): 8,144 / idle, walk, run, slam

2026-10-14 整理：全集中的 GLB 已作为当前游戏资源接入。重复 ZIP 与自动 .blend1 备份已删除；Git 仅保存 .blend、GLB、像素贴图、造型参考及来源清单，画廊、渲染与动作帧为本地生成内容，需运行工具重新生成。
