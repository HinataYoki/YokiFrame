#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;

namespace YokiFrame
{
    /// <summary>
    /// 把 Unity Console 对 LogKit 包装帧的双击跳转重定向到真实业务调用点。
    /// Core 禁止引用引擎无法使用 HideInCallstack，因此在 Editor 侧按当前条目堆栈改写跳转目标。
    /// Unity 2022.3 ~ Unity 6 均适用：不同版本的 OnOpenAsset 签名和 GetAssetPath API 通过条件编译隔离。
    /// </summary>
    internal static class UnityLogKitConsoleCallsiteRouter
    {
        internal const string WRAPPER_FRAME_PREFIX = "YokiFrame.LogKit:";
        internal const string SOURCE_LOCATION_PREFIX = " (at ";

        // ── Unity 6: [OnOpenAsset(int)] 签名已废弃，改用 EntityId 重载 ──────────────
#if UNITY_6000_3_OR_NEWER
        /// <summary>
        /// Unity 6 的 OnOpenAsset 回调；直接接收 EntityId，无需通过废弃的 int 转型。
        /// </summary>
        [OnOpenAsset(-1)]
        private static bool RedirectLogKitWrapperToCallsite(UnityEngine.EntityId entityId, int line)
        {
            return TryRedirectByPath(AssetDatabase.GetAssetPath(entityId), line);
        }
#else
        // ── Unity 2022.3 ~ Unity 5: 标准 (int, int) 签名 ─────────────────────────
        /// <summary>
        /// Unity 2022.3 的 OnOpenAsset 回调；使用标准整数实例 ID。
        /// </summary>
        [OnOpenAsset(-1)]
        private static bool RedirectLogKitWrapperToCallsite(int instanceId, int line)
        {
            return TryRedirectByPath(AssetDatabase.GetAssetPath(instanceId), line);
        }
#endif

        /// <summary>
        /// 根据已知的资源路径和行号执行跳转重定向。
        /// </summary>
        /// <param name="requestedPath">Unity 请求打开的项目相对路径。</param>
        /// <param name="line">Unity 请求打开的行号。</param>
        /// <returns>已改写跳转目标时返回 true，其余情况交回 Unity 默认处理。</returns>
        private static bool TryRedirectByPath(string requestedPath, int line)
        {
            if (line <= 0 || string.IsNullOrEmpty(requestedPath))
            {
                return false;
            }

            string activeText = UnityLogKitConsoleReflection.ReadActiveEntryText();
            if (!TryResolveCallsiteFromText(requestedPath, line, activeText, out string filePath, out int lineNumber))
            {
                return false;
            }

            return InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
        }

        /// <summary>
        /// 在堆栈文本中确认请求帧属于 LogKit 包装层，并返回其后的首个业务调用帧。
        /// 与 Console 读取分离，便于单元测试。
        /// </summary>
        /// <param name="requestedPath">Unity 请求打开的项目相对路径。</param>
        /// <param name="requestedLine">Unity 请求打开的行号。</param>
        /// <param name="activeText">Console 当前选中条目的完整正文（含调用堆栈）。</param>
        /// <param name="filePath">业务调用帧的源文件路径。</param>
        /// <param name="lineNumber">业务调用帧的行号。</param>
        /// <returns>请求帧确为 LogKit 包装层且存在业务调用帧时返回 true。</returns>
        internal static bool TryResolveCallsiteFromText(
            string requestedPath,
            int requestedLine,
            string activeText,
            out string filePath,
            out int lineNumber)
        {
            filePath = string.Empty;
            lineNumber = 0;
            if (string.IsNullOrEmpty(activeText))
            {
                return false;
            }

            var matchedRequestedFrame = false;
            string[] frames = activeText.Split('\n');
            for (var index = 0; index < frames.Length; index++)
            {
                if (!TryParseSourceLocation(frames[index], out string frameFile, out int frameLine))
                {
                    continue;
                }

                if (frames[index].TrimStart().StartsWith(WRAPPER_FRAME_PREFIX, StringComparison.Ordinal))
                {
                    matchedRequestedFrame |= frameLine == requestedLine
                        && frameFile.EndsWith(requestedPath, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!matchedRequestedFrame)
                {
                    return false;
                }

                filePath = frameFile;
                lineNumber = frameLine;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从单个堆栈帧文本中解析 Unity 风格的源码位置。
        /// </summary>
        /// <param name="frame">形如 <c>Type:Method () (at Assets/Path/File.cs:42)</c> 的堆栈帧。</param>
        /// <param name="filePath">解析出的源文件路径。</param>
        /// <param name="lineNumber">解析出的源文件行号。</param>
        /// <returns>成功解析到有效文件行号时返回 true。</returns>
        internal static bool TryParseSourceLocation(string frame, out string filePath, out int lineNumber)
        {
            filePath = string.Empty;
            lineNumber = 0;
            int locationStart = frame.LastIndexOf(SOURCE_LOCATION_PREFIX, StringComparison.Ordinal);
            if (locationStart < 0)
            {
                return false;
            }

            locationStart += SOURCE_LOCATION_PREFIX.Length;
            int locationEnd = frame.IndexOf(')', locationStart);
            if (locationEnd <= locationStart)
            {
                return false;
            }

            int separator = frame.LastIndexOf(':', locationEnd - 1);
            if (separator <= locationStart)
            {
                return false;
            }

            filePath = frame.Substring(locationStart, separator - locationStart).Trim();
            string lineText = frame.Substring(separator + 1, locationEnd - separator - 1).Trim();
            if (filePath.Length == 0 || !int.TryParse(lineText, out lineNumber) || lineNumber <= 0)
            {
                filePath = string.Empty;
                lineNumber = 0;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 缓存读取 Unity Console 当前选中条目正文所需的内部成员；版本不匹配时静默退化为空。
    /// </summary>
    internal static class UnityLogKitConsoleReflection
    {
        private static readonly FieldInfo sConsoleWindowField = FindStaticField("ms_ConsoleWindow");
        private static readonly FieldInfo sActiveTextField = FindInstanceField("m_ActiveText");

        /// <summary>
        /// 读取 Console 当前选中条目的完整正文，含 Unity 解析出的调用堆栈。
        /// </summary>
        /// <returns>当前条目正文；Console 未打开或内部结构变化时返回空字符串。</returns>
        internal static string ReadActiveEntryText()
        {
            if (sConsoleWindowField == null || sActiveTextField == null)
            {
                return string.Empty;
            }

            object console = sConsoleWindowField.GetValue(null);
            if (console == null)
            {
                return string.Empty;
            }

            return sActiveTextField.GetValue(console) as string ?? string.Empty;
        }

        private static FieldInfo FindStaticField(string fieldName)
        {
            Type t = ResolveConsoleWindowType();
            return t?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static FieldInfo FindInstanceField(string fieldName)
        {
            Type t = ResolveConsoleWindowType();
            return t?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static Type ResolveConsoleWindowType()
            => typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
    }
}

#endif
