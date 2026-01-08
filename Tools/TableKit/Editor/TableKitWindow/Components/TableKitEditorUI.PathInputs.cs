#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 路径输入组件与验证
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 路径输入组件

        private VisualElement CreateValidatedPathRow(string labelText, ref TextField textField, string initialValue,
            Action<string> onPathChanged, bool isAbsolute, string dialogTitle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;

            var label = new Label(labelText);
            label.style.width = 80;
            label.style.color = new StyleColor(Design.TextSecondary);
            label.style.fontSize = 12;
            row.Add(label);

            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.flexGrow = 1;
            row.Add(fieldContainer);

            var field = new TextField();
            field.style.flexGrow = 1;
            field.value = initialValue;
            UpdatePathValidation(field, initialValue, isAbsolute);
            field.RegisterValueChangedCallback(evt =>
            {
                UpdatePathValidation(field, evt.newValue, isAbsolute);
                onPathChanged?.Invoke(evt.newValue);
            });
            fieldContainer.Add(field);
            textField = field;

            var btnContainer = new VisualElement();
            btnContainer.style.flexDirection = FlexDirection.Row;
            row.Add(btnContainer);

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
            btnContainer.Add(browseBtn);

            var openBtn = CreateOpenFolderButton(() => field.value);
            btnContainer.Add(openBtn);

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
            label.style.width = 80;
            label.style.color = new StyleColor(Design.TextSecondary);
            label.style.fontSize = 12;
            row.Add(label);

            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.flexGrow = 1;
            row.Add(fieldContainer);

            var field = new TextField();
            field.style.flexGrow = 1;
            field.value = initialValue;
            UpdateFileValidation(field, initialValue);
            field.RegisterValueChangedCallback(evt =>
            {
                UpdateFileValidation(field, evt.newValue);
                onPathChanged?.Invoke(evt.newValue);
            });
            fieldContainer.Add(field);
            textField = field;

            var btnContainer = new VisualElement();
            btnContainer.style.flexDirection = FlexDirection.Row;
            row.Add(btnContainer);

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
            btnContainer.Add(browseBtn);

            var openBtn = CreateOpenFolderButton(() => Path.GetDirectoryName(field.value));
            btnContainer.Add(openBtn);

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

        /// <summary>
        /// 刷新配置状态指示器
        /// </summary>
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
    }
}
#endif
