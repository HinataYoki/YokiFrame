using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 LocalizationKit 目录过滤、覆盖率和投影。</summary>
public sealed partial class LocalizationKitPageViewModel
{
    private void ApplyCatalog(LocalizationCatalog catalog)
    {
        mCatalog = catalog;
        mCatalogLanguages = catalog.Languages;
        ProviderText = catalog.Provider + " · " + Path.GetFileName(catalog.SourcePath);
        UpdateLanguageOptions(catalog.Languages);
        RebuildLanguageCoverage(catalog);
        SetStatistics(catalog.Languages.Count, catalog.Entries.Count, catalog.MissingEntryCount);
        ApplyFilters();
    }

    /// <summary>把加载失败状态投影到页面，并清除已失效的旧项目数据。</summary>
    private void ApplyLoadFailure(LocalizationOperationResult result)
    {
        mCatalog = null;
        mHasLoadError = true;
        ClearCatalogProjection();
        ProviderText = string.IsNullOrWhiteSpace(result.Provider) ? GetString(UnknownProviderKey, "未知") : result.Provider;
        SetStatistics(0, 0, 0);
        SetStatus(string.Format(GetString(LoadFailedStatusTemplateKey, "失败: {0}"), string.Join("; ", result.Diagnostics)));
    }

    /// <summary>基于缓存目录应用搜索、缺失和语言筛选，不执行文件 IO。</summary>
    private void ApplyFilters()
    {
        if (mCatalog is null)
        {
            return;
        }

        int? selectedId = SelectedEntry?.Id;
        IReadOnlyList<LocalizationEntryRecord> filteredEntries = mService.Filter(
            mCatalog,
            SearchText,
            MissingOnly,
            PAGE_ENTRY_LIMIT);
        Entries.Clear();
        foreach (LocalizationEntryRecord entry in filteredEntries)
        {
            if (MatchesLanguage(entry))
            {
                Entries.Add(entry);
            }
        }

        SelectedEntry = selectedId.HasValue
            ? Entries.FirstOrDefault(entry => entry.Id == selectedId.Value) ?? Entries.FirstOrDefault()
            : Entries.FirstOrDefault();
        mHasLoadError = false;
        SetStatus(string.Format(
            GetString(LoadedEntriesTemplateKey, "已加载 {0} / {1} 条"), Entries.Count, EntryCount));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>重建目录级语言覆盖统计，并与缺失判定复用同一值存在规则。</summary>
    private void RebuildLanguageCoverage(LocalizationCatalog catalog)
    {
        LanguageCoverage.Clear();
        foreach (LocalizationLanguageRecord language in catalog.Languages)
        {
            int presentCount = 0;
            foreach (LocalizationEntryRecord entry in catalog.Entries)
            {
                if (entry.HasValueFor(language.Id))
                {
                    presentCount++;
                }
            }

            LanguageCoverage.Add(new LocalizationLanguageCoverageViewModel(
                language.Id,
                presentCount,
                catalog.Entries.Count - presentCount));
        }
    }

    /// <summary>同步语言选项并恢复仍然有效的筛选值。</summary>
    private void UpdateLanguageOptions(IReadOnlyList<LocalizationLanguageRecord> languages)
    {
        // 语言筛选使用展示值存储；“全部”哨兵在 Normalize 中与各语言标签互转。
        if (NormalizeLanguageFilter(mSelectedLanguage) == LANGUAGE_ALL)
        {
            mSelectedLanguage = GetString(LanguageAllKey, "全部");
        }

        string selectedLanguage = mSelectedLanguage;
        string[] nextOptions = new[] { GetString(LanguageAllKey, "全部") }.Concat(languages.Select(static language => language.Id)).ToArray();
        if (!LanguageOptions.SequenceEqual(nextOptions))
        {
            LanguageOptions.Clear();
            foreach (string option in nextOptions)
            {
                LanguageOptions.Add(option);
            }
        }

        string normalizedLanguage = nextOptions.Contains(selectedLanguage, StringComparer.Ordinal) ? selectedLanguage : "全部";
        if (!string.Equals(mSelectedLanguage, normalizedLanguage, StringComparison.Ordinal))
        {
            mSelectedLanguage = normalizedLanguage;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(HasActiveFilters));
        }
    }

    /// <summary>按当前语言筛选存在可显示文本的条目。</summary>
    private bool MatchesLanguage(LocalizationEntryRecord entry)
    {
        return NormalizeLanguageFilter(SelectedLanguage) == LANGUAGE_ALL
            || entry.HasValueFor(SelectedLanguage);
    }

    /// <summary>按目录语言顺序重建当前条目的对照预览。</summary>
    private void RebuildSelectedValueRows()
    {
        SelectedValueRows.Clear();
        if (SelectedEntry is null)
        {
            return;
        }

        foreach (LocalizationLanguageRecord language in mCatalogLanguages)
        {
            bool hasText = SelectedEntry.Values.TryGetValue(language.Id, out string? value)
                && !string.IsNullOrWhiteSpace(value);
            string pluralValue = string.Empty;
            if (SelectedEntry.PluralValues.TryGetValue(language.Id, out IReadOnlyDictionary<string, string>? plural))
            {
                pluralValue = string.Join(" · ", plural.Select(pair => pair.Key + " = " + pair.Value));
            }

            SelectedValueRows.Add(new LocalizationPreviewValueViewModel(
                language.Id,
                hasText ? value! : GetString(NotConfiguredKey, "未配置"),
                pluralValue,
                !SelectedEntry.HasValueFor(language.Id)));
        }
    }

    /// <summary>清理旧目录投影，使不同项目的条目和覆盖率不会交叉显示。</summary>
    private void ClearCatalogProjection()
    {
        mCatalogLanguages = Array.Empty<LocalizationLanguageRecord>();
        Entries.Clear();
        LanguageCoverage.Clear();
        SelectedEntry = null;
        string allLabel = GetString(LanguageAllKey, "全部");
        if (LanguageOptions.Count != 1 || LanguageOptions[0] != allLabel)
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(allLabel);
        }

        if (NormalizeLanguageFilter(mSelectedLanguage) != LANGUAGE_ALL)
        {
            mSelectedLanguage = allLabel;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>使缓存失效并提升加载版本，防止旧文件读取结果覆盖新项目状态。</summary>
    private void InvalidateCatalog(string statusText)
    {
        mCatalog = null;
        mIsRefreshing = false;
        mLoadVersion++;
        ClearCatalogProjection();
        ProviderText = "Json";
        SetStatistics(0, 0, 0);
        SetStatus(statusText);
    }

    /// <summary>更新顶部统计字段并通知依赖的摘要绑定。</summary>
    private void SetStatistics(int languageCount, int entryCount, int missingEntryCount)
    {
        LanguageCount = languageCount;
        EntryCount = entryCount;
        MissingEntryCount = missingEntryCount;
        OnPropertyChanged(nameof(LanguageCount));
        OnPropertyChanged(nameof(EntryCount));
        OnPropertyChanged(nameof(MissingEntryCount));
        OnPropertyChanged(nameof(SummaryText));
    }

}
