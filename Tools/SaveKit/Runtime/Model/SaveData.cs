using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 的模块化保存容器。容器只保存稳定模块 ID 和 payload，不依赖具体序列化器。
    /// </summary>
    public sealed class SaveData
    {
        private readonly Dictionary<string, byte[]> moduleData = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> moduleRefs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<ISaveSerializer, byte[]>> serializeDelegates = new(StringComparer.Ordinal);
        private readonly Dictionary<Type, string> moduleIdsByType = new();
        private ISaveSerializer serializer;

        /// <summary>获取当前容器中的模块数量。</summary>
        public int ModuleCount
        {
            get
            {
                var count = moduleRefs.Count;
                foreach (var key in moduleData.Keys)
                {
                    if (!moduleRefs.ContainsKey(key))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>设置容器读取模块时使用的序列化器。</summary>
        /// <param name="saveSerializer">序列化器；不可为空。</param>
        public void SetSerializer(ISaveSerializer saveSerializer)
        {
            serializer = saveSerializer ?? throw new ArgumentNullException(nameof(saveSerializer));
        }

        /// <summary>获取容器当前使用的序列化器。</summary>
        public ISaveSerializer GetSerializer()
        {
            return serializer;
        }

        /// <summary>注册或替换一个强类型模块。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="data">模块实例。</param>
        /// <param name="moduleId">可选稳定模块 ID；为空时使用类型完整名称。</param>
        public void RegisterModule<T>(T data, string moduleId = null) where T : class
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var id = SaveModuleIdentity.GetId<T>(moduleId);
            moduleIdsByType[typeof(T)] = id;
            RegisterModuleCore(id, data);
        }

        /// <summary>注销一个强类型模块。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="moduleId">可选稳定模块 ID；为空时使用当前容器记录或类型完整名称。</param>
        /// <returns>模块存在并被移除时返回 true。</returns>
        public bool RemoveModule<T>(string moduleId = null) where T : class
        {
            var id = ResolveModuleId<T>(moduleId);
            var removed = moduleRefs.Remove(id);
            serializeDelegates.Remove(id);
            if (moduleData.Remove(id))
            {
                removed = true;
            }

            if (moduleIdsByType.TryGetValue(typeof(T), out var registeredId) && registeredId == id)
            {
                moduleIdsByType.Remove(typeof(T));
            }

            return removed;
        }

        /// <summary>获取强类型模块；首次读取原始 payload 后会缓存模块实例。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="moduleId">可选稳定模块 ID；为空时使用当前容器记录或类型完整名称。</param>
        /// <returns>模块实例；不存在时返回空。</returns>
        public T GetModule<T>(string moduleId = null) where T : class
        {
            var id = ResolveModuleId<T>(moduleId);
            object module;
            if (moduleRefs.TryGetValue(id, out module))
            {
                return module as T;
            }

            byte[] bytes;
            if (!moduleData.TryGetValue(id, out bytes))
            {
                return null;
            }

            if (serializer == null)
            {
                throw new InvalidOperationException("Save serializer is not set.");
            }

            var restored = serializer is IModuleIdAwareSaveSerializer idAwareSerializer
                ? idAwareSerializer.Deserialize<T>(id, bytes)
                : serializer.Deserialize<T>(bytes);
            if (restored != null)
            {
                moduleRefs[id] = restored;
                serializeDelegates[id] = saveSerializer => saveSerializer.Serialize(restored);
                moduleIdsByType[typeof(T)] = id;
            }

            return restored;
        }

        /// <summary>判断指定类型模块是否存在。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="moduleId">可选稳定模块 ID；为空时使用当前容器记录或类型完整名称。</param>
        /// <returns>存在时返回 true。</returns>
        public bool HasModule<T>(string moduleId = null) where T : class
        {
            var id = ResolveModuleId<T>(moduleId);
            return moduleRefs.ContainsKey(id) || moduleData.ContainsKey(id);
        }

        /// <summary>清空全部模块。</summary>
        public void Clear()
        {
            moduleRefs.Clear();
            moduleData.Clear();
            serializeDelegates.Clear();
            moduleIdsByType.Clear();
        }

        /// <summary>注册 Architecture 使用的运行时类型模块。</summary>
        /// <param name="data">模块对象。</param>
        /// <param name="type">模块具体类型。</param>
        internal void RegisterModuleByType(object data, Type type, string moduleId = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var id = SaveModuleIdentity.GetId(type, moduleId);
            moduleIdsByType[type] = id;
            RegisterModuleCore(id, data);
        }

        /// <summary>获取原始模块 ID 快照，供容器序列化和后端迁移使用。</summary>
        internal IReadOnlyList<string> GetModuleIds()
        {
            var ids = new List<string>(moduleRefs.Count + moduleData.Count);
            foreach (var pair in moduleRefs)
            {
                ids.Add(pair.Key);
            }

            foreach (var pair in moduleData)
            {
                if (!moduleRefs.ContainsKey(pair.Key))
                {
                    ids.Add(pair.Key);
                }
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>把模块集合序列化为按 ID 排序的稳定记录。</summary>
        internal SaveModuleRecord[] SerializeModules(ISaveSerializer saveSerializer)
        {
            if (saveSerializer == null)
            {
                throw new ArgumentNullException(nameof(saveSerializer));
            }

            var ids = GetModuleIds();
            var records = new SaveModuleRecord[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                Func<ISaveSerializer, byte[]> serialize;
                if (serializeDelegates.TryGetValue(id, out serialize))
                {
                    records[i] = new SaveModuleRecord(id, serialize(saveSerializer));
                    continue;
                }

                byte[] bytes;
                records[i] = new SaveModuleRecord(id, moduleData.TryGetValue(id, out bytes) ? CopyBytes(bytes) : Array.Empty<byte>());
            }

            return records;
        }

        /// <summary>写入已由调用方独占的原始 payload，避免反序列化路径二次拷贝。</summary>
        internal void SetRawModuleOwned(string id, byte[] bytes)
        {
            SaveModuleIdentity.ValidateId(id);
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            moduleData[id] = bytes;
            moduleRefs.Remove(id);
            serializeDelegates.Remove(id);
        }

        /// <summary>判断原始模块 payload 是否已存在，不复制字节。</summary>
        internal bool ContainsRawModule(string id)
        {
            return moduleData.ContainsKey(id);
        }

        /// <summary>获取模块原始字节或当前对象序列化结果。</summary>
        internal byte[] GetRawModuleOrSerializedRef(string id, ISaveSerializer saveSerializer)
        {
            byte[] bytes;
            if (moduleData.TryGetValue(id, out bytes))
            {
                return CopyBytes(bytes);
            }

            Func<ISaveSerializer, byte[]> serialize;
            return serializeDelegates.TryGetValue(id, out serialize) ? serialize(saveSerializer) : null;
        }

        /// <summary>让序列化器验证所有原始模块 payload，避免延迟到 GetModule 才发现迁移错误。</summary>
        /// <param name="saveSerializer">当前序列化器。</param>
        internal void ValidateRawModules(ISaveSerializer saveSerializer)
        {
            if (saveSerializer == null)
            {
                throw new ArgumentNullException(nameof(saveSerializer));
            }

            foreach (var pair in moduleData)
            {
                saveSerializer.ValidatePayload(pair.Key, pair.Value);
            }
        }

        /// <summary>获取模块具体 ID。</summary>
        private void RegisterModuleCore(string id, object data)
        {
            SaveModuleIdentity.ValidateId(id);
            moduleRefs[id] = data;
            serializeDelegates[id] = saveSerializer => saveSerializer.Serialize(data);
            moduleData.Remove(id);
        }

        /// <summary>解析模块的显式 ID、当前容器注册 ID 或类型全名。</summary>
        private string ResolveModuleId<T>(string moduleId) where T : class
        {
            if (!string.IsNullOrEmpty(moduleId))
            {
                return SaveModuleIdentity.GetId<T>(moduleId);
            }

            return moduleIdsByType.TryGetValue(typeof(T), out var registeredId)
                ? registeredId
                : SaveModuleIdentity.GetId<T>();
        }

        /// <summary>复制字节数组，阻止外部修改容器内部 payload。</summary>
        private static byte[] CopyBytes(byte[] bytes)
        {
            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }
    }
}
