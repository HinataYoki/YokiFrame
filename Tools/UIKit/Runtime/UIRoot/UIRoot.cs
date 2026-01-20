using System;
using System.Collections;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
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
        private static bool sIsInitialized;

        public static UIRoot Instance
        {
            get
            {
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

        #endregion

        #region 面板加载器

#if YOKIFRAME_UNITASK_SUPPORT
        private IPanelLoaderPool mLoaderPool = new DefaultPanelLoaderUniTaskPool();
#else
        private IPanelLoaderPool mLoaderPool = new DefaultPanelLoaderPool();
#endif

        public void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            mLoaderPool = loaderPool;
            KitLogger.Log($"[UIRoot] 加载池已切换为: {loaderPool.GetType().Name}");
        }

        #endregion

        #region 面板加载

        public IPanel LoadPanel(PanelHandler handler)
        {
            var loader = mLoaderPool.AllocateLoader();
            var prefab = loader.Load(handler);

            if (prefab == default)
            {
                KitLogger.Error($"[UIRoot] 面板加载失败: {handler.Type.Name}");
                return null;
            }

            bool isSceneObject = prefab.scene.IsValid();
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            if (isSceneObject) Destroy(prefab);

            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
        }

        public void LoadPanelAsync(PanelHandler handler, Action<IPanel> onComplete)
        {
            var loader = mLoaderPool.AllocateLoader();
            loader.LoadAsync(handler, prefab =>
            {
                if (prefab == default)
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

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<IPanel> LoadPanelUniTaskAsync(PanelHandler handler, CancellationToken ct = default)
        {
            var loader = mLoaderPool.AllocateLoader();
            var tcs = new UniTaskCompletionSource<GameObject>();
            loader.LoadAsync(handler, prefab => tcs.TrySetResult(prefab));

            var prefab = await tcs.Task.AttachExternalCancellation(ct);
            if (prefab == default)
            {
                KitLogger.Error($"[UIRoot] 面板加载失败: {handler.Type.Name}");
                return null;
            }

            handler.Prefab = prefab;
            handler.Loader = loader;

#if UNITY_2022_3_OR_NEWER
            var op = InstantiateAsync(prefab);
            await op.ToUniTask(cancellationToken: ct);
            if (op.isDone && op.Result.Length > 0)
            {
                var panel = op.Result[0].GetComponent<UIPanel>();
                handler.Panel = panel;
                panel.Handler = handler;
                SetLevelOfPanel(handler.Level, panel);
                return panel;
            }
            return null;
#else
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
#endif
        }
#endif

        private static void SetupPanelHandler(PanelHandler handler, IPanelLoader loader,
            GameObject prefab, UIPanel panel)
        {
            handler.Prefab = prefab;
            handler.Loader = loader;
            handler.Panel = panel;
            panel.Handler = handler;
        }

        #endregion

        #region 面板操作（供 UIKit 调用）

        internal IPanel OpenPanelInternal(Type type, UILevel level, IUIData data)
        {
            WeakenAllHot();

            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                OpenAndShowPanelInternal(handler.Panel, data);
                return handler.Panel;
            }

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;

            var panel = LoadPanel(handler);
            if (panel != default && panel.Transform != default)
            {
                SetupPanelInternal(handler, panel);
                OpenAndShowPanelInternal(panel, data);
                return panel;
            }

            handler.Recycle();
            return null;
        }

        internal void OpenPanelAsyncInternal(Type type, UILevel level, IUIData data, Action<IPanel> callback)
        {
            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                OpenAndShowPanelInternal(handler.Panel, data);
                callback?.Invoke(handler.Panel);
                return;
            }

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;

            LoadPanelAsync(handler, panel =>
            {
                if (panel != default && panel.Transform != default)
                {
                    SetupPanelInternal(handler, panel);
                    OpenAndShowPanelInternal(panel, data);
                    callback?.Invoke(panel);
                }
                else
                {
                    handler.Recycle();
                    callback?.Invoke(null);
                }
            });
        }

        private void SetupPanelInternal(PanelHandler handler, IPanel panel)
        {
            panel.Transform.gameObject.name = handler.Type.Name;
            AddToOpenedCache(handler.Type, handler);
            handler.Hot += OpenHot;
            panel.Init(handler.Data);
            RegisterPanelToLevel(panel);
        }

        private void OpenAndShowPanelInternal(IPanel panel, IUIData data)
        {
            if (panel == default) return;
            panel.Open(data);
            panel.Show();
        }

        internal void ClosePanelInternal(IPanel panel)
        {
            if (panel == default) return;

            var unityObj = panel as UnityEngine.Object;
            if (unityObj == default)
            {
                if (panel.Handler != default)
                {
                    RemoveFromStack(panel);
                    UnregisterPanelFromLevel(panel);
                    RemoveFromOpenedCache(panel.Handler.Type);
                    panel.Handler.Recycle();
                }
                return;
            }

            panel.Close();
            if (panel.Handler == default) return;

            RemoveFromStack(panel);
            UnregisterPanelFromLevel(panel);
            OnPanelCloseFocus(panel);

            // 根据 CacheMode 决策是否销毁
            if (ShouldDestroyOnClose(panel.Handler))
            {
                DestroyPanelInternal(panel);
                RemoveFromOpenedCache(panel.Handler.Type);
                panel.Handler.Recycle();
            }
        }

        internal void DestroyPanelInternal(IPanel panel)
        {
            if (panel != default && panel.Transform != default && panel.Transform.gameObject != default)
            {
                panel.Cleanup();
                Destroy(panel.Transform.gameObject);
            }
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            InitializeComponents();
            InitializeUILevels();
            InitializeInputModule();
            InitializeLevelPanels();
            InitializeFocusSystem();
        }

        private void InitializeComponents()
        {
            var root = transform.root;
            if (Canvas == default) Canvas = GetComponent<Canvas>();
            if (CanvasScaler == default) CanvasScaler = GetComponent<CanvasScaler>();
            if (GraphicRaycaster == default) GraphicRaycaster = GetComponent<GraphicRaycaster>();
            if (EventSystem == default) EventSystem = root.GetComponentInChildren<EventSystem>();
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
            if (child != default) return child as RectTransform;

            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.transform as RectTransform;
            rect.SetParent(transform);
            return rect;
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

        private void InitializeInputModule()
        {
            if (EventSystem == default) return;
            if (EventSystem.GetComponent<BaseInputModule>() != default) return;

#if YOKIFRAME_INPUTSYSTEM_SUPPORT && !ENABLE_LEGACY_INPUT_MANAGER
            EventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            EventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
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
