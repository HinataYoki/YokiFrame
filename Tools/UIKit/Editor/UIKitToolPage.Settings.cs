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
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = Spacing.SM;
            toolbar.style.paddingRight = Spacing.SM;
            toolbar.style.paddingTop = Spacing.XS;
            toolbar.style.paddingBottom = Spacing.XS;
            toolbar.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.Add(toolbar);

            var titleLabel = new Label("UIKit Canvas 配置");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.flexGrow = 1;
            toolbar.Add(titleLabel);

            var resetBtn = new UIButton(OnResetSettings) { text = "重置默认" };
            resetBtn.style.height = 24;
            toolbar.Add(resetBtn);

            var saveBtn = new UIButton(OnSaveSettings) { text = "保存" };
            saveBtn.style.height = 24;
            saveBtn.style.marginLeft = Spacing.XS;
            toolbar.Add(saveBtn);

            // 内容区域
            mSettingsContent = new ScrollView();
            mSettingsContent.style.flexGrow = 1;
            mSettingsContent.style.paddingLeft = Spacing.MD;
            mSettingsContent.style.paddingRight = Spacing.MD;
            mSettingsContent.style.paddingTop = Spacing.MD;
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

            // Canvas 配置
            var (canvasCard, canvasBody) = CreateCard("Canvas 配置", KitIcons.SETTINGS);
            mSettingsContent.Add(canvasCard);

            canvasBody.Add(CreateEnumField("渲染模式", mSettings.RenderMode, v => mSettings.RenderMode = (RenderMode)v));
            canvasBody.Add(CreateIntField("排序顺序", mSettings.SortOrder, v => mSettings.SortOrder = v));
            canvasBody.Add(CreateIntField("目标显示器", mSettings.TargetDisplay, v => mSettings.TargetDisplay = v));
            canvasBody.Add(CreateToggleField("像素完美", mSettings.PixelPerfect, v => mSettings.PixelPerfect = v));

            // CanvasScaler 配置
            var (scalerCard, scalerBody) = CreateCard("CanvasScaler 配置", KitIcons.SETTINGS);
            mSettingsContent.Add(scalerCard);

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
            mSettingsContent.Add(raycasterCard);

            raycasterBody.Add(CreateToggleField("忽略反向图形", mSettings.IgnoreReversedGraphics, v => mSettings.IgnoreReversedGraphics = v));
            raycasterBody.Add(CreateEnumField("阻挡对象类型", mSettings.BlockingObjects, v => mSettings.BlockingObjects = (GraphicRaycaster.BlockingObjects)v));
            raycasterBody.Add(CreateLayerMaskField("阻挡层级", mSettings.BlockingMask, v => mSettings.BlockingMask = v));
        }

        #endregion

        #region 配置字段创建

        private VisualElement CreateEnumField(string label, Enum value, Action<Enum> onChanged)
        {
            var row = CreateFieldRow(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new EnumField(value);
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            
            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateIntField(string label, int value, Action<int> onChanged)
        {
            var row = CreateFieldRow(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new IntegerField { value = value };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            
            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateFloatField(string label, float value, Action<float> onChanged)
        {
            var row = CreateFieldRow(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new FloatField { value = value };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            
            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateVector2Field(string label, Vector2 value, Action<Vector2> onChanged)
        {
            var row = CreateFieldRow(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new Vector2Field { value = value, label = string.Empty };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            
            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
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
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new UISlider(min, max) { value = value };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            fieldContainer.Add(field);
            
            var valueLabel = new Label(value.ToString("F2"));
            valueLabel.style.width = 40;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
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
            fieldContainer.style.flexGrow = 1;
            fieldContainer.style.overflow = Overflow.Hidden;
            
            var field = new UnityEditor.UIElements.MaskField(layerNames, value) { label = string.Empty };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke((LayerMask)evt.newValue));
            
            fieldContainer.Add(field);
            row.Add(fieldContainer);
            return row;
        }

        private VisualElement CreateFieldRow(string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = Spacing.SM;

            var labelElement = new Label(label);
            labelElement.style.width = 150;
            labelElement.style.minWidth = 150;
            labelElement.style.flexShrink = 0;
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
