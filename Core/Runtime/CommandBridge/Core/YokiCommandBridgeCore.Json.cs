using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥核心的 JSON 协议解析片段。
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        private static bool IsLikelyIncompleteJsonDocument(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return true;

            var start = 0;
            while (start < content.Length && char.IsWhiteSpace(content[start]))
                start++;

            if (start >= content.Length)
                return true;

            var first = content[start];
            if (first != '{' && first != '[')
                return false;

            int rootEndIndex;
            return !TryGetJsonRootEndIndex(content, out rootEndIndex);
        }

        private static bool IsCompleteJsonDocument(string content)
        {
            int rootEndIndex;
            if (!TryGetJsonRootEndIndex(content, out rootEndIndex))
                return false;

            for (var i = rootEndIndex + 1; i < content.Length; i++)
            {
                if (!char.IsWhiteSpace(content[i]))
                    return false;
            }

            return true;
        }

        private static bool TryGetJsonRootEndIndex(string content, out int rootEndIndex)
        {
            rootEndIndex = -1;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var start = 0;
            while (start < content.Length && char.IsWhiteSpace(content[start]))
                start++;

            if (start >= content.Length)
                return false;

            var first = content[start];
            if (first != '{' && first != '[')
                return false;

            var objectDepth = 0;
            var arrayDepth = 0;
            var inString = false;
            var escaping = false;
            for (var i = start; i < content.Length; i++)
            {
                var c = content[i];
                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaping = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    objectDepth++;
                else if (c == '}')
                    objectDepth--;
                else if (c == '[')
                    arrayDepth++;
                else if (c == ']')
                    arrayDepth--;

                if (objectDepth < 0 || arrayDepth < 0)
                    return false;

                if (objectDepth == 0 && arrayDepth == 0)
                {
                    rootEndIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveResponseRequestId(string fallbackRequestId, string commandJson, out string invalidRequestId)
        {
            invalidRequestId = null;
            var requestId = JsonHelper.ExtractString(commandJson ?? string.Empty, "requestId");
            if (string.IsNullOrEmpty(requestId))
                return fallbackRequestId;

            if (CommandBridgeProtocol.IsSafeIdentifier(requestId))
                return requestId;

            invalidRequestId = requestId;
            return fallbackRequestId;
        }

        private string BuildResultPath(string requestId)
        {
            return Path.Combine(mResultDir, requestId + "-response.json");
        }

        private static string ResolveSafeFallbackRequestId(string commandName)
        {
            if (CommandBridgeProtocol.IsSafeIdentifier(commandName))
                return commandName;

            return "command-" + Guid.NewGuid().ToString("N");
        }
    }
}
