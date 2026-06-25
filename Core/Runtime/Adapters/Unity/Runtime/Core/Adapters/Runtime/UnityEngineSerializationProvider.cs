#if !GODOT
using UnityEngine;
using YokiFrame;

namespace YokiFrame.Unity
{
    /// <summary>
    /// ISerializationProvider 的 Unity 实现，基于 UnityEngine.JsonUtility
    /// </summary>
    /// <remarks>
    /// JsonUtility 仅序列化 public 字段和标记了 [SerializeField] 的私有字段。
    /// 需要完整 JSON 支持时，可使用 Newtonsoft.Json 或自定义实现替换。
    /// </remarks>
    public sealed class UnityEngineSerializationProvider : ISerializationProvider
    {
        /// <inheritdoc />
        public string Serialize<T>(T data)
        {
            return JsonUtility.ToJson(data, prettyPrint: false);
        }

        /// <inheritdoc />
        public T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
#endif
