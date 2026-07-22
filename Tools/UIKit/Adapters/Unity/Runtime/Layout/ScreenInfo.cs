#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>UIKit 使用的屏幕方向分类。</summary>
    public enum ScreenAspect
    {
        Portrait,
        Landscape,
        Square
    }

    /// <summary>
    /// 提供 UIKit 使用的屏幕尺寸、安全区和方向快照，并在尺寸变化时发出通知。
    /// </summary>
    public static class ScreenInfo
    {
        private const float DEFAULT_DPI = 96f;
        private const float INCH_TO_CM = 2.54f;
        private static Vector2Int sLastSize;
        private static ScreenAspect sLastAspect;
        private static Rect sLastSafeArea;
        private static bool sSafeAreaCacheValid;
        private static bool sInitialized;

        /// <summary>屏幕尺寸变化事件。</summary>
        public static event Action<Vector2Int> OnScreenSizeChanged;

        /// <summary>屏幕方向变化事件。</summary>
        public static event Action<ScreenAspect> OnAspectChanged;

        /// <summary>安全区锚点需要重新计算时触发；只由 UIRoot 每帧采样一次。</summary>
        internal static event Action OnSafeAreaChanged;

        /// <summary>当前屏幕宽度。</summary>
        public static int Width => Screen.width;

        /// <summary>当前屏幕高度。</summary>
        public static int Height => Screen.height;

        /// <summary>当前屏幕尺寸。</summary>
        public static Vector2Int Size => new(Screen.width, Screen.height);

        /// <summary>当前屏幕 DPI；设备未提供时回落到 96 DPI。</summary>
        public static float Dpi => Screen.dpi > 0f ? Screen.dpi : DEFAULT_DPI;

        /// <summary>当前屏幕宽高比。</summary>
        public static float AspectRatio => Height > 0 ? (float)Width / Height : 1f;

        /// <summary>当前屏幕方向。</summary>
        public static ScreenAspect Aspect
        {
            get
            {
                float ratio = AspectRatio;
                if (ratio > 1.1f) return ScreenAspect.Landscape;
                if (ratio < 0.9f) return ScreenAspect.Portrait;
                return ScreenAspect.Square;
            }
        }

        /// <summary>当前设备安全区。</summary>
        public static Rect SafeArea => Screen.safeArea;

        /// <summary>判断 UIKit Root 是否已建立统一的屏幕状态采样。</summary>
        internal static bool IsInitialized => sInitialized;

        /// <summary>按 left、right、top、bottom 顺序返回安全区边距。</summary>
        public static Vector4 SafeAreaInsets
        {
            get
            {
                Rect safeArea = Screen.safeArea;
                return new Vector4(
                    safeArea.x,
                    Width - safeArea.x - safeArea.width,
                    Height - safeArea.y - safeArea.height,
                    safeArea.y);
            }
        }

        /// <summary>把像素转换为密度无关像素。</summary>
        public static float PixelsToDp(float pixels) => pixels * DEFAULT_DPI / Dpi;

        /// <summary>把密度无关像素转换为像素。</summary>
        public static float DpToPixels(float dp) => dp * Dpi / DEFAULT_DPI;

        /// <summary>把像素转换为英寸。</summary>
        public static float PixelsToInches(float pixels) => pixels / Dpi;

        /// <summary>把英寸转换为像素。</summary>
        public static float InchesToPixels(float inches) => inches * Dpi;

        /// <summary>把像素转换为厘米。</summary>
        public static float PixelsToCm(float pixels) => PixelsToInches(pixels) * INCH_TO_CM;

        /// <summary>把厘米转换为像素。</summary>
        public static float CmToPixels(float centimeters) => InchesToPixels(centimeters / INCH_TO_CM);

        /// <summary>由 UIRoot 首次创建时初始化屏幕变化基线。</summary>
        internal static void Initialize()
        {
            sLastSize = Size;
            sLastAspect = Aspect;
            sLastSafeArea = Screen.safeArea;
            sSafeAreaCacheValid = true;
            sInitialized = true;
        }

        /// <summary>由 UIRoot 每帧检查分辨率和方向变化。</summary>
        internal static void Update()
        {
            Vector2Int size = Size;
            ScreenAspect aspect = Aspect;
            Rect safeArea = Screen.safeArea;
            if (!sInitialized)
            {
                Initialize();
                return;
            }

            bool sizeChanged = size != sLastSize;
            if (sizeChanged)
            {
                sLastSize = size;
                if (OnScreenSizeChanged != null) OnScreenSizeChanged(size);
            }

            if (aspect != sLastAspect)
            {
                sLastAspect = aspect;
                if (OnAspectChanged != null) OnAspectChanged(aspect);
            }

            bool safeAreaChanged = !sSafeAreaCacheValid || safeArea != sLastSafeArea;
            sLastSafeArea = safeArea;
            sSafeAreaCacheValid = true;
            if ((sizeChanged || safeAreaChanged) && OnSafeAreaChanged != null)
                OnSafeAreaChanged();
        }

        /// <summary>读取 Root 本帧采样的安全区；Root 不存在或缓存失效时返回 false。</summary>
        internal static bool TryGetCachedSafeArea(out Rect safeArea)
        {
            safeArea = sLastSafeArea;
            return sInitialized && sSafeAreaCacheValid;
        }

        /// <summary>使下一次 Root 采样强制重新发布安全区布局变化。</summary>
        internal static void InvalidateSafeAreaCache()
        {
            sSafeAreaCacheValid = false;
        }

        /// <summary>Root 销毁后停止复用旧会话采样，独立组件回退为自身轮询。</summary>
        internal static void Shutdown()
        {
            sInitialized = false;
            sSafeAreaCacheValid = false;
        }
    }
}
#endif
