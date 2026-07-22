#if UNITY_EDITOR

using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 保存一次依赖刷新所使用的 package、asmdef 与预编译程序集事实快照。
    /// </summary>
    internal sealed class DependencyInventorySnapshot
    {
        /// <summary>
        /// 创建不可变依赖快照，并复制输入数组以避免刷新过程中被外部修改。
        /// </summary>
        /// <param name="packageNames">已注册 package 名称。</param>
        /// <param name="assemblyDefinitionNames">asmdef JSON 中的真实 name。</param>
        /// <param name="precompiledAssemblyNames">预编译程序集文件名。</param>
        public DependencyInventorySnapshot(
            string[] packageNames,
            string[] assemblyDefinitionNames,
            string[] precompiledAssemblyNames,
            string[] diagnostics = null)
        {
            PackageNames = packageNames == null ? Array.Empty<string>() : (string[])packageNames.Clone();
            AssemblyDefinitionNames = assemblyDefinitionNames == null
                ? Array.Empty<string>()
                : (string[])assemblyDefinitionNames.Clone();
            PrecompiledAssemblyNames = precompiledAssemblyNames == null
                ? Array.Empty<string>()
                : (string[])precompiledAssemblyNames.Clone();
            Diagnostics = diagnostics == null ? Array.Empty<string>() : (string[])diagnostics.Clone();
        }

        /// <summary>
        /// 获取本次采集的已注册 package 名称。
        /// </summary>
        public string[] PackageNames { get; }

        /// <summary>
        /// 获取本次采集的 asmdef 真实 name。
        /// </summary>
        public string[] AssemblyDefinitionNames { get; }

        /// <summary>
        /// 获取本次采集的预编译程序集文件名。
        /// </summary>
        public string[] PrecompiledAssemblyNames { get; }

        /// <summary>
        /// 获取采集过程中已隔离的单文件诊断；这些诊断不应阻断其它依赖证据参与规划。
        /// </summary>
        public string[] Diagnostics { get; }
    }
}

#endif
