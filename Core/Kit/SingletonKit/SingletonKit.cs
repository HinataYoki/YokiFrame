using System;
using System.Reflection;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 单例管理核心类：负责单例的持有、创建与生命周期管理
    /// </summary>
    public static class SingletonKit<T> where T : class, ISingleton
    {
        #region 实例持有

        /// <summary>
        /// 静态实例
        /// </summary>
        private static T mInstance;

        /// <summary>
        /// 是否正在销毁（防止 OnDestroy 中重新创建）
        /// </summary>
        private static bool mIsQuitting;

        /// <summary>
        /// 线程锁
        /// </summary>
        private static readonly object mLock = new();

        /// <summary>
        /// 创建策略委托（静态构造器中确定，避免每次运行时反射判断）
        /// </summary>
        private static readonly Func<T> mCreator;

        /// <summary>
        /// 是否是 MonoBehaviour 类型（缓存）
        /// </summary>
        private static readonly bool mIsMonoBehaviour;

        /// <summary>
        /// 缓存的无参构造函数（避免重复反射）
        /// </summary>
        private static readonly ConstructorInfo mCachedCtor;

        /// <summary>
        /// 缓存的路径属性（避免重复反射）
        /// </summary>
        private static readonly MonoSingletonPathAttribute mCachedPathAttribute;

        /// <summary>
        /// 静态构造器：一次性确定创建策略并缓存反射结果
        /// </summary>
        static SingletonKit()
        {
            var type = typeof(T);
            mIsMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(type);

            if (mIsMonoBehaviour)
            {
                mCreator = CreateMonoSingleton;
                // 缓存路径属性
                mCachedPathAttribute = GetPathAttribute(type);
                // 监听应用退出事件
                Application.quitting += OnApplicationQuitting;
            }
            else
            {
                mCreator = CreateNormalSingleton;
                // 缓存构造函数
                mCachedCtor = GetNonArgsConstructor(type);
            }
        }

        /// <summary>
        /// 获取路径属性（缓存用）- 使用 Attribute.GetCustomAttribute 直接获取单个属性，避免数组分配
        /// </summary>
        private static MonoSingletonPathAttribute GetPathAttribute(Type type)
        {
            return Attribute.GetCustomAttribute(type, typeof(MonoSingletonPathAttribute), true) as MonoSingletonPathAttribute;
        }

        /// <summary>
        /// 获取无参构造函数（缓存用）
        /// </summary>
        private static ConstructorInfo GetNonArgsConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < constructors.Length; i++)
            {
                if (constructors[i].GetParameters().Length == 0)
                {
                    return constructors[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 应用退出时标记
        /// </summary>
        private static void OnApplicationQuitting()
        {
            mIsQuitting = true;
        }

        /// <summary>
        /// 获取单例实例（线程安全，懒加载）
        /// </summary>
        public static T Instance
        {
            get
            {
                // 如果正在退出，不再创建新实例
                if (mIsQuitting && mIsMonoBehaviour)
                {
                    return null;
                }

                if (mInstance == null)
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
        /// 资源释放
        /// </summary>
        public static void Dispose()
        {
            mInstance = null;
        }

        #endregion

        #region 创建逻辑

        /// <summary>
        /// 创建普通 C# 单例
        /// </summary>
        private static T CreateNormalSingleton()
        {
            if (mCachedCtor == null)
            {
                throw new Exception($"Non-Args Constructor() not found! in {typeof(T)}");
            }

            var instance = mCachedCtor.Invoke(null) as T;
            instance.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// 创建 MonoBehaviour 单例
        /// </summary>
        private static T CreateMonoSingleton()
        {
            // 非运行时或正在退出时不创建
            if (!Application.isPlaying || mIsQuitting) return null;

            var type = typeof(T);

            // 尝试查找场景中已存在的实例
            if (UnityEngine.Object.FindFirstObjectByType(type) is T instance)
            {
                instance.OnSingletonInit();
                return instance;
            }

            // 根据缓存的属性或默认逻辑创建 GameObject
            if (mCachedPathAttribute != null)
            {
                instance = CreateComponentOnGameObject(mCachedPathAttribute);
            }
            else
            {
                // 默认创建
                var obj = new GameObject(type.Name);
                UnityEngine.Object.DontDestroyOnLoad(obj);
                instance = obj.AddComponent(type) as T;
            }

            instance.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// 根据路径属性创建组件
        /// </summary>
        private static T CreateComponentOnGameObject(MonoSingletonPathAttribute pathAttr)
        {
            var obj = FindOrCreateGameObjectPath(pathAttr.PathInHierarchy);
            if (obj == null)
            {
                // 兜底创建
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
        /// 查找或创建 GameObject 路径（使用 Span 优化字符串分割）
        /// </summary>
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
        /// 查找或创建子物体
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
