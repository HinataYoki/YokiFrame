#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Security.Cryptography;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class YokiFrameEngineEvalRequest
    {
        public const int MAX_ID_LENGTH = 64;
        public string Id { get; private set; }
        public string Code { get; private set; }
        public string Language { get; private set; }

        public static bool IsSafeId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MAX_ID_LENGTH) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z')
                    && !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }

        public static bool TryParse(string json, out YokiFrameEngineEvalRequest request, out string error)
        {
            request = null;
            error = "Inline code and an explicit script language are required; codeFile is not supported.";
            try
            {
                using (var document = JsonDocument.Parse(json ?? "{}"))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("codeFile", out _)
                        || !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String
                        || !root.TryGetProperty("language", out var language) || language.ValueKind != JsonValueKind.String)
                        return false;
                    string id = Guid.NewGuid().ToString("N");
                    if (root.TryGetProperty("id", out var idValue))
                    {
                        if (idValue.ValueKind != JsonValueKind.String) return false;
                        id = idValue.GetString();
                    }
                    if (!IsSafeId(id) || string.IsNullOrWhiteSpace(code.GetString())
                        || Encoding.UTF8.GetByteCount(code.GetString()) > 128 * 1024) return false;
                    request = new YokiFrameEngineEvalRequest { Id = id, Code = code.GetString(), Language = language.GetString() };
                    error = string.Empty;
                    return true;
                }
            }
            catch (JsonException exception) { error = exception.Message; return false; }
        }

        public static string ComputeSha256(string text)
        {
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }
    }
}
#endif
