# 第三方依赖

YokiFrame 的 Core 不强制绑定第三方库。只在项目确实需要对应能力时安装，并按 Kit 页面中的接入方式配置。

## 怎么选择

| 依赖 | 适合的场景 | 影响的能力 |
| --- | --- | --- |
| UniTask | 大量异步加载、取消和 UI 流程 | ResKit、ActionKit 的可选异步入口 |
| YooAsset | AssetBundle、RawFile、热更新或生产级资源管理 | ResKit、SceneKit |
| Luban | 配置表和本地化表生成 | TableKit、LocalizationKit |
| DOTween | 补间动画和演出流程 | ActionKit、UIKit |
| Unity Input System | 重绑定、多设备、手柄和触屏输入 | Unity 项目输入、UIKit 导航接入 |
| Nino | 大型或高频存档的二进制序列化 | SaveKit |
| ZString | 高频日志和字符串构建 | 可选性能优化 |

没有安装可选依赖时，YokiFrame 的纯 C# Core 仍可使用；对应扩展 API 不会出现在项目中。

## 安装入口

| 依赖 | 推荐入口 |
| --- | --- |
| UniTask | [Unity Git URL](https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask) |
| YooAsset | [GitHub](https://github.com/tuyoogame/YooAsset.git) |
| Luban | [GitHub](https://github.com/focus-creative-games/luban) |
| DOTween | [官网](https://dotween.demigiant.com/download.php) |
| Unity Input System | Unity Package Manager：`com.unity.inputsystem` |
| ZString | [Releases](https://github.com/Cysharp/ZString/releases) |
| Nino | [GitHub](https://github.com/JasonXuDeveloper/Nino.git) |

## 接入原则

- 先安装第三方库，再打开 Workbench 或重新导入 Unity 项目，让项目识别扩展。
- 资源库、异步库和动画库的具体 API 仍以各自官方文档为准；YokiFrame 只提供对应 Kit 的适配入口。
- YooAsset 的 package 生命周期仍由项目负责；YokiFrame 不会替项目销毁 package。
- ActionKit 的取消能力取决于底层任务是否提供取消令牌；普通 `Task` 不会因为 controller 取消而自动停止。
- Luban 由 TableKit/LocalizationKit 的生成流程调用；生成前先确认配置、数据目录和输出目录。
- Unity Input System 只负责输入接入；YokiFrame 不替代项目的 gameplay 输入设计。

## 相关资料

- 各 Kit 的使用前提和支持范围以对应 Kit 页面为准。
- [ActionKit](../03-Tool/ActionKit.md)
- [ResKit](../02-Core/ResKit.md)
- [SaveKit](../03-Tool/SaveKit.md)
- [TableKit](../03-Tool/TableKit.md)
- [UIKit](../03-Tool/UIKit.md)
