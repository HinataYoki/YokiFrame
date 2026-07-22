#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.Build;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 封装当前 Unity 构建目标的 PlayerSettings 编译宏读取与写入。
    /// </summary>
    internal sealed class UnityDependencyDefineStore
    {
        /// <summary>
        /// 读取当前有效构建目标的全部编译宏。
        /// </summary>
        /// <returns>PlayerSettings 当前宏数组。</returns>
        internal string[] ReadSymbols()
        {
            var target = ResolveNamedBuildTarget();
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            return symbols ?? Array.Empty<string>();
        }

        /// <summary>
        /// 将规范化后的完整宏数组写回当前有效构建目标。
        /// </summary>
        /// <param name="symbols">需要完整覆盖写入的目标宏。</param>
        internal void WriteSymbols(string[] symbols)
        {
            var target = ResolveNamedBuildTarget();
            PlayerSettings.SetScriptingDefineSymbols(target, symbols);
            // Unity 6 Alpha 可能只更新当前 Editor 内存态；立即保存可避免重启后宏回退。
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 优先使用用户选择的构建目标组，无法使用时回落到当前活动目标组。
        /// </summary>
        /// <returns>可供 PlayerSettings 新 API 使用的 NamedBuildTarget。</returns>
        /// <exception cref="InvalidOperationException">选择和活动目标组均无效时抛出。</exception>
        private static NamedBuildTarget ResolveNamedBuildTarget()
        {
            var selectedGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (selectedGroup != BuildTargetGroup.Unknown)
            {
                return NamedBuildTarget.FromBuildTargetGroup(selectedGroup);
            }

            var activeGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            if (activeGroup != BuildTargetGroup.Unknown)
            {
                return NamedBuildTarget.FromBuildTargetGroup(activeGroup);
            }

            throw new InvalidOperationException("无法解析当前 Unity 构建目标，依赖宏未修改。");
        }
    }
}

#endif
