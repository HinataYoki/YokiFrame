#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 监听 Unity 依赖环境变化，并把七组可选依赖同步为当前构建目标的 YokiFrame 宏。
    /// </summary>
    [InitializeOnLoad]
    public static class DependencyDefineService
    {
        /// <summary>
        /// UniTask 可选依赖编译宏。
        /// </summary>
        public const string UNITASK_SUPPORT_DEFINE = DependencyDefineCatalog.UNITASK_SUPPORT_DEFINE;

        /// <summary>
        /// YooAsset 可选依赖编译宏。
        /// </summary>
        public const string YOOASSET_SUPPORT_DEFINE = DependencyDefineCatalog.YOOASSET_SUPPORT_DEFINE;

        /// <summary>
        /// Luban 可选依赖编译宏。
        /// </summary>
        public const string LUBAN_SUPPORT_DEFINE = DependencyDefineCatalog.LUBAN_SUPPORT_DEFINE;

        /// <summary>
        /// ZString 可选依赖编译宏。
        /// </summary>
        public const string ZSTRING_SUPPORT_DEFINE = DependencyDefineCatalog.ZSTRING_SUPPORT_DEFINE;

        /// <summary>
        /// DOTween 可选依赖编译宏。
        /// </summary>
        public const string DOTWEEN_SUPPORT_DEFINE = DependencyDefineCatalog.DOTWEEN_SUPPORT_DEFINE;

        /// <summary>
        /// Nino 可选依赖编译宏。
        /// </summary>
        public const string NINO_SUPPORT_DEFINE = DependencyDefineCatalog.NINO_SUPPORT_DEFINE;

        /// <summary>
        /// Unity Input System 可选依赖编译宏。
        /// </summary>
        public const string INPUT_SYSTEM_SUPPORT_DEFINE = DependencyDefineCatalog.INPUT_SYSTEM_SUPPORT_DEFINE;

        private static readonly UnityDependencyInventoryProvider sInventoryProvider = new();
        private static readonly UnityDependencyDefineStore sDefineStore = new();
        private static readonly DependencyDefineRefreshCoordinator sRefreshCoordinator = new(
            sInventoryProvider.Capture,
            sDefineStore.ReadSymbols,
            sDefineStore.WriteSymbols,
            GetActiveBuildTarget);

        private static bool sRefreshScheduled;

        /// <summary>
        /// 订阅 package 与编译事件，并在 Editor 完成当前初始化后安排首次依赖刷新。
        /// </summary>
        static DependencyDefineService()
        {
            Events.registeredPackages += OnRegisteredPackages;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            ScheduleRefresh();
        }

        /// <summary>
        /// 获取 UniTask 可选依赖编译宏。
        /// </summary>
        public static string UniTaskSupportDefine => UNITASK_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 YooAsset 可选依赖编译宏。
        /// </summary>
        public static string YooAssetSupportDefine => YOOASSET_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 Luban 可选依赖编译宏。
        /// </summary>
        public static string LubanSupportDefine => LUBAN_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 ZString 可选依赖编译宏。
        /// </summary>
        public static string ZStringSupportDefine => ZSTRING_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 DOTween 可选依赖编译宏。
        /// </summary>
        public static string DOTweenSupportDefine => DOTWEEN_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 Nino 可选依赖编译宏。
        /// </summary>
        public static string NinoSupportDefine => NINO_SUPPORT_DEFINE;

        /// <summary>
        /// 获取 Unity Input System 可选依赖编译宏。
        /// </summary>
        public static string InputSystemSupportDefine => INPUT_SYSTEM_SUPPORT_DEFINE;

        /// <summary>
        /// 手动刷新当前 Unity 构建目标的 YokiFrame 可选依赖宏。
        /// </summary>
        [MenuItem("YokiFrame/Refresh Dependency Defines")]
        public static void RefreshDefines()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleRefresh();
                return;
            }

            ExecuteRefresh();
        }

        /// <summary>
        /// package 注册状态发生变化后安排一次去重刷新。
        /// </summary>
        /// <param name="_">本次 package 注册变化参数；刷新统一重新采集完整快照。</param>
        private static void OnRegisteredPackages(PackageRegistrationEventArgs _)
        {
            ScheduleRefresh();
        }

        /// <summary>
        /// Unity 完成程序集编译后重新确认预编译程序集与 asmdef 证据。
        /// </summary>
        /// <param name="_">Unity compilationFinished 提供的上下文。</param>
        private static void OnCompilationFinished(object _)
        {
            ScheduleRefresh();
        }

        /// <summary>
        /// 接收 AssetPostprocessor 的资源变化通知，只在依赖证据文件变化时安排刷新。
        /// </summary>
        /// <param name="assetGroups">导入、删除和移动路径分组。</param>
        internal static void NotifyAssetsChanged(params string[][] assetGroups)
        {
            for (var groupIndex = 0; groupIndex < assetGroups.Length; groupIndex++)
            {
                if (ContainsDependencyMarker(assetGroups[groupIndex]))
                {
                    ScheduleRefresh();
                    return;
                }
            }
        }

        /// <summary>
        /// 将同一帧内的多次依赖变化合并为一次 Editor delayCall。
        /// </summary>
        private static void ScheduleRefresh()
        {
            if (sRefreshScheduled)
            {
                return;
            }

            sRefreshScheduled = true;
            EditorApplication.delayCall += RefreshWhenEditorIsReady;
        }

        /// <summary>
        /// 等待 Unity 完成编译或资源更新后再刷新，避免阻塞主线程和竞争 PlayerSettings。
        /// </summary>
        private static void RefreshWhenEditorIsReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RefreshWhenEditorIsReady;
                return;
            }

            sRefreshScheduled = false;
            ExecuteRefresh();
        }

        /// <summary>
        /// 执行协调器并把失败或实际写入结果输出到 Unity Console。
        /// </summary>
        private static void ExecuteRefresh()
        {
            var result = sRefreshCoordinator.Refresh();
            if (!result.Succeeded)
            {
                Debug.LogError(
                    "[YokiFrame][DependencyDefineService][target="
                    + result.BuildTarget
                    + "] "
                    + result.ErrorMessage);
                return;
            }

            if (result.Changed)
            {
                Debug.Log(CreateRefreshSummary(result));
            }

            for (var index = 0; index < result.InventoryDiagnostics.Length; index++)
            {
                Debug.LogWarning(
                    "[YokiFrame][DependencyDefineService][target="
                    + result.BuildTarget
                    + "] "
                    + result.InventoryDiagnostics[index]);
            }
        }

        /// <summary>
        /// 读取当前 Unity 构建目标，用于把宏变化与实际 PlayerSettings 写入平台关联。
        /// </summary>
        /// <returns>Unity 当前 activeBuildTarget 的稳定名称。</returns>
        private static string GetActiveBuildTarget()
        {
            return EditorUserBuildSettings.activeBuildTarget.ToString();
        }

        /// <summary>
        /// 根据保留的宏规划和 inventory 生成单条可审计 Console 摘要。
        /// </summary>
        /// <param name="result">已经成功完成的刷新结果。</param>
        /// <returns>包含目标平台、宏差异和原始依赖证据的日志文本。</returns>
        private static string CreateRefreshSummary(DependencyDefineRefreshResult result)
        {
            var plan = result.Plan;
            return "[YokiFrame][DependencyDefineService][target="
                + result.BuildTarget
                + "] 依赖宏已刷新 +["
                + string.Join(", ", plan.AddedSymbols)
                + "] -["
                + string.Join(", ", plan.RemovedSymbols)
                + "] packages=["
                + string.Join(", ", result.Snapshot.PackageNames)
                + "] asmdefs=["
                + string.Join(", ", result.Snapshot.AssemblyDefinitionNames)
                + "] dlls=["
                + string.Join(", ", result.Snapshot.PrecompiledAssemblyNames)
                + "]";
        }

        /// <summary>
        /// 判断一组资源路径中是否包含会改变依赖 inventory 的文件。
        /// </summary>
        /// <param name="assetPaths">AssetPostprocessor 提供的资源路径。</param>
        /// <returns>存在 asmdef、asmref 或 DLL 时返回 true。</returns>
        private static bool ContainsDependencyMarker(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            for (var index = 0; index < assetPaths.Length; index++)
            {
                var extension = Path.GetExtension(assetPaths[index]);
                if (string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 把 Unity 资源导入、删除和移动事件转交给依赖宏服务。
    /// </summary>
    internal sealed class DependencyDefineAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Unity 完成一批资源变更后，仅转发可能影响依赖检测的路径集合。
        /// </summary>
        /// <param name="importedAssets">本批导入资源。</param>
        /// <param name="deletedAssets">本批删除资源。</param>
        /// <param name="movedAssets">本批移动后的资源。</param>
        /// <param name="movedFromAssetPaths">本批移动前的资源。</param>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            DependencyDefineService.NotifyAssetsChanged(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}

#endif
