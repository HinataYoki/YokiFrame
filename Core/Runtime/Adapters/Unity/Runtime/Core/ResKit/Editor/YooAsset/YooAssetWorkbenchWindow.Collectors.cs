#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetWorkbenchWindow
    {
        private void RefreshCollectorCanvas()
        {
            if (mCollectorList == null)
            {
                return;
            }

            mCollectorList.Clear();
            var group = CurrentGroup;
            if (group == null)
            {
                UpdateCountBadge(mCollectorCountBadge, 0);
                if (mGroupSummaryLabel != null)
                {
                    mGroupSummaryLabel.text = "选择一个分组查看收集器";
                }
                mCollectorList.Add(YokiFrameUIComponents.CreateEmptyState(KitIcons.DOCUMENT, "暂无收集器", "点击下方按钮添加收集器"));
                return;
            }

            var collectorCount = group.Collectors != null ? group.Collectors.Count : 0;
            UpdateCountBadge(mCollectorCountBadge, collectorCount);
            if (mGroupSummaryLabel != null)
            {
                mGroupSummaryLabel.text = string.IsNullOrEmpty(group.GroupDesc)
                    ? "分组：" + group.GroupName
                    : group.GroupName + " - " + group.GroupDesc;
            }

            if (group.Collectors == null || group.Collectors.Count == 0)
            {
                mCollectorList.Add(YokiFrameUIComponents.CreateEmptyState(KitIcons.DOCUMENT, "暂无收集器", "点击下方按钮添加收集器"));
                return;
            }

            for (var i = 0; i < group.Collectors.Count; i++)
            {
#if YOOASSET_3_0_OR_NEWER
                mCollectorList.Add(CreateCollectorCard(group.Collectors[i], i));
#else
                mCollectorList.Add(CreateCollectorCard(group.Collectors[i], i));
#endif
            }
        }

#if YOOASSET_3_0_OR_NEWER
        private VisualElement CreateCollectorCard(BundleCollector collector, int index)
#else
        private VisualElement CreateCollectorCard(AssetBundleCollector collector, int index)
#endif
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.AddToClassList("yoo-config-card");
            card.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerElevated);
            SetBorder(card, YokiFrameUIComponents.Colors.BorderLight);
            SetRadius(card, 8f);
            card.style.overflow = Overflow.Hidden;
            card.style.marginLeft = 0f;
            card.style.marginRight = 0f;
            card.style.marginTop = 0f;
            card.style.marginBottom = 8f;

            var body = new VisualElement();
            body.AddToClassList("card-body");
            body.style.paddingLeft = 12f;
            body.style.paddingRight = 12f;
            body.style.paddingTop = 10f;
            body.style.paddingBottom = 10f;
            card.Add(body);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            body.Add(header);

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.FOLDER) };
            icon.style.width = 16f;
            icon.style.height = 16f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 8f;
            icon.tintColor = YokiFrameUIComponents.Colors.BrandWarning;
            header.Add(icon);

            var path = new Label(string.IsNullOrEmpty(collector.CollectPath) ? "<未设置路径>" : collector.CollectPath);
            path.style.flexGrow = 1f;
            path.style.minWidth = 0f;
            path.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            path.style.unityFontStyleAndWeight = FontStyle.Bold;
            path.style.overflow = Overflow.Hidden;
            path.style.textOverflow = TextOverflow.Ellipsis;
            header.Add(path);

            var delete = new Button(() => RemoveCollector(collector)) { text = "×" };
            delete.tooltip = "删除收集器";
            delete.style.width = 24f;
            delete.style.height = 24f;
            delete.style.paddingLeft = 0f;
            delete.style.paddingRight = 0f;
            delete.style.flexShrink = 0f;
            header.Add(delete);

            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.flexWrap = Wrap.Wrap;
            badges.style.marginTop = 8f;
            body.Add(badges);

            badges.Add(CreateRuleBadge(ShortRuleName(collector.CollectorType.ToString()), CollectorTypeColor()));
            badges.Add(CreateRuleBadge(ShortRuleName(collector.AddressRuleName), AddressRuleColor()));
            badges.Add(CreateRuleBadge(ShortRuleName(collector.PackRuleName), PackRuleColor()));
            badges.Add(CreateRuleBadge(ShortRuleName(collector.FilterRuleName), FilterRuleColor()));

            if (!string.IsNullOrEmpty(collector.AssetTags))
            {
                var tags = collector.AssetTags.Split(';');
                for (var i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i].Trim();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        badges.Add(CreateRuleBadge(tag, TagColor()));
                    }
                }
            }

            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button)
                {
                    return;
                }

                mExpandedCollectorIndex = mExpandedCollectorIndex == index ? -1 : index;
                RefreshCollectorCanvas();
                evt.StopPropagation();
            });

            if (mExpandedCollectorIndex == index)
            {
                body.Add(CreateCollectorEditPanel(collector));
            }

            return card;
        }

