#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity JsonUtility 到 SaveKit JSON 契约的宿主适配器。
    /// </summary>
    public sealed class UnityJsonSaveCodec : IJsonSaveCodec
    {
        /// <inheritdoc />
        public string Serialize<T>(T data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, false);
        }

        /// <inheritdoc />
        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
            }

            return JsonUtility.FromJson<T>(json);
        }

        /// <inheritdoc />
        public string Serialize(object data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, false);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(string json, object target)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("JSON payload cannot be empty.", nameof(json));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            JsonUtility.FromJsonOverwrite(json, target);
        }
    }
}
#endif
