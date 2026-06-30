using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 托管 C# 运行时能力门面。Core 只持有抽象，具体后端由宿主 Adapter 或可选包注册。
    /// </summary>
    public static class ManagedRuntimeKit
    {
        private const int MAX_BACKEND_ID_LENGTH = 128;

        private static readonly object sLock = new object();
        private static readonly Dictionary<string, IManagedRuntimeBackend> sBackends =
            new Dictionary<string, IManagedRuntimeBackend>(StringComparer.OrdinalIgnoreCase);

        private static IManagedRuntimeBackend sCurrentBackend;

        static ManagedRuntimeKit()
        {
            ResetForTests();
        }

        public static ManagedRuntimeInfo CurrentInfo
        {
            get
            {
                lock (sLock)
                    return EnsureCurrentBackend().GetInfo();
            }
        }

        public static string CurrentBackendId
        {
            get { return CurrentInfo.BackendId; }
        }

        public static void RegisterBackend(IManagedRuntimeBackend backend)
        {
            if (backend == null)
                return;

            if (!IsValidBackendId(backend.BackendId))
                throw new ArgumentException("Managed runtime backend id is invalid.", nameof(backend));

            lock (sLock)
            {
                sBackends[backend.BackendId] = backend;
                if (sCurrentBackend != null &&
                    string.Equals(sCurrentBackend.BackendId, backend.BackendId, StringComparison.OrdinalIgnoreCase))
                {
                    sCurrentBackend = backend;
                }
            }
        }

        public static bool IsBackendAvailable(string backendId)
        {
            lock (sLock)
            {
                IManagedRuntimeBackend backend;
                if (!TryGetBackendLocked(backendId, out backend))
                    return false;

                return backend.Availability == ManagedRuntimeAvailability.Available;
            }
        }

        public static ManagedRuntimeInfo[] GetBackendInfos()
        {
            lock (sLock)
            {
                EnsureCurrentBackend();
                var infos = new ManagedRuntimeInfo[sBackends.Count];
                var index = 0;
                foreach (var pair in sBackends)
                {
                    infos[index] = pair.Value.GetInfo();
                    index++;
                }

                return infos;
            }
        }

        public static ManagedRuntimeSelectionResult SelectBackend(string backendId)
        {
            if (!IsValidBackendId(backendId))
            {
                return ManagedRuntimeSelectionResult.Failure(
                    ManagedRuntimeSelectionStatus.InvalidBackendId,
                    backendId,
                    "Managed runtime backend id is invalid.");
            }

            lock (sLock)
            {
                IManagedRuntimeBackend backend;
                if (!TryGetBackendLocked(backendId, out backend))
                {
                    return ManagedRuntimeSelectionResult.Failure(
                        ManagedRuntimeSelectionStatus.NotInstalled,
                        backendId,
                        "Managed runtime backend is not installed.");
                }

                if (backend.Availability != ManagedRuntimeAvailability.Available)
                {
                    return ManagedRuntimeSelectionResult.Failure(
                        ManagedRuntimeSelectionStatus.Unavailable,
                        backend.BackendId,
                        "Managed runtime backend is not available.");
                }

                if (sCurrentBackend != null &&
                    string.Equals(sCurrentBackend.BackendId, backend.BackendId, StringComparison.OrdinalIgnoreCase))
                {
                    return ManagedRuntimeSelectionResult.SuccessResult(
                        ManagedRuntimeSelectionStatus.AlreadySelected,
                        backend.BackendId,
                        "Managed runtime backend is already selected.");
                }

                sCurrentBackend = backend;
                return ManagedRuntimeSelectionResult.SuccessResult(
                    ManagedRuntimeSelectionStatus.Selected,
                    backend.BackendId,
                    "Managed runtime backend selected.");
            }
        }

        public static ManagedRuntimeValidationResult ValidateCurrent()
        {
            lock (sLock)
                return EnsureCurrentBackend().Validate();
        }

        public static ManagedRuntimeActionDescriptor[] GetActions(string backendId)
        {
            IManagedRuntimeWorkflowBackend workflowBackend;
            if (!TryGetWorkflowBackend(backendId, out workflowBackend))
                return new ManagedRuntimeActionDescriptor[0];

            var actions = workflowBackend.GetActions();
            if (actions == null || actions.Length == 0)
                return new ManagedRuntimeActionDescriptor[0];

            var copy = new ManagedRuntimeActionDescriptor[actions.Length];
            Array.Copy(actions, copy, actions.Length);
            return copy;
        }

        public static ManagedRuntimeActionResult ExecuteAction(string backendId, string actionId, string payloadJson)
        {
            if (!IsValidBackendId(backendId))
            {
                return ManagedRuntimeActionResult.Failure(
                    backendId,
                    actionId,
                    "Managed runtime backend id is invalid.",
                    "InvalidBackendId");
            }

            if (!IsValidBackendId(actionId))
            {
                return ManagedRuntimeActionResult.Failure(
                    backendId,
                    actionId,
                    "Managed runtime action id is invalid.",
                    "InvalidActionId");
            }

            IManagedRuntimeBackend backend;
            lock (sLock)
            {
                if (!TryGetBackendLocked(backendId, out backend))
                {
                    return ManagedRuntimeActionResult.Failure(
                        backendId,
                        actionId,
                        "Managed runtime backend is not installed.",
                        "BackendNotInstalled");
                }
            }

            var workflowBackend = backend as IManagedRuntimeWorkflowBackend;
            if (workflowBackend == null)
            {
                return ManagedRuntimeActionResult.Failure(
                    backend.BackendId,
                    actionId,
                    "Managed runtime backend '" + backend.BackendId + "' does not support workflow actions.",
                    "WorkflowNotSupported");
            }

            return workflowBackend.ExecuteAction(actionId, string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
        }

        public static ManagedRuntimeActionResult GetBackendSettings(string backendId)
        {
            if (!IsValidBackendId(backendId))
            {
                return ManagedRuntimeActionResult.Failure(
                    backendId,
                    "get_backend_settings",
                    "Managed runtime backend id is invalid.",
                    "InvalidBackendId");
            }

            IManagedRuntimeSettingsBackend settingsBackend;
            var resolvedBackendId = backendId;
            if (!TryGetSettingsBackend(backendId, out settingsBackend, out resolvedBackendId))
            {
                return ManagedRuntimeActionResult.Failure(
                    resolvedBackendId,
                    "get_backend_settings",
                    "Managed runtime backend '" + resolvedBackendId + "' does not support settings.",
                    "SettingsNotSupported");
            }

            try
            {
                var settingsJson = settingsBackend.GetSettingsJson();
                return ManagedRuntimeActionResult.SuccessResult(
                    resolvedBackendId,
                    "get_backend_settings",
                    "Managed runtime backend settings loaded.",
                    string.IsNullOrEmpty(settingsJson) ? "{}" : settingsJson);
            }
            catch (Exception ex)
            {
                return ManagedRuntimeActionResult.Failure(
                    resolvedBackendId,
                    "get_backend_settings",
                    ex.Message,
                    "SettingsReadFailed");
            }
        }

        public static ManagedRuntimeActionResult SaveBackendSettings(string backendId, string payloadJson)
        {
            if (!IsValidBackendId(backendId))
            {
                return ManagedRuntimeActionResult.Failure(
                    backendId,
                    "save_backend_settings",
                    "Managed runtime backend id is invalid.",
                    "InvalidBackendId");
            }

            IManagedRuntimeSettingsBackend settingsBackend;
            var resolvedBackendId = backendId;
            if (!TryGetSettingsBackend(backendId, out settingsBackend, out resolvedBackendId))
            {
                return ManagedRuntimeActionResult.Failure(
                    resolvedBackendId,
                    "save_backend_settings",
                    "Managed runtime backend '" + resolvedBackendId + "' does not support settings.",
                    "SettingsNotSupported");
            }

            try
            {
                return settingsBackend.SaveSettings(string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
            }
            catch (Exception ex)
            {
                return ManagedRuntimeActionResult.Failure(
                    resolvedBackendId,
                    "save_backend_settings",
                    ex.Message,
                    "SettingsSaveFailed");
            }
        }

        internal static void ResetForTests()
        {
            lock (sLock)
            {
                sBackends.Clear();
                var defaultBackend = new DefaultManagedRuntimeBackend();
                sBackends[defaultBackend.BackendId] = defaultBackend;
                sCurrentBackend = defaultBackend;
            }
        }

        private static IManagedRuntimeBackend EnsureCurrentBackend()
        {
            if (sCurrentBackend != null)
                return sCurrentBackend;

            IManagedRuntimeBackend backend;
            if (TryGetBackendLocked(ManagedRuntimeBackendIds.Default, out backend))
            {
                sCurrentBackend = backend;
                return backend;
            }

            var defaultBackend = new DefaultManagedRuntimeBackend();
            sBackends[defaultBackend.BackendId] = defaultBackend;
            sCurrentBackend = defaultBackend;
            return defaultBackend;
        }

        private static bool TryGetBackendLocked(string backendId, out IManagedRuntimeBackend backend)
        {
            backend = null;
            if (!IsValidBackendId(backendId))
                return false;

            return sBackends.TryGetValue(backendId, out backend);
        }

        private static bool TryGetWorkflowBackend(string backendId, out IManagedRuntimeWorkflowBackend workflowBackend)
        {
            workflowBackend = null;
            if (!IsValidBackendId(backendId))
                return false;

            lock (sLock)
            {
                IManagedRuntimeBackend backend;
                if (!TryGetBackendLocked(backendId, out backend))
                    return false;

                workflowBackend = backend as IManagedRuntimeWorkflowBackend;
                return workflowBackend != null;
            }
        }

        private static bool TryGetSettingsBackend(
            string backendId,
            out IManagedRuntimeSettingsBackend settingsBackend,
            out string resolvedBackendId)
        {
            settingsBackend = null;
            resolvedBackendId = backendId ?? string.Empty;
            if (!IsValidBackendId(backendId))
                return false;

            lock (sLock)
            {
                IManagedRuntimeBackend backend;
                if (!TryGetBackendLocked(backendId, out backend))
                    return false;

                resolvedBackendId = backend.BackendId;
                settingsBackend = backend as IManagedRuntimeSettingsBackend;
                return settingsBackend != null;
            }
        }

        private static bool IsValidBackendId(string backendId)
        {
            if (string.IsNullOrEmpty(backendId) || backendId.Length > MAX_BACKEND_ID_LENGTH)
                return false;

            for (var i = 0; i < backendId.Length; i++)
            {
                var c = backendId[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-')
                {
                    continue;
                }

                return false;
            }

            return backendId != "." && backendId != "..";
        }

        private sealed class DefaultManagedRuntimeBackend : IManagedRuntimeBackend
        {
            public string BackendId
            {
                get { return ManagedRuntimeBackendIds.Default; }
            }

            public string DisplayName
            {
                get { return "Default Managed Runtime"; }
            }

            public ManagedRuntimeAvailability Availability
            {
                get { return ManagedRuntimeAvailability.Available; }
            }

            public ManagedRuntimeCapabilities Capabilities
            {
                get
                {
                    return ManagedRuntimeCapabilities.HostExecution |
                           ManagedRuntimeCapabilities.AssemblyInspection |
                           ManagedRuntimeCapabilities.Diagnostics;
                }
            }

            public ManagedRuntimeInfo GetInfo()
            {
                return new ManagedRuntimeInfo(
                    BackendId,
                    DisplayName,
                    "Host",
                    "Current",
                    "Host",
                    Availability,
                    Capabilities,
                    "Uses the managed runtime already provided by the current host.");
            }

            public ManagedRuntimeValidationResult Validate()
            {
                return ManagedRuntimeValidationResult.Valid(
                    GetInfo(),
                    new ManagedRuntimeDiagnostic(
                        ManagedRuntimeDiagnosticSeverity.Info,
                        "managedruntime.default.ready",
                        "Default managed runtime backend is available.",
                        null));
            }
        }
    }
}
