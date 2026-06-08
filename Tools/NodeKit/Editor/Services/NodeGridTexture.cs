using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// In-memory procedural dot-grid texture for the node graph background.
    /// Applied as a UIToolkit background-image on a VisualElement inside contentViewContainer,
    /// so pan and zoom are handled natively by the GraphView view transform.
    /// </summary>
    internal static class NodeGridTexture
    {
        private static Texture2D sCached;

        internal static Texture2D GetOrCreate()
        {
            if (sCached != default) return sCached;

            const int tilePixels = 20;
            const int dotRadius = 2;
            const float dotAlpha = 0.15f;

            sCached = new Texture2D(tilePixels, tilePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[tilePixels * tilePixels];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Draw a filled circle at each corner of the tile (they all meet at grid intersections)
            Color dotColor = new(1f, 1f, 1f, dotAlpha);

            // Center dot (all 4 quadrants contribute to form one dot at the intersection)
            int cx = 0; // Dot at origin (tiles to form grid at all corners)
            int cy = 0;
            for (int dy = -dotRadius; dy <= dotRadius; dy++)
            {
                for (int dx = -dotRadius; dx <= dotRadius; dx++)
                {
                    if (dx * dx + dy * dy > dotRadius * dotRadius) continue;
                    int px = (cx + dx + tilePixels) % tilePixels;
                    int py = (cy + dy + tilePixels) % tilePixels;
                    pixels[py * tilePixels + px] = dotColor;
                }
            }

            sCached.SetPixels(pixels);
            sCached.Apply();
            return sCached;
        }
    }
}
