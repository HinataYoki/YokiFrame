#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>保存目录移动日志并跨 Domain Reload 验证；失败按逆序移动恢复 GUID 与原始文件。</summary>
    [InitializeOnLoad]
    internal static class UIKitCodeLayoutMigrationTransaction
    {
        private const string SESSION_KEY = "UIKit.CodeLayoutMigration";
        internal static bool IsPending => !string.IsNullOrEmpty(SessionState.GetString(SESSION_KEY, string.Empty));

        /// <summary>恢复当前编辑器会话中的迁移记录，编译结束后统一核对或回滚。</summary>
        static UIKitCodeLayoutMigrationTransaction()
        {
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update += CompleteWhenReady;
        }

        /// <summary>重验预览快照、保存移动日志并启动编译；所有实际文件移动都保留 .meta GUID。</summary>
        internal static void Execute(UIKitCodeLayoutMigrationPlan plan)
        {
            UIKitCodeLayoutMigration.RequireIdle();
            UIKitCodeLayoutMigrationPlan current = UIKitCodeLayoutMigration.Build(plan.Root);
            if (current.Describe() != plan.Describe() || JsonUtility.ToJson(new Record { moves = current.Moves })
                != JsonUtility.ToJson(new Record { moves = plan.Moves }))
                throw new InvalidOperationException("目录内容在预览后变化，请重新预览。");
            foreach (var file in plan.Files)
                if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(file.source)) || Convert.ToBase64String(File.ReadAllBytes(UIKitPanelCodeLayout.ToAbsolutePath(file.source))) != file.bytes)
                    throw new InvalidOperationException("文件在预览后变化，请重新预览: " + file.source);
            var record = new Record { moves = plan.Moves, files = plan.Files };
            Save(record);
            EditorApplication.LockReloadAssemblies();
            try
            {
                // 后续步骤可能使用刚移动的目录，必须让 AssetDatabase 逐步更新目录索引。
                Apply(record);
            }
            catch (Exception exception)
            {
                try { Restore(record); SessionState.EraseString(SESSION_KEY); }
                catch (Exception rollback) { throw new AggregateException("目录迁移及回滚失败，保留会话记录。", exception, rollback); }
                throw;
            }
            finally { EditorApplication.UnlockReloadAssemblies(); AssetDatabase.Refresh(); }
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>按预检顺序执行移动，每一步保存恢复位置；供事务及隔离资产测试使用。</summary>
        internal static void Apply(Record record)
        {
            for (; record.completed < record.moves.Count;)
            {
                var move = record.moves[record.completed];
                EnsureFolder(UIKitPanelCodeLayout.AssetDirectory(move.destination), record.createdFolders);
                string error = AssetDatabase.MoveAsset(move.source, move.destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(move.source + ": " + error);
                record.completed++;
                Save(record);
            }
        }

        /// <summary>只创建缺失的目标父目录，记录其所有权以便回滚时删除空目录。</summary>
        private static void EnsureFolder(string path, List<string> created)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(UIKitPanelCodeLayout.AssetDirectory(path), created);
            string guid = AssetDatabase.CreateFolder(UIKitPanelCodeLayout.AssetDirectory(path), Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid)) throw new IOException("无法创建迁移目录: " + path);
            created.Add(path);
        }

        /// <summary>回滚已成功的移动，不覆盖意外出现的原路径；失败时保留日志供人工恢复。</summary>
        internal static void Restore(Record record)
        {
            while (record.completed > 0)
            {
                var move = record.moves[record.completed - 1];
                string error = AssetDatabase.MoveAsset(move.destination, move.source);
                if (!string.IsNullOrEmpty(error)) throw new IOException("回滚失败: " + move.destination + ": " + error);
                record.completed--;
                Save(record);
            }
            for (var index = record.createdFolders.Count - 1; index >= 0; index--)
            {
                string path = record.createdFolders[index];
                if (Directory.Exists(UIKitPanelCodeLayout.ToAbsolutePath(path)) && Directory.GetFileSystemEntries(UIKitPanelCodeLayout.ToAbsolutePath(path)).Length == 0)
                    if (!AssetDatabase.DeleteAsset(path)) throw new IOException("无法移除本事务空目录: " + path);
            }
            foreach (var file in record.files)
                if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(file.source)) || Convert.ToBase64String(File.ReadAllBytes(UIKitPanelCodeLayout.ToAbsolutePath(file.source))) != file.bytes)
                    throw new IOException("回滚后的文件字节不一致: " + file.source);
        }

        /// <summary>验证源码和元数据完全不变，并核对脚本 GUID 与实际编译类型身份。</summary>
        internal static void Verify(Record record)
        {
            foreach (var file in record.files)
            {
                if (File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(file.source)) || !File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(file.destination))
                    || Convert.ToBase64String(File.ReadAllBytes(UIKitPanelCodeLayout.ToAbsolutePath(file.destination))) != file.bytes)
                    throw new IOException("迁移文件校验失败: " + file.destination);
                if (!string.IsNullOrEmpty(file.guid) && AssetDatabase.AssetPathToGUID(file.destination) != file.guid)
                    throw new IOException("迁移 GUID 不一致: " + file.destination);
                if (string.IsNullOrEmpty(file.type)) continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(file.destination);
                if (script == default || script.GetClass()?.AssemblyQualifiedName != file.type)
                    throw new IOException("迁移脚本身份不一致: " + file.destination);
            }
        }

        /// <summary>编译终态只记入会话日志，实际校验避开编译回调内部的资产导入。</summary>
        private static void OnCompilationFinished(object _)
        {
            if (!IsPending) return;
            Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
            record.compiled = true;
            Save(record);
        }

        /// <summary>脚本重载发生时补记成功编译事实，防止回调顺序导致事务永远等待。</summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnReloaded() => OnCompilationFinished(null);

        /// <summary>编译和导入结束后验证；失败恢复原目录并重新导入，成功才清除事务记录。</summary>
        private static void CompleteWhenReady()
        {
            if (!IsPending || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Record record = JsonUtility.FromJson<Record>(SessionState.GetString(SESSION_KEY, string.Empty));
            if (!record.compiled) return;
            try
            {
                if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("迁移后编译失败。");
                Verify(record);
                SessionState.EraseString(SESSION_KEY);
                Debug.Log("UIKit 代码目录迁移完成；源码、GUID 和类型身份验证通过。");
            }
            catch (Exception exception)
            {
                try
                {
                    Restore(record);
                    SessionState.EraseString(SESSION_KEY);
                    AssetDatabase.Refresh();
                    Debug.LogError("UIKit 目录迁移已回滚: " + exception.Message);
                }
                catch (Exception rollback)
                {
                    EditorApplication.update -= CompleteWhenReady;
                    Debug.LogException(new AggregateException("目录迁移回滚失败，已停止自动重试并保留会话记录。", exception, rollback));
                }
            }
        }

        /// <summary>记录本会话的可恢复进度；测试完成后也须清除同一记录，避免后续自动校验。</summary>
        internal static void Save(Record record)
        {
            if (record == null) SessionState.EraseString(SESSION_KEY);
            else SessionState.SetString(SESSION_KEY, JsonUtility.ToJson(record));
        }

        [Serializable]
        internal sealed class Record
        {
            public List<UIKitCodeLayoutMigrationPlan.Move> moves = new();
            public List<UIKitCodeLayoutMigrationPlan.FileState> files = new();
            public List<string> createdFolders = new();
            public int completed;
            public bool compiled;
        }
    }
}
#endif
