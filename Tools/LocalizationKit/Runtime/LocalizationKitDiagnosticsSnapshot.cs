using System.Collections.Generic;

namespace YokiFrame
{
    internal sealed class LocalizationKitDiagnosticsSnapshot
    {
        internal LocalizationKitDiagnosticsSnapshot(
            LanguageId currentLanguage,
            LanguageId defaultLanguage,
            List<LocalizationLanguageDiagnosticsSnapshot> languages,
            string providerType,
            string formatterType,
            int binderCount,
            int textCacheCount,
            int pluralCacheCount)
        {
            CurrentLanguage = currentLanguage;
            DefaultLanguage = defaultLanguage;
            Languages = languages ?? new List<LocalizationLanguageDiagnosticsSnapshot>();
            ProviderType = providerType ?? string.Empty;
            FormatterType = formatterType ?? string.Empty;
            BinderCount = binderCount;
            TextCacheCount = textCacheCount;
            PluralCacheCount = pluralCacheCount;
        }

        internal LanguageId CurrentLanguage { get; }

        internal LanguageId DefaultLanguage { get; }

        internal List<LocalizationLanguageDiagnosticsSnapshot> Languages { get; }

        internal string ProviderType { get; }

        internal string FormatterType { get; }

        internal int BinderCount { get; }

        internal int TextCacheCount { get; }

        internal int PluralCacheCount { get; }
    }
}
