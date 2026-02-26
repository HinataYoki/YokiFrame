#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - 状态图标绘制
    /// </summary>
    public static partial class KitIconGenerator
    {
        #region 状态图标绘制

        /// <summary>
        /// 成功图标 - 对勾
        /// </summary>
        private static void DrawSuccessIcon(Texture2D tex, Color32 color)
        {
            DrawLine(tex, 8, 16, 14, 10, 3, color);
            DrawLine(tex, 14, 10, 24, 22, 3, color);
        }

        /// <summary>
        /// 警告图标 - 三角形感叹号
        /// </summary>
        private static void DrawWarningIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 16, 28, 4, 6, 28, 6, color);
            var dark = new Color32(60, 50, 20, 255);
            DrawFilledRect(tex, 14, 12, 4, 8, dark);
            DrawFilledRect(tex, 14, 8, 4, 3, dark);
        }

        /// <summary>
        /// 错误图标 - X
        /// </summary>
        private static void DrawErrorIcon(Texture2D tex, Color32 color)
        {
            DrawLine(tex, 8, 8, 24, 24, 3, color);
            DrawLine(tex, 24, 8, 8, 24, 3, color);
        }

        /// <summary>
        /// 信息图标 - i
        /// </summary>
        private static void DrawInfoIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            var white = new Color32(255, 255, 255, 255);
            DrawFilledRect(tex, 14, 8, 4, 12, white);
            DrawFilledRect(tex, 14, 21, 4, 4, white);
        }

        #endregion
    }
}
#endif
