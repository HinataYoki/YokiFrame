#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class UnityLiveCodeHost
    {
        [Serializable]
        private sealed class BindingRecord
        {
            public string targetId;
            public string scenePath;
            public string componentId;
            public string exportId;
            public string state;
        }

        [Serializable]
        private sealed class BindingLedger
        {
            public int schema = 1;
            public string sourcePath;
            public string scriptGuid;
            public List<BindingRecord> bindings = new List<BindingRecord>();
        }

        private string BindingPath(string sourcePath) => YokiFrameFilePathPolicy.CombineInside(mProjectRoot,
            ".yokiframe", "engine", "live-exports", "bindings", LiveCodeManager.Hash(sourcePath) + ".json");

        private BindingLedger ReadBindings(string sourcePath)
        {
            string path = BindingPath(sourcePath);
            if (!File.Exists(path)) return new BindingLedger { sourcePath = sourcePath, scriptGuid = AssetDatabase.AssetPathToGUID(sourcePath) };
            var ledger = JsonUtility.FromJson<BindingLedger>(LiveExportStore.ReadText(path));
            if (ledger == null || ledger.schema != 1 || ledger.sourcePath != sourcePath || ledger.bindings == null
                || ledger.bindings.Count > 256) throw new InvalidDataException("Invalid binding ledger.");
            var targets = new HashSet<string>();
            foreach (var binding in ledger.bindings)
                if (string.IsNullOrEmpty(binding.targetId) || !targets.Add(binding.targetId))
                    throw new InvalidDataException("Duplicate binding identity.");
            return ledger;
        }

        private void SaveBindings(BindingLedger ledger)
        {
            string path = BindingPath(ledger.sourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string text = JsonUtility.ToJson(ledger, true);
            if (File.Exists(path)) LiveExportStore.Replace(path, text);
            else LiveExportStore.WriteNew(path, text);
        }

        /// <summary>
        /// 把已提交导出版本绑定到已保存场景对象。不保存调用前已有的场景改动。
        /// </summary>
        /// <param name="exportId">导出版本标识。</param>
        /// <param name="target">显式场景对象；为空时只接受唯一捕获目标。</param>
        /// <returns>绑定脚本的资源路径。</returns>
        private string BindVersioned(string exportId, object target)
        {
            var store = ExportStore;
            var version = store.ReadVersion(exportId);
            RequireCommitted(store, version);
            Type type = RequireExportedType(version);
            GameObject owner = ResolveBindingOwner(version, target);
            string targetId = RequireTargetIdentity(owner);
            LiveExportTarget captured = FindCapturedTarget(version, targetId, owner.scene.path);
            var ledger = ReadBindings(version.SourcePath);
            RequireScriptGuid(ledger, version.SourcePath);
            BindingRecord record = FindBinding(ledger, targetId);
            Component existing = FindSingleComponent(owner, type);
            record = AdoptLegacyBinding(store, exportId, version, captured, ledger, record, existing, targetId, owner);
            if (existing != default) return ConfirmExisting(version, ledger, record, existing, owner, exportId);
            return AttachNew(version, ledger, record, owner, type, captured, exportId, targetId);
        }

        /// <summary>拒绝播放、编译、导入中，以及尚未提交或源码已变的导出。</summary>
        /// <param name="store">导出存储。</param>
        /// <param name="version">待绑定版本。</param>
        private static void RequireCommitted(LiveExportStore store, LiveExportVersion version)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Binding requires idle Edit Mode after normal Unity compilation.");
            if (version.BatchId.Length > 0 && store.ReadBatch(version.BatchId).State != "Committed")
                throw new InvalidOperationException("Export batch has not committed.");
            if (!store.MatchesSource(version)) throw new InvalidOperationException("ExportConflict: source changed.");
        }

        /// <summary>取得已编译导出类型。</summary>
        /// <param name="version">导出版本。</param>
        /// <returns>可挂载的组件类型。</returns>
        private Type RequireExportedType(LiveExportVersion version)
        {
            Type type = ExportedType(version);
            if (type == null) throw new InvalidOperationException("The requested export version has not compiled.");
            return type;
        }

        /// <summary>解析显式目标或唯一捕获目标，并要求它是已保存、已加载且无未保存改动的场景对象。</summary>
        /// <param name="version">导出版本。</param>
        /// <param name="target">调用方目标。</param>
        /// <returns>可绑定的场景对象。</returns>
        private static GameObject ResolveBindingOwner(LiveExportVersion version, object target)
        {
            GameObject owner = target as GameObject;
            if (target != null && owner == default) throw new ArgumentException("Binding target must be a scene GameObject.");
            if (owner == default)
            {
                if (version.Targets.Count != 1) throw new ArgumentException("A multi-target export requires an explicit target.");
                if (GlobalObjectId.TryParse(version.Targets[0].TargetId, out var identity))
                    owner = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity) as GameObject;
            }

            if (owner == default || !owner.scene.IsValid() || !owner.scene.isLoaded
                || string.IsNullOrEmpty(owner.scene.path) || EditorUtility.IsPersistent(owner))
                throw new InvalidOperationException("Binding requires a saved, loaded scene target.");
            if (owner.scene.isDirty) throw new InvalidOperationException("Binding never saves pre-existing scene edits.");
            return owner;
        }

        /// <summary>读取持久对象身份。</summary>
        /// <param name="owner">场景对象。</param>
        /// <returns>GlobalObjectId 文本。</returns>
        private static string RequireTargetIdentity(GameObject owner)
        {
            string targetId = GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();
            if (!UnityLiveFieldState.IsPersistentId(targetId)) throw new InvalidOperationException("Target identity is unavailable.");
            return targetId;
        }

        /// <summary>按对象身份查找捕获目标，并拒绝场景路径变化。</summary>
        /// <param name="version">导出版本。</param>
        /// <param name="targetId">对象身份。</param>
        /// <param name="scenePath">当前场景路径。</param>
        /// <returns>匹配的捕获目标；没有时返回 null。</returns>
        private static LiveExportTarget FindCapturedTarget(
            LiveExportVersion version, string targetId, string scenePath)
        {
            LiveExportTarget captured = null;
            foreach (var item in version.Targets)
                if (item.TargetId == targetId) captured = item;
            if (captured != null && captured.ScenePath != scenePath)
                throw new InvalidOperationException("Target scene changed.");
            return captured;
        }

        /// <summary>拒绝脚本 GUID 变化后的隐式续绑。</summary>
        /// <param name="ledger">绑定账本。</param>
        /// <param name="sourcePath">导出源码路径。</param>
        private static void RequireScriptGuid(BindingLedger ledger, string sourcePath)
        {
            if (string.IsNullOrEmpty(ledger.scriptGuid) || ledger.scriptGuid != AssetDatabase.AssetPathToGUID(sourcePath))
                throw new InvalidOperationException("Script GUID changed; binding refused.");
        }

        /// <summary>按对象身份查找既有绑定记录。</summary>
        /// <param name="ledger">绑定账本。</param>
        /// <param name="targetId">对象身份。</param>
        /// <returns>匹配记录；没有时返回 null。</returns>
        private static BindingRecord FindBinding(BindingLedger ledger, string targetId)
        {
            foreach (var item in ledger.bindings) if (item.targetId == targetId) return item;
            return null;
        }

        /// <summary>只接受零个或一个同类型组件。</summary>
        /// <param name="owner">场景对象。</param>
        /// <param name="type">导出组件类型。</param>
        /// <returns>唯一组件；不存在时返回 null。</returns>
        private static Component FindSingleComponent(GameObject owner, Type type)
        {
            Component[] components = owner.GetComponents(type);
            if (components.Length > 1) throw new InvalidOperationException("Multiple matching components are ambiguous.");
            return components.Length == 0 ? null : components[0];
        }

        /// <summary>
        /// 把旧版已绑定记录补成带组件身份的账本项。中断的旧绑定没有精确身份，必须先人工核对。
        /// </summary>
        /// <param name="store">导出存储。</param>
        /// <param name="exportId">导出标识。</param>
        /// <param name="version">导出版本。</param>
        /// <param name="captured">捕获目标。</param>
        /// <param name="ledger">绑定账本。</param>
        /// <param name="record">既有记录。</param>
        /// <param name="existing">场景上的既有组件。</param>
        /// <param name="targetId">对象身份。</param>
        /// <param name="owner">场景对象。</param>
        /// <returns>补齐后的记录；无需补齐时返回原记录。</returns>
        private BindingRecord AdoptLegacyBinding(
            LiveExportStore store, string exportId, LiveExportVersion version,
            LiveExportTarget captured, BindingLedger ledger, BindingRecord record,
            Component existing, string targetId, GameObject owner)
        {
            if (version.BatchId.Length != 0 || captured == null || record != null) return record;
            using var legacy = JsonDocument.Parse(LiveExportStore.ReadText(store.RecordPath(exportId)));
            bool wasBound = legacy.RootElement.TryGetProperty("bound", out var bound) && bound.ValueKind == JsonValueKind.True;
            bool wasBinding = legacy.RootElement.TryGetProperty("binding", out var binding) && binding.ValueKind == JsonValueKind.True;
            if (wasBinding) throw new InvalidOperationException("Interrupted legacy binding has no exact component identity; reconcile it first.");
            if (!wasBound) return record;
            if (existing == default) throw new InvalidOperationException("The legacy bound component is missing.");
            if (ledger.bindings.Count >= 256) throw new InvalidOperationException("At most 256 bound targets per script.");
            record = new BindingRecord
            {
                targetId = targetId, scenePath = owner.scene.path, exportId = exportId, state = "Bound",
                componentId = GlobalObjectId.GetGlobalObjectIdSlow(existing).ToString()
            };
            ledger.bindings.Add(record);
            return record;
        }

        /// <summary>确认场景上的组件与账本身份一致，不隐式收养未跟踪组件。</summary>
        /// <param name="version">导出版本。</param>
        /// <param name="ledger">绑定账本。</param>
        /// <param name="record">绑定记录。</param>
        /// <param name="existing">既有组件。</param>
        /// <param name="owner">场景对象。</param>
        /// <param name="exportId">导出标识。</param>
        /// <returns>脚本资源路径。</returns>
        private string ConfirmExisting(
            LiveExportVersion version, BindingLedger ledger, BindingRecord record,
            Component existing, GameObject owner, string exportId)
        {
            if (record == null || (record.state != "Bound" && record.state != "Saving")
                || record.scenePath != owner.scene.path
                || GlobalObjectId.GetGlobalObjectIdSlow(existing).ToString() != record.componentId)
                throw new InvalidOperationException("An untracked or replaced component exists; no implicit adoption.");
            record.exportId = exportId;
            record.state = "Bound";
            SaveBindings(ledger);
            return version.SourcePath;
        }

        /// <summary>
        /// 新增组件并保存场景。保存前失败会回滚 Undo；保存后失败保留场景，避免撤销已落盘内容。
        /// </summary>
        /// <param name="version">导出版本。</param>
        /// <param name="ledger">绑定账本。</param>
        /// <param name="record">绑定记录。</param>
        /// <param name="owner">场景对象。</param>
        /// <param name="type">组件类型。</param>
        /// <param name="captured">捕获字段。</param>
        /// <param name="exportId">导出标识。</param>
        /// <param name="targetId">对象身份。</param>
        /// <returns>脚本资源路径。</returns>
        private string AttachNew(
            LiveExportVersion version, BindingLedger ledger, BindingRecord record,
            GameObject owner, Type type, LiveExportTarget captured, string exportId, string targetId)
        {
            if (record != null && record.state != "Failed")
                throw new InvalidOperationException("The recorded component is missing; reconcile the binding before retrying.");
            if (record == null)
            {
                if (ledger.bindings.Count >= 256) throw new InvalidOperationException("At most 256 bound targets per script.");
                record = new BindingRecord { targetId = targetId, scenePath = owner.scene.path };
                ledger.bindings.Add(record);
            }

            record.exportId = exportId;
            record.state = "Adding";
            record.componentId = "";
            SaveBindings(ledger);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind live behaviour " + version.ClassName);
            bool saved = false;
            try
            {
                saved = SaveAttachedComponent(owner, type, captured, record, ledger);
                Undo.CollapseUndoOperations(group);
                return version.SourcePath;
            }
            catch
            {
                if (!saved)
                {
                    Undo.RevertAllDownToGroup(group);
                    record.state = "Failed";
                    record.componentId = "";
                    SaveBindings(ledger);
                }

                throw;
            }
        }

        /// <summary>挂载组件、恢复字段并保存场景。</summary>
        /// <param name="owner">场景对象。</param>
        /// <param name="type">组件类型。</param>
        /// <param name="captured">捕获字段。</param>
        /// <param name="record">绑定记录。</param>
        /// <param name="ledger">绑定账本。</param>
        /// <returns>场景保存成功时返回 true。</returns>
        private bool SaveAttachedComponent(
            GameObject owner, Type type, LiveExportTarget captured, BindingRecord record, BindingLedger ledger)
        {
            Component component = Undo.AddComponent(owner, type);
            if (component == default) throw new InvalidOperationException("Unity could not attach the component.");
            if (captured != null) JsonUtility.FromJson<UnityLiveFieldState>(captured.Fields).Restore(component, true);
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            record.componentId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            record.state = "Saving";
            SaveBindings(ledger);
            if (!EditorSceneManager.SaveScene(owner.scene)) throw new IOException("Could not save the target scene.");
            record.componentId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            record.state = "Bound";
            SaveBindings(ledger);
            return true;
        }

        private void WriteBindingStatus(RoslynJsonBuilder json, LiveExportVersion version)
        {
            json.Name("bindings").StartArray();
            foreach (var binding in ReadBindings(version.SourcePath).bindings)
                json.StartObject().Property("targetId", binding.targetId).Property("componentId", binding.componentId)
                    .Property("scenePath", binding.scenePath).Property("exportId", binding.exportId)
                    .Property("state", binding.state).EndObject();
            json.EndArray();
        }
    }
}
#endif
