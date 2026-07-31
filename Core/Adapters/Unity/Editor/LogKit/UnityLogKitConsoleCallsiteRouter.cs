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
        internal const string UNITY_ADAPTER_FRAME_PREFIX = "YokiFrame.Unity.UnityEngineLogger:";
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
            if (UnityLogKitConsoleReflection.TryReadActiveEntry(
                out string activeFile,
                out int activeLine,
                out string activeMessage))
            {
                // Unity 2022.3 可能先把适配层 file/line 传给 OnOpenAsset；优先使用当前
                // LogEntries 条目确认选中项，避免把普通 Console 日志误判成 LogKit 日志。
                if (!IsLogKitSourcePath(activeFile))
                {
                    return false;
                }

                string selectedText = string.IsNullOrEmpty(activeText) ? activeMessage : activeText;
                if (!TryResolveCallsiteFromText(
                    activeFile,
                    activeLine,
                    selectedText,
                    out string selectedFilePath,
                    out int selectedLineNumber))
                {
                    return false;
                }

                return InternalEditorUtility.OpenFileAtLineExternal(selectedFilePath, selectedLineNumber);
            }

            // 不支持 LogEntries 内部 API 的版本仍保留旧的 m_ActiveText 解析路径。
            if (!TryResolveCallsiteFromText(requestedPath, line, activeText, out string filePath, out int lineNumber))
            {
                return false;
            }

            return InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
        }

        /// <summary>
        /// 判断 Console 条目的来源是否属于 LogKit 的 Unity 包装层。
        /// Unity 2022.3 通常返回 UnityEngineLogger.cs，Unity 6 在隐藏调用栈后通常返回 LogKit.cs。
        /// </summary>
        /// <param name="filePath">Unity Console 条目中的源文件路径。</param>
        /// <returns>属于 LogKit 包装层时返回 true。</returns>
        private static bool IsLogKitSourcePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string normalizedPath = filePath.Replace('\\', '/');
            return normalizedPath.EndsWith("/UnityEngineLogger.cs", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.IndexOf("/LogKit/", StringComparison.OrdinalIgnoreCase) >= 0;
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

            bool matchedRequestedFrame = false;
            string[] frames = activeText.Split('\n');
            for (int index = 0; index < frames.Length; index++)
            {
                string frame = frames[index].TrimEnd('\r');
                bool isWrapperFrame = IsLogKitWrapperFrame(frame);
                if (!TryParseSourceLocation(frames[index], out string frameFile, out int frameLine))
                {
                    // Unity 2022.3 的某些 Console 展开状态会隐藏包装帧的源码位置，
                    // 但保留类型和方法名；先保留包装层状态，后续仍可定位有源码位置的业务帧。
                    if (isWrapperFrame && IsLogKitSourcePath(requestedPath))
                    {
                        matchedRequestedFrame = true;
                    }
                    continue;
                }

                if (isWrapperFrame)
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
        /// 判断堆栈帧是否属于 Core LogKit 或 Unity Logger 适配层。
        /// </summary>
        /// <param name="frame">单个 Unity 风格堆栈帧。</param>
        /// <returns>属于 LogKit 包装帧时返回 true。</returns>
        private static bool IsLogKitWrapperFrame(string frame)
        {
            string trimmedFrame = frame.TrimStart();
            return trimmedFrame.StartsWith(WRAPPER_FRAME_PREFIX, StringComparison.Ordinal)
                || trimmedFrame.StartsWith(UNITY_ADAPTER_FRAME_PREFIX, StringComparison.Ordinal);
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
        private static readonly FieldInfo sLastActiveEntryIndexField = FindInstanceField("m_LastActiveEntryIndex");
        private static readonly Type sLogEntriesType = ResolveType("UnityEditor.LogEntries", "UnityEditorInternal.LogEntries");
        private static readonly Type sLogEntryType = ResolveType("UnityEditor.LogEntry", "UnityEditorInternal.LogEntry");
        private static readonly MethodInfo sGetCountMethod = FindStaticMethod(sLogEntriesType, "GetCount", Type.EmptyTypes);
        private static readonly MethodInfo sGetEntryInternalMethod = FindStaticMethod(
            sLogEntriesType,
            "GetEntryInternal",
            new[] { typeof(int), sLogEntryType });
        private static readonly MethodInfo sStartGettingEntriesMethod = FindStaticMethod(
            sLogEntriesType,
            "StartGettingEntries",
            Type.EmptyTypes);
        private static readonly MethodInfo sEndGettingEntriesMethod = FindStaticMethod(
            sLogEntriesType,
            "EndGettingEntries",
            Type.EmptyTypes);
        private static readonly FieldInfo sMessageField = FindInstanceField(sLogEntryType, "message");
        private static readonly FieldInfo sFileField = FindInstanceField(sLogEntryType, "file");
        private static readonly FieldInfo sLineField = FindInstanceField(sLogEntryType, "line");

        /// <summary>
        /// 读取 Console 当前选中条目的完整正文，含 Unity 解析出的调用堆栈。
        /// </summary>
        /// <returns>当前条目正文；Console 未打开或内部结构变化时返回空字符串。</returns>
        internal static string ReadActiveEntryText()
        {
            object console = GetConsoleWindow();
            if (console == null || sActiveTextField == null)
            {
                return string.Empty;
            }

            return sActiveTextField.GetValue(console) as string ?? string.Empty;
        }

        /// <summary>
        /// 读取 Unity Console 当前选中条目的原生 file/line/message 元数据。
        /// 该 API 仅在 Editor 双击路由时调用，不进入日志写入热路径。
        /// </summary>
        /// <param name="filePath">当前条目的源文件路径。</param>
        /// <param name="line">当前条目的源文件行号。</param>
        /// <param name="message">当前条目的正文或调用堆栈文本。</param>
        /// <returns>成功读取选中条目时返回 true。</returns>
        internal static bool TryReadActiveEntry(out string filePath, out int line, out string message)
        {
            filePath = string.Empty;
            line = 0;
            message = string.Empty;

            object console = GetConsoleWindow();
            if (!CanReadActiveEntry(console))
            {
                return false;
            }

            int entryIndex = (int)sLastActiveEntryIndexField.GetValue(console);
            int entryCount = (int)sGetCountMethod.Invoke(null, null);
            if (entryIndex < 0 || entryIndex >= entryCount)
            {
                return false;
            }

            return TryReadEntry(entryIndex, out filePath, out line, out message);
        }

        /// <summary>
        /// 判断当前 Unity 版本是否具备读取选中 Console 条目的完整反射成员。
        /// </summary>
        /// <param name="console">当前 ConsoleWindow 实例。</param>
        /// <returns>所有必要成员存在时返回 true。</returns>
        private static bool CanReadActiveEntry(object console)
        {
            return console != null
                && sLastActiveEntryIndexField != null
                && sLogEntryType != null
                && sGetCountMethod != null
                && sGetEntryInternalMethod != null
                && sStartGettingEntriesMethod != null
                && sEndGettingEntriesMethod != null
                && sFileField != null
                && sLineField != null
                && sMessageField != null;
        }

        /// <summary>
        /// 在 Unity LogEntries 的读取事务内提取指定条目的文件、行号和正文。
        /// </summary>
        /// <param name="entryIndex">Unity Console 条目索引。</param>
        /// <param name="filePath">条目源文件路径。</param>
        /// <param name="line">条目源文件行号。</param>
        /// <param name="message">条目正文。</param>
        /// <returns>成功读取有效源文件位置时返回 true。</returns>
        private static bool TryReadEntry(int entryIndex, out string filePath, out int line, out string message)
        {
            filePath = string.Empty;
            line = 0;
            message = string.Empty;
            object entry = Activator.CreateInstance(sLogEntryType, true);
            bool started = false;
            try
            {
                sStartGettingEntriesMethod.Invoke(null, null);
                started = true;
                if (!(sGetEntryInternalMethod.Invoke(null, new[] { (object)entryIndex, entry }) is bool retrieved)
                    || !retrieved)
                {
                    return false;
                }

                filePath = sFileField.GetValue(entry) as string ?? string.Empty;
                line = sLineField.GetValue(entry) is int entryLine ? entryLine : 0;
                message = sMessageField.GetValue(entry) as string ?? string.Empty;
                return line > 0 && filePath.Length > 0;
            }
            finally
            {
                if (started)
                {
                    sEndGettingEntriesMethod.Invoke(null, null);
                }
            }
        }

        /// <summary>
        /// 查找 ConsoleWindow 上的私有静态字段。
        /// </summary>
        /// <param name="fieldName">字段名。</param>
        /// <returns>找到的字段；Unity 版本不支持时返回 null。</returns>
        private static FieldInfo FindStaticField(string fieldName)
        {
            Type t = ResolveConsoleWindowType();
            return t == null ? null : t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        }

        /// <summary>
        /// 查找 ConsoleWindow 上的私有实例字段。
        /// </summary>
        /// <param name="fieldName">字段名。</param>
        /// <returns>找到的字段；Unity 版本不支持时返回 null。</returns>
        private static FieldInfo FindInstanceField(string fieldName)
        {
            Type t = ResolveConsoleWindowType();
            return FindInstanceField(t, fieldName);
        }

        /// <summary>
        /// 查找指定类型上的私有实例字段。
        /// </summary>
        /// <param name="ownerType">声明字段的类型。</param>
        /// <param name="fieldName">字段名。</param>
        /// <returns>找到的字段；类型或字段不存在时返回 null。</returns>
        private static FieldInfo FindInstanceField(Type ownerType, string fieldName)
        {
            return ownerType == null
                ? null
                : ownerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// 获取 Unity ConsoleWindow 的单例；Console 未打开时返回 null。
        /// </summary>
        /// <returns>当前 ConsoleWindow 实例。</returns>
        private static object GetConsoleWindow()
        {
            return sConsoleWindowField == null ? null : sConsoleWindowField.GetValue(null);
        }

        /// <summary>
        /// 在 UnityEditor 程序集中按候选名称解析内部类型。
        /// </summary>
        /// <param name="typeNames">按优先级排列的候选全名。</param>
        /// <returns>解析出的类型；不存在时返回 null。</returns>
        private static Type ResolveType(params string[] typeNames)
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            for (int index = 0; index < typeNames.Length; index++)
            {
                Type resolvedType = editorAssembly.GetType(typeNames[index]);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        }

        /// <summary>
        /// 按静态方法名和参数类型查找 Unity 内部反射入口。
        /// </summary>
        /// <param name="ownerType">声明方法的类型。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="parameterTypes">参数类型列表。</param>
        /// <returns>找到的方法；参数或类型缺失时返回 null。</returns>
        private static MethodInfo FindStaticMethod(Type ownerType, string methodName, Type[] parameterTypes)
        {
            if (ownerType == null || parameterTypes == null)
            {
                return null;
            }

            for (int index = 0; index < parameterTypes.Length; index++)
            {
                if (parameterTypes[index] == null)
                {
                    return null;
                }
            }

            return ownerType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameterTypes,
                null);
        }

        /// <summary>
        /// 获取 Unity ConsoleWindow 类型。
        /// </summary>
        /// <returns>ConsoleWindow 类型；当前 Unity 版本不存在时返回 null。</returns>
        private static Type ResolveConsoleWindowType()
        {
            return typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
        }
    }
}

#endif
