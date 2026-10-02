# 安装、更新、回滚

查看将要发生的变化时运行 `installer plan`。用户确认安装、更新、接管或回滚后，用相同参数运行 `installer apply`。

| 方式 | 装到哪里 | 怎么选 |
|---|---|---|
| `unity-local` | `Packages/com.hinatayoki.yokiframe` | 项目内一份本地包，与 Git URL 二选一 |
| `unity-git` | `Packages/manifest.json` | `--git-url` 使用绝对的 `file:`、`https:` 或 `git:` 地址 |
| `godot-local` | `addons/yokiframe` | 备份现有 add-on 后整份替换 |

Unity 包由 Installer 维护。计划列出用户改过的受管文件时，先把覆盖内容和用户确认下来。接管旧目录时，计划给出冲突文件、行号和 Kit。

执行后阅读返回的 rollback、conflicts、logs 和 evidence。需要重做时，先使用这次事务的回滚结果，再重新 plan。
