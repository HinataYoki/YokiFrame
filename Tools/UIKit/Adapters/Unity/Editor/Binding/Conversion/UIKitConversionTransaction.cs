#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>保存转换前的源码和 Prefab 快照；跨编译恢复，编译或类型验证失败时还原完整事务。</summary>
    [InitializeOnLoad]
    internal static class UIKitConversionTransaction
    {
        private const string SESSION_KEY = "UIKit.BindingConversion";
        private static bool sCompilationFailed;
        private static bool sObservedCompilation;
        internal static bool IsPending => !string.IsNullOrEmpty(SessionState.GetString(SESSION_KEY, string.Empty));

        /// <summary>监听编译终态并在 Domain Reload 后校验新类型，确保转换不是仅写文件即视为成功。</summary>
        static UIKitConversionTransaction()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            if (IsPending) EditorApplication.update += ValidateAfterReload;
        }

        /// <summary>提交预构建文件集、保留 Unity GUID 移动脚本，并保存精确绑定的转换配置。</summary>
        internal static void Execute(AbstractBind bind, BindType kind, UIKitPanelCodeLayout layout, string fullName,
            Dictionary<string, string> relocations, Dictionary<string, string> sources, string customTypeName = null)
        {
            Record record = Capture(layout, fullName, kind, relocations, sources);
            string bindJson = EditorJsonUtility.ToJson(bind);
            record.bindJson = bindJson;
            UIKitGeneratedOwnerCodeService.ResolvePrefab(bind, out _, out _, out record.ownerPath);
            SessionState.SetString(SESSION_KEY, JsonUtility.ToJson(record));
            sCompilationFailed = false;
            sObservedCompilation = false;
            EditorApplication.LockReloadAssemblies();
            try
            {
                foreach (string destination in relocations.Values)
                    EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var move in relocations)
                    {
                        string error = AssetDatabase.MoveAsset(move.Key, move.Value);
                        if (!string.IsNullOrEmpty(error)) throw new IOException(move.Key + " -> " + move.Value + ": " + error);
                    }
                    UIKitPanelCodeGenerator.CommitSources(sources);
                    if (!string.IsNullOrEmpty(customTypeName))
                        UIKitBindConversion.SetGeneratedType(bind, customTypeName);
                    UIKitBindConversion.SetKind(bind, kind);
                    UIKitBindCodeService.SaveAndQueue(bind, layout);
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }
            catch (Exception exception)
            {
                EditorJsonUtility.FromJsonOverwrite(bindJson, bind);
                try { Restore(record); }
                catch (Exception rollback) { throw new AggregateException("UIKit 转换及回滚失败。", exception, rollback); }
                SessionState.EraseString(SESSION_KEY);
                AssetDatabase.Refresh();
                throw;
            }
            finally { EditorApplication.UnlockReloadAssemblies(); }
            AssetDatabase.Refresh();
            EditorApplication.update -= WatchCompilation;
            EditorApplication.update += WatchCompilation;
        }

        /// <summary>脚本实际重载后再标记类型可用；不依赖特定 Unity 版本是否发出全局编译完成回调。</summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!IsPending) return;
            Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
            record.compiled = true;
            SessionState.SetString(SESSION_KEY, JsonUtility.ToJson(record));
            EditorApplication.update -= ValidateAfterReload;
            EditorApplication.update += ValidateAfterReload;
        }

        /// <summary>失败编译不会触发脚本重载，通过 Editor 的实际编译状态保证仍能完成回滚。</summary>
        private static void WatchCompilation()
        {
            if (!IsPending) { EditorApplication.update -= WatchCompilation; return; }
            if (EditorApplication.isCompiling) { sObservedCompilation = true; return; }
            if (EditorApplication.isUpdating) return;
            if (EditorUtility.scriptCompilationFailed)
            {
                EditorApplication.update -= WatchCompilation;
                RollbackCompilation();
            }
            else if (sObservedCompilation)
            {
                EditorApplication.update -= WatchCompilation;
                OnScriptsReloaded();
            }
        }

        /// <summary>通过 AssetDatabase 建立目标目录，使脚本移动在暂停导入期间也能识别合法资源路径。</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>记录所有将被覆盖或移动的文件、GUID 和当前回填队列，失败只恢复本事务触及的内容。</summary>
        private static Record Capture(UIKitPanelCodeLayout layout, string fullName, BindType kind,
            Dictionary<string, string> relocations, Dictionary<string, string> sources)
        {
            Record record = new()
            {
                typeName = fullName, assemblyName = layout.AssemblyName, kind = kind,
                pendingBindings = UIKitPendingBindingService.CaptureQueue(),
                elementComponentName = layout.ElementComponentName,
                request = new UIKitPanelGenerationRequest
                {
                    panelName = layout.PanelName, scriptFolder = layout.ScriptFolder,
                    scriptNamespace = layout.ScriptNamespace, assemblyName = layout.AssemblyName,
                    prefabFolder = layout.PrefabFolder, prefabPath = layout.PrefabPath, codeTemplate = layout.CodeTemplate,
                },
            };
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase) { layout.PrefabPath };
            foreach (var move in relocations)
            {
                paths.Add(move.Key);
            }
            foreach (string path in sources.Keys) paths.Add(path);
            foreach (string path in paths)
            {
                record.files.Add(Snapshot(path));
                record.files.Add(Snapshot(path + ".meta"));
            }
            return record;
        }

        /// <summary>按原始字节备份源码或资产，保留编码、换行和 Unity 序列化内容。</summary>
        private static FileRecord Snapshot(string path)
        {
            string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(path);
            return new FileRecord { path = path, existed = File.Exists(absolutePath),
                bytes = File.Exists(absolutePath)
                    ? System.Convert.ToBase64String(File.ReadAllBytes(absolutePath)) : string.Empty };
        }

        /// <summary>收集本次编译错误；任何编译失败都使迁移回滚，避免交付引用已失效的半转换状态。</summary>
        private static void OnAssemblyCompiled(string _, CompilerMessage[] messages)
        {
            if (!IsPending) return;
            foreach (CompilerMessage message in messages)
                if (message.type == CompilerMessageType.Error) sCompilationFailed = true;
        }

        /// <summary>编译失败时延后回滚，避开 Unity 编译回调内部再次导入资源。</summary>
        private static void OnCompilationFinished(object _)
        {
            if (!IsPending) return;
            if (sCompilationFailed)
            {
                EditorApplication.update -= RollbackWhenReady;
                EditorApplication.update += RollbackWhenReady;
            }
            else
            {
                Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
                record.compiled = true;
                SessionState.SetString(SESSION_KEY, JsonUtility.ToJson(record));
                EditorApplication.update -= ValidateAfterReload;
                EditorApplication.update += ValidateAfterReload;
            }
        }

        /// <summary>编译器退出后恢复失败事务，回调执行一次即移除，避免在编译回调内重入导入。</summary>
        private static void RollbackWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RollbackWhenReady;
            RollbackCompilation();
        }

        /// <summary>脚本编译失败后恢复原代码和绑定，并触发原代码重新导入。</summary>
        private static void RollbackCompilation()
        {
            if (!IsPending) return;
            Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
            Restore(record);
            SessionState.EraseString(SESSION_KEY);
            AssetDatabase.Refresh();
            Debug.LogError("UIKit 绑定转换未通过编译，已恢复原代码和 Prefab；请查看编译诊断。");
        }

        /// <summary>Domain Reload 后核对目标类型基类与脚本身份，再完成 Prefab 回填并结束事务。</summary>
        private static void ValidateAfterReload()
        {
            if (!IsPending)
            {
                EditorApplication.update -= ValidateAfterReload;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
            if (!record.compiled) return;
            Type type = Type.GetType(record.typeName + ", " + record.assemblyName, false);
            Type expected = record.kind == BindType.Component ? typeof(UIComponent) : typeof(UIElement);
            if (type == null || !expected.IsAssignableFrom(type)
                || (record.kind == BindType.Element && typeof(UIComponent).IsAssignableFrom(type)))
            {
                RollbackCompilation();
                return;
            }
            UIKitPanelCodeLayout layout = new(record.request, record.elementComponentName);
            UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.BindOwner(layout, record.typeName,
                record.assemblyName, record.kind == BindType.Element ? UIKitGeneratedOwnerKind.Element
                    : UIKitGeneratedOwnerKind.Component, record.ownerPath, out string error);
            if (status != UIKitPrefabBindingStatus.Success)
            {
                Debug.LogError("UIKit 转换回填失败: " + error);
                RollbackCompilation();
                return;
            }
            UIKitPendingBindingService.Process();
            SessionState.EraseString(SESSION_KEY);
        }

        /// <summary>按快照恢复事务涉及的文件和回填队列；恢复异常保留记录供诊断，不吞掉失败。</summary>
        private static void Restore(Record record)
        {
            // 批量导入失败时 AssetDatabase 的移动缓存可能尚未提交，按原始文件及 .meta 快照恢复。
            foreach (FileRecord file in record.files)
            {
                string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(file.path);
                if (file.existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                    File.WriteAllBytes(absolutePath, System.Convert.FromBase64String(file.bytes));
                }
                else if (File.Exists(absolutePath)) File.Delete(absolutePath);
            }
            UIKitPendingBindingService.RestoreQueue(record.pendingBindings);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == record.request.prefabPath)
            {
                Transform current = stage.prefabContentsRoot.transform;
                if (!string.IsNullOrEmpty(record.ownerPath))
                    foreach (string index in record.ownerPath.Split('/')) current = current.GetChild(int.Parse(index));
                AbstractBind bind = current.GetComponent<AbstractBind>();
                if (bind != default) EditorJsonUtility.FromJsonOverwrite(record.bindJson, bind);
            }
        }

        [Serializable]
        private sealed class Record
        {
            public string typeName;
            public string assemblyName;
            public BindType kind;
            public string pendingBindings;
            public string elementComponentName;
            public UIKitPanelGenerationRequest request;
            public string ownerPath;
            public string bindJson;
            public bool compiled;
            public List<FileRecord> files = new();
        }

        [Serializable]
        private sealed class FileRecord { public string path; public bool existed; public string bytes; }
    }
}
#endif