#if YOOASSET_3_0_OR_NEWER
        private VisualElement CreateCollectorEditPanel(BundleCollector collector)
#else
        private VisualElement CreateCollectorEditPanel(AssetBundleCollector collector)
#endif
        {
            var panel = new VisualElement();
            panel.style.marginTop = 12f;
            panel.style.paddingTop = 12f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderTopColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            panel.Add(CreatePathRow(collector));
            panel.Add(CreateCollectorTypeRow(collector));
            panel.Add(CreateRuleDropdownRow("寻址规则", GetAddressRuleNames(), collector.AddressRuleName, value =>
            {
                collector.AddressRuleName = value;
                ModifyCollector(collector);
            }));
            panel.Add(CreateRuleDropdownRow("打包规则", GetPackRuleNames(), collector.PackRuleName, value =>
            {
                collector.PackRuleName = value;
                ModifyCollector(collector);
            }));
            panel.Add(CreateRuleDropdownRow("过滤规则", GetFilterRuleNames(), collector.FilterRuleName, value =>
            {
                collector.FilterRuleName = value;
                ModifyCollector(collector);
            }));
            panel.Add(CreateTextRow("资源标签", collector.AssetTags, value =>
            {
                collector.AssetTags = value;
                ModifyCollector(collector);
            }));
            panel.Add(CreateTextRow("用户数据", collector.UserData, value =>
            {
                collector.UserData = value;
                ModifyCollector(collector);
            }));
            return panel;
        }

#if YOOASSET_3_0_OR_NEWER
        private VisualElement CreatePathRow(BundleCollector collector)
#else
        private VisualElement CreatePathRow(AssetBundleCollector collector)
#endif
        {
            var field = new TextField { value = collector.CollectPath };
            field.SetEnabled(false);
            field.style.flexGrow = 1f;
            StyleField(field);
            var row = YokiFrameUIComponents.CreateCompactFormRow("收集路径", field);

            var browse = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择收集路径", "Assets", string.Empty);
                path = NormalizeAssetPath(path);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                collector.CollectPath = path;
                field.value = path;
                ModifyCollector(collector);
                RefreshCollectorCanvas();
            }) { text = "..." };
            browse.style.width = 32f;
            browse.style.marginLeft = 4f;
            row.Add(browse);
            return row;
        }

#if YOOASSET_3_0_OR_NEWER
        private VisualElement CreateCollectorTypeRow(BundleCollector collector)
#else
        private VisualElement CreateCollectorTypeRow(AssetBundleCollector collector)
#endif
        {
            var choices = new List<string>
            {
                "MainAssetCollector",
                "StaticAssetCollector",
                "DependAssetCollector"
            };
            var index = choices.IndexOf(collector.CollectorType.ToString());
            if (index < 0)
            {
                index = 0;
            }

            return CreateRuleDropdownRow("收集类型", choices, choices[index], value =>
            {
                collector.CollectorType = (ECollectorType)choices.IndexOf(value);
                ModifyCollector(collector);
                RefreshCollectorCanvas();
            });
        }

        private static VisualElement CreateRuleDropdownRow(string label, List<string> choices, string current, Action<string> onChanged)
        {
            var index = choices.IndexOf(current);
            if (index < 0)
            {
                index = 0;
            }

            var dropdown = new DropdownField(choices, index);
            StyleField(dropdown);
            dropdown.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return YokiFrameUIComponents.CreateCompactFormRow(label, dropdown);
        }

        private static VisualElement CreateTextRow(string label, string value, Action<string> onChanged)
        {
            var field = new TextField { value = value ?? string.Empty };
            StyleField(field);
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return YokiFrameUIComponents.CreateCompactFormRow(label, field);
        }

        private static void StyleField(VisualElement field)
        {
            field.style.minHeight = 28f;
            field.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerToolbar);
            field.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            SetBorder(field, YokiFrameUIComponents.Colors.BorderLight);
            SetRadius(field, 5f);
            field.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ApplyFieldTextColor(field);
            });
        }

        private static void ApplyFieldTextColor(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            if (element is Label)
            {
                element.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            }

            for (var i = 0; i < element.childCount; i++)
            {
                ApplyFieldTextColor(element[i]);
            }
        }
    }
}
#endif
#endif