#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - Kit 模块图标绘制
    /// </summary>
    public static partial class KitIconGenerator
    {
        #region Kit 图标绘制

        /// <summary>
        /// 架构图标 - 建筑/积木形状
        /// </summary>
        private static void DrawArchitectureIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 24, 6, color);
            DrawFilledRect(tex, 6, 10, 5, 12, color);
            DrawFilledRect(tex, 21, 10, 5, 12, color);
            DrawFilledRect(tex, 4, 22, 24, 6, color);
            DrawFilledRect(tex, 11, 14, 10, 4, color);
        }

        /// <summary>
        /// 盒子图标 - 资源包
        /// </summary>
        private static void DrawBoxIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 24, 20, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 4, 24, 24, 4, darker);
            DrawFilledRect(tex, 14, 4, 4, 24, darker);
        }

        /// <summary>
        /// 文档图标 - 日志
        /// </summary>
        private static void DrawDocumentIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 2, 20, 28, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledTriangle(tex, 20, 30, 26, 30, 26, 24, darker);
            var line = new Color32(255, 255, 255, 180);
            DrawFilledRect(tex, 9, 22, 14, 2, line);
            DrawFilledRect(tex, 9, 17, 14, 2, line);
            DrawFilledRect(tex, 9, 12, 10, 2, line);
        }

        /// <summary>
        /// 齿轮图标 - 代码生成
        /// </summary>
        private static void DrawGearIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            DrawFilledCircle(tex, cx, cy, 5, new Color32(0, 0, 0, 0));
            DrawFilledRect(tex, 14, 2, 4, 6, color);
            DrawFilledRect(tex, 14, 24, 4, 6, color);
            DrawFilledRect(tex, 2, 14, 6, 4, color);
            DrawFilledRect(tex, 24, 14, 6, 4, color);
        }

        /// <summary>
        /// 信号图标 - 事件
        /// </summary>
        private static void DrawSignalIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 4, color);
            DrawCircleOutline(tex, cx, cy, 8, 2, color);
            DrawCircleOutline(tex, cx, cy, 12, 2, color);
        }

        /// <summary>
        /// 循环图标 - 状态机
        /// </summary>
        private static void DrawCycleIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawCircleOutline(tex, cx, cy, 11, 3, color);
            DrawFilledTriangle(tex, 16, 28, 22, 22, 16, 22, color);
        }

        /// <summary>
        /// 回收图标 - 对象池
        /// </summary>
        private static void DrawRecycleIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledTriangle(tex, cx, cy + 10, cx - 9, cy - 5, cx + 9, cy - 5, color);
            DrawFilledTriangle(tex, cx, cy + 4, cx - 4, cy - 2, cx + 4, cy - 2, new Color32(0, 0, 0, 0));
        }

        /// <summary>
        /// 靶心图标 - 单例
        /// </summary>
        private static void DrawTargetIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 13, color);
            DrawFilledCircle(tex, cx, cy, 9, new Color32(0, 0, 0, 0));
            DrawFilledCircle(tex, cx, cy, 6, color);
            DrawFilledCircle(tex, cx, cy, 3, new Color32(0, 0, 0, 0));
        }

        /// <summary>
        /// 链条图标 - 流式API
        /// </summary>
        private static void DrawChainIcon(Texture2D tex, Color32 color)
        {
            DrawCircleOutline(tex, 11, 16, 7, 3, color);
            DrawCircleOutline(tex, 21, 16, 7, 3, color);
        }

        /// <summary>
        /// 工具箱图标
        /// </summary>
        private static void DrawToolboxIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 8, 24, 18, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 12, 26, 8, 4, darker);
            DrawFilledRect(tex, 4, 18, 24, 2, darker);
        }

        /// <summary>
        /// 闪电图标 - 动作
        /// </summary>
        private static void DrawLightningIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 18, 30, 10, 18, 18, 18, color);
            DrawFilledTriangle(tex, 14, 2, 22, 14, 14, 14, color);
            DrawFilledRect(tex, 14, 14, 4, 6, color);
        }

        /// <summary>
        /// 扬声器图标 - 音频
        /// </summary>
        private static void DrawSpeakerIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 12, 8, 8, color);
            DrawFilledTriangle(tex, 12, 12, 12, 20, 20, 26, color);
            DrawFilledTriangle(tex, 12, 12, 20, 6, 20, 26, color);
            DrawCircleOutline(tex, 20, 16, 6, 2, color);
            DrawCircleOutline(tex, 20, 16, 10, 2, color);
        }

        /// <summary>
        /// 星星图标 - Buff
        /// </summary>
        private static void DrawSparkleIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledTriangle(tex, cx, cy + 12, cx - 4, cy, cx + 4, cy, color);
            DrawFilledTriangle(tex, cx, cy - 12, cx - 4, cy, cx + 4, cy, color);
            DrawFilledTriangle(tex, cx - 12, cy, cx, cy - 4, cx, cy + 4, color);
            DrawFilledTriangle(tex, cx + 12, cy, cx, cy - 4, cx, cy + 4, color);
        }

        /// <summary>
        /// 地球图标 - 本地化
        /// </summary>
        private static void DrawGlobeIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 13, color);
            var line = new Color32(255, 255, 255, 150);
            DrawLine(tex, cx, 3, cx, 29, 2, line);
            DrawLine(tex, 3, cy, 29, cy, 2, line);
            DrawCircleOutline(tex, cx, cy, 8, 2, line);
        }

        /// <summary>
        /// 软盘图标 - 存档
        /// </summary>
        private static void DrawDiskIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 24, 24, color);
            var lighter = new Color32(255, 255, 255, 200);
            DrawFilledRect(tex, 8, 16, 16, 10, lighter);
            var darker = new Color32((byte)(color.r * 0.5f), (byte)(color.g * 0.5f), (byte)(color.b * 0.5f), 255);
            DrawFilledRect(tex, 10, 4, 12, 8, darker);
        }

        /// <summary>
        /// 场记板图标 - 场景
        /// </summary>
        private static void DrawClapperIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 24, 18, color);
            var darker = new Color32((byte)(color.r * 0.6f), (byte)(color.g * 0.6f), (byte)(color.b * 0.6f), 255);
            DrawFilledRect(tex, 4, 22, 24, 6, darker);
            var white = new Color32(255, 255, 255, 255);
            DrawFilledRect(tex, 8, 22, 4, 6, white);
            DrawFilledRect(tex, 16, 22, 4, 6, white);
            DrawFilledRect(tex, 24, 22, 4, 6, white);
        }

        /// <summary>
        /// 图表图标 - 表格
        /// </summary>
        private static void DrawChartIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 6, 20, color);
            DrawFilledRect(tex, 13, 10, 6, 14, color);
            DrawFilledRect(tex, 22, 6, 6, 18, color);
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 2, 2, 28, 2, darker);
        }

        /// <summary>
        /// 画框图标 - UI
        /// </summary>
        private static void DrawFrameIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 4, 4, 24, 24, color);
            DrawFilledRect(tex, 7, 7, 18, 18, new Color32(0, 0, 0, 0));
            var lighter = new Color32((byte)Mathf.Min(255, color.r + 50), (byte)Mathf.Min(255, color.g + 50), (byte)Mathf.Min(255, color.b + 50), 255);
            DrawFilledRect(tex, 9, 9, 6, 6, lighter);
            DrawFilledRect(tex, 17, 9, 6, 6, lighter);
            DrawFilledRect(tex, 9, 17, 14, 4, lighter);
        }

        /// <summary>
        /// 书本图标 - 文档
        /// </summary>
        private static void DrawBookIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 4, 20, 24, color);
            var darker = new Color32((byte)(color.r * 0.6f), (byte)(color.g * 0.6f), (byte)(color.b * 0.6f), 255);
            DrawFilledRect(tex, 6, 4, 4, 24, darker);
            var line = new Color32(255, 255, 255, 150);
            DrawFilledRect(tex, 13, 10, 10, 2, line);
            DrawFilledRect(tex, 13, 15, 10, 2, line);
            DrawFilledRect(tex, 13, 20, 8, 2, line);
        }

        /// <summary>
        /// 默认图标 - 方块
        /// </summary>
        private static void DrawDefaultIcon(Texture2D tex, Color32 color)
        {
            DrawFilledRect(tex, 6, 6, 20, 20, color);
        }

        /// <summary>
        /// 设置图标 - 齿轮（与 CODEGEN 相同形状，不同颜色）
        /// </summary>
        private static void DrawSettingsIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            DrawFilledCircle(tex, cx, cy, 12, color);
            DrawFilledCircle(tex, cx, cy, 5, new Color32(0, 0, 0, 0));
            DrawFilledRect(tex, 14, 2, 4, 6, color);
            DrawFilledRect(tex, 14, 24, 4, 6, color);
            DrawFilledRect(tex, 2, 14, 6, 4, color);
            DrawFilledRect(tex, 24, 14, 6, 4, color);
        }

        /// <summary>
        /// 重置图标 - 逆时针箭头
        /// </summary>
        private static void DrawResetIcon(Texture2D tex, Color32 color)
        {
            int cx = 16, cy = 16;
            // 绘制圆弧（用圆环模拟）
            DrawCircleOutline(tex, cx, cy, 10, 3, color);
            // 清除右上角部分形成缺口
            DrawFilledRect(tex, 16, 4, 12, 12, new Color32(0, 0, 0, 0));
            // 绘制箭头
            DrawFilledTriangle(tex, 20, 4, 26, 10, 20, 16, color);
        }

        /// <summary>
        /// 手柄图标 - InputKit
        /// </summary>
        private static void DrawGamepadIcon(Texture2D tex, Color32 color)
        {
            // 手柄主体
            DrawFilledRect(tex, 4, 10, 24, 14, color);
            // 左摇杆
            DrawFilledCircle(tex, 10, 16, 4, new Color32(255, 255, 255, 180));
            // 右按钮区
            DrawFilledCircle(tex, 22, 14, 2, new Color32(255, 255, 255, 180));
            DrawFilledCircle(tex, 22, 18, 2, new Color32(255, 255, 255, 180));
            // 手柄握把
            var darker = new Color32((byte)(color.r * 0.7f), (byte)(color.g * 0.7f), (byte)(color.b * 0.7f), 255);
            DrawFilledRect(tex, 4, 20, 6, 6, darker);
            DrawFilledRect(tex, 22, 20, 6, 6, darker);
        }

        /// <summary>
        /// 键盘图标
        /// </summary>
        private static void DrawKeyboardIcon(Texture2D tex, Color32 color)
        {
            // 键盘外框
            DrawFilledRect(tex, 2, 8, 28, 16, color);
            var keyColor = new Color32(255, 255, 255, 180);
            // 按键行
            for (int row = 0; row < 3; row++)
            {
                int y = 10 + row * 4;
                int startX = 4 + row;
                for (int i = 0; i < 6 - row; i++)
                {
                    DrawFilledRect(tex, startX + i * 4, y, 3, 3, keyColor);
                }
            }
        }

        /// <summary>
        /// 触摸图标
        /// </summary>
        private static void DrawTouchIcon(Texture2D tex, Color32 color)
        {
            // 手指
            DrawFilledCircle(tex, 16, 10, 6, color);
            DrawFilledRect(tex, 13, 10, 6, 14, color);
            // 触摸波纹
            DrawCircleOutline(tex, 16, 10, 10, 2, new Color32(color.r, color.g, color.b, 150));
            DrawCircleOutline(tex, 16, 10, 14, 2, new Color32(color.r, color.g, color.b, 80));
        }

        /// <summary>
        /// 空间网格图标 - SpatialKit
        /// </summary>
        private static void DrawSpatialGridIcon(Texture2D tex, Color32 color)
        {
            // 绘制 3x3 网格
            var lineColor = color;
            // 垂直线
            DrawFilledRect(tex, 10, 4, 2, 24, lineColor);
            DrawFilledRect(tex, 20, 4, 2, 24, lineColor);
            // 水平线
            DrawFilledRect(tex, 4, 10, 24, 2, lineColor);
            DrawFilledRect(tex, 4, 20, 24, 2, lineColor);
            // 外框
            DrawFilledRect(tex, 4, 4, 24, 2, lineColor);
            DrawFilledRect(tex, 4, 26, 24, 2, lineColor);
            DrawFilledRect(tex, 4, 4, 2, 24, lineColor);
            DrawFilledRect(tex, 26, 4, 2, 24, lineColor);
            // 中心点标记
            var highlight = new Color32(255, 255, 255, 200);
            DrawFilledCircle(tex, 16, 16, 3, highlight);
        }

        #endregion
    }
}
#endif
