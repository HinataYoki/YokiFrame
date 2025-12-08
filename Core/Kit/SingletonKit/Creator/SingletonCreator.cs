using System.Reflection;
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 普通单例创建类
    /// </summary>
    internal static class SingletonCreator
    {
        /// <summary>
        /// 创建单例
        /// </summary>
        public static T CreateSingleton<T>() where T : class, ISingleton
        {
            var type = typeof(T);
            var monoBehaviourType = typeof(MonoBehaviour);

            if (monoBehaviourType.IsAssignableFrom(type))
            {
                return CreateMonoSingleton<T>();
            }
            else
            {
                var instance = CreateNonArgsConstructorObject<T>();
                instance.OnSingletonInit();
                return instance;
            }
        }

        /// <summary>
        /// 泛型方法：创建MonoBehaviour单例
        /// </summary>
        public static T CreateMonoSingleton<T>() where T : class, ISingleton
        {
            //判断T实例存在的条件是否满足
            if (!Application.isPlaying) return null;
            var type = typeof(T);
            var monoBehaviourType = typeof(MonoBehaviour);
            if (!monoBehaviourType.IsAssignableFrom(type)) return null;

            //判断当前场景中是否存在T实例
            //如果还是无法找到instance  则主动去创建同名Obj 并挂载相关脚本组件
            T instance = UnityEngine.Object.FindFirstObjectByType(type) as T;
            if (instance == null)
            {
                //MemberInfo：获取有关成员属性的信息并提供对成员元数据的访问
                MemberInfo info = typeof(T);
                //获取T类型 自定义属性，并找到相关路径属性，利用该属性创建T实例
                var attributes = info.GetCustomAttributes(true);

                foreach (var atribute in attributes)
                {
                    if (atribute is MonoSingletonPathAttribute defineAttri)
                    {
                        instance = CreateComponentOnGameObject<T>(defineAttri);
                        break;
                    }
                    continue;
                }

                //依旧创建失败则默认创建
                if (instance == null)
                {
                    var obj = new GameObject(typeof(T).Name);
                    UnityEngine.Object.DontDestroyOnLoad(obj);
                    instance = obj.AddComponent(typeof(T)) as T;
                }
            }

            instance.OnSingletonInit();
            return instance;
        }

        private static T CreateNonArgsConstructorObject<T>() where T : class
        {
            var type = typeof(T);
            // 获取构造函数
            var constructorInfos = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 获取无参构造函数
            var ctor = Array.Find(constructorInfos, c => c.GetParameters().Length == 0);

            return ctor switch
            {
                null => throw new Exception($"Non-Args Constructor() not found! in {type}"),
                _ => ctor.Invoke(null) as T
            };
        }

        /// <summary>
        /// 在GameObject上创建T组件（脚本）
        /// </summary>
        /// <param name="defineAttri">路径</param>
        /// <returns></returns>
        private static T CreateComponentOnGameObject<T>(MonoSingletonPathAttribute defineAttri) where T : class
        {
            var obj = FindGameObject(defineAttri.PathInHierarchy, true);
            if (obj == null)
            {
                obj = defineAttri.IsRectTransform ? new GameObject(typeof(T).Name, typeof(RectTransform)) : new GameObject(typeof(T).Name);
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }

            return obj.AddComponent(typeof(T)) as T;
        }

        /// <summary>
        /// 查找Obj（对于路径 进行拆分）
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="build">true</param>
        /// <returns></returns>
        private static GameObject FindGameObject(string path, bool build)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var subPath = path.Split('/');
            if (subPath == null || subPath.Length == 0)
            {
                return null;
            }

            return FindGameObject(null, subPath, 0, build);
        }

        /// <summary>
        /// 查找Obj（一个嵌套查找Obj的过程）
        /// </summary>
        /// <param name="root">父节点</param>
        /// <param name="subPath">拆分后的路径节点</param>
        /// <param name="index">下标</param>
        /// <param name="build">true</param>
        /// <returns></returns>
        private static GameObject FindGameObject(GameObject root, string[] subPath, int index, bool build)
        {
            GameObject client = null;

            if (root == null)
            {
                client = GameObject.Find(subPath[index]);
            }
            else
            {
                var child = root.transform.Find(subPath[index]);
                if (child != null)
                {
                    client = child.gameObject;
                }
            }

            if (client == null)
            {
                if (build)
                {
                    client = new GameObject(subPath[index]);
                    if (root != null)
                    {
                        client.transform.SetParent(root.transform);
                    }

                    if (index == 0)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(client);
                    }
                }
            }

            if (client == null)
            {
                return null;
            }

            return ++index == subPath.Length ? client : FindGameObject(client, subPath, index, build);
        }
    }



    /// <summary>
    /// 用于调整生成的Mono单例所在的层级
    /// 例如"YokiFrame/ActionKit/Queue"
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        public MonoSingletonPathAttribute(string pathInHierarchy, bool isRectTransform = false)
        {
            PathInHierarchy = pathInHierarchy;
            IsRectTransform = isRectTransform;
        }

        public string PathInHierarchy { get; private set; }
        public bool IsRectTransform { get; private set; }
    }
}