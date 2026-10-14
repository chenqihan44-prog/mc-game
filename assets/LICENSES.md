# 素材与许可

- 方块配色、程序化像素材质和构件、生物模型、界面控件与矢量图标、枝叶贴片、程序化合成音效与旋律：本项目内容；不引用外部音频素材。
- `assets/ui/lighthouse-sunset.png` 与 `lighthouse-night.png`：依据用户提供的 UI 参考图，使用 OpenAI 内置图像生成工具制作的无文字背景。生产提示词保存在 `assets/ui/generation-prompts.md`；按钮、文字、滑块及任务节点由 Godot 控件实时绘制。
- Windows 字体通过 Godot SystemFont 调用本机安装字体，不复制、不分发字体文件。
- `assets/creatures/*.glb`：依据本聊天的生物概念稿和已确认的牛、猪、守护者细化版，在本地 Blender 中重新制作的 26 种方块模型、程序化像素贴图、刚性分段骨骼与基础动画。当前资源不复用 Lux3D 网格；此前 Lux3D 原始交付保留在开发资料中。制作脚本见 `tools/build-block-creature-collection.py` 与两个小样制作脚本，来源、尺寸和摘要见 `data/creature_models.json`。发布包不包含 API 密钥。
- Godot Engine 4.6：MIT，官方许可 https://godotengine.org/license/ 。发布包须保留引擎版权和第三方许可文本。
- .NET SDK/Runtime：Microsoft .NET，MIT；SDK 仅供构建，发布运行时遵循其附带许可。

声音由 `ProceduralAudio` 在运行时生成 PCM，包含材质脚步、交互与战斗提示、生物预警、风与营火环境声、昼夜短旋律。它们不引用外部录音或歌曲；听感与长期体验仍需试玩评估。
