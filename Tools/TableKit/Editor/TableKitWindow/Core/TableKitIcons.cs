#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 独立图标生成器
    /// 动态生成矢量图标，避免 Emoji 字体兼容性问题
    /// </summary>
    [InitializeOnLoad]
    internal static class TableKitIcons
    {
        #region 图标 ID 常量

        public const string CHEVRON_RIGHT = "chevron_right";
        public const string CHEVRON_DOWN = "chevron_down";
        public const string DOT = "dot";
        public const string DELETE = "delete";

        #endregion

        private static readonly Dictionary<string, Texture2D> sIconCache = new();
        private const int ICON_SIZE = 32;

        static TableKitIcons()
        {
            EditorApplication.delayCall += GenerateAllIcons;
        }

        /// <summary>
        /// 获取图标纹理
        /// </summary>
        public static Texture2D GetIcon(string iconId)
        {
            if (sIconCache.TryGetValue(iconId, out var cached) && cached != null)
                return cached;

            var icon = GenerateIcon(iconId);
            if (icon != null)
                sIconCache[iconId] = icon;
            return icon;
        }

        private static void GenerateAllIcons()
        {
            GenerateIcon(CHEVRON_RIGHT);
            GenerateIcon(CHEVRON_DOWN);
            GenerateIcon(DOT);
            GenerateIcon(DELETE);
        }

        private static Texture2D GenerateIcon(string iconId)
        {
            if (sIconCache.TryGetValue(iconId, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // 清空为透明
            var pixels = new Color32[ICON_SIZE * ICON_SIZE];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);

            var gray = new Color32(150, 150, 160, 255);
            var green = new Color32(100, 200, 100, 255);
            var red = new Color32(220, 80, 80, 255);

            switch (iconId)
            {
                case CHEVRON_RIGHT:
                    DrawChevron(tex, gray, 0);
                    break;
                case CHEVRON_DOWN:
                    DrawChevron(tex, gray, 1);
                    break;
                case DOT:
                    DrawFilledCircle(tex, 16, 16, 6, green);
                    break;
                case DELETE:
                    DrawLine(tex, 8, 8, 24, 24, 3, red);
                    DrawLine(tex, 24, 8, 8, 24, 3, red);
                    break;
            }

            tex.Apply();
            sIconCache[iconId] = tex;
            return tex;
        }

        #region 绘制方法

        private static void DrawChevron(Texture2D tex, Color32 color, int direction)
        {
            if (direction == 0) // 右
            {
                DrawLine(tex, 12, 8, 20, 16, 3, color);
                DrawLine(tex, 20, 16, 12, 24, 3, color);
            }
            else // 下
            {
                DrawLine(tex, 8, 12, 16, 20, 3, color);
                DrawLine(tex, 16, 20, 24, 12, 3, color);
            }
        }

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

        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
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

        #endregion
    }
}
#endif
