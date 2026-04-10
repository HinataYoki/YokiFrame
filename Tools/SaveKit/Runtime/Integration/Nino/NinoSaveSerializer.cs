#if HAS_NINO
using System;
using Nino.Core;

namespace YokiFrame
{
    /// <summary>
    /// Nino 二进制序列化器 - 高性能二进制序列化实现
    /// 需要序列化的类型必须标记 [NinoType] 属性
    /// </summary>
    public class NinoSaveSerializer : ISaveSerializer
    {
        public byte[] Serialize<T>(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return NinoSerializer.Serialize(data);
        }

        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));

            return (T)NinoDeserializer.Deserialize(bytes, typeof(T));
        }

        public byte[] Serialize(object data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return NinoSerializer.Serialize(data);
        }

        public void DeserializeOverwrite(byte[] bytes, object target)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // Nino 不支持直接覆盖已有对象，反序列化为新对象后逐字段拷贝
            var targetType = target.GetType();
            var newObj = NinoDeserializer.Deserialize(bytes, targetType);

            var fields = targetType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                field.SetValue(target, field.GetValue(newObj));
            }
        }
    }
}
#endif
