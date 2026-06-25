namespace YokiFrame
{
    internal readonly struct LocalizationLanguageDiagnosticsSnapshot
    {
        internal LocalizationLanguageDiagnosticsSnapshot(LanguageId id, LanguageInfo info, bool isLoaded)
        {
            Id = id;
            Info = info;
            IsLoaded = isLoaded;
        }

        internal LanguageId Id { get; }

        internal LanguageInfo Info { get; }

        internal bool IsLoaded { get; }
    }
}
