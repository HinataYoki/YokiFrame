#if GODOT
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot Node 单例基类。推荐作为 Autoload 或场景根节点使用；缺失时会尝试挂到当前 SceneTree.Root。
    /// </summary>
    public abstract partial class GodotSingleton<T> : Node, ISingleton where T : GodotSingleton<T>, new()
    {
        private static T sInstance;
        public static T Instance
        {
            get
            {
                if (sInstance != null)
                    return sInstance;

                var instance = new T();
                instance.Name = typeof(T).Name;

                if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
                    tree.Root.AddChild(instance);

                RegisterInstance(instance);
                return sInstance;
            }
        }

        public static void Dispose()
        {
            if (sInstance == null)
                return;

            var current = sInstance;
            sInstance = null;
            SingletonRegistry.Unregister(typeof(T));
            current.QueueFree();
        }

        public override void _EnterTree()
        {
            RegisterInstance(this as T);
        }

        public override void _ExitTree()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
                SingletonRegistry.Unregister(typeof(T));
            }
        }

        public virtual void OnSingletonInit()
        {
        }

        private static void RegisterInstance(T instance)
        {
            if (instance == null)
                return;

            if (sInstance != null && !ReferenceEquals(sInstance, instance))
            {
                instance.QueueFree();
                return;
            }

            sInstance = instance;
            sInstance.OnSingletonInit();
            SingletonRegistry.Register(typeof(T), sInstance, "Godot", "GodotSingleton");
        }
    }
}
#endif
