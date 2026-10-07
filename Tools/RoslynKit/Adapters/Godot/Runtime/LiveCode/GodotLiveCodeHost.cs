#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class GodotLiveCodeHost : ILiveCodeHost, ILiveSnapshotHost, ILiveFieldHost
    {
        private sealed class Attachment : IDisposable
        {
            internal GodotLiveBehaviourHost Host;
            internal LiveMethodInvoker Invoker;
            internal readonly Dictionary<string, LiveFieldBinding> Fields = new Dictionary<string, LiveFieldBinding>();
            public void Dispose()
            {
                if (Host == null || !GodotObject.IsInstanceValid(Host)) return;
                Host.Stop();
                Host.Free();
                Host = null;
                Fields.Clear();
            }
        }
        private readonly Func<string, string, object[], object> mInvoke;
        public GodotLiveCodeHost(Func<string, string, object[], object> invoke) { mInvoke = invoke; }
        public string WrapBehaviour(string className, string members, bool persistent)
        {
            LiveCodeManager.ValidateName(className);
            if (persistent) throw new NotSupportedException("Godot prototype export is not a snapshot recovery operation.");
            return "using System;\nusing Godot;\npublic sealed class " + className
                + " : YokiFrame.GodotLiveBehaviour {\n#line 1 \"members.cs\"\n" + members + "\n#line default\n}\n";
        }
        public IDisposable Prepare(string id, object target, Type behaviourType, string previousState)
        {
            var owner = target as Node;
            if (owner == null || !GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree() || owner.IsQueuedForDeletion())
                throw new ArgumentException("An active scene Node is required.");
            var instance = Activator.CreateInstance(behaviourType) as GodotLiveBehaviour;
            if (instance == null) throw new ArgumentException("Invalid Godot prototype type.");
            instance.Node = owner;
            Capture(instance, false, id, null, out _);
            if (!string.IsNullOrEmpty(previousState)) RestoreFields(instance, previousState, null, false);
            var host = new GodotLiveBehaviourHost { Name = "YokiLive_" + id };
            var attachment = new Attachment { Host = host };
            try
            {
                host.Configure(id, instance, mInvoke, owner);
                owner.AddChild(host);
                attachment.Invoker = new LiveMethodInvoker(instance);
                return attachment;
            }
            catch { attachment.Dispose(); throw; }
        }
        public bool IsAlive(IDisposable attachment)
        {
            var item = attachment as Attachment;
            return item != null && item.Host != null && GodotObject.IsInstanceValid(item.Host)
                && item.Host.IsInsideTree() && !item.Host.IsQueuedForDeletion() && !item.Host.Faulted && item.Host.Instance != null;
        }
        public void Activate(IDisposable attachment) => Require(attachment).Host.Activate();
        public void Suspend(IDisposable attachment) => Require(attachment).Host.Suspend();
        public object ReadField(IDisposable attachment, string name) => BindTunableField(attachment, name).Read();
        public void SetField(IDisposable attachment, string name, object value)
        {
            var binding = BindTunableField(attachment, name);
            if (value == null ? binding.ValueType.IsValueType : !binding.ValueType.IsInstanceOfType(value))
                throw new ArgumentException("Field value type mismatch.");
            binding.Write(value);
        }
        public object Invoke(IDisposable attachment, string method, object[] arguments) =>
            Require(attachment).Invoker.Invoke(method, arguments);
        public string CaptureState(IDisposable attachment) =>
            Capture(Require(attachment).Host.Instance, false, "", null, out _);
        public LiveFieldBinding BindTunableField(IDisposable attachment, string name)
        {
            var item = Require(attachment);
            if (!item.Fields.TryGetValue(name, out var binding))
                item.Fields.Add(name, binding = new LiveFieldBinding(item.Host.Instance, RequireField(item.Host.Instance, name)));
            return binding;
        }
        public object DecodeTunableValue(Type type, JsonElement value)
        {
            if (LiveFieldValues.TryDecodeScalar(type, value, out var scalar)) return scalar;
            int size = type == typeof(Vector2) ? 2 : type == typeof(Vector3) ? 3 :
                type == typeof(Vector4) || type == typeof(Quaternion) || type == typeof(Color) ? 4 : 0;
            if (size == 0) throw new NotSupportedException("Unsupported Godot field type: " + type.FullName);
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != size)
                throw new ArgumentException("Godot vector/color requires a numeric array of length " + size + ".");
            float x = LiveFieldValues.ReadSingle(value[0]), y = LiveFieldValues.ReadSingle(value[1]);
            float z = size > 2 ? LiveFieldValues.ReadSingle(value[2]) : 0;
            float w = size > 3 ? LiveFieldValues.ReadSingle(value[3]) : 0;
            if (type == typeof(Vector2)) return new Vector2(x, y);
            if (type == typeof(Vector3)) return new Vector3(x, y, z);
            if (type == typeof(Vector4)) return new Vector4(x, y, z, w);
            if (type == typeof(Quaternion)) return new Quaternion(x, y, z, w);
            return new Color(x, y, z, w);
        }
        public LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject)
        {
            var host = Require(attachment).Host;
            if (!host.Active || !host.CanProcess()) throw new NotSupportedException("Inactive Godot behaviour cannot be snapshotted.");
            string fields = Capture(host.Instance, true, id, keyForObject, out var references);
            return new LiveSnapshotState(Identity(host.Instance.Node, keyForObject(host.Instance.Node, id + ":target")), fields, references);
        }
        public object ResolveSnapshotObject(LiveObjectIdentity identity, Func<LiveObjectIdentity, object> resolver)
        {
            if (resolver == null) throw new InvalidOperationException("Explicit Godot object mapping is required: " + identity.Key);
            var value = resolver(identity) as GodotObject;
            if (value == null || !GodotObject.IsInstanceValid(value) || value.GetType().FullName != identity.TypeName)
                throw new InvalidOperationException("Godot object mapping type/lifetime mismatch: " + identity.Key);
            if (value is Node node && (!node.IsInsideTree() || node.IsQueuedForDeletion() || ScenePath(node) != identity.ScenePath))
                throw new InvalidOperationException("Godot node must be in the original scene: " + identity.Key);
            return value;
        }
        public void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references) =>
            RestoreFields(Require(attachment).Host.Instance, fields, references, true);
        public void ValidateSnapshotState(LiveSnapshotState state)
        {
            LiveSnapshotStore.ValidateSnapshotFieldText(state.Fields);
            using var document = JsonDocument.Parse(state.Fields);
            var values = document.RootElement.GetProperty("values");
            if (values.GetArrayLength() > 256) throw new ArgumentException("Too many fields.");
            var names = new HashSet<string>();
            var refs = new HashSet<string>();
            foreach (var reference in state.References) refs.Add(reference.Field);
            foreach (var item in values.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString();
                if (string.IsNullOrEmpty(name) || !names.Add(name)
                    || item.GetProperty("instanceId").GetString() != ""
                    || item.GetProperty("reference").GetBoolean() != refs.Remove(name))
                    throw new ArgumentException("Invalid Godot field snapshot.");
            }
            if (refs.Count > 0) throw new ArgumentException("Unknown reference fields.");
        }
        public string Export(string id, string className, string source, string sourceHash, IDisposable attachment, string outputPath) =>
            throw new NotSupportedException("Godot prototype export/bind is not implemented; use normal Godot C# scripts.");
        public string Bind(string exportId, object target) => throw new NotSupportedException("Godot persistent binding is not implemented.");
        private Attachment Require(IDisposable value)
        {
            if (!IsAlive(value)) throw new InvalidOperationException("Godot live behaviour is unavailable.");
            return (Attachment)value;
        }
        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (field.DeclaringType == type && !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), false)
                    && (field.IsPublic || field.IsDefined(typeof(ExportAttribute), false))) yield return field;
        }
        private static FieldInfo RequireField(object instance, string name)
        {
            foreach (var field in Fields(instance.GetType())) if (field.Name == name) return field;
            throw new ArgumentException("Only declared public/Export fields are accessible: " + name);
        }
        private string Capture(object instance, bool snapshot, string id, Func<object, string, string> key,
            out List<LiveSnapshotReference> references)
        {
            references = new List<LiveSnapshotReference>();
            var json = new RoslynJsonBuilder().StartObject().Name("values").StartArray();
            int count = 0;
            foreach (var field in Fields(instance.GetType()))
            {
                if (++count > 256) throw new NotSupportedException("Godot snapshot supports 256 fields.");
                object value = field.GetValue(instance);
                var reference = value as GodotObject;
                bool hasReference = reference != null && GodotObject.IsInstanceValid(reference);
                if (reference is GodotLiveBehaviourHost) throw new NotSupportedException("Transient host references cannot be restored.");
                if (hasReference && snapshot)
                    references.Add(new LiveSnapshotReference(field.Name, Identity(reference, key(reference, id + ":field:" + field.Name))));
                string text = typeof(GodotObject).IsAssignableFrom(field.FieldType) ? "" : Encode(field.FieldType, value);
                json.StartObject().Property("name", field.Name).Property("type", field.FieldType.FullName)
                    .Property("text", text).Property("isNull", value == null).Property("reference", hasReference)
                    .Property("instanceId", hasReference && !snapshot ? reference.GetInstanceId().ToString(CultureInfo.InvariantCulture) : "").EndObject();
            }
            return json.EndArray().EndObject().ToString();
        }
        private void RestoreFields(object instance, string fields, IReadOnlyDictionary<string, object> references, bool snapshot)
        {
            using var document = JsonDocument.Parse(fields);
            var values = document.RootElement.GetProperty("values");
            int count = 0;
            foreach (var field in Fields(instance.GetType())) count++;
            if (snapshot && count != values.GetArrayLength()) throw new InvalidOperationException("Field set changed.");
            var seen = new HashSet<string>();
            foreach (var item in values.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString();
                if (!seen.Add(name)) throw new ArgumentException("Duplicate field.");
                FieldInfo field = null;
                foreach (var candidate in Fields(instance.GetType())) if (candidate.Name == name) { field = candidate; break; }
                if (field == null && !snapshot) continue;
                if (field == null || field.FieldType.FullName != item.GetProperty("type").GetString())
                    throw new InvalidOperationException("Field type changed: " + name);
                object value;
                if (typeof(GodotObject).IsAssignableFrom(field.FieldType))
                {
                    value = null;
                    if (item.GetProperty("reference").GetBoolean())
                    {
                        if (snapshot)
                        {
                            if (references == null || !references.TryGetValue(name, out value)) throw new InvalidOperationException("Reference missing: " + name);
                        }
                        else value = GodotObject.InstanceFromId(ulong.Parse(item.GetProperty("instanceId").GetString(), CultureInfo.InvariantCulture));
                        if (!(value is GodotObject obj) || !GodotObject.IsInstanceValid(obj) || !field.FieldType.IsInstanceOfType(value))
                            throw new InvalidOperationException("Reference invalid: " + name);
                    }
                }
                else if (field.FieldType == typeof(string)) value = item.GetProperty("isNull").GetBoolean() ? null : item.GetProperty("text").GetString();
                else if (field.FieldType.IsEnum) value = Enum.Parse(field.FieldType, item.GetProperty("text").GetString());
                else
                {
                    using var encoded = JsonDocument.Parse(item.GetProperty("text").GetString());
                    value = DecodeTunableValue(field.FieldType, encoded.RootElement);
                }
                field.SetValue(instance, value);
            }
        }
        private static string Encode(Type type, object value)
        {
            if (type == typeof(string)) return (string)value ?? "";
            if (type.IsEnum) return value.ToString();
            if (type == typeof(bool)) return (bool)value ? "true" : "false";
            if (type == typeof(int)) return ((int)value).ToString(CultureInfo.InvariantCulture);
            if (type == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number)) throw new NotSupportedException("Nonfinite field.");
                return number.ToString("R", CultureInfo.InvariantCulture);
            }
            if (type == typeof(Vector2)) { var v = (Vector2)value; return Numbers(v.X, v.Y); }
            if (type == typeof(Vector3)) { var v = (Vector3)value; return Numbers(v.X, v.Y, v.Z); }
            if (type == typeof(Vector4)) { var v = (Vector4)value; return Numbers(v.X, v.Y, v.Z, v.W); }
            if (type == typeof(Quaternion)) { var v = (Quaternion)value; return Numbers(v.X, v.Y, v.Z, v.W); }
            if (type == typeof(Color)) { var v = (Color)value; return Numbers(v.R, v.G, v.B, v.A); }
            throw new NotSupportedException("Unsupported Godot snapshot field: " + type.FullName);
        }
        private static string Numbers(params float[] values)
        {
            var text = new System.Text.StringBuilder("[");
            for (int index = 0; index < values.Length; index++)
            {
                if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new NotSupportedException("Nonfinite field.");
                if (index > 0) text.Append(',');
                text.Append(values[index].ToString("R", CultureInfo.InvariantCulture));
            }
            return text.Append(']').ToString();
        }
        private static LiveObjectIdentity Identity(GodotObject value, string key) =>
            new LiveObjectIdentity(key, "", value is Node node ? ScenePath(node) : "",
                value.GetType().FullName, value is Node named ? named.Name.ToString() : value.GetType().Name);
        private static string ScenePath(Node node)
        {
            for (Node current = node; current != null; current = current.GetParent())
                if (!string.IsNullOrEmpty(current.SceneFilePath)) return current.SceneFilePath;
            return "";
        }
    }
}
#endif
