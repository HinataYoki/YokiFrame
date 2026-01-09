#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - TableKit 运行时操作
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region TableKit 操作

        /// <summary>
        /// 刷新编辑器缓存
        /// </summary>
        private void RefreshEditorCache()
        {
            var tablesType = FindTablesType();
            if (tablesType == null)
            {
                EditorUtility.DisplayDialog("TableKit", "cfg.Tables 类型不存在，请先生成配置表代码", "确定");
                return;
            }

            try
            {
                var tableKitType = FindTableKitType();
                if (tableKitType == null)
                {
                    EditorUtility.DisplayDialog("TableKit", "TableKit 类型不存在，请先生成配置表代码", "确定");
                    return;
                }

                tableKitType.GetMethod("SetEditorDataPath")?.Invoke(null, new object[] { mEditorDataPath });
                tableKitType.GetMethod("RefreshEditor")?.Invoke(null, null);

                var tables = tableKitType.GetProperty("TablesEditor")?.GetValue(null);
                RefreshTablesInfo(tables);

                mLogContent.value = $"[{DateTime.Now:HH:mm:ss}] [OK] 编辑器缓存已刷新";
                UpdateStatusBanner(BuildStatus.Success);
            }
            catch (Exception ex)
            {
                mLogContent.value = $"[{DateTime.Now:HH:mm:ss}] [FAIL] 加载配置表失败:\n{ex.Message}";
                UpdateStatusBanner(BuildStatus.Failed);
            }
        }

        /// <summary>
        /// 刷新配置表信息
        /// </summary>
        private void RefreshTablesInfo(object tables)
        {
            mCachedTables = tables;
            RefreshTablesInfoInternal(tables, mTablesSearchText);
        }

        /// <summary>
        /// 刷新配置表信息（内部实现，支持搜索过滤）
        /// </summary>
        private void RefreshTablesInfoInternal(object tables, string searchText)
        {
            mTablesInfoContainer.Clear();

            if (tables == null)
            {
                var hint = new Label("配置表未加载");
                hint.style.color = new StyleColor(Design.BrandDanger);
                hint.style.marginTop = 8;
                mTablesInfoContainer.Add(hint);
                return;
            }

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 250;
            mTablesInfoContainer.Add(scrollView);

            var properties = tables.GetType().GetProperties();
            var matchCount = 0;
            var totalCount = 0;

            foreach (var prop in properties)
            {
                if (prop.PropertyType.Namespace != "cfg") continue;
                totalCount++;

                // 搜索过滤：匹配属性名或类型名（不区分大小写）
                if (!string.IsNullOrEmpty(searchText))
                {
                    var lowerSearch = searchText.ToLowerInvariant();
                    var matchName = prop.Name.ToLowerInvariant().Contains(lowerSearch);
                    var matchType = prop.PropertyType.Name.ToLowerInvariant().Contains(lowerSearch);
                    if (!matchName && !matchType) continue;
                }

                matchCount++;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                var nameLabel = new Label($"• {prop.Name}");
                nameLabel.style.width = 150;
                nameLabel.style.color = new StyleColor(Design.TextPrimary);
                row.Add(nameLabel);

                var typeLabel = new Label(prop.PropertyType.Name);
                typeLabel.style.color = new StyleColor(Design.BrandSuccess);
                row.Add(typeLabel);

                scrollView.Add(row);
            }

            // 显示统计信息
            var statsLabel = new Label();
            statsLabel.style.marginTop = 8;
            statsLabel.style.fontSize = Design.FontSizeSmall;
            statsLabel.style.color = new StyleColor(Design.TextTertiary);

            if (string.IsNullOrEmpty(searchText))
            {
                statsLabel.text = $"共 {totalCount} 个配置表";
            }
            else
            {
                statsLabel.text = $"找到 {matchCount}/{totalCount} 个匹配项";
            }
            mTablesInfoContainer.Add(statsLabel);

            if (matchCount == 0 && !string.IsNullOrEmpty(searchText))
            {
                var noResult = new Label("没有找到匹配的配置表");
                noResult.style.color = new StyleColor(Design.BrandWarning);
                noResult.style.marginTop = 8;
                scrollView.Add(noResult);
            }
        }

        private Type FindTablesType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("cfg.Tables");
                if (type != null) return type;
            }
            return null;
        }

        private Type FindTableKitType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("TableKit");
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
#endif
