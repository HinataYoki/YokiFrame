#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEditor;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 独立编辑器窗口
    /// 可在没有 YokiFrame 的项目中独立使用
    /// </summary>
    public class TableKitEditorWindow : EditorWindow
    {
        private TableKitEditorUI mEditorUI;

        [MenuItem("Tools/TableKit/配置表工具 %l", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<TableKitEditorWindow>();
            window.titleContent = new GUIContent("TableKit");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        private void CreateGUI()
        {
            mEditorUI = new TableKitEditorUI();
            rootVisualElement.Add(mEditorUI.BuildUI());
        }

        private void OnDestroy()
        {
            mEditorUI?.SavePrefs();
        }

        private void Update()
        {
            if (EditorApplication.isPlaying)
            {
                mEditorUI?.RefreshStatus();
            }
        }
    }
}
#endif
