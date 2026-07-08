#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板 Prefab 与绑定代码生成服务。
    /// </summary>
    internal static partial class UIKitPanelPrefabCreator
    {
        /// <summary>默认面板 Prefab 输出目录。</summary>
        public const string DEFAULT_PREFAB_FOLDER = "Assets/Resources/Art/UIPrefab";

        /// <summary>默认面板脚本输出目录。</summary>
        public const string DEFAULT_SCRIPT_FOLDER = "Assets/Scripts/UI";

        /// <summary>默认面板脚本命名空间。</summary>
        public const string DEFAULT_SCRIPT_NAMESPACE = "GameUI";

        /// <summary>默认用户脚本程序集名称。</summary>
        public const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

        /// <summary>默认完整代码模板名。</summary>
        public const string DEFAULT_CODE_TEMPLATE = "Default";

        /// <summary>最小代码模板名。</summary>
        public const string MINIMAL_CODE_TEMPLATE = "Minimal";

        private const string PENDING_SESSION_KEY = "YokiFrame.UIKit.PendingPanelPrefabs";
        private const string PENDING_OPEN_STAGE_SESSION_KEY = "YokiFrame.UIKit.PendingPanelPrefabStages";
        private const char PENDING_SEPARATOR = '|';
        private const int MAX_OPEN_STAGE_BIND_RETRY_COUNT = 60;

        private static readonly HashSet<string> sCSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
        };

        /// <summary>
        /// 获取可用于面板生成的代码模板名称。
        /// </summary>
        public static string[] GetCodeTemplateNames() =>
            new[] { DEFAULT_CODE_TEMPLATE, MINIMAL_CODE_TEMPLATE };

        /// <summary>
        /// 根据命令载荷创建面板 Prefab 与绑定脚本。
        /// </summary>
        public static UIKitEditorCommandResult CreatePanelPrefab(string payloadJson)
        {
            var request = UIKitPanelCreateRequest.FromJson(payloadJson);
            ValidateRequest(request);

            var prefabFolder = NormalizeAssetFolder(request.PrefabFolder, DEFAULT_PREFAB_FOLDER);
            var scriptFolder = NormalizeAssetFolder(request.ScriptFolder, DEFAULT_SCRIPT_FOLDER);
            var prefabPath = CombineAssetPath(prefabFolder, request.PanelName + ".prefab");
            if (File.Exists(prefabPath) && !request.Overwrite)
                throw new InvalidOperationException("Prefab 已存在: " + prefabPath);

            EnsureAssetFolder(prefabFolder);
            EnsureAssetFolder(CombineAssetPath(scriptFolder, request.PanelName));

            GameObject panelRoot = default;
            try
            {
                panelRoot = CreatePanelRoot(request.PanelName);
                var prefab = PrefabUtility.SaveAsPrefabAsset(panelRoot, prefabPath);
                var scriptsChanged = GenerateCodeForPrefab(prefab, request, scriptFolder);
                AddPendingPrefab(request.PanelName, request.ScriptNamespace, prefabPath, scriptFolder, request.AssemblyName, false);
                AssetDatabase.SaveAssets();
                if (scriptsChanged)
                    AssetDatabase.Refresh();
                ProcessPendingPrefabBindingsIfReady(scriptsChanged);

                return UIKitEditorCommandResult.Success(
                    "UIPrefab 已创建",
                    prefabPath,
                    GetPanelScriptPath(request, scriptFolder),
                    GetPanelDesignerPath(request, scriptFolder),
                    scriptsChanged);
            }
            finally
            {
                if (panelRoot != default)
                    UnityEngine.Object.DestroyImmediate(panelRoot);
            }
        }

        /// <summary>
        /// 为已有 Prefab 重新生成 UIKit 绑定脚本。
        /// </summary>
        public static UIKitEditorCommandResult GenerateCodeForPrefab(GameObject prefab, UIKitPanelCreateRequest request)
        {
            if (prefab == default)
                throw new ArgumentNullException(nameof(prefab));

            request.ApplyDefaultsFromPrefab(prefab.name);
            ValidateRequest(request);
            var scriptFolder = NormalizeAssetFolder(request.ScriptFolder, DEFAULT_SCRIPT_FOLDER);
            var scriptsChanged = GenerateCodeForPrefab(prefab, request, scriptFolder);
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            AddPendingPrefab(request.PanelName, request.ScriptNamespace, prefabPath, scriptFolder, request.AssemblyName, false);
            AssetDatabase.SaveAssets();
            if (scriptsChanged)
                AssetDatabase.Refresh();
            ProcessPendingPrefabBindingsIfReady(scriptsChanged);

            return UIKitEditorCommandResult.Success(
                "UI 代码已生成",
                prefabPath,
                GetPanelScriptPath(request, scriptFolder),
                GetPanelDesignerPath(request, scriptFolder),
                scriptsChanged);
        }

        /// <summary>
        /// 为当前打开的 Prefab Stage 内容重新生成 UIKit 绑定脚本，避免离线保存同一 Prefab 资源。
        /// </summary>
        public static UIKitEditorCommandResult GenerateCodeForPrefabContents(
            GameObject prefabContentsRoot,
            string prefabPath,
            UIKitPanelCreateRequest request)
        {
            if (prefabContentsRoot == default)
                throw new ArgumentNullException(nameof(prefabContentsRoot));

            request.ApplyDefaultsFromPrefab(prefabContentsRoot.name);
            ValidateRequest(request);
            var scriptFolder = NormalizeAssetFolder(request.ScriptFolder, DEFAULT_SCRIPT_FOLDER);
            var scriptsChanged = GenerateCodeForPrefab(prefabContentsRoot, request, scriptFolder);
            AssetDatabase.SaveAssets();
            if (scriptsChanged)
            {
                AddPendingPrefab(request.PanelName, request.ScriptNamespace, prefabPath, scriptFolder, request.AssemblyName, true);
                AssetDatabase.Refresh();
                ProcessPendingPrefabBindingsIfReady(true);
            }
            else
            {
                var bound = UIKitPrefabBindingProcessor.TryBindGeneratedPanelContents(
                    request.PanelName,
                    request.ScriptNamespace,
                    prefabContentsRoot,
                    scriptFolder,
                    request.AssemblyName);
                if (bound && prefabContentsRoot.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(prefabContentsRoot.scene);
                if (bound)
                    ClearOpenPrefabStageBinding(prefabPath);
                if (!bound)
                {
                    AddPendingPrefab(request.PanelName, request.ScriptNamespace, prefabPath, scriptFolder, request.AssemblyName, true);
                    ProcessPendingPrefabBindingsIfReady(false);
                }
            }

            return UIKitEditorCommandResult.Success(
                "UI 代码已生成",
                prefabPath,
                GetPanelScriptPath(request, scriptFolder),
                GetPanelDesignerPath(request, scriptFolder),
                scriptsChanged);
        }

        /// <summary>
        /// 处理等待脚本编译完成后才能绑定的面板 Prefab。
        /// </summary>
        public static void ProcessPendingPrefabBindings()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPendingPrefabBindings;
                return;
            }

            var pending = SessionState.GetString(PENDING_SESSION_KEY, string.Empty);
            if (string.IsNullOrEmpty(pending))
                return;

            var remaining = new List<string>();
            var retrySoon = false;
            var lines = pending.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!TryParsePendingEntry(lines[i], out var panelName, out var scriptNamespace, out var prefabPath, out var scriptFolder, out var assemblyName))
                    continue;

                var requiresOpenPrefabStage = IsOpenPrefabStageBindingPending(prefabPath);
                bool handledByOpenPrefabStage;
                var openStageBound = TryBindOpenPrefabStage(
                    panelName,
                    scriptNamespace,
                    prefabPath,
                    scriptFolder,
                    assemblyName,
                    out handledByOpenPrefabStage);
                if (handledByOpenPrefabStage)
                {
                    if (!openStageBound)
                    {
                        remaining.Add(lines[i]);
                        if (requiresOpenPrefabStage && ShouldRetryOpenPrefabStageBinding(prefabPath))
                            retrySoon = true;
                    }
                    else
                    {
                        ClearOpenPrefabStageBinding(prefabPath);
                    }
                    continue;
                }

                if (requiresOpenPrefabStage)
                {
                    remaining.Add(lines[i]);
                    if (ShouldRetryOpenPrefabStageBinding(prefabPath))
                        retrySoon = true;
                    continue;
                }

                if (!TryBindGeneratedPanel(panelName, scriptNamespace, prefabPath, scriptFolder, assemblyName))
                    remaining.Add(lines[i]);
            }

            SessionState.SetString(PENDING_SESSION_KEY, string.Join("\n", remaining.ToArray()));
            if (retrySoon)
                EditorApplication.delayCall += ProcessPendingPrefabBindings;
        }

        private static void ProcessPendingPrefabBindingsIfReady(bool scriptsChanged)
        {
            if (scriptsChanged || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPendingPrefabBindings;
                return;
            }

            ProcessPendingPrefabBindings();
        }

        private static bool TryBindOpenPrefabStage(
            string panelName,
            string scriptNamespace,
            string prefabPath,
            string scriptFolder,
            string assemblyName,
            out bool handled)
        {
            handled = false;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null ||
                prefabStage.prefabContentsRoot == null ||
                string.IsNullOrEmpty(prefabStage.prefabAssetPath) ||
                string.IsNullOrEmpty(prefabPath) ||
                !string.Equals(
                    NormalizeAssetPathForComparison(prefabStage.prefabAssetPath),
                    NormalizeAssetPathForComparison(prefabPath),
                    StringComparison.Ordinal))
            {
                return false;
            }

            handled = true;
            var bound = UIKitPrefabBindingProcessor.TryBindGeneratedPanelContents(
                panelName,
                scriptNamespace,
                prefabStage.prefabContentsRoot,
                scriptFolder,
                assemblyName);
            if (bound && prefabStage.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            return bound;
        }

        private static GameObject CreatePanelRoot(string panelName)
        {
            var gameObject = new GameObject(panelName);
            var rect = gameObject.AddComponent<RectTransform>();
            StretchRect(rect);

            var panelChild = new GameObject("Panel");
            panelChild.transform.SetParent(gameObject.transform, false);
            var panelRect = panelChild.AddComponent<RectTransform>();
            StretchRect(panelRect);

            var panelImage = panelChild.AddComponent<UnityEngine.UI.Image>();
            panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            panelImage.type = UnityEngine.UI.Image.Type.Sliced;
            panelImage.color = Color.white;
            return gameObject;
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
#endif
