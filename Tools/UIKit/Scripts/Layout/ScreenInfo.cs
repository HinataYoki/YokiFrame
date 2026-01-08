using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 屏幕方向
    /// </summary>
    public enum ScreenAspect
    {
        Portrait,   // 竖屏
        Landscape,  // 横屏
        Square      // 方形
    }

    /// <summary>
    /// 屏幕信息工具类 - 查询屏幕尺寸、DPI 和方向
    /// </summary>
    public static class ScreenInfo
    {
        #region 事件

        /// <summary>
        /// 屏幕尺寸变化事件
        /// </summary>
        public static event Action<Vector2Int> OnScreenSizeChanged;

        /// <summary>
        /// 屏幕方向变化事件
        /// </summary>
        public static event Action<ScreenAspect> OnAspectChanged;

        #endregion

        #region 缓存

        private static Vector2Int sCachedScreenSize;
        private static float sCachedDpi;
        private static ScreenAspect sCachedAspect;
        private static bool sInitialized;

        #endregion

        #region 属性

        /// <summary>
        /// 屏幕宽度（像素）
        /// </summary>
        public static int Width => Screen.width;

        /// <summary>
        /// 屏幕高度（像素）
        /// </summary>
        public static int Height => Screen.height;

        /// <summary>
        /// 屏幕尺寸
        /// </summary>
        public static Vector2Int Size => new(Screen.width, Screen.height);

        /// <summary>
        /// 屏幕 DPI
        /// </summary>
        public static float Dpi => Screen.dpi > 0 ? Screen.dpi : DEFAULT_DPI;

        /// <summary>
        /// 屏幕宽高比
        /// </summary>
        public static float AspectRatio => Height > 0 ? (float)Width / Height : 1f;

        /// <summary>
        /// 屏幕方向
        /// </summary>
        public static ScreenAspect Aspect
        {
            get
            {
                var ratio = AspectRatio;
                if (ratio > 1.1f) return ScreenAspect.Landscape;
                if (ratio < 0.9f) return ScreenAspect.Portrait;
                return ScreenAspect.Square;
            }
        }

        /// <summary>
        /// 是否为竖屏
        /// </summary>
        public static bool IsPortrait => Aspect == ScreenAspect.Portrait;

        /// <summary>
        /// 是否为横屏
        /// </summary>
        public static bool IsLandscape => Aspect == ScreenAspect.Landscape;

        /// <summary>
        /// 安全区
        /// </summary>
        public static Rect SafeArea => Screen.safeArea;

        /// <summary>
        /// 安全区边距 (left, right, top, bottom)
        /// </summary>
        public static Vector4 SafeAreaInsets
        {
            get
            {
                var safeArea = Screen.safeArea;
                return new Vector4(
                    safeArea.x,
                    Width - safeArea.x - safeArea.width,
                    Height - safeArea.y - safeArea.height,
                    safeArea.y
                );
            }
        }

        #endregion

        #region 常量

        private const float DEFAULT_DPI = 96f;
        private const float INCH_TO_CM = 2.54f;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化屏幕监听（由 UIRoot 调用）
        /// </summary>
        internal static void Initialize()
        {
            if (sInitialized) return;
            sInitialized = true;

            sCachedScreenSize = Size;
            sCachedDpi = Dpi;
            sCachedAspect = Aspect;
        }

        /// <summary>
        /// 更新检测（由 UIRoot 在 Update 中调用）
        /// </summary>
        internal static void Update()
        {
            var currentSize = Size;
            var currentAspect = Aspect;

            if (currentSize != sCachedScreenSize)
            {
                var previousSize = sCachedScreenSize;
                sCachedScreenSize = currentSize;
                OnScreenSizeChanged?.Invoke(currentSize);
                
                // 通过 EventKit 广播
                EventKit.Type.Send(new ScreenSizeChangedEvent 
                { 
                    PreviousSize = new Vector2(previousSize.x, previousSize.y),
                    NewSize = new Vector2(currentSize.x, currentSize.y) 
                });
            }

            if (currentAspect != sCachedAspect)
            {
                sCachedAspect = currentAspect;
                OnAspectChanged?.Invoke(currentAspect);
                
                // 通过 EventKit 广播
                EventKit.Type.Send(new ScreenAspectChangedEvent { NewAspect = currentAspect });
            }
        }

        #endregion

        #region 尺寸转换

        /// <summary>
        /// 像素转 DP（密度无关像素）
        /// </summary>
        public static float PixelsToDp(float pixels) => pixels * DEFAULT_DPI / Dpi;

        /// <summary>
        /// DP 转像素
        /// </summary>
        public static float DpToPixels(float dp) => dp * Dpi / DEFAULT_DPI;

        /// <summary>
        /// 像素转英寸
        /// </summary>
        public static float PixelsToInches(float pixels) => pixels / Dpi;

        /// <summary>
        /// 英寸转像素
        /// </summary>
        public static float InchesToPixels(float inches) => inches * Dpi;

        /// <summary>
        /// 像素转厘米
        /// </summary>
        public static float PixelsToCm(float pixels) => PixelsToInches(pixels) * INCH_TO_CM;

        /// <summary>
        /// 厘米转像素
        /// </summary>
        public static float CmToPixels(float cm) => InchesToPixels(cm / INCH_TO_CM);

        #endregion

        #region 屏幕尺寸查询

        /// <summary>
        /// 获取屏幕对角线尺寸（英寸）
        /// </summary>
        public static float GetDiagonalInches()
        {
            var widthInches = PixelsToInches(Width);
            var heightInches = PixelsToInches(Height);
            return Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);
        }

        /// <summary>
        /// 是否为平板设备（对角线 > 7 英寸）
        /// </summary>
        public static bool IsTablet() => GetDiagonalInches() > 7f;

        /// <summary>
        /// 是否为手机设备（对角线 <= 7 英寸）
        /// </summary>
        public static bool IsPhone() => GetDiagonalInches() <= 7f;

        /// <summary>
        /// 获取推荐的 UI 缩放因子
        /// </summary>
        public static float GetRecommendedUIScale()
        {
            var dpi = Dpi;
            if (dpi <= 120) return 0.75f;
            if (dpi <= 160) return 1.0f;
            if (dpi <= 240) return 1.5f;
            if (dpi <= 320) return 2.0f;
            if (dpi <= 480) return 3.0f;
            return 4.0f;
        }

        #endregion
    }
}
