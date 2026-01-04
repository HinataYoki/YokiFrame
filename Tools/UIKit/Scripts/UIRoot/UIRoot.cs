using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 根节点 - 管理 UI 层级、Canvas 和面板加载
    /// </summary>
    public class UIRoot : MonoBehaviour, ISingleton
    {
        #region 单例

        private static UIRoot mInstance;
        private static bool mIsInitialized;

        public static UIRoot Instance
        {
            get
            {
                if (mInstance == null)
                {
                    mInstance = FindFirstObjectByType<UIRoot>();
                    if (mInstance == null)
                    {
                        mInstance = CreateFromPrefab();
                    }
                    if (!mIsInitialized && mInstance != null)
                    {
                        mInstance.Initialize();
                        mIsInitialized = true;
                    }
                }
                return mInstance;
            }
        }

        private static UIRoot CreateFromPrefab()
        {
            var prefab = Resources.Load<GameObject>(nameof(UIKit));
            if (prefab == null)
            {
                KitLogger.Error("[UIRoot] UIKit 预制体未找到，请确保 Resources 文件夹中存在 UIKit.prefab");
                return null;
            }
            var uikit = Instantiate(prefab);
            uikit.name = nameof(UIKit);
            DontDestroyOnLoad(uikit);
            return uikit.GetComponentInChildren<UIRoot>();
        }

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
                if (value == null) return;
                mUICamera = value;
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = mUICamera;
            }
        }

        #endregion

        #region UI 层级

        public static Dictionary<UILevel, RectTransform> UILevelDic { get; } = new();

        public void SetLevelOfPanel(UILevel level, IPanel panel)
        {
            if (panel == null) return;
            var hasCanvas = panel.Transform.GetComponent<Canvas>() != null;
            var targetLevel = hasCanvas ? UILevel.CanvasPanel : level;
            panel.Transform.SetParent(UILevelDic[targetLevel]);
            SetupRectTransform(panel.Transform as RectTransform);
        }

        #endregion

        #region 面板加载

        private IPanelLoaderPool mLoaderPool = new DefaultPanelLoaderPool();

        public void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            mLoaderPool = loaderPool;
            KitLogger.Log($"[UIRoot] 加载池已切换为: {loaderPool.GetType().Name}");
        }

        public IPanel LoadPanel(PanelHandler handler)
        {
            var loader = mLoaderPool.AllocateLoader();
            var prefab = loader.Load(handler);
            
            if (prefab == null)
            {
                KitLogger.Error($"[UIRoot] 面板加载失败: {handler.Type.Name}");
                return null;
            }

            // 检查是否是真正的预制体（不在场景中）还是测试用的临时对象
            bool isSceneObject = prefab.scene.IsValid();
            
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            
            // 如果是场景中的临时对象（测试用），销毁它
            if (isSceneObject)
            {
                Destroy(prefab);
            }
            
            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
        }

        public void LoadPanelAsync(PanelHandler handler, Action<IPanel> onComplete)
        {
            var loader = mLoaderPool.AllocateLoader();
            loader.LoadAsync(handler, prefab =>
            {
                if (prefab == null)
                {
                    KitLogger.Error($"[UIRoot] 面板加载失败: {handler.Type.Name}");
                    onComplete?.Invoke(null);
                    return;
                }

                handler.Prefab = prefab;
                handler.Loader = loader;

#if UNITY_2022_3_OR_NEWER
                StartCoroutine(InstantiatePanelAsync(handler, onComplete));
#else
                var panel = Instantiate(prefab).GetComponent<UIPanel>();
                SetupPanelHandler(handler, loader, prefab, panel);
                SetLevelOfPanel(handler.Level, panel);
                onComplete?.Invoke(panel);
#endif
            });
        }

#if UNITY_2022_3_OR_NEWER
        private IEnumerator InstantiatePanelAsync(PanelHandler handler, Action<IPanel> onComplete)
        {
            var op = InstantiateAsync(handler.Prefab);
            yield return op;
            
            if (op.isDone && op.Result.Length > 0)
            {
                var panel = op.Result[0].GetComponent<UIPanel>();
                handler.Panel = panel;
                panel.Handler = handler;
                SetLevelOfPanel(handler.Level, panel);
                onComplete?.Invoke(panel);
            }
        }
#endif

        private static void SetupPanelHandler(PanelHandler handler, IPanelLoader loader, GameObject prefab, UIPanel panel)
        {
            handler.Prefab = prefab;
            handler.Loader = loader;
            handler.Panel = panel;
            panel.Handler = handler;
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            InitializeComponents();
            InitializeUILevels();
            InitializeInputModule();
        }

        private void InitializeComponents()
        {
            var root = transform.root;
            Canvas ??= GetComponent<Canvas>();
            CanvasScaler ??= GetComponent<CanvasScaler>();
            GraphicRaycaster ??= GetComponent<GraphicRaycaster>();
            EventSystem ??= root.GetComponentInChildren<EventSystem>();
        }

        private void InitializeUILevels()
        {
            UILevelDic.Clear();
            foreach (UILevel level in Enum.GetValues(typeof(UILevel)))
            {
                var rect = GetOrCreateLevelNode(level.ToString());
                UILevelDic.Add(level, rect);
                SetupRectTransform(rect);
            }
        }

        private RectTransform GetOrCreateLevelNode(string name)
        {
            var child = transform.Find(name);
            if (child != null) return child as RectTransform;
            
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.transform as RectTransform;
            rect.SetParent(transform);
            return rect;
        }

        private static void SetupRectTransform(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }

        private void InitializeInputModule()
        {
            if (EventSystem == null || EventSystem.GetComponent<BaseInputModule>() != null) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            EventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            EventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        #endregion

        #region 生命周期

        void ISingleton.OnSingletonInit() { }

        private void OnDestroy()
        {
            if (mInstance != this) return;
            mInstance = null;
            mIsInitialized = false;
            UILevelDic.Clear();
        }

        #endregion
    }
}
