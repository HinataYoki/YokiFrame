#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - UI 组件（Toggle、Callout、按钮样式）
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 胶囊 Toggle 组件

        /// <summary>
        /// 创建胶囊样式的 Toggle 开关
        /// </summary>
        private VisualElement CreateCapsuleToggle(string label, bool initialValue, Action<bool> onValueChanged)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var track = new VisualElement { name = "toggle-track" };
            track.style.width = 36;
            track.style.height = 20;
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 10;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 10;
            track.style.backgroundColor = new StyleColor(initialValue ? Design.BrandPrimary : new Color(0.3f, 0.3f, 0.32f));
            track.style.marginRight = 8;
            track.style.cursor = StyleKeyword.Initial;

            var thumb = new VisualElement { name = "toggle-thumb" };
            thumb.style.width = 16;
            thumb.style.height = 16;
            thumb.style.borderTopLeftRadius = thumb.style.borderTopRightRadius = 8;
            thumb.style.borderBottomLeftRadius = thumb.style.borderBottomRightRadius = 8;
            thumb.style.backgroundColor = new StyleColor(Color.white);
            thumb.style.position = Position.Absolute;
            thumb.style.top = 2;
            thumb.style.left = initialValue ? 18 : 2;
            track.Add(thumb);

            container.Add(track);

            if (!string.IsNullOrEmpty(label))
            {
                var labelEl = new Label(label);
                labelEl.style.color = new StyleColor(Design.TextSecondary);
                labelEl.style.fontSize = Design.FontSizeSmall;
                container.Add(labelEl);
            }

            // 使用 userData 存储当前状态
            container.userData = initialValue;
            container.RegisterCallback<ClickEvent>(_ =>
            {
                var isChecked = !(bool)container.userData;
                container.userData = isChecked;
                track.style.backgroundColor = new StyleColor(isChecked ? Design.BrandPrimary : new Color(0.3f, 0.3f, 0.32f));
                thumb.style.left = isChecked ? 18 : 2;
                onValueChanged?.Invoke(isChecked);
            });

            return container;
        }

        /// <summary>
        /// 更新 Toggle 开关的视觉状态（不触发回调）
        /// </summary>
        private void UpdateCapsuleToggle(VisualElement toggle, bool value)
        {
            if (toggle == null) return;

            toggle.userData = value;
            var track = toggle.Q<VisualElement>("toggle-track");
            var thumb = toggle.Q<VisualElement>("toggle-thumb");

            if (track != null)
                track.style.backgroundColor = new StyleColor(value ? Design.BrandPrimary : new Color(0.3f, 0.3f, 0.32f));
            if (thumb != null)
                thumb.style.left = value ? 18 : 2;
        }

        #endregion

        #region Callout 与子区域

        private VisualElement CreateCallout(string message, Color accentColor)
        {
            var callout = new VisualElement();
            callout.style.flexDirection = FlexDirection.Row;
            callout.style.alignItems = Align.Center;
            callout.style.backgroundColor = new StyleColor(new Color(accentColor.r * 0.15f, accentColor.g * 0.15f, accentColor.b * 0.15f, 0.5f));
            callout.style.borderLeftWidth = 3;
            callout.style.borderLeftColor = new StyleColor(accentColor);
            callout.style.borderTopLeftRadius = callout.style.borderTopRightRadius = 4;
            callout.style.borderBottomLeftRadius = callout.style.borderBottomRightRadius = 4;
            callout.style.paddingLeft = 10;
            callout.style.paddingRight = 10;
            callout.style.paddingTop = 8;
            callout.style.paddingBottom = 8;

            var text = new Label(message);
            text.style.fontSize = 11;
            text.style.color = new StyleColor(accentColor);
            text.style.whiteSpace = WhiteSpace.Normal;
            callout.Add(text);

            return callout;
        }

        private VisualElement CreateSubSection(string title)
        {
            var section = new VisualElement { style = { marginTop = 12 } };

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = new StyleColor(Design.TextTertiary);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 4;
            titleLabel.style.letterSpacing = 1;
            section.Add(titleLabel);

            return section;
        }

        #endregion

        #region 按钮样式

        private void ApplySecondaryButtonStyle(Button btn)
        {
            btn.style.height = 28;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.backgroundColor = new StyleColor(Design.LayerElevated);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
        }

        private void ApplySmallButtonStyle(Button btn)
        {
            btn.style.height = 24;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.backgroundColor = new StyleColor(Design.LayerElevated);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
        }

        private void ApplyBrowseButtonStyle(Button btn)
        {
            btn.style.width = 28;
            btn.style.height = 20;
            btn.style.marginLeft = 4;
            btn.style.backgroundColor = new StyleColor(Design.LayerElevated);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
        }

        /// <summary>
        /// 创建快速打开目录按钮
        /// </summary>
        /// <param name="getPath">获取路径的委托，支持动态获取当前输入框的值</param>
        private Button CreateOpenFolderButton(Func<string> getPath)
        {
            var btn = new Button(() =>
            {
                var path = getPath?.Invoke();
                if (string.IsNullOrEmpty(path)) return;

                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path);

                if (Directory.Exists(fullPath))
                    EditorUtility.RevealInFinder(fullPath);
                else
                    EditorUtility.DisplayDialog("提示", $"目录不存在:\n{fullPath}", "确定");
            }) { text = "↗" };

            btn.style.width = 24;
            btn.style.height = 20;
            btn.style.marginLeft = 2;
            btn.style.backgroundColor = new StyleColor(Design.LayerElevated);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            btn.tooltip = "在资源管理器中打开";

            return btn;
        }

        #endregion
    }
}
#endif
