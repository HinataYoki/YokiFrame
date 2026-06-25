using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 保存数据容器，按模块类型保存序列化数据。
    /// </summary>
    public class SaveData
    {
        private readonly Dictionary<int, byte[]> moduleData = new();
        private readonly Dictionary<int, object> moduleRefs = new();
        private readonly Dictionary<int, Func<ISaveSerializer, byte[]>> serializeDelegates = new();

        private ISaveSerializer serializer;

        /// <summary>
        /// 当前保存容器中的模块数量。
        /// </summary>
        public int ModuleCount
        {
            get
            {
                int count = moduleRefs.Count;
                foreach (int key in moduleData.Keys)
                {
                    if (!moduleRefs.ContainsKey(key))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 设置保存容器使用的序列化器。
        /// </summary>
        /// <param name="saveSerializer">保存数据序列化器。</param>
        public void SetSerializer(ISaveSerializer saveSerializer)
        {
            serializer = saveSerializer;
        }

        /// <summary>
        /// 获取保存容器当前使用的序列化器。
        /// </summary>
        /// <returns>当前序列化器。</returns>
        public ISaveSerializer GetSerializer() => serializer;

        /// <summary>
        /// 注册指定类型的保存模块。
        /// </summary>
        /// <typeparam name="T">保存模块类型。</typeparam>
        /// <param name="data">保存模块实例。</param>
        public void RegisterModule<T>(T data) where T : class
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int key = GetTypeKey<T>();
            moduleRefs[key] = data;
            serializeDelegates[key] = saveSerializer => saveSerializer.Serialize(data);
            moduleData.Remove(key);
        }

        internal void RegisterModuleByType(object data, Type type)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            int key = type.FullName.GetHashCode();
            moduleRefs[key] = data;
            serializeDelegates[key] = saveSerializer => saveSerializer.Serialize(data);
            moduleData.Remove(key);
        }

        /// <summary>
        /// 注销指定类型的保存模块。
        /// </summary>
        /// <typeparam name="T">保存模块类型。</typeparam>
        /// <returns>成功移除模块时返回 true。</returns>
        public bool UnregisterModule<T>() where T : class
        {
            int key = GetTypeKey<T>();
            bool removed = moduleRefs.Remove(key);
            serializeDelegates.Remove(key);
            moduleData.Remove(key);
            return removed;
        }

        /// <summary>
        /// 获取指定类型的保存模块。
        /// </summary>
        /// <typeparam name="T">保存模块类型。</typeparam>
        /// <returns>保存模块实例；不存在时返回空。</returns>
        public T GetModule<T>() where T : class
        {
            int key = GetTypeKey<T>();
            object module;
            if (moduleRefs.TryGetValue(key, out module))
            {
                return module as T;
            }

            byte[] bytes;
            if (moduleData.TryGetValue(key, out bytes))
            {
                if (serializer == null)
                {
                    throw new InvalidOperationException("Save serializer is not set.");
                }

                return serializer.Deserialize<T>(bytes);
            }

            return null;
        }

        /// <summary>
        /// 检查是否存在指定类型的保存模块。
        /// </summary>
        /// <typeparam name="T">保存模块类型。</typeparam>
        /// <returns>存在模块时返回 true。</returns>
        public bool HasModule<T>() where T : class
        {
            int key = GetTypeKey<T>();
            return moduleRefs.ContainsKey(key) || moduleData.ContainsKey(key);
        }

        /// <summary>
        /// 移除指定类型的保存模块。
        /// </summary>
        /// <typeparam name="T">保存模块类型。</typeparam>
        /// <returns>成功移除模块时返回 true。</returns>
        public bool RemoveModule<T>() where T : class
        {
            return UnregisterModule<T>();
        }

        internal static int GetTypeKey<T>() => typeof(T).FullName.GetHashCode();

        internal IEnumerable<int> GetModuleKeys() => moduleData.Keys;

        internal void SetRawModule(int key, byte[] data)
        {
            moduleData[key] = data;
        }

        internal byte[] GetRawModule(int key)
        {
            byte[] bytes;
            return moduleData.TryGetValue(key, out bytes) ? bytes : null;
        }

        internal byte[] GetRawModuleOrSerializedRef(int key, ISaveSerializer saveSerializer)
        {
            byte[] bytes;
            if (moduleData.TryGetValue(key, out bytes))
            {
                return bytes;
            }

            Func<ISaveSerializer, byte[]> serialize;
            return serializeDelegates.TryGetValue(key, out serialize) ? serialize(saveSerializer) : null;
        }

        internal bool RemoveRawModule(int key)
        {
            return moduleData.Remove(key);
        }

        internal ModuleBytes[] SerializeRegisteredModules(ISaveSerializer saveSerializer)
        {
            int count = serializeDelegates.Count;
            foreach (int key in moduleData.Keys)
            {
                if (!serializeDelegates.ContainsKey(key))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return Array.Empty<ModuleBytes>();
            }

            ModuleBytes[] modules = new ModuleBytes[count];
            int index = 0;
            foreach (KeyValuePair<int, Func<ISaveSerializer, byte[]>> pair in serializeDelegates)
            {
                modules[index++] = new ModuleBytes(pair.Key, pair.Value(saveSerializer));
            }

            foreach (KeyValuePair<int, byte[]> pair in moduleData)
            {
                if (!serializeDelegates.ContainsKey(pair.Key))
                {
                    modules[index++] = new ModuleBytes(pair.Key, pair.Value);
                }
            }

            return modules;
        }

        /// <summary>
        /// 清空全部保存模块。
        /// </summary>
        public void Clear()
        {
            moduleData.Clear();
            moduleRefs.Clear();
            serializeDelegates.Clear();
        }
    }
}
