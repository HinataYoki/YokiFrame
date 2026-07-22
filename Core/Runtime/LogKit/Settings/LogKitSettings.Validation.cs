#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>承载 set_settings 完整对象的语义校验和原子应用。</summary>
    public static partial class LogKitSettings
    {
        private static readonly char[] sInvalidPathChars = Path.GetInvalidPathChars();

        /// <summary>
        /// 严格解析并原子应用 Workbench 提交的完整设置；失败时不修改任何当前会话值。
        /// </summary>
        /// <param name="payloadJson">直接铺开全部设置字段的顶层 JSON 对象。</param>
        /// <param name="errorMessage">失败时返回可显示原因。</param>
        /// <returns>全部字段通过语法和语义校验并已应用时返回 true。</returns>
        internal static bool TryApplyCompletePayload(string payloadJson, out string errorMessage)
        {
            if (!LogKitSettingsPayloadParser.TryParse(
                    payloadJson,
                    out var payload,
                    out errorMessage)
                || !TryValidatePayload(payload, out errorMessage))
            {
                return false;
            }

            ApplyValidatedPayload(payload);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>校验数值下限、目录长度和两个安全文件名。</summary>
        private static bool TryValidatePayload(
            LogKitSettingsPayload payload,
            out string errorMessage)
        {
            if (payload.MaxQueueSize < 1
                || payload.MaxSameLogCount < 0
                || payload.MaxRetentionDays < 1
                || payload.MaxFileSizeMb < 1
                || payload.ImGuiMaxLogCount < 1)
            {
                errorMessage = "LogKit numeric settings are outside their supported ranges.";
                return false;
            }

            if (!IsSafeDirectory(payload.LogDirectory))
            {
                errorMessage = "LogKit logDirectory is too long or contains invalid path characters.";
                return false;
            }

            if (!IsSafeFileName(payload.EditorFileName)
                || !IsSafeFileName(payload.PlayerFileName))
            {
                errorMessage = "LogKit file names must be non-empty file names without directory segments.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>一次写入已经完整校验的设置，再同步 Runtime 并只推进一次设置版本。</summary>
        private static void ApplyValidatedPayload(LogKitSettingsPayload payload)
        {
            KitSettings.SetBool(KIT_NAME, ENABLED_KEY, payload.Enabled);
            KitSettings.SetString(KIT_NAME, MINIMUM_LEVEL_KEY, payload.MinimumLevel.ToString());
            KitSettings.SetBool(KIT_NAME, SAVE_LOG_IN_EDITOR_KEY, payload.SaveLogInEditor);
            KitSettings.SetBool(KIT_NAME, SAVE_LOG_IN_PLAYER_KEY, payload.SaveLogInPlayer);
            KitSettings.SetBool(KIT_NAME, ENABLE_IMGUI_IN_PLAYER_KEY, payload.EnableImGuiInPlayer);
            KitSettings.SetBool(KIT_NAME, ENABLE_ENCRYPTION_KEY, payload.EnableEncryption);
            KitSettings.SetInt(KIT_NAME, MAX_QUEUE_SIZE_KEY, payload.MaxQueueSize);
            KitSettings.SetInt(KIT_NAME, MAX_SAME_LOG_COUNT_KEY, payload.MaxSameLogCount);
            KitSettings.SetInt(KIT_NAME, MAX_RETENTION_DAYS_KEY, payload.MaxRetentionDays);
            KitSettings.SetInt(KIT_NAME, MAX_FILE_SIZE_MB_KEY, payload.MaxFileSizeMb);
            KitSettings.SetInt(KIT_NAME, IMGUI_MAX_LOG_COUNT_KEY, payload.ImGuiMaxLogCount);
            KitSettings.SetString(KIT_NAME, LOG_DIRECTORY_KEY, payload.LogDirectory);
            KitSettings.SetString(KIT_NAME, EDITOR_FILE_NAME_KEY, payload.EditorFileName);
            KitSettings.SetString(KIT_NAME, PLAYER_FILE_NAME_KEY, payload.PlayerFileName);
            ApplyBaseRuntimeSettings();
            BumpSettingsVersion();
        }

        /// <summary>判断目录配置是否长度受限且不含当前平台非法路径字符；空值表示宿主默认目录。</summary>
        private static bool IsSafeDirectory(string value)
        {
            return value != null
                && value.Length <= 4096
                && value.IndexOfAny(sInvalidPathChars) < 0;
        }

        /// <summary>判断文件名是否为长度受限且不包含目录或非法字符的单段名称。</summary>
        private static bool IsSafeFileName(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= 255
                && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
#endif
