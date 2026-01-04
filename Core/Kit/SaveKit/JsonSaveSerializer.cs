using System;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// JSON 序列化器 - 使用 Unity JsonUtility 实现
    /// 默认的序列化器实现
    /// </summary>
    public class JsonSaveSerializer : ISaveSerializer
    {
        /// <summary>
        /// 是否使用美化格式（调试用）
        /// </summary>
        public bool PrettyPrint { get; set; } = false;

        public byte[] Serialize<T>(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var json = JsonUtility.ToJson(data, PrettyPrint);
            return Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));

            var json = Encoding.UTF8.GetString(bytes);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
