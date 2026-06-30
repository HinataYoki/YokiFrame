#if !GODOT
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace YokiFrame.Unity
{
    /// <summary>
    /// LeanCLR Unity 包的 ManagedRuntimeKit 后端探测器。只依赖 Unity PackageManager，不直接引用 LeanCLR API。
    /// </summary>
    public sealed class UnityLeanClrManagedRuntimeBackend : IManagedRuntimeBackend, IManagedRuntimeWorkflowBackend, IManagedRuntimeSettingsBackend
    {
        public const string PackageName = "com.code-philosophy.leanclr";
        public const string GitUrl = "https://github.com/focus-creative-games/leanclr-unity.git";
        private const string DefaultAotRulePath = "ProjectSettings/YokiFrame/ManagedRuntime/LeanCLR/aot.xml";
        private const string LeanClrEditorAssemblyName = "LeanCLR.Editor";
        private static readonly string[] KnownPlayerTestAssemblyNames =
        {
            "Unity.Collections.Tests.CoreCLR.InternalJobNestedPrivate",
            "Unity.Collections.Tests.CoreCLR.PrivateJobNested",
            "Unity.Collections.Tests.CoreCLR.ProtectedJobNested",
            "Unity.Collections.Tests.CoreCLR.PublicJobPrivateGeneric"
        };

        public static bool IsLeanClrPackageInstalled
        {
            get
            {
                PackageManagerPackageInfo packageInfo;
                return TryGetPackageInfo(out packageInfo);
            }
        }

        public string BackendId
        {
            get { return ManagedRuntimeBackendIds.LeanClr; }
        }

        public string DisplayName
        {
            get { return "LeanCLR for Unity"; }
        }

        public ManagedRuntimeAvailability Availability
        {
            get { return IsLeanClrPackageInstalled ? ManagedRuntimeAvailability.Available : ManagedRuntimeAvailability.NotInstalled; }
        }

        public ManagedRuntimeCapabilities Capabilities
        {
            get
            {
                return ManagedRuntimeCapabilities.HostExecution |
                       ManagedRuntimeCapabilities.AssemblyInspection |
                       ManagedRuntimeCapabilities.DynamicAssemblyLoad |
                       ManagedRuntimeCapabilities.AotCompilation |
                       ManagedRuntimeCapabilities.Interpreter |
                       ManagedRuntimeCapabilities.HotUpdateAssembly |
                       ManagedRuntimeCapabilities.BuildPipelineControl |
                       ManagedRuntimeCapabilities.Diagnostics;
            }
        }

        public ManagedRuntimeInfo GetInfo()
        {
            PackageManagerPackageInfo packageInfo;
            var installed = TryGetPackageInfo(out packageInfo);
            var description = installed
                ? BuildInstalledDescription(packageInfo)
                : "LeanCLR Unity package is not installed. Add " + GitUrl + " to Packages/manifest.json.";

            return new ManagedRuntimeInfo(
                BackendId,
                DisplayName,
                "Unity",
                EditorUserBuildSettings.activeBuildTarget.ToString(),
                "AOT + Interpreter",
                installed ? ManagedRuntimeAvailability.Available : ManagedRuntimeAvailability.NotInstalled,
                Capabilities,
                description);
        }

        public ManagedRuntimeValidationResult Validate()
        {
            PackageManagerPackageInfo packageInfo;
            var installed = TryGetPackageInfo(out packageInfo);
            ManagedRuntimeInfo info = GetInfo();
            if (installed)
            {
                return ManagedRuntimeValidationResult.Valid(
                    info,
                    new ManagedRuntimeDiagnostic(
                        ManagedRuntimeDiagnosticSeverity.Info,
                        "managedruntime.leanclr.package_detected",
                        "LeanCLR Unity package is installed.",
                        BuildInstalledDescription(packageInfo)));
            }

            return ManagedRuntimeValidationResult.Invalid(
                info,
                new ManagedRuntimeDiagnostic(
                    ManagedRuntimeDiagnosticSeverity.Warning,
                    "managedruntime.leanclr.not_installed",
                    "LeanCLR Unity package is not installed.",
                    "Install " + PackageName + " from " + GitUrl + "."));
        }

        public ManagedRuntimeActionDescriptor[] GetActions()
        {
            var supported = IsLeanClrPackageInstalled;
            return new[]
            {
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.EnableBackend,
                    "Enable LeanCLR",
                    "Enable LeanCLR in project settings and select it as the current managed runtime backend.",
                    supported,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.InstallLocalIl2Cpp,
                    "Install Local IL2CPP",
                    "Install LeanCLR local IL2CPP replacement files under Library/LeanCLR.",
                    supported,
                    true,
                    true),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.GenerateAotRules,
                    "Generate AOT Rules",
                    "Create the default YokiFrame LeanAOT rule file and add it to LeanCLR settings.",
                    supported,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.CompileDll,
                    "Compile DLL",
                    "Run LeanCLR CompileDll for the active Unity build target.",
                    supported,
                    false,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Build Player",
                    "Build the active Unity target with LeanCLR enabled. Requires outputPath in the payload.",
                    supported,
                    true,
                    false),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.RunSetupPipeline,
                    "Run Setup Pipeline",
                    "Enable LeanCLR, generate the default AOT rule file, then install local IL2CPP replacement files.",
                    supported,
                    true,
                    true),
                new ManagedRuntimeActionDescriptor(
                    ManagedRuntimeActionIds.OpenBackendSettings,
                    "Open LeanCLR Settings",
                    "Open Unity Project Settings > LeanCLR for the original LeanCLR settings panel.",
                    supported,
                    false,
                    false)
            };
        }

        public ManagedRuntimeActionResult ExecuteAction(string actionId, string payloadJson)
        {
            if (!IsLeanClrPackageInstalled)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    actionId,
                    "LeanCLR Unity package is not installed.",
                    "LeanClrNotInstalled");
            }

            try
            {
                switch (actionId)
                {
                    case ManagedRuntimeActionIds.EnableBackend:
                        return EnableLeanClr(payloadJson);
                    case ManagedRuntimeActionIds.InstallLocalIl2Cpp:
                        return InstallLocalIl2Cpp(payloadJson);
                    case ManagedRuntimeActionIds.GenerateAotRules:
                        return GenerateAotRules(payloadJson);
                    case ManagedRuntimeActionIds.CompileDll:
                        return CompileDll(payloadJson);
                    case ManagedRuntimeActionIds.BuildPlayer:
                        return BuildPlayer(payloadJson);
                    case ManagedRuntimeActionIds.RunSetupPipeline:
                        return RunSetupPipeline(payloadJson);
                    case ManagedRuntimeActionIds.OpenBackendSettings:
                        return OpenBackendSettings(payloadJson);
                    default:
                        return ManagedRuntimeActionResult.Failure(
                            BackendId,
                            actionId,
                            "Unknown LeanCLR managed runtime action.",
                            "UnknownAction");
                }
            }
            catch (Exception ex)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    actionId,
                    ex.Message,
                    "LeanClrActionFailed");
            }
        }

        public string GetSettingsJson()
        {
            if (!IsLeanClrPackageInstalled)
                return BuildUnsupportedSettingsJson("LeanCLR Unity package is not installed.");

            try
            {
                EnsureLeanClrSettingsInitialized();
                return BuildLeanClrSettingsJson();
            }
            catch (Exception ex)
            {
                return BuildUnsupportedSettingsJson(ex.Message);
            }
        }

        public ManagedRuntimeActionResult SaveSettings(string payloadJson)
        {
            if (!IsLeanClrPackageInstalled)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    "save_backend_settings",
                    "LeanCLR Unity package is not installed.",
                    "LeanClrNotInstalled");
            }

            try
            {
                EnsureLeanClrSettingsInitialized();
                ApplyLeanClrSettings(payloadJson);
                SaveLeanClrSettings();
                return ManagedRuntimeActionResult.SuccessResult(
                    BackendId,
                    "save_backend_settings",
                    "LeanCLR settings saved.",
                    BuildLeanClrSettingsJson());
            }
            catch (Exception ex)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    "save_backend_settings",
                    ex.Message,
                    "LeanClrSettingsSaveFailed");
            }
        }

        private ManagedRuntimeActionResult EnableLeanClr(string payloadJson)
        {
            EnsureLeanClrSettingsInitialized();
            SetLeanClrEnable(true);
            SaveLeanClrSettings();
            var selection = ManagedRuntimeKit.SelectBackend(BackendId);
            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.EnableBackend,
                selection.Message,
                "{\"selected\":" + (selection.Success ? "true" : "false") + "}");
        }

        private ManagedRuntimeActionResult InstallLocalIl2Cpp(string payloadJson)
        {
            if (!IsConfirmed(payloadJson))
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.InstallLocalIl2Cpp,
                    "Installing local IL2CPP replacement files requires confirmation.",
                    "ConfirmationRequired");
            }

            EnsureLeanClrSettingsInitialized();
            InvokeLocalInstallerInstallLocal();
            var localIl2CppPath = GetLeanClrStaticStringProperty("LeanCLR.Settings", "LocalIl2CppPath");
            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.InstallLocalIl2Cpp,
                "LeanCLR local IL2CPP replacement files installed.",
                "{\"localIl2CppPath\":\"" + JsonHelper.EscapeString(localIl2CppPath) + "\"}");
        }

        private ManagedRuntimeActionResult GenerateAotRules(string payloadJson)
        {
            EnsureLeanClrSettingsInitialized();
            var rulePath = JsonHelper.ExtractString(payloadJson, "rulePath");
            if (string.IsNullOrEmpty(rulePath))
                rulePath = DefaultAotRulePath;

            var normalizedRulePath = NormalizeProjectRelativePath(rulePath);
            var absoluteRulePath = Path.GetFullPath(normalizedRulePath);
            var directory = Path.GetDirectoryName(absoluteRulePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(absoluteRulePath))
                File.WriteAllText(absoluteRulePath, BuildDefaultAotRuleXml(), new UTF8Encoding(false));

            EnsureAotRuleFileRegistered(normalizedRulePath);
            SaveLeanClrSettings();
            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.GenerateAotRules,
                "LeanCLR AOT rule file is ready.",
                "{\"rulePath\":\"" + JsonHelper.EscapeString(normalizedRulePath.Replace('\\', '/')) + "\"}");
        }

        private ManagedRuntimeActionResult CompileDll(string payloadJson)
        {
            EnsureLeanClrSettingsInitialized();
            InvokeStaticVoid("LeanCLR.Commands.CompileDllCommand", "CompileDllActiveTarget");
            var outputPath = GetLeanClrCompileDllOutputPath();
            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.CompileDll,
                "LeanCLR CompileDll finished for the active build target.",
                "{\"outputPath\":\"" + JsonHelper.EscapeString(outputPath) + "\"}");
        }

        private ManagedRuntimeActionResult BuildPlayer(string payloadJson)
        {
            if (!IsConfirmed(payloadJson))
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Building a Unity player requires confirmation.",
                    "ConfirmationRequired");
            }

            var outputPath = JsonHelper.ExtractString(payloadJson, "outputPath");
            if (string.IsNullOrEmpty(outputPath))
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Missing 'outputPath' in build payload.",
                    "MissingOutputPath");
            }

            var excludedPackageTestAssemblies = EnsureKnownPlayerTestAssembliesExcludedForPlayerBuild();
            if (excludedPackageTestAssemblies > 0 && EditorApplication.isCompiling)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Known package test assemblies were excluded. Wait for Unity compilation to finish, then run Build Player again.",
                    "RecompileRequiredAfterTestAssemblyExclusion");
            }

            EnsureLeanClrSettingsInitialized();
            SetLeanClrEnable(true);
            SaveLeanClrSettings();

            var scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.BuildPlayer,
                    "No enabled scenes were found in EditorBuildSettings.",
                    "NoEnabledScenes");
            }

            var absoluteOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
            options = SanitizePlayerBuildOptions(options);

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutputPath,
                target = EditorUserBuildSettings.activeBuildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            var result = report != null ? report.summary.result : BuildResult.Unknown;
            var success = result == BuildResult.Succeeded;
            var dataJson = "{\"outputPath\":\"" + JsonHelper.EscapeString(absoluteOutputPath.Replace('\\', '/')) +
                           "\",\"result\":\"" + JsonHelper.EscapeString(result.ToString()) +
                           "\",\"testAssembliesExcluded\":true,\"patchedPackageTestAssemblies\":" +
                           excludedPackageTestAssemblies.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

            if (success)
            {
                return ManagedRuntimeActionResult.SuccessResult(
                    BackendId,
                    ManagedRuntimeActionIds.BuildPlayer,
                    "Unity player build finished with LeanCLR enabled.",
                    dataJson);
            }

            return ManagedRuntimeActionResult.Failure(
                BackendId,
                ManagedRuntimeActionIds.BuildPlayer,
                "Unity player build finished with result " + result + ".",
                "BuildFailed");
        }

        private ManagedRuntimeActionResult RunSetupPipeline(string payloadJson)
        {
            if (!IsConfirmed(payloadJson))
            {
                return ManagedRuntimeActionResult.Failure(
                    BackendId,
                    ManagedRuntimeActionIds.RunSetupPipeline,
                    "Running the LeanCLR setup pipeline requires confirmation.",
                    "ConfirmationRequired");
            }

            var enable = EnableLeanClr(payloadJson);
            if (!enable.Success)
                return enable;

            var rules = GenerateAotRules(payloadJson);
            if (!rules.Success)
                return rules;

            var install = InstallLocalIl2Cpp(payloadJson);
            if (!install.Success)
                return install;

            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.RunSetupPipeline,
                "LeanCLR setup pipeline finished.",
                "{\"enabled\":true,\"aotRules\":true,\"localIl2Cpp\":true}");
        }

        private ManagedRuntimeActionResult OpenBackendSettings(string payloadJson)
        {
            SettingsService.OpenProjectSettings("Project/LeanCLR");
            return ManagedRuntimeActionResult.SuccessResult(
                BackendId,
                ManagedRuntimeActionIds.OpenBackendSettings,
                "Unity Project Settings > LeanCLR opened.",
                "{\"settingsPath\":\"Project/LeanCLR\"}");
        }

        private static bool TryGetPackageInfo(out PackageManagerPackageInfo packageInfo)
        {
            packageInfo = null;
            try
            {
                packageInfo = PackageManagerPackageInfo.FindForPackageName(PackageName);
                return packageInfo != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string BuildInstalledDescription(PackageManagerPackageInfo packageInfo)
        {
            if (packageInfo == null)
                return string.Empty;

            var version = string.IsNullOrEmpty(packageInfo.version) ? "unknown" : packageInfo.version;
            var source = packageInfo.source.ToString();
            return PackageName + " " + version + " (" + source + ")";
        }

        private static bool IsConfirmed(string payloadJson)
        {
            bool confirmed;
            return JsonHelper.TryExtractBool(payloadJson, "confirmed", out confirmed) && confirmed;
        }

        private static string[] GetEnabledScenePaths()
        {
            var scenes = new List<string>();
            var editorScenes = EditorBuildSettings.scenes;
            if (editorScenes != null)
            {
                for (var i = 0; i < editorScenes.Length; i++)
                {
                    var scene = editorScenes[i];
                    if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                        scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        private static BuildOptions SanitizePlayerBuildOptions(BuildOptions options)
        {
            return options & ~BuildOptions.IncludeTestAssemblies;
        }

        private static bool IsKnownPlayerTestAssemblyName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            for (var i = 0; i < KnownPlayerTestAssemblyNames.Length; i++)
            {
                if (string.Equals(assemblyName, KnownPlayerTestAssemblyNames[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int EnsureKnownPlayerTestAssembliesExcludedForPlayerBuild()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var patched = 0;
            patched += PatchKnownPlayerTestAssemblyDefinitions(Path.Combine(projectRoot, "Library", "PackageCache"));
            patched += PatchKnownPlayerTestAssemblyDefinitions(Path.Combine(projectRoot, "Packages"));

            if (patched > 0)
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            return patched;
        }

        private static int PatchKnownPlayerTestAssemblyDefinitions(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
                return 0;

            string[] asmdefPaths;
            try
            {
                asmdefPaths = Directory.GetFiles(rootDirectory, "*.asmdef", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return 0;
            }

            var patched = 0;
            for (var i = 0; i < asmdefPaths.Length; i++)
            {
                var path = asmdefPaths[i];
                string json;
                try
                {
                    json = File.ReadAllText(path);
                }
                catch (Exception)
                {
                    continue;
                }

                var assemblyName = JsonHelper.ExtractString(json, "name");
                if (!IsKnownPlayerTestAssemblyName(assemblyName))
                    continue;

                var patchedJson = BuildPlayerExcludedAsmdefJson(json);
                if (string.Equals(json, patchedJson, StringComparison.Ordinal))
                    continue;

                File.WriteAllText(path, patchedJson, new UTF8Encoding(false));
                patched++;
            }

            return patched;
        }

        private static string BuildPlayerExcludedAsmdefJson(string json)
        {
            var patchedJson = DisableAsmdefAutoReferenced(json);
            patchedJson = EnsureAsmdefIncludePlatformsEditorOnly(patchedJson);
            return patchedJson;
        }

        private static string DisableAsmdefAutoReferenced(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            const string PropertyName = "\"autoReferenced\"";
            var propertyIndex = json.IndexOf(PropertyName, StringComparison.Ordinal);
            if (propertyIndex < 0)
                return json;

            var colonIndex = json.IndexOf(':', propertyIndex + PropertyName.Length);
            if (colonIndex < 0)
                return json;

            var valueIndex = colonIndex + 1;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex]))
                valueIndex++;

            const string TrueValue = "true";
            if (valueIndex + TrueValue.Length > json.Length)
                return json;

            if (!string.Equals(json.Substring(valueIndex, TrueValue.Length), TrueValue, StringComparison.OrdinalIgnoreCase))
                return json;

            return json.Substring(0, valueIndex) + "false" + json.Substring(valueIndex + TrueValue.Length);
        }

        private static string EnsureAsmdefIncludePlatformsEditorOnly(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            const string PropertyName = "\"includePlatforms\"";
            var propertyIndex = json.IndexOf(PropertyName, StringComparison.Ordinal);
            if (propertyIndex < 0)
                return json;

            var colonIndex = json.IndexOf(':', propertyIndex + PropertyName.Length);
            if (colonIndex < 0)
                return json;

            var arrayStart = json.IndexOf('[', colonIndex + 1);
            if (arrayStart < 0)
                return json;

            var arrayEnd = FindJsonArrayEnd(json, arrayStart);
            if (arrayEnd < 0)
                return json;

            var arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            if (arrayContent.IndexOf("\"Editor\"", StringComparison.OrdinalIgnoreCase) >= 0)
                return json;

            return json.Substring(0, arrayStart + 1) +
                   "\n        \"Editor\"\n    " +
                   json.Substring(arrayEnd);
        }

        private static int FindJsonArrayEnd(string json, int arrayStart)
        {
            var inString = false;
            var escaped = false;
            for (var i = arrayStart + 1; i < json.Length; i++)
            {
                var c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = inString;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString && c == ']')
                    return i;
            }

            return -1;
        }

        private static string BuildDefaultAotRuleXml()
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                   "<aot>\n" +
                   "  <!-- YokiFrame default LeanAOT rule file. Add assembly/type/method rules here when you want selective AOT. -->\n" +
                   "</aot>\n";
        }

        private static string BuildLeanClrSettingsJson()
        {
            var settings = GetLeanClrSettingsInstance();
            var settingsType = settings.GetType();
            var aotSettings = EnsureObjectField(settings, settingsType, "leanAOTSettings", "LeanCLR.LeanAOTSettings");
            var hotUpdateSettings = EnsureObjectField(settings, settingsType, "hotUpdateSettings", "LeanCLR.HotUpdateSettings");
            var gcSettings = EnsureObjectField(settings, settingsType, "gcSettings", "LeanCLR.GCSettings");

            var sb = new StringBuilder(640);
            sb.Append("{\"supported\":true,\"backendId\":\"");
            sb.Append(JsonHelper.EscapeString(ManagedRuntimeBackendIds.LeanClr));
            sb.Append("\",\"source\":\"");
            sb.Append(JsonHelper.EscapeString(GetLeanClrStaticStringProperty("LeanCLR.Settings", "SettingsPath")));
            sb.Append("\",\"settingsPath\":\"Project/LeanCLR\",\"installRootDir\":\"");
            sb.Append(JsonHelper.EscapeString(GetLeanClrStaticStringProperty("LeanCLR.Settings", "InstallRootDir")));
            sb.Append("\",\"localIl2CppPath\":\"");
            sb.Append(JsonHelper.EscapeString(GetLeanClrStaticStringProperty("LeanCLR.Settings", "LocalIl2CppPath")));
            sb.Append("\",\"canOpenNativePanel\":true,\"enable\":");
            sb.Append(GetBoolField(settings, settingsType, "enable", false) ? "true" : "false");
            sb.Append(",\"layoutValidation\":");
            sb.Append(GetBoolField(aotSettings, aotSettings.GetType(), "layoutValidation", false) ? "true" : "false");
            sb.Append(",\"enablePgoProfile\":");
            sb.Append(GetBoolField(aotSettings, aotSettings.GetType(), "enablePgoProfile", false) ? "true" : "false");
            sb.Append(",\"ruleFiles\":");
            AppendStringArray(sb, GetStringArrayField(aotSettings, aotSettings.GetType(), "ruleFiles"));
            sb.Append(",\"pgoRuleFiles\":");
            AppendStringArray(sb, GetStringArrayField(aotSettings, aotSettings.GetType(), "pgoRuleFiles"));
            sb.Append(",\"lazyLoadedAssemblyNames\":");
            AppendStringArray(sb, GetStringArrayField(aotSettings, aotSettings.GetType(), "lazyLoadedAssemblyNames"));
            sb.Append(",\"hotUpdateAssemblyNames\":");
            AppendStringArray(sb, GetStringArrayField(hotUpdateSettings, hotUpdateSettings.GetType(), "hotUpdateAssemblyNames"));
            sb.Append(",\"gcMode\":\"");
            sb.Append(JsonHelper.EscapeString(GetEnumField(gcSettings, gcSettings.GetType(), "mode")));
            sb.Append("\",\"enableGCDebug\":");
            sb.Append(GetBoolField(gcSettings, gcSettings.GetType(), "enableGCDebug", false) ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildUnsupportedSettingsJson(string message)
        {
            return "{\"supported\":false,\"backendId\":\"" +
                   JsonHelper.EscapeString(ManagedRuntimeBackendIds.LeanClr) +
                   "\",\"canOpenNativePanel\":false,\"message\":\"" +
                   JsonHelper.EscapeString(message) +
                   "\"}";
        }

        private static void ApplyLeanClrSettings(string payloadJson)
        {
            var settings = GetLeanClrSettingsInstance();
            var settingsType = settings.GetType();
            var aotSettings = EnsureObjectField(settings, settingsType, "leanAOTSettings", "LeanCLR.LeanAOTSettings");
            var hotUpdateSettings = EnsureObjectField(settings, settingsType, "hotUpdateSettings", "LeanCLR.HotUpdateSettings");
            var gcSettings = EnsureObjectField(settings, settingsType, "gcSettings", "LeanCLR.GCSettings");

            SetBoolFieldIfPresent(settings, settingsType, "enable", payloadJson, "enable");
            SetBoolFieldIfPresent(aotSettings, aotSettings.GetType(), "layoutValidation", payloadJson, "layoutValidation");
            SetBoolFieldIfPresent(aotSettings, aotSettings.GetType(), "enablePgoProfile", payloadJson, "enablePgoProfile");
            SetStringArrayFieldIfPresent(aotSettings, aotSettings.GetType(), "ruleFiles", payloadJson, "ruleFiles", "ruleFilesText");
            SetStringArrayFieldIfPresent(aotSettings, aotSettings.GetType(), "pgoRuleFiles", payloadJson, "pgoRuleFiles", "pgoRuleFilesText");
            SetStringArrayFieldIfPresent(aotSettings, aotSettings.GetType(), "lazyLoadedAssemblyNames", payloadJson, "lazyLoadedAssemblyNames", "lazyLoadedAssemblyNamesText");
            SetStringArrayFieldIfPresent(hotUpdateSettings, hotUpdateSettings.GetType(), "hotUpdateAssemblyNames", payloadJson, "hotUpdateAssemblyNames", "hotUpdateAssemblyNamesText");
            SetEnumFieldIfPresent(gcSettings, gcSettings.GetType(), "mode", payloadJson, "gcMode");
            SetBoolFieldIfPresent(gcSettings, gcSettings.GetType(), "enableGCDebug", payloadJson, "enableGCDebug");
        }

        private static bool GetBoolField(object instance, Type ownerType, string fieldName, bool fallback)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(bool))
                return fallback;

            return (bool)field.GetValue(instance);
        }

        private static string GetEnumField(object instance, Type ownerType, string fieldName)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
                return string.Empty;

            var value = field.GetValue(instance);
            return value != null ? value.ToString() : string.Empty;
        }

        private static string[] GetStringArrayField(object instance, Type ownerType, string fieldName)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
                return new string[0];

            return field.GetValue(instance) as string[] ?? new string[0];
        }

        private static void AppendStringArray(StringBuilder sb, string[] values)
        {
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    sb.Append('"');
                    sb.Append(JsonHelper.EscapeString(values[i]));
                    sb.Append('"');
                }
            }

            sb.Append(']');
        }

        private static void SetBoolFieldIfPresent(object instance, Type ownerType, string fieldName, string payloadJson, string payloadField)
        {
            bool value;
            if (!JsonHelper.TryExtractBool(payloadJson, payloadField, out value))
                return;

            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(bool))
                throw new MissingFieldException(ownerType.FullName, fieldName);

            field.SetValue(instance, value);
        }

        private static void SetEnumFieldIfPresent(object instance, Type ownerType, string fieldName, string payloadJson, string payloadField)
        {
            var rawValue = JsonHelper.ExtractString(payloadJson, payloadField);
            if (rawValue == null)
                return;

            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || !field.FieldType.IsEnum)
                throw new MissingFieldException(ownerType.FullName, fieldName);

            var value = Enum.Parse(field.FieldType, rawValue, true);
            field.SetValue(instance, value);
        }

        private static void SetStringArrayFieldIfPresent(
            object instance,
            Type ownerType,
            string fieldName,
            string payloadJson,
            string arrayPayloadField,
            string textPayloadField)
        {
            var arrayPayload = JsonHelper.ExtractRaw(payloadJson, arrayPayloadField);
            string[] values = null;
            if (!string.IsNullOrEmpty(arrayPayload))
            {
                values = ParseJsonStringArray(arrayPayload);
            }
            else
            {
                var text = JsonHelper.ExtractString(payloadJson, textPayloadField);
                if (text == null)
                    return;

                values = SplitSettingsText(DecodeSimpleJsonString(text));
            }

            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(string[]))
                throw new MissingFieldException(ownerType.FullName, fieldName);

            field.SetValue(instance, values ?? new string[0]);
        }

        private static string[] ParseJsonStringArray(string json)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(json))
                return values.ToArray();

            var inString = false;
            var escaped = false;
            var sb = new StringBuilder();
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (!inString)
                {
                    if (c == '"')
                    {
                        inString = true;
                        escaped = false;
                        sb.Length = 0;
                    }
                    continue;
                }

                if (escaped)
                {
                    AppendDecodedEscape(sb, c, json, ref i);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    values.Add(sb.ToString());
                    inString = false;
                    continue;
                }

                sb.Append(c);
            }

            return values.ToArray();
        }

        private static string[] SplitSettingsText(string text)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(text))
                return values.ToArray();

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length > 0)
                    values.Add(line);
            }

            return values.ToArray();
        }

        private static string DecodeSimpleJsonString(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value ?? string.Empty;

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(c);
                    continue;
                }

                i++;
                AppendDecodedEscape(sb, value[i], value, ref i);
            }

            return sb.ToString();
        }

        private static void AppendDecodedEscape(StringBuilder sb, char escaped, string source, ref int index)
        {
            switch (escaped)
            {
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case '"':
                    sb.Append('"');
                    break;
                case '\\':
                    sb.Append('\\');
                    break;
                case '/':
                    sb.Append('/');
                    break;
                case 'u':
                    if (index + 4 < source.Length)
                    {
                        var hex = source.Substring(index + 1, 4);
                        int code;
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                        {
                            sb.Append((char)code);
                            index += 4;
                            break;
                        }
                    }
                    sb.Append('u');
                    break;
                default:
                    sb.Append(escaped);
                    break;
            }
        }

        private static string NormalizeProjectRelativePath(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."))
                .Replace('\\', '/')
                .TrimEnd('/');
            if (Path.IsPathRooted(normalized))
            {
                var absolute = Path.GetFullPath(normalized).Replace('\\', '/');
                if (!absolute.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("AOT rule path must stay inside the Unity project.");

                return absolute.Substring(projectRoot.Length + 1);
            }

            if (normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.IndexOf("/../", StringComparison.Ordinal) >= 0 ||
                normalized == "..")
            {
                throw new InvalidOperationException("AOT rule path must stay inside the Unity project.");
            }

            var resolved = Path.GetFullPath(Path.Combine(projectRoot, normalized)).Replace('\\', '/');
            if (!resolved.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("AOT rule path must stay inside the Unity project.");

            return normalized;
        }

        private static void EnsureLeanClrSettingsInitialized()
        {
            var settings = GetLeanClrSettingsInstance();
            var settingsType = settings.GetType();
            EnsureObjectField(settings, settingsType, "leanAOTSettings", "LeanCLR.LeanAOTSettings");
            EnsureObjectField(settings, settingsType, "hotUpdateSettings", "LeanCLR.HotUpdateSettings");
            EnsureObjectField(settings, settingsType, "gcSettings", "LeanCLR.GCSettings");
        }

        private static void SetLeanClrEnable(bool enabled)
        {
            var settings = GetLeanClrSettingsInstance();
            var enableField = settings.GetType().GetField("enable", BindingFlags.Instance | BindingFlags.Public);
            if (enableField == null)
                throw new MissingFieldException("LeanCLR.Settings.enable");

            enableField.SetValue(settings, enabled);
        }

        private static void EnsureAotRuleFileRegistered(string rulePath)
        {
            var settings = GetLeanClrSettingsInstance();
            var settingsType = settings.GetType();
            var aotSettings = EnsureObjectField(settings, settingsType, "leanAOTSettings", "LeanCLR.LeanAOTSettings");
            var ruleFilesField = aotSettings.GetType().GetField("ruleFiles", BindingFlags.Instance | BindingFlags.Public);
            if (ruleFilesField == null)
                throw new MissingFieldException("LeanCLR.LeanAOTSettings.ruleFiles");

            var existing = ruleFilesField.GetValue(aotSettings) as string[];
            var normalized = rulePath.Replace('\\', '/');
            if (existing != null)
            {
                for (var i = 0; i < existing.Length; i++)
                {
                    if (string.Equals((existing[i] ?? string.Empty).Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            var list = new List<string>();
            if (existing != null)
                list.AddRange(existing);
            list.Add(normalized);
            ruleFilesField.SetValue(aotSettings, list.ToArray());
        }

        private static object EnsureObjectField(object instance, Type ownerType, string fieldName, string typeName)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
                throw new MissingFieldException(ownerType.FullName, fieldName);

            var value = field.GetValue(instance);
            if (value != null)
                return value;

            var fieldType = FindLeanClrType(typeName);
            value = Activator.CreateInstance(fieldType);
            field.SetValue(instance, value);
            return value;
        }

        private static object GetLeanClrSettingsInstance()
        {
            var settingsType = FindLeanClrType("LeanCLR.Settings");
            var property = settingsType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (property == null)
                throw new MissingMemberException("LeanCLR.Settings.Instance");

            var settings = property.GetValue(null, null);
            if (settings == null)
                throw new InvalidOperationException("LeanCLR Settings instance is null.");

            return settings;
        }

        private static void SaveLeanClrSettings()
        {
            InvokeStaticVoid("LeanCLR.Settings", "Save");
        }

        private static void InvokeLocalInstallerInstallLocal()
        {
            var installerType = FindLeanClrType("LeanCLR.LocalInstaller");
            var installer = Activator.CreateInstance(installerType);
            var method = installerType.GetMethod("InstallLocal", BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
                throw new MissingMethodException("LeanCLR.LocalInstaller", "InstallLocal");

            method.Invoke(installer, null);
        }

        private static void InvokeStaticVoid(string typeName, string methodName)
        {
            var type = FindLeanClrType(typeName);
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method == null)
                throw new MissingMethodException(typeName, methodName);

            method.Invoke(null, null);
        }

        private static string GetLeanClrCompileDllOutputPath()
        {
            var settingsType = FindLeanClrType("LeanCLR.Settings");
            var method = settingsType.GetMethod("GetCompileDllOutputPath", BindingFlags.Static | BindingFlags.Public);
            if (method == null)
                return string.Empty;

            var result = method.Invoke(null, new object[] { EditorUserBuildSettings.activeBuildTarget });
            return result != null ? result.ToString().Replace('\\', '/') : string.Empty;
        }

        private static string GetLeanClrStaticStringProperty(string typeName, string propertyName)
        {
            var type = FindLeanClrType(typeName);
            var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
            if (property == null)
                return string.Empty;

            var value = property.GetValue(null, null);
            return value != null ? value.ToString().Replace('\\', '/') : string.Empty;
        }

        private static Type FindLeanClrType(string typeName)
        {
            var type = Type.GetType(typeName + ", " + LeanClrEditorAssemblyName);
            if (type != null)
                return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            throw new TypeLoadException(typeName);
        }
    }
}
#endif
