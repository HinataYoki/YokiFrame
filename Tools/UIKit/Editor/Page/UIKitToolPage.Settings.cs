#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;
using UIButton = UnityEngine.UIElements.Button;
using UIToggle = UnityEngine.UIElements.Toggle;
using UISlider = UnityEngine.UIElements.Slider;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页中的设置子页。
    /// 用于编辑 UIKit Canvas、CanvasScaler 和 GraphicRaycaster 的默认配置。
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 设置

        private VisualElement mSettingsContent;
        private UIKitSettings mSettings;

        #endregion

        #region 设置 UI

        private void BuildSettingsUI(VisualElement container)
        {
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-ui-settings-toolbar");
            container.Add(toolbar);

            var titleLabel = new Label("UIKit Canvas 配置");
            titleLabel.AddToClassList("yoki-ui-settings-toolbar__title");
            toolbar.Add(titleLabel);

            var resetBtn = new UIButton(OnResetSettings) { text = "重置默认" };
            resetBtn.AddToClassList("yoki-ui-button");
            toolbar.Add(resetBtn);

            var saveBtn = new UIButton(OnSaveSettings) { text = "保存" };
            saveBtn.AddToClassList("yoki-ui-button");
            saveBtn.AddToClassList("yoki-ui-button--with-margin");
            toolbar.Add(saveBtn);

            mSettingsContent = new ScrollView();
            mSettingsContent.AddToClassList("yoki-ui-settings-content");
            container.Add(mSettingsContent);

            LoadSettings();
            BuildSettingsContent();
        }

        private void LoadSettings()
        {
            mSettings = UIKitSettings.Instance;
        }

        private void BuildSettingsContent()
        {
            mSettingsContent.Clear();

            var contentWrapper = new VisualElement();
            contentWrapper.AddToClassList("yoki-ui-settings-wrapper");
            mSettingsContent.Add(contentWrapper);

            var metricStrip = CreateKitMetricStrip();
            var (renderModeCard, renderModeValue) = CreateKitMetricCard("渲染模式", mSettings.RenderMode.ToString(), "Canvas 当前渲染策略");
            var (scaleModeCard, scaleModeValue) = CreateKitMetricCard("缩放模式", mSettings.ScaleMode.ToString(), "CanvasScaler 当前缩放策略");
            var (sortOrderCard, sortOrderValue) = CreateKitMetricCard("排序顺序", mSettings.SortOrder.ToString(), "默认 Sorting Order");
            var (blockingCard, blockingValue) = CreateKitMetricCard("拦截类型", mSettings.BlockingObjects.ToString(), "Raycaster 拦截对象类型");
            metricStrip.Add(renderModeCard);
            metricStrip.Add(scaleModeCard);
            metricStrip.Add(sortOrderCard);
            metricStrip.Add(blockingCard);
            contentWrapper.Add(metricStrip);

            var (canvasPanel, canvasBody) = CreateKitSectionPanel("Canvas 配置", "管理 UIKit Canvas 的渲染模式、排序与显示器参数。", KitIcons.SETTINGS);
            contentWrapper.Add(canvasPanel);

            canvasBody.Add(CreateEnumField("渲染模式", mSettings.RenderMode, v => mSettings.RenderMode = (RenderMode)v));
            canvasBody.Add(CreateIntField("排序顺序", mSettings.SortOrder, v => mSettings.SortOrder = v));
            canvasBody.Add(CreateIntField("目标显示器", mSettings.TargetDisplay, v => mSettings.TargetDisplay = v));
            canvasBody.Add(CreateModernToggle("像素完美", mSettings.PixelPerfect, v => mSettings.PixelPerfect = v));

            var (scalerPanel, scalerBody) = CreateKitSectionPanel("CanvasScaler 配置", "统一设置参考分辨率、屏幕匹配与 DPI 参数。", KitIcons.SETTINGS);
            contentWrapper.Add(scalerPanel);

            scalerBody.Add(CreateEnumField("缩放模式", mSettings.ScaleMode, v => mSettings.ScaleMode = (CanvasScaler.ScaleMode)v));
            scalerBody.Add(CreateVector2Field("参考分辨率", mSettings.ReferenceResolution, v => mSettings.ReferenceResolution = v));
            scalerBody.Add(CreateEnumField("屏幕匹配模式", mSettings.ScreenMatchMode, v => mSettings.ScreenMatchMode = (CanvasScaler.ScreenMatchMode)v));
            scalerBody.Add(CreateSliderField("宽高匹配权重", mSettings.MatchWidthOrHeight, 0f, 1f, v => mSettings.MatchWidthOrHeight = v));
            scalerBody.Add(CreateFloatField("参考像素每单位", mSettings.ReferencePixelsPerUnit, v => mSettings.ReferencePixelsPerUnit = v));
            scalerBody.Add(CreateEnumField("物理单位", mSettings.PhysicalUnit, v => mSettings.PhysicalUnit = (CanvasScaler.Unit)v));
            scalerBody.Add(CreateFloatField("回退屏幕 DPI", mSettings.FallbackScreenDPI, v => mSettings.FallbackScreenDPI = v));
            scalerBody.Add(CreateFloatField("默认精灵 DPI", mSettings.DefaultSpriteDPI, v => mSettings.DefaultSpriteDPI = v));
            scalerBody.Add(CreateFloatField("动态像素每单位", mSettings.DynamicPixelsPerUnit, v => mSettings.DynamicPixelsPerUnit = v));

            var (raycasterPanel, raycasterBody) = CreateKitSectionPanel("GraphicRaycaster 配置", "配置 UI 射线拦截路径与反向图形忽略策略。", KitIcons.SETTINGS);
            contentWrapper.Add(raycasterPanel);

            raycasterBody.Add(CreateModernToggle("忽略反向图形", mSettings.IgnoreReversedGraphics, v => mSettings.IgnoreReversedGraphics = v));
            raycasterBody.Add(CreateEnumField("阻挡对象类型", mSettings.BlockingObjects, v => mSettings.BlockingObjects = (GraphicRaycaster.BlockingObjects)v));
            raycasterBody.Add(CreateLayerMaskField("阻挡层级", mSettings.BlockingMask, v => mSettings.BlockingMask = v));

            renderModeValue.text = mSettings.RenderMode.ToString();
            scaleModeValue.text = mSettings.ScaleMode.ToString();
            sortOrderValue.text = mSettings.SortOrder.ToString();
            blockingValue.text = mSettings.BlockingObjects.ToString();
        }

        #endregion

        #region 配置字段创建

        private VisualElement CreateEnumField(string label, Enum value, Action<Enum> onChanged)
        {
            var row = CreateFieldRow(label);

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-settings-field-row__field");

#if UNITY_2022_1_OR_NEWER
            var field = new UnityEngine.UIElements.EnumField(value);
#else
            var field = new UnityEditor.UIElements.EnumField(value);
#endif
            field.AddToClassList("yoki-ui-field--grow");
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));

            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateIntField(string label, int value, Action<int> onChanged)
        {
            var row = CreateFieldRow(label);

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-field--overflow-hidden");

#if UNITY_2022_1_OR_NEWER
            var field = new UnityEngine.UIElements.IntegerField { value = value };
#else
            var field = new UnityEditor.UIElements.IntegerField { value = value };
#endif
            field.AddToClassList("yoki-ui-field--grow");
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));

            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateFloatField(string label, float value, Action<float> onChanged)
        {
            var row = CreateFieldRow(label);

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-field--overflow-hidden");

#if UNITY_2022_1_OR_NEWER
            var field = new UnityEngine.UIElements.FloatField { value = value };
#else
            var field = new UnityEditor.UIElements.FloatField { value = value };
#endif
            field.AddToClassList("yoki-ui-field--grow");
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));

            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateVector2Field(string label, Vector2 value, Action<Vector2> onChanged)
        {
            return CreateFigmaVector2Input(label, value, onChanged);
        }

        private VisualElement CreateToggleField(string label, bool value, Action<bool> onChanged)
        {
            var row = CreateFieldRow(label);
            var field = new UIToggle { value = value };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(field);
            return row;
        }

        private VisualElement CreateSliderField(string label, float value, float min, float max, Action<float> onChanged)
        {
            var row = CreateFieldRow(label);

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-settings-field-row__field");

            var field = new UISlider(min, max) { value = value };
            field.AddToClassList("yoki-ui-field--grow");
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            fieldContainer.Add(field);

            var valueLabel = new Label(value.ToString("F2"));
            valueLabel.AddToClassList("yoki-ui-settings-field-row__value-label");
            field.RegisterValueChangedCallback(evt => valueLabel.text = evt.newValue.ToString("F2"));
            fieldContainer.Add(valueLabel);

            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateLayerMaskField(string label, LayerMask value, Action<LayerMask> onChanged)
        {
            var row = CreateFieldRow(label);

            var layerNames = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 32; i++)
            {
                var layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layerNames.Add(layerName);
                }
            }

            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-field--overflow-hidden");

            var field = new UnityEditor.UIElements.MaskField(layerNames, value) { label = string.Empty };
            field.AddToClassList("yoki-ui-field--grow");
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke((LayerMask)evt.newValue));

            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateFieldRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-ui-settings-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("yoki-ui-settings-field-row__label");
            row.Add(labelElement);

            return row;
        }

        #endregion

        #region 设置操作

        private void OnResetSettings()
        {
            if (!EditorUtility.DisplayDialog("重置确认", "确定要重置为默认配置吗？", "确定", "取消"))
            {
                return;
            }

            mSettings.ResetToDefault();
            BuildSettingsContent();
            EditorUtility.SetDirty(mSettings);
        }

        private void OnSaveSettings()
        {
            EditorUtility.SetDirty(mSettings);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("保存成功", "UIKit 配置已保存。", "确定");
        }

        #endregion
    }
}
#endif
