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
    /// </summary>
    public class KitLoggerToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "KitLogger";
        public override string PageIcon => KitIcons.KITLOGGER;
        public override int Priority => 36;

        private Label mLogDirLabel;
        private Label mEditorLogLabel;
        private Label mPlayerLogLabel;
        private Toggle mSaveLogEditorToggle;
        private Toggle mSaveLogPlayerToggle;
        private Toggle mEnableIMGUIPlayerToggle;
        private Toggle mEncryptionToggle;
        private IntegerField mMaxQueueSizeField;
        private IntegerField mMaxSameLogCountField;
        private IntegerField mMaxRetentionDaysField;
        private IntegerField mMaxFileMBField;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            root.Add(toolbar);

            var openDirBtn = new Button(OpenLogFolder) { text = "📂 打开日志目录" };
            openDirBtn.AddToClassList("toolbar-button");
            toolbar.Add(openDirBtn);

            var decryptBtn = new Button(DecryptLogFile) { text = "🔓 解密日志文件" };
            decryptBtn.AddToClassList("toolbar-button");
            toolbar.Add(decryptBtn);

            var refreshBtn = new Button(RefreshStatus) { text = "🔄 刷新" };
            refreshBtn.AddToClassList("toolbar-button");
            toolbar.Add(refreshBtn);

            var resetBtn = new Button(ResetToDefault) { text = "↩️ 重置默认" };
            resetBtn.AddToClassList("toolbar-button");
            toolbar.Add(resetBtn);

            // 主内容区
            var content = new ScrollView();
            content.style.flexGrow = 1;
            content.style.paddingLeft = 20;
            content.style.paddingRight = 20;
            content.style.paddingTop = 20;
            root.Add(content);

            // 日志目录信息卡片
            content.Add(CreateDirectoryCard());

            // 配置卡片
            content.Add(CreateConfigCard());

            // 日志文件状态卡片
            content.Add(CreateFileStatusCard());

            RefreshStatus();
        }

        private VisualElement CreateDirectoryCard()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.style.marginBottom = 16;

            var header = new VisualElement();
            header.AddToClassList("card-header");
            var title = new Label("📁 日志目录");
            title.AddToClassList("card-title");
            header.Add(title);
            card.Add(header);

            var body = new VisualElement();
            body.AddToClassList("card-body");
            card.Add(body);

            mLogDirLabel = CreateInfoRow(body, "路径");
            mLogDirLabel.style.whiteSpace = WhiteSpace.Normal;
            mLogDirLabel.style.overflow = Overflow.Hidden;

            return card;
        }

        private VisualElement CreateConfigCard()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.style.marginBottom = 16;

            var header = new VisualElement();
            header.AddToClassList("card-header");
            var title = new Label("⚙️ 配置");
            title.AddToClassList("card-title");
            header.Add(title);
            card.Add(header);

            var body = new VisualElement();
            body.AddToClassList("card-body");
            card.Add(body);

            // 编辑器保存日志开关
            var saveLogEditorRow = new VisualElement();
            saveLogEditorRow.AddToClassList("info-row");
            saveLogEditorRow.style.alignItems = Align.Center;

            var saveLogEditorLabel = new Label("编辑器保存日志");
            saveLogEditorLabel.AddToClassList("info-label");
            saveLogEditorRow.Add(saveLogEditorLabel);

            mSaveLogEditorToggle = new Toggle { value = KitLogger.SaveLogInEditor };
            mSaveLogEditorToggle.RegisterValueChangedCallback(evt =>
            {
                KitLogger.SaveLogInEditor = evt.newValue;
            });
            saveLogEditorRow.Add(mSaveLogEditorToggle);
            body.Add(saveLogEditorRow);

            // 真机保存日志开关
            var saveLogPlayerRow = new VisualElement();
            saveLogPlayerRow.AddToClassList("info-row");
            saveLogPlayerRow.style.alignItems = Align.Center;

            var saveLogPlayerLabel = new Label("真机保存日志");
            saveLogPlayerLabel.AddToClassList("info-label");
            saveLogPlayerRow.Add(saveLogPlayerLabel);

            mSaveLogPlayerToggle = new Toggle { value = KitLogger.SaveLogInPlayer };
            mSaveLogPlayerToggle.RegisterValueChangedCallback(evt =>
            {
                KitLogger.SaveLogInPlayer = evt.newValue;
            });
            saveLogPlayerRow.Add(mSaveLogPlayerToggle);
            body.Add(saveLogPlayerRow);

            // 真机 IMGUI 开关
            var imguiPlayerRow = new VisualElement();
            imguiPlayerRow.AddToClassList("info-row");
            imguiPlayerRow.style.alignItems = Align.Center;

            var imguiPlayerLabel = new Label("真机启用 IMGUI");
            imguiPlayerLabel.AddToClassList("info-label");
            imguiPlayerRow.Add(imguiPlayerLabel);

            mEnableIMGUIPlayerToggle = new Toggle { value = KitLogger.EnableIMGUIInPlayer };
            mEnableIMGUIPlayerToggle.RegisterValueChangedCallback(evt =>
            {
                KitLogger.EnableIMGUIInPlayer = evt.newValue;
            });
            imguiPlayerRow.Add(mEnableIMGUIPlayerToggle);
            body.Add(imguiPlayerRow);

            // 加密开关
            var encryptRow = new VisualElement();
            encryptRow.AddToClassList("info-row");
            encryptRow.style.alignItems = Align.Center;

            var encryptLabel = new Label("启用加密");
            encryptLabel.AddToClassList("info-label");
            encryptRow.Add(encryptLabel);

            mEncryptionToggle = new Toggle { value = KitLogger.EnableEncryption };
            mEncryptionToggle.RegisterValueChangedCallback(evt =>
            {
                KitLogger.EnableEncryption = evt.newValue;
            });
            encryptRow.Add(mEncryptionToggle);
            body.Add(encryptRow);

            // 可配置项
            var configInfo = new VisualElement();
            configInfo.style.marginTop = 12;
            configInfo.style.paddingTop = 12;
            configInfo.style.borderTopWidth = 1;
            configInfo.style.borderTopColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));

            // 最大队列
            mMaxQueueSizeField = CreateIntConfigRow(configInfo, "最大队列", KitLogger.MaxQueueSize, value =>
            {
                KitLogger.MaxQueueSize = Mathf.Max(100, value);
            });

            // 重复日志阈值
            mMaxSameLogCountField = CreateIntConfigRow(configInfo, "重复日志阈值", KitLogger.MaxSameLogCount, value =>
            {
                KitLogger.MaxSameLogCount = Mathf.Max(1, value);
            });

            // 保留天数
            mMaxRetentionDaysField = CreateIntConfigRow(configInfo, "保留天数", KitLogger.MaxRetentionDays, value =>
            {
                KitLogger.MaxRetentionDays = Mathf.Max(1, value);
            });

            // 单文件上限 (MB)
            mMaxFileMBField = CreateIntConfigRow(configInfo, "单文件上限 (MB)", (int)(KitLogger.MaxFileBytes / 1024 / 1024), value =>
            {
                KitLogger.MaxFileBytes = Mathf.Max(1, value) * 1024L * 1024L;
            });

            body.Add(configInfo);

            return card;
        }

        private IntegerField CreateIntConfigRow(VisualElement parent, string label, int value, System.Action<int> onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var labelElement = new Label(label);
            labelElement.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            labelElement.style.fontSize = 12;
            labelElement.style.flexGrow = 1;
            row.Add(labelElement);

            var field = new IntegerField();
            field.value = value;
            field.style.width = 80;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(field);

            parent.Add(row);
            return field;
        }

        private VisualElement CreateFileStatusCard()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.style.marginBottom = 16;

            var header = new VisualElement();
            header.AddToClassList("card-header");
            var title = new Label("📄 日志文件");
            title.AddToClassList("card-title");
            header.Add(title);
            card.Add(header);

            var body = new VisualElement();
            body.AddToClassList("card-body");
            card.Add(body);

            mEditorLogLabel = CreateInfoRow(body, "editor.log");
            mPlayerLogLabel = CreateInfoRow(body, "player.log");

            return card;
        }

        private Label CreateInfoRow(VisualElement parent, string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");

            var label = new Label(labelText);
            label.AddToClassList("info-label");
            row.Add(label);

            var value = new Label("-");
            value.AddToClassList("info-value");
            row.Add(value);

            parent.Add(row);
            return value;
        }

        private void RefreshStatus()
        {
            string logDir = KitLoggerWriter.LogDirectory;
            mLogDirLabel.text = logDir;

            // 更新 Toggle 状态
            mSaveLogEditorToggle.SetValueWithoutNotify(KitLogger.SaveLogInEditor);
            mSaveLogPlayerToggle.SetValueWithoutNotify(KitLogger.SaveLogInPlayer);
            mEnableIMGUIPlayerToggle.SetValueWithoutNotify(KitLogger.EnableIMGUIInPlayer);
            mEncryptionToggle.SetValueWithoutNotify(KitLogger.EnableEncryption);

            // 更新配置字段
            mMaxQueueSizeField.SetValueWithoutNotify(KitLogger.MaxQueueSize);
            mMaxSameLogCountField.SetValueWithoutNotify(KitLogger.MaxSameLogCount);
            mMaxRetentionDaysField.SetValueWithoutNotify(KitLogger.MaxRetentionDays);
            mMaxFileMBField.SetValueWithoutNotify((int)(KitLogger.MaxFileBytes / 1024 / 1024));

            // 检查日志文件状态
            string editorLog = Path.Combine(logDir, "editor.log");
            string playerLog = Path.Combine(logDir, "player.log");

            mEditorLogLabel.text = GetFileStatus(editorLog);
            mPlayerLogLabel.text = GetFileStatus(playerLog);
        }

        private string GetFileStatus(string filePath)
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

        private string FormatFileSize(long bytes)
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
            RefreshStatus();
        }
    }
}
#endif
