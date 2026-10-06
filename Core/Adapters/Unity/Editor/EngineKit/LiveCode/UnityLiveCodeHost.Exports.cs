#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YokiFrame.Json;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    public sealed partial class UnityLiveCodeHost
    {
        private const string ExportMarker = "YokiFrame.LiveExport";
        private YokiFrameLiveExportStore ExportStore => new YokiFrameLiveExportStore(mProjectRoot);

        public YokiFrameLiveExportTarget CaptureExportTarget(IDisposable attachment)
        {
            var live = Require(attachment).Host;
            var owner = live.gameObject;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();
            if (string.IsNullOrEmpty(owner.scene.path) || !UnityLiveSnapshotIdentities.IsSaved(owner, id))
                throw new NotSupportedException("Export needs an object recorded in a clean saved scene before Play.");
            var fields = UnityLiveFieldState.Capture(live.Instance, true);
            foreach (var field in fields.values)
            {
                if (field.instanceId == 0) continue;
                Object reference = EditorUtility.InstanceIDToObject(field.instanceId);
                if (reference == default || reference is YokiFrameUnityLiveBehaviourHost
                    || !UnityLiveSnapshotIdentities.IsSaved(reference, field.globalId))
                    throw new NotSupportedException("Export field has a runtime-only reference: " + field.name);
            }
            return new YokiFrameLiveExportTarget
            { TargetId = id, ScenePath = owner.scene.path, Fields = JsonUtility.ToJson(fields) };
        }

        public string VersionExportSource(string source, string exportId)
        {
            if (!Guid.TryParseExact(exportId, "N", out _) || !source.StartsWith(SourceHeader, StringComparison.Ordinal))
                throw new ArgumentException("Invalid generated export source or ID.");
            return SourceHeader + "[assembly: System.Reflection.AssemblyMetadata(\"" + ExportMarker + "\", \"" + exportId + "\")]\n"
                + source.Substring(SourceHeader.Length);
        }

        public YokiFrameLiveExportVersion ReadExport(string exportId) => ExportStore.ReadVersion(exportId);

        public YokiFrameLiveExportVersion CreateReexport(string previousExportId, string members, string batchId)
        {
            var store = ExportStore;
            var previous = store.ReadVersion(previousExportId);
            var version = new YokiFrameLiveExportVersion
            {
                ExportId = Guid.NewGuid().ToString("N"), BatchId = batchId, PreviousExportId = previousExportId,
                ClassName = previous.ClassName, SourcePath = previous.SourcePath, PreviousMetaHash = store.MetaHash(previous)
            };
            version.Source = VersionExportSource(WrapBehaviour(version.ClassName, members, true), version.ExportId);
            version.SourceHash = YokiFrameLiveCodeManager.Hash(version.Source);
            // Existing components retain their current fields. Captured defaults are only for not-yet-bound targets.
            foreach (var target in previous.Targets) version.Targets.Add(target);
            store.ValidateCurrent(version);
            return version;
        }

        public YokiFrameLiveExportBatch StageExports(string batchId, IReadOnlyList<YokiFrameLiveExportVersion> versions)
        {
            foreach (var version in versions)
            {
                if (version.PreviousExportId.Length != 0) continue;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type existing = assembly.GetType(version.ClassName, false);
                    if (existing != null && typeof(MonoBehaviour).IsAssignableFrom(existing))
                        throw new InvalidOperationException("A persistent class already exists; use Reexport: " + version.ClassName);
                }
            }
            return ExportStore.Stage(batchId, versions);
        }

        public void QueueExportCommit(string batchId, Action guard)
        {
            guard();
            var store = ExportStore;
            if (!store.MarkQueued(batchId)) return;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    guard();
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                        throw new InvalidOperationException("Export commit requires an idle editor.");
                    YokiFrameLiveExportBatch result;
                    // No awaits here: release all editor locks before asking Unity to import.
                    EditorApplication.LockReloadAssemblies();
                    try
                    {
                        AssetDatabase.DisallowAutoRefresh();
                        try
                        {
                            AssetDatabase.StartAssetEditing();
                            try { result = store.Commit(batchId); }
                            finally { AssetDatabase.StopAssetEditing(); }
                        }
                        finally { AssetDatabase.AllowAutoRefresh(); }
                    }
                    finally { EditorApplication.UnlockReloadAssemblies(); }
                    if (result.State == "Committed" || result.State == "Partial") AssetDatabase.Refresh();
                }
                catch (Exception error)
                {
                    try { store.FailQueued(batchId, error.Message); }
                    catch (Exception persistence) { Debug.LogException(persistence); }
                    Debug.LogException(error);
                }
            };
        }

        private static bool HasCompiledVersion(Type type, string exportId)
        {
            if (type == null) return false;
            foreach (var attribute in type.Assembly.GetCustomAttributesData())
                if (attribute.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute"
                    && attribute.ConstructorArguments.Count == 2
                    && (string)attribute.ConstructorArguments[0].Value == ExportMarker
                    && (string)attribute.ConstructorArguments[1].Value == exportId) return true;
            return false;
        }

        private Type ExportedType(YokiFrameLiveExportVersion version)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(version.SourcePath);
            Type type = script == default ? null : script.GetClass();
            if (type == null || type.FullName != version.ClassName || !typeof(MonoBehaviour).IsAssignableFrom(type))
                return null;
            return version.BatchId.Length == 0 || HasCompiledVersion(type, version.ExportId) ? type : null;
        }

        public string ReadExportStatus(string batchId, string exportId)
        {
            var store = ExportStore;
            if (string.IsNullOrEmpty(batchId))
            {
                var version = store.ReadVersion(exportId);
                batchId = version.BatchId;
                if (batchId.Length == 0)
                {
                    var legacy = new YokiFrameEngineJsonBuilder().StartObject().Property("operation", "live_export_status")
                        .Property("state", "Legacy").Name("versions").StartArray();
                    WriteVersionStatus(legacy, version, store);
                    return legacy.EndArray().EndObject().ToString();
                }
            }
            var batch = store.ReadBatch(batchId);
            var json = new YokiFrameEngineJsonBuilder().StartObject().Property("operation", "live_export_status")
                .Property("batchId", batch.BatchId).Property("state", batch.State).Property("error", batch.Error)
                .Name("versions").StartArray();
            foreach (string id in batch.ExportIds) WriteVersionStatus(json, store.ReadVersion(id), store);
            return json.EndArray().EndObject().ToString();
        }

        private void WriteVersionStatus(YokiFrameEngineJsonBuilder json, YokiFrameLiveExportVersion version, YokiFrameLiveExportStore store)
        {
            bool matches = store.MatchesSource(version);
            json.StartObject().Property("exportId", version.ExportId).Property("previousExportId", version.PreviousExportId)
                .Property("className", version.ClassName).Property("sourcePath", version.SourcePath)
                .Property("sourceHash", version.SourceHash).Property("sourceMatches", matches)
                .Property("compiled", matches && !EditorApplication.isCompiling && ExportedType(version) != null)
                .Name("targets").StartArray();
            foreach (var target in version.Targets)
                json.StartObject().Property("targetId", target.TargetId).Property("scenePath", target.ScenePath).EndObject();
            json.EndArray();
            WriteBindingStatus(json, version);
            json.EndObject();
        }
    }
}
#endif
