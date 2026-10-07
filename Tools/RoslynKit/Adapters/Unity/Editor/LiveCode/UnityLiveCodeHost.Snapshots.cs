#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    public sealed partial class UnityLiveCodeHost
    {
        public LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment,
            Func<object, string, string> keyForObject)
        {
            var value = Require(attachment);
            if (!value.Host.isActiveAndEnabled)
                throw new NotSupportedException("Inactive or suspended behaviours are not supported by snapshot v1.");
            object instance = value.Host.Instance;
            var state = UnityLiveFieldState.Capture(instance, false);
            int roots = 0;
            foreach (var field in state.values) if (UnityLiveFieldState.IsRoot(field.name)) roots++;
            if (roots > 256) throw new NotSupportedException("Snapshot supports at most 256 root fields per behaviour.");
            var references = new List<LiveSnapshotReference>();
            foreach (var field in state.values)
            {
                if (field.instanceId != default)
                {
#if UNITY_6000_5_OR_NEWER
                    Object reference = EditorUtility.EntityIdToObject(field.instanceId);
#else
                    Object reference = EditorUtility.InstanceIDToObject(field.instanceId);
#endif
                    if (reference == default) throw new InvalidOperationException("Field object disappeared: " + field.name);
                    references.Add(new LiveSnapshotReference(field.name,
                        DescribeObject(reference, keyForObject(reference, id + ":field:" + field.name))));
                    if (references.Count > 256) throw new NotSupportedException("Snapshot supports at most 256 object references per behaviour.");
                    field.hasReference = true;
                }
                // Old instance IDs must never survive into a recovery record.
                field.instanceId = default;
                field.globalId = "";
            }
            var owner = value.Host.gameObject;
            return new LiveSnapshotState(DescribeObject(owner, keyForObject(owner, id + ":target")),
                JsonUtility.ToJson(state), references);
        }

        private static LiveObjectIdentity DescribeObject(Object value, string runtimeKey)
        {
            if (value is UnityLiveBehaviourHost)
                throw new NotSupportedException("References to transient live hosts cannot be restored.");
            string globalId = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
            if (!UnityLiveSnapshotIdentities.IsSaved(value, globalId)) globalId = "";
            if (globalId.Length > 0 && GlobalObjectId.TryParse(globalId, out var parsed)
                && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) != value)
                globalId = "";
            string key = globalId.Length == 0 ? runtimeKey : "global:" + globalId;
            return new LiveObjectIdentity(key, globalId, ScenePath(value), value.GetType().FullName, value.name);
        }

        public object ResolveSnapshotObject(LiveObjectIdentity identity,
            Func<LiveObjectIdentity, object> resolver)
        {
            Object value;
            if (!string.IsNullOrEmpty(identity.GlobalId))
            {
                if (!UnityLiveFieldState.IsPersistentId(identity.GlobalId)
                    || !GlobalObjectId.TryParse(identity.GlobalId, out var global))
                    throw new InvalidOperationException("Invalid persistent object identity: " + identity.Key);
                value = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global);
                if (value == default || GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() != identity.GlobalId
                    || ScenePath(value) != identity.ScenePath)
                    throw new InvalidOperationException("Load the original scene/asset before restoring: " + identity.Key);
            }
            else
            {
                if (resolver == null)
                    throw new InvalidOperationException("Explicit runtime object mapping is required: " + identity.Key);
                value = resolver(identity) as Object;
                if (value == default || EditorUtility.IsPersistent(value)
                    || ((value is GameObject || value is Component) && !HasLiveScene(value))
                    || (!string.IsNullOrEmpty(identity.ScenePath) && ScenePath(value) != identity.ScenePath))
                    throw new InvalidOperationException("Runtime mapping must return a live object in its original scene, when applicable: " + identity.Key);
            }
            if (value == default || value.GetType().FullName != identity.TypeName)
                throw new InvalidOperationException("Object type mismatch: " + identity.Key);
            return value;
        }

        public void RestoreSnapshotFields(IDisposable attachment, string fields,
            IReadOnlyDictionary<string, object> references)
        {
            LiveSnapshotStore.ValidateSnapshotFieldText(fields);
            object instance = Require(attachment).Host.Instance;
            var state = JsonUtility.FromJson<UnityLiveFieldState>(fields);
            if (state == null || state.values == null || state.values.Count > LiveFieldValues.MaxNodes)
                throw new InvalidOperationException("Snapshot field set does not match the source.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            int usedReferences = 0;
            foreach (var item in state.values)
            {
                if (item == null || !names.Add(item.name) || item.instanceId != default || !string.IsNullOrEmpty(item.globalId))
                    throw new InvalidOperationException("Invalid snapshot field or old instance identity.");
                if (references.TryGetValue(item.name, out var resolved))
                {
                    Object reference = resolved as Object;
                    if (!item.hasReference || reference == default)
                        throw new InvalidOperationException("Snapshot field reference mismatch: " + item.name);
#if UNITY_6000_5_OR_NEWER
                    item.instanceId = reference.GetEntityId();
#else
                    item.instanceId = reference.GetInstanceID();
#endif
                    usedReferences++;
                }
                else if (item.hasReference)
                    throw new InvalidOperationException("Snapshot field reference missing: " + item.name);
            }
            if (usedReferences != references.Count)
                throw new InvalidOperationException("Snapshot contains unknown field references.");
            state.Restore(instance, false, true, references);
        }

        public void ValidateSnapshotState(LiveSnapshotState snapshot)
        {
            LiveSnapshotStore.ValidateSnapshotFieldText(snapshot.Fields);
            var state = JsonUtility.FromJson<UnityLiveFieldState>(snapshot.Fields);
            if (state == null || state.values == null || state.values.Count > LiveFieldValues.MaxNodes)
                throw new InvalidOperationException("Invalid snapshot field set.");
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in snapshot.References) referenced.Add(reference.Field);
            var names = new HashSet<string>(StringComparer.Ordinal);
            int roots = 0;
            foreach (var item in state.values)
            {
                if (item == null || string.IsNullOrEmpty(item.name) || string.IsNullOrEmpty(item.type)
                    || !names.Add(item.name) || item.instanceId != default || !string.IsNullOrEmpty(item.globalId)
                    || item.hasReference != referenced.Remove(item.name))
                    throw new InvalidOperationException("Invalid snapshot field or reference mapping.");
                if (UnityLiveFieldState.IsRoot(item.name)) roots++;
            }
            if (roots > 256 || referenced.Count != 0) throw new InvalidOperationException("Invalid snapshot fields or references.");
        }

        private static string ScenePath(Object value)
        {
            if (value is GameObject owner) return owner.scene.path;
            if (value is Component component) return component.gameObject.scene.path;
            return "";
        }

        private static bool HasLiveScene(Object value)
        {
            if (value is GameObject owner) return owner.scene.IsValid() && owner.scene.isLoaded;
            if (value is Component component) return component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
            return false;
        }
    }
}
#endif
