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

            // 浏览按钮
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
            }) { text = "..." };
            ApplyBrowseButtonStyle(browseBtn);
            browseBtn.tooltip = "浏览文件夹";
            pathContainer.Add(browseBtn);

            // 快速打开目录按钮
            var openBtn = CreateOpenFolderButton(() => field.value);
            pathContainer.Add(openBtn);

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

            // 浏览按钮
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
            }) { text = "..." };
            ApplyBrowseButtonStyle(browseBtn);
            browseBtn.tooltip = "浏览文件";
            pathContainer.Add(browseBtn);

            // 快速打开所在目录按钮
            var openBtn = CreateOpenFolderButton(() => Path.GetDirectoryName(field.value));
            pathContainer.Add(openBtn);

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

            // 检查 Luban 环境（必须）
            // 1. 工作目录必须存在且包含 luban.conf 文件
            var workDir = string.IsNullOrEmpty(mLubanWorkDir) ? "" :
                (Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir));
            bool workDirValid = !string.IsNullOrEmpty(mLubanWorkDir) 
                && Directory.Exists(workDir)
                && File.Exists(Path.Combine(workDir, "luban.conf"));

            // 2. Luban.dll 路径必须存在且文件名为 Luban.dll
            var dllPath = string.IsNullOrEmpty(mLubanDllPath) ? "" :
                (Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath));
            bool dllValid = !string.IsNullOrEmpty(mLubanDllPath) 
                && File.Exists(dllPath)
                && Path.GetFileName(dllPath).Equals("Luban.dll", StringComparison.OrdinalIgnoreCase);

            bool lubanValid = workDirValid && dllValid;

            // 检查输出路径（可选但推荐）
            var dataDir = string.IsNullOrEmpty(mOutputDataDir) ? "" :
                (Path.IsPathRooted(mOutputDataDir) ? mOutputDataDir : Path.Combine(projectRoot, mOutputDataDir));
            bool dataDirValid = !string.IsNullOrEmpty(mOutputDataDir) && Directory.Exists(dataDir);

            var codeDir = string.IsNullOrEmpty(mOutputCodeDir) ? "" :
                (Path.IsPathRooted(mOutputCodeDir) ? mOutputCodeDir : Path.Combine(projectRoot, mOutputCodeDir));
            bool codeDirValid = !string.IsNullOrEmpty(mOutputCodeDir) && Directory.Exists(codeDir);

            bool outputValid = dataDirValid && codeDirValid;

            // 设置状态点颜色
            // 红色：Luban 环境无效（工作目录无 luban.conf 或 Luban.dll 路径错误）
            // 黄色：Luban 有效但输出目录无效
            // 绿色：全部有效
            Color statusColor;
            if (!lubanValid)
                statusColor = Design.BrandDanger;  // 红色
            else if (!outputValid)
                statusColor = Design.BrandWarning; // 黄色
            else
                statusColor = Design.BrandSuccess; // 绿色

            mConfigStatusDot.style.backgroundColor = new StyleColor(statusColor);
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
