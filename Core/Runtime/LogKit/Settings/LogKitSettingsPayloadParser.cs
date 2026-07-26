#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>
    /// 严格解析 Workbench 提交的 LogKit 顶层扁平设置对象，拒绝重复、嵌套和类型漂移。
    /// </summary>
    internal static class LogKitSettingsPayloadParser
    {
        private const int MAX_PAYLOAD_LENGTH = 32 * 1024;
        private const int ENABLED_BIT = 1 << 0;
        private const int MINIMUM_LEVEL_BIT = 1 << 1;
        private const int SAVE_EDITOR_BIT = 1 << 2;
        private const int SAVE_PLAYER_BIT = 1 << 3;
        private const int PLAYER_IMGUI_BIT = 1 << 4;
        private const int ENCRYPTION_BIT = 1 << 5;
        private const int QUEUE_SIZE_BIT = 1 << 6;
        private const int SAME_LOG_BIT = 1 << 7;
        private const int RETENTION_BIT = 1 << 8;
        private const int FILE_SIZE_BIT = 1 << 9;
        private const int IMGUI_COUNT_BIT = 1 << 10;
        private const int DIRECTORY_BIT = 1 << 11;
        private const int EDITOR_FILE_BIT = 1 << 12;
        private const int PLAYER_FILE_BIT = 1 << 13;
        private const int ALL_FIELDS = (1 << 14) - 1;

        /// <summary>
        /// 解析完整设置对象；只接受固定字段、标准 JSON primitive 和对象结束后的空白。
        /// </summary>
        /// <param name="json">待解析的完整 payload。</param>
        /// <param name="payload">成功时返回强类型设置。</param>
        /// <param name="errorMessage">失败时返回可显示原因。</param>
        /// <returns>语法、字段和 primitive 类型全部有效时返回 true。</returns>
        internal static bool TryParse(
            string json,
            out LogKitSettingsPayload payload,
            out string errorMessage)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MAX_PAYLOAD_LENGTH)
            {
                errorMessage = "LogKit settings payload is empty or too large.";
                return false;
            }

            var candidate = new LogKitSettingsPayload();
            int index = 0;
            int fieldMask = 0;
            if (!TryReadObject(json, ref index, candidate, ref fieldMask, out errorMessage))
            {
                return false;
            }

            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            if (index != json.Length || fieldMask != ALL_FIELDS)
            {
                errorMessage = index != json.Length
                    ? "LogKit settings payload contains trailing JSON content."
                    : "LogKit settings payload must contain every required setting exactly once.";
                return false;
            }

            payload = candidate;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>读取唯一顶层对象，并在每个逗号边界验证属性结构。</summary>
        private static bool TryReadObject(
            string json, ref int index, LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            if (!LogKitSettingsJsonReader.Consume(json, ref index, '{'))
            {
                errorMessage = "LogKit settings payload must be a JSON object.";
                return false;
            }

            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            if (LogKitSettingsJsonReader.Consume(json, ref index, '}'))
            {
                errorMessage = "LogKit settings payload cannot be empty.";
                return false;
            }

            while (index < json.Length)
            {
                if (!TryReadProperty(json, ref index, payload, ref fieldMask, out errorMessage))
                {
                    return false;
                }

                LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
                if (LogKitSettingsJsonReader.Consume(json, ref index, '}'))
                {
                    return true;
                }

                if (!LogKitSettingsJsonReader.Consume(json, ref index, ','))
                {
                    errorMessage = "LogKit settings properties must be separated by commas.";
                    return false;
                }

                LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}')
                {
                    errorMessage = "LogKit settings payload cannot contain a trailing comma.";
                    return false;
                }
            }

            errorMessage = "LogKit settings payload is missing its closing object delimiter.";
            return false;
        }

        /// <summary>读取一个属性名、冒号及其固定类型值。</summary>
        private static bool TryReadProperty(
            string json,
            ref int index,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            if (!LogKitSettingsJsonReader.TryReadString(
                    json, ref index, out var key, out errorMessage))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "LogKit settings property names must be JSON strings.";
                }

                return false;
            }

            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            if (!LogKitSettingsJsonReader.Consume(json, ref index, ':'))
            {
                errorMessage = "LogKit setting '" + key + "' is missing a colon.";
                return false;
            }

            LogKitSettingsJsonReader.SkipWhitespace(json, ref index);
            return TryReadSetting(json, ref index, key, payload, ref fieldMask, out errorMessage);
        }

        /// <summary>按固定字段目录选择严格 primitive reader，并拒绝未知字段。</summary>
        private static bool TryReadSetting(
            string json,
            ref int index,
            string key,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            int fieldBit = GetBooleanFieldBit(key);
            if (fieldBit != 0)
            {
                return TryReadBooleanSetting(
                    json, ref index, key, fieldBit, payload, ref fieldMask, out errorMessage);
            }

            fieldBit = GetIntegerFieldBit(key);
            if (fieldBit != 0)
            {
                return TryReadIntegerSetting(
                    json, ref index, key, fieldBit, payload, ref fieldMask, out errorMessage);
            }

            fieldBit = GetStringFieldBit(key);
            if (fieldBit != 0)
            {
                return TryReadStringSetting(
                    json, ref index, key, fieldBit, payload, ref fieldMask, out errorMessage);
            }

            if (string.Equals(key, LogKitSettings.MINIMUM_LEVEL_KEY, StringComparison.Ordinal))
            {
                return TryReadLevelSetting(
                    json, ref index, key, payload, ref fieldMask, out errorMessage);
            }

            errorMessage = "Unknown LogKit setting: " + key;
            return false;
        }

        /// <summary>读取并保存固定布尔字段。</summary>
        private static bool TryReadBooleanSetting(
            string json,
            ref int index,
            string key,
            int fieldBit,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            if (!TryClaimField(key, fieldBit, ref fieldMask, out errorMessage)
                || !LogKitSettingsJsonReader.TryReadBoolean(json, ref index, out var value))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "LogKit setting '" + key + "' must be a JSON boolean.";
                }

                return false;
            }

            AssignBoolean(payload, fieldBit, value);
            return true;
        }

        /// <summary>读取并保存固定整数字段。</summary>
        private static bool TryReadIntegerSetting(
            string json,
            ref int index,
            string key,
            int fieldBit,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            if (!TryClaimField(key, fieldBit, ref fieldMask, out errorMessage)
                || !LogKitSettingsJsonReader.TryReadInteger(json, ref index, out var value))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "LogKit setting '" + key + "' must be a JSON integer.";
                }

                return false;
            }

            AssignInteger(payload, fieldBit, value);
            return true;
        }

        /// <summary>读取并保存固定字符串字段。</summary>
        private static bool TryReadStringSetting(
            string json,
            ref int index,
            string key,
            int fieldBit,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            if (!TryClaimField(key, fieldBit, ref fieldMask, out errorMessage)
                || !LogKitSettingsJsonReader.TryReadString(
                    json, ref index, out var value, out errorMessage))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "LogKit setting '" + key + "' must be a JSON string.";
                }

                return false;
            }

            AssignString(payload, fieldBit, value);
            return true;
        }

        /// <summary>读取最低等级字符串，并只接受四个正式枚举名称。</summary>
        private static bool TryReadLevelSetting(
            string json,
            ref int index,
            string key,
            LogKitSettingsPayload payload,
            ref int fieldMask,
            out string errorMessage)
        {
            if (!TryClaimField(key, MINIMUM_LEVEL_BIT, ref fieldMask, out errorMessage)
                || !LogKitSettingsJsonReader.TryReadString(
                    json, ref index, out var value, out errorMessage)
                || !TryParseLevel(value, out payload.MinimumLevel))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = "LogKit minimumLevel must be Debug, Info, Warning or Error.";
                }

                return false;
            }

            return true;
        }

        /// <summary>声明字段只出现一次，防止后值静默覆盖前值。</summary>
        private static bool TryClaimField(
            string key,
            int fieldBit,
            ref int fieldMask,
            out string errorMessage)
        {
            if ((fieldMask & fieldBit) != 0)
            {
                errorMessage = "Duplicate LogKit setting: " + key;
                return false;
            }

            fieldMask |= fieldBit;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>返回固定布尔字段对应的位标识。</summary>
        private static int GetBooleanFieldBit(string key)
        {
            if (key == LogKitSettings.ENABLED_KEY) return ENABLED_BIT;
            if (key == LogKitSettings.SAVE_LOG_IN_EDITOR_KEY) return SAVE_EDITOR_BIT;
            if (key == LogKitSettings.SAVE_LOG_IN_PLAYER_KEY) return SAVE_PLAYER_BIT;
            if (key == LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY) return PLAYER_IMGUI_BIT;
            return key == LogKitSettings.ENABLE_ENCRYPTION_KEY ? ENCRYPTION_BIT : 0;
        }

        /// <summary>返回固定整数字段对应的位标识。</summary>
        private static int GetIntegerFieldBit(string key)
        {
            if (key == LogKitSettings.MAX_QUEUE_SIZE_KEY) return QUEUE_SIZE_BIT;
            if (key == LogKitSettings.MAX_SAME_LOG_COUNT_KEY) return SAME_LOG_BIT;
            if (key == LogKitSettings.MAX_RETENTION_DAYS_KEY) return RETENTION_BIT;
            if (key == LogKitSettings.MAX_FILE_SIZE_MB_KEY) return FILE_SIZE_BIT;
            return key == LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY ? IMGUI_COUNT_BIT : 0;
        }

        /// <summary>返回固定字符串字段对应的位标识。</summary>
        private static int GetStringFieldBit(string key)
        {
            if (key == LogKitSettings.LOG_DIRECTORY_KEY) return DIRECTORY_BIT;
            if (key == LogKitSettings.EDITOR_FILE_NAME_KEY) return EDITOR_FILE_BIT;
            return key == LogKitSettings.PLAYER_FILE_NAME_KEY ? PLAYER_FILE_BIT : 0;
        }

        /// <summary>把布尔字段写入强类型 payload。</summary>
        private static void AssignBoolean(LogKitSettingsPayload payload, int fieldBit, bool value)
        {
            if (fieldBit == ENABLED_BIT) payload.Enabled = value;
            else if (fieldBit == SAVE_EDITOR_BIT) payload.SaveLogInEditor = value;
            else if (fieldBit == SAVE_PLAYER_BIT) payload.SaveLogInPlayer = value;
            else if (fieldBit == PLAYER_IMGUI_BIT) payload.EnableImGuiInPlayer = value;
            else payload.EnableEncryption = value;
        }

        /// <summary>把整数字段写入强类型 payload。</summary>
        private static void AssignInteger(LogKitSettingsPayload payload, int fieldBit, int value)
        {
            if (fieldBit == QUEUE_SIZE_BIT) payload.MaxQueueSize = value;
            else if (fieldBit == SAME_LOG_BIT) payload.MaxSameLogCount = value;
            else if (fieldBit == RETENTION_BIT) payload.MaxRetentionDays = value;
            else if (fieldBit == FILE_SIZE_BIT) payload.MaxFileSizeMb = value;
            else payload.ImGuiMaxLogCount = value;
        }

        /// <summary>把字符串字段写入强类型 payload。</summary>
        private static void AssignString(LogKitSettingsPayload payload, int fieldBit, string value)
        {
            if (fieldBit == DIRECTORY_BIT) payload.LogDirectory = value;
            else if (fieldBit == EDITOR_FILE_BIT) payload.EditorFileName = value;
            else payload.PlayerFileName = value;
        }

        /// <summary>把最低等级名称解析为固定枚举，拒绝数字枚举文本。</summary>
        private static bool TryParseLevel(string value, out LogLevel level)
        {
            if (string.Equals(value, "Debug", StringComparison.OrdinalIgnoreCase)) level = LogLevel.Debug;
            else if (string.Equals(value, "Info", StringComparison.OrdinalIgnoreCase)) level = LogLevel.Info;
            else if (string.Equals(value, "Warning", StringComparison.OrdinalIgnoreCase)) level = LogLevel.Warning;
            else if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase)) level = LogLevel.Error;
            else
            {
                level = LogLevel.Debug;
                return false;
            }

            return true;
        }

    }

    /// <summary>保存严格 parser 输出的完整 LogKit 设置，只有校验通过后才写入 Store。</summary>
    internal sealed class LogKitSettingsPayload
    {
        internal bool Enabled;
        internal LogLevel MinimumLevel;
        internal bool SaveLogInEditor;
        internal bool SaveLogInPlayer;
        internal bool EnableImGuiInPlayer;
        internal bool EnableEncryption;
        internal int MaxQueueSize;
        internal int MaxSameLogCount;
        internal int MaxRetentionDays;
        internal int MaxFileSizeMb;
        internal int ImGuiMaxLogCount;
        internal string LogDirectory = string.Empty;
        internal string EditorFileName = string.Empty;
        internal string PlayerFileName = string.Empty;
    }
}
#endif
