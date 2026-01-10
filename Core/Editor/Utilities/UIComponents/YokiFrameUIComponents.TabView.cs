#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 标签页视图
    /// 提供统一的标签页切换功能，支持多个标签页内容切换
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// 标签页视图组件
        /// </summary>
        public class TabView
        {
            /// <summary>根容器</summary>
            public VisualElement Root { get; }
            
            /// <summary>标签栏容器</summary>
            public VisualElement TabBar { get; }
            
            /// <summary>内容容器</summary>
            public VisualElement ContentContainer { get; }
            
            /// <summary>当前选中的标签页索引</summary>
            public int SelectedIndex { get; private set; }
            
            /// <summary>标签页切换回调</summary>
            public event Action<int> OnTabChanged;

            private readonly List<Button> mTabButtons = new();
            private readonly List<VisualElement> mTabContents = new();

            /// <summary>
            /// 创建标签页视图
            /// </summary>
            internal TabView()
            {
                Root = new VisualElement();
                Root.style.flexGrow = 1;
                Root.style.flexDirection = FlexDirection.Column;

                // 标签栏 - 使用 USS 类
                TabBar = new VisualElement();
                TabBar.name = "tab-bar";
                TabBar.AddToClassList("tab-bar");
                Root.Add(TabBar);

                // 内容容器
                ContentContainer = new VisualElement();
                ContentContainer.name = "tab-content";
                ContentContainer.style.flexGrow = 1;
                ContentContainer.style.overflow = Overflow.Hidden;
                Root.Add(ContentContainer);
            }

            /// <summary>
            /// 添加标签页
            /// </summary>
            /// <param name="title">标签页标题</param>
            /// <param name="content">标签页内容</param>
            /// <param name="iconId">可选的图标 ID</param>
            public void AddTab(string title, VisualElement content, string iconId = null)
            {
                int tabIndex = mTabButtons.Count;
                
                // 创建标签按钮 - 使用 USS 类
                var btn = new Button(() => SwitchTo(tabIndex));
                btn.AddToClassList("tab-button");
                btn.style.flexDirection = FlexDirection.Row;
                btn.style.alignItems = Align.Center;

                // 添加图标（如果有）
                if (!string.IsNullOrEmpty(iconId))
                {
                    var icon = new Image { image = KitIcons.GetTexture(iconId) };
                    icon.style.width = 14;
                    icon.style.height = 14;
                    icon.style.marginRight = Spacing.XS;
                    btn.Add(icon);
                }

                // 添加标题
                var label = new Label(title);
                btn.Add(label);

                TabBar.Add(btn);
                mTabButtons.Add(btn);

                // 设置内容
                content.style.flexGrow = 1;
                content.style.display = DisplayStyle.None;
                mTabContents.Add(content);

                // 如果是第一个标签页，默认选中
                if (mTabButtons.Count == 1)
                {
                    SwitchTo(0);
                }
            }

            /// <summary>
            /// 切换到指定标签页
            /// </summary>
            /// <param name="index">标签页索引</param>
            public void SwitchTo(int index)
            {
                if (index < 0 || index >= mTabButtons.Count) return;

                SelectedIndex = index;

                // 更新内容显示
                ContentContainer.Clear();
                if (index < mTabContents.Count)
                {
                    var content = mTabContents[index];
                    content.style.display = DisplayStyle.Flex;
                    ContentContainer.Add(content);
                }

                // 更新按钮样式 - 使用 USS 类
                for (int i = 0; i < mTabButtons.Count; i++)
                {
                    if (i == index)
                        mTabButtons[i].AddToClassList("selected");
                    else
                        mTabButtons[i].RemoveFromClassList("selected");
                }

                OnTabChanged?.Invoke(index);
            }

            /// <summary>
            /// 获取标签页数量
            /// </summary>
            public int TabCount => mTabButtons.Count;
        }

        /// <summary>
        /// 创建标签页视图
        /// </summary>
        /// <returns>标签页视图实例</returns>
        public static TabView CreateTabView() => new();

        /// <summary>
        /// 创建带初始标签页的标签页视图
        /// </summary>
        /// <param name="tabs">标签页配置数组 (标题, 内容, 可选图标ID)</param>
        /// <returns>标签页视图实例</returns>
        public static TabView CreateTabView(params (string title, VisualElement content, string iconId)[] tabs)
        {
            var tabView = new TabView();
            foreach (var (title, content, iconId) in tabs)
            {
                tabView.AddTab(title, content, iconId);
            }
            return tabView;
        }

        /// <summary>
        /// 创建带初始标签页的标签页视图（无图标版本）
        /// </summary>
        /// <param name="tabs">标签页配置数组 (标题, 内容)</param>
        /// <returns>标签页视图实例</returns>
        public static TabView CreateTabView(params (string title, VisualElement content)[] tabs)
        {
            var tabView = new TabView();
            foreach (var (title, content) in tabs)
            {
                tabView.AddTab(title, content);
            }
            return tabView;
        }
    }
}
#endif
