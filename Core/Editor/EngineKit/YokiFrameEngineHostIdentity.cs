#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using System.IO;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 从已发布的 engine registry（<c>engine.json</c>）读取宿主会话身份。
    /// </summary>
    /// <remarks>
    /// 存在的理由：会话身份（sessionId / generation）本来就由 pump 写进 engine.json 与 heartbeat，
    /// 因此读取方**不需要修改 pump**、也不需要访问它的私有静态字段——直接读已发布状态即可。
    /// 按 (存在性, mtime, 长度) 缓存：宿主每帧都会用身份做状态指纹，不能每帧读文件。
    /// </remarks>
    public sealed class YokiFrameEngineHostIdentityReader
    {
        private readonly string mRegistryPath;
        private bool mHasCache;
        private bool mCachedExists;
        private DateTime mCachedWriteUtc = DateTime.MinValue;
        private long mCachedLength = -1L;
        private bool mAvailable;
        private string mSessionId = string.Empty;
        private long mGeneration;
        private string mReason = string.Empty;

        /// <summary>创建读取器。</summary>
        /// <param name="registryPath">engine.json 绝对路径。</param>
        public YokiFrameEngineHostIdentityReader(string registryPath)
        {
            if (string.IsNullOrWhiteSpace(registryPath))
            {
                throw new ArgumentException("registryPath is required.", nameof(registryPath));
            }

            mRegistryPath = registryPath;
        }

        /// <summary>获取 registry 路径。</summary>
        public string RegistryPath
        {
            get { return mRegistryPath; }
        }

        /// <summary>
        /// 读取会话身份；未发布时返回 false 并给出可诊断原因（不伪造身份）。
        /// </summary>
        /// <param name="sessionId">会话标识。</param>
        /// <param name="generation">域代次。</param>
        /// <param name="reason">不可用原因。</param>
        /// <returns>读到有效身份时返回 true。</returns>
        public bool TryRead(out string sessionId, out long generation, out string reason)
        {
            Refresh();
            sessionId = mSessionId;
            generation = mGeneration;
            reason = mReason;
            return mAvailable;
        }

        private void Refresh()
        {
            var info = new FileInfo(mRegistryPath);
            bool exists = info.Exists;
            DateTime writeUtc = exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            long length = exists ? info.Length : -1L;
            if (mHasCache && exists == mCachedExists && writeUtc == mCachedWriteUtc && length == mCachedLength)
            {
                return;
            }

            mCachedExists = exists;
            mCachedWriteUtc = writeUtc;
            mCachedLength = length;
            mHasCache = true;

            if (!exists)
            {
                SetUnavailable("engine registry was not found: " + mRegistryPath);
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(mRegistryPath);
            }
            catch (IOException exception)
            {
                SetUnavailable("engine registry could not be read: " + exception.Message);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                SetUnavailable("engine registry could not be read: " + exception.Message);
                return;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                SetUnavailable("engine registry is not valid JSON: " + exception.Message);
                return;
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    SetUnavailable("engine registry root must be a JSON object.");
                    return;
                }

                string sessionId = ReadString(root, "sessionId");
                long generation = ReadLong(root, "generation");
                if (sessionId.Length == 0)
                {
                    SetUnavailable("engine registry has no sessionId; the host has not published its identity yet.");
                    return;
                }

                mAvailable = true;
                mSessionId = sessionId;
                mGeneration = generation;
                mReason = string.Empty;
            }
        }

        private void SetUnavailable(string reason)
        {
            mAvailable = false;
            mSessionId = string.Empty;
            mGeneration = 0L;
            mReason = reason ?? string.Empty;
        }

        private static string ReadString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return string.Empty;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return (value.GetString() ?? string.Empty).Trim();
            }

            return string.Empty;
        }

        private static long ReadLong(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return 0L;
            }

            if (value.TryGetInt64(out long parsed))
            {
                return parsed;
            }

            // 兼容以字符串发布的代次（历史上不同宿主序列化方式不同）。
            if (value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long fromText))
            {
                return fromText;
            }

            return 0L;
        }
    }
}
#endif