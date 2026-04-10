#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 旧版查看窗口的代码扫描视图。
    /// </summary>
    public partial class EventKitViewerWindow
    {
        #region 扫描视图构建

        /// <summary>
        /// 构建代码扫描页。
        /// </summary>
        private void BuildCodeScanView()
        {
            mCodeScanView = new VisualElement();
            mCodeScanView.style.flexGrow = 1;
            mCodeScanView.style.flexDirection = FlexDirection.Column;

            BuildScanToolbar();
            BuildScanResults();
        }

        /// <summary>
        /// 构建扫描工具栏。
        /// </summary>
        private void BuildScanToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            mCodeScanView.Add(toolbar);

            var folderLabel = new Label("扫描目录:");
            folderLabel.style.marginRight = 4;
            folderLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(folderLabel);

            mScanFolderField = new TextField { value = mScanFolder };
            mScanFolderField.style.width = 200;
            mScanFolderField.style.marginRight = 4;
            mScanFolderField.RegisterValueChangedCallback(evt => mScanFolder = evt.newValue);
            toolbar.Add(mScanFolderField);

            var browseBtn = new Button(() =>
            {
                var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    int idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                    mScanFolder = idx >= 0 ? folder[idx..] : folder;
                    mScanFolderField.value = mScanFolder;
                }
            })
            {
                text = "..."
            };
            browseBtn.style.width = 25;
            browseBtn.style.height = 22;
            browseBtn.style.marginRight = 12;
            toolbar.Add(browseBtn);

            var typeLabel = new Label("类型:");
            typeLabel.style.marginRight = 4;
            typeLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(typeLabel);

            mScanTypeFilter = new DropdownField
            {
                choices = new List<string> { "All", "Enum", "Type", "String" },
                value = mScanFilterType
            };
            mScanTypeFilter.style.width = 70;
            mScanTypeFilter.style.marginRight = 12;
            mScanTypeFilter.RegisterValueChangedCallback(evt =>
            {
                mScanFilterType = evt.newValue;
                RefreshScanResultsView();
            });
            toolbar.Add(mScanTypeFilter);

            var callLabel = new Label("调用:");
            callLabel.style.marginRight = 4;
            callLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(callLabel);

            mScanCallFilter = new DropdownField
            {
                choices = new List<string> { "All", "Register", "Send" },
                value = mScanFilterCall
            };
            mScanCallFilter.style.width = 80;
            mScanCallFilter.style.marginRight = 12;
            mScanCallFilter.RegisterValueChangedCallback(evt =>
            {
                mScanFilterCall = evt.newValue;
                RefreshScanResultsView();
            });
            toolbar.Add(mScanCallFilter);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var scanBtn = new Button(ExecuteScan) { text = "扫描" };
            scanBtn.style.height = 22;
            scanBtn.style.paddingLeft = 16;
            scanBtn.style.paddingRight = 16;
            scanBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.45f, 0.65f));
            scanBtn.style.color = new StyleColor(Color.white);
            toolbar.Add(scanBtn);
        }

        /// <summary>
        /// 构建扫描结果列表。
        /// </summary>
        private void BuildScanResults()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            mCodeScanView.Add(container);

            mScanCountLabel = new Label("(0 / 0)");
            mScanCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            var header = CreateMonitorHeaderRow("扫描结果", EditorTools.KitIcons.TARGET, mScanCountLabel);
            header.style.marginBottom = 8;
            container.Add(header);

            mScanResultsListView = new ListView();
            mScanResultsListView.fixedItemHeight = 32;
            mScanResultsListView.makeItem = MakeScanResultItem;
            mScanResultsListView.bindItem = BindScanResultItem;
            mScanResultsListView.style.flexGrow = 1;
            mScanResultsListView.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mScanResultsListView.style.borderTopLeftRadius = 4;
            mScanResultsListView.style.borderTopRightRadius = 4;
            mScanResultsListView.style.borderBottomLeftRadius = 4;
            mScanResultsListView.style.borderBottomRightRadius = 4;
            container.Add(mScanResultsListView);
        }

        #endregion

        #region 扫描结果列表项

        /// <summary>
        /// 创建扫描结果列表项。
        /// </summary>
        private VisualElement MakeScanResultItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 32;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var typeBadge = new Label();
            typeBadge.name = "type";
            typeBadge.style.width = 45;
            typeBadge.style.height = 18;
            typeBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            typeBadge.style.borderTopLeftRadius = 9;
            typeBadge.style.borderTopRightRadius = 9;
            typeBadge.style.borderBottomLeftRadius = 9;
            typeBadge.style.borderBottomRightRadius = 9;
            typeBadge.style.marginRight = 4;
            typeBadge.style.fontSize = 10;
            item.Add(typeBadge);

            var callBadge = new Label();
            callBadge.name = "call";
            callBadge.style.width = 55;
            callBadge.style.height = 18;
            callBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            callBadge.style.borderTopLeftRadius = 9;
            callBadge.style.borderTopRightRadius = 9;
            callBadge.style.borderBottomLeftRadius = 9;
            callBadge.style.borderBottomRightRadius = 9;
            callBadge.style.marginRight = 8;
            callBadge.style.fontSize = 10;
            item.Add(callBadge);

            var keyLabel = new Label();
            keyLabel.name = "key";
            keyLabel.style.width = 150;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.overflow = Overflow.Hidden;
            keyLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(keyLabel);

            var pathLabel = new Label();
            pathLabel.name = "path";
            pathLabel.style.flexGrow = 1;
            pathLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            pathLabel.style.fontSize = 11;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(pathLabel);

            var jumpBtn = new Button { text = "跳转" };
            jumpBtn.name = "jump";
            jumpBtn.style.height = 20;
            jumpBtn.style.paddingLeft = 8;
            jumpBtn.style.paddingRight = 8;
            item.Add(jumpBtn);

            return item;
        }

        /// <summary>
        /// 绑定扫描结果列表项。
        /// </summary>
        private void BindScanResultItem(VisualElement element, int index)
        {
            if (mScanResultsListView?.itemsSource is not List<EventCodeScanner.ScanResult> filteredResults)
            {
                return;
            }

            if (index < 0 || index >= filteredResults.Count)
            {
                return;
            }

            var result = filteredResults[index];

            var typeBadge = element.Q<Label>("type");
            typeBadge.text = result.EventType;
            typeBadge.style.backgroundColor = new StyleColor(GetEventTypeColor(result.EventType));
            typeBadge.style.color = new StyleColor(Color.white);

            var callBadge = element.Q<Label>("call");
            callBadge.text = result.CallType;
            callBadge.style.backgroundColor = new StyleColor(
                result.CallType == "Register" ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.5f, 0.3f));
            callBadge.style.color = new StyleColor(Color.white);

            element.Q<Label>("key").text = result.EventKey;

            string shortPath = result.FilePath.Length > 40 ? "..." + result.FilePath[^37..] : result.FilePath;
            element.Q<Label>("path").text = $"{shortPath}:{result.LineNumber}";

            var jumpBtn = element.Q<Button>("jump");
            jumpBtn.clickable = new Clickable(() => OpenFileAtLine(result.FilePath, result.LineNumber));
        }

        #endregion

        #region 扫描逻辑

        /// <summary>
        /// 执行代码扫描。
        /// </summary>
        private void ExecuteScan()
        {
            mScanResults.Clear();
            mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true));
            RefreshScanResultsView();
        }

        /// <summary>
        /// 刷新扫描结果视图。
        /// </summary>
        private void RefreshScanResultsView()
        {
            if (mScanResultsListView == null)
            {
                return;
            }

            var filteredResults = GetFilteredScanResults();
            mScanCountLabel.text = $"({filteredResults.Count} / {mScanResults.Count})";
            mScanResultsListView.itemsSource = filteredResults;
            mScanResultsListView.RefreshItems();
        }

        /// <summary>
        /// 获取当前筛选后的扫描结果。
        /// </summary>
        private List<EventCodeScanner.ScanResult> GetFilteredScanResults()
        {
            var filtered = new List<EventCodeScanner.ScanResult>(mScanResults.Count);
            foreach (var result in mScanResults)
            {
                if (PassScanFilter(result))
                {
                    filtered.Add(result);
                }
            }

            return filtered;
        }

        /// <summary>
        /// 判断扫描结果是否通过筛选条件。
        /// </summary>
        private bool PassScanFilter(EventCodeScanner.ScanResult result)
        {
            if (mScanFilterType != "All" && result.EventType != mScanFilterType)
            {
                return false;
            }

            if (mScanFilterCall != "All" && result.CallType != mScanFilterCall)
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}
#endif
