#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - UI 操作和通用图标绘制
    /// </summary>
    public static partial class KitIconGenerator
    {
        #region UI 操作图标绘制

        /// <summary>
        /// 弹出图标 - 窗口弹出
        /// </summary>
        private static void DrawPopoutIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 20, 20, color);
            DrawFilledRect(tex, 7, 7, 14, 14, new Color32(0, 0, 0, 0));
            DrawFilledRect(tex, 18, 18, 10, 3, color);
            DrawFilledRect(tex, 25, 11, 3, 10, color);
            DrawLine(tex, 14, 14, 24, 24, 2, color);
        }

        /// <summary>
        /// 文档文件夹图标
        /// </summary>
        private static void DrawFolderDocsIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 6, 24, 18, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 4, 24, 10, 4, darker);
            var line = new Color32(255, 255, 255, 180);
            DrawFilledRect(tex, 8, 12, 16, 2, line);
            DrawFilledRect(tex, 8, 17, 12, 2, line);
        }

        /// <summary>
        /// 工具文件夹图标
        /// </summary>
        private static void DrawFolderToolsIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 6, 24, 18, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 4, 24, 10, 4, darker);
            var line = new Color32(255, 255, 255, 200);
            DrawFilledRect(tex, 12, 10, 3, 12, line);
            DrawFilledRect(tex, 10, 10, 7, 3, line);
        }

        /// <summary>
        /// 提示图标 - 灯泡
        /// </summary>
        private static void DrawTipIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 18;
            DrawFilledCircle(tex, cx, cy, 10, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 12, 4, 8, 6, darker);
            DrawFilledRect(tex, 13, 5, 6, 1, color);
            DrawFilledRect(tex, 13, 7, 6, 1, color);
        }

        /// <summary>
        /// 分类图标 - 带字母的圆形
        /// </summary>
        private static void DrawCategoryIcon(Texture2D tex, Color32 color, string letter)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            var white = new Color32(255, 255, 255, 255);
            switch (letter)
            {
                case "C":
                    DrawCircleOutline(tex, cx, cy, 7, 3, white);
                    DrawFilledRect(tex, 18, 10, 6, 12, new Color32(0, 0, 0, 0));
                    break;
                case "K":
                    DrawFilledRect(tex, 10, 8, 3, 16, white);
                    DrawLine(tex, 13, 16, 20, 8, 3, white);
                    DrawLine(tex, 13, 16, 20, 24, 3, white);
                    break;
                case "T":
                    DrawFilledRect(tex, 9, 20, 14, 3, white);
                    DrawFilledRect(tex, 14, 8, 4, 15, white);
                    break;
            }
        }

        /// <summary>
        /// 包图标
        /// </summary>
        private static void DrawPackageIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 6, 20, 16, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 6, 22, 20, 4, darker);
            DrawFilledRect(tex, 14, 6, 4, 20, darker);
        }

        /// <summary>
        /// GitHub 图标 - 简化的猫头形状
        /// </summary>
        private static void DrawGitHubIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            DrawFilledTriangle(tex, 6, 24, 6, 28, 10, 26, color);
            DrawFilledTriangle(tex, 26, 24, 26, 28, 22, 26, color);
            var dark = new Color32(40, 40, 45, 255);
            DrawFilledCircle(tex, 12, 16, 2, dark);
            DrawFilledCircle(tex, 20, 16, 2, dark);
        }

        #endregion

        #region 通用操作图标绘制

        /// <summary>
        /// 刷新图标
        /// </summary>
        private static void DrawRefreshIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawCircleOutline(tex, cx, cy, 10, 3, color);
            DrawFilledTriangle(tex, 20, 26, 26, 20, 20, 20, color);
        }

        /// <summary>
        /// 复制图标
        /// </summary>
        private static void DrawCopyIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 10, 6, 16, 20, color);
            var lighter = new Color32((byte)Mathf.Min(255, color.r + 40), (byte)Mathf.Min(255, color.g + 40), (byte)Mathf.Min(255, color.b + 40), 255);
            DrawFilledRect(tex, 6, 10, 16, 20, lighter);
            DrawFilledRect(tex, 9, 13, 10, 14, new Color32(0, 0, 0, 0));
        }

        /// <summary>
        /// 删除图标
        /// </summary>
        private static void DrawDeleteIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 8, 6, 16, 20, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 6, 24, 20, 4, darker);
            DrawFilledRect(tex, 12, 26, 8, 4, darker);
        }

        /// <summary>
        /// 播放图标
        /// </summary>
        private static void DrawPlayIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 10, 6, 10, 26, 26, 16, color);
        }

        /// <summary>
        /// 暂停图标
        /// </summary>
        private static void DrawPauseIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 8, 6, 6, 20, color);
            DrawFilledRect(tex, 18, 6, 6, 20, color);
        }

        /// <summary>
        /// 停止图标
        /// </summary>
        private static void DrawStopIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 8, 8, 16, 16, color);
        }

        /// <summary>
        /// 展开图标
        /// </summary>
        private static void DrawExpandIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 8, 20, 24, 20, 16, 8, color);
        }

        #endregion
    }
}
#endif
