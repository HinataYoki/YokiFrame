# Changelog

## 2.0.1

- TableKit 支持发现并校验新版 Luban 可选 Agent、MCP 与官方 Skill 路径，旧版 Luban 缺少这些路径时仍可验证和生成。
- Workbench 不再提供复制 AI 指引或 MCP 配置入口；配表需求由 YokiFrame Skill 读取 TableKit 配置并导入官方 Luban Skill。
- Skill 路径兼容 `Luban.Skill` 根目录及其 `skills` 子目录。

YokiFrame 2.x 是全新架构，不维护 1.x 及此前版本的历史更新记录。当前能力与边界请以 [README](README.md) 和 `Documentation~/` 中的文档为准。
