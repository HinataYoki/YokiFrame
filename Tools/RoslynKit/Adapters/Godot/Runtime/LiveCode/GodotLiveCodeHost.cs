#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godot;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Godot LiveCode 宿主：把原型挂到已有节点，并负责字段、快照和调用。</summary>
    public sealed class GodotLiveCodeHost : ILiveCodeHost, ILiveSnapshotHost, ILiveFieldHost
    {
        private sealed class Attachment : IDisposable
        {
            internal GodotLiveBehaviourHost Host;
            internal LiveMethodInvoker Invoker;
            internal readonly Dictionary<string, LiveFieldBinding> Fields = new Dictionary<string, LiveFieldBinding>();

            /// <summary>停止并释放宿主。宿主已失效时只清字段缓存，不再调用 Godot。</summary>
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

        /// <summary>创建宿主。调用委托由 LiveCodeManager 提供，避免宿主反向依赖管理器。</summary>
        /// <param name="invoke">跨原型调用入口。</param>
        public GodotLiveCodeHost(Func<string, string, object[], object> invoke) { mInvoke = invoke; }

        /// <summary>把成员包进 GodotLiveBehaviour。持久导出不是快照恢复，直接拒绝。</summary>
        /// <param name="className">已校验的类名。</param>
        /// <param name="members">原型成员源码。</param>
        /// <param name="persistent">为 true 时拒绝，不生成可落盘包装。</param>
        /// <returns>可编译的包装源码。</returns>
        public string WrapBehaviour(string className, string members, bool persistent)
        {
            LiveCodeManager.ValidateName(className);
            if (persistent) throw new NotSupportedException("Godot prototype export is not a snapshot recovery operation.");
            return "using System;\nusing Godot;\npublic sealed class " + className
                + " : YokiFrame.GodotLiveBehaviour {\n#line 1 \"members.cs\"\n" + members + "\n#line default\n}\n";
        }

        /// <summary>把原型挂到活动节点。配置失败时释放已创建宿主，不留下半挂接节点。</summary>
        /// <param name="id">LiveCode 标识。</param>
        /// <param name="target">必须是仍在场景树中的 Node。</param>
        /// <param name="behaviourType">GodotLiveBehaviour 派生类型。</param>
        /// <param name="previousState">上次字段状态；为空时不恢复。</param>
        /// <returns>可释放的挂接。</returns>
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

        /// <summary>判断挂接是否仍可调用。已出树、排队删除、故障或原型被清空都视为不可用。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <returns>仍可用时返回 true。</returns>
        public bool IsAlive(IDisposable attachment)
        {
            var item = attachment as Attachment;
            return item != null && item.Host != null && GodotObject.IsInstanceValid(item.Host)
                && item.Host.IsInsideTree() && !item.Host.IsQueuedForDeletion() && !item.Host.Faulted && item.Host.Instance != null;
        }

        /// <summary>激活挂接。不可用时抛出，不把异常吞掉。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        public void Activate(IDisposable attachment) => Require(attachment).Host.Activate();

        /// <summary>暂停挂接。不可用时抛出，不改其他原型。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        public void Suspend(IDisposable attachment) => Require(attachment).Host.Suspend();

        /// <summary>读取可调字段。字段不存在或挂接不可用时抛出。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="name">声明在原型上的字段名。</param>
        /// <returns>当前字段值。</returns>
        public object ReadField(IDisposable attachment, string name) => BindTunableField(attachment, name).Read();

        /// <summary>写入可调字段。null 不能写入值类型，类型不匹配时不写。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="name">声明在原型上的字段名。</param>
        /// <param name="value">新值。</param>
        public void SetField(IDisposable attachment, string name, object value)
        {
            var binding = BindTunableField(attachment, name);
            if (value == null ? binding.ValueType.IsValueType : !binding.ValueType.IsInstanceOfType(value))
                throw new ArgumentException("Field value type mismatch.");
            binding.Write(value);
        }

        /// <summary>调用原型方法。参数转换失败由调用器抛出。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="method">方法名。</param>
        /// <param name="arguments">方法参数。</param>
        /// <returns>方法返回值。</returns>
        public object Invoke(IDisposable attachment, string method, object[] arguments) =>
            Require(attachment).Invoker.Invoke(method, arguments);

        /// <summary>捕获当前字段状态。Godot 对象引用只记录实例标识，不进快照引用表。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <returns>字段状态 JSON。</returns>
        public string CaptureState(IDisposable attachment) =>
            Capture(Require(attachment).Host.Instance, false, "", null, out _);

        /// <summary>绑定可调字段并缓存。重复绑定返回同一对象，避免每次反射。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="name">声明在原型上的字段名。</param>
        /// <returns>字段绑定。</returns>
        public LiveFieldBinding BindTunableField(IDisposable attachment, string name)
        {
            var item = Require(attachment);
            if (!item.Fields.TryGetValue(name, out var binding))
                item.Fields.Add(name, binding = new LiveFieldBinding(item.Host.Instance, RequireField(item.Host.Instance, name)));
            return binding;
        }

        /// <summary>把 JSON 解码成 Godot 可调值。标量交给共享解码器，向量和颜色必须是定长数组。</summary>
        /// <param name="type">目标字段类型。</param>
        /// <param name="value">JSON 值。</param>
        /// <returns>可写入字段的值。</returns>
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

        /// <summary>捕获快照。未激活或当前不能处理的原型拒绝快照，避免保存一个不会再跑的状态。</summary>
        /// <param name="id">LiveCode 标识，用于生成引用键。</param>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="keyForObject">为 Godot 对象分配稳定键的委托。</param>
        /// <returns>目标身份、字段和引用。</returns>
        public LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject)
        {
            var host = Require(attachment).Host;
            if (!host.Active || !host.CanProcess()) throw new NotSupportedException("Inactive Godot behaviour cannot be snapshotted.");
            string fields = Capture(host.Instance, true, id, keyForObject, out var references);
            return new LiveSnapshotState(Identity(host.Instance.Node, keyForObject(host.Instance.Node, id + ":target")), fields, references);
        }

        /// <summary>按显式映射恢复 Godot 对象。没有映射、类型不符或节点已离开原场景时失败。</summary>
        /// <param name="identity">快照中的对象身份。</param>
        /// <param name="resolver">调用方提供的对象映射。</param>
        /// <returns>仍有效的 Godot 对象。</returns>
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

        /// <summary>按快照引用恢复字段。挂接不可用时抛出，不部分写入。</summary>
        /// <param name="attachment">Prepare 返回的挂接。</param>
        /// <param name="fields">字段快照 JSON。</param>
        /// <param name="references">已解析的引用对象。</param>
        public void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references) =>
            RestoreFields(Require(attachment).Host.Instance, fields, references, true);

        /// <summary>校验快照字段与引用表一致。引用缺失、重复或数量超限时拒绝恢复。</summary>
        /// <param name="state">待恢复的快照。</param>
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

        /// <summary>Godot 原型不支持导出。调用方应改用普通 Godot C# 脚本。</summary>
        /// <param name="id">LiveCode 标识。</param>
        /// <param name="className">类名。</param>
        /// <param name="source">源码。</param>
        /// <param name="sourceHash">源码哈希。</param>
        /// <param name="attachment">当前挂接。</param>
        /// <param name="outputPath">请求的输出路径。</param>
        /// <returns>不会返回。</returns>
        public string Export(string id, string className, string source, string sourceHash, IDisposable attachment, string outputPath) =>
            throw new NotSupportedException("Godot prototype export/bind is not implemented; use normal Godot C# scripts.");

        /// <summary>Godot 不支持持久绑定。调用直接失败，不创建场景引用。</summary>
        /// <param name="exportId">导出标识。</param>
        /// <param name="target">请求绑定的对象。</param>
        /// <returns>不会返回。</returns>
        public string Bind(string exportId, object target) => throw new NotSupportedException("Godot persistent binding is not implemented.");

        /// <summary>要求挂接仍然可用。不可用时抛出，避免把操作落到已释放宿主。</summary>
        /// <param name="value">Prepare 返回的挂接。</param>
        /// <returns>可用挂接。</returns>
        private Attachment Require(IDisposable value)
        {
            if (!IsAlive(value)) throw new InvalidOperationException("Godot live behaviour is unavailable.");
            return (Attachment)value;
        }

        /// <summary>枚举原型自身声明的公开或 Export 字段。基类字段和只读字段不进入快照。</summary>
        /// <param name="type">原型类型。</param>
        /// <returns>可读写字段。</returns>
        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (field.DeclaringType == type && !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), false)
                    && (field.IsPublic || field.IsDefined(typeof(ExportAttribute), false))) yield return field;
        }

        /// <summary>按名查找可访问字段。找不到时抛出，不回退到属性或基类字段。</summary>
        /// <param name="instance">原型实例。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段。</returns>
        private static FieldInfo RequireField(object instance, string name)
        {
            foreach (var field in Fields(instance.GetType())) if (field.Name == name) return field;
            throw new ArgumentException("Only declared public/Export fields are accessible: " + name);
        }

        /// <summary>捕获字段。快照模式记录 Godot 引用；普通状态只记录实例标识。宿主引用不能恢复。</summary>
        /// <param name="instance">原型实例。</param>
        /// <param name="snapshot">为 true 时生成快照引用。</param>
        /// <param name="id">LiveCode 标识。</param>
        /// <param name="key">快照引用键生成器；普通捕获可以为空。</param>
        /// <param name="references">快照引用；普通捕获保持空列表。</param>
        /// <returns>字段 JSON。</returns>
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

        /// <summary>恢复字段。快照要求字段集合完全一致；普通状态允许跳过已经不存在的字段。</summary>
        /// <param name="instance">原型实例。</param>
        /// <param name="fields">字段 JSON。</param>
        /// <param name="references">快照引用；普通恢复可以为空。</param>
        /// <param name="snapshot">为 true 时按快照约束校验。</param>
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

        /// <summary>把字段值编码成文本。非有限浮点和不受支持的类型直接拒绝，不写成不可恢复文本。</summary>
        /// <param name="type">字段类型。</param>
        /// <param name="value">字段值。</param>
        /// <returns>可再次解码的文本。</returns>
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

        /// <summary>把数值写成 JSON 数组文本。任一非有限值都拒绝。</summary>
        /// <param name="values">分量。</param>
        /// <returns>JSON 数组文本。</returns>
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

        /// <summary>构造对象身份。节点额外记录所属场景路径，普通 Godot 对象只记录类型和名字。</summary>
        /// <param name="value">有效的 Godot 对象。</param>
        /// <param name="key">调用方分配的稳定键。</param>
        /// <returns>快照身份。</returns>
        private static LiveObjectIdentity Identity(GodotObject value, string key) =>
            new LiveObjectIdentity(key, "", value is Node node ? ScenePath(node) : "",
                value.GetType().FullName, value is Node named ? named.Name.ToString() : value.GetType().Name);

        /// <summary>向上查找最近的场景文件路径。根节点也没有场景文件时返回空字符串。</summary>
        /// <param name="node">起始节点。</param>
        /// <returns>场景文件路径。</returns>
        private static string ScenePath(Node node)
        {
            for (Node current = node; current != null; current = current.GetParent())
                if (!string.IsNullOrEmpty(current.SceneFilePath)) return current.SceneFilePath;
            return "";
        }
    }
}
#endif
