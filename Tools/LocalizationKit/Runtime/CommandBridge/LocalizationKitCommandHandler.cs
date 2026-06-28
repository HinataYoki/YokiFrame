using System;
using System.Text;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 命令桥处理器。
    /// 工作台、AI 和 snapshot publisher 共用这一个只读状态出口；切换语言是显式用户动作。
    /// </summary>
    public sealed class LocalizationKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        /// <inheritdoc />
        public string KitName => "LocalizationKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "list_languages",
            "get_workbench_snapshot",
            "set_language"
        };

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            LocalizationKitDiagnosticsSnapshot snapshot = LocalizationKit.CreateDiagnosticsSnapshot();
            return LocalizationKit.DiagnosticVersion.ToString() + ":" + BuildStatsJson(snapshot);
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson(LocalizationKit.CreateDiagnosticsSnapshot());
                case "list_languages":
                    return BuildLanguagesJson(LocalizationKit.CreateDiagnosticsSnapshot().Languages);
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                case "set_language":
                    return SetLanguage(payloadJson);
                default:
                    throw new NotSupportedException("Unknown LocalizationKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            LocalizationKitDiagnosticsSnapshot snapshot = LocalizationKit.CreateDiagnosticsSnapshot();
            string stats = BuildStatsJson(snapshot);
            string languages = BuildLanguagesJson(snapshot.Languages);

            var sb = new StringBuilder(stats.Length + languages.Length + 64);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"languages\":");
            sb.Append(languages);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson(LocalizationKitDiagnosticsSnapshot snapshot)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"currentLanguage\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.CurrentLanguage.ToString()));
            sb.Append("\",\"defaultLanguage\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.DefaultLanguage.ToString()));
            sb.Append("\",\"availableLanguageCount\":");
            sb.Append(snapshot.Languages.Count);
            sb.Append(",\"binderCount\":");
            sb.Append(snapshot.BinderCount);
            sb.Append(",\"textCacheCount\":");
            sb.Append(snapshot.TextCacheCount);
            sb.Append(",\"pluralCacheCount\":");
            sb.Append(snapshot.PluralCacheCount);
            sb.Append(",\"providerType\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.ProviderType));
            sb.Append("\",\"formatterType\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.FormatterType));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static string BuildLanguagesJson(System.Collections.Generic.List<LocalizationLanguageDiagnosticsSnapshot> languages)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"languages\":[");
            for (int i = 0; i < languages.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendLanguage(sb, languages[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(languages.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SetLanguage(string payloadJson)
        {
            LanguageId language;
            if (!TryReadLanguage(payloadJson, out language))
                throw new ArgumentException("Missing or invalid 'language' in payload");

            LanguageId before = LocalizationKit.GetCurrentLanguage();
            bool changed = LocalizationKit.SetLanguage(language);
            LanguageId current = LocalizationKit.GetCurrentLanguage();

            var sb = new StringBuilder(128);
            sb.Append("{\"changed\":");
            sb.Append(changed && before != current ? "true" : "false");
            sb.Append(",\"accepted\":");
            sb.Append(changed ? "true" : "false");
            sb.Append(",\"currentLanguage\":\"");
            sb.Append(JsonHelper.EscapeString(current.ToString()));
            sb.Append("\",\"requestedLanguage\":\"");
            sb.Append(JsonHelper.EscapeString(language.ToString()));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static bool TryReadLanguage(string payloadJson, out LanguageId language)
        {
            language = default(LanguageId);
            string value = JsonHelper.ExtractString(payloadJson ?? string.Empty, "language");
            if (string.IsNullOrEmpty(value))
                value = JsonHelper.ExtractString(payloadJson ?? string.Empty, "languageId");

            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out language))
                return IsDefinedLanguage(language);

            int id;
            if (JsonHelper.TryExtractInt(payloadJson ?? string.Empty, "language", out id) ||
                JsonHelper.TryExtractInt(payloadJson ?? string.Empty, "languageId", out id))
            {
                language = (LanguageId)id;
                if (IsDefinedLanguage(language))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDefinedLanguage(LanguageId language)
        {
            switch (language)
            {
                case LanguageId.ChineseSimplified:
                case LanguageId.ChineseTraditional:
                case LanguageId.English:
                case LanguageId.Japanese:
                case LanguageId.Korean:
                case LanguageId.French:
                case LanguageId.German:
                case LanguageId.Spanish:
                case LanguageId.Portuguese:
                case LanguageId.Russian:
                case LanguageId.Arabic:
                case LanguageId.Thai:
                case LanguageId.Vietnamese:
                case LanguageId.Indonesian:
                    return true;
                default:
                    return false;
            }
        }

        private static void AppendLanguage(StringBuilder sb, LocalizationLanguageDiagnosticsSnapshot language)
        {
            LanguageInfo info = language.Info;
            sb.Append("{\"id\":\"");
            sb.Append(JsonHelper.EscapeString(language.Id.ToString()));
            sb.Append("\",\"numericId\":");
            sb.Append((int)language.Id);
            sb.Append(",\"displayNameTextId\":");
            sb.Append(info.DisplayNameTextId);
            sb.Append(",\"nativeNameTextId\":");
            sb.Append(info.NativeNameTextId);
            sb.Append(",\"iconSpriteId\":");
            sb.Append(info.IconSpriteId);
            sb.Append(",\"isLoaded\":");
            sb.Append(language.IsLoaded ? "true" : "false");
            sb.Append(",\"isCurrent\":");
            sb.Append(LocalizationKit.GetCurrentLanguage() == language.Id ? "true" : "false");
            sb.Append(",\"isDefault\":");
            sb.Append(LocalizationKit.GetDefaultLanguage() == language.Id ? "true" : "false");
            sb.Append(",\"hasInfo\":");
            sb.Append(info.IsValid ? "true" : "false");
            sb.Append('}');
        }
    }
}
