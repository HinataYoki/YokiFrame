#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 收集器编辑面板
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>当前展开的卡片索引（-1 表示无展开）</summary>
        private int mYooExpandedCardIndex = -1;

        /// <summary>
        /// 切换卡片展开/折叠状态
        /// </summary>
        private void ToggleYooCardExpand(int index)
        {
            mYooExpandedCardIndex = mYooExpandedCardIndex == index ? -1 : index;
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 创建收集器编辑面板
        /// </summary>
        private VisualElement CreateYooCollectorEditPanel(AssetBundleCollector collector)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 12;
            panel.style.paddingTop = 12;
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new StyleColor(new Color(0.35f, 0.35f, 0.38f));

            // 阻止点击事件冒泡到卡片
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // CollectPath 文件夹选择器
            panel.Add(CreateYooPathEditRow(collector));

            // CollectorType 下拉选择器
            panel.Add(CreateYooTypeEditRow(collector));

            // AddressRule 下拉选择器
            panel.Add(CreateYooAddressEditRow(collector));

            // PackRule 下拉选择器
            panel.Add(CreateYooPackEditRow(collector));

            // FilterRule 下拉选择器
            panel.Add(CreateYooFilterEditRow(collector));

            // AssetTags 文本输入框
            panel.Add(CreateYooTagsEditRow(collector));

            // UserData 文本输入框
            panel.Add(CreateYooUserDataEditRow(collector));

            return panel;
        }

        private VisualElement CreateYooPathEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("收集路径");
            var pathField = new TextField { value = collector.CollectPath };
            pathField.style.flexGrow = 1;
            pathField.style.flexShrink = 1;
            pathField.SetEnabled(false);
            row.Add(pathField);

            var browseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择收集路径", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        path = "Assets" + path[Application.dataPath.Length..];
                    collector.CollectPath = path;
                    pathField.value = path;
                    AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                    MarkYooDirty();
                    RefreshYooCollectorCanvas();
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            browseBtn.style.flexGrow = 0;
            browseBtn.style.flexShrink = 0;
            row.Add(browseBtn);
            return row;
        }

        private VisualElement CreateYooTypeEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("收集类型");
            var choices = new List<string> { "MainAssetCollector", "StaticAssetCollector", "DependAssetCollector" };
            var dropdown = new DropdownField(choices, (int)collector.CollectorType);
            dropdown.style.flexGrow = 1;
            dropdown.style.flexShrink = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                collector.CollectorType = (ECollectorType)choices.IndexOf(evt.newValue);
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
                RefreshYooCollectorCanvas();
            });
            row.Add(dropdown);
            return row;
        }

        private VisualElement CreateYooAddressEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("寻址规则");
            var choices = GetYooAddressRuleNames();
            var idx = choices.IndexOf(collector.AddressRuleName);
            var dropdown = new DropdownField(choices, idx >= 0 ? idx : 0);
            dropdown.style.flexGrow = 1;
            dropdown.style.flexShrink = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                collector.AddressRuleName = evt.newValue;
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
                RefreshYooCollectorCanvas();
            });
            row.Add(dropdown);
            return row;
        }

        private VisualElement CreateYooPackEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("打包规则");
            var choices = GetYooPackRuleNames();
            var idx = choices.IndexOf(collector.PackRuleName);
            var dropdown = new DropdownField(choices, idx >= 0 ? idx : 0);
            dropdown.style.flexGrow = 1;
            dropdown.style.flexShrink = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                collector.PackRuleName = evt.newValue;
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
                RefreshYooCollectorCanvas();
            });
            row.Add(dropdown);
            return row;
        }

        private VisualElement CreateYooFilterEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("过滤规则");
            var choices = GetYooFilterRuleNames();
            var idx = choices.IndexOf(collector.FilterRuleName);
            var dropdown = new DropdownField(choices, idx >= 0 ? idx : 0);
            dropdown.style.flexGrow = 1;
            dropdown.style.flexShrink = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                collector.FilterRuleName = evt.newValue;
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
                RefreshYooCollectorCanvas();
            });
            row.Add(dropdown);
            return row;
        }

        private VisualElement CreateYooTagsEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("资源标签");
            var field = new TextField { value = collector.AssetTags ?? "" };
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                collector.AssetTags = evt.newValue;
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
            });
            row.Add(field);
            return row;
        }

        private VisualElement CreateYooUserDataEditRow(AssetBundleCollector collector)
        {
            var row = CreateYooEditRow("用户数据");
            var field = new TextField { value = collector.UserData ?? "" };
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                collector.UserData = evt.newValue;
                AssetBundleCollectorSettingData.ModifyCollector(YooCurrentGroup, collector);
                MarkYooDirty();
            });
            row.Add(field);
            return row;
        }

        /// <summary>
        /// 创建编辑行容器
        /// </summary>
        private VisualElement CreateYooEditRow(string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var labelElement = new Label(label);
            labelElement.style.width = 70;
            labelElement.style.minWidth = 70;
            labelElement.style.fontSize = 12;
            labelElement.style.flexGrow = 0;
            labelElement.style.flexShrink = 0;
            labelElement.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            row.Add(labelElement);

            return row;
        }

        /// <summary>
        /// 获取寻址规则名称列表
        /// </summary>
        private static List<string> GetYooAddressRuleNames() => new()
        {
            "AddressDisable",
            "AddressByFileName",
            "AddressByFilePath",
            "AddressByFolderAndFileName",
            "AddressByGroupAndFileName"
        };

        /// <summary>
        /// 获取打包规则名称列表
        /// </summary>
        private static List<string> GetYooPackRuleNames() => new()
        {
            "PackDirectory",
            "PackTopDirectory",
            "PackSeparately",
            "PackCollector",
            "PackGroup",
            "PackRawFile",
            "PackShaderVariants"
        };

        /// <summary>
        /// 获取过滤规则名称列表
        /// </summary>
        private static List<string> GetYooFilterRuleNames() => new()
        {
            "CollectAll",
            "CollectScene",
            "CollectPrefab",
            "CollectSprite"
        };
    }
}
#endif
