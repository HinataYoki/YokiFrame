#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 文档页面 - 带语法高亮的详细 API 文档
    /// </summary>
    public partial class DocumentationToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "文档";
        public override string PageIcon => KitIcons.DOCUMENTATION;
        public override int Priority => 0;
        
        private ScrollView mTocScrollView;
        private ScrollView mContentScrollView;
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
        }
        
        #endregion
        
        #region 数据结构
        
        private class DocModule
        {
            public string Name;
            public string Icon;
            public string Category;
            public string Description;
            public List<string> Keywords = new();
            public List<DocSection> Sections = new();
        }
        
        private class DocSection
        {
            public string Title;
            public string Description;
            public List<CodeExample> CodeExamples = new();
        }
        
        private class CodeExample
        {
            public string Title;
            public string Code;
            public string Explanation;
        }
        
        #endregion
    }
}
#endif
