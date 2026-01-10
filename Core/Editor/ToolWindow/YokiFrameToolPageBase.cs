#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具页面基类
    /// 提供现代化 UI 组件的便捷创建方法和响应式数据绑定支持
    /// </summary>
    public abstract class YokiFrameToolPageBase : IYokiFrameToolPage
    {
        public abstract string PageName { get; }
        public virtual string PageIcon => KitIcons.DOCUMENT;
        public virtual int Priority => 100;
        
        protected bool IsPlaying => EditorApplication.isPlaying;
        protected VisualElement Root { get; private set; }
        
        /// <summary>
        /// 订阅管理器 - 自动在 OnDeactivate 时清理所有订阅
        /// </summary>
        protected CompositeDisposable Subscriptions { get; } = new(8);

        /// <summary>
        /// VisualElement 查询缓存 - 避免重复查询
        /// </summary>
        private readonly Dictionary<string, VisualElement> mQueryCache = new(16);
        
        public VisualElement CreateUI()
        {
            Root = new VisualElement();
            Root.style.flexGrow = 1;
            BuildUI(Root);
            return Root;
        }
        
        /// <summary>
        /// 构建页面 UI
        /// </summary>
        protected abstract void BuildUI(VisualElement root);
        
        public virtual void OnActivate()
        {
            // 监听 PlayMode 状态变化
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        public virtual void OnDeactivate()
        {
            // 清理所有订阅
            Subscriptions.Clear();
            // 清理查询缓存
            mQueryCache.Clear();
            // 清理 EditorEventCenter 中属于此页面的订阅
            EditorEventCenter.UnregisterAll(this);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        /// <summary>
        /// 轮询更新（已废弃，请使用响应式订阅）
        /// </summary>
        [Obsolete("使用响应式订阅替代轮询。此方法将在未来版本中移除。")]
        public virtual void OnUpdate() { }

        /// <summary>
        /// PlayMode 状态变化回调，子类可重写
        /// </summary>
        protected virtual void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // 退出 PlayMode 时清理订阅
                Subscriptions.Clear();
            }
        }

        #region VisualElement 查询缓存

        /// <summary>
        /// 缓存查询 VisualElement - 避免重复调用 Q&lt;T&gt;()
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="name">元素名称</param>
        /// <returns>查询到的元素，未找到返回 null</returns>
        protected T QueryCached<T>(string name) where T : VisualElement
        {
            if (Root == null) return null;
            
            if (!mQueryCache.TryGetValue(name, out var element))
            {
                element = Root.Q<T>(name);
                if (element != null) mQueryCache[name] = element;
            }
            return element as T;
        }

        /// <summary>
        /// 缓存查询 VisualElement（通过类名）
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="className">CSS 类名</param>
        /// <returns>查询到的元素，未找到返回 null</returns>
        protected T QueryCachedByClass<T>(string className) where T : VisualElement
        {
            if (Root == null) return null;
            
            var cacheKey = $"class:{className}";
            if (!mQueryCache.TryGetValue(cacheKey, out var element))
            {
                element = Root.Q<T>(className: className);
                if (element != null) mQueryCache[cacheKey] = element;
            }
            return element as T;
        }

        /// <summary>
        /// 清除查询缓存
        /// </summary>
        protected void ClearQueryCache()
        {
            mQueryCache.Clear();
        }

        #endregion

        #region 响应式数据绑定

        /// <summary>
        /// 订阅 EditorEventCenter 类型事件（自动管理生命周期）
        /// </summary>
        /// <typeparam name="T">事件数据类型</typeparam>
        /// <param name="handler">事件处理器</param>
        protected void SubscribeEvent<T>(Action<T> handler)
        {
            Subscriptions.Add(EditorEventCenter.Register(this, handler));
        }

        /// <summary>
        /// 订阅 EditorEventCenter 枚举键事件（自动管理生命周期）
        /// </summary>
        /// <typeparam name="TKey">枚举键类型</typeparam>
        /// <typeparam name="TValue">事件数据类型</typeparam>
        /// <param name="key">枚举键</param>
        /// <param name="handler">事件处理器</param>
        protected void SubscribeEvent<TKey, TValue>(TKey key, Action<TValue> handler) where TKey : Enum
        {
            Subscriptions.Add(EditorEventCenter.Register(this, key, handler));
        }

        /// <summary>
        /// 订阅数据通道（自动管理生命周期）
        /// </summary>
        protected void SubscribeChannel<T>(string channel, Action<T> callback)
        {
            Subscriptions.Add(EditorDataBridge.Subscribe(channel, callback));
        }

        /// <summary>
        /// 订阅数据通道（带节流，自动管理生命周期）
        /// </summary>
        protected void SubscribeChannelThrottled<T>(string channel, Action<T> callback, float intervalSeconds)
        {
            Subscriptions.Add(EditorDataBridge.SubscribeThrottled(channel, callback, intervalSeconds));
        }

        /// <summary>
        /// 绑定 Label 到 ReactiveProperty（自动管理生命周期）
        /// </summary>
        /// <param name="label">目标 Label</param>
        /// <param name="property">响应式属性</param>
        /// <returns>订阅句柄</returns>
        protected IDisposable BindToLabel(Label label, ReactiveProperty<string> property)
        {
            if (label == null)
            {
                UnityEngine.Debug.LogWarning("[YokiFrameToolPage] BindToLabel: Label 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            // 立即设置初始值
            label.text = property.Value ?? string.Empty;
            
            var subscription = property.Subscribe(value => label.text = value ?? string.Empty);
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 绑定 Label 到 ReactiveProperty（带格式化）
        /// </summary>
        /// <typeparam name="T">属性值类型</typeparam>
        /// <param name="label">目标 Label</param>
        /// <param name="property">响应式属性</param>
        /// <param name="formatter">格式化函数</param>
        /// <returns>订阅句柄</returns>
        protected IDisposable BindToLabel<T>(Label label, ReactiveProperty<T> property, Func<T, string> formatter)
        {
            if (label == null)
            {
                UnityEngine.Debug.LogWarning("[YokiFrameToolPage] BindToLabel: Label 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            // 立即设置初始值
            label.text = formatter(property.Value);
            
            var subscription = property.Subscribe(value => label.text = formatter(value));
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 绑定 VisualElement 可见性到 ReactiveProperty
        /// </summary>
        /// <param name="element">目标元素</param>
        /// <param name="property">响应式属性（true=可见）</param>
        /// <returns>订阅句柄</returns>
        protected IDisposable BindToVisibility(VisualElement element, ReactiveProperty<bool> property)
        {
            if (element == null)
            {
                UnityEngine.Debug.LogWarning("[YokiFrameToolPage] BindToVisibility: Element 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            // 立即设置初始值
            element.style.display = property.Value ? DisplayStyle.Flex : DisplayStyle.None;
            
            var subscription = property.Subscribe(visible =>
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            });
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 绑定 ListView 到 ReactiveCollection
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="listView">目标 ListView</param>
        /// <param name="collection">响应式集合</param>
        /// <param name="makeItem">创建列表项回调</param>
        /// <param name="bindItem">绑定列表项回调</param>
        /// <returns>订阅句柄</returns>
        protected IDisposable BindToListView<T>(
            ListView listView,
            ReactiveCollection<T> collection,
            Func<VisualElement> makeItem,
            Action<VisualElement, int> bindItem)
        {
            if (listView == null)
            {
                UnityEngine.Debug.LogWarning("[YokiFrameToolPage] BindToListView: ListView 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = collection;

            var subscription = collection.Subscribe(_ =>
            {
                listView.RefreshItems();
            });
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 创建防抖器（自动管理生命周期）
        /// </summary>
        protected Debounce CreateDebounce(float delaySeconds)
        {
            var debounce = new Debounce(delaySeconds, Root);
            Subscriptions.Add(debounce);
            return debounce;
        }

        /// <summary>
        /// 创建节流器（自动管理生命周期）
        /// </summary>
        protected Throttle CreateThrottle(float intervalSeconds)
        {
            var throttle = new Throttle(intervalSeconds);
            Subscriptions.Add(throttle);
            return throttle;
        }

        #endregion
        
        #region 工具栏组件

        /// <summary>
        /// 创建工具栏容器
        /// </summary>
        protected VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            return toolbar;
        }
        
        /// <summary>
        /// 创建工具栏主按钮（品牌色填充）
        /// </summary>
        protected Button CreateToolbarPrimaryButton(string text, Action onClick) 
            => YokiFrameUIComponents.CreateToolbarPrimaryButton(text, onClick);
        
        /// <summary>
        /// 创建工具栏次要按钮
        /// </summary>
        protected Button CreateToolbarButton(string text, Action onClick) 
            => YokiFrameUIComponents.CreateToolbarButton(text, onClick);
        
        /// <summary>
        /// 创建工具栏 Toggle
        /// </summary>
        protected VisualElement CreateToolbarToggle(string text, bool value, Action<bool> onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("toolbar-toggle");
            if (value) container.AddToClassList("checked");
            
            var label = new Label(text);
            label.AddToClassList("toolbar-toggle-label");
            container.Add(label);
            
            container.RegisterCallback<ClickEvent>(_ =>
            {
                bool isChecked = container.ClassListContains("checked");
                if (isChecked)
                    container.RemoveFromClassList("checked");
                else
                    container.AddToClassList("checked");
                onChanged?.Invoke(!isChecked);
            });
            
            return container;
        }
        
        /// <summary>
        /// 创建工具栏弹性空间
        /// </summary>
        protected VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            return spacer;
        }

        #endregion

        #region 卡片与布局组件

        /// <summary>
        /// 创建现代化卡片
        /// </summary>
        protected (VisualElement card, VisualElement body) CreateCard(string title = null, string icon = null) 
            => YokiFrameUIComponents.CreateCard(title, icon);
        
        /// <summary>
        /// 创建分割面板
        /// </summary>
        protected TwoPaneSplitView CreateSplitView(float initialLeftWidth = 280f)
        {
            var splitView = new TwoPaneSplitView(0, initialLeftWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;
            return splitView;
        }
        
        /// <summary>
        /// 创建面板头部
        /// </summary>
        protected VisualElement CreatePanelHeader(string title)
        {
            var header = new VisualElement();
            header.AddToClassList("panel-header");
            
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("panel-title");
            header.Add(titleLabel);
            
            return header;
        }

        #endregion

        #region 表单与信息组件

        /// <summary>
        /// 创建现代化 Toggle 开关
        /// </summary>
        protected VisualElement CreateModernToggle(string label, bool value, Action<bool> onChanged) 
            => YokiFrameUIComponents.CreateModernToggle(label, value, onChanged);
        
        /// <summary>
        /// 创建信息行
        /// </summary>
        protected (VisualElement row, Label valueLabel) CreateInfoRow(string label, string initialValue = "-") 
            => YokiFrameUIComponents.CreateInfoRow(label, initialValue);
        
        /// <summary>
        /// 创建整数配置行
        /// </summary>
        protected (VisualElement row, TextField field) CreateIntConfigRow(
            string label, int value, Action<int> onChanged, int minValue = int.MinValue) 
            => YokiFrameUIComponents.CreateIntConfigRow(label, value, onChanged, minValue);

        #endregion

        #region 按钮组件

        /// <summary>
        /// 创建主按钮
        /// </summary>
        protected Button CreatePrimaryButton(string text, Action onClick) 
            => YokiFrameUIComponents.CreatePrimaryButton(text, onClick);
        
        /// <summary>
        /// 创建次要按钮
        /// </summary>
        protected Button CreateSecondaryButton(string text, Action onClick) 
            => YokiFrameUIComponents.CreateSecondaryButton(text, onClick);
        
        /// <summary>
        /// 创建危险按钮
        /// </summary>
        protected Button CreateDangerButton(string text, Action onClick) 
            => YokiFrameUIComponents.CreateDangerButton(text, onClick);
        
        /// <summary>
        /// 创建带图标的工具栏按钮
        /// </summary>
        protected Button CreateToolbarButtonWithIcon(string iconId, string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarButtonWithIcon(iconId, text, onClick);
        
        /// <summary>
        /// 创建带图标的操作按钮
        /// </summary>
        protected Button CreateActionButtonWithIcon(string iconId, string text, Action onClick, bool isDanger = false)
            => YokiFrameUIComponents.CreateActionButtonWithIcon(iconId, text, onClick, isDanger);

        #endregion

        #region 提示与状态组件

        /// <summary>
        /// 创建帮助框
        /// </summary>
        protected VisualElement CreateHelpBox(string message) 
            => YokiFrameUIComponents.CreateHelpBox(message);
        
        /// <summary>
        /// 创建帮助框（带类型）
        /// </summary>
        protected VisualElement CreateHelpBox(string message, YokiFrameUIComponents.HelpBoxType type) 
            => YokiFrameUIComponents.CreateHelpBox(message, type);
        
        /// <summary>
        /// 创建空状态提示
        /// </summary>
        protected VisualElement CreateEmptyState(string message) 
            => YokiFrameUIComponents.CreateEmptyState(KitIcons.INFO, message);
        
        /// <summary>
        /// 创建空状态提示（带图标和提示）
        /// </summary>
        protected VisualElement CreateEmptyState(string icon, string message, string hint = null) 
            => YokiFrameUIComponents.CreateEmptyState(icon, message, hint);
        
        /// <summary>
        /// 创建分隔线
        /// </summary>
        protected VisualElement CreateDivider() 
            => YokiFrameUIComponents.CreateDivider();

        #endregion

        #region 动画辅助

        /// <summary>
        /// 为元素添加淡入动画
        /// </summary>
        protected void AddFadeInAnimation(VisualElement element, int delayMs = 0) 
            => YokiFrameUIComponents.AddFadeInAnimation(element, delayMs);
        
        /// <summary>
        /// 为元素添加滑入动画
        /// </summary>
        protected void AddSlideInAnimation(VisualElement element, YokiFrameUIComponents.SlideDirection direction, int delayMs = 0) 
            => YokiFrameUIComponents.AddSlideInAnimation(element, direction, delayMs);

        #endregion
    }
}
#endif
