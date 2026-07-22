#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>声明需要应用的安全区边缘。</summary>
    [System.Flags]
    public enum SafeAreaEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom,
        Horizontal = Left | Right,
        Vertical = Top | Bottom
    }

    /// <summary>把 RectTransform 锚点适配到设备安全区，并响应屏幕变化。</summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Safe Area Adapter")]
    public sealed class SafeAreaAdapter : MonoBehaviour
    {
        [SerializeField] private SafeAreaEdge mEdges = SafeAreaEdge.All;
#if UNITY_EDITOR
        [SerializeField] private bool mSimulateInEditor;
        [SerializeField] private Vector4 mSimulatedInsets = new(50f, 50f, 100f, 50f);
#endif
        private RectTransform mRectTransform;
        private Rect mLastFallbackSafeArea;
        private Vector2Int mLastFallbackScreenSize;
        private ScreenOrientation mLastFallbackOrientation;
        private bool mHasFallbackSnapshot;

        /// <summary>获取或设置需要适配的边缘。</summary>
        public SafeAreaEdge Edges
        {
            get { return mEdges; }
            set
            {
                if (mEdges == value) return;
                mEdges = value;
                ApplySafeArea();
            }
        }

        /// <summary>读取当前设备安全区。</summary>
        public Rect CurrentSafeArea => GetSafeArea();

        /// <summary>请求 Root 在下一帧重新采样安全区，保留旧公开刷新入口。</summary>
        public static void InvalidateCache()
        {
            ScreenInfo.InvalidateSafeAreaCache();
        }

        /// <summary>刷新当前组件的安全区锚点。</summary>
        public void Refresh()
        {
            ApplySafeArea();
        }

        /// <summary>获取单侧安全区像素边距。</summary>
        public float GetInset(SafeAreaEdge edge)
        {
            Rect safeArea = GetSafeArea();
            switch (edge)
            {
                case SafeAreaEdge.Left: return safeArea.x;
                case SafeAreaEdge.Right: return Screen.width - safeArea.x - safeArea.width;
                case SafeAreaEdge.Top: return Screen.height - safeArea.y - safeArea.height;
                case SafeAreaEdge.Bottom: return safeArea.y;
                default: return 0f;
            }
        }

        /// <summary>按 left、right、top、bottom 顺序获取安全区边距。</summary>
        public Vector4 GetInsets()
        {
            Rect safeArea = GetSafeArea();
            return new Vector4(
                safeArea.x,
                Screen.width - safeArea.x - safeArea.width,
                Screen.height - safeArea.y - safeArea.height,
                safeArea.y);
        }

        /// <summary>初始化 RectTransform 并订阅屏幕变化。</summary>
        private void Awake()
        {
            mRectTransform = GetComponent<RectTransform>();
        }

        /// <summary>启用时立即应用安全区并订阅屏幕尺寸通知。</summary>
        private void OnEnable()
        {
            ApplySafeArea();
            ScreenInfo.OnSafeAreaChanged += OnSafeAreaChanged;
        }

        /// <summary>禁用时取消屏幕变化订阅。</summary>
        private void OnDisable()
        {
            ScreenInfo.OnSafeAreaChanged -= OnSafeAreaChanged;
        }

        /// <summary>Root 未创建时独立轮询安全区；正常 UIKit 会话只由 Root 统一采样。</summary>
        private void Update()
        {
            if (ScreenInfo.IsInitialized) return;
            Rect safeArea = GetSafeArea();
            Vector2Int size = new(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;
            if (mHasFallbackSnapshot
                && safeArea == mLastFallbackSafeArea
                && size == mLastFallbackScreenSize
                && orientation == mLastFallbackOrientation) return;
            mLastFallbackSafeArea = safeArea;
            mLastFallbackScreenSize = size;
            mLastFallbackOrientation = orientation;
            mHasFallbackSnapshot = true;
            ApplySafeArea(safeArea);
        }

        /// <summary>优先读取当前组件的 Editor 模拟值，否则复用 Root 本帧采样的安全区。</summary>
        private Rect GetSafeArea()
        {
#if UNITY_EDITOR
            if (mSimulateInEditor)
            {
                return new Rect(
                    mSimulatedInsets.x,
                    mSimulatedInsets.w,
                    Screen.width - mSimulatedInsets.x - mSimulatedInsets.y,
                    Screen.height - mSimulatedInsets.z - mSimulatedInsets.w);
            }
#endif
            return ScreenInfo.TryGetCachedSafeArea(out Rect safeArea)
                ? safeArea
                : Screen.safeArea;
        }

        /// <summary>按边缘开关计算锚点并清零偏移。</summary>
        private void ApplySafeArea()
        {
            ApplySafeArea(GetSafeArea());
        }

        /// <summary>把已采样安全区写入 RectTransform，避免重复读取 Unity 屏幕状态。</summary>
        private void ApplySafeArea(Rect safeArea)
        {
            if (mRectTransform == null) mRectTransform = GetComponent<RectTransform>();
            if (mRectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;
            mRectTransform.anchorMin = new Vector2(
                (mEdges & SafeAreaEdge.Left) != 0 ? safeArea.x / Screen.width : 0f,
                (mEdges & SafeAreaEdge.Bottom) != 0 ? safeArea.y / Screen.height : 0f);
            mRectTransform.anchorMax = new Vector2(
                (mEdges & SafeAreaEdge.Right) != 0 ? (safeArea.x + safeArea.width) / Screen.width : 1f,
                (mEdges & SafeAreaEdge.Top) != 0 ? (safeArea.y + safeArea.height) / Screen.height : 1f);
            mRectTransform.offsetMin = Vector2.zero;
            mRectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>收到 Root 的统一安全区布局更新后刷新当前锚点。</summary>
        private void OnSafeAreaChanged()
        {
            ApplySafeArea();
        }
    }
}
#endif
