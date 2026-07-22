#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// Unity UIKit 的唯一运行时根，负责 Canvas、层级容器和面板 owner 生命周期。
    /// </summary>
    [MonoSingletonPath("UIKit/UIRoot", true)]
    public sealed partial class UIRoot : MonoSingleton<UIRoot>
    {
        internal const string DEFAULT_PREFAB_PATH_PREFIX = "Art/UIPrefab";
        internal const int DEFAULT_REUSABLE_CAPACITY = 8;

        [Header("Panel Loading")]
        [SerializeField, InspectorName("Prefab Path Prefix")]
        private string mPrefabPathPrefix = DEFAULT_PREFAB_PATH_PREFIX;
        [SerializeField, InspectorName("Use Addressable Location")]
        private bool mUseAddressableLocation;
        [Header("Cache")]
        [SerializeField, Min(0), InspectorName("Reusable Cache Capacity")]
        private int mReusableCacheCapacity = DEFAULT_REUSABLE_CAPACITY;
        private UIKitController mController;
        private RectTransform mCanvasRoot;
        private Canvas mCanvas;
        private CanvasScaler mCanvasScaler;
        private RectTransform mStorageRoot;
        private Camera mWorldCamera;
        private int mMainThreadId;

        /// <summary>
        /// 获取 UIKit 使用的 Canvas；读取现有 Root 时不会创建新的 Root。
        /// </summary>
        public Canvas Canvas => mCanvas;

        /// <summary>
        /// 获取显式绑定的 UI Camera；Overlay 模式下为空。
        /// </summary>
        public Camera WorldCamera => mWorldCamera;

        /// <summary>获取或创建当前 UIRoot，并遵循 UIKit 显式 Prefab 选择。</summary>
        public new static UIRoot Instance => UIKit.GetOrCreateRoot();

        /// <summary>
        /// 清除当前 UIRoot 并同步提交销毁意图；Play Mode 的 Unity 实体仍按引擎时序延迟销毁。
        /// </summary>
        public new static void Dispose()
        {
            GameObject legacyOwner = default;
            if (TryGetInstance(out UIRoot root) && root != default)
            {
                Transform owner = root.FindLegacyHierarchyOwner();
                if (owner != default) legacyOwner = owner.gameObject;
                root.DisposeController();
            }
#if UNITY_EDITOR
            bool hadInstance = HasInstance;
#endif
            MonoSingleton<UIRoot>.Dispose();
            DestroyLegacyHierarchyOwner(legacyOwner);
#if UNITY_EDITOR
            if (hadInstance) UIKit.AdvanceDiagnosticVersion();
#endif
        }

        /// <summary>
        /// 获取当前控制器，仅供同程序集的静态门面转发操作。
        /// </summary>
        internal UIKitController Controller => mController;

        /// <summary>
        /// 获取禁用的实例暂存根，预加载和关闭保留面板不会注册到任何 UILevel。
        /// </summary>
        internal RectTransform StorageRoot => mStorageRoot;

        /// <summary>获取默认 ResKit Panel loader 使用的资源路径前缀。</summary>
        internal string PrefabPathPrefix => string.IsNullOrWhiteSpace(mPrefabPathPrefix)
            ? DEFAULT_PREFAB_PATH_PREFIX
            : mPrefabPathPrefix;

        /// <summary>获取默认 Panel loader 是否直接使用类型名作为 addressable location。</summary>
        internal bool UseAddressableLocation => mUseAddressableLocation;

        /// <summary>获取 Prefab 序列化的初始 Reusable LRU 容量。</summary>
        internal int InitialReusableCacheCapacity => Math.Max(0, mReusableCacheCapacity);

        /// <summary>
        /// MonoSingleton 首次登记后构建 Canvas 和运行时控制器。
        /// </summary>
        public override void OnSingletonInit()
        {
            mMainThreadId = Thread.CurrentThread.ManagedThreadId;
            InitializeCanvas();
            EnsureEventSystem();
            ScreenInfo.Initialize();
            mController = new UIKitController(this);
        }

        /// <summary>
        /// 在 Unity 提交延迟销毁后扫描外部单独销毁的 Panel 组件，不在热路径创建临时集合。
        /// </summary>
        private void LateUpdate()
        {
            UIKitController controller = mController;
            if (controller != null) controller.SweepDestroyedEntries();
            TrackExternalFocusChanges();
            ScreenInfo.Update();
        }

        /// <summary>
        /// 显式绑定 Screen Space Camera；不接受 Unity fake-null 引用。
        /// </summary>
        /// <param name="camera">有效的 UI Camera。</param>
        public void BindWorldCamera(Camera camera)
        {
            AssertMainThread();
            if (camera == default) throw new ArgumentNullException(nameof(camera));
            mWorldCamera = camera;
            mCanvas.worldCamera = camera;
            mCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        /// <summary>
        /// Root 销毁时先释放全部 Panel lease，再交给 MonoSingleton 清理静态引用。
        /// </summary>
        protected override void OnDestroy()
        {
            DisposeController();
            if (!TryGetInstance(out UIRoot currentRoot) || ReferenceEquals(currentRoot, this))
                ScreenInfo.Shutdown();
            DisposeFocusState();
#if UNITY_EDITOR
            UIKit.AdvanceDiagnosticVersion();
#endif
            base.OnDestroy();
        }

        /// <summary>
        /// 同步释放当前 controller；销毁回调重入期间保留已释放实例，避免把旧 Root 误判为不存在。
        /// </summary>
        private void DisposeController()
        {
            UIKitController controller = mController;
            if (controller == null) return;
            try
            {
                controller.Dispose();
            }
            finally
            {
                if (ReferenceEquals(mController, controller)) mController = null;
            }
        }

        /// <summary>
        /// 校验 UIKit 操作仍在创建 Root 的 Unity 主线程执行。
        /// </summary>
        internal void AssertMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mMainThreadId)
                throw new InvalidOperationException("UIKit operations must run on the Unity main thread.");
        }

        /// <summary>
        /// 尝试读取现有 Root，不触发 MonoSingleton 自动创建。
        /// </summary>
        internal static bool TryGetExisting(out UIRoot root)
        {
            return TryGetInstance(out root) && root != default && root.mController != null;
        }

        /// <summary>
        /// 创建或复用 Canvas 子节点；已有 Prefab 组件保留其序列化配置。
        /// </summary>
        private void InitializeCanvas()
        {
            mCanvasRoot = FindOrCreateCanvasRoot();
            mStorageRoot = FindOrCreateStorageRoot();
            bool hasCanvas = mCanvasRoot.GetComponent<Canvas>() != default;
            bool hasCanvasScaler = mCanvasRoot.GetComponent<CanvasScaler>() != default;
            mCanvas = GetOrAddComponent<Canvas>(mCanvasRoot.gameObject);
            mCanvasScaler = GetOrAddComponent<CanvasScaler>(mCanvasRoot.gameObject);
            GetOrAddComponent<GraphicRaycaster>(mCanvasRoot.gameObject);
            if (!hasCanvas) ApplyDefaultCanvasOptions();
            if (!hasCanvasScaler) ApplyDefaultCanvasScalerOptions();
            mWorldCamera = mCanvas.worldCamera;
        }

        /// <summary>
        /// 优先复用旧版 UIRoot 节点自身的 Canvas，否则读取或创建当前 Canvas 子节点。
        /// </summary>
        private RectTransform FindOrCreateCanvasRoot()
        {
            Canvas rootCanvas = GetComponent<Canvas>();
            if (rootCanvas != default && transform is RectTransform rootRect) return rootRect;
            Transform existing = transform.Find("Canvas");
            if (existing != default && existing is RectTransform existingRect) return existingRect;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.SetParent(transform, false);
            return canvasRect;
        }

        /// <summary>
        /// 创建禁用的面板暂存根，使 Prefab 实例化阶段不会短暂触发可见生命周期。
        /// </summary>
        private RectTransform FindOrCreateStorageRoot()
        {
            Transform existing = transform.Find("Storage");
            if (existing != default && existing is RectTransform existingRect)
            {
                existingRect.gameObject.SetActive(false);
                return existingRect;
            }

            var storageObject = new GameObject("Storage", typeof(RectTransform));
            var storageRect = storageObject.GetComponent<RectTransform>();
            storageRect.SetParent(transform, false);
            storageObject.SetActive(false);
            return storageRect;
        }

        /// <summary>
        /// 只为缺少序列化 Canvas 的动态兜底 Root 应用稳定默认值。
        /// </summary>
        private void ApplyDefaultCanvasOptions()
        {
            mCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mCanvas.sortingOrder = 0;
        }

        /// <summary>只为缺少序列化 CanvasScaler 的动态兜底 Root 应用缩放默认值。</summary>
        private void ApplyDefaultCanvasScalerOptions()
        {
            mCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            mCanvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            mCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            mCanvasScaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>实例化包含 UIRoot 的模板 Prefab，恢复资产原名并登记其中唯一的 Root 组件。</summary>
        /// <param name="prefab">包内模板或项目 Prefab Variant。</param>
        /// <returns>实例化并完成初始化的 UIRoot。</returns>
        internal static UIRoot CreateFromPrefab(GameObject prefab)
        {
            if (prefab == default) throw new ArgumentNullException(nameof(prefab));
            GameObject owner = UnityEngine.Object.Instantiate(prefab);
            owner.name = prefab.name;
            UIRoot[] roots = owner.GetComponentsInChildren<UIRoot>(true);
            if (roots.Length != 1)
            {
                DestroyPrefabOwner(owner);
                throw new InvalidOperationException("UIKit root prefab must contain exactly one UIRoot component.");
            }

            UIRoot root = roots[0];
            RegisterInstance(root);
            return root;
        }

        /// <summary>包内模板缺失时调用通用 MonoSingleton 路径创建最小可用 Root。</summary>
        internal static UIRoot CreateProceduralFallback()
        {
            return MonoSingleton<UIRoot>.Instance;
        }

        /// <summary>销毁无法提供 UIRoot 的无效 Prefab 实例。</summary>
        /// <param name="owner">刚创建且尚未登记的 Prefab 根对象。</param>
        private static void DestroyPrefabOwner(GameObject owner)
        {
            if (owner == default) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(owner);
                return;
            }
#endif
            UnityEngine.Object.Destroy(owner);
        }

        /// <summary>
        /// 识别旧版 `UIKit/UIRoot` 结构的 Prefab owner；普通场景父节点不参与 Root 级联销毁。
        /// </summary>
        private Transform FindLegacyHierarchyOwner()
        {
            Transform owner = transform.parent;
            if (owner == default) return null;
            if (owner.GetComponent<UIRoot>() != default) return null;
            UIRoot nestedRoot = owner.GetComponentInChildren<UIRoot>(true);
            return nestedRoot == this ? owner : null;
        }

        /// <summary>
        /// 销毁旧版 Prefab owner，避免只销毁 UIRoot 后遗留 EventSystem 与 UICamera。
        /// </summary>
        /// <param name="owner">调用基类 Dispose 前捕获的旧版 UIKit 根对象。</param>
        private static void DestroyLegacyHierarchyOwner(GameObject owner)
        {
            if (owner == default) return;
            owner.SetActive(false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(owner);
                return;
            }
#endif
            UnityEngine.Object.Destroy(owner);
        }

        /// <summary>
        /// 获取已有组件或在同一 GameObject 上补齐组件。
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != default ? component : target.AddComponent<T>();
        }
    }
}
#endif
