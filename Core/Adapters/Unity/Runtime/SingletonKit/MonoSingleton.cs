#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// Unity MonoBehaviour 单例基类；需要 GameObject 或 Unity 生命周期时使用。
    /// </summary>
    /// <typeparam name="T">具体 MonoBehaviour 单例类型。</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
    {
        private static readonly object sLock = new();
        private static T sInstance;
        private static T sPendingDestroyInstance;
        private bool mSingletonInitialized;

        /// <summary>
        /// 获取单例实例；不存在时优先查找场景实例，仍不存在则自动创建 GameObject。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (MonoSingletonExitState.IsQuitting)
                {
                    return null;
                }

                if (sInstance != null)
                {
                    return sInstance;
                }

                lock (sLock)
                {
                    if (MonoSingletonExitState.IsQuitting)
                    {
                        return null;
                    }

                    if (sInstance == null)
                    {
                        RegisterInstance(FindOrCreateInstance());
                    }

                    return sInstance;
                }
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
        /// 清除单例实例引用，并销毁关联 GameObject。
        /// </summary>
        public static void Dispose()
        {
            if (sInstance == null)
            {
                return;
            }

            GameObject instanceObject = sInstance.gameObject;
#if UNITY_EDITOR
            SingletonRegistry.Unregister(typeof(T));
#endif
            sPendingDestroyInstance = sInstance;
            sInstance = null;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityObject.DestroyImmediate(instanceObject);
                return;
            }
#endif
            UnityObject.Destroy(instanceObject);
        }

        /// <summary>
        /// Unity Awake 回调，负责登记手动放入场景的单例组件。
        /// </summary>
        protected virtual void Awake()
        {
            RegisterInstance(this as T);
        }

        /// <summary>
        /// 组件销毁时清除静态引用并更新诊断状态。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
#if UNITY_EDITOR
                SingletonRegistry.Unregister(typeof(T));
