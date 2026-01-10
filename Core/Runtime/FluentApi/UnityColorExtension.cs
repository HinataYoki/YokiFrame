using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Color 扩展方法
    /// </summary>
    public static class UnityColorExtension
    {
        #region 分量修改
        
        /// <summary>
        /// 修改 R 分量
        /// </summary>
        public static Color R(this Color self, float r)
        {
            self.r = r;
            return self;
        }

        /// <summary>
        /// 修改 G 分量
        /// </summary>
        public static Color G(this Color self, float g)
        {
            self.g = g;
            return self;
        }

        /// <summary>
        /// 修改 B 分量
        /// </summary>
        public static Color B(this Color self, float b)
        {
            self.b = b;
            return self;
        }

        /// <summary>
        /// 修改 Alpha 分量
        /// </summary>
        public static Color A(this Color self, float a)
        {
            self.a = a;
            return self;
        }

        #endregion

        #region 常用操作
        
        /// <summary>
        /// 设置透明度（返回新 Color）
        /// </summary>
        public static Color WithAlpha(this Color self, float alpha)
        {
            return new Color(self.r, self.g, self.b, alpha);
        }

        /// <summary>
        /// 转为完全不透明
        /// </summary>
        public static Color Opaque(this Color self)
        {
            return new Color(self.r, self.g, self.b, 1f);
        }

        /// <summary>
        /// 转为完全透明
        /// </summary>
        public static Color Transparent(this Color self)
        {
            return new Color(self.r, self.g, self.b, 0f);
        }

        /// <summary>
        /// 反转颜色（不影响 Alpha）
        /// </summary>
        public static Color Invert(this Color self)
        {
            return new Color(1f - self.r, 1f - self.g, 1f - self.b, self.a);
        }

        /// <summary>
        /// 转为灰度
        /// </summary>
        public static Color Grayscale(this Color self)
        {
            float gray = self.grayscale;
            return new Color(gray, gray, gray, self.a);
        }

        #endregion

        #region 颜色混合
        
        /// <summary>
        /// 线性插值到目标颜色
        /// </summary>
        public static Color LerpTo(this Color self, Color target, float t)
        {
            return Color.Lerp(self, target, t);
        }

        /// <summary>
        /// 与另一颜色混合
        /// </summary>
        public static Color Blend(this Color self, Color other, float ratio = 0.5f)
        {
            return Color.Lerp(self, other, ratio);
        }

        #endregion

        #region 转换
        
        /// <summary>
        /// 转为 Color32
        /// </summary>
        public static Color32 ToColor32(this Color self)
        {
            return self;
        }

        /// <summary>
        /// 转为十六进制字符串（不含 #）
        /// </summary>
        public static string ToHex(this Color self, bool includeAlpha = false)
        {
            Color32 c = self;
            return includeAlpha 
                ? $"{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}" 
                : $"{c.r:X2}{c.g:X2}{c.b:X2}";
        }

        /// <summary>
        /// 从十六进制字符串解析颜色
        /// </summary>
        public static Color FromHex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            
            if (hex.Length == 6)
            {
                return new Color(
                    int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    1f
                );
            }
            else if (hex.Length == 8)
            {
                return new Color(
                    int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f
                );
            }
            
            return Color.white;
        }

        #endregion
    }
}
