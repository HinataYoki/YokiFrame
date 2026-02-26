#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Figma Vector2 调试窗口 - 用于验证组件是否正常工作
    /// </summary>
    public class FigmaVector2DebugWindow : EditorWindow
    {
        [MenuItem("YokiFrame/Debug/Figma Vector2 Test Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<FigmaVector2DebugWindow>();
            window.titleContent = new GUIContent("Figma Vector2 Test");
            window.minSize = new Vector2(400, 300);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            var title = new Label("Figma Vector2 输入框测试");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);

            // 测试 1：基础 Figma Vector2 输入
            var test1 = CreateFigmaVector2Input(
                label: "测试分辨率",
                value: new Vector2(1920, 1080),
                onChanged: v => { },
                onSwap: () => { }
            );
            root.Add(test1);

            // 测试 2：无对调按钮
            var test2 = CreateFigmaVector2Input(
                label: "无对调按钮",
                value: new Vector2(800, 600),
                onChanged: v => { }
            );
            root.Add(test2);

            // 测试 3：Vector2Int
            var test3 = CreateFigmaVector2IntInput(
                label: "整数分辨率",
                value: new Vector2Int(1280, 720),
                onChanged: v => { },
                onSwap: () => { }
            );
            root.Add(test3);
        }
    }
}
#endif
