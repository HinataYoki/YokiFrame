#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>
    /// inspect 的**唯一底层契约**：按「根名 + 选择器」解析出一个对象实例。
    /// </summary>
    /// <remarks>
    /// 设计要点：上层（inspect 操作）只认识这个接口，不认识任何具体机制。
    /// 因此"框架服务 / 静态类型 / 引擎单例 / 游戏自定义容器"这些**不同底层**，
    /// 各自实现成一个策略即可；新增一类来源 = 新增一个实现，**不需要改高层**。
    /// </remarks>
    public interface IYokiFrameInspectRoot
    {
        /// <summary>获取根名（inspect payload 的 root 字段）。</summary>
        string Name { get; }

        /// <summary>按选择器解析实例。</summary>
        /// <param name="selector">选择器文本（类型名、键、路径…由实现定义）。</param>
        /// <param name="instance">解析到的实例。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>解析成功时返回 true。</returns>
        bool TryResolve(string selector, out object instance, out string error);
    }

    /// <summary>
    /// 根的注册表：inspect 的高层入口（组合 + 按名查表 + 诊断用清单）。
    /// </summary>
    /// <remarks>
    /// 这是"把不同底层抽象成一个高层"的落点：高层只问注册表要实例，
    /// 具体是哪种机制完全由注册进来的策略决定，且**顺序敏感**（先注册的先命中）。
    /// </remarks>
    public sealed class YokiFrameInspectRootRegistry
    {
        private readonly List<IYokiFrameInspectRoot> mRoots = new List<IYokiFrameInspectRoot>();

        /// <summary>创建只含内置根的注册表（框架服务 + 静态类型）。</summary>
        /// <returns>注册表。</returns>
        public static YokiFrameInspectRootRegistry CreateDefault()
        {
            var registry = new YokiFrameInspectRootRegistry();
            registry.Add(new ArchitectureInspectRoot());
            registry.Add(new StaticTypeInspectRoot());
            return registry;
        }

        /// <summary>获取全部根（只读）。</summary>
        public IReadOnlyList<IYokiFrameInspectRoot> Roots
        {
            get { return mRoots; }
        }

        /// <summary>注册一个根；同名已存在时替换（后注册者优先语义由调用方决定）。</summary>
        /// <param name="root">根。</param>
        /// <returns>当前注册表。</returns>
        public YokiFrameInspectRootRegistry Add(IYokiFrameInspectRoot root)
        {
            if (root == null || string.IsNullOrWhiteSpace(root.Name))
            {
                return this;
            }

            for (var index = 0; index < mRoots.Count; index++)
            {
                if (string.Equals(mRoots[index].Name, root.Name, StringComparison.OrdinalIgnoreCase))
                {
                    mRoots[index] = root;
                    return this;
                }
            }

            mRoots.Add(root);
            return this;
        }

        /// <summary>注册一个"由访问器解析"的根（引擎单例、自动加载、游戏容器都走这一条）。</summary>
        /// <param name="name">根名。</param>
        /// <param name="accessor">解析器；返回 null 表示未命中。</param>
        /// <returns>当前注册表。</returns>
        public YokiFrameInspectRootRegistry AddAccessor(string name, Func<string, object> accessor)
        {
            return accessor == null ? this : Add(new AccessorInspectRoot(name, accessor));
        }

        /// <summary>按名解析实例。</summary>
        /// <param name="rootName">根名。</param>
        /// <param name="selector">选择器。</param>
        /// <param name="instance">实例。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryResolve(string rootName, string selector, out object instance, out string error)
        {
            instance = null;
            error = string.Empty;
            IYokiFrameInspectRoot root = Find(rootName);
            if (root == null)
            {
                error = "unknown root: " + rootName + ". Known roots: " + DescribeRoots() + ".";
                return false;
            }

            return root.TryResolve(selector, out instance, out error);
        }

        /// <summary>按名查找根。</summary>
        /// <param name="rootName">根名。</param>
        /// <returns>根；未注册返回 null。</returns>
        public IYokiFrameInspectRoot Find(string rootName)
        {
            for (var index = 0; index < mRoots.Count; index++)
            {
                if (string.Equals(mRoots[index].Name, rootName, StringComparison.OrdinalIgnoreCase))
                {
                    return mRoots[index];
                }
            }

            return null;
        }

        /// <summary>列出已注册根名（诊断/报错用）。</summary>
        /// <returns>逗号分隔的根名。</returns>
        public string DescribeRoots()
        {
            var names = new List<string>(mRoots.Count);
            for (var index = 0; index < mRoots.Count; index++)
            {
                names.Add(mRoots[index].Name);
            }

            return string.Join(", ", names);
        }
    }

    /// <summary>策略：框架 Architecture 里注册的服务/模型。</summary>
    public sealed class ArchitectureInspectRoot : IYokiFrameInspectRoot
    {
        /// <summary>根名。</summary>
        public const string ROOT_NAME = "service";

        /// <summary>获取根名。</summary>
        public string Name
        {
            get { return ROOT_NAME; }
        }

        /// <summary>按类型全名或短名解析已注册的服务/模型。</summary>
        /// <param name="selector">类型全名或短名。</param>
        /// <param name="instance">实例。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>命中时返回 true。</returns>
        public bool TryResolve(string selector, out object instance, out string error)
        {
            instance = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(selector))
            {
                error = "service root requires a type name as selector.";
                return false;
            }

            foreach (var registration in ArchitectureRegistry.ReadLiveServiceCatalog(4096, out _))
            {
                Type type = registration.ImplementationType;
                if (string.Equals(type.FullName, selector, StringComparison.Ordinal)
                    || string.Equals(type.Name, selector, StringComparison.Ordinal))
                {
                    if (!ArchitectureRegistry.TryResolveServiceRegistration(registration.RegistrationId, out var service)) continue;
                    if (instance != null && !ReferenceEquals(instance, service))
                    {
                        instance = null;
                        error = "Multiple services match; use object_list and RequireService<T> with an architecture name.";
                        return false;
                    }
                    instance = service;
                }
            }
            if (instance != null) return true;
            error = "no registered service/model matched: " + selector + ".";
            return false;
        }
    }

    /// <summary>策略：类型上的静态成员（全局配置、纯静态表）。</summary>
    public sealed class StaticTypeInspectRoot : IYokiFrameInspectRoot
    {
        /// <summary>根名。</summary>
        public const string ROOT_NAME = "static";

        /// <summary>获取根名。</summary>
        public string Name
        {
            get { return ROOT_NAME; }
        }

        /// <summary>按类型全名解析（实例为 null，表示读静态成员）。</summary>
        /// <param name="selector">类型全名。</param>
        /// <param name="instance">恒为 null。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>找到类型时返回 true。</returns>
        public bool TryResolve(string selector, out object instance, out string error)
        {
            instance = null;
            error = string.Empty;
            if (FindType(selector) == null)
            {
                error = "type was not found: " + selector + ".";
                return false;
            }

            return true;
        }

        /// <summary>按全名在各已加载程序集里找类型。</summary>
        /// <param name="name">类型全名。</param>
        /// <returns>类型；未找到返回 null。</returns>
        public static Type FindType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Type direct = Type.GetType(name);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    Type match = assemblies[index].GetType(name);
                    if (match != null)
                    {
                        return match;
                    }
                }
                catch (Exception)
                {
                    // 动态程序集可能拒绝枚举：跳过。
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 策略：由访问器解析的根——**引擎差异的统一收口**。
    /// </summary>
    /// <remarks>
    /// Unity 的 Mono 单例、Godot 的 C# AutoLoad、游戏自己的容器，差异只在"怎么查"，
    /// 因此统一成"名字 + 访问器"这一个实现；各适配器只提供访问器，不再各写一套根。
    /// </remarks>
    public sealed class AccessorInspectRoot : IYokiFrameInspectRoot
    {
        private readonly Func<string, object> mAccessor;

        /// <summary>创建访问器根。</summary>
        /// <param name="name">根名。</param>
        /// <param name="accessor">解析器。</param>
        public AccessorInspectRoot(string name, Func<string, object> accessor)
        {
            Name = name ?? string.Empty;
            mAccessor = accessor;
        }

        /// <summary>获取根名。</summary>
        public string Name { get; }

        /// <summary>调用访问器解析。</summary>
        /// <param name="selector">选择器。</param>
        /// <param name="instance">实例。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>命中时返回 true。</returns>
        public bool TryResolve(string selector, out object instance, out string error)
        {
            instance = mAccessor == null ? null : mAccessor(selector);
            error = instance == null ? Name + " root did not resolve: " + selector + "." : string.Empty;
            return instance != null;
        }
    }
}
#endif
