#if !GODOT
using System;
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// MonoBehaviour 单例基类。
    /// 仅在需要 Unity 生命周期回调时使用；无 Unity 依赖的单例使用 Base 层 Singleton&lt;T&gt;
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
    {
        private static T sInstance;
        private static readonly object sLock = new();

        /// <summary>
        /// 获取单例实例。若不存在则查找场景中已有的实例，
        /// 未找到则自动创建新的 GameObject。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (sInstance == default)
                {
                    lock (sLock)
                    {
                        if (sInstance == default)
                        {
                            // 先尝试查找场景中已有的实例
#if UNITY_6000_6_OR_NEWER
                            sInstance = FindAnyObjectByType<T>();
#elif UNITY_2022_2_OR_NEWER || UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                            sInstance = FindFirstObjectByType<T>();
#else
                            sInstance = FindObjectOfType<T>();
#endif
                            if (sInstance == default)
                            {
                                var go = CreateSingletonGameObject();
                                sInstance = go.AddComponent<T>();
                                // AddComponent 调用 Awake，之后触发 OnSingletonInit
                                sInstance.OnSingletonInit();
                            }

                            MoveRootToDontDestroyOnLoad(sInstance.gameObject);
                            SingletonRegistry.Register(typeof(T), sInstance, "Unity", "MonoSingleton");
                        }
                    }
                }
                return sInstance;
            }
        }

        /// <summary>
        /// 清除单例实例引用并销毁关联的 GameObject
        /// </summary>
        public static void Dispose()
        {
            if (sInstance != default)
            {
                var gameObjectInstance = sInstance.gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(gameObjectInstance);
                else
#endif
                    UnityEngine.Object.Destroy(gameObjectInstance);
                SingletonRegistry.Unregister(typeof(T));
                sInstance = null;
            }
        }

        /// <summary>
        /// 单例初始化完成后的回调
        /// </summary>
        public virtual void OnSingletonInit() { }

        /// <summary>
        /// 组件销毁时清除单例引用
        /// </summary>
        protected virtual void OnDestroy()
        {
            SingletonRegistry.Unregister(typeof(T));
            if (sInstance == this)
                sInstance = null;
        }

        private static GameObject CreateSingletonGameObject()
        {
            var attribute = Attribute.GetCustomAttribute(typeof(T), typeof(MonoSingletonPathAttribute)) as MonoSingletonPathAttribute;
            if (attribute == null || string.IsNullOrEmpty(attribute.PathInHierarchy))
                return new GameObject(typeof(T).Name);

            var normalizedPath = attribute.PathInHierarchy.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalizedPath))
                return new GameObject(typeof(T).Name);

            var existing = GameObject.Find(normalizedPath);
            if (existing != default)
                return existing;

            var segments = normalizedPath.Split('/');
            Transform parent = null;
            GameObject current = null;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                    continue;

                var path = BuildPath(segments, i);
                current = GameObject.Find(path);
                if (current == default)
                    current = CreatePathNode(segment, attribute.IsRectTransform && i == segments.Length - 1);

                if (parent != default)
                    current.transform.SetParent(parent, false);

                parent = current.transform;
            }

            return current != default ? current : new GameObject(typeof(T).Name);
        }

        private static void MoveRootToDontDestroyOnLoad(GameObject gameObject)
        {
            if (!Application.isPlaying)
                return;

            if (gameObject == default)
                return;

            var root = gameObject.transform.root.gameObject;
            if (root != default)
                DontDestroyOnLoad(root);
        }

        private static GameObject CreatePathNode(string name, bool useRectTransform)
        {
            return useRectTransform
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);
        }

        private static string BuildPath(string[] segments, int lastIndex)
        {
            var path = segments[0];
            for (var i = 1; i <= lastIndex; i++)
                path += "/" + segments[i];

            return path;
        }
    }
}
#endif
