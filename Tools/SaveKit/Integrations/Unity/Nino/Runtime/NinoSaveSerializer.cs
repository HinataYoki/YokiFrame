#if UNITY_2022_3_OR_NEWER && YOKIFRAME_NINO_SUPPORT
using System;
using System.IO;
using System.Reflection;
using Nino.Core;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 的 Nino 二进制序列化后端。payload 版本和迁移由 Nino 自己负责。
    /// Nino 4.x 的生成代码由程序集级 ModuleInitializer 自行注册，适配层不再手工扫描并初始化生成类型。
    /// </summary>
    public sealed class NinoSaveSerializer : ISaveSerializer, IModuleIdAwareSaveSerializer
    {
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

            return NinoSerializer.Serialize(data);
        }

        /// <inheritdoc />
        public T Deserialize<T>(byte[] bytes)
        {
            ValidateBytes(bytes);
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

            // 非泛型入口必须显式传入运行时类型，否则 Nino 只能看到 object 的静态类型。
            return NinoSerializer.SerializeBoxed(data, data.GetType());
        }

        /// <inheritdoc />
        public void ValidatePayload(string moduleId, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidDataException("Nino save payload is empty or truncated.");
            }
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(byte[] bytes, object target)
        {
            ValidateBytes(bytes);
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Type targetType = target.GetType();
            object boxedTarget = target;
            // Nino 4.x 以 DeserializeBoxed 取代已移除的 DeserializeRefBoxed：目标实例存在时按原实例写入。
            NinoDeserializer.DeserializeBoxed(bytes, ref boxedTarget, targetType);
            if (!ReferenceEquals(boxedTarget, target))
            {
                // payload 运行时类型与目标类型不一致时 Nino 会返回新实例，此处回拷以保持“原地覆盖”契约。
                CopyInstanceValues(boxedTarget, target, targetType);
            }
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(string moduleId, byte[] bytes, object target)
        {
            DeserializeOverwrite(bytes, target);
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

            const BindingFlags FIELD_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (Type type = targetType; type != null && type != typeof(object); type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(FIELD_FLAGS);
                for (var i = 0; i < fields.Length; i++)
                {
                    fields[i].SetValue(target, fields[i].GetValue(source));
                }
            }

            // 属性部分保持原逻辑（公开属性天然继承，Public|Instance 已覆盖基类）。
            const BindingFlags PROP_FLAGS = BindingFlags.Public | BindingFlags.Instance;
            PropertyInfo[] properties = targetType.GetProperties(PROP_FLAGS);
            for (var i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(target, property.GetValue(source, null), null);
                }
            }
        }
    }
}
#endif
