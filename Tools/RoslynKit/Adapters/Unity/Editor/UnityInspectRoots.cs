#if UNITY_EDITOR || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Unity 侧为 inspect 追加的内置根。
    /// </summary>
    /// <remarks>
    /// 这里只提供**访问器**，不新增根类型：引擎差异（Unity 的 Mono 单例、Godot 的 AutoLoad、
    /// 游戏自己的容器）全部收口在 <see cref="AccessorInspectRoot"/> 这一个策略里。
    /// 之所以放在适配器而不是 Core：SingletonRegistry 的编译守卫是 UNITY_EDITOR / (GODOT &amp;&amp; TOOLS)，
    /// 工具链（YOKIFRAME_TOOLING）构建里不存在该类型。
    /// </remarks>
    internal static class UnityInspectRoots
    {
        /// <summary>单例根名。</summary>
        internal const string SINGLETON_ROOT = "singleton";

        /// <summary>创建 Unity 侧内置根。</summary>
        /// <returns>根列表。</returns>
        internal static IReadOnlyList<IInspectRoot> Create()
        {
            return new IInspectRoot[]
            {
                new AccessorInspectRoot(SINGLETON_ROOT, ResolveSingleton)
            };
        }

        /// <summary>
        /// 按类型全名或短名解析活动单例。
        /// </summary>
        /// <remarks>
        /// 与 Architecture 同思路：SingletonRegistry 只存**诊断快照**（刻意不持有实例引用），
        /// 所以要拿实例得扫描已加载程序集、按名字匹配类型并读它的**静态 Instance 属性**。
        /// 这样 Unity 的 MonoSingleton 与框架自带单例都能读，且不依赖任何具体基类。
        /// </remarks>
        /// <param name="selector">类型全名或短名。</param>
        /// <returns>实例；未命中或尚未创建返回 null。</returns>
        private static object ResolveSingleton(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return null;
            }

            foreach (System.Reflection.Assembly assembly in LoadedAssemblies.Get())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type.IsAbstract || type.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    if (!string.Equals(type.FullName, selector, StringComparison.Ordinal)
                        && !string.Equals(type.Name, selector, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    System.Reflection.PropertyInfo instance = type.GetProperty(
                        "Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instance == null || !instance.CanRead)
                    {
                        continue;
                    }

                    object value = instance.GetValue(null);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }
}
#endif