#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 创建面板功能 - 样式辅助方法
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 辅助方法 - 样式

        /// <summary>
        /// 应用扁平输入框样式
        /// </summary>
        private void ApplyFlatInputStyle(TextField textField)
        {
            textField.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.16f));
            textField.style.borderTopWidth = 1;
            textField.style.borderBottomWidth = 1;
            textField.style.borderLeftWidth = 1;
            textField.style.borderRightWidth = 1;
            textField.style.borderTopColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            textField.style.borderBottomColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            textField.style.borderLeftColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            textField.style.borderRightColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            textField.style.borderTopLeftRadius = 4;
            textField.style.borderTopRightRadius = 4;
            textField.style.borderBottomLeftRadius = 4;
            textField.style.borderBottomRightRadius = 4;
        }

        /// <summary>
        /// 应用主角输入框样式
        /// </summary>
        private void ApplyHeroInputStyle(TextField textField)
        {
            ApplyFlatInputStyle(textField);
            textField.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            textField.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            textField.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            textField.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            textField.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        }

        /// <summary>
        /// 应用主按钮样式
        /// </summary>
        private void ApplyPrimaryButtonStyle(Button button)
        {
            button.style.backgroundColor = new StyleColor(new Color(0.2f, 0.45f, 0.75f));
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.color = new StyleColor(Color.white);

            // 悬停效果通过伪状态处理（UI Toolkit 限制，这里简化处理）
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.25f, 0.5f, 0.85f));
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new StyleColor(new Color(0.2f, 0.45f, 0.75f));
            });
        }

        #endregion
    }
}
#endif
