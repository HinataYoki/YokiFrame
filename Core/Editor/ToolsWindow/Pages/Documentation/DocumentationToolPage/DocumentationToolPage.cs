#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame documentation page.
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
        private VisualElement mRootContainer;
        private readonly List<DocModule> mModules = new();
        private readonly Dictionary<VisualElement, int> mTocItemMap = new();
        private VisualElement mSelectedTocItem;
        private VisualElement mHighlightIndicator;
        private VisualElement mTocItemsContainer;

        private VisualElement mOnThisPagePanel;
        private VisualElement mOnThisPageContainer;
        private readonly List<(string title, VisualElement element, int level)> mCurrentHeadings = new();
        private VisualElement mSelectedHeadingItem;
        private readonly List<(VisualElement navItem, VisualElement contentElement)> mHeadingNavMap = new();
        private bool mIsScrollingByClick;

        private const float ON_THIS_PAGE_MIN_WIDTH = 1200f;

        private static class Theme
        {
            public static readonly Color BgPrimary = new(0.16f, 0.16f, 0.16f);
            public static readonly Color BgSecondary = new(0.14f, 0.14f, 0.14f);
            public static readonly Color BgTertiary = new(0.12f, 0.12f, 0.12f);
            public static readonly Color BgCode = new(0.1f, 0.1f, 0.1f);
            public static readonly Color BgHover = new(0.2f, 0.2f, 0.2f);
            public static readonly Color BgSelected = new(0.24f, 0.37f, 0.58f);

            public static readonly Color AccentBlue = new(0.34f, 0.61f, 0.84f);
            public static readonly Color AccentGreen = new(0.4f, 0.7f, 0.4f);
            public static readonly Color AccentOrange = new(0.9f, 0.6f, 0.3f);
            public static readonly Color AccentPurple = new(0.7f, 0.5f, 0.8f);
            public static readonly Color AccentRed = new(0.9f, 0.4f, 0.4f);
            public static readonly Color AccentYellow = new(0.9f, 0.8f, 0.4f);

            public static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f);
            public static readonly Color TextSecondary = new(0.8f, 0.8f, 0.8f);
            public static readonly Color TextMuted = new(0.6f, 0.6f, 0.6f);
            public static readonly Color TextDim = new(0.5f, 0.5f, 0.5f);

            public static readonly Color Border = new(0.25f, 0.25f, 0.25f);
            public static readonly Color BorderDark = new(0.1f, 0.1f, 0.1f);

            public static readonly Color CategoryCore = new(0.55f, 0.7f, 0.85f);
            public static readonly Color CategoryKit = new(0.55f, 0.75f, 0.6f);
            public static readonly Color CategoryTools = new(0.85f, 0.7f, 0.55f);

            public static readonly Color CategoryCoreBg = new(0.14f, 0.15f, 0.17f);
            public static readonly Color CategoryKitBg = new(0.14f, 0.16f, 0.15f);
            public static readonly Color CategoryToolsBg = new(0.16f, 0.15f, 0.14f);
        }

        protected override void BuildUI(VisualElement root)
        {
            InitializeDocumentation();

            mRootContainer = root;

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            root.Add(container);

            container.Add(CreateTocPanel());

            mContentScrollView = new ScrollView();
            mContentScrollView.style.flexGrow = 1;
            mContentScrollView.style.backgroundColor = new StyleColor(Theme.BgPrimary);
            mContentScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            YokiFrameUIComponents.FixScrollViewDragger(mContentScrollView);
            mContentScrollView.verticalScroller.valueChanged += OnContentScrollChanged;
            container.Add(mContentScrollView);

            container.Add(CreateOnThisPagePanel());
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            if (mModules.Count > 0)
            {
                SelectModule(0);
            }
        }

        private void InitializeDocumentation()
        {
            mModules.Clear();

            foreach (var module in DocumentationModuleRegistry.Modules)
            {
                if (module == null || string.IsNullOrEmpty(module.Name))
                {
                    continue;
                }

                mModules.Add(module);
            }
        }
    }
}
#endif
