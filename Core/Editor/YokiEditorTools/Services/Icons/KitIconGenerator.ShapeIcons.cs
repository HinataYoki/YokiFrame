#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - 形状图标绘制（箭头、圆点、菱形、三角形等）
    /// </summary>
    public static partial class KitIconGenerator
    {
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

        #region 圆点和形状图标绘制

        /// <summary>
        /// 实心圆点图标
        /// </summary>
        private static void DrawDotFilledIcon(Texture2D tex, Color32 color)
        {
            DrawFilledCircle(tex, 16, 16, 8, color);
        }

        /// <summary>
        /// 空心圆点图标
        /// </summary>
        private static void DrawDotEmptyIcon(Texture2D tex, Color32 color)
        {
            DrawCircleOutline(tex, 16, 16, 8, 2, color);
        }

        /// <summary>
        /// 半圆图标（暂停状态）
        /// </summary>
        private static void DrawDotHalfIcon(Texture2D tex, Color32 color)
        {
            // 左半圆实心
            for (int y = 0; y < ICON_SIZE; y++)
            {
                for (int x = 0; x < ICON_SIZE; x++)
                {
                    int dx = x - 16;
                    int dy = y - 16;
                    int d2 = dx * dx + dy * dy;
                    if (d2 <= 64) // 半径 8
                    {
                        if (dx <= 0)
                            tex.SetPixel(x, y, color);
                    }
                }
            }
            // 右半圆空心
            DrawCircleOutline(tex, 16, 16, 8, 2, color);
        }

        /// <summary>
        /// 菱形图标
        /// </summary>
        private static void DrawDiamondIcon(Texture2D tex, Color32 color)
        {
            // 绘制菱形轮廓
            DrawLine(tex, 16, 6, 26, 16, 2, color);
            DrawLine(tex, 26, 16, 16, 26, 2, color);
            DrawLine(tex, 16, 26, 6, 16, 2, color);
            DrawLine(tex, 6, 16, 16, 6, 2, color);
        }

        /// <summary>
        /// 向上三角形图标
        /// </summary>
        private static void DrawTriangleUpIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 16, 24, 6, 8, 26, 8, color);
        }

        /// <summary>
        /// 向下三角形图标
        /// </summary>
        private static void DrawTriangleDownIcon(Texture2D tex, Color32 color)
        {
            DrawFilledTriangle(tex, 16, 8, 6, 24, 26, 24, color);
        }

        #endregion
    }
}
#endif
