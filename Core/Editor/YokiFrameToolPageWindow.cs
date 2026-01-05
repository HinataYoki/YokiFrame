#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 独立的工具页面窗口，用于将页面拖拽出来单独显示
    /// </summary>
    public class YokiFrameToolPageWindow : EditorWindow
    {
        private const string STYLE_PATH = "Assets/YokiFrame/Core/Editor/Styles/YokiFrameToolStyles.uss";
        
        private IYokiFrameToolPage mPage;
        private VisualElement mPageElement;
        
        public static YokiFrameToolPageWindow Open(IYokiFrameToolPage page)
        {
            // 检查是否已有该页面的窗口
            var existingWindows = Resources.FindObjectsOfTypeAll<YokiFrameToolPageWindow>();
            foreach (var win in existingWindows)
            {
                if (win.mPage?.GetType() == page.GetType())
                {
                    win.Focus();
                    return win;
                }
            }
            
            // 创建新窗口
            var window = CreateInstance<YokiFrameToolPageWindow>();
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
            if (mPage == null) return;
            
            var root = rootVisualElement;
            
            // 加载样式
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            
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
