#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>
    /// 已加载程序集的枚举入口。Core 不引用宿主 SDK，默认使用 AppDomain；
    /// Unity 6.5 及以上由适配器替换为 CurrentAssemblies，避免读到已卸载程序集。
    /// </summary>
    public static class LoadedAssemblies
    {
        private static Func<IEnumerable<Assembly>> sSource = DefaultSource;

        /// <summary>
        /// 返回当前应参与类型查找和编译引用的程序集。
        /// </summary>
        /// <returns>已加载且可安全反射的程序集。</returns>
        public static IEnumerable<Assembly> Get()
        {
            return sSource();
        }

        /// <summary>
        /// 替换枚举实现。只应由匹配的宿主适配器在安装时调用一次。
        /// </summary>
        /// <param name="source">宿主提供的程序集枚举；为空时恢复默认实现。</param>
        public static void Use(Func<IEnumerable<Assembly>> source)
        {
            sSource = source ?? DefaultSource;
        }

        /// <summary>2022.3 与 Godot 可用的默认枚举。</summary>
        /// <returns>当前 AppDomain 中的程序集。</returns>
        private static IEnumerable<Assembly> DefaultSource()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }
    }
}
#endif
