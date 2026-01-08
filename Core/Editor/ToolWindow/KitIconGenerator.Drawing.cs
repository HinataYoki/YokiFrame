#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Kit 图标生成器 - 基础绘制方法
    /// </summary>
    public static partial class KitIconGenerator
    {
        #region 基础绘制方法

        /// <summary>
        /// 绘制填充矩形
        /// </summary>
        private static void DrawFilledRect(Texture2D tex, int x, int y, int w, int h, Color32 color)
        {
            for (int py = y; py < y + h && py < ICON_SIZE; py++)
            {
                for (int px = x; px < x + w && px < ICON_SIZE; px++)
                {
                    if (px >= 0 && py >= 0)
                        tex.SetPixel(px, py, color);
                }
            }
        }

        /// <summary>
        /// 绘制填充圆形
        /// </summary>
        private static void DrawFilledCircle(Texture2D tex, int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= r2)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < ICON_SIZE && py >= 0 && py < ICON_SIZE)
                            tex.SetPixel(px, py, color);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制空心圆形
        /// </summary>
        private static void DrawCircleOutline(Texture2D tex, int cx, int cy, int radius, int thickness, Color32 color)
        {
            int outerR2 = radius * radius;
            int innerR2 = (radius - thickness) * (radius - thickness);
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int d2 = x * x + y * y;
                    if (d2 <= outerR2 && d2 >= innerR2)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < ICON_SIZE && py >= 0 && py < ICON_SIZE)
                            tex.SetPixel(px, py, color);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制线段
        /// </summary>
        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // 绘制粗线
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                {
                    for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
                    {
                        int px = x0 + tx;
                        int py = y0 + ty;
                        if (px >= 0 && px < ICON_SIZE && py >= 0 && py < ICON_SIZE)
                            tex.SetPixel(px, py, color);
                    }
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>
        /// 绘制三角形
        /// </summary>
        private static void DrawFilledTriangle(Texture2D tex, int x0, int y0, int x1, int y1, int x2, int y2, Color32 color)
        {
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
            int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
            int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                    {
                        if (x >= 0 && x < ICON_SIZE && y >= 0 && y < ICON_SIZE)
                            tex.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static bool PointInTriangle(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float area = 0.5f * (-y1 * x2 + y0 * (-x1 + x2) + x0 * (y1 - y2) + x1 * y2);
            float s = 1 / (2 * area) * (y0 * x2 - x0 * y2 + (y2 - y0) * px + (x0 - x2) * py);
            float t = 1 / (2 * area) * (x0 * y1 - y0 * x1 + (y0 - y1) * px + (x1 - x0) * py);
            return s >= 0 && t >= 0 && (s + t) <= 1;
        }

        #endregion
    }
}
#endif
