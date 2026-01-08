#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 工具页面 - 静态代码扫描视图
    /// 设计目标：可视化数据流向与逻辑健康度
    /// 布局模式：三栏流式布局（发送 -> 事件 -> 接收）
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 健康状态枚举

        private enum HealthStatus
        {
            Healthy,    // 完美闭环：有发有收
            Orphan,     // 孤儿事件：有发送者，无接收者
            LeakRisk,   // 潜在泄露：Register > Unregister
            NoSender    // 无发送源：有接收者，无发送者
        }

        #endregion

        #region 代码扫描私有字段

        private TextField mScanFolderField;
        private ScrollView mScanResultsScrollView;
        private Label mScanSummaryLabel;
        private string mScanFolder = "Assets/Scripts";
        private readonly List<EventCodeScanner.ScanResult> mScanResults = new(256);

        #endregion

        #region 事件流数据结构

        /// <summary>
        /// 事件流数据
        /// </summary>
        private class EventFlowData
        {
            public string EventType;
            public string EventKey;
            public string ParamType;
            public List<EventCodeScanner.ScanResult> Senders = new(4);
            public List<EventCodeScanner.ScanResult> Receivers = new(4);
            public List<EventCodeScanner.ScanResult> Unregisters = new(4);

            public HealthStatus GetHealthStatus()
            {
                var hasSenders = Senders.Count > 0;
                var hasReceivers = Receivers.Count > 0;

                if (!hasSenders && hasReceivers)
                    return HealthStatus.NoSender;
                if (hasSenders && !hasReceivers)
                    return HealthStatus.Orphan;
                if (Receivers.Count > Unregisters.Count && Unregisters.Count > 0)
                    return HealthStatus.LeakRisk;
                return HealthStatus.Healthy;
            }
        }

        #endregion

        #region 创建代码扫描视图

        private VisualElement CreateCodeScanView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;

            // 工具栏
            var toolbar = CreateScanToolbar();
            container.Add(toolbar);

            // 结果滚动视图
            mScanResultsScrollView = new ScrollView();
            mScanResultsScrollView.style.flexGrow = 1;
            mScanResultsScrollView.style.paddingLeft = 16;
            mScanResultsScrollView.style.paddingRight = 16;
            mScanResultsScrollView.style.paddingTop = 16;
            container.Add(mScanResultsScrollView);

            return container;
        }

        private VisualElement CreateScanToolbar()
        {
            var toolbar = CreateToolbar();

            var folderLabel = new Label("扫描目录:");
            folderLabel.AddToClassList("toolbar-label");
            toolbar.Add(folderLabel);

            mScanFolderField = new TextField();
            mScanFolderField.value = mScanFolder;
            mScanFolderField.style.width = 200;
            mScanFolderField.RegisterValueChangedCallback(evt => mScanFolder = evt.newValue);
            toolbar.Add(mScanFolderField);

            var browseBtn = CreateToolbarButton("...", BrowseScanFolder);
            toolbar.Add(browseBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            mScanSummaryLabel = new Label();
            mScanSummaryLabel.AddToClassList("toolbar-label");
            toolbar.Add(mScanSummaryLabel);

            var scanBtn = CreateToolbarButton("🔍 扫描", PerformScan);
            toolbar.Add(scanBtn);

            return toolbar;
        }

        private void BrowseScanFolder()
        {
            var folder = EditorUtility.OpenFolderPanel("选择扫描目录", mScanFolder, "");
            if (!string.IsNullOrEmpty(folder))
            {
                var idx = folder.IndexOf("Assets", StringComparison.Ordinal);
                mScanFolder = idx >= 0 ? folder[idx..] : folder;
                mScanFolderField.value = mScanFolder;
            }
        }

        private void PerformScan()
        {
            mScanResults.Clear();
            mScanResults.AddRange(EventCodeScanner.ScanFolder(mScanFolder, true));
            RefreshScanResults();
        }

        #endregion

        #region 刷新扫描结果

        private void RefreshScanResults()
        {
            mScanResultsScrollView.Clear();

            if (mScanResults.Count == 0)
            {
                mScanSummaryLabel.text = "无结果";
                mScanResultsScrollView.Add(CreateEmptyState("点击「扫描」按钮开始扫描代码"));
                return;
            }

            // 统计
            var enumCount = 0;
            var typeCount = 0;
            var stringCount = 0;
            foreach (var result in mScanResults)
            {
                switch (result.EventType)
                {
                    case "Enum": enumCount++; break;
                    case "Type": typeCount++; break;
                    case "String": stringCount++; break;
                }
            }
            mScanSummaryLabel.text = $"共 {mScanResults.Count} 处 (Enum:{enumCount} Type:{typeCount} String:{stringCount})";

            // 构建事件流数据
            var eventFlows = BuildEventFlows();

            // 按类型分组渲染
            RenderEventFlowsByType(eventFlows);
        }

        /// <summary>
        /// 构建事件流数据结构
        /// </summary>
        private Dictionary<string, Dictionary<string, EventFlowData>> BuildEventFlows()
        {
            var flows = new Dictionary<string, Dictionary<string, EventFlowData>>();

            foreach (var result in mScanResults)
            {
                var eventType = result.EventType;
                var eventKey = result.EventKey;

                if (!flows.TryGetValue(eventType, out var typeDict))
                {
                    typeDict = new Dictionary<string, EventFlowData>();
                    flows[eventType] = typeDict;
                }

                if (!typeDict.TryGetValue(eventKey, out var flowData))
                {
                    flowData = new EventFlowData
                    {
                        EventType = eventType,
                        EventKey = eventKey,
                        ParamType = result.ParamType
                    };
                    typeDict[eventKey] = flowData;
                }

                switch (result.CallType)
                {
                    case "Send":
                        flowData.Senders.Add(result);
                        break;
                    case "Register":
                        flowData.Receivers.Add(result);
                        break;
                    case "UnRegister":
                        flowData.Unregisters.Add(result);
                        break;
                }
            }

            return flows;
        }

        #endregion
    }
}
#endif
