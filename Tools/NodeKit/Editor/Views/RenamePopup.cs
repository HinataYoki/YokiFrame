using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 重命名弹窗（UIToolkit 实现）
    /// </summary>
    public class RenamePopup : EditorWindow
    {
        private static RenamePopup sCurrent;
        private Object mTarget;
        private TextField mInputField;
        private System.Action<string> mOnRename;

        /// <summary>
        /// 显示重命名弹窗
        /// </summary>
        public static RenamePopup Show(Object target, System.Action<string> onRename = null, float width = 200)
        {
            if (sCurrent != default)
                sCurrent.Close();

            var window = CreateInstance<RenamePopup>();
            sCurrent = window;
            window.mTarget = target;
            window.mOnRename = onRename;
            window.titleContent = new GUIContent($"Rename {target.name}");
            window.minSize = new Vector2(100, 50);
            window.maxSize = new Vector2(400, 50);

            // 设置位置到鼠标附近
            var mousePos = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero);
            window.position = new Rect(mousePos.x - width * 0.5f, mousePos.y - 10, width, 50);
            window.ShowPopup();
            return window;
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("yoki-rename-popup");
            rootVisualElement.style.paddingTop = 4;
            rootVisualElement.style.paddingBottom = 4;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;

            mInputField = new TextField { value = mTarget.name };
            mInputField.style.flexGrow = 1;
            mInputField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.Add(mInputField);

            // 延迟聚焦
            EditorApplication.delayCall += () =>
            {
                mInputField.Focus();
                mInputField.SelectAll();
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ApplyRename();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
        }

        private void ApplyRename()
        {
            var newName = mInputField.value?.Trim();
            if (string.IsNullOrEmpty(newName))
                newName = GetDefaultName();

            if (mTarget.name != newName)
            {
                Undo.RecordObject(mTarget, "Rename");
                mTarget.name = newName;
                EditorUtility.SetDirty(mTarget);

                // 处理资产重命名
                var assetPath = AssetDatabase.GetAssetPath(mTarget);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (mTarget is Node node && node.Graph != default)
                        AssetDatabase.SetMainObject(node.Graph, assetPath);
                    AssetDatabase.ImportAsset(assetPath);
                }

                mOnRename?.Invoke(newName);
            }
            Close();
        }

        private string GetDefaultName()
        {
            if (mTarget == default) return "Node";
            var type = mTarget.GetType();
            return ObjectNames.NicifyVariableName(type.Name);
        }

        private void OnLostFocus() => Close();

        private void OnDestroy()
        {
            if (sCurrent == this)
                sCurrent = null;
        }
    }
}
