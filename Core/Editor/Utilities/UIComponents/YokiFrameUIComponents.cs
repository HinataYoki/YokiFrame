#if UNITY_EDITOR
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 现代化 UI 组件工厂
    /// 提供统一风格的 UI 组件创建方法
    /// 使用 partial class 按功能模块拆分
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 设计令牌常量

        /// <summary>
        /// 颜色常量 - 统一的设计系统配色
        /// </summary>
        public static class Colors
        {
            // 品牌色
            public static readonly Color BrandPrimary = new(0.13f, 0.59f, 0.95f);      // #2196F3
            public static readonly Color BrandPrimaryHover = new(0.26f, 0.65f, 0.96f);
            public static readonly Color BrandSuccess = new(0.30f, 0.69f, 0.31f);       // #4CAF50
            public static readonly Color BrandDanger = new(0.96f, 0.26f, 0.21f);        // #F44336
            public static readonly Color BrandWarning = new(1f, 0.60f, 0f);             // #FF9800
            
            // 层级背景色
            public static readonly Color LayerCard = new(0.18f, 0.18f, 0.21f);
            public static readonly Color LayerElevated = new(0.20f, 0.22f, 0.24f);
            public static readonly Color LayerHover = new(0.23f, 0.24f, 0.27f);
            public static readonly Color LayerToolbar = new(0.15f, 0.15f, 0.15f);
            public static readonly Color LayerFilterBar = new(0.13f, 0.13f, 0.13f);
            public static readonly Color LayerTabBar = new(0.12f, 0.12f, 0.12f);
            public static readonly Color LayerSection = new(0.18f, 0.18f, 0.18f);
            
            // 文本色
            public static readonly Color TextPrimary = new(0.94f, 0.94f, 0.96f);
            public static readonly Color TextSecondary = new(0.71f, 0.73f, 0.76f);
            public static readonly Color TextTertiary = new(0.51f, 0.53f, 0.57f);
            
            // 边框色
            public static readonly Color BorderDefault = new(0.22f, 0.23f, 0.25f);
            public static readonly Color BorderLight = new(0.2f, 0.2f, 0.2f);
            
            // 状态颜色（用于状态指示器）
            public static readonly Color StatusSuccess = new(0.30f, 0.69f, 0.31f);
            public static readonly Color StatusWarning = new(1f, 0.60f, 0f);
            public static readonly Color StatusError = new(0.96f, 0.26f, 0.21f);
            public static readonly Color StatusInfo = new(0.13f, 0.59f, 0.95f);
            
            // 徽章颜色
            public static readonly Color BadgeDefault = new(0.25f, 0.25f, 0.28f);
            public static readonly Color BadgeSuccess = new(0.20f, 0.45f, 0.22f);
            public static readonly Color BadgeWarning = new(0.50f, 0.35f, 0.10f);
            public static readonly Color BadgeError = new(0.50f, 0.20f, 0.18f);
            public static readonly Color BadgeInfo = new(0.15f, 0.35f, 0.55f);
            
            // 文件状态颜色
            public static readonly Color FileExists = new(0.30f, 0.69f, 0.31f);
            public static readonly Color FileNotExists = new(0.71f, 0.73f, 0.76f);
        }

        /// <summary>
        /// 间距常量
        /// </summary>
        public static class Spacing
        {
            public const float XS = 4f;
            public const float SM = 8f;
            public const float MD = 12f;
            public const float LG = 16f;
            public const float XL = 20f;
        }

        /// <summary>
        /// 圆角常量
        /// </summary>
        public static class Radius
        {
            public const float SM = 3f;
            public const float MD = 4f;
            public const float LG = 6f;
        }

        #endregion
    }
}
#endif
