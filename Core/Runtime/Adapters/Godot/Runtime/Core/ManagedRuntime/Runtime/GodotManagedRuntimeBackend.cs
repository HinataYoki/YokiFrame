#if GODOT
using System;
using Godot;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot .NET 后端占位实现。它暴露与 Unity LeanCLR 相同的工作台动作槽位，
    /// 但不会把 Unity IL2CPP 替换流程套用到 Godot。
    /// </summary>
    public sealed class GodotDotNetManagedRuntimeBackend : IManagedRuntimeBackend, IManagedRuntimeWorkflowBackend
    {
        public string BackendId
        {
            get { return ManagedRuntimeBackendIds.GodotDotNet; }
        }

        public string DisplayName
        {
            get { return "Godot .NET"; }
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
                "Godot",
                GetGodotVersion(),
                ".NET Host",
                Availability,
                Capabilities,
                "Godot .NET backend is available. LeanCLR-style AOT/build actions require a Godot-specific toolchain backend.");
        }

        public ManagedRuntimeValidationResult Validate()
        {
            return ManagedRuntimeValidationResult.Valid(
                GetInfo(),
                new ManagedRuntimeDiagnostic(
                    ManagedRuntimeDiagnosticSeverity.Info,
                    "managedruntime.godot_dotnet.ready",
                    "Godot .NET backend is available.",
                    "LeanCLR IL2CPP replacement actions are Unity-specific until a Godot backend implements them."));
        }

        public ManagedRuntimeActionDescriptor[] GetActions()
        {
            return new[]
            {
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.EnableBackend,
                    "Enable Godot .NET",
                    "Select Godot .NET as the current managed runtime backend.",
                    true,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.InstallLocalIl2Cpp,
                    "Install Local IL2CPP",
                    "Not available for Godot. Godot needs a dedicated managed runtime backend instead of Unity IL2CPP replacement files.",
                    false,
                    true,
                    true),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.GenerateAotRules,
                    "Generate AOT Rules",
                    "Not available until a Godot-specific AOT rule generator is installed.",
                    false,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.CompileDll,
                    "Compile DLL",
                    "Not available until a Godot-specific managed assembly build backend is installed.",
                    false,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Build Player",
                    "Not available until a Godot export backend is installed.",
                    false,
                    true,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.RunSetupPipeline,
                    "Run Setup Pipeline",
                    "Not available until a Godot-specific managed runtime setup pipeline is installed.",
                    false,
                    true,
                    true)
            };
        }

        public ManagedRuntimeActionResult ExecuteAction(string actionId, string payloadJson)
        {
            if (actionId == ManagedRuntimeActionIds.EnableBackend)
            {
                var selection = ManagedRuntimeKit.SelectBackend(BackendId);
                return ManagedRuntimeActionResult.SuccessResult(
                    BackendId,
                    actionId,
                    selection.Message,
                    "{\"selected\":" + (selection.Success ? "true" : "false") + "}");
            }

            return ManagedRuntimeActionResult.Failure(
                BackendId,
                actionId,
                "This ManagedRuntimeKit action is not available for Godot yet. Install or implement a Godot-specific backend to enable it.",
                "GodotManagedRuntimeActionUnavailable");
        }

        private static string GetGodotVersion()
        {
            try
            {
                var versionInfo = Engine.GetVersionInfo();
                if (versionInfo.TryGetValue("string", out var version))
                    return version.ToString();
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }
    }

    public static class GodotManagedRuntimeBackendRegistration
    {
        public static void EnsureRegistered()
        {
            ManagedRuntimeKit.RegisterBackend(new GodotDotNetManagedRuntimeBackend());
        }
    }
}
#endif
