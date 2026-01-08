#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 多目标输出配置
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 输出目标数据结构

        /// <summary>
        /// 额外输出目标配置
        /// </summary>
        [Serializable]
        private class ExtraOutputTarget
        {
            /// <summary>目标名称</summary>
            public string name = "服务端";
            /// <summary>导出目标（client/server/all），决定导出哪些字段分组</summary>
            public string target = "server";
            /// <summary>数据格式（bin/json/lua）</summary>
            public string dataTarget = "json";
            /// <summary>数据输出目录</summary>
            public string dataDir = "";
            /// <summary>代码生成器类型</summary>
            public string codeTarget = "java-json";
            /// <summary>代码输出目录</summary>
            public string codeDir = "";
            /// <summary>是否启用</summary>
            public bool enabled = true;
        }

        #endregion

        #region 多目标输出字段

        private List<ExtraOutputTarget> mExtraOutputTargets = new();
        private VisualElement mExtraOutputContainer;
        private const string PREF_EXTRA_OUTPUT_TARGETS = "TableKit_ExtraOutputTargets";

        // 所有可用的代码生成器选项（包含客户端和服务端）
        private static readonly string[] ALL_CODE_TARGET_OPTIONS =
        {
            // Unity 客户端
            "cs-bin",
            "cs-simple-json",
            "cs-newtonsoft-json",
            // .NET 服务端
            "cs-dotnet-json",
            // Java
            "java-bin",
            "java-json",
            // Go
            "go-bin",
            "go-json",
            // Python
            "python-json",
            // C++
            "cpp-bin",
            // Rust
            "rust-bin",
            "rust-json",
            // Lua
            "lua-lua",
            "lua-bin",
            // TypeScript
            "typescript-bin",
            "typescript-json"
        };

        #endregion

        #region 多目标输出持久化

        private void LoadExtraOutputTargets()
        {
            var json = EditorPrefs.GetString(PREF_EXTRA_OUTPUT_TARGETS, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ExtraOutputTargetListWrapper>(json);
                    mExtraOutputTargets = wrapper?.targets ?? new List<ExtraOutputTarget>();
                }
                catch
                {
                    mExtraOutputTargets = new List<ExtraOutputTarget>();
                }
            }
            else
            {
                mExtraOutputTargets = new List<ExtraOutputTarget>();
            }
        }

        private void SaveExtraOutputTargets()
        {
            var wrapper = new ExtraOutputTargetListWrapper { targets = mExtraOutputTargets };
            var json = JsonUtility.ToJson(wrapper);
            EditorPrefs.SetString(PREF_EXTRA_OUTPUT_TARGETS, json);
        }

        [Serializable]
        private class ExtraOutputTargetListWrapper
        {
            public List<ExtraOutputTarget> targets = new();
        }

        #endregion

        #region 多目标输出 UI 构建

        /// <summary>
        /// 构建多目标输出区块
        /// </summary>
        private VisualElement BuildExtraOutputSection(VisualElement container)
        {
            var section = CreateSubSection("额外输出目标");
            container.Add(section);

            // 说明
            var hint = new Label("可添加多个输出目标，每个目标可独立选择导出字段分组");
            hint.style.fontSize = Design.FontSizeSmall;
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 4;
            section.Add(hint);

            // 分组运行提示
            var groupHint = new Label("提示: 不同导出目标会分批运行 Luban，确保字段正确导出");
            groupHint.style.fontSize = Design.FontSizeSmall;
            groupHint.style.color = new StyleColor(Design.BrandWarning);
            groupHint.style.marginTop = 2;
            section.Add(groupHint);

            // 输出目标列表容器
            mExtraOutputContainer = new VisualElement();
            mExtraOutputContainer.style.marginTop = 8;
            section.Add(mExtraOutputContainer);

            // 添加按钮
            var addBtn = new Button(AddExtraOutputTarget) { text = "+ 添加输出目标" };
            addBtn.style.marginTop = 8;
            addBtn.style.alignSelf = Align.FlexStart;
            ApplySmallButtonStyle(addBtn);
            section.Add(addBtn);

            // 刷新列表
            RefreshExtraOutputList();

            return section;
        }

        /// <summary>
        /// 刷新额外输出目标列表
        /// </summary>
        private void RefreshExtraOutputList()
        {
            mExtraOutputContainer.Clear();

            for (int i = 0; i < mExtraOutputTargets.Count; i++)
            {
                var target = mExtraOutputTargets[i];
                var index = i;
                mExtraOutputContainer.Add(BuildExtraOutputTargetItem(target, index));
            }
        }

        /// <summary>
        /// 构建单个输出目标项
        /// </summary>
        private VisualElement BuildExtraOutputTargetItem(ExtraOutputTarget target, int index)
        {
            var item = new VisualElement();
            item.style.backgroundColor = new StyleColor(Design.LayerElevated);
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 6;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;
            item.style.marginLeft = 8;
            item.style.marginRight = 8;
            item.style.marginBottom = 8;

            // 标题行：名称 + 导出目标 + 启用开关 + 删除按钮
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            item.Add(headerRow);

            var leftHeader = new VisualElement();
            leftHeader.style.flexDirection = FlexDirection.Row;
            leftHeader.style.alignItems = Align.Center;
            headerRow.Add(leftHeader);            // 名称输入
            var nameField = new TextField();
            nameField.style.width = 80;
            nameField.value = target.name;
            nameField.RegisterValueChangedCallback(evt =>
            {
                target.name = evt.newValue;
                SaveExtraOutputTargets();
            });
            leftHeader.Add(nameField);

            // 导出目标下拉（决定字段分组）
            var targetDropdown = new DropdownField(new List<string>(TARGET_OPTIONS), 0);
            targetDropdown.style.width = 70;
            targetDropdown.style.marginLeft = 8;
            targetDropdown.value = target.target;
            targetDropdown.tooltip = "决定导出哪些字段分组（client=客户端字段, server=服务端字段, all=全部）";
            targetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.target = evt.newValue;
                SaveExtraOutputTargets();
            });
            leftHeader.Add(targetDropdown);

            // 启用开关
            var enableToggle = CreateCapsuleToggle("启用", target.enabled, v =>
            {
                target.enabled = v;
                SaveExtraOutputTargets();
            });
            enableToggle.style.marginLeft = 8;
            leftHeader.Add(enableToggle);

            // 删除按钮
            var deleteBtn = new Button(() =>
            {
                mExtraOutputTargets.RemoveAt(index);
                SaveExtraOutputTargets();
                RefreshExtraOutputList();
            }) { text = "×" };
            deleteBtn.style.width = 24;
            deleteBtn.style.height = 24;
            deleteBtn.style.backgroundColor = new StyleColor(Color.clear);
            deleteBtn.style.color = new StyleColor(Design.TextTertiary);
            headerRow.Add(deleteBtn);

            // 单独生成按钮
            var generateBtn = new Button(() => GenerateSingleTarget(index)) { text = "生成" };
            generateBtn.style.marginLeft = 4;
            generateBtn.style.height = 22;
            generateBtn.style.paddingLeft = 8;
            generateBtn.style.paddingRight = 8;
            generateBtn.style.backgroundColor = new StyleColor(Design.BrandPrimary);
            generateBtn.style.color = new StyleColor(Color.white);
            generateBtn.style.borderTopLeftRadius = generateBtn.style.borderTopRightRadius = 3;
            generateBtn.style.borderBottomLeftRadius = generateBtn.style.borderBottomRightRadius = 3;
            generateBtn.tooltip = "仅生成此目标的数据和代码";
            headerRow.Add(generateBtn);

            // 数据配置行
            var dataRow = new VisualElement();
            dataRow.style.flexDirection = FlexDirection.Row;
            dataRow.style.alignItems = Align.Center;
            dataRow.style.marginTop = 8;
            item.Add(dataRow);

            var dataLabel = new Label("数据:");
            dataLabel.style.width = 40;
            dataLabel.style.color = new StyleColor(Design.TextSecondary);
            dataRow.Add(dataLabel);

            var dataTargetDropdown = new DropdownField(new List<string>(DATA_TARGET_OPTIONS), 0);
            dataTargetDropdown.style.width = 70;
            dataTargetDropdown.value = target.dataTarget;
            dataRow.Add(dataTargetDropdown);

            var dataDirField = new TextField();
            dataDirField.style.flexGrow = 1;
            dataDirField.style.marginLeft = 8;
            dataDirField.value = target.dataDir;
            dataDirField.RegisterValueChangedCallback(evt =>
            {
                target.dataDir = evt.newValue;
                SaveExtraOutputTargets();
            });
            dataRow.Add(dataDirField);

            var dataBrowseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择数据输出目录", target.dataDir, "");
                if (!string.IsNullOrEmpty(path))
                {
                    target.dataDir = path;
                    dataDirField.value = path;
                    SaveExtraOutputTargets();
                }
            }) { text = "..." };
            dataBrowseBtn.style.width = 24;
            dataBrowseBtn.style.marginLeft = 4;
            dataRow.Add(dataBrowseBtn);

            // 快速打开数据目录按钮
            var dataOpenBtn = CreateOpenFolderButton(() => dataDirField.value);
            dataRow.Add(dataOpenBtn);

            // 代码配置行
            var codeRow = new VisualElement();
            codeRow.style.flexDirection = FlexDirection.Row;
            codeRow.style.alignItems = Align.Center;
            codeRow.style.marginTop = 4;
            item.Add(codeRow);

            var codeLabel = new Label("代码:");
            codeLabel.style.width = 40;
            codeLabel.style.color = new StyleColor(Design.TextSecondary);
            codeRow.Add(codeLabel);

            var codeTargetDropdown = new DropdownField(new List<string>(ALL_CODE_TARGET_OPTIONS), 0);
            codeTargetDropdown.style.width = 130;
            codeTargetDropdown.value = target.codeTarget;
            codeRow.Add(codeTargetDropdown);

            // 数据格式改变时，自动同步代码类型
            dataTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.dataTarget = evt.newValue;
                // bin -> 匹配 -bin 后缀的代码类型，json -> 匹配 -json/-lua 后缀
                var newCodeTarget = GetMatchingCodeTarget(target.codeTarget, evt.newValue);
                if (newCodeTarget != target.codeTarget)
                {
                    target.codeTarget = newCodeTarget;
                    codeTargetDropdown.SetValueWithoutNotify(newCodeTarget);
                }
                SaveExtraOutputTargets();
            });

            // 代码类型改变时，自动同步数据格式
            codeTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.codeTarget = evt.newValue;
                // -bin 后缀 -> bin，其他 -> json
                var newDataTarget = GetMatchingDataTarget(evt.newValue);
                if (newDataTarget != target.dataTarget)
                {
                    target.dataTarget = newDataTarget;
                    dataTargetDropdown.SetValueWithoutNotify(newDataTarget);
                }
                SaveExtraOutputTargets();
            });

            var codeDirField = new TextField();
            codeDirField.style.flexGrow = 1;
            codeDirField.style.marginLeft = 8;
            codeDirField.value = target.codeDir;
            codeDirField.RegisterValueChangedCallback(evt =>
            {
                target.codeDir = evt.newValue;
                SaveExtraOutputTargets();
            });
            codeRow.Add(codeDirField);

            var codeBrowseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择代码输出目录", target.codeDir, "");
                if (!string.IsNullOrEmpty(path))
                {
                    target.codeDir = path;
                    codeDirField.value = path;
                    SaveExtraOutputTargets();
                }
            }) { text = "..." };
            codeBrowseBtn.style.width = 24;
            codeBrowseBtn.style.marginLeft = 4;
            codeRow.Add(codeBrowseBtn);

            // 快速打开代码目录按钮
            var codeOpenBtn = CreateOpenFolderButton(() => codeDirField.value);
            codeRow.Add(codeOpenBtn);

            return item;
        }

        /// <summary>
        /// 添加新的输出目标
        /// </summary>
        private void AddExtraOutputTarget()
        {
            mExtraOutputTargets.Add(new ExtraOutputTarget
            {
                name = $"目标{mExtraOutputTargets.Count + 1}",
                target = "server",
                dataTarget = "json",
                dataDir = "",
                codeTarget = "java-json",
                codeDir = "",
                enabled = true
            });
            SaveExtraOutputTargets();
            RefreshExtraOutputList();
        }

        #endregion

        #region 数据格式与代码类型同步

        /// <summary>
        /// 根据数据格式获取匹配的代码类型
        /// </summary>
        /// <param name="currentCodeTarget">当前代码类型</param>
        /// <param name="dataTarget">目标数据格式</param>
        /// <returns>匹配的代码类型</returns>
        private static string GetMatchingCodeTarget(string currentCodeTarget, string dataTarget)
        {
            if (string.IsNullOrEmpty(currentCodeTarget))
            {
                return dataTarget switch
                {
                    "bin" => "java-bin",
                    "lua" => "lua-lua",
                    _ => "java-json"
                };
            }

            // 获取代码类型的前缀（如 java-json -> java）
            var dashIndex = currentCodeTarget.LastIndexOf('-');
            if (dashIndex <= 0) return currentCodeTarget;

            var prefix = currentCodeTarget.Substring(0, dashIndex);
            
            // 根据数据格式确定目标后缀
            string targetSuffix;
            if (dataTarget == "bin")
            {
                targetSuffix = "-bin";
            }
            else if (dataTarget == "lua")
            {
                // lua 数据格式只能用 lua-lua 或 lua-bin
                if (prefix == "lua")
                    targetSuffix = "-lua";
                else
                    return "lua-lua"; // 切换到 lua 数据时，直接切换到 lua-lua
            }
            else // json
            {
                // lua 代码生成器特殊处理
                targetSuffix = prefix == "lua" ? "-lua" : "-json";
            }

            var newCodeTarget = prefix + targetSuffix;

            // 检查新代码类型是否在可用选项中
            foreach (var option in ALL_CODE_TARGET_OPTIONS)
            {
                if (option == newCodeTarget) return newCodeTarget;
            }

            // 如果没有匹配的，返回原值
            return currentCodeTarget;
        }

        /// <summary>
        /// 根据代码类型获取匹配的数据格式
        /// </summary>
        /// <param name="codeTarget">代码类型</param>
        /// <returns>匹配的数据格式</returns>
        private static string GetMatchingDataTarget(string codeTarget)
        {
            if (string.IsNullOrEmpty(codeTarget)) return "json";

            // -bin 后缀对应 bin 数据格式
            if (codeTarget.EndsWith("-bin"))
                return "bin";
            
            // lua-lua 对应 lua 数据格式
            if (codeTarget == "lua-lua")
                return "lua";

            // 其他对应 json
            return "json";
        }

        #endregion
    }
}
#endif
