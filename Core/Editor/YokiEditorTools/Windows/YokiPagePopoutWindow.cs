#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 独立的工具页面窗口
    /// 用于将页面弹出到单独窗口显示
    /// </summary>
    public class YokiPagePopoutWindow : EditorWindow
    {
        private IYokiToolPage mPage;
        private VisualElement mPageElement;
        
        /// <summary>
        /// 打开弹出窗口
        /// </summary>
        public static YokiPagePopoutWindow Open(IYokiToolPage page)
        {
            // 检查是否已有该页面的窗口
            var existingWindows = Resources.FindObjectsOfTypeAll<YokiPagePopoutWindow>();
            for (int i = 0; i < existingWindows.Length; i++)
            {
                var win = existingWindows[i];
                if (win.mPage?.GetType() == page.GetType())
                {
                    win.Focus();
                    return win;
                }
            }
            
            // 创建新窗口
            var window = CreateInstance<YokiPagePopoutWindow>();
            window.titleContent = new GUIContent(page.PageName);
            window.minSize = new Vector2(600, 400);
            window.mPage = page;
            window.Show();
            
            return window;
        }
        
        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            mPage?.OnDeactivate();
        }
        
        private void CreateGUI()
        {
            if (mPage == default) return;
            
            var root = rootVisualElement;
            
            // 应用样式
            YokiStyleService.Apply(root, YokiStyleProfile.Full);
            
            // 创建页面 UI
            mPageElement = mPage.CreateUI();
            root.Add(mPageElement);
            
            mPage.OnActivate();
        }
        
        private void OnEditorUpdate()
        {
            mPage?.OnUpdate();
        }
    }
}
#endif
