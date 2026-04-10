using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Central singleton manager responsible for instance creation, caching, and lifecycle control.
    /// </summary>
    /// <remarks>
    /// This class supports both plain C# singletons and <see cref="MonoBehaviour"/>-based singletons through one
    /// shared access path. Plain C# singletons are created with reflection, while Mono singletons are located or
    /// created in the scene hierarchy and marked as <c>DontDestroyOnLoad</c> when needed.
    /// </remarks>
    public static class SingletonKit<T> where T : class, ISingleton
    {
        #region Instance State

        /// <summary>
        /// Cached singleton instance.
        /// </summary>
        private static T mInstance;

        /// <summary>
        /// Whether the application is quitting.
        /// </summary>
        /// <remarks>
        /// This prevents <see cref="MonoBehaviour"/> singletons from being recreated during teardown.
        /// </remarks>
        private static bool mIsQuitting;

        /// <summary>
        /// Synchronization lock used during lazy initialization.
        /// </summary>
        private static readonly object mLock = new();

        /// <summary>
        /// Cached creation strategy selected by the static constructor.
        /// </summary>
        private static readonly Func<T> mCreator;

        /// <summary>
        /// Whether <typeparamref name="T"/> derives from <see cref="MonoBehaviour"/>.
        /// </summary>
        private static readonly bool mIsMonoBehaviour;

        /// <summary>
        /// Cached hierarchy-path attribute for Mono singletons.
        /// </summary>
        private static readonly MonoSingletonPathAttribute mCachedPathAttribute;

        /// <summary>
        /// Initializes the singleton creation strategy.
        /// </summary>
        static SingletonKit()
        {
            var type = typeof(T);
            mIsMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(type);

            if (mIsMonoBehaviour)
            {
                mCreator = CreateMonoSingleton;
                mCachedPathAttribute = Attribute.GetCustomAttribute(type, typeof(MonoSingletonPathAttribute), true) as MonoSingletonPathAttribute;
                Application.quitting += OnApplicationQuitting;
            }
            else
            {
                mCreator = CreateNormalSingleton;
            }
        }

        /// <summary>
        /// Marks Mono singleton creation as disabled during application shutdown.
        /// </summary>
        private static void OnApplicationQuitting()
        {
            mIsQuitting = true;
        }

        /// <summary>
        /// Gets the singleton instance using lazy, thread-safe initialization.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (mIsQuitting && mIsMonoBehaviour)
                {
                    return null;
                }

                if (mInstance is null)
                {
                    lock (mLock)
                    {
                        mInstance ??= mCreator();
                    }
                }

                return mInstance;
            }
        }

        /// <summary>
        /// Clears the cached instance reference.
        /// </summary>
        public static void Dispose()
        {
            mInstance = null;
        }

        #endregion

        #region Creation

        /// <summary>
        /// Creates a plain C# singleton instance through reflection.
        /// </summary>
        /// <remarks>
        /// For IL2CPP or AOT environments, the singleton type still needs to be preserved and reachable.
        /// </remarks>
        private static T CreateNormalSingleton()
        {
            var type = typeof(T);

            T instance;
            try
            {
                instance = Activator.CreateInstance(type, true) as T;
            }
            catch (MissingMethodException)
            {
                throw new InvalidOperationException(
                    $"[SingletonKit] 类型 {type.Name} 必须有无参构造函数。" +
                    $"如果是 IL2CPP 构建，请确保该类型被显式引用或添加 [Preserve] 特性。");
            }

            instance?.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// Creates or locates a <see cref="MonoBehaviour"/> singleton instance.
        /// </summary>
        private static T CreateMonoSingleton()
        {
            if (!Application.isPlaying || mIsQuitting) return null;

            var type = typeof(T);

            if (UnityEngine.Object.FindFirstObjectByType(type) is T instance)
            {
                instance.OnSingletonInit();
                return instance;
            }

            if (mCachedPathAttribute is not null)
            {
                instance = CreateComponentOnGameObject(mCachedPathAttribute);
            }
            else
            {
                var obj = new GameObject(type.Name);
                UnityEngine.Object.DontDestroyOnLoad(obj);
                instance = obj.AddComponent(type) as T;
            }

            instance?.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// Creates or finds the GameObject defined by <see cref="MonoSingletonPathAttribute"/> and attaches the component.
        /// </summary>
        private static T CreateComponentOnGameObject(MonoSingletonPathAttribute pathAttr)
        {
            var obj = FindOrCreateGameObjectPath(pathAttr.PathInHierarchy);
            if (obj == null)
            {
                obj = pathAttr.IsRectTransform
                    ? new GameObject(typeof(T).Name, typeof(RectTransform))
                    : new GameObject(typeof(T).Name);
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }

            var instance = obj.GetComponent(typeof(T)) as T;
            if (instance == null)
            {
                instance = obj.AddComponent(typeof(T)) as T;
            }

            return instance;
        }

        /// <summary>
        /// Finds or creates a GameObject hierarchy path.
        /// </summary>
        /// <remarks>
        /// Uses span-based parsing to avoid allocating intermediate string arrays during path splitting.
        /// </remarks>
        private static GameObject FindOrCreateGameObjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var pathSpan = path.AsSpan();
            GameObject current = null;
            int start = 0;
            int depth = 0;

            for (int i = 0; i <= pathSpan.Length; i++)
            {
                if (i == pathSpan.Length || pathSpan[i] == '/')
                {
                    if (i > start)
                    {
                        var segment = pathSpan.Slice(start, i - start).ToString();
                        current = FindOrCreateChild(current, segment, depth == 0);
                        depth++;
                    }

                    start = i + 1;
                }
            }

            return current;
        }

        /// <summary>
        /// Finds or creates one child GameObject in the hierarchy path.
        /// </summary>
        private static GameObject FindOrCreateChild(GameObject parent, string name, bool isRoot)
        {
            GameObject child;

            if (parent == null)
            {
                child = GameObject.Find(name);
            }
            else
            {
                var childTransform = parent.transform.Find(name);
                child = childTransform != null ? childTransform.gameObject : null;
            }

            if (child == null)
            {
                child = new GameObject(name);
                if (parent != null)
                {
                    child.transform.SetParent(parent.transform);
                }

                if (isRoot)
                {
                    UnityEngine.Object.DontDestroyOnLoad(child);
                }
            }

            return child;
        }

        #endregion
    }
}
