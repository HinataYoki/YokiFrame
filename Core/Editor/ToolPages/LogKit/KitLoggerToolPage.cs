#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// KitLogger 工具页面 - 日志管理
    /// 采用现代化 UI 设计：卡片布局、Toggle 开关、品牌色按钮
    /// 使用 USS 类消除内联样式
    /// </summary>
    public class KitLoggerToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "KitLogger";
        public override string PageIcon => KitIcons.KITLOGGER;
        public override int Priority => 36;

        // UI 元素引用
        private Label mLogDirLabel;
        private Label mEditorLogLabel;
        private Label mPlayerLogLabel;
        
        // Toggle 容器引用（用于更新状态）
        private VisualElement mSaveLogEditorToggle;
        private VisualElement mSaveLogPlayerToggle;
        private VisualElement mEnableIMGUIPlayerToggle;
        private VisualElement mEncryptionToggle;
        
        // 配置字段引用
        private TextField mMaxQueueSizeField;
        private TextField mMaxSameLogCountField;
        private TextField mMaxRetentionDaysField;
        private TextField mMaxFileMBField;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            root.Add(CreateToolbarSection());

            // 主内容区（带滚动）
            var content = new ScrollView();
            content.AddToClassList("content-area");
            content.AddToClassList("content-area--padded");
            root.Add(content);

            // 日志目录卡片
            content.Add(CreateDirectoryCard());

            // 配置卡片
            content.Add(CreateConfigCard());

            // 日志文件状态卡片
            content.Add(CreateFileStatusCard());

            RefreshStatus();
        }

        private VisualElement CreateToolbarSection()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            // 主按钮 - 品牌色填充
            var openDirBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.FOLDER_DOCS, "打开日志目录", OpenLogFolder);
            openDirBtn.AddToClassList("primary");
            toolbar.Add(openDirBtn);

            // 次要按钮
            var decryptBtn = YokiFrameUIComponents.CreateToolbarButton("解密日志", DecryptLogFile);
            toolbar.Add(decryptBtn);

            var refreshBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", RefreshStatus);
            toolbar.Add(refreshBtn);

            // 弹性空间
            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            // 重置按钮放右侧
            var resetBtn = YokiFrameUIComponents.CreateToolbarButtonWithIcon(KitIcons.RESET, "重置默认", ResetToDefault);
            toolbar.Add(resetBtn);

            return toolbar;
        }

        private VisualElement CreateDirectoryCard()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("日志目录", KitIcons.FOLDER_DOCS);
            card.AddToClassList("yoki-card--spaced");

            var (row, valueLabel) = YokiFrameUIComponents.CreateInfoRow("路径");
            valueLabel.AddToClassList("yoki-info-value--wrap");
            mLogDirLabel = valueLabel;
            body.Add(row);

            return card;
        }

        private VisualElement CreateConfigCard()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("配置", KitIcons.SETTINGS);
            card.AddToClassList("yoki-card--spaced");

            // === Toggle 开关区域 ===
            var toggleSection = new VisualElement();
            toggleSection.AddToClassList("yoki-toggle-section");

            // 编辑器保存日志
            mSaveLogEditorToggle = YokiFrameUIComponents.CreateModernToggle(
                "编辑器保存日志",
                KitLogger.SaveLogInEditor,
                value => KitLogger.SaveLogInEditor = value
            );
            toggleSection.Add(mSaveLogEditorToggle);

            // 真机保存日志
            mSaveLogPlayerToggle = YokiFrameUIComponents.CreateModernToggle(
                "真机保存日志",
                KitLogger.SaveLogInPlayer,
                value => KitLogger.SaveLogInPlayer = value
            );
            toggleSection.Add(mSaveLogPlayerToggle);

            // 真机 IMGUI
            mEnableIMGUIPlayerToggle = YokiFrameUIComponents.CreateModernToggle(
                "真机启用 IMGUI",
                KitLogger.EnableIMGUIInPlayer,
                value => KitLogger.EnableIMGUIInPlayer = value
            );
            toggleSection.Add(mEnableIMGUIPlayerToggle);

            // 启用加密
            mEncryptionToggle = YokiFrameUIComponents.CreateModernToggle(
                "启用加密",
                KitLogger.EnableEncryption,
                value => KitLogger.EnableEncryption = value
            );
            toggleSection.Add(mEncryptionToggle);

            body.Add(toggleSection);

            // === 分隔线 ===
            body.Add(YokiFrameUIComponents.CreateDivider());

            // === 数值配置区域 ===
            var configSection = new VisualElement();
            configSection.AddToClassList("yoki-config-section");

            // 配置项标题
            var configTitle = new Label("高级配置");
            configTitle.AddToClassList("yoki-config-section__title");
            configSection.Add(configTitle);

            // 最大队列
            var (queueRow, queueField) = YokiFrameUIComponents.CreateIntConfigRow(
                "最大队列",
                KitLogger.MaxQueueSize,
                value => KitLogger.MaxQueueSize = Mathf.Max(100, value),
                100
            );
            mMaxQueueSizeField = queueField;
            configSection.Add(queueRow);

            // 重复日志阈值
            var (sameLogRow, sameLogField) = YokiFrameUIComponents.CreateIntConfigRow(
                "重复日志阈值",
                KitLogger.MaxSameLogCount,
                value => KitLogger.MaxSameLogCount = Mathf.Max(1, value),
                1
            );
            mMaxSameLogCountField = sameLogField;
            configSection.Add(sameLogRow);

            // 保留天数
            var (retentionRow, retentionField) = YokiFrameUIComponents.CreateIntConfigRow(
                "保留天数",
                KitLogger.MaxRetentionDays,
                value => KitLogger.MaxRetentionDays = Mathf.Max(1, value),
                1
            );
            mMaxRetentionDaysField = retentionField;
            configSection.Add(retentionRow);

            // 单文件上限
            var (fileSizeRow, fileSizeField) = YokiFrameUIComponents.CreateIntConfigRow(
                "单文件上限 (MB)",
                (int)(KitLogger.MaxFileBytes / 1024 / 1024),
                value => KitLogger.MaxFileBytes = Mathf.Max(1, value) * 1024L * 1024L,
                1
            );
            mMaxFileMBField = fileSizeField;
            configSection.Add(fileSizeRow);

            body.Add(configSection);

            return card;
        }

        private VisualElement CreateFileStatusCard()
        {
            var (card, body) = YokiFrameUIComponents.CreateCard("日志文件", KitIcons.DOCUMENTATION);
            card.AddToClassList("yoki-card--spaced");

            var (editorRow, editorValue) = YokiFrameUIComponents.CreateInfoRow("editor.log");
            mEditorLogLabel = editorValue;
            body.Add(editorRow);

            var (playerRow, playerValue) = YokiFrameUIComponents.CreateInfoRow("player.log");
            mPlayerLogLabel = playerValue;
            body.Add(playerRow);

            return card;
        }

        private void RefreshStatus()
        {
            string logDir = KitLoggerWriter.LogDirectory;
            mLogDirLabel.text = logDir;

            // 更新 Toggle 状态
            UpdateToggleState(mSaveLogEditorToggle, KitLogger.SaveLogInEditor);
            UpdateToggleState(mSaveLogPlayerToggle, KitLogger.SaveLogInPlayer);
            UpdateToggleState(mEnableIMGUIPlayerToggle, KitLogger.EnableIMGUIInPlayer);
            UpdateToggleState(mEncryptionToggle, KitLogger.EnableEncryption);

            // 更新配置字段
            mMaxQueueSizeField?.SetValueWithoutNotify(KitLogger.MaxQueueSize.ToString());
            mMaxSameLogCountField?.SetValueWithoutNotify(KitLogger.MaxSameLogCount.ToString());
            mMaxRetentionDaysField?.SetValueWithoutNotify(KitLogger.MaxRetentionDays.ToString());
            mMaxFileMBField?.SetValueWithoutNotify(((int)(KitLogger.MaxFileBytes / 1024 / 1024)).ToString());

            // 检查日志文件状态
            string editorLog = Path.Combine(logDir, "editor.log");
            string playerLog = Path.Combine(logDir, "player.log");

            mEditorLogLabel.text = GetFileStatus(editorLog);
            mPlayerLogLabel.text = GetFileStatus(playerLog);
        }

        private static void UpdateToggleState(VisualElement toggle, bool isChecked)
        {
            if (toggle == null) return;
            
            if (isChecked && !toggle.ClassListContains("checked"))
                toggle.AddToClassList("checked");
            else if (!isChecked && toggle.ClassListContains("checked"))
                toggle.RemoveFromClassList("checked");
        }

        private static string GetFileStatus(string filePath)
        {
            if (!File.Exists(filePath))
                return "不存在";

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
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, "editor.log");
            if (File.Exists(filePath)) EditorUtility.RevealInFinder(filePath);
            else EditorUtility.RevealInFinder(dir);
        }

        private void DecryptLogFile()
        {
            string dir = KitLoggerWriter.LogDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = EditorUtility.OpenFilePanel("选择日志文件", dir, "log,txt");
            if (string.IsNullOrEmpty(path)) return;

            string[] lines = File.ReadAllLines(path);
            var sb = new StringBuilder(lines.Length * 256);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string decoded = KitLoggerWriter.DecryptString(line);
                sb.AppendLine(decoded);
            }

            string outPath = path + ".decoded.log";
            File.WriteAllText(outPath, sb.ToString());
            EditorUtility.RevealInFinder(outPath);
            Debug.Log($"[KitLogger] 解密完成: {outPath}");
        }

        private void ResetToDefault()
        {
            if (!EditorUtility.DisplayDialog("重置配置", "确定要将所有配置重置为默认值吗？", "确定", "取消"))
                return;

            KitLogger.ResetToDefault();
            RefreshStatus();
            Debug.Log("[KitLogger] 配置已重置为默认值");
        }

        public override void OnActivate()
        {
            base.OnActivate();
            RefreshStatus();
        }
    }
}
#endif
