using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 根节点 - 统一管理 UI 系统
    /// </summary>
    public partial class UIRoot : MonoBehaviour, ISingleton
    {
        #region 单例

        private static UIRoot sInstance;
        internal static UIRoot sInstanceInternal => sInstance;
        private static bool sIsInitialized;
        private static bool sIsQuitting; // 防止退出时重新创建

        public static UIRoot Instance
        {
            get
            {
                if (sIsQuitting) return null; // 退出时不再创建
                
                if (sInstance == default)
                {
                    sInstance = FindFirstObjectByType<UIRoot>();
                    if (sInstance == default)
                    {
                        sInstance = CreateFromPrefab();
                    }
                    if (!sIsInitialized && sInstance != default)
                    {
                        sInstance.Initialize();
                        sIsInitialized = true;
                    }
                }
                return sInstance;
            }
        }

        private static UIRoot CreateFromPrefab()
        {
            var prefab = Resources.Load<GameObject>(nameof(UIKit));
            if (prefab == default)
            {
                KitLogger.Error("[UIRoot] UIKit 预制体未找到");
                return null;
            }
            var uikit = Instantiate(prefab);
            uikit.name = nameof(UIKit);
            DontDestroyOnLoad(uikit);
            return uikit.GetComponentInChildren<UIRoot>();
        }

        #endregion

        #region 配置

        [SerializeField] private UIRootConfig mConfig = new();

        /// <summary>
        /// UIKit Canvas 配置（从 UIKitSettings 读取）
        /// </summary>
        private UIKitSettings Config => UIKitSettings.Instance;

        #endregion

        #region 组件引用

        public Canvas Canvas;
        public CanvasScaler CanvasScaler;
        public GraphicRaycaster GraphicRaycaster;
        public EventSystem EventSystem;

        private Camera mUICamera;
        public Camera UICamera
        {
            get => mUICamera;
            set
            {
                if (value == default) return;
                mUICamera = value;
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = mUICamera;
            }
        }

        #endregion

        #region UI 层级节点

        public static System.Collections.Generic.Dictionary<UILevel, RectTransform> UILevelDic { get; } = new();

        public void SetLevelOfPanel(UILevel level, IPanel panel)
        {
            if (panel == default) return;
            var hasCanvas = panel.Transform.GetComponent<Canvas>() != default;
            var targetLevel = hasCanvas ? UILevel.CanvasPanel : level;
            panel.Transform.SetParent(UILevelDic[targetLevel]);
            SetupRectTransform(panel.Transform as RectTransform);
        }

        private static void SetupRectTransform(RectTransform rect)
        {
            if (rect == default) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }

        #endregion

        #region 生命周期

        void ISingleton.OnSingletonInit() { }

        private void Update()
        {
            UpdateFocusSystem();
        }

        private void LateUpdate()
        {
            LateUpdateFocusSystem();
        }

        private void OnDestroy()
        {
            if (sInstance != this) return;
            
            // 标记正在退出，防止异步任务触发单例重新创建
            sIsQuitting = true;
            
            sInstance = null;
            sIsInitialized = false;
            UILevelDic.Clear();
            ClearAllStacks();
            ClearAllLevels();
            DisposeFocusSystem();
            
            // 清空组件引用，防止 DontDestroyOnLoad 对象残留
            Canvas = null;
            CanvasScaler = null;
            GraphicRaycaster = null;
            EventSystem = null;
            mUICamera = null;
        }

        #endregion
    }
}
