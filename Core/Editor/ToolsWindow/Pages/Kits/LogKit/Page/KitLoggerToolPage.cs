#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// KitLogger 工具页面，提供日志目录、运行配置和日志文件状态管理。
    /// </summary>
    [YokiToolPage(
        kit: "LogKit",
        name: "KitLogger",
        icon: KitIcons.KITLOGGER,
        priority: 36,
        category: YokiPageCategory.Tool)]
    public class KitLoggerToolPage : YokiToolPageBase
    {
        private Label mLogDirLabel;
        private Label mEditorLogLabel;
        private Label mPlayerLogLabel;

        private VisualElement mSaveLogEditorToggle;
        private VisualElement mSaveLogPlayerToggle;
        private VisualElement mEnableIMGUIPlayerToggle;
        private VisualElement mEncryptionToggle;

        private TextField mMaxQueueSizeField;
        private TextField mMaxSameLogCountField;
        private TextField mMaxRetentionDaysField;
        private TextField mMaxFileMBField;

        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "KitLogger",
                "统一管理日志目录、运行配置、文件状态与解密流程。",
                KitIcons.KITLOGGER,
                "KIT 配置工具");
            root.Add(scaffold.Root);

            var content = new ScrollView();
            content.AddToClassList("content-area");
            content.AddToClassList("content-area--padded");
            scaffold.Content.Add(content);

            scaffold.Toolbar.Add(CreateToolbarSection());

            content.Add(CreateDirectoryCard());
            content.Add(CreateConfigCard());
            content.Add(CreateFileStatusCard());

            RefreshStatus();
        }

        private VisualElement CreateToolbarSection()
        {
            var toolbar = CreateToolbar();

            var openDirButton = CreateToolbarButtonWithIcon(KitIcons.FOLDER_DOCS, "打开日志目录", OpenLogFolder);
            openDirButton.AddToClassList("primary");
            toolbar.Add(openDirButton);

            toolbar.Add(CreateToolbarButton("解密日志", DecryptLogFile));
            toolbar.Add(CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshStatus));
            toolbar.Add(CreateToolbarSpacer());
            toolbar.Add(CreateToolbarButtonWithIcon(KitIcons.RESET, "重置默认", ResetToDefault));

            return toolbar;
        }

        private VisualElement CreateDirectoryCard()
        {
            var (card, body) = CreateCard("日志目录", KitIcons.FOLDER_DOCS);
            card.AddToClassList("yoki-card--spaced");

            var (row, valueLabel) = CreateInfoRow("路径");
            valueLabel.AddToClassList("yoki-info-value--wrap");
            mLogDirLabel = valueLabel;
            body.Add(row);

            return card;
        }

        private VisualElement CreateConfigCard()
        {
            var (card, body) = CreateCard("配置", KitIcons.SETTINGS);
            card.AddToClassList("yoki-card--spaced");

            var toggleSection = new VisualElement();
            toggleSection.AddToClassList("yoki-toggle-section");

            mSaveLogEditorToggle = CreateModernToggle(
                "编辑器保存日志",
                KitLogger.SaveLogInEditor,
                value => KitLogger.SaveLogInEditor = value);
            toggleSection.Add(mSaveLogEditorToggle);

            mSaveLogPlayerToggle = CreateModernToggle(
                "真机保存日志",
                KitLogger.SaveLogInPlayer,
                value => KitLogger.SaveLogInPlayer = value);
            toggleSection.Add(mSaveLogPlayerToggle);

            mEnableIMGUIPlayerToggle = CreateModernToggle(
                "真机启用 IMGUI",
                KitLogger.EnableIMGUIInPlayer,
                value => KitLogger.EnableIMGUIInPlayer = value);
            toggleSection.Add(mEnableIMGUIPlayerToggle);

            mEncryptionToggle = CreateModernToggle(
                "启用加密",
                KitLogger.EnableEncryption,
                value => KitLogger.EnableEncryption = value);
            toggleSection.Add(mEncryptionToggle);

            body.Add(toggleSection);
            body.Add(YokiFrameUIComponents.CreateDivider());

            var configSection = new VisualElement();
            configSection.AddToClassList("yoki-config-section");

            var configTitle = CreateSectionHeader("高级配置", KitIcons.SETTINGS);
            configTitle.AddToClassList("yoki-config-section__title");
            configSection.Add(configTitle);

            var (queueRow, queueField) = CreateIntConfigRow(
                "最大队列",
                KitLogger.MaxQueueSize,
                value => KitLogger.MaxQueueSize = Mathf.Max(100, value),
                100);
            mMaxQueueSizeField = queueField;
            configSection.Add(queueRow);

            var (sameLogRow, sameLogField) = CreateIntConfigRow(
                "重复日志阈值",
                KitLogger.MaxSameLogCount,
                value => KitLogger.MaxSameLogCount = Mathf.Max(1, value),
                1);
            mMaxSameLogCountField = sameLogField;
            configSection.Add(sameLogRow);

            var (retentionRow, retentionField) = CreateIntConfigRow(
                "保留天数",
                KitLogger.MaxRetentionDays,
                value => KitLogger.MaxRetentionDays = Mathf.Max(1, value),
                1);
            mMaxRetentionDaysField = retentionField;
            configSection.Add(retentionRow);

            var (fileSizeRow, fileSizeField) = CreateIntConfigRow(
                "单文件上限 (MB)",
                (int)(KitLogger.MaxFileBytes / 1024 / 1024),
                value => KitLogger.MaxFileBytes = Mathf.Max(1, value) * 1024L * 1024L,
                1);
            mMaxFileMBField = fileSizeField;
            configSection.Add(fileSizeRow);

            body.Add(configSection);
            return card;
        }

        private VisualElement CreateFileStatusCard()
        {
            var (card, body) = CreateCard("日志文件", KitIcons.DOCUMENTATION);
            card.AddToClassList("yoki-card--spaced");

            var (editorRow, editorValue) = CreateInfoRow("editor.log");
            mEditorLogLabel = editorValue;
            body.Add(editorRow);

            var (playerRow, playerValue) = CreateInfoRow("player.log");
            mPlayerLogLabel = playerValue;
            body.Add(playerRow);

            return card;
        }

        private void RefreshStatus()
        {
            string logDir = KitLoggerWriter.LogDirectory;
            mLogDirLabel.text = logDir;

            UpdateToggleState(mSaveLogEditorToggle, KitLogger.SaveLogInEditor);
            UpdateToggleState(mSaveLogPlayerToggle, KitLogger.SaveLogInPlayer);
            UpdateToggleState(mEnableIMGUIPlayerToggle, KitLogger.EnableIMGUIInPlayer);
            UpdateToggleState(mEncryptionToggle, KitLogger.EnableEncryption);

            mMaxQueueSizeField?.SetValueWithoutNotify(KitLogger.MaxQueueSize.ToString());
            mMaxSameLogCountField?.SetValueWithoutNotify(KitLogger.MaxSameLogCount.ToString());
            mMaxRetentionDaysField?.SetValueWithoutNotify(KitLogger.MaxRetentionDays.ToString());
            mMaxFileMBField?.SetValueWithoutNotify(((int)(KitLogger.MaxFileBytes / 1024 / 1024)).ToString());

            string editorLog = Path.Combine(logDir, "editor.log");
            string playerLog = Path.Combine(logDir, "player.log");

            mEditorLogLabel.text = GetFileStatus(editorLog);
            mPlayerLogLabel.text = GetFileStatus(playerLog);
        }

        private static void UpdateToggleState(VisualElement toggle, bool isChecked)
        {
            if (toggle == null)
            {
                return;
            }

            if (isChecked && !toggle.ClassListContains("checked"))
            {
                toggle.AddToClassList("checked");
            }
            else if (!isChecked && toggle.ClassListContains("checked"))
            {
                toggle.RemoveFromClassList("checked");
            }
        }

        private static string GetFileStatus(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return "不存在";
            }

            var info = new FileInfo(filePath);
            string size = FormatFileSize(info.Length);
            string time = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            return $"{size} | {time}";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }

        private void OpenLogFolder()
        {
            string dir = KitLoggerWriter.LogDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string filePath = Path.Combine(dir, "editor.log");
            if (File.Exists(filePath))
            {
                EditorUtility.RevealInFinder(filePath);
            }
            else
            {
                EditorUtility.RevealInFinder(dir);
            }
        }

        private void DecryptLogFile()
        {
            string dir = KitLoggerWriter.LogDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string path = EditorUtility.OpenFilePanel("选择日志文件", dir, "log,txt");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string[] lines = File.ReadAllLines(path);
            var sb = new StringBuilder(lines.Length * 256);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string decoded = KitLoggerWriter.DecryptString(line);
                sb.AppendLine(decoded);
            }

            string outPath = path + ".decoded.log";
            File.WriteAllText(outPath, sb.ToString());
            EditorUtility.RevealInFinder(outPath);
        }

        private void ResetToDefault()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "重置配置",
                "确定要将所有配置重置为默认值吗？",
                "确定",
                "取消");
            if (!confirmed)
            {
                return;
            }

            KitLogger.ResetToDefault();
            RefreshStatus();
        }

        public override void OnActivate()
        {
            base.OnActivate();
            RefreshStatus();
        }
    }
}
#endif
