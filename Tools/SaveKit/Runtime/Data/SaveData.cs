using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 存档数据容器 - 存储所有需要持久化的游戏数据
    /// 使用延迟序列化：注册模块引用，保存时才在线程池执行序列化
    /// </summary>
    [Serializable]
    public class SaveData : IPoolable
    {
        #region 字段

        /// <summary>
        /// 模块字节数据存储（加载后的数据）
        /// </summary>
        private readonly Dictionary<int, byte[]> mModuleData = new();

        /// <summary>
        /// 模块对象引用存储（延迟序列化）
        /// </summary>
        private readonly Dictionary<int, object> mModuleRefs = new();

        /// <summary>
        /// 模块序列化委托存储（用于类型安全的序列化）
        /// </summary>
        private readonly Dictionary<int, Func<ISaveSerializer, byte[]>> mSerializeDelegates = new();

        /// <summary>
        /// 序列化器引用
        /// </summary>
        private ISaveSerializer mSerializer;

        #endregion

        #region 属性

        public bool IsRecycled { get; set; }

        /// <summary>
        /// 获取已注册的模块数量
        /// </summary>
        public int ModuleCount => mModuleRefs.Count;

        #endregion

        #region 类型 Key 缓存

        /// <summary>
        /// 泛型类型 Key 缓存，避免每次调用都计算哈希
        /// </summary>
        private static class TypeKeyCache<T>
        {
            public static readonly int Key = typeof(T).FullName.GetHashCode();
        }

        /// <summary>
        /// 获取类型的哈希 key
        /// </summary>
        internal static int GetTypeKey<T>() => TypeKeyCache<T>.Key;

        #endregion

        #region 序列化器

        /// <summary>
        /// 设置序列化器
        /// </summary>
        public void SetSerializer(ISaveSerializer serializer)
        {
            mSerializer = serializer;
        }

        /// <summary>
        /// 获取序列化器
        /// </summary>
        public ISaveSerializer GetSerializer() => mSerializer;

        #endregion

        #region 模块注册 API

        /// <summary>
        /// 注册模块对象引用（延迟序列化）
        /// 只存储引用，保存时才在线程池执行序列化，主线程零阻塞
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <param name="data">模块对象引用</param>
        public void RegisterModule<T>(T data) where T : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var key = GetTypeKey<T>();
            
            // 存储引用
            mModuleRefs[key] = data;
            
            // 存储类型安全的序列化委托（避免反射）
            mSerializeDelegates[key] = serializer => serializer.Serialize(data);
            
            // 移除旧的字节数据
            mModuleData.Remove(key);
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public bool UnregisterModule<T>() where T : class
        {
            var key = GetTypeKey<T>();
            var removed = mModuleRefs.Remove(key);
            mSerializeDelegates.Remove(key);
            mModuleData.Remove(key);
            return removed;
        }

        /// <summary>
        /// 获取模块数据
        /// 优先返回已注册的引用，否则从字节数据反序列化
        /// </summary>
        public T GetModule<T>() where T : class
        {
            var key = GetTypeKey<T>();
            
            // 优先从引用获取
            if (mModuleRefs.TryGetValue(key, out var obj))
                return obj as T;
            
            // 从字节数据反序列化（加载后的数据）
            if (mModuleData.TryGetValue(key, out var bytes))
            {
                if (mSerializer == null)
                    throw new InvalidOperationException("Serializer not set.");
                return mSerializer.Deserialize<T>(bytes);
            }

            return null;
        }

        /// <summary>
        /// 检查是否存在模块
        /// </summary>
        public bool HasModule<T>() where T : class
        {
            var key = GetTypeKey<T>();
            return mModuleRefs.ContainsKey(key) || mModuleData.ContainsKey(key);
        }

        /// <summary>
        /// 移除模块
        /// </summary>
        public bool RemoveModule<T>() where T : class
        {
            return UnregisterModule<T>();
        }

        #endregion

        #region 内部方法（SaveKit 使用）

        /// <summary>
        /// 获取所有字节模块的类型哈希
        /// </summary>
        internal IEnumerable<int> GetModuleKeys() => mModuleData.Keys;

        /// <summary>
        /// 通过 key 直接设置原始字节数据（加载时使用）
        /// </summary>
        internal void SetRawModule(int key, byte[] data)
        {
            mModuleData[key] = data;
        }

        /// <summary>
        /// 通过 key 直接获取原始字节数据
        /// </summary>
        internal byte[] GetRawModule(int key)
        {
            return mModuleData.TryGetValue(key, out var data) ? data : null;
        }

        /// <summary>
        /// 检查是否存在指定 key 的字节模块
        /// </summary>
        internal bool HasRawModule(int key) => mModuleData.ContainsKey(key);

        /// <summary>
        /// 通过 key 直接移除原始字节数据
        /// </summary>
        internal bool RemoveRawModule(int key) => mModuleData.Remove(key);

        /// <summary>
        /// 序列化所有已注册的模块（在线程池调用）
        /// </summary>
        /// <param name="serializer">序列化器</param>
        /// <returns>序列化后的模块数据数组</returns>
        internal (int key, byte[] bytes)[] SerializeRegisteredModules(ISaveSerializer serializer)
        {
            var count = mSerializeDelegates.Count;
            if (count == 0)
                return Array.Empty<(int, byte[])>();

            var result = new (int key, byte[] bytes)[count];
            var i = 0;

            foreach (var kvp in mSerializeDelegates)
            {
                var bytes = kvp.Value(serializer);
                result[i++] = (kvp.Key, bytes);
            }

            return result;
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清空所有模块数据
        /// </summary>
        public void Clear()
        {
            mModuleData.Clear();
            mModuleRefs.Clear();
            mSerializeDelegates.Clear();
        }

        /// <summary>
        /// 回收时清理数据
        /// </summary>
        public void OnRecycled()
        {
            Clear();
            mSerializer = null;
        }

        #endregion
    }
}
