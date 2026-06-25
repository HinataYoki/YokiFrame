using System;
using System.Reflection;
using System.Text;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 基于 <see cref="ISerializationProvider"/> 的 SaveKit 序列化器适配器。
    /// </summary>
    public sealed class SerializationProviderSaveSerializer : ISaveSerializer
    {
        private static readonly MethodInfo SerializeGenericMethod =
            typeof(ISerializationProvider).GetMethod("Serialize");

        private readonly ISerializationProvider provider;

        /// <summary>
        /// 创建基于引擎序列化提供器的保存序列化器。
        /// </summary>
        /// <param name="provider">引擎或项目注入的序列化提供器。</param>
        public SerializationProviderSaveSerializer(ISerializationProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            this.provider = provider;
        }

        /// <inheritdoc />
        public byte[] Serialize<T>(T data)
        {
            string json = provider.Serialize(data);
            return Encoding.UTF8.GetBytes(json ?? string.Empty);
        }

        /// <inheritdoc />
        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            string json = Encoding.UTF8.GetString(bytes);
            return provider.Deserialize<T>(json);
        }

        /// <inheritdoc />
        public byte[] Serialize(object data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            MethodInfo method = SerializeGenericMethod.MakeGenericMethod(data.GetType());
            string json = method.Invoke(provider, new[] { data }) as string;
            return Encoding.UTF8.GetBytes(json ?? string.Empty);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(byte[] bytes, object target)
        {
            throw new NotSupportedException("ISerializationProvider does not support overwrite deserialization.");
        }
    }
}
