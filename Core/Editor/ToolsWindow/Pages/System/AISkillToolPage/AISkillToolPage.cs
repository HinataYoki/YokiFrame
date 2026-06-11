#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    [YokiToolPage(
        kit: "System",
        name: "AI Skill",
        icon: KitIcons.CODE,
        priority: 5,
        category: YokiPageCategory.System)]
    public sealed class AISkillToolPage : YokiToolPageBase
    {
        private const string SKILL_FILE = "SKILL.md";

        private static readonly (string skillName, string srcPath, string label)[] sSkills =
        {
            ("yokiframe",         "Assets/YokiFrame/Core/Editor/Skills/yokiframe/SKILL.md",         "框架 Skill"),
            ("yokiframe-editor",  "Assets/YokiFrame/Core/Editor/Skills/yokiframe-editor/SKILL.md",   "编辑器 Skill"),
        };

        private static readonly (string name, string dir, string icon)[] sTools =
        {
            ("Claude Code",     ".claude/skills",   KitIcons.CATEGORY_CORE),
            ("Codex CLI",       ".codex/skills",     KitIcons.CODE),
            ("Cursor",          ".cursor/skills",    KitIcons.EVENT),
            ("Windsurf",        ".windsurf/skills",  KitIcons.SCROLL),
            ("GitHub Copilot",  ".github/skills",    KitIcons.GITHUB),
            ("AGENTS.md",       ".agents/skills",    KitIcons.DOCUMENT),
        };

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");

        // ---- 状态 ----
        private int mSelectedSkillIndex;
        private Button mBtnFramework;
        private Button mBtnEditor;

        // 动态 UI 引用
        private readonly List<(VisualElement card, VisualElement statusRow, Label statusDot, Label statusText)> mToolRows = new();
        private YokiFrameUIComponents.HudCardResult mHudInstalled;
        private YokiFrameUIComponents.HudCardResult mHudPending;
        private TextField mCustomPathField;
        private VisualElement mCustomStatusRow;
        private Label mCustomDot;
        private Label mCustomStatusText;

        // ---- 便利属性 ----
        private string CurrentSkillName => sSkills[mSelectedSkillIndex].skillName;
        private string CurrentTemplateFullPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath,
                sSkills[mSelectedSkillIndex].srcPath.Replace("Assets/", ""))).Replace("\\", "/");

        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                title: "AI Skill",
                summary: "向 AI 编码工具安装 YokiFrame 框架 Skill，让 AI 准确使用全部 API 和编码规范。",
                iconId: KitIcons.CODE);

            // ---- Toolbar: Skill 选择器 + 批量按钮 ----
            BuildSkillTabs(scaffold.Toolbar);
            scaffold.Toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());
            scaffold.Toolbar.Add(CreateToolbarPrimaryButton("全部安装", InstallAll));
            scaffold.Toolbar.Add(CreateToolbarButton("全部卸载", UninstallAll));
            scaffold.Toolbar.Add(CreateToolbarButton("刷新", RefreshAllUI));

            // ---- 统计卡片行 ----
            BuildStatsRow(scaffold.Content);

            // ---- 预设工具网格 ----
            BuildToolsGrid(scaffold.Content);

            // ---- 自定义路径 ----
            BuildCustomSection(scaffold.Content);

            root.Add(scaffold.Root);
        }

        #region Skill Tabs

        private void BuildSkillTabs(VisualElement parent)
        {
            mBtnFramework = YokiFrameUIComponents.CreateFilterButton("框架 Skill", true, () => SwitchSkill(0));
            parent.Add(mBtnFramework);

            mBtnEditor = YokiFrameUIComponents.CreateFilterButton("编辑器 Skill", false, () => SwitchSkill(1));
            parent.Add(mBtnEditor);
        }

        private void SwitchSkill(int index)
        {
            if (mSelectedSkillIndex == index) return;
            mSelectedSkillIndex = index;

            YokiFrameUIComponents.SetFilterButtonActive(mBtnFramework, index == 0);
            YokiFrameUIComponents.SetFilterButtonActive(mBtnEditor, index == 1);

            RefreshAllUI();
        }

        #endregion

        #region Stats Row

        private void BuildStatsRow(VisualElement parent)
        {
            var (container, cards) = YokiFrameUIComponents.CreateHudCardRow(new[]
            {
                new YokiFrameUIComponents.HudCardConfig { Title = $"{sSkills[mSelectedSkillIndex].label} 已安装", Value = "—", AccentColor = YokiFrameUIComponents.Colors.BrandSuccess, IconId = KitIcons.CHECK },
                new YokiFrameUIComponents.HudCardConfig { Title = "待安装", Value = "—", AccentColor = YokiFrameUIComponents.Colors.TextTertiary, IconId = KitIcons.DOT_EMPTY },
            });

            mHudInstalled = cards[0];
            mHudPending = cards[1];
            parent.Add(container);
        }

        private void RefreshStats()
        {
            int installed = 0;
            for (int i = 0; i < sTools.Length; i++)
            {
                if (File.Exists(GetTargetFile(sTools[i].dir))) installed++;
            }

            mHudInstalled?.SetValue(installed.ToString(), YokiFrameUIComponents.Colors.BrandSuccess);
            mHudPending?.SetValue((sTools.Length - installed).ToString(),
                installed == sTools.Length ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary);
        }

        #endregion

        #region Tools Grid

        private void BuildToolsGrid(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel(
                title: "预设工具",
                summary: $"当前 Skill：{sSkills[mSelectedSkillIndex].label} — {sSkills[mSelectedSkillIndex].skillName}",
                iconId: KitIcons.FOLDER_TOOLS);
            panel.AddToClassList("yoki-kit-panel--slate");

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 4;

            mToolRows.Clear();
            for (int i = 0; i < sTools.Length; i++)
            {
                grid.Add(BuildToolCard(i));
            }

            body.Add(grid);
            parent.Add(panel);
        }

        private VisualElement BuildToolCard(int index)
        {
            var (name, dir, iconId) = sTools[index];

            var card = new VisualElement();
            card.AddToClassList("yoki-kit-panel");
            card.style.width = new Length(33f, LengthUnit.Percent);
            card.style.flexGrow = 1;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.minWidth = 200;

            // ---- Header ----
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 6;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);

            var icon = new Image { image = KitIcons.GetTexture(iconId) };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 8;
            icon.tintColor = YokiFrameUIComponents.Colors.TextSecondary;
            header.Add(icon);

            var title = new Label(name);
            title.style.fontSize = 13;
            title.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);
            card.Add(header);

            // ---- Body ----
            var body = new VisualElement();
            body.style.paddingTop = 6;
            body.style.paddingBottom = 6;

            var path = new Label($"{dir}/{CurrentSkillName}");
            path.style.fontSize = 10;
            path.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            path.style.marginBottom = 8;
            path.style.unityTextAlign = TextAnchor.MiddleLeft;
            path.name = $"tool-path-{index}";
            body.Add(path);

            // ---- Status row ----
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 8;

            var dot = new Label("●");
            dot.style.fontSize = 10;
            dot.style.marginRight = 4;
            dot.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusRow.Add(dot);

            var statusText = new Label();
            statusText.style.fontSize = 11;
            statusText.style.flexGrow = 1;
            statusRow.Add(statusText);

            body.Add(statusRow);

            // ---- Action buttons ----
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;

            var installBtn = new Button(() => InstallTo(index)) { text = "安装" };
            installBtn.AddToClassList("action-button");
            installBtn.AddToClassList("primary");
            installBtn.style.height = 26;
            installBtn.style.fontSize = 11;
            installBtn.style.flexGrow = 1;
            installBtn.style.marginRight = 4;
            actions.Add(installBtn);

            var uninstallBtn = new Button(() => UninstallFrom(index)) { text = "卸载" };
            uninstallBtn.AddToClassList("action-button");
            uninstallBtn.style.height = 26;
            uninstallBtn.style.fontSize = 11;
            uninstallBtn.style.flexGrow = 1;
            actions.Add(uninstallBtn);

            body.Add(actions);
            card.Add(body);

            // ---- 缓存引用 ----
            mToolRows.Add((card, statusRow, dot, statusText));

            // ---- 初始渲染 ----
            RefreshToolRow(index);

            return card;
        }

        private void RefreshToolRow(int index)
        {
            if (index < 0 || index >= mToolRows.Count) return;
            var (_, _, dot, statusText) = mToolRows[index];
            var (_, dir, _) = sTools[index];
            var installed = File.Exists(GetTargetFile(dir));

            dot.text = installed ? "●" : "○";
            dot.style.color = new StyleColor(installed ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary);
            statusText.text = installed ? "已安装" : "未安装";
            statusText.style.color = new StyleColor(installed ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary);

            // 更新 path label
            if (Root != default)
            {
                var pathLabel = Root.Q<Label>($"tool-path-{index}");
                if (pathLabel != default)
                    pathLabel.text = $"{dir}/{CurrentSkillName}";
            }
        }

        private void RefreshAllToolRows()
        {
            for (int i = 0; i < sTools.Length; i++)
                RefreshToolRow(i);
        }

        #endregion

        #region Custom Path Section

        private void BuildCustomSection(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel(
                title: "自定义路径",
                summary: "为其他 AI 工具指定安装目录（相对项目根目录），Skill 将写入 {目录}/yokiframe/SKILL.md",
                iconId: KitIcons.SETTINGS);
            panel.AddToClassList("yoki-kit-panel--blue");
            body.style.paddingBottom = 8;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var prefix = new Label($"{ProjectRoot}/");
            prefix.style.fontSize = 12;
            prefix.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            prefix.style.marginRight = 0;
            row.Add(prefix);

            mCustomPathField = new TextField();
            mCustomPathField.value = ".my-tool/skills";
            mCustomPathField.style.flexGrow = 1;
            mCustomPathField.style.marginLeft = 0;
            mCustomPathField.style.marginRight = 12;
            mCustomPathField.style.minWidth = 180;
            row.Add(mCustomPathField);

            // Status
            mCustomStatusRow = new VisualElement();
            mCustomStatusRow.style.flexDirection = FlexDirection.Row;
            mCustomStatusRow.style.alignItems = Align.Center;
            mCustomStatusRow.style.marginRight = 12;

            mCustomDot = new Label("○");
            mCustomDot.style.fontSize = 10;
            mCustomDot.style.marginRight = 4;
            mCustomStatusRow.Add(mCustomDot);

            mCustomStatusText = new Label("未安装");
            mCustomStatusText.style.fontSize = 11;
            mCustomStatusRow.Add(mCustomStatusText);

            row.Add(mCustomStatusRow);

            var installBtn = new Button(InstallCustom) { text = "安装" };
            installBtn.AddToClassList("action-button");
            installBtn.AddToClassList("primary");
            installBtn.style.height = 28;
            installBtn.style.fontSize = 12;
            installBtn.style.marginRight = 4;
            row.Add(installBtn);

            var uninstallBtn = new Button(UninstallCustom) { text = "卸载" };
            uninstallBtn.AddToClassList("action-button");
            uninstallBtn.style.height = 28;
            uninstallBtn.style.fontSize = 12;
            row.Add(uninstallBtn);

            body.Add(row);
            parent.Add(panel);

            RefreshCustomUI();
        }

        private void RefreshCustomUI()
        {
            if (mCustomDot == default) return;
            var installed = File.Exists(GetCustomTargetFile());
            mCustomDot.text = installed ? "●" : "○";
            mCustomDot.style.color = new StyleColor(installed ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary);
            mCustomStatusText.text = installed ? "已安装" : "未安装";
            mCustomStatusText.style.color = new StyleColor(installed ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary);
        }

        #endregion

        #region Path Helpers

        private string GetTargetDir(string relativeDir) =>
            Path.Combine(ProjectRoot, relativeDir, CurrentSkillName).Replace("\\", "/");

        private string GetTargetFile(string relativeDir) =>
            Path.Combine(GetTargetDir(relativeDir), SKILL_FILE).Replace("\\", "/");

        private string GetCustomDir()
        {
            var input = mCustomPathField?.value?.Trim() ?? "";
            return string.IsNullOrEmpty(input) ? ".custom/skills" : input;
        }

        private string GetCustomTargetDir() =>
            Path.Combine(ProjectRoot, GetCustomDir(), CurrentSkillName).Replace("\\", "/");

        private string GetCustomTargetFile() =>
            Path.Combine(GetCustomTargetDir(), SKILL_FILE).Replace("\\", "/");

        #endregion

        #region Install / Uninstall — 预设工具

        private void InstallTo(int index)
        {
            if (!TryReadTemplate(out var content)) return;
            var dir = sTools[index].dir;
            var targetFile = GetTargetFile(dir);

            if (File.Exists(targetFile))
            {
                if (!EditorUtility.DisplayDialog("覆盖确认",
                    $"Skill 已存在于:\n{targetFile}\n\n是否覆盖？", "覆盖", "取消"))
                    return;
            }

            Directory.CreateDirectory(GetTargetDir(dir));
            File.WriteAllText(targetFile, content);
            AssetDatabase.Refresh();
            RefreshToolRow(index);
            RefreshStats();
        }

        private void UninstallFrom(int index)
        {
            var dir = sTools[index].dir;
            var targetDir = GetTargetDir(dir);

            if (!Directory.Exists(targetDir))
            {
                RefreshToolRow(index);
                return;
            }

            if (!EditorUtility.DisplayDialog("卸载确认",
                $"确认删除？\n{targetDir}", "删除", "取消"))
                return;

            Directory.Delete(targetDir, recursive: true);
            var meta = targetDir + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
            AssetDatabase.Refresh();
            RefreshToolRow(index);
            RefreshStats();
        }

        #endregion

        #region Install / Uninstall — 自定义路径

        private void InstallCustom()
        {
            if (!TryReadTemplate(out var content)) return;
            var targetFile = GetCustomTargetFile();

            if (File.Exists(targetFile))
            {
                if (!EditorUtility.DisplayDialog("覆盖确认",
                    $"Skill 已存在于:\n{targetFile}\n\n是否覆盖？", "覆盖", "取消"))
                    return;
            }

            Directory.CreateDirectory(GetCustomTargetDir());
            File.WriteAllText(targetFile, content);
            AssetDatabase.Refresh();
            RefreshCustomUI();
        }

        private void UninstallCustom()
        {
            var targetDir = GetCustomTargetDir();
            if (!Directory.Exists(targetDir))
            {
                RefreshCustomUI();
                return;
            }

            if (!EditorUtility.DisplayDialog("卸载确认",
                $"确认删除？\n{targetDir}", "删除", "取消"))
                return;

            Directory.Delete(targetDir, recursive: true);
            var meta = targetDir + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
            AssetDatabase.Refresh();
            RefreshCustomUI();
        }

        #endregion

        #region Batch

        private void InstallAll()
        {
            if (!TryReadTemplate(out _)) return;
            var label = sSkills[mSelectedSkillIndex].label;

            if (!EditorUtility.DisplayDialog("批量安装",
                $"将为全部 {sTools.Length} 个工具安装「{label}」。", "全部安装", "取消"))
                return;

            for (int i = 0; i < sTools.Length; i++)
            {
                if (!TryReadTemplate(out var content)) continue;
                Directory.CreateDirectory(GetTargetDir(sTools[i].dir));
                File.WriteAllText(GetTargetFile(sTools[i].dir), content);
            }

            AssetDatabase.Refresh();
            RefreshAllUI();
        }

        private void UninstallAll()
        {
            var label = sSkills[mSelectedSkillIndex].label;

            if (!EditorUtility.DisplayDialog("批量卸载",
                $"将从全部 {sTools.Length} 个工具中卸载「{label}」。\n此操作不可撤销。", "全部卸载", "取消"))
                return;

            for (int i = 0; i < sTools.Length; i++)
            {
                var targetDir = GetTargetDir(sTools[i].dir);
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, recursive: true);
                    var meta = targetDir + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }
            }

            AssetDatabase.Refresh();
            RefreshAllUI();
        }

        #endregion

        #region Refresh All

        private void RefreshAllUI()
        {
            RefreshAllToolRows();
            RefreshCustomUI();
            RefreshStats();
        }

        #endregion

        #region Template Read

        private bool TryReadTemplate(out string content)
        {
            var path = CurrentTemplateFullPath;
            if (!File.Exists(path))
            {
                Debug.LogError($"[AI Skill] 模板文件不存在: {path}");
                content = null;
                return false;
            }
            content = File.ReadAllText(path);
            return true;
        }

        #endregion
    }
}
#endif
