#if UNITY_EDITOR
using System.Text.RegularExpressions;

namespace YokiFrame
{
    /// <summary>集中维护真实 Player 产物禁止出现的工具类型、成员、参数与配置文本。</summary>
    public sealed partial class YokiFramePlayerBuildBoundaryTests
    {
        private static readonly Regex sTypeDeclarationPattern = new(
            @"^\s*(?:public|internal|private|protected)\s+(?:(?:sealed|static|abstract|readonly|partial)\s+)*(?:class|struct|interface|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant);

        private static readonly string[] sRequiredToolTypeNames =
        {
            "ActionKitDiagnosticHistory", "AudioHistoryEntry", "ArchitectureRegistry", "EventKitDiagnosticRegistry",
            "EasyEventEditorHook", "EventKitEditorNotification", "EventKitEditorNotificationKind", "EventKitEditorChannel",
            "IEngineLoggerWithStackTrace", "LogKitWorkbenchSnapshot", "YokiFrameCommandDispatcher",
            "YokiFrameEditorFileBridgePump", "YokiFrameKitInteractionRegistry",
            "SaveKitDiagnosticsSnapshot", "SaveKitDiagnosticsMeta", "SaveKitInteractionProvider",
            "SaveKitCommandHandler", "SaveKitSnapshotWriter", "SaveKitEditorInstaller",
            "UnitySaveKitEditorInstaller",
            "UnityEditorContextService", "UnityEditorContextInteractionProvider",
            "UIKitInteractionProvider", "UIKitCommandHandler", "UIKitSnapshotWriter",
            "UIKitBindScanner", "UIKitBindInspector", "UIKitPanelCodeGenerator",
            "UIKitPrefabBindingProcessor", "UIKitEditorContextWriter",
            "UIPanelValidator", "UILevelPropertyDrawer",
            "YokiFrameSharedMemoryTelemetryContract", "ArchitectureRealtimeTestBootstrap",
            "AudioKitWorkbenchStressController", "EventKitRuntimeSmokeController",
            "FsmKitRuntimeSmokeController", "PoolKitRuntimeStressController",
            "SpatialGizmoDiagnosticsFrame", "SpatialGizmoIndexSnapshot"
        };

        private static readonly string[] sSmokeSourceRelativePaths =
        {
            "Assets/Scripts/ArchitectureRealtimeSmoke/ArchitectureLifecycleTestArchitecture.cs",
            "Assets/Scripts/ArchitectureRealtimeSmoke/ArchitectureRealtimeTestBootstrap.cs",
            "Assets/Scripts/ArchitectureRealtimeSmoke/ArchitectureRegistrationTestArchitecture.cs",
            "Assets/Scripts/AudioKitWorkbenchStress/AudioKitWorkbenchStressController.cs",
            "Assets/Scripts/AudioKitWorkbenchStress/AudioKitWorkbenchStressController.Pressure.cs",
            "Assets/Scripts/EventKitRuntimeSmoke/EventKitRuntimeSmokeController.cs",
            "Assets/Scripts/FsmKitRuntimeSmoke/FsmKitRuntimeSmokeController.cs",
            "Assets/Scripts/PoolKitRuntimeStress/PoolKitRuntimeStressController.cs",
            "Assets/Scripts/PoolKitRuntimeStress/PoolKitRuntimeStressController.Pressure.cs",
            "Assets/Scripts/PoolKitRuntimeStress/PoolKitStressTokens.cs",
            "Assets/Scripts/SpatialKitWorkbenchStress/SpatialKitWorkbenchStressController.cs"
        };

        private static readonly string[] sEditorConfigurationTokens =
        {
            "editor-settings.json", "saveLogInEditor", "SaveLogInEditor", "editorFileName",
            "EditorFileName", "yoki_editor.log"
        };

        private static readonly string[] sForbiddenPlayerMemberNames =
        {
            "GetAllEvents", "GetDebugInfo", "GetListeners", "ListenerCount", "WithCallback", "mOnUnRegister",
            "WithEditorUnregisterNotification", "mUnregisterNotification"
        };

        private static readonly string[] sForbiddenPlayerParameterNames =
        {
            "monitorChannel", "sourceFile", "sourceLine"
        };

        private static readonly string[] sForbiddenPlayerAssemblyReferenceFragments =
        {
            ".Editor", ".Tests", "Workbench", "CommandBridge", "Telemetry"
        };

        private static readonly string[] sForbiddenPlayerConstructorParameters =
        {
            "ResCacheEntry:providerName", "ResPendingLoad:providerName"
        };

        private static readonly string[] sForbiddenPlayerQualifiedMemberNames =
        {
            "ActionController.mStackTraceRegistered",
            "ActionController.MarkStackTraceRegistered",
            "ActionController.TryClearStackTraceRegistered",
            "ActionKitScheduler.FrameCount",
            "ActionKitScheduler.FinishedCount",
            "ActionKitScheduler.CancelledCount",
            "ActionKitScheduler.FaultedCount",
            "ActionKitScheduler.ExecutingCount",
            "ActionKitScheduler.DiagnosticVersion",
            "ActionKitScheduler.GetExecutingActionControllers",
            "ActionKitScheduler.GetExecutingActions",
            "ActionKitScheduler.NotifyStateChanged",
            "UIKit.DiagnosticVersion", "UIKitController.DiagnosticVersion",
            "IFSM.Name", "IFSM.EnumType", "IFSM.CurrentState", "IFSM.CurrentStateId",
            "IFSM.GetAllStates", "IFSM.GetStateOrderIndex",
            "FSM.Name", "FSM.EnumType", "FSM.CurrentState", "FSM.CurrentStateId",
            "FSM.GetAllStates", "FSM.GetStateOrderIndex", "FSM.mStateOrder", "FSM.mName",
            "FSM.PublishStateAdded", "FSM.PublishStateRemoved", "FSM.PublishFsmStarted", "FSM.PublishStateChanged",
            "LogKit.DiagnosticVersion", "LogKit.LoggerName",
            "LogKit.GetHistory", "LogKit.ClearHistory", "LogKit.GetStats",
            "LogKitSettings.SettingsVersion", "LogKitSettings.BuildJson",
            "LogKitSettings.ApplyPayload", "LogKitSettings.ResetToDefaults",
            "ResKit.EnableLoadLocationTracking", "ResKit.DiagnosticVersion", "ResKit.LoadedCount",
            "ResKit.ProviderName",
            "ResKit.InFlightCount", "ResKit.TotalRefCount", "ResKit.UnloadHistoryCount",
            "ResKit.GetLoadedAssets", "ResKit.GetUnloadHistory", "ResKit.ClearUnloadHistory",
            "ResKit.CaptureDiagnosticSnapshot", "ResKit.CaptureLoadSource",
            "ResHandle.ProviderName", "ResHandle.RefCount", "IResourceProvider.ProviderName",
            "ResCacheEntry.ProviderName", "ResPendingLoad.ProviderName"
        };
    }
}
#endif
