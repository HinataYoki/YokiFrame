#if YOKIFRAME_NINO_SUPPORT
using System;
using System.Reflection;
using System.Threading;
using Nino.Core;

namespace YokiFrame
{
    /// <summary>
    /// 基于 Nino 的 SaveKit 二进制序列化器。
    /// </summary>
    public sealed class NinoSaveSerializer : ISaveSerializer
    {
        private static int sInitialized;

        /// <inheritdoc />
        public byte[] Serialize<T>(T data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            EnsureNinoInitialized();
            return NinoSerializer.Serialize(data);
        }

        /// <inheritdoc />
        public T Deserialize<T>(byte[] bytes)
        {
            ValidateBytes(bytes);
            EnsureNinoInitialized();
            return NinoDeserializer.Deserialize<T>(bytes);
        }

        /// <inheritdoc />
        public byte[] Serialize(object data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            EnsureNinoInitialized();
            return NinoSerializer.Serialize(data);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(byte[] bytes, object target)
        {
            ValidateBytes(bytes);
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            EnsureNinoInitialized();
            var targetType = target.GetType();
            object boxedTarget = target;
            NinoDeserializer.DeserializeRefBoxed(ref boxedTarget, bytes, targetType);

            if (!ReferenceEquals(boxedTarget, target))
            {
                CopyInstanceValues(boxedTarget, target, targetType);
            }
        }

        private static void EnsureNinoInitialized()
        {
            if (Volatile.Read(ref sInitialized) != 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref sInitialized, 1) != 0)
            {
                return;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                InitializeGeneratedRegistration(assemblies[i]);
            }
        }

        private static void InitializeGeneratedRegistration(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
            {
                return;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.FullName == null || type.FullName.IndexOf(".NinoGen.", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (type.Name != "Serializer" && type.Name != "Deserializer" && type.Name != "NinoBuiltInTypesRegistration")
                {
                    continue;
                }

                var init = type.GetMethod("Init", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (init != null)
                {
                    init.Invoke(null, null);
                }
            }
        }

        private static void ValidateBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Bytes cannot be null or empty.", nameof(bytes));
            }
        }

        private static void CopyInstanceValues(object source, object target, Type targetType)
        {
            if (source == null)
            {
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var fields = targetType.GetFields(flags);
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i].SetValue(target, fields[i].GetValue(source));
            }

            var properties = targetType.GetProperties(flags);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                property.SetValue(target, property.GetValue(source, null), null);
            }
        }
    }
}
#endif
