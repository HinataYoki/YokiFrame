#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - UI 组件与辅助方法
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 胶囊 Toggle 组件

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
                labelEl.style.fontSize = 12;
                container.Add(labelEl);
            }

            bool isChecked = initialValue;
            container.RegisterCallback<ClickEvent>(_ =>
            {
                isChecked = !isChecked;
                track.style.backgroundColor = new StyleColor(isChecked ? Design.BrandPrimary : new Color(0.3f, 0.3f, 0.32f));
                thumb.style.left = isChecked ? 18 : 2;
                onValueChanged?.Invoke(isChecked);
            });

            return container;
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

        #region 路径输入组件

        private VisualElement CreateValidatedPathRow(string labelText, ref TextField textField, string initialValue,
            Action<string> onPathChanged, bool isAbsolute, string dialogTitle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;

            var label = new Label(labelText);
            label.style.width = 100;
            label.style.color = new StyleColor(Design.TextSecondary);
            label.style.fontSize = 12;
            row.Add(label);

            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;

            var field = new TextField();
            field.style.flexGrow = 1;
            field.value = initialValue;
            UpdatePathValidation(field, initialValue, isAbsolute);
            field.RegisterValueChangedCallback(evt =>
            {
                UpdatePathValidation(field, evt.newValue, isAbsolute);
                onPathChanged?.Invoke(evt.newValue);
            });
            pathContainer.Add(field);
            textField = field;

            var browseBtn = new Button(() =>
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var startPath = string.IsNullOrEmpty(initialValue) ? projectRoot :
                    (Path.IsPathRooted(initialValue) ? initialValue : Path.Combine(projectRoot, initialValue));
                var path = EditorUtility.OpenFolderPanel(dialogTitle, startPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string newPath;
                    if (isAbsolute)
                    {
                        newPath = GetRelativePath(projectRoot, path);
                    }
                    else
                    {
                        var idx = path.IndexOf("Assets", StringComparison.Ordinal);
                        newPath = idx >= 0 ? path.Substring(idx) : path;
                        if (!newPath.EndsWith("/")) newPath += "/";
                    }
                    field.value = newPath;
                    onPathChanged?.Invoke(newPath);
                }
            }) { text = "📁" };
            ApplyBrowseButtonStyle(browseBtn);
            pathContainer.Add(browseBtn);

            row.Add(pathContainer);
            return row;
        }

        private VisualElement CreateValidatedFileRow(string labelText, ref TextField textField, string initialValue,
            Action<string> onPathChanged, string extension, string dialogTitle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;

            var label = new Label(labelText);
            label.style.width = 100;
            label.style.color = new StyleColor(Design.TextSecondary);
            label.style.fontSize = 12;
            row.Add(label);

            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;

            var field = new TextField();
            field.style.flexGrow = 1;
            field.value = initialValue;
            UpdateFileValidation(field, initialValue);
            field.RegisterValueChangedCallback(evt =>
            {
                UpdateFileValidation(field, evt.newValue);
                onPathChanged?.Invoke(evt.newValue);
            });
            pathContainer.Add(field);
            textField = field;

            var browseBtn = new Button(() =>
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var startPath = string.IsNullOrEmpty(initialValue) ? projectRoot :
                    Path.GetDirectoryName(Path.IsPathRooted(initialValue) ? initialValue : Path.Combine(projectRoot, initialValue));
                var path = EditorUtility.OpenFilePanel(dialogTitle, startPath, extension);
                if (!string.IsNullOrEmpty(path))
                {
                    var relativePath = GetRelativePath(projectRoot, path);
                    field.value = relativePath;
                    onPathChanged?.Invoke(relativePath);
                }
            }) { text = "📄" };
            ApplyBrowseButtonStyle(browseBtn);
            pathContainer.Add(browseBtn);

            row.Add(pathContainer);
            return row;
        }

        #endregion

        #region 验证与路径辅助

        private void UpdatePathValidation(TextField field, string path, bool isAbsolute)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullPath = string.IsNullOrEmpty(path) ? "" :
                (Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));

            bool isValid = !string.IsNullOrEmpty(path) && Directory.Exists(fullPath);
            ApplyValidationBorder(field, isValid);
        }

        private void UpdateFileValidation(TextField field, string path)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullPath = string.IsNullOrEmpty(path) ? "" :
                (Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));

            bool isValid = !string.IsNullOrEmpty(path) && File.Exists(fullPath);
            ApplyValidationBorder(field, isValid);
        }

        private void ApplyValidationBorder(TextField field, bool isValid)
        {
            field.style.borderLeftWidth = field.style.borderRightWidth = 1;
            field.style.borderTopWidth = field.style.borderBottomWidth = 1;
            var color = new StyleColor(isValid ? Design.BorderValid : Design.BorderInvalid);
            field.style.borderLeftColor = field.style.borderRightColor = color;
            field.style.borderTopColor = field.style.borderBottomColor = color;
        }

        private string GetRelativePath(string projectRoot, string absolutePath)
        {
            projectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            absolutePath = absolutePath.Replace('\\', '/');

            if (absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relative = absolutePath.Substring(projectRoot.Length).TrimStart('/');
                return string.IsNullOrEmpty(relative) ? "." : relative;
            }
            return absolutePath;
        }

        private void RefreshConfigStatus()
        {
            if (mConfigStatusDot == null) return;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            var workDir = string.IsNullOrEmpty(mLubanWorkDir) ? "" :
                (Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir));
            bool workDirValid = !string.IsNullOrEmpty(mLubanWorkDir) && Directory.Exists(workDir);

            var dllPath = string.IsNullOrEmpty(mLubanDllPath) ? "" :
                (Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath));
            bool dllValid = !string.IsNullOrEmpty(mLubanDllPath) && File.Exists(dllPath);

            bool allValid = workDirValid && dllValid;
            mConfigStatusDot.style.backgroundColor = new StyleColor(allValid ? Design.BrandSuccess : Design.BrandDanger);
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

        #endregion
    }
}
#endif
