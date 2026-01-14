#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 文档页面 - 带语法高亮的详细 API 文档
    /// </summary>
    [YokiToolPage(
        kit: "Documentation",
        name: "文档",
        icon: KitIcons.DOCUMENTATION,
        priority: 0,
        category: YokiPageCategory.Documentation)]
    public partial class DocumentationToolPage : YokiToolPageBase
    {

        private ScrollView mTocScrollView;
        private ScrollView mContentScrollView;
        private VisualElement mRootContainer; // Toast 显示的固定容器
        private readonly List<DocModule> mModules = new();
        private readonly Dictionary<VisualElement, int> mTocItemMap = new();
        private VisualElement mSelectedTocItem;
        private VisualElement mHighlightIndicator;
        private VisualElement mTocItemsContainer;

        // 右侧本页导航
        private VisualElement mOnThisPagePanel;
        private VisualElement mOnThisPageContainer;
        private readonly List<(string title, VisualElement element, int level)> mCurrentHeadings = new();
        private VisualElement mSelectedHeadingItem;

        // 导航项与内容元素的映射（用于滚动同步）
        private readonly List<(VisualElement navItem, VisualElement contentElement)> mHeadingNavMap = new();
        private bool mIsScrollingByClick; // 防止点击滚动时触发滚动监听

        // 响应式布局阈值
        private const float ON_THIS_PAGE_MIN_WIDTH = 1200f;

        #region 颜色主题

        private static class Theme
        {
            // 背景色
            public static readonly Color BgPrimary = new(0.16f, 0.16f, 0.16f);
            public static readonly Color BgSecondary = new(0.14f, 0.14f, 0.14f);
            public static readonly Color BgTertiary = new(0.12f, 0.12f, 0.12f);
            public static readonly Color BgCode = new(0.1f, 0.1f, 0.1f);
            public static readonly Color BgHover = new(0.2f, 0.2f, 0.2f);
            public static readonly Color BgSelected = new(0.24f, 0.37f, 0.58f);

            // 强调色
            public static readonly Color AccentBlue = new(0.34f, 0.61f, 0.84f);
            public static readonly Color AccentGreen = new(0.4f, 0.7f, 0.4f);
            public static readonly Color AccentOrange = new(0.9f, 0.6f, 0.3f);
            public static readonly Color AccentPurple = new(0.7f, 0.5f, 0.8f);
            public static readonly Color AccentRed = new(0.9f, 0.4f, 0.4f);
            public static readonly Color AccentYellow = new(0.9f, 0.8f, 0.4f);

            // 文字色
            public static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f);
            public static readonly Color TextSecondary = new(0.8f, 0.8f, 0.8f);
            public static readonly Color TextMuted = new(0.6f, 0.6f, 0.6f);
            public static readonly Color TextDim = new(0.5f, 0.5f, 0.5f);

            // 边框色
            public static readonly Color Border = new(0.25f, 0.25f, 0.25f);
            public static readonly Color BorderDark = new(0.1f, 0.1f, 0.1f);

            // 分类颜色
            public static readonly Color CategoryCore = new(0.55f, 0.7f, 0.85f);
            public static readonly Color CategoryKit = new(0.55f, 0.75f, 0.6f);
            public static readonly Color CategoryTools = new(0.85f, 0.7f, 0.55f);

            // 分类背景色
            public static readonly Color CategoryCoreBg = new(0.14f, 0.15f, 0.17f);
            public static readonly Color CategoryKitBg = new(0.14f, 0.16f, 0.15f);
            public static readonly Color CategoryToolsBg = new(0.16f, 0.15f, 0.14f);
        }

        #endregion

        protected override void BuildUI(VisualElement root)
        {
            InitializeDocumentation();

            // 保存根容器引用（用于 Toast 等固定位置元素）
            mRootContainer = root;

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            root.Add(container);

            // 左侧目录面板
            container.Add(CreateTocPanel());

            // 中间内容区域
            mContentScrollView = new ScrollView();
            mContentScrollView.style.flexGrow = 1;
            mContentScrollView.style.backgroundColor = new StyleColor(Theme.BgPrimary);
            mContentScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            // 确保滚动条可正常拖动
            YokiFrameUIComponents.FixScrollViewDragger(mContentScrollView);

            // 监听滚动事件，同步更新右侧导航高亮
            mContentScrollView.verticalScroller.valueChanged += OnContentScrollChanged;

            container.Add(mContentScrollView);

            // 右侧本页导航面板
            container.Add(CreateOnThisPagePanel());

            // 监听窗口大小变化，响应式显示/隐藏右侧面板
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            if (mModules.Count > 0) SelectModule(0);
        }

        #region 文档数据初始化

        private void InitializeDocumentation()
        {
            mModules.Clear();

            // Architecture
            mModules.Add(CreateArchitectureDoc());

            // Core Kit
            mModules.Add(CreateEventKitDoc());
            mModules.Add(CreateFsmKitDoc());
            mModules.Add(CreatePoolKitDoc());
            mModules.Add(CreateSingletonKitDoc());
            mModules.Add(CreateResKitDoc());
            mModules.Add(CreateLogKitDoc());
            mModules.Add(CreateCodeGenKitDoc());
            mModules.Add(CreateFluentApiDoc());
            mModules.Add(CreateToolClassDoc());

            // Tools
            mModules.Add(CreateUIKitDoc());
            mModules.Add(CreateActionKitDoc());
            mModules.Add(CreateAudioKitDoc());
            mModules.Add(CreateSaveKitDoc());
            mModules.Add(CreateTableKitDoc());
            mModules.Add(CreateBuffKitDoc());
            mModules.Add(CreateLocalizationKitDoc());
            mModules.Add(CreateSceneKitDoc());
            mModules.Add(CreateInputKitDoc());
            mModules.Add(CreateSpatialKitDoc());
        }

        #endregion

        #region 文档模块创建方法

        private DocModule CreateArchitectureDoc() => new()
        {
            Name = "Architecture",
            Icon = KitIcons.ARCHITECTURE,
            Category = "CORE",
            Description = "YokiFrame 的核心架构系统，提供服务注册、依赖注入和模块化管理。",
            Keywords = new List<string> { "DI", "IoC", "服务注册", "模块化" },
            Sections = ArchitectureDocData.GetAllSections()
        };

        private DocModule CreateSingletonKitDoc() => new()
        {
            Name = "SingletonKit",
            Icon = KitIcons.SINGLETON,
            Category = "CORE KIT",
            Description = "单例模式工具，提供普通单例和 MonoBehaviour 单例两种实现。支持线程安全和自动创建。",
            Keywords = new List<string> { "单例", "Singleton", "全局访问" },
            Sections = SingletonKitDocData.GetAllSections()
        };

        private DocModule CreateEventKitDoc() => new()
        {
            Name = "EventKit",
            Icon = KitIcons.EVENTKIT,
            Category = "CORE KIT",
            Description = "事件系统工具，提供类型事件、枚举事件、字符串事件、EasyEvent 等多种事件模式。支持自动注销和事件通道。",
            Keywords = new List<string> { "事件", "消息", "发布订阅", "解耦" },
            Sections = EventKitDocData.GetAllSections()
        };

        private DocModule CreateFsmKitDoc() => new()
        {
            Name = "FsmKit",
            Icon = KitIcons.FSMKIT,
            Category = "CORE KIT",
            Description = "有限状态机工具，支持状态定义、条件转换、层级状态机等功能。适合 AI、游戏流程控制等场景。",
            Keywords = new List<string> { "状态机", "FSM", "AI", "流程控制" },
            Sections = FsmKitDocData.GetAllSections()
        };

        private DocModule CreatePoolKitDoc() => new()
        {
            Name = "PoolKit",
            Icon = KitIcons.POOLKIT,
            Category = "CORE KIT",
            Description = "对象池工具，提供安全对象池、自定义对象池、容器池等功能。减少 GC，提升性能。",
            Keywords = new List<string> { "对象池", "内存优化", "GC", "复用" },
            Sections = PoolKitDocData.GetAllSections()
        };

        private DocModule CreateResKitDoc() => new()
        {
            Name = "ResKit",
            Icon = KitIcons.RESKIT,
            Category = "CORE KIT",
            Description = "资源管理工具，提供同步/异步加载、引用计数、资源缓存等功能。支持 UniTask 异步和自定义加载器扩展。",
            Keywords = new List<string> { "资源加载", "引用计数", "异步", "缓存" },
            Sections = ResKitDocData.GetAllSections()
        };

        private DocModule CreateLogKitDoc() => new()
        {
            Name = "KitLogger",
            Icon = KitIcons.KITLOGGER,
            Category = "CORE KIT",
            Description = "高性能日志系统，支持日志级别控制、文件写入、加密存储、IMGUI 运行时显示。后台线程异步写入，不阻塞主线程。",
            Keywords = new List<string> { "日志", "调试", "文件写入", "异步" },
            Sections = LogKitDocData.GetAllSections()
        };

        private DocModule CreateCodeGenKitDoc() => new()
        {
            Name = "CodeGenKit",
            Icon = KitIcons.CODEGEN,
            Category = "CORE KIT",
            Description = "代码生成工具，提供结构化的代码生成 API。支持命名空间、类、方法等代码结构的生成。UIKit 的代码生成基于此实现。",
            Keywords = new List<string> { "代码生成", "自动化", "模板" },
            Sections = CodeGenKitDocData.GetAllSections()
        };

        private DocModule CreateFluentApiDoc() => new()
        {
            Name = "FluentApi",
            Icon = KitIcons.FLUENTAPI,
            Category = "CORE KIT",
            Description = "流畅 API 扩展方法集合，提供链式调用支持。包含 Object、String、Transform、Vector、Color 等类型的扩展。",
            Keywords = new List<string> { "链式调用", "扩展方法", "语法糖" },
            Sections = FluentApiDocData.GetAllSections()
        };

        private DocModule CreateToolClassDoc() => new()
        {
            Name = "ToolClass",
            Icon = KitIcons.TOOLCLASS,
            Category = "CORE KIT",
            Description = "工具类集合，包含 BindValue（数据绑定）、PooledLinkedList（池化链表）、SpanSplitter（零分配字符串分割）、FastDictionary（快速字典）等高性能工具。",
            Keywords = new List<string> { "数据绑定", "MVVM", "高性能", "字典", "缓存" },
            Sections = ToolClassDocData.GetAllSections()
        };

        private DocModule CreateUIKitDoc() => new()
        {
            Name = "UIKit",
            Icon = KitIcons.UIKIT,
            Category = "TOOLS",
            Description = "现代化 UI 管理工具，提供面板动画系统、增强生命周期钩子、多命名栈管理、预加载缓存、LRU 淘汰策略、手柄/键盘导航、对话框系统、模态面板、Canvas 优化等功能。",
            Keywords = new List<string> { "UI管理", "面板堆栈", "缓存", "异步加载", "动画", "生命周期" },
            Sections = UIKitDocData.GetAllSections()
        };

        private DocModule CreateActionKitDoc() => new()
        {
            Name = "ActionKit",
            Icon = KitIcons.ACTIONKIT,
            Category = "TOOLS",
            Description = "轻量级动作序列系统，支持延时、回调、序列、并行、重复等动作组合。基于对象池设计，零 GC 运行。",
            Keywords = new List<string> { "动作", "序列", "延时", "回调", "协程替代" },
            Sections = ActionKitDocData.GetAllSections()
        };

        private DocModule CreateAudioKitDoc() => new()
        {
            Name = "AudioKit",
            Icon = KitIcons.AUDIOKIT,
            Category = "TOOLS",
            Description = "音频管理工具，提供 BGM、音效、3D 音频、音频通道、资源管理等功能。支持 FMOD 扩展。",
            Keywords = new List<string> { "音频", "BGM", "音效", "3D音频", "FMOD" },
            Sections = AudioKitDocData.GetAllSections()
        };

        private DocModule CreateSaveKitDoc() => new()
        {
            Name = "SaveKit",
            Icon = KitIcons.SAVEKIT,
            Category = "TOOLS",
            Description = "存档系统工具，提供多槽位存档、版本迁移、加密、自动保存等功能。采用延迟序列化设计，主线程零阻塞。",
            Keywords = new List<string> { "存档", "持久化", "加密", "版本迁移", "异步" },
            Sections = SaveKitDocData.GetAllSections()
        };

        private DocModule CreateTableKitDoc() => new()
        {
            Name = "TableKit",
            Icon = KitIcons.TABLEKIT,
            Category = "TOOLS",
            Description = "Luban 配置表集成工具，提供编辑器配置界面和运行时代码生成。支持 Binary 和 JSON 两种数据格式，自动检测加载模式。",
            Keywords = new List<string> { "配置表", "Luban", "Excel", "数据驱动" },
            Sections = TableKitDocData.GetAllSections()
        };

        private DocModule CreateBuffKitDoc() => new()
        {
            Name = "BuffKit",
            Icon = KitIcons.BUFFKIT,
            Category = "TOOLS",
            Description = "Buff 系统工具，提供 Buff 定义、叠加模式、查询、免疫、修饰器、事件、序列化等功能。适合 RPG、MOBA 等游戏。",
            Keywords = new List<string> { "Buff", "状态效果", "增益", "减益", "RPG" },
            Sections = BuffKitDocData.GetAllSections()
        };

        private DocModule CreateLocalizationKitDoc() => new()
        {
            Name = "LocalizationKit",
            Icon = KitIcons.LOCALIZATIONKIT,
            Category = "TOOLS",
            Description = "本地化系统工具，提供多语言文本管理、参数化文本、复数形式、UI 绑定、异步加载等功能。支持 JSON 和 TableKit 数据源。",
            Keywords = new List<string> { "多语言", "国际化", "i18n", "文本" },
            Sections = LocalizationKitDocData.GetAllSections()
        };

        private DocModule CreateSceneKitDoc() => new()
        {
            Name = "SceneKit",
            Icon = KitIcons.SCENEKIT,
            Category = "TOOLS",
            Description = "场景管理工具，提供统一的场景加载、切换、卸载、预加载、过渡效果等功能。支持 YooAsset 扩展和自定义加载器。",
            Keywords = new List<string> { "场景切换", "过渡效果", "预加载", "异步" },
            Sections = SceneKitDocData.GetAllSections()
        };

        private DocModule CreateInputKitDoc() => new()
        {
            Name = "InputKit",
            Icon = KitIcons.INPUTKIT,
            Category = "TOOLS",
            Description = "输入管理工具，提供双输入系统支持、运行时重绑定、输入缓冲、连招检测、上下文系统、触屏控件、震动反馈、调试工具等功能。",
            Keywords = new List<string> { "输入", "手柄", "键盘", "触屏", "重绑定", "连招", "震动" },
            Sections = InputKitDocData.GetAllSections()
        };

        private DocModule CreateSpatialKitDoc() => new()
        {
            Name = "SpatialKit",
            Icon = KitIcons.SPATIALKIT,
            Category = "TOOLS",
            Description = "空间索引工具，提供空间哈希网格、四叉树、八叉树三种高性能空间分区数据结构。用于优化大量实体的邻居查询、范围检测等空间操作。",
            Keywords = new List<string> { "空间索引", "四叉树", "八叉树", "空间哈希", "感知系统" },
            Sections = SpatialKitDocData.GetAllSections()
        };

        #endregion
    }

    #region 文档数据结构（供拆分文件使用）

    /// <summary>
    /// 文档模块
    /// </summary>
    internal class DocModule
    {
        public string Name;
        public string Icon;
        public string Category;
        public string Description;
        public List<string> Keywords = new();
        public List<DocSection> Sections = new();
    }

    /// <summary>
    /// 文档章节
    /// </summary>
    internal class DocSection
    {
        public string Title;
        public string Description;
        public List<CodeExample> CodeExamples = new();
    }

    /// <summary>
    /// 代码示例
    /// </summary>
    internal class CodeExample
    {
        public string Title;
        public string Code;
        public string Explanation;
    }

    #endregion
}
#endif
