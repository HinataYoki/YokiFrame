#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - UI 操作图标绘制
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

        #region 箭头图标绘制

        /// <summary>
        /// 箭头图标 - 方向: 0=右, 1=下, 2=左, 3=上
        /// </summary>
        private static void DrawArrowIcon(Texture2D tex, Color32 color, int direction)
        {
            switch (direction)
            {
                case 0:
                    DrawFilledTriangle(tex, 10, 8, 10, 24, 24, 16, color);
                    break;
                case 1:
                    DrawFilledTriangle(tex, 8, 22, 24, 22, 16, 8, color);
                    break;
                case 2:
                    DrawFilledTriangle(tex, 22, 8, 22, 24, 8, 16, color);
                    break;
                default:
                    DrawFilledTriangle(tex, 8, 10, 24, 10, 16, 24, color);
                    break;
            }
        }

        /// <summary>
        /// 折叠箭头图标 - 方向: 0=右, 1=下
        /// </summary>
        private static void DrawChevronIcon(Texture2D tex, Color32 color, int direction)
        {
            if (direction == 0)
            {
                DrawLine(tex, 12, 8, 20, 16, 3, color);
                DrawLine(tex, 20, 16, 12, 24, 3, color);
            }
            else
            {
                DrawLine(tex, 8, 12, 16, 20, 3, color);
                DrawLine(tex, 16, 20, 24, 12, 3, color);
            }
        }

        #endregion

        #region 数据流图标绘制

        /// <summary>
        /// 发送图标 - 向右箭头
        /// </summary>
        private static void DrawSendIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 13, 16, 6, color);
            DrawFilledTriangle(tex, 18, 8, 18, 24, 28, 16, color);
        }

        /// <summary>
        /// 接收图标 - 向左箭头
        /// </summary>
        private static void DrawReceiveIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 12, 13, 16, 6, color);
            DrawFilledTriangle(tex, 14, 8, 14, 24, 4, 16, color);
        }

        /// <summary>
        /// 事件图标 - 闪电
        /// </summary>
        private static void DrawEventIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 18, 28, 10, 16, 18, 16, color);
            DrawFilledTriangle(tex, 14, 4, 22, 16, 14, 16, color);
        }

        #endregion

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

        #endregion
    }
}
#endif
