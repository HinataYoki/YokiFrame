#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具总面板 - UI 构建
    /// </summary>
    public partial class YokiFrameToolWindow
    {
        #region 侧边栏构建

        /// <summary>
        /// 创建侧边栏
        /// </summary>
        private VisualElement CreateSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("sidebar");
            sidebar.style.overflow = Overflow.Hidden;

            // 标题区域（固定在顶部，不参与滚动）
            var header = new VisualElement();
            header.AddToClassList("sidebar-header");
            header.style.flexDirection = FlexDirection.Column;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0; // 防止被压缩

            // 添加框架图标 - 居中突出显示
            Texture2D iconTexture = LoadIcon();
            if (iconTexture != null)
            {
                var iconImage = new Image { image = iconTexture };
                iconImage.style.width = 64;
                iconImage.style.height = 64;
                iconImage.style.marginBottom = 8;
                header.Add(iconImage);
            }

            var title = new Label("YokiFrame");
            title.AddToClassList("sidebar-title");
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(title);
            sidebar.Add(header);

            // 页面列表（带分组）- 可滚动区域
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("sidebar-list");
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            list.style.flexGrow = 1;
            list.style.flexShrink = 1;
            
            // 确保滚动条可正常拖动
            YokiFrameUIComponents.FixScrollViewDragger(list);

            // 创建列表内容容器（用于放置高亮指示器）
            mSidebarListContainer = new VisualElement();
            mSidebarListContainer.style.position = Position.Relative;
            list.Add(mSidebarListContainer);

            // 创建高亮指示器
            CreateSidebarHighlight();
            mSidebarListContainer.Add(mSidebarHighlight);

            // 分离文档和工具页面
            var docPages = new List<(int index, IYokiFrameToolPage page)>();
            var toolPages = new List<(int index, IYokiFrameToolPage page)>();

            for (int i = 0; i < mPages.Count; i++)
            {
                var page = mPages[i];
                if (page is DocumentationToolPage)
                    docPages.Add((i, page));
                else
                    toolPages.Add((i, page));
            }

            // 文档分组
            if (docPages.Count > 0)
            {
                var docsGroup = CreateSidebarGroup(KitIcons.FOLDER_DOCS, "文档", docPages.Count, "docs");
                foreach (var (index, page) in docPages)
                {
                    var item = CreateSidebarItem(page, index, "docs");
                    docsGroup.Add(item);
                }
                mSidebarListContainer.Add(docsGroup);
            }

            // 工具分组
            if (toolPages.Count > 0)
            {
                var toolsGroup = CreateSidebarGroup(KitIcons.FOLDER_TOOLS, "工具", toolPages.Count, "tools");
                foreach (var (index, page) in toolPages)
                {
                    var item = CreateSidebarItem(page, index, "tools");
                    toolsGroup.Add(item);
                }
                mSidebarListContainer.Add(toolsGroup);
            }

            sidebar.Add(list);

            // 底部版本信息区域
            sidebar.Add(CreateVersionInfoPanel());

            return sidebar;
        }

        /// <summary>
        /// 创建侧边栏高亮指示器
        /// </summary>
        private void CreateSidebarHighlight()
        {
            mSidebarHighlight = new VisualElement();
            mSidebarHighlight.style.position = Position.Absolute;
            mSidebarHighlight.style.borderTopLeftRadius = 6;
            mSidebarHighlight.style.borderTopRightRadius = 6;
            mSidebarHighlight.style.borderBottomLeftRadius = 6;
            mSidebarHighlight.style.borderBottomRightRadius = 6;
            mSidebarHighlight.style.opacity = 0;
            mSidebarHighlight.pickingMode = PickingMode.Ignore;

            // 添加过渡动画
            mSidebarHighlight.style.transitionProperty = new List<StylePropertyName>
            {
                new("top"),
                new("left"),
                new("width"),
                new("height"),
                new("opacity"),
                new("background-color")
            };
            mSidebarHighlight.style.transitionDuration = new List<TimeValue>
            {
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond)
            };
            mSidebarHighlight.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
        }

        /// <summary>
        /// 创建侧边栏分组
        /// </summary>
        private VisualElement CreateSidebarGroup(string iconId, string title, int count, string groupClass)
        {
            var group = new VisualElement();
            group.AddToClassList("sidebar-group");
            group.AddToClassList(groupClass);

            var header = new VisualElement();
            header.AddToClassList("sidebar-group-header");

            var iconImage = new Image { image = KitIcons.GetTexture(iconId) };
            iconImage.AddToClassList("sidebar-group-icon");
            header.Add(iconImage);

            var titleLabel = new Label(title.ToUpper());
            titleLabel.AddToClassList("sidebar-group-title");
            header.Add(titleLabel);

            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("sidebar-group-count");
            header.Add(countLabel);

            group.Add(header);
            return group;
        }

        /// <summary>
        /// 创建侧边栏项
        /// </summary>
        private VisualElement CreateSidebarItem(IYokiFrameToolPage page, int index, string groupClass)
        {
            var item = new VisualElement();
            item.AddToClassList("sidebar-item");

            // 图标 - 使用生成的纹理图标
            var iconTexture = KitIcons.GetTexture(page.PageIcon);
            var icon = new Image { image = iconTexture };
            icon.AddToClassList("sidebar-item-icon");
            item.Add(icon);

            var label = new Label(page.PageName);
            label.AddToClassList("sidebar-item-label");
            item.Add(label);

            // 弹出按钮
            var popoutBtn = new Button(() => PopoutPage(page));
            popoutBtn.AddToClassList("sidebar-popout-btn");
            popoutBtn.tooltip = "在独立窗口中打开";
            var popoutIcon = new Image { image = KitIcons.GetTexture(KitIcons.POPOUT) };
            popoutIcon.style.width = 12;
            popoutIcon.style.height = 12;
            popoutBtn.Add(popoutIcon);
            item.Add(popoutBtn);

            // 点击选择页面
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == popoutBtn) return;
                SelectPage(index);
                evt.StopPropagation();
            });

            mSidebarItems[page] = item;
            return item;
        }

        /// <summary>
        /// 将侧边栏高亮指示器平滑移动到目标项
        /// </summary>
        private void MoveSidebarHighlight(VisualElement targetItem)
        {
            if (targetItem == null || mSidebarHighlight == null || mSidebarListContainer == null) return;

            // 获取目标项相对于容器的位置
            var targetRect = targetItem.worldBound;
            var containerRect = mSidebarListContainer.worldBound;

            // 计算相对位置
            float relativeTop = targetRect.y - containerRect.y;
            float relativeLeft = targetRect.x - containerRect.x;

            // 设置高亮指示器位置和大小
            mSidebarHighlight.style.top = relativeTop;
            mSidebarHighlight.style.left = relativeLeft;
            mSidebarHighlight.style.width = targetRect.width;
            mSidebarHighlight.style.height = targetRect.height;
            mSidebarHighlight.style.opacity = 1;
        }

        #endregion

        #region 版本信息面板

        /// <summary>
        /// 创建版本信息面板
        /// </summary>
        private VisualElement CreateVersionInfoPanel()
        {
            var versionPanel = new VisualElement();
            versionPanel.style.flexShrink = 0; // 防止被压缩
            versionPanel.style.paddingLeft = 16;
            versionPanel.style.paddingRight = 16;
            versionPanel.style.paddingTop = 12;
            versionPanel.style.paddingBottom = 16;
            versionPanel.style.borderTopWidth = 1;
            versionPanel.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            versionPanel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.09f));

            // 读取 package.json 获取版本
            string version = GetPackageVersion();

            // 版本行
            var versionRow = CreateVersionRow(version);
            versionPanel.Add(versionRow);

            // GitHub 链接
            var linkRow = CreateGitHubLinkRow();
            versionPanel.Add(linkRow);

            return versionPanel;
        }

        /// <summary>
        /// 创建版本行
        /// </summary>
        private VisualElement CreateVersionRow(string version)
        {
            var versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.alignItems = Align.Center;
            versionRow.style.marginBottom = 8;

            var versionIcon = new Image { image = KitIcons.GetTexture(KitIcons.PACKAGE) };
            versionIcon.style.width = 12;
            versionIcon.style.height = 12;
            versionIcon.style.marginRight = 8;
            versionRow.Add(versionIcon);

            var versionLabel = new Label("YokiFrame");
            versionLabel.style.fontSize = 12;
            versionLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.78f));
            versionLabel.style.flexGrow = 1;
            versionRow.Add(versionLabel);

            var versionBadge = new Label($"v{version}");
            versionBadge.style.fontSize = 10;
            versionBadge.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            versionBadge.style.backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.45f, 0.35f));
            versionBadge.style.paddingLeft = 6;
            versionBadge.style.paddingRight = 6;
            versionBadge.style.paddingTop = 2;
            versionBadge.style.paddingBottom = 2;
            versionBadge.style.borderTopLeftRadius = 4;
            versionBadge.style.borderTopRightRadius = 4;
            versionBadge.style.borderBottomLeftRadius = 4;
            versionBadge.style.borderBottomRightRadius = 4;
            versionRow.Add(versionBadge);

            return versionRow;
        }

        /// <summary>
        /// 创建 GitHub 链接行
        /// </summary>
        private VisualElement CreateGitHubLinkRow()
        {
            var linkRow = new VisualElement();
            linkRow.style.flexDirection = FlexDirection.Row;
            linkRow.style.alignItems = Align.Center;
            linkRow.style.paddingTop = 6;
            linkRow.style.paddingBottom = 6;
            linkRow.style.paddingLeft = 4;
            linkRow.style.borderTopLeftRadius = 4;
            linkRow.style.borderTopRightRadius = 4;
            linkRow.style.borderBottomLeftRadius = 4;
            linkRow.style.borderBottomRightRadius = 4;
            linkRow.style.transitionProperty = new List<StylePropertyName> { new("background-color") };
            linkRow.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };

            var linkIcon = new Image { image = KitIcons.GetTexture(KitIcons.GITHUB) };
            linkIcon.style.width = 11;
            linkIcon.style.height = 11;
            linkIcon.style.marginRight = 8;
            linkRow.Add(linkIcon);

            var linkLabel = new Label("GitHub");
            linkLabel.style.fontSize = 11;
            linkLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            linkLabel.style.transitionProperty = new List<StylePropertyName> { new("color") };
            linkLabel.style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            linkRow.Add(linkLabel);

            linkRow.RegisterCallback<MouseEnterEvent>(evt =>
            {
                linkRow.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                linkLabel.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            });
            linkRow.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                linkRow.style.backgroundColor = new StyleColor(Color.clear);
                linkLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            });
            linkRow.RegisterCallback<ClickEvent>(evt =>
            {
                Application.OpenURL("https://github.com/HinataYoki/YokiFrame");
            });

            return linkRow;
        }

        /// <summary>
        /// 从 package.json 读取版本号
        /// </summary>
        private string GetPackageVersion()
        {
            const string DEFAULT_VERSION = "1.0.0";

            // 尝试多个可能的路径
            string[] possiblePaths =
            {
                "Packages/com.hinatayoki.yokiframe/package.json",  // Package 安装路径
                "Assets/YokiFrame/package.json"                    // Assets 文件夹路径
            };

            foreach (var packagePath in possiblePaths)
            {
                if (!System.IO.File.Exists(packagePath)) continue;

                try
                {
                    string json = System.IO.File.ReadAllText(packagePath);
                    int versionIndex = json.IndexOf("\"version\"");
                    if (versionIndex < 0) continue;

                    int colonIndex = json.IndexOf(':', versionIndex);
                    int startQuote = json.IndexOf('"', colonIndex);
                    int endQuote = json.IndexOf('"', startQuote + 1);

                    if (startQuote >= 0 && endQuote > startQuote)
                    {
                        return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                    }
                }
                catch
                {
                    // 忽略解析错误，尝试下一个路径
                }
            }

            return DEFAULT_VERSION;
        }

        #endregion
    }
}
#endif
