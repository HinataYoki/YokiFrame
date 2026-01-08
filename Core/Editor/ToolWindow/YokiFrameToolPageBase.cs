#if UNITY_EDITOR
using System;
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
        public virtual string PageIcon => "📄";
        public virtual int Priority => 100;
        
        protected bool IsPlaying => EditorApplication.isPlaying;
        protected VisualElement Root { get; private set; }
        
        /// <summary>
        /// 订阅管理器 - 自动在 OnDeactivate 时清理所有订阅
        /// </summary>
        protected CompositeDisposable Subscriptions { get; } = new(8);
        
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
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
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

        #region 响应式数据绑定

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
        protected (VisualElement row, IntegerField field) CreateIntConfigRow(
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

        #endregion

        #region 提示与状态组件

        /// <summary>
        /// 创建帮助框
        /// </summary>
        protected VisualElement CreateHelpBox(string message) 
            => YokiFrameUIComponents.CreateHelpBox(message);
        
        /// <summary>
        /// 创建空状态提示
        /// </summary>
        protected VisualElement CreateEmptyState(string message) 
            => YokiFrameUIComponents.CreateEmptyState("📭", message);
        
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
