#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 数据预览功能
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 数据预览字段

        private string mCurrentPreviewJsonPath;
        private string mDataPreviewSearchText;
        private ScrollView mDataPreviewTreeContainer;
        private Label mDataPreviewMatchLabel;
        private string[] mCachedJsonFiles;
        private int mSearchMatchCount;
        private VisualElement mFirstMatchElement;

        #endregion

        #region E. 数据预览区块

        /// <summary>
        /// 构建数据预览区块
        /// </summary>
        private VisualElement BuildDataPreview()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 12;
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 12;

            // 标题行
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginBottom = 8;
            container.Add(headerRow);

            var title = new Label("数据预览");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            headerRow.Add(title);

            // 搜索行
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;
            headerRow.Add(searchRow);

            var searchField = new TextField();
            searchField.style.width = 150;
            searchField.value = "";
            searchField.RegisterValueChangedCallback(evt =>
            {
                mDataPreviewSearchText = evt.newValue;
                RefreshDataPreviewWithSearch();
            });
            searchRow.Add(searchField);

            mDataPreviewMatchLabel = new Label();
            mDataPreviewMatchLabel.style.marginLeft = 8;
            mDataPreviewMatchLabel.style.fontSize = Design.FontSizeSmall;
            mDataPreviewMatchLabel.style.display = DisplayStyle.None;
            searchRow.Add(mDataPreviewMatchLabel);

            // 内容容器
            mDataPreviewContainer = new VisualElement();
            container.Add(mDataPreviewContainer);

            var hint = new Label("点击「验证配置」后显示数据预览");
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            mDataPreviewContainer.Add(hint);

            return container;
        }

        #endregion

        #region 数据预览加载

        /// <summary>
        /// 加载数据预览
        /// </summary>
        private void LoadDataPreview(string dataDir, System.Text.StringBuilder logBuilder)
        {
            mDataPreviewContainer.Clear();
            mCurrentPreviewJsonPath = null;
            mCachedJsonFiles = null;

            if (!Directory.Exists(dataDir))
            {
                AddPreviewHint("验证数据目录不存在", Design.BrandDanger);
                return;
            }

            var jsonFiles = Directory.GetFiles(dataDir, "*.json");
            if (jsonFiles.Length == 0)
            {
                AddPreviewHint("没有找到 JSON 数据文件", Design.BrandDanger);
                return;
            }

            mCachedJsonFiles = jsonFiles;
            logBuilder.AppendLine($"✓ 找到 {jsonFiles.Length} 个数据文件");

            var fileNames = new List<string>();
            foreach (var file in jsonFiles) fileNames.Add(Path.GetFileNameWithoutExtension(file));

            // 选择下拉
            var selectRow = new VisualElement();
            selectRow.style.flexDirection = FlexDirection.Row;
            selectRow.style.alignItems = Align.Center;
            selectRow.style.marginTop = 8;
            mDataPreviewContainer.Add(selectRow);

            var selectLabel = new Label("选择配置表:");
            selectLabel.style.width = 80;
            selectLabel.style.color = new StyleColor(Design.TextSecondary);
            selectRow.Add(selectLabel);

            var dropdown = new DropdownField(fileNames, 0);
            dropdown.style.flexGrow = 1;
            selectRow.Add(dropdown);

            // 树形容器
            mDataPreviewTreeContainer = new ScrollView { name = "tree-container" };
            mDataPreviewTreeContainer.style.marginTop = 8;
            mDataPreviewTreeContainer.style.maxHeight = 300;
            mDataPreviewTreeContainer.style.backgroundColor = new StyleColor(Design.LayerConsole);
            mDataPreviewTreeContainer.style.borderTopLeftRadius = mDataPreviewTreeContainer.style.borderTopRightRadius = 4;
            mDataPreviewTreeContainer.style.borderBottomLeftRadius = mDataPreviewTreeContainer.style.borderBottomRightRadius = 4;
            mDataPreviewContainer.Add(mDataPreviewTreeContainer);

            mCurrentPreviewJsonPath = jsonFiles[0];
            LoadJsonToTreeWithSearch(jsonFiles[0], mDataPreviewTreeContainer, mDataPreviewSearchText);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = fileNames.IndexOf(evt.newValue);
                if (idx >= 0 && idx < jsonFiles.Length)
                {
                    mCurrentPreviewJsonPath = jsonFiles[idx];
                    LoadJsonToTreeWithSearch(jsonFiles[idx], mDataPreviewTreeContainer, mDataPreviewSearchText);
                }
            });
        }

        /// <summary>
        /// 刷新数据预览（带搜索）
        /// </summary>
        private void RefreshDataPreviewWithSearch()
        {
            if (mDataPreviewTreeContainer == null || string.IsNullOrEmpty(mCurrentPreviewJsonPath)) return;
            LoadJsonToTreeWithSearch(mCurrentPreviewJsonPath, mDataPreviewTreeContainer, mDataPreviewSearchText);
        }

        private void AddPreviewHint(string message, Color color)
        {
            var hint = new Label(message);
            hint.style.color = new StyleColor(color);
            hint.style.marginTop = 8;
            mDataPreviewContainer.Add(hint);
        }

        #endregion

        #region JSON 树形视图

        private void LoadJsonToTree(string jsonPath, ScrollView container) =>
            LoadJsonToTreeWithSearch(jsonPath, container, "");

        private void LoadJsonToTreeWithSearch(string jsonPath, ScrollView container, string searchText)
        {
            container.Clear();
            mSearchMatchCount = 0;
            mFirstMatchElement = null;

            try
            {
                var json = JSON.Parse(File.ReadAllText(jsonPath));
                if (json == null)
                {
                    AddTreeError(container, "JSON 解析失败");
                    UpdateMatchLabel(0);
                    return;
                }

                var lowerSearch = string.IsNullOrEmpty(searchText) ? "" : searchText.ToLowerInvariant();
                BuildJsonTreeWithSearch(json, container, 0, Path.GetFileNameWithoutExtension(jsonPath), lowerSearch);

                UpdateMatchLabel(mSearchMatchCount);

                // 滚动到第一个匹配项
                if (mFirstMatchElement != null && !string.IsNullOrEmpty(searchText))
                {
                    container.schedule.Execute(() =>
                    {
                        mFirstMatchElement.schedule.Execute(() =>
                        {
                            container.ScrollTo(mFirstMatchElement);
                        });
                    }).ExecuteLater(50);
                }
            }
            catch (Exception ex)
            {
                AddTreeError(container, $"加载失败: {ex.Message}");
                UpdateMatchLabel(0);
            }
        }

        private void UpdateMatchLabel(int count)
        {
            if (mDataPreviewMatchLabel == null) return;

            if (string.IsNullOrEmpty(mDataPreviewSearchText))
            {
                mDataPreviewMatchLabel.style.display = DisplayStyle.None;
            }
            else
            {
                mDataPreviewMatchLabel.style.display = DisplayStyle.Flex;
                mDataPreviewMatchLabel.text = count > 0 ? $"找到 {count} 处匹配" : "无匹配";
                mDataPreviewMatchLabel.style.color = new StyleColor(count > 0 ? Design.BrandSuccess : Design.BrandWarning);
            }
        }

        private void AddTreeError(VisualElement container, string message)
        {
            var label = new Label(message);
            label.style.color = new StyleColor(Design.BrandDanger);
            label.style.paddingLeft = 8;
            label.style.paddingTop = 4;
            container.Add(label);
        }

        private void BuildJsonTree(JSONNode node, VisualElement parent, int depth, string key = null) =>
            BuildJsonTreeWithSearch(node, parent, depth, key, "");

        private bool BuildJsonTreeWithSearch(JSONNode node, VisualElement parent, int depth, string key, string searchText)
        {
            var indent = depth * 16;
            var hasMatch = false;

            if (node.IsArray)
            {
                var foldout = new Foldout { text = string.IsNullOrEmpty(key) ? $"Array [{node.Count}]" : $"{key} [{node.Count}]", value = depth < 1 };
                foldout.style.marginLeft = indent;

                // 检查 key 是否匹配
                if (!string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText))
                {
                    hasMatch = true;
                    HighlightFoldout(foldout);
                }

                parent.Add(foldout);

                int idx = 0;
                foreach (var item in node.Children)
                {
                    var childMatch = BuildJsonTreeWithSearch(item, foldout, depth + 1, $"[{idx}]", searchText);
                    if (childMatch)
                    {
                        hasMatch = true;
                        foldout.value = true; // 展开包含匹配项的节点
                    }

                    if (++idx >= 50)
                    {
                        var more = new Label($"... 还有 {node.Count - 50} 项");
                        more.style.color = new StyleColor(Design.TextTertiary);
                        more.style.marginLeft = (depth + 1) * 16;
                        foldout.Add(more);
                        break;
                    }
                }
            }
            else if (node.IsObject)
            {
                var foldout = new Foldout { text = string.IsNullOrEmpty(key) ? "Object" : key, value = depth < 2 };
                foldout.style.marginLeft = indent;

                // 检查 key 是否匹配
                if (!string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText))
                {
                    hasMatch = true;
                    HighlightFoldout(foldout);
                }

                parent.Add(foldout);

                foreach (var kvp in node.AsObject)
                {
                    var childMatch = BuildJsonTreeWithSearch(kvp.Value, foldout, depth + 1, kvp.Key, searchText);
                    if (childMatch)
                    {
                        hasMatch = true;
                        foldout.value = true; // 展开包含匹配项的节点
                    }
                }
            }
            else
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginLeft = indent;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                parent.Add(row);

                var keyMatched = !string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText);
                var valueMatched = !string.IsNullOrEmpty(searchText) && node.Value.ToLowerInvariant().Contains(searchText);

                if (keyMatched || valueMatched)
                {
                    hasMatch = true;
                    mSearchMatchCount++;

                    // 高亮整行背景
                    row.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0f, 0.15f));
                    row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 2;
                    row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 2;
                    row.style.paddingLeft = 4;
                    row.style.marginLeft = indent - 4;

                    if (mFirstMatchElement == null)
                    {
                        mFirstMatchElement = row;
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    var keyLabel = new Label($"{key}: ");
                    keyLabel.style.color = keyMatched
                        ? new StyleColor(new Color(1f, 0.6f, 0f)) // 高亮橙色
                        : new StyleColor(Design.BrandPrimary);
                    if (keyMatched) keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    row.Add(keyLabel);
                }

                var valueLabel = new Label(node.Value);
                if (valueMatched)
                {
                    valueLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0f)); // 高亮橙色
                    valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    valueLabel.style.color = GetValueColor(node);
                }
                row.Add(valueLabel);
            }

            return hasMatch;
        }

        private void HighlightFoldout(Foldout foldout)
        {
            mSearchMatchCount++;
            foldout.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0f, 0.1f));
            foldout.style.borderTopLeftRadius = foldout.style.borderTopRightRadius = 2;
            foldout.style.borderBottomLeftRadius = foldout.style.borderBottomRightRadius = 2;

            if (mFirstMatchElement == null)
            {
                mFirstMatchElement = foldout;
            }
        }

        private StyleColor GetValueColor(JSONNode node)
        {
            if (node.IsNumber) return new StyleColor(Design.BrandSuccess);
            if (node.IsBoolean) return new StyleColor(Design.BrandDanger);
            if (node.IsNull) return new StyleColor(Design.TextTertiary);
            return new StyleColor(Design.BrandWarning);
        }

        #endregion
    }
}
#endif
