#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具页面基类
    /// </summary>
    public abstract class YokiFrameToolPageBase : IYokiFrameToolPage
    {
        public abstract string PageName { get; }
        public virtual string PageIcon => "📄";
        public virtual int Priority => 100;
        
        protected bool IsPlaying => EditorApplication.isPlaying;
        protected VisualElement Root { get; private set; }
        
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
        
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }
        public virtual void OnUpdate() { }
        
        /// <summary>
        /// 创建工具栏
        /// </summary>
        protected VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            return toolbar;
        }
        
        /// <summary>
        /// 创建工具栏按钮
        /// </summary>
        protected Button CreateToolbarButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("toolbar-button");
            return button;
        }
        
        /// <summary>
        /// 创建工具栏 Toggle
        /// </summary>
        protected VisualElement CreateToolbarToggle(string text, bool value, System.Action<bool> onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("toolbar-toggle");
            if (value) container.AddToClassList("checked");
            
            var label = new Label(text);
            label.AddToClassList("toolbar-toggle-label");
            container.Add(label);
            
            container.RegisterCallback<ClickEvent>(evt =>
            {
                var isChecked = container.ClassListContains("checked");
                if (isChecked)
                    container.RemoveFromClassList("checked");
                else
                    container.AddToClassList("checked");
                onChanged?.Invoke(!isChecked);
            });
            
            return container;
        }
        
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
        
        /// <summary>
        /// 创建帮助框
        /// </summary>
        protected VisualElement CreateHelpBox(string message)
        {
            var box = new VisualElement();
            box.AddToClassList("help-box");
            
            var text = new Label(message);
            text.AddToClassList("help-box-text");
            box.Add(text);
            
            return box;
        }
        
        /// <summary>
        /// 创建空状态提示
        /// </summary>
        protected VisualElement CreateEmptyState(string message)
        {
            var container = new VisualElement();
            container.AddToClassList("empty-state");
            
            var text = new Label(message);
            text.AddToClassList("empty-state-text");
            container.Add(text);
            
            return container;
        }
    }
}
#endif
