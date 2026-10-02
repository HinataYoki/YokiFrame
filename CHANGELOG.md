# Changelog

## 2.0.3

- ResKit 的 YooAsset 集成支持多包自动探测：普通路径按登记顺序查找，`package:{包名}/{location}` 显式指定且失败不回退。
- YooAsset 联网初始化支持 RemoteOnly、RemoteThenCached、RemoteThenOffline，并兼容 V2/V3。远端版本请求可由 `AppendTimestampToVersionRequest` 控制是否追加时间戳，默认开启以绕过 CDN 缓存；鉴权签名覆盖完整 URL 或网关拒绝未知查询参数时关闭。
- UIKit 按挂载身份确认类型迁移与遗留删除，遗留代码检查限定在当前生成目录。
- SaveKit 刷新时解析宿主用户目录并扫描存档文件。
- Godot 安装后的插件可以正常编译；共享设置写入 `project.godot`，内容没有变化时不重写。
- Installer 优化安装目标选择。Godot 宿主按需注册，FileBridge 时钟与引擎页面保持一致。

## 2.0.2

- UIKit 代码生成与目录迁移能力完善。


## 2.0.1

- TableKit 支持发现并校验新版 Luban 可选 Agent、MCP 与官方 Skill 路径，旧版 Luban 缺少这些路径时仍可验证和生成。
- Workbench 不再提供复制 AI 指引或 MCP 配置入口；配表需求由 YokiFrame Skill 读取 TableKit 配置并导入官方 Luban Skill。
- Skill 路径兼容 `Luban.Skill` 根目录及其 `skills` 子目录。

YokiFrame 2.x 是全新架构，不维护 1.x 及此前版本的历史更新记录。当前能力与边界请以 [README](README.md) 和 `Documentation~/` 中的文档为准。
