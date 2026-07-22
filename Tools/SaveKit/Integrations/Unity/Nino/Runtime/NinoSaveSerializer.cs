#if UNITY_2022_3_OR_NEWER && YOKIFRAME_NINO_SUPPORT
using System;
using System.Reflection;
using System.Threading;
using Nino.Core;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 的 Nino 二进制序列化后端。payload 版本和迁移由 Nino 自己负责。
    /// </summary>
    public sealed class NinoSaveSerializer : ISaveSerializer, IModuleIdAwareSaveSerializer
    {
        private static int sInitialized;

        /// <inheritdoc />
        public string SerializerId
        {
            get { return "nino"; }
        }

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
        public T Deserialize<T>(string moduleId, byte[] bytes)
        {
            return Deserialize<T>(bytes);
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
        public void ValidatePayload(string moduleId, byte[] bytes)
        {
            ValidateBytes(bytes);
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

        /// <inheritdoc />
        public void DeserializeOverwrite(string moduleId, byte[] bytes, object target)
        {
            DeserializeOverwrite(bytes, target);
        }

        /// <summary>
        /// 确保 Nino 生成注册代码只初始化一次；Unity 6.5 及以上通过 Unity 维护的程序集集合规避卸载程序集，
        /// Unity 2022.3 基线继续使用其可用的 AppDomain 路径。
        /// </summary>
        private static void EnsureNinoInitialized()
        {
            if (Volatile.Read(ref sInitialized) != 0 || Interlocked.Exchange(ref sInitialized, 1) != 0)
            {
                return;
            }

#if UNITY_6000_5_OR_NEWER
            foreach (Assembly assembly in CurrentAssemblies.GetLoadedAssemblies())
            {
                InitializeGeneratedRegistration(assembly);
            }
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                InitializeGeneratedRegistration(assemblies[i]);
            }
#endif
        }

        /// <summary>扫描已加载程序集并调用 Nino 生成注册入口。</summary>
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
                init?.Invoke(null, null);
            }
        }

        /// <summary>验证 Nino payload 非空。</summary>
        private static void ValidateBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Nino payload cannot be empty.", nameof(bytes));
            }
        }

        /// <summary>当 Nino 返回新实例时，把字段和值复制到调用方对象。</summary>
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
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(target, property.GetValue(source, null), null);
                }
            }
        }
    }
}
#endif
