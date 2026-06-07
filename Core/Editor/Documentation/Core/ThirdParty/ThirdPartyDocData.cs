#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 第三方库索引文档 — 列出 YokiFrame 依赖的所有第三方库，
    /// 包含仓库地址、文档链接、框架中的作用以及使用方。
    /// </summary>
    internal sealed class ThirdPartyDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "第三方库索引",
                Icon = KitIcons.PACKAGE,
                Category = "REFERENCE",
                Description = "YokiFrame 依赖的所有第三方库一览，标注必需/可选，方便用户选择安装。",
                Keywords = new List<string> { "第三方", "依赖", "插件", "Package" },
                Sections = new List<DocSection>
                {
                    CreateUniTaskSection(),
                    CreateYooAssetSection(),
                    CreateLubanSection(),
                    CreateFmodSection(),
                    CreateDOTweenSection(),
                    CreateInputSystemSection(),
                    CreateZStringSection(),
                    CreateNinoSection(),
                }
            };
        }

        /// <summary>
        /// UniTask — 零 GC 异步操作库，YokiFrame 异步基础设施。
        /// 类型：推荐安装（缺省时异步 API 不可用，降级为同步回调）
        /// </summary>
        private static DocSection CreateUniTaskSection()
        {
            return new DocSection
            {
                Title = "UniTask",
                Description = "Cysharp 开源的零 GC 异步库，为 Unity 量身定制的 async/await 实现。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• 所有异步 API 的基础（加载资源、切换场景、存档等）\n"
                    + "• 提供 CancellationToken 生命周期绑定\n"
                    + "• LINQ to UniTask 异步查询\n\n"
                    + "使用方：ActionKit、AudioKit、ResKit、SaveKit、SceneKit、UIKit、InputKit、LocalizationKit\n\n"
                    + "类型：推荐安装",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/Cysharp/UniTask" },
                    new() { Name = "文档", Url = "https://github.com/Cysharp/UniTask?tab=readme-ov-file#readme" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// 方式1：通过 Package Manager → Add package from git URL\nhttps://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask\n\n// 方式2：通过 OpenUPM\nopenupm add com.cysharp.unitask",
                        Explanation = "推荐使用 git URL 方式安装，确保获得最新版本。"
                    }
                }
            };
        }

        /// <summary>
        /// YooAsset — 资产管理与热更新框架。
        /// 类型：推荐安装（缺省时 ResKit 使用内置简易加载器）
        /// </summary>
        private static DocSection CreateYooAssetSection()
        {
            return new DocSection
            {
                Title = "YooAsset",
                Description = "TuyooGame 开源的 Unity 资产管理系统，支持 AssetBundle、RawFile、场景加载与热更新。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• ResKit 的主要资源加载后端\n"
                    + "• 支持资源包加密、离线模式、Host 模式、增量更新\n"
                    + "• UIKit/SceneKit 通过 ResKit 间接使用 YooAsset 加载资源\n\n"
                    + "使用方：ResKit、SceneKit、UIKit\n\n"
                    + "类型：推荐安装",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/tuyoogame/YooAsset" },
                    new() { Name = "文档", Url = "https://www.yooasset.com/" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// Package Manager → Add package from git URL\nhttps://github.com/tuyoogame/YooAsset.git",
                        Explanation = "导入后 DependecyDefineService 自动添加 YOKIFRAME_YOOASSET_SUPPORT 宏。"
                    }
                }
            };
        }

        /// <summary>
        /// Luban — 配置表工具，TableKit 的运行基础。
        /// 类型：必需（TableKit 完全依赖 Luban 生成代码）
        /// </summary>
        private static DocSection CreateLubanSection()
        {
            return new DocSection
            {
                Title = "Luban",
                Description = "Focus Creative Games 开源的配置表解决方案，支持 Excel/CSV → C#/JSON/Lua 多输出。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• TableKit 核心依赖 — 无 Luban 则 TableKit 无法使用\n"
                    + "• 负责配置表的解析与代码生成\n\n"
                    + "使用方：TableKit（必需）\n\n"
                    + "类型：必需",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/focus-creative-games/luban" },
                    new() { Name = "文档", Url = "https://luban.doc.code-philosophy.com/" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// Package Manager → Add package from git URL\nhttps://github.com/focus-creative-games/luban.git",
                        Explanation = "导入后 DependecyDefineService 自动添加 YOKIFRAME_LUBAN_SUPPORT 宏。未安装 Luban 时 TableKit 窗口不显示。"
                    }
                }
            };
        }

        /// <summary>
        /// FMOD — 专业游戏音频中间件。
        /// 类型：可选（AudioKit 内置 Unity AudioSource 后端，安装 FMOD 后可切换）
        /// </summary>
        private static DocSection CreateFmodSection()
        {
            return new DocSection
            {
                Title = "FMOD",
                Description = "Firelight Technologies 的游戏音频引擎，提供高级混音、动态音乐、3D 空间音频。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• AudioKit 的可选音频后端（替换默认 Unity AudioSource）\n"
                    + "• 安装后 AudioKit 自动检测并提供 FmodAudioBackend\n\n"
                    + "使用方：AudioKit（可选）\n\n"
                    + "类型：可选",
                Links = new List<PluginLink>
                {
                    new() { Name = "官网", Url = "https://www.fmod.com/download#fmodforunity" },
                    new() { Name = "文档", Url = "https://www.fmod.com/docs/2.02/unity/" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Asset Store / 官网）",
                        Code = "// 方式1：Unity Asset Store 搜索 \"FMOD\"\n// 方式2：FMOD 官网下载 Unity Integration\nhttps://www.fmod.com/download#fmodforunity\n\n// 导入后 DependecyDefineService 自动添加 YOKIFRAME_FMOD_SUPPORT 宏",
                        Explanation = "FMOD 需要注册账号并下载对应平台的 Unity Integration 包。"
                    }
                }
            };
        }

        /// <summary>
        /// DOTween — 高性能动画补间库。
        /// 类型：可选（缺省时 ActionKit/UIKit 动画降级为内置实现）
        /// </summary>
        private static DocSection CreateDOTweenSection()
        {
            return new DocSection
            {
                Title = "DOTween",
                Description = "Demigiant 开发的高性能 Unity 补间动画引擎，支持 Transform、UI、材质、音频等几乎所有类型。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• ActionKit 可选集成（通过 DOTweenAction 直接驱动 DOTween）\n"
                    + "• UIKit 可选集成（面板打开/关闭动画）\n\n"
                    + "使用方：ActionKit（可选）、UIKit（可选）\n\n"
                    + "类型：可选",
                Links = new List<PluginLink>
                {
                    new() { Name = "官网", Url = "https://dotween.demigiant.com/download.php" },
                    new() { Name = "文档", Url = "https://dotween.demigiant.com/documentation.php" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Asset Store / 官网）",
                        Code = "// 方式1：Unity Asset Store 搜索 \"DOTween\"（免费）\n// 方式2：DOTween 官网下载\nhttps://dotween.demigiant.com/download.php\n\n// 导入后运行 Tools → Demigiant → DOTween → Setup\n// DependecyDefineService 自动添加 YOKIFRAME_DOTWEEN_SUPPORT 宏",
                        Explanation = "安装后需运行一次 Setup 以确保平台兼容。"
                    }
                }
            };
        }

        /// <summary>
        /// Unity Input System — 新版输入框架。
        /// 类型：可选（InputKit 支持 Legacy Input 降级，但新功能需要 Input System）
        /// </summary>
        private static DocSection CreateInputSystemSection()
        {
            return new DocSection
            {
                Title = "Unity Input System",
                Description = "Unity 官方的现代输入系统，支持设备抽象、输入重绑、触屏、手柄等。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• InputKit 的重绑（Rebind）功能需要 Input System\n"
                    + "• InputKit 的上下文（Context）功能支持 Input System 输入映射\n"
                    + "• 不安装时 InputKit 使用 Legacy Input 降级方案\n\n"
                    + "使用方：InputKit（可选增强）\n\n"
                    + "类型：可选",
                Links = new List<PluginLink>
                {
                    new() { Name = "文档", Url = "https://docs.unity3d.com/Packages/com.unity.inputsystem@1.0/manual/Installation.html" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// Window → Package Manager → Unity Registry → Input System → Install\n// 或：Package Manager → Add package by name\ncom.unity.inputsystem\n\n// 导入后 DependecyDefineService 自动添加 YOKIFRAME_INPUTSYSTEM_SUPPORT 宏",
                        Explanation = "Unity 官方包，通过 Package Manager 直接安装。首次安装会提示启用 Input System 后端。"
                    }
                }
            };
        }

        /// <summary>
        /// ZString — 零 GC 字符串构建器。
        /// 类型：推荐安装（缺省时降级为 StringBuilder，存在 GC 分配）
        /// </summary>
        private static DocSection CreateZStringSection()
        {
            return new DocSection
            {
                Title = "ZString",
                Description = "Cysharp 开源的零 GC 字符串工具库，基于 Utf8ValueStringBuilder 和 Span 实现。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• 全域热路径字符串拼接替代（StringBuilder / string.Format / $ 插值）\n"
                    + "• YokiFrame 内部使用 ZString 优化所有高频字符串操作\n"
                    + "• 不安装时降级为 StringBuilder，存在少量 GC 分配\n\n"
                    + "使用方：全局（性能优化层）\n\n"
                    + "类型：推荐安装",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/Cysharp/ZString" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// Package Manager → Add package from git URL\nhttps://github.com/Cysharp/ZString.git\n\n// DependecyDefineService 自动添加 YOKIFRAME_ZSTRING_SUPPORT 宏",
                        Explanation = "推荐所有项目安装，显著降低字符串相关 GC 分配。"
                    }
                }
            };
        }

        /// <summary>
        /// Nino — 高性能二进制序列化库。
        /// 类型：可选（缺省时 SaveKit 使用 JSON 序列化）
        /// </summary>
        private static DocSection CreateNinoSection()
        {
            return new DocSection
            {
                Title = "Nino",
                Description = "JasonXuDeveloper 开源的高性能二进制序列化库，零 GC、零反射、纯 AOT 友好。\n\n"
                    + "在 YokiFrame 中的作用：\n"
                    + "• SaveKit 的可选序列化后端（替代默认 JSON）\n"
                    + "• 提供更快的存档读写速度和更小的文件体积\n"
                    + "• 不安装时 SaveKit 使用内置 JSON 序列化\n\n"
                    + "使用方：SaveKit（可选增强）\n\n"
                    + "类型：可选",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/JasonXuDeveloper/Nino" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（Package Manager）",
                        Code = "// Package Manager → Add package from git URL\nhttps://github.com/JasonXuDeveloper/Nino.git\n\n// DependecyDefineService 自动添加 YOKIFRAME_NINO_SUPPORT 宏",
                        Explanation = "如需存档高性能需求（大量数据/高频写入），推荐安装 Nino。"
                    }
                }
            };
        }
    }
}
#endif
