#if UNITY_EDITOR

using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 集中定义 Unity 可选依赖与 YokiFrame 编译宏之间的稳定映射。
    /// </summary>
    internal static class DependencyDefineCatalog
    {
        internal const string UNITASK_SUPPORT_DEFINE = "YOKIFRAME_UNITASK_SUPPORT";
        internal const string YOOASSET_SUPPORT_DEFINE = "YOKIFRAME_YOOASSET_SUPPORT";
        internal const string LUBAN_SUPPORT_DEFINE = "YOKIFRAME_LUBAN_SUPPORT";
        internal const string ZSTRING_SUPPORT_DEFINE = "YOKIFRAME_ZSTRING_SUPPORT";
        internal const string DOTWEEN_SUPPORT_DEFINE = "YOKIFRAME_DOTWEEN_SUPPORT";
        internal const string NINO_SUPPORT_DEFINE = "YOKIFRAME_NINO_SUPPORT";
        internal const string INPUT_SYSTEM_SUPPORT_DEFINE = "YOKIFRAME_INPUTSYSTEM_SUPPORT";
        private const string OBSOLETE_FMOD_SUPPORT_DEFINE = "YOKIFRAME_FMOD_SUPPORT";

        /// <summary>
        /// 当前允许自动生成的七组可选依赖定义。
        /// </summary>
        internal static readonly DependencyDefinition[] Definitions =
        {
            new(UNITASK_SUPPORT_DEFINE, "com.cysharp.unitask", "UniTask", "UniTask.dll"),
            new(YOOASSET_SUPPORT_DEFINE, "com.tuyoogame.yooasset", "YooAsset", "YooAsset.dll"),
            new(LUBAN_SUPPORT_DEFINE, "com.code-philosophy.luban", "Luban.Runtime", "Luban.Runtime.dll"),
            new(ZSTRING_SUPPORT_DEFINE, "com.cysharp.zstring", "ZString", "ZString.dll"),
            new(DOTWEEN_SUPPORT_DEFINE, "com.demigiant.dotween", "DOTween.Modules", "DOTween.dll"),
            new(NINO_SUPPORT_DEFINE, "com.jasonxudeveloper.nino", "Nino.Core", "Nino.Core.dll"),
            new(INPUT_SYSTEM_SUPPORT_DEFINE, "com.unity.inputsystem", "Unity.InputSystem", "Unity.InputSystem.dll")
        };

        /// <summary>
        /// 判断宏是否由当前依赖服务负责维护。
        /// </summary>
        /// <param name="symbol">待检查的编译宏。</param>
        /// <returns>属于受管或废弃依赖宏时返回 true。</returns>
        internal static bool IsManagedSymbol(string symbol)
        {
            for (var index = 0; index < Definitions.Length; index++)
            {
                if (string.Equals(symbol, Definitions[index].DefineSymbol, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            // 旧版本可能已把此宏写入 PlayerSettings；将其视为受管项以便下次刷新移除。
            return string.Equals(symbol, OBSOLETE_FMOD_SUPPORT_DEFINE, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 描述一个可选依赖可被 package、asmdef 或预编译程序集命中的证据。
    /// </summary>
    internal sealed class DependencyDefinition
    {
        /// <summary>
        /// 创建单个可选依赖的检测定义。
        /// </summary>
        /// <param name="defineSymbol">依赖存在时启用的 YokiFrame 宏。</param>
        /// <param name="packageName">Unity Package Manager 中的包名。</param>
        /// <param name="assemblyDefinitionName">asmdef JSON 内的真实 name。</param>
        /// <param name="precompiledAssemblyName">预编译程序集文件名。</param>
        internal DependencyDefinition(
            string defineSymbol,
            string packageName,
            string assemblyDefinitionName,
            string precompiledAssemblyName)
        {
            DefineSymbol = defineSymbol;
            PackageName = packageName;
            AssemblyDefinitionName = assemblyDefinitionName;
            PrecompiledAssemblyName = precompiledAssemblyName;
        }

        /// <summary>
        /// 获取依赖存在时需要启用的 YokiFrame 宏。
        /// </summary>
        internal string DefineSymbol { get; }

        /// <summary>
        /// 获取 Unity Package Manager 中的包名证据。
        /// </summary>
        internal string PackageName { get; }

        /// <summary>
        /// 获取 asmdef JSON 内的真实程序集名称证据。
        /// </summary>
        internal string AssemblyDefinitionName { get; }

        /// <summary>
        /// 获取预编译程序集文件名证据。
        /// </summary>
        internal string PrecompiledAssemblyName { get; }

        /// <summary>
        /// 判断一次不可变 inventory 快照中是否存在当前依赖的任一可靠证据。
        /// </summary>
        /// <param name="snapshot">本次刷新统一采集的依赖快照。</param>
        /// <returns>存在 package、asmdef 或 DLL 证据时返回 true。</returns>
        internal bool IsDetected(DependencyInventorySnapshot snapshot)
        {
            return Contains(snapshot.PackageNames, PackageName) ||
                   Contains(snapshot.AssemblyDefinitionNames, AssemblyDefinitionName) ||
                   Contains(snapshot.PrecompiledAssemblyNames, PrecompiledAssemblyName);
        }

        /// <summary>
        /// 以忽略大小写的方式检查 Unity 依赖标识，兼容不同平台的文件名大小写差异。
        /// </summary>
        /// <param name="values">当前证据集合。</param>
        /// <param name="expected">期望的依赖标识。</param>
        /// <returns>集合包含期望标识时返回 true。</returns>
        private static bool Contains(string[] values, string expected)
        {
            if (string.IsNullOrEmpty(expected))
            {
                return false;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

#endif
