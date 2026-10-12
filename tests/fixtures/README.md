# 历史兼容性测试输入

这些文件是固定的回归输入，需纳入 Git；它们不是当前编译生成的 DLL 或用户个人存档。

- `published-format2/EmberLighthouse.dll`：旧 `EmberLighthouse-population` 版本，保存格式 2。
- `published-format3/EmberLighthouse.dll`：旧 `EmberLighthouse-feedback` 版本，保存格式 3。
- `published-format4/EmberLighthouse.dll`：旧 `EmberLighthouse-presentation` 版本，保存格式 4，供历史格式 3→4 迁移检查使用。
- `published-format4/data`：取自 Git 提交 `d30023b` 的格式 4 时期目录数据，供旧 DLL 读取；当前目录新增的鞘翅等物品无法由旧 DLL 校验，历史测试不能使用当前 `data`。
- 三个目录内的 `GodotSharp.dll`：对应 Godot 4.6 的托管依赖，许可见仓库中的 `assets/GODOT_LICENSE.txt` 和 `assets/GODOT_COPYRIGHT.txt`。
- `published-format3/worlds/860b32146dd04be2b2e1aba37057c630`：旧发布版产生的自动化测试世界，供真实格式 3 迁移验证使用；脚本复制到 `test-results` 后测试，不修改此输入。

核心测试运行后可执行 `tools/test-save-compat.ps1 -OldFormat 2` 和 `-OldFormat 3`。`tools/test-published-migration.ps1` 默认读取固定的 `published-format4` DLL，检查原有格式 3→4 迁移与原始备份。当前源码已采用更新的存档格式，此历史检查不替代当前版本的核心迁移测试。