#endif
                sInstance = null;
            }

            if (ReferenceEquals(sPendingDestroyInstance, this))
            {
                sPendingDestroyInstance = null;
            }
        }

        /// <summary>
        /// 应用退出时阻止销毁链路重新创建单例。
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            MonoSingletonExitState.MarkQuitting();
        }

        /// <summary>
        /// 单例初始化完成后调用；子类可重写完成自身初始化。
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }

        /// <summary>
        /// 查找场景实例；不存在时按路径规则创建新的 GameObject 和组件。
        /// </summary>
        /// <returns>可登记的 MonoSingleton 实例。</returns>
        private static T FindOrCreateInstance()
        {
#if UNITY_2023_1_OR_NEWER
            // 新版本使用不排序的查找 API，保留包含非激活对象的单例语义。
            T instance = UnityObject.FindAnyObjectByType<T>(FindObjectsInactive.Include);
#else
            // Unity 2022.3 使用稳定的 includeInactive 重载，避免依赖双参数新签名。
            T instance = UnityObject.FindObjectOfType<T>(true);
#endif
            if (instance != null && !IsPendingDestroy(instance))
            {
                return instance;
            }

            GameObject gameObject = CreateSingletonGameObject();
            T existing = gameObject.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 登记实例并处理重复实例。
        /// </summary>
        /// <param name="instance">待登记的实例。</param>
        protected static void RegisterInstance(T instance)
        {
            if (instance == null || IsPendingDestroy(instance))
            {
                return;
            }

            if (sInstance != null && !ReferenceEquals(sInstance, instance))
            {
                DestroyDuplicate(instance);
                return;
            }

            sInstance = instance;
            sInstance.EnsureSingletonInitialized();
            MoveRootToDontDestroyOnLoad(sInstance.gameObject);
#if UNITY_EDITOR
            SingletonRegistry.Register(typeof(T), sInstance, "Unity", "MonoSingleton");
#endif
        }

        /// <summary>
        /// 确保 OnSingletonInit 对同一实例只调用一次。
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

        /// <summary>
        /// 销毁重复的 MonoSingleton 实例，避免同类型实例同时存活。
        /// </summary>
        /// <param name="instance">重复实例。</param>
        private static void DestroyDuplicate(T instance)
        {
            GameObject instanceObject = instance.gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityObject.DestroyImmediate(instanceObject);
                return;
            }
#endif
            UnityObject.Destroy(instanceObject);
        }

        /// <summary>
        /// 根据 MonoSingletonPathAttribute 创建或复用层级路径上的 GameObject。
        /// </summary>
        /// <returns>承载单例组件的 GameObject。</returns>
        private static GameObject CreateSingletonGameObject()
        {
            var attribute = Attribute.GetCustomAttribute(typeof(T), typeof(MonoSingletonPathAttribute)) as MonoSingletonPathAttribute;
            if (attribute == null || string.IsNullOrEmpty(attribute.PathInHierarchy))
            {
                return new GameObject(typeof(T).Name);
            }

            string normalizedPath = attribute.PathInHierarchy.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return new GameObject(typeof(T).Name);
            }

            GameObject existing = GameObject.Find(normalizedPath);
            if (existing != null && !IsPendingDestroyObject(existing))
            {
                return existing;
            }

            return CreatePath(normalizedPath.Split('/'), attribute.IsRectTransform);
        }

        /// <summary>
        /// 按路径片段创建层级节点，并返回最后一级 GameObject。
        /// </summary>
        /// <param name="segments">路径片段。</param>
        /// <param name="lastNodeUsesRectTransform">最后一级是否使用 RectTransform。</param>
        /// <returns>路径最后一级 GameObject。</returns>
        private static GameObject CreatePath(string[] segments, bool lastNodeUsesRectTransform)
        {
            Transform parent = null;
            GameObject current = null;

            for (var index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                string path = BuildPath(segments, index);
                current = GameObject.Find(path);
                if (index == segments.Length - 1 && IsPendingDestroyObject(current))
                {
                    current = null;
                }
                if (current == null)
                {
                    current = CreatePathNode(segment, lastNodeUsesRectTransform && index == segments.Length - 1);
                }

                if (parent != null)
                {
                    current.transform.SetParent(parent, false);
                }

                parent = current.transform;
            }

            return current != null ? current : new GameObject(typeof(T).Name);
        }

        /// <summary>
        /// 创建路径节点。
        /// </summary>
        /// <param name="name">节点名称。</param>
        /// <param name="useRectTransform">是否使用 RectTransform。</param>
        /// <returns>新建 GameObject。</returns>
        private static GameObject CreatePathNode(string name, bool useRectTransform)
        {
            return useRectTransform ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        }

        /// <summary>
        /// 构造从根到指定索引的层级路径。
        /// </summary>
        /// <param name="segments">路径片段。</param>
        /// <param name="lastIndex">最后一个片段索引。</param>
        /// <returns>Unity 层级路径。</returns>
        private static string BuildPath(string[] segments, int lastIndex)
        {
            string path = segments[0];
            for (var index = 1; index <= lastIndex; index++)
            {
                path += "/" + segments[index];
            }

            return path;
        }

        /// <summary>
        /// 判断实例是否已清除静态所有权但仍在等待 Unity 提交延迟销毁。
        /// </summary>
        /// <param name="instance">待检查的单例组件。</param>
        /// <returns>实例属于当前 pending-destroy 对象时返回 true。</returns>
        private static bool IsPendingDestroy(T instance)
        {
            return instance != null && ReferenceEquals(instance, sPendingDestroyInstance);
        }

        /// <summary>
        /// 判断路径候选 GameObject 是否承载当前待销毁实例，避免同帧重建复用旧对象。
        /// </summary>
        /// <param name="gameObject">路径查找得到的候选对象。</param>
        /// <returns>候选对象属于待销毁实例时返回 true。</returns>
        private static bool IsPendingDestroyObject(GameObject gameObject)
        {
            if (gameObject == null || sPendingDestroyInstance == null) return false;
            T component = gameObject.GetComponent<T>();
            return component != null && ReferenceEquals(component, sPendingDestroyInstance);
        }

        /// <summary>
        /// 在运行模式下把单例根节点标记为切场景不销毁。
        /// </summary>
        /// <param name="gameObject">单例所在 GameObject。</param>
        private static void MoveRootToDontDestroyOnLoad(GameObject gameObject)
        {
            if (!Application.isPlaying || gameObject == null)
            {
                return;
            }

            GameObject root = gameObject.transform.root.gameObject;
            if (root != null)
            {
                DontDestroyOnLoad(root);
            }
        }
    }

    /// <summary>
    /// 记录 Unity 应用退出状态，避免退出销毁期间重新创建 MonoSingleton。
    /// </summary>
    internal static class MonoSingletonExitState
    {
        private static bool sIsQuitting;

        /// <summary>
        /// 获取当前是否处于应用退出或退出播放模式阶段。
        /// </summary>
        internal static bool IsQuitting
        {
            get { return sIsQuitting; }
        }

        /// <summary>
        /// 标记应用正在退出。
        /// </summary>
        internal static void MarkQuitting()
        {
            sIsQuitting = true;
        }

        /// <summary>
        /// 重置退出状态，供 Unity 子系统重载或测试使用。
        /// </summary>
        internal static void ResetForTests()
        {
            sIsQuitting = false;
        }

        /// <summary>
        /// Unity 子系统注册阶段重置退出状态。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            ResetForTests();
        }
    }
}
#endif
