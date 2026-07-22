#if GODOT
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// Godot Node 单例基类；推荐作为 Autoload 或场景根节点使用。
    /// </summary>
    /// <typeparam name="T">具体 Godot Node 单例类型。</typeparam>
    public abstract partial class GodotSingleton<T> : Node, ISingleton where T : GodotSingleton<T>, new()
    {
        private static T sInstance;
        private bool mSingletonInitialized;

        /// <summary>
        /// 获取单例实例；不存在时创建节点，并尽量挂载到当前 SceneTree 根节点。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (sInstance != null)
                {
                    return sInstance;
                }

                T instance = new();
                instance.Name = typeof(T).Name;
                AttachToSceneTreeRoot(instance);
                RegisterInstance(instance);
                return sInstance;
            }
        }

        /// <summary>
        /// 获取当前是否已经存在可用实例。
        /// </summary>
        public static bool HasInstance
        {
            get { return sInstance != null; }
        }

        /// <summary>
        /// 尝试获取当前实例，不触发自动创建。
        /// </summary>
        /// <param name="instance">当前实例；尚未创建时为 null。</param>
        /// <returns>存在实例时返回 true。</returns>
        public static bool TryGetInstance(out T instance)
        {
            instance = sInstance;
            return instance != null;
        }

        /// <summary>
        /// 清除单例引用，并释放当前 Godot 节点。
        /// </summary>
        public new static void Dispose()
        {
            if (sInstance == null)
            {
                return;
            }

            T current = sInstance;
            sInstance = null;
#if GODOT && TOOLS
            SingletonRegistry.Unregister(typeof(T));
#endif
            current.QueueFree();
        }

        /// <summary>
        /// Godot 进入场景树回调，负责登记 Autoload 或场景中手动创建的实例。
        /// </summary>
        public override void _EnterTree()
        {
            RegisterInstance(this as T);
        }

        /// <summary>
        /// Godot 离开场景树回调，负责清理静态引用和诊断状态。
        /// </summary>
        public override void _ExitTree()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
#if GODOT && TOOLS
                SingletonRegistry.Unregister(typeof(T));
#endif
            }
        }

        /// <summary>
        /// 单例初始化完成后调用；子类可重写完成自身初始化。
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }

        /// <summary>
        /// 尝试把自动创建的节点挂到当前 SceneTree 根节点。
        /// </summary>
        /// <param name="instance">待挂载节点。</param>
        private static void AttachToSceneTreeRoot(T instance)
        {
            if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
            {
                tree.Root.AddChild(instance);
            }
        }

        /// <summary>
        /// 登记实例并处理重复节点。
        /// </summary>
        /// <param name="instance">待登记实例。</param>
        private static void RegisterInstance(T instance)
        {
            if (instance == null)
            {
                return;
            }

            if (sInstance != null && !ReferenceEquals(sInstance, instance))
            {
                instance.QueueFree();
                return;
            }

            sInstance = instance;
            sInstance.EnsureSingletonInitialized();
#if GODOT && TOOLS
            SingletonRegistry.Register(typeof(T), sInstance, "Godot", "GodotSingleton");
#endif
        }

        /// <summary>
        /// 确保 OnSingletonInit 对同一节点只调用一次。
        /// </summary>
        private void EnsureSingletonInitialized()
        {
            if (mSingletonInitialized)
            {
                return;
            }

            mSingletonInitialized = true;
            OnSingletonInit();
        }
    }
}
#endif
