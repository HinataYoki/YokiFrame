using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// InputKit 的诊断快照构建逻辑。
    /// </summary>
    public static partial class InputKit
    {
        internal static InputKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            var actions = new List<InputActionDiagnosticsSnapshot>(sActionStates.Count);
            foreach (var pair in sActionStates)
            {
                actions.Add(new(pair.Value));
            }

            actions.Sort(CompareActionSnapshots);

            var enabledActionMaps = new List<string>(sEnabledActionMaps.Count);
            for (var i = 0; i < sEnabledActionMaps.Count; i++)
            {
                enabledActionMaps.Add(sEnabledActionMaps[i]);
            }

            var activeContexts = new List<InputContextDiagnosticsSnapshot>();
            var registeredContexts = new List<InputContextDiagnosticsSnapshot>();
            if (sContextStack != null)
            {
                sContextStack.CopyActiveContextDiagnostics(activeContexts);
                sContextStack.CopyRegisteredContextDiagnostics(registeredContexts);
            }

            var backendName = sBackend != null ? sBackend.BackendName : string.Empty;
            var bufferedInputCount = sInputBuffer != null ? sInputBuffer.Count : 0;
            return new(
                sBackend != null,
                backendName,
                sCurrentDeviceType,
                IsGamepadConnected,
                sCurrentTime,
                sBufferWindowMs,
                bufferedInputCount,
                enabledActionMaps,
                actions,
                activeContexts,
                registeredContexts);
        }

        private static int CompareActionSnapshots(InputActionDiagnosticsSnapshot left, InputActionDiagnosticsSnapshot right)
        {
            return string.Compare(left.ActionName, right.ActionName, StringComparison.Ordinal);
        }
    }
}
