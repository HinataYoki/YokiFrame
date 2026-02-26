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
    /// UIKitToolPage - 设置功能
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
            // 工具栏
            var toolbar = new VisualElement();
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

            // 内容区域
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

            // 添加容器包装，提供左右留白
            var contentWrapper = new VisualElement();
            contentWrapper.AddToClassList("yoki-ui-settings-wrapper");
            mSettingsContent.Add(contentWrapper);

            // Canvas 配置
            var (canvasCard, canvasBody) = CreateCard("Canvas 配置", KitIcons.SETTINGS);
            contentWrapper.Add(canvasCard);

            canvasBody.Add(CreateEnumField("渲染模式", mSettings.RenderMode, v => mSettings.RenderMode = (RenderMode)v));
            canvasBody.Add(CreateIntField("排序顺序", mSettings.SortOrder, v => mSettings.SortOrder = v));
            canvasBody.Add(CreateIntField("目标显示器", mSettings.TargetDisplay, v => mSettings.TargetDisplay = v));
            canvasBody.Add(CreateModernToggle("像素完美", mSettings.PixelPerfect, v => mSettings.PixelPerfect = v));

            // CanvasScaler 配置
            var (scalerCard, scalerBody) = CreateCard("CanvasScaler 配置", KitIcons.SETTINGS);
            contentWrapper.Add(scalerCard);

            scalerBody.Add(CreateEnumField("缩放模式", mSettings.ScaleMode, v => mSettings.ScaleMode = (CanvasScaler.ScaleMode)v));
            scalerBody.Add(CreateVector2Field("参考分辨率", mSettings.ReferenceResolution, v => mSettings.ReferenceResolution = v));
            scalerBody.Add(CreateEnumField("屏幕匹配模式", mSettings.ScreenMatchMode, v => mSettings.ScreenMatchMode = (CanvasScaler.ScreenMatchMode)v));
            scalerBody.Add(CreateSliderField("宽高匹配权重", mSettings.MatchWidthOrHeight, 0f, 1f, v => mSettings.MatchWidthOrHeight = v));
            scalerBody.Add(CreateFloatField("参考像素每单位", mSettings.ReferencePixelsPerUnit, v => mSettings.ReferencePixelsPerUnit = v));
            scalerBody.Add(CreateEnumField("物理单位", mSettings.PhysicalUnit, v => mSettings.PhysicalUnit = (CanvasScaler.Unit)v));
            scalerBody.Add(CreateFloatField("回退屏幕 DPI", mSettings.FallbackScreenDPI, v => mSettings.FallbackScreenDPI = v));
            scalerBody.Add(CreateFloatField("默认精灵 DPI", mSettings.DefaultSpriteDPI, v => mSettings.DefaultSpriteDPI = v));
            scalerBody.Add(CreateFloatField("动态像素每单位", mSettings.DynamicPixelsPerUnit, v => mSettings.DynamicPixelsPerUnit = v));

            // GraphicRaycaster 配置
            var (raycasterCard, raycasterBody) = CreateCard("GraphicRaycaster 配置", KitIcons.SETTINGS);
            contentWrapper.Add(raycasterCard);

            raycasterBody.Add(CreateModernToggle("忽略反向图形", mSettings.IgnoreReversedGraphics, v => mSettings.IgnoreReversedGraphics = v));
            raycasterBody.Add(CreateEnumField("阻挡对象类型", mSettings.BlockingObjects, v => mSettings.BlockingObjects = (GraphicRaycaster.BlockingObjects)v));
            raycasterBody.Add(CreateLayerMaskField("阻挡层级", mSettings.BlockingMask, v => mSettings.BlockingMask = v));
        }

        #endregion

        #region 配置字段创建

        private VisualElement CreateEnumField(string label, Enum value, Action<Enum> onChanged)
        {
            var row = CreateFieldRow(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("yoki-ui-settings-field-row__field");
            
#if UNITY_2023_1_OR_NEWER
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
            
#if UNITY_2023_1_OR_NEWER
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
            
#if UNITY_2023_1_OR_NEWER
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
            // 直接使用 Figma 风格单行紧凑型 Vector2 输入组件
            // 该组件自带主标签，无需 CreateFieldRow
            return CreateFigmaVector2Input(
                label: label,
                value: value,
                onChanged: onChanged
            );
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
                return;

            mSettings.ResetToDefault();
            BuildSettingsContent();
            EditorUtility.SetDirty(mSettings);
        }

        private void OnSaveSettings()
        {
            EditorUtility.SetDirty(mSettings);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("保存成功", "UIKit 配置已保存", "确定");
        }

        #endregion
    }
}
#endif
