#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - 其他杂项图标绘制
    /// </summary>
    public static partial class KitIconGenerator
    {
        #region 其他图标绘制

        /// <summary>
        /// 剪贴板图标
        /// </summary>
        private static void DrawClipboardIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 4, 20, 24, color);
            var darker = new Color32((byte)(color.r * 0.6f), (byte)(color.g * 0.6f), (byte)(color.b * 0.6f), 255);
            DrawFilledRect(tex, 10, 24, 12, 4, darker);
            var line = new Color32(255, 255, 255, 150);
            DrawFilledRect(tex, 10, 10, 12, 2, line);
            DrawFilledRect(tex, 10, 15, 12, 2, line);
        }

        /// <summary>
        /// 堆栈图标
        /// </summary>
        private static void DrawStackIcon(Texture2D tex, Color32 color)
        {
            var c1 = color;
            var c2 = new Color32((byte)(color.r * 0.8f), (byte)(color.g * 0.8f), (byte)(color.b * 0.8f), 255);
            var c3 = new Color32((byte)(color.r * 0.6f), (byte)(color.g * 0.6f), (byte)(color.b * 0.6f), 255);
            DrawFilledRect(tex, 6, 4, 20, 6, c3);
            DrawFilledRect(tex, 6, 12, 20, 6, c2);
            DrawFilledRect(tex, 6, 20, 20, 6, c1);
        }

        /// <summary>
        /// 缓存图标
        /// </summary>
        private static void DrawCacheIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            var white = new Color32(255, 255, 255, 200);
            DrawFilledRect(tex, 10, 10, 12, 12, white);
        }

        /// <summary>
        /// 音乐图标
        /// </summary>
        private static void DrawMusicIcon(Texture2D tex, Color32 color)
        {
            DrawFilledCircle(tex, 10, 10, 5, color);
            DrawFilledCircle(tex, 22, 6, 5, color);
            DrawFilledRect(tex, 14, 10, 3, 18, color);
            DrawFilledRect(tex, 26, 6, 3, 18, color);
            DrawFilledRect(tex, 14, 24, 15, 3, color);
        }

        /// <summary>
        /// 音量图标
        /// </summary>
        private static void DrawVolumeIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 12, 6, 8, color);
            DrawFilledTriangle(tex, 12, 12, 12, 20, 20, 26, color);
            DrawFilledTriangle(tex, 12, 12, 20, 6, 20, 26, color);
            DrawCircleOutline(tex, 20, 16, 6, 2, color);
        }

        /// <summary>
        /// 时间轴图标
        /// </summary>
        private static void DrawTimelineIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 14, 24, 4, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 8, 10, 3, 12, darker);
            DrawFilledRect(tex, 16, 10, 3, 12, darker);
            DrawFilledRect(tex, 24, 10, 3, 12, darker);
        }

        /// <summary>
        /// 圆点图标
        /// </summary>
        private static void DrawDotIcon(Texture2D tex, Color32 color)
        {
            DrawFilledCircle(tex, 16, 16, 6, color);
        }

        /// <summary>
        /// 卷轴图标 - 日志/时间轴
        /// </summary>
        private static void DrawScrollIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 8, 6, 16, 20, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledCircle(tex, 8, 26, 3, darker);
            DrawFilledCircle(tex, 24, 26, 3, darker);
            DrawFilledRect(tex, 8, 24, 16, 4, darker);
            DrawFilledCircle(tex, 8, 6, 3, darker);
            DrawFilledCircle(tex, 24, 6, 3, darker);
            DrawFilledRect(tex, 8, 4, 16, 4, darker);
            var line = new Color32(255, 255, 255, 150);
            DrawFilledRect(tex, 11, 12, 10, 2, line);
            DrawFilledRect(tex, 11, 17, 10, 2, line);
        }

        /// <summary>
        /// 监听者图标 - 耳朵形状
        /// </summary>
        private static void DrawListenerIcon(Texture2D tex, Color32 color)
        {
            DrawCircleOutline(tex, 16, 16, 10, 3, color);
            DrawCircleOutline(tex, 16, 16, 5, 2, color);
            DrawFilledRect(tex, 14, 4, 4, 6, color);
        }

        /// <summary>
        /// 位置图标 - 地图标记
        /// </summary>
        private static void DrawLocationIcon(Texture2D tex, Color32 color)
        {
            // 绘制水滴形状的位置标记
            DrawFilledCircle(tex, 16, 18, 8, color);
            DrawFilledTriangle(tex, 10, 14, 22, 14, 16, 4, color);
            // 内部白点
            var white = new Color32(255, 255, 255, 200);
            DrawFilledCircle(tex, 16, 18, 3, white);
        }

        /// <summary>
        /// 代码图标 - 尖括号
        /// </summary>
        private static void DrawCodeIcon(Texture2D tex, Color32 color)
        {
            // 左尖括号 <
            DrawLine(tex, 12, 16, 6, 10, 2, color);
            DrawLine(tex, 6, 10, 12, 4, 2, color);
            // 右尖括号 >
            DrawLine(tex, 20, 16, 26, 10, 2, color);
            DrawLine(tex, 26, 10, 20, 4, 2, color);
            // 斜杠 /
            DrawLine(tex, 18, 22, 14, 6, 2, color);
        }

        /// <summary>
        /// 文件夹图标
        /// </summary>
        private static void DrawFolderIcon(Texture2D tex, Color32 color)
        {
            // 文件夹主体
            DrawFilledRect(tex, 4, 8, 24, 16, color);
            // 文件夹标签
            var darker = new Color32((byte)(color.r * 0.8f), (byte)(color.g * 0.8f), (byte)(color.b * 0.8f), 255);
            DrawFilledRect(tex, 4, 24, 10, 4, darker);
        }

        /// <summary>
        /// 时钟图标 - 等待/进行中
        /// </summary>
        private static void DrawClockIcon(Texture2D tex, Color32 color)
        {
            // 圆形表盘
            DrawCircleOutline(tex, 16, 16, 10, 2, color);
            // 时针
            DrawLine(tex, 16, 16, 16, 10, 2, color);
            // 分针
            DrawLine(tex, 16, 16, 22, 16, 2, color);
            // 中心点
            DrawFilledCircle(tex, 16, 16, 2, color);
        }

        /// <summary>
        /// 勾选图标 - 简单对勾
        /// </summary>
        private static void DrawCheckIcon(Texture2D tex, Color32 color)
        {
            DrawLine(tex, 8, 16, 14, 10, 3, color);
            DrawLine(tex, 14, 10, 24, 22, 3, color);
        }

        #endregion
    }
}
#endif
