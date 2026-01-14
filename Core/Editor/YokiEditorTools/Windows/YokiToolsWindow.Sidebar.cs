#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiToolsWindow - 侧边栏构建
    /// </summary>
    public partial class YokiToolsWindow
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

            // 标题区域
            var header = CreateSidebarHeader();
            sidebar.Add(header);

            // 页面列表（可滚动）
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("sidebar-list");
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            list.style.flexGrow = 1;
            list.style.flexShrink = 1;
            YokiFrameUIComponents.FixScrollViewDragger(list);

            // 列表内容容器
            mSidebarListContainer = new VisualElement();
            mSidebarListContainer.style.position = Position.Relative;
            list.Add(mSidebarListContainer);

            // 高亮指示器
            CreateSidebarHighlight();
            mSidebarListContainer.Add(mSidebarHighlight);

            // 分组构建
            BuildSidebarGroups();

            sidebar.Add(list);
            sidebar.Add(CreateVersionInfoPanel());

            return sidebar;
        }

        /// <summary>
        /// 创建侧边栏头部
        /// </summary>
        private VisualElement CreateSidebarHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sidebar-header");
            header.style.flexDirection = FlexDirection.Column;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;

            var iconTexture = LoadIcon();
            if (iconTexture != default)
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

            return header;
        }

        /// <summary>
        /// 构建侧边栏分组
        /// </summary>
        private void BuildSidebarGroups()
        {
            var docPages = new List<(int index, YokiPageInfo info)>();
            var toolPages = new List<(int index, YokiPageInfo info)>();

            for (int i = 0; i < mPageInfos.Count; i++)
            {
                var info = mPageInfos[i];
                if (info.Category == YokiPageCategory.Documentation)
                    docPages.Add((i, info));
                else
                    toolPages.Add((i, info));
            }

            // 文档分组
            if (docPages.Count > 0)
            {
                var docsGroup = CreateSidebarGroup(KitIcons.FOLDER_DOCS, "文档", docPages.Count, "docs");
                for (int i = 0; i < docPages.Count; i++)
                {
                    var (index, info) = docPages[i];
                    var item = CreateSidebarItem(info, index, "docs");
                    docsGroup.Add(item);
                }
                mSidebarListContainer.Add(docsGroup);
            }

            // 工具分组
            if (toolPages.Count > 0)
            {
                var toolsGroup = CreateSidebarGroup(KitIcons.FOLDER_TOOLS, "工具", toolPages.Count, "tools");
                for (int i = 0; i < toolPages.Count; i++)
                {
                    var (index, info) = toolPages[i];
                    var item = CreateSidebarItem(info, index, "tools");
                    toolsGroup.Add(item);
                }
                mSidebarListContainer.Add(toolsGroup);
            }
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

            mSidebarHighlight.style.transitionProperty = new List<StylePropertyName>
            {
                new("top"), new("left"), new("width"), new("height"),
                new("opacity"), new("background-color")
            };
            mSidebarHighlight.style.transitionDuration = new List<TimeValue>
            {
                new(200, TimeUnit.Millisecond), new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond), new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond), new(200, TimeUnit.Millisecond)
            };
            mSidebarHighlight.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut), new(EasingMode.EaseOut),
                new(EasingMode.EaseOut), new(EasingMode.EaseOut),
                new(EasingMode.EaseOut), new(EasingMode.EaseOut)
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
        private VisualElement CreateSidebarItem(YokiPageInfo info, int index, string groupClass)
        {
            var item = new VisualElement();
            item.AddToClassList("sidebar-item");

            var iconTexture = KitIcons.GetTexture(info.Icon);
            var icon = new Image { image = iconTexture };
            icon.AddToClassList("sidebar-item-icon");
            item.Add(icon);

            var label = new Label(info.Name);
            label.AddToClassList("sidebar-item-label");
            item.Add(label);

            // 弹出按钮
            var popoutBtn = new Button();
            popoutBtn.AddToClassList("sidebar-popout-btn");
            popoutBtn.tooltip = "在独立窗口中打开";
            var popoutIcon = new Image { image = KitIcons.GetTexture(KitIcons.POPOUT) };
            popoutIcon.style.width = 12;
            popoutIcon.style.height = 12;
            popoutBtn.Add(popoutIcon);
            popoutBtn.clicked += () => PopoutPage(info);
            item.Add(popoutBtn);

            // 点击选择页面
            int capturedIndex = index;
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == popoutBtn) return;
                SelectPage(capturedIndex);
                evt.StopPropagation();
            });

            mSidebarItems[info] = item;
            return item;
        }

        /// <summary>
        /// 移动侧边栏高亮指示器
        /// </summary>
        private void MoveSidebarHighlight(VisualElement targetItem)
        {
            if (targetItem == default || mSidebarHighlight == default || mSidebarListContainer == default)
                return;

            var targetRect = targetItem.worldBound;
            var containerRect = mSidebarListContainer.worldBound;

            float relativeTop = targetRect.y - containerRect.y;
            float relativeLeft = targetRect.x - containerRect.x;

            mSidebarHighlight.style.top = relativeTop;
            mSidebarHighlight.style.left = relativeLeft;
            mSidebarHighlight.style.width = targetRect.width;
            mSidebarHighlight.style.height = targetRect.height;
            mSidebarHighlight.style.opacity = 1;
        }

        #endregion

        #region 版本信息

        private VisualElement CreateVersionInfoPanel()
        {
            var panel = new VisualElement();
            panel.style.flexShrink = 0;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 12;
            panel.style.paddingBottom = 16;
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.09f));

            string version = GetPackageVersion();
            panel.Add(CreateVersionRow(version));
            panel.Add(CreateGitHubLinkRow());

            return panel;
        }

        private VisualElement CreateVersionRow(string version)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.PACKAGE) };
            icon.style.width = 12;
            icon.style.height = 12;
            icon.style.marginRight = 8;
            row.Add(icon);

            var label = new Label("YokiFrame");
            label.style.fontSize = 12;
            label.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.78f));
            label.style.flexGrow = 1;
            row.Add(label);

            var badge = new Label($"v{version}");
            badge.style.fontSize = 10;
            badge.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            badge.style.backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.45f, 0.35f));
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            row.Add(badge);

            return row;
        }

        private VisualElement CreateGitHubLinkRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 4;
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.GITHUB) };
            icon.style.width = 11;
            icon.style.height = 11;
            icon.style.marginRight = 8;
            row.Add(icon);

            var label = new Label("GitHub");
            label.style.fontSize = 11;
            label.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            row.Add(label);

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                row.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.2f));
                label.style.color = new StyleColor(new Color(0.34f, 0.61f, 0.84f));
            });

            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                row.style.backgroundColor = new StyleColor(Color.clear);
                label.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
            });

            row.RegisterCallback<ClickEvent>(_ =>
            {
                Application.OpenURL("https://github.com/HinataYoki/YokiFrame");
            });

            return row;
        }

        private string GetPackageVersion()
        {
            const string DEFAULT_VERSION = "1.0.0";
            string[] paths = { "Packages/com.hinatayoki.yokiframe/package.json", "Assets/YokiFrame/package.json" };

            for (int i = 0; i < paths.Length; i++)
            {
                if (!File.Exists(paths[i])) continue;

                try
                {
                    string json = File.ReadAllText(paths[i]);
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
                catch { }
            }

            return DEFAULT_VERSION;
        }

        #endregion
    }
}
#endif
