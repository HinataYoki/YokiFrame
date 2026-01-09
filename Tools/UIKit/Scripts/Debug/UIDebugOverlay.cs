using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 运行时调试覆盖层 - 显示面板数、栈深度、当前焦点等信息
    /// </summary>
    public partial class UIDebugOverlay : MonoBehaviour
    {
        #region 单例

        private static UIDebugOverlay sInstance;
        
        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static UIDebugOverlay Instance
        {
            get
            {
                if (sInstance == null)
                {
                    var go = new GameObject("[UIDebugOverlay]");
                    sInstance = go.AddComponent<UIDebugOverlay>();
                    DontDestroyOnLoad(go);
                }
                return sInstance;
            }
        }

        #endregion

        #region 配置

        /// <summary>
        /// 切换显示的热键
        /// </summary>
        [SerializeField]
        private KeyCode mToggleKey = KeyCode.F12;
        
        /// <summary>
        /// 是否需要按住 Shift
        /// </summary>
        [SerializeField]
        private bool mRequireShift = true;
        
        /// <summary>
        /// 显示位置
        /// </summary>
        [SerializeField]
        private OverlayPosition mPosition = OverlayPosition.TopLeft;
        
        /// <summary>
        /// 背景透明度
        /// </summary>
        [SerializeField]
        [Range(0f, 1f)]
        private float mBackgroundAlpha = 0.8f;
        
        /// <summary>
        /// 字体大小
        /// </summary>
        [SerializeField]
        private int mFontSize = 14;

        #endregion

        #region 字段

        private bool mIsVisible;
        private GUIStyle mBoxStyle;
        private GUIStyle mLabelStyle;
        private GUIStyle mHeaderStyle;
        private Rect mWindowRect;
        private bool mStylesInitialized;
        
        // 缓存数据（避免每帧查询）
        private int mCachedPanelCount;
        private int mCachedStackDepth;
        private string mCachedFocusName;
        private string mCachedInputMode;
        private int mCachedCacheCount;
        private float mLastUpdateTime;
        private const float UPDATE_INTERVAL = 0.2f;

        #endregion

        #region 枚举

        /// <summary>
        /// 覆盖层位置
        /// </summary>
        public enum OverlayPosition
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsVisible
        {
            get => mIsVisible;
            set => mIsVisible = value;
        }

        /// <summary>
        /// 切换热键
        /// </summary>
        public KeyCode ToggleKey
        {
            get => mToggleKey;
            set => mToggleKey = value;
        }

        /// <summary>
        /// 是否需要 Shift 键
        /// </summary>
        public bool RequireShift
        {
            get => mRequireShift;
            set => mRequireShift = value;
        }

        /// <summary>
        /// 显示位置
        /// </summary>
        public OverlayPosition Position
        {
            get => mPosition;
            set => mPosition = value;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示覆盖层
        /// </summary>
        public static void Show()
        {
            Instance.mIsVisible = true;
        }

        /// <summary>
        /// 隐藏覆盖层
        /// </summary>
        public static void Hide()
        {
            if (sInstance != null)
            {
                sInstance.mIsVisible = false;
            }
        }

        /// <summary>
        /// 切换显示状态
        /// </summary>
        public static void Toggle()
        {
            Instance.mIsVisible = !Instance.mIsVisible;
        }

        /// <summary>
        /// 配置覆盖层
        /// </summary>
        public static void Configure(KeyCode toggleKey, bool requireShift = true, OverlayPosition position = OverlayPosition.TopLeft)
        {
            var instance = Instance;
            instance.mToggleKey = toggleKey;
            instance.mRequireShift = requireShift;
            instance.mPosition = position;
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            // 检测热键
            if (Input.GetKeyDown(mToggleKey))
            {
                if (!mRequireShift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    mIsVisible = !mIsVisible;
                }
            }
            
            // 更新缓存数据
            if (mIsVisible && Time.unscaledTime - mLastUpdateTime > UPDATE_INTERVAL)
            {
                UpdateCachedData();
                mLastUpdateTime = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!mIsVisible) return;
            
            InitializeStyles();
            
            // 计算窗口位置
            var windowWidth = 220f;
            var windowHeight = 160f;
            var padding = 10f;
            
            var x = mPosition switch
            {
                OverlayPosition.TopLeft => padding,
                OverlayPosition.BottomLeft => padding,
                OverlayPosition.TopRight => Screen.width - windowWidth - padding,
                OverlayPosition.BottomRight => Screen.width - windowWidth - padding,
                _ => padding
            };
            
            var y = mPosition switch
            {
                OverlayPosition.TopLeft => padding,
                OverlayPosition.TopRight => padding,
                OverlayPosition.BottomLeft => Screen.height - windowHeight - padding,
                OverlayPosition.BottomRight => Screen.height - windowHeight - padding,
                _ => padding
            };
            
            mWindowRect = new Rect(x, y, windowWidth, windowHeight);
            
            // 绘制窗口
            GUI.Box(mWindowRect, "", mBoxStyle);
            
            GUILayout.BeginArea(new Rect(mWindowRect.x + 10, mWindowRect.y + 5, mWindowRect.width - 20, mWindowRect.height - 10));
            {
                DrawOverlayContent();
            }
            GUILayout.EndArea();
        }

        #endregion

    }
}
