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
            ".yokiframe", "engine", "live-exports", "bindings", YokiFrameLiveCodeManager.Hash(sourcePath) + ".json");

        private BindingLedger ReadBindings(string sourcePath)
        {
            string path = BindingPath(sourcePath);
            if (!File.Exists(path)) return new BindingLedger { sourcePath = sourcePath, scriptGuid = AssetDatabase.AssetPathToGUID(sourcePath) };
            var ledger = JsonUtility.FromJson<BindingLedger>(YokiFrameLiveExportStore.ReadText(path));
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
            if (File.Exists(path)) YokiFrameLiveExportStore.Replace(path, text);
            else YokiFrameLiveExportStore.WriteNew(path, text);
        }

        private string BindVersioned(string exportId, object target)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Binding requires idle Edit Mode after normal Unity compilation.");
            var store = ExportStore;
            var version = store.ReadVersion(exportId);
            if (version.BatchId.Length > 0 && store.ReadBatch(version.BatchId).State != "Committed")
                throw new InvalidOperationException("Export batch has not committed.");
            if (!store.MatchesSource(version)) throw new InvalidOperationException("ExportConflict: source changed.");
            Type type = ExportedType(version);
            if (type == null) throw new InvalidOperationException("The requested export version has not compiled.");
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
            string targetId = GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();
            if (!UnityLiveFieldState.IsPersistentId(targetId)) throw new InvalidOperationException("Target identity is unavailable.");
            YokiFrameLiveExportTarget captured = null;
            foreach (var item in version.Targets)
                if (item.TargetId == targetId) captured = item;
            if (captured != null && captured.ScenePath != owner.scene.path)
                throw new InvalidOperationException("Target scene changed.");
            var ledger = ReadBindings(version.SourcePath);
            if (string.IsNullOrEmpty(ledger.scriptGuid) || ledger.scriptGuid != AssetDatabase.AssetPathToGUID(version.SourcePath))
                throw new InvalidOperationException("Script GUID changed; binding refused.");
            BindingRecord record = null;
            foreach (var item in ledger.bindings) if (item.targetId == targetId) record = item;
            Component[] components = owner.GetComponents(type);
            if (components.Length > 1) throw new InvalidOperationException("Multiple matching components are ambiguous.");
            Component existing = components.Length == 0 ? null : components[0];

            if (version.BatchId.Length == 0 && captured != null && record == null)
            {
                using var legacy = JsonDocument.Parse(YokiFrameLiveExportStore.ReadText(store.RecordPath(exportId)));
                bool wasBound = legacy.RootElement.TryGetProperty("bound", out var bound) && bound.ValueKind == JsonValueKind.True;
                bool wasBinding = legacy.RootElement.TryGetProperty("binding", out var binding) && binding.ValueKind == JsonValueKind.True;
                if (wasBinding) throw new InvalidOperationException("Interrupted legacy binding has no exact component identity; reconcile it first.");
                if (wasBound)
                {
                    if (existing == default) throw new InvalidOperationException("The legacy bound component is missing.");
                    if (ledger.bindings.Count >= 256) throw new InvalidOperationException("At most 256 bound targets per script.");
                    // Schema 1 only attested to a type on its original target. Record the identity once, without changing history.
                    record = new BindingRecord
                    {
                        targetId = targetId, scenePath = owner.scene.path, exportId = exportId, state = "Bound",
                        componentId = GlobalObjectId.GetGlobalObjectIdSlow(existing).ToString()
                    };
                    ledger.bindings.Add(record);
                }
            }
            if (existing != default)
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
            if (record != null && record.state != "Failed")
                throw new InvalidOperationException("The recorded component is missing; reconcile the binding before retrying.");
            if (record == null)
            {
                if (ledger.bindings.Count >= 256) throw new InvalidOperationException("At most 256 bound targets per script.");
                record = new BindingRecord { targetId = targetId, scenePath = owner.scene.path };
                ledger.bindings.Add(record);
            }
            record.exportId = exportId; record.state = "Adding"; record.componentId = "";
            SaveBindings(ledger);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind live behaviour " + version.ClassName);
            bool saved = false;
            try
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
                saved = true;
                record.componentId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
                record.state = "Bound";
                SaveBindings(ledger);
                Undo.CollapseUndoOperations(group);
                return version.SourcePath;
            }
            catch
            {
                if (!saved)
                {
                    Undo.RevertAllDownToGroup(group);
                    record.state = "Failed"; record.componentId = "";
                    SaveBindings(ledger);
                }
                throw;
            }
        }

        private void WriteBindingStatus(YokiFrameEngineJsonBuilder json, YokiFrameLiveExportVersion version)
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
