#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>
    /// 已加载程序集的枚举入口。Core 不引用宿主 SDK。
    /// Unity 6.5 及以上由适配器替换为 CurrentAssemblies 和 GetLoadedAssemblyPath。
    /// </summary>
    public static class LoadedAssemblies
    {
        private static Func<IEnumerable<Assembly>> sSource = DefaultSource;
        private static Func<Assembly, string> sPath = DefaultPath;

        /// <summary>
        /// 返回当前应参与类型查找和编译引用的程序集。
        /// </summary>
        /// <returns>已加载且可安全反射的程序集。</returns>
        public static IEnumerable<Assembly> Get()
        {
            return sSource();
        }

        /// <summary>
        /// 返回程序集的磁盘路径。动态程序集或未知路径由宿主返回空。
        /// </summary>
        /// <param name="assembly">已加载程序集。</param>
        /// <returns>程序集路径；未知时为空。</returns>
        public static string GetPath(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            return sPath(assembly);
        }

        /// <summary>
        /// 替换枚举实现。只应由匹配的宿主适配器在安装时调用一次。
        /// </summary>
        /// <param name="source">宿主提供的程序集枚举；为空时恢复默认实现。</param>
        public static void Use(Func<IEnumerable<Assembly>> source)
        {
            sSource = source ?? DefaultSource;
        }

        /// <summary>
        /// 替换路径查询。只应由匹配的宿主适配器在安装时调用一次。
        /// </summary>
        /// <param name="path">宿主提供的路径查询；为空时恢复默认实现。</param>
        public static void UsePath(Func<Assembly, string> path)
        {
            sPath = path ?? DefaultPath;
        }

        /// <summary>2022.3 与 Godot 可用的默认枚举。Unity 6.5 起必须由适配器接管。</summary>
        /// <returns>当前 AppDomain 中的程序集。</returns>
        private static IEnumerable<Assembly> DefaultSource()
        {
#if UNITY_6000_5_OR_NEWER
            throw new InvalidOperationException(
                "Unity 6.5+ requires the host adapter to enumerate loaded assemblies.");
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        /// <summary>2022.3 与 Godot 使用 Assembly.Location。Unity 6.5 起该属性恒为空，必须由适配器接管。</summary>
        /// <param name="assembly">已加载程序集。</param>
        /// <returns>程序集路径。</returns>
        private static string DefaultPath(Assembly assembly)
        {
#if UNITY_6000_5_OR_NEWER
            throw new InvalidOperationException(
                "Unity 6.5+ requires the host adapter to resolve assembly paths.");
#else
            return assembly.Location;
#endif
        }
    }
}
#endif
