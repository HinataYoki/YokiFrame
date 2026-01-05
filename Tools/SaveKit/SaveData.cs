using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 存档数据容器 - 存储所有需要持久化的游戏数据
    /// 使用类型哈希作为 key 避免魔法字符串
    /// </summary>
    [Serializable]
    public class SaveData : IPoolable
    {
        /// <summary>
        /// 模块数据存储，key 为类型哈希
        /// </summary>
        private Dictionary<int, byte[]> mModuleData = new();

        /// <summary>
        /// 序列化器引用（用于模块序列化）
        /// </summary>
        private ISaveSerializer mSerializer;

        public bool IsRecycled { get; set; }

        /// <summary>
        /// 设置序列化器
        /// </summary>
        public void SetSerializer(ISaveSerializer serializer)
        {
            mSerializer = serializer;
        }

        /// <summary>
        /// 获取类型的哈希 key
        /// 使用 FullName 确保同名类在不同命名空间下有不同的 key
        /// 同一个类即使成员变化，key 也保持不变
        /// </summary>
        private static int GetTypeKey<T>() => typeof(T).FullName.GetHashCode();

        /// <summary>
        /// 设置模块数据
        /// </summary>
        public void SetModule<T>(T data) where T : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (mSerializer == null)
                throw new InvalidOperationException("Serializer not set. Use SaveKit to manage SaveData.");

            var key = GetTypeKey<T>();
            var bytes = mSerializer.Serialize(data);
            mModuleData[key] = bytes;
        }

        /// <summary>
        /// 获取模块数据
        /// </summary>
        public T GetModule<T>() where T : class
        {
            if (mSerializer == null)
                throw new InvalidOperationException("Serializer not set. Use SaveKit to manage SaveData.");

            var key = GetTypeKey<T>();
            if (!mModuleData.TryGetValue(key, out var bytes))
                return null;

            return mSerializer.Deserialize<T>(bytes);
        }

        /// <summary>
        /// 检查是否存在模块数据
        /// </summary>
        public bool HasModule<T>() where T : class
        {
            var key = GetTypeKey<T>();
            return mModuleData.ContainsKey(key);
        }

        /// <summary>
        /// 移除模块数据
        /// </summary>
        public bool RemoveModule<T>() where T : class
        {
            var key = GetTypeKey<T>();
            return mModuleData.Remove(key);
        }

        /// <summary>
        /// 获取所有模块的类型哈希
        /// </summary>
        public IEnumerable<int> GetModuleKeys() => mModuleData.Keys;

        /// <summary>
        /// 获取模块数量
        /// </summary>
        public int ModuleCount => mModuleData.Count;

        /// <summary>
        /// 通过 key 直接设置原始字节数据（内部使用）
        /// </summary>
        public void SetRawModule(int key, byte[] data)
        {
            mModuleData[key] = data;
        }

        /// <summary>
        /// 通过 key 直接获取原始字节数据（内部使用）
        /// </summary>
        public byte[] GetRawModule(int key)
        {
            return mModuleData.TryGetValue(key, out var data) ? data : null;
        }

        /// <summary>
        /// 检查是否存在指定 key 的模块（内部使用）
        /// </summary>
        public bool HasRawModule(int key) => mModuleData.ContainsKey(key);

        /// <summary>
        /// 通过 key 直接移除原始字节数据（内部使用）
        /// </summary>
        public bool RemoveRawModule(int key) => mModuleData.Remove(key);

        /// <summary>
        /// 清空所有模块数据
        /// </summary>
        public void Clear()
        {
            mModuleData.Clear();
        }

        /// <summary>
        /// 回收时清理数据
        /// </summary>
        public void OnRecycled()
        {
            mModuleData.Clear();
            mSerializer = null;
        }
    }
}
