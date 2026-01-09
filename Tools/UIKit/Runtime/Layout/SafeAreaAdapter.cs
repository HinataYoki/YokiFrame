using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 安全区边缘配置
    /// </summary>
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

    /// <summary>
    /// 安全区适配器 - 处理设备安全区（刘海屏、圆角等）
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaAdapter : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("需要适配的边缘")]
        private SafeAreaEdge mEdges = SafeAreaEdge.All;

        [SerializeField]
        [Tooltip("是否在编辑器中模拟安全区")]
        private bool mSimulateInEditor = false;

        [SerializeField]
        [Tooltip("编辑器模拟的安全区边距")]
        private Vector4 mSimulatedInsets = new(50, 50, 100, 50); // left, right, top, bottom

        #endregion

        #region 缓存

        private RectTransform mRectTransform;
        private Rect mLastSafeArea;
        private Vector2Int mLastScreenSize;
        private ScreenOrientation mLastOrientation;

        // 静态缓存，避免每帧重算
        private static Rect sCachedSafeArea;
        private static Vector2Int sCachedScreenSize;
        private static bool sCacheValid;

        #endregion

        #region 属性

        /// <summary>
        /// 需要适配的边缘
        /// </summary>
        public SafeAreaEdge Edges
        {
            get => mEdges;
            set
            {
                if (mEdges != value)
                {
                    mEdges = value;
                    ApplySafeArea();
                }
            }
        }

        /// <summary>
        /// 当前安全区
        /// </summary>
        public Rect CurrentSafeArea => GetSafeArea();

        #endregion

        #region 生命周期

        private void Awake()
        {
            mRectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
            ScreenInfo.OnScreenSizeChanged += OnScreenSizeChanged;
        }

        private void OnDisable()
        {
            ScreenInfo.OnScreenSizeChanged -= OnScreenSizeChanged;
        }

        private void Update()
        {
            // 检测屏幕变化（旋转、分辨率改变等）
            var safeArea = GetSafeArea();
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var orientation = Screen.orientation;

            if (safeArea != mLastSafeArea || screenSize != mLastScreenSize || orientation != mLastOrientation)
            {
                mLastSafeArea = safeArea;
                mLastScreenSize = screenSize;
                mLastOrientation = orientation;
                ApplySafeArea();
            }
        }

        #endregion

        #region 安全区计算

        /// <summary>
        /// 获取安全区
        /// </summary>
        private Rect GetSafeArea()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            
            // 检查缓存
            if (sCacheValid && sCachedScreenSize == screenSize)
            {
                return sCachedSafeArea;
            }

#if UNITY_EDITOR
            if (mSimulateInEditor)
            {
                var simulated = new Rect(
                    mSimulatedInsets.x,
                    mSimulatedInsets.w,
                    Screen.width - mSimulatedInsets.x - mSimulatedInsets.y,
                    Screen.height - mSimulatedInsets.z - mSimulatedInsets.w
                );
                sCachedSafeArea = simulated;
                sCachedScreenSize = screenSize;
                sCacheValid = true;
                return simulated;
            }
#endif

            sCachedSafeArea = Screen.safeArea;
            sCachedScreenSize = screenSize;
            sCacheValid = true;
            return sCachedSafeArea;
        }

        /// <summary>
        /// 应用安全区
        /// </summary>
        private void ApplySafeArea()
        {
            if (mRectTransform == null) return;

            var safeArea = GetSafeArea();
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            // 计算锚点
            var anchorMin = new Vector2(
                (mEdges & SafeAreaEdge.Left) != 0 ? safeArea.x / screenWidth : 0,
                (mEdges & SafeAreaEdge.Bottom) != 0 ? safeArea.y / screenHeight : 0
            );

            var anchorMax = new Vector2(
                (mEdges & SafeAreaEdge.Right) != 0 ? (safeArea.x + safeArea.width) / screenWidth : 1,
                (mEdges & SafeAreaEdge.Top) != 0 ? (safeArea.y + safeArea.height) / screenHeight : 1
            );

            mRectTransform.anchorMin = anchorMin;
            mRectTransform.anchorMax = anchorMax;
            mRectTransform.offsetMin = Vector2.zero;
            mRectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 屏幕尺寸变化回调
        /// </summary>
        private void OnScreenSizeChanged(Vector2Int newSize)
        {
            InvalidateCache();
            ApplySafeArea();
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        public static void InvalidateCache()
        {
            sCacheValid = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 强制刷新安全区
        /// </summary>
        public void Refresh()
        {
            InvalidateCache();
            ApplySafeArea();
        }

        /// <summary>
        /// 获取指定边缘的安全区边距
        /// </summary>
        public float GetInset(SafeAreaEdge edge)
        {
            var safeArea = GetSafeArea();
            return edge switch
            {
                SafeAreaEdge.Left => safeArea.x,
                SafeAreaEdge.Right => Screen.width - safeArea.x - safeArea.width,
                SafeAreaEdge.Top => Screen.height - safeArea.y - safeArea.height,
                SafeAreaEdge.Bottom => safeArea.y,
                _ => 0
            };
        }

        /// <summary>
        /// 获取所有边缘的安全区边距
        /// </summary>
        public Vector4 GetInsets()
        {
            var safeArea = GetSafeArea();
            return new Vector4(
                safeArea.x,                                    // left
                Screen.width - safeArea.x - safeArea.width,    // right
                Screen.height - safeArea.y - safeArea.height,  // top
                safeArea.y                                     // bottom
            );
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (mRectTransform == null)
            {
                mRectTransform = GetComponent<RectTransform>();
            }
            
            if (Application.isPlaying)
            {
                ApplySafeArea();
            }
        }

        private void Reset()
        {
            mEdges = SafeAreaEdge.All;
        }
#endif
    }
}
