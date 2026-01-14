#if UNITY_EDITOR
namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 编辑器工具系统 - 入口索引
    /// 
    /// 本文件是 AI/新人快速定位入口的"地图文件"。
    /// 阅读顺序建议：Index → Window → Registry → Services
    /// 
    /// ═══════════════════════════════════════════════════════════════════
    /// 【快速定位手册】
    /// ═══════════════════════════════════════════════════════════════════
    /// 
    /// 1. 打开工具窗口
    ///    → YokiToolsMenu.Open()
    ///    → 快捷键: Ctrl+E
    /// 
    /// 2. 窗口壳（主界面）
    ///    → YokiToolsWindow.CreateGUI()
    ///    → 文件: Windows/YokiToolsWindow.cs
    /// 
    /// 3. 页面发现与注册
    ///    → YokiToolPageRegistry.Collect()
    ///    → 使用 [YokiToolPage] 特性标记页面
    /// 
    /// 4. 样式系统
    ///    → YokiStyleService.Apply(profile)
    ///    → 使用 [YokiEditorStyle] 特性注册 Kit 样式
    /// 
    /// 5. Kit 样式注册
    ///    → YokiEditorStyleAttribute
    ///    → 搜索 [YokiEditorStyle( 找到所有 Kit 样式入口
    /// 
    /// ═══════════════════════════════════════════════════════════════════
    /// 【目录结构】
    /// ═══════════════════════════════════════════════════════════════════
    /// 
    /// YokiEditorTools/
    /// ├── YokiEditorToolsIndex.cs      ← 你在这里（入口索引）
    /// │
    /// ├── EntryPoints/                  ← 所有 [MenuItem] 入口
    /// │   └── YokiToolsMenu.cs
    /// │
    /// ├── Windows/                      ← 窗口壳
    /// │   ├── YokiToolsWindow.cs        ← 主窗口
    /// │   ├── YokiToolsWindow.Sidebar.cs
    /// │   ├── YokiToolsWindow.Content.cs
    /// │   └── YokiPagePopoutWindow.cs   ← 弹出窗口
    /// │
    /// ├── Pages/                        ← 页面系统
    /// │   ├── IYokiToolPage.cs          ← 页面接口
    /// │   ├── YokiToolPageBase.cs       ← 页面基类
    /// │   └── Kits/                     ← 各 Kit 的页面
    /// │       ├── EventKit/
    /// │       ├── PoolKit/
    /// │       └── ...
    /// │
    /// ├── Registry/                     ← 注册中心
    /// │   ├── YokiToolPageAttribute.cs  ← 页面元数据特性
    /// │   ├── YokiToolPageRegistry.cs   ← 页面注册表
    /// │   ├── YokiEditorStyleAttribute.cs ← 样式注册特性
    /// │   └── YokiStyleRegistry.cs      ← 样式注册表
    /// │
    /// ├── Services/                     ← 服务层
    /// │   └── YokiStyleService.cs       ← 样式加载/缓存服务
    /// │
    /// └── Styling/                      ← 样式资源
    ///     ├── Tokens/
    ///     │   └── YokiTokens.uss        ← 设计令牌
    ///     ├── Core/
    ///     │   └── YokiCoreComponents.uss ← 核心组件样式
    ///     ├── Shell/
    ///     │   └── YokiWindowShell.uss   ← 窗口壳样式
    ///     └── Kits/                     ← Kit 专用样式
    ///         ├── EventKit/
    ///         └── ...
    /// 
    /// ═══════════════════════════════════════════════════════════════════
    /// 【依赖方向】（单向，禁止反向）
    /// ═══════════════════════════════════════════════════════════════════
    /// 
    /// Menu → Window → Registry → Page → Services
    ///                    ↓
    ///              StyleService
    /// 
    /// ═══════════════════════════════════════════════════════════════════
    /// 【命名规则】（便于 grep 检索）
    /// ═══════════════════════════════════════════════════════════════════
    /// 
    /// - 注册中心: *Registry*
    /// - 服务层:   *Service*
    /// - 页面:     *Page*
    /// - 窗口:     *Window*
    /// - 样式:     *Style* / *Styling*
    /// - 入口:     *Menu* / *Entry* / *Index*
    /// 
    /// </summary>
    public static class YokiEditorToolsIndex
    {
        /// <summary>
        /// 根命名空间
        /// </summary>
        public const string NAMESPACE = "YokiFrame.EditorTools";

        /// <summary>
        /// 样式资源根路径
        /// </summary>
        public static string STYLING_ROOT => YokiEditorPaths.GetStylingRoot();
    }
}
#endif
