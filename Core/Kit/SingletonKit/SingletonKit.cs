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
        /// 线程锁
        /// </summary>
        private static readonly object mLock = new();

        /// <summary>
        /// 创建策略委托（静态构造器中确定，避免每次运行时反射判断）
        /// </summary>
        private static readonly Func<T> mCreator;

        /// <summary>
        /// 静态构造器：一次性确定创建策略
        /// </summary>
        static SingletonKit()
        {
            var type = typeof(T);
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                mCreator = CreateMonoSingleton;
            }
            else
            {
                mCreator = CreateNormalSingleton;
            }
        }

        /// <summary>
        /// 获取单例实例（线程安全，懒加载）
        /// </summary>
        public static T Instance
        {
            get
            {
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
            var instance = CreateNonArgsConstructorObject();
            instance.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// 创建普通 C# 对象单例
        /// </summary>
        private static T CreateNonArgsConstructorObject()
        {
            var type = typeof(T);
            // 获取构造函数（包括私有构造）
            var constructorInfos = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 获取无参构造函数
            var ctor = Array.Find(constructorInfos, c => c.GetParameters().Length == 0);

            if (ctor == null)
                throw new Exception($"Non-Args Constructor() not found! in {type}");

            return ctor.Invoke(null) as T;
        }

        /// <summary>
        /// 创建 MonoBehaviour 单例
        /// </summary>
        private static T CreateMonoSingleton()
        {
            if (!Application.isPlaying) return null;

            var type = typeof(T);

            // 尝试查找场景中已存在的实例
            if (UnityEngine.Object.FindFirstObjectByType(type) is not T instance)
            {
                // 如果没找到，检查是否有路径属性
                MemberInfo info = typeof(T);
                var attributes = info.GetCustomAttributes(true);
                MonoSingletonPathAttribute pathAttribute = null;

                foreach (var attribute in attributes)
                {
                    if (attribute is MonoSingletonPathAttribute defineAttri)
                    {
                        pathAttribute = defineAttri;
                        break;
                    }
                }

                // 根据属性或默认逻辑创建 GameObject
                if (pathAttribute != null)
                {
                    instance = CreateComponentOnGameObject(pathAttribute);
                }
                else
                {
                    // 默认创建
                    var obj = new GameObject(type.Name);
                    UnityEngine.Object.DontDestroyOnLoad(obj);
                    instance = obj.AddComponent(type) as T;
                }
            }

            instance.OnSingletonInit();
            return instance;
        }

        /// <summary>
        /// 根据路径属性创建组件
        /// </summary>
        private static T CreateComponentOnGameObject(MonoSingletonPathAttribute defineAttri)
        {
            var obj = FindAndCreateGameObjectPath(defineAttri.PathInHierarchy);
            if (obj == null)
            {
                // 如果路径创建失败（极其罕见），兜底创建
                obj = defineAttri.IsRectTransform
                    ? new GameObject(typeof(T).Name, typeof(RectTransform))
                    : new GameObject(typeof(T).Name);
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }

            var instance = obj.GetComponent(typeof(T));
            if (instance == null)
            {
                instance = obj.AddComponent(typeof(T));
            }
            return instance as T;
        }

        /// <summary>
        /// 递归查找并创建 GameObject 路径
        /// </summary>
        private static GameObject FindAndCreateGameObjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var subPath = path.Split('/');
            if (subPath.Length == 0) return null;

            return FindGameObjectRecursive(null, subPath, 0);
        }

        private static GameObject FindGameObjectRecursive(GameObject root, string[] subPath, int index)
        {
            GameObject currentObj = null;

            // 查找当前层级
            if (root == null)
            {
                currentObj = GameObject.Find(subPath[index]);
            }
            else
            {
                var child = root.transform.Find(subPath[index]);
                if (child != null) currentObj = child.gameObject;
            }

            // 如果不存在则创建
            if (currentObj == null)
            {
                currentObj = new GameObject(subPath[index]);
                if (root != null)
                {
                    currentObj.transform.SetParent(root.transform);
                }

                // 根节点设置 DontDestroyOnLoad
                if (index == 0)
                {
                    UnityEngine.Object.DontDestroyOnLoad(currentObj);
                }
            }

            // 递归结束条件
            if (index == subPath.Length - 1)
            {
                return currentObj;
            }

            return FindGameObjectRecursive(currentObj, subPath, index + 1);
        }

        #endregion
    }

    /// <summary>
    /// 用于定义生成的 Mono 单例所在的层级路径
    /// 例如: [MonoSingletonPath("YokiFrame/ActionKit/Queue")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        public string PathInHierarchy { get; private set; }
        public bool IsRectTransform { get; private set; }

        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }
    }
}