#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SimpleJSON;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - Luban 生成与数据预览
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region Luban 生成

        private void GenerateLuban() => ExecuteLuban(false);
        private void ValidateLuban() => ExecuteLuban(true);

        private void ExecuteLuban(bool validateOnly)
        {
            if (!ValidateLubanConfig()) return;

            mGenerateBtn.SetEnabled(false);
            UpdateStatusBanner(BuildStatus.Building);

            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 开始{(validateOnly ? "验证" : "生成")}...");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);
            var dllPath = Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath);

            try
            {
                var args = BuildLubanArgs(validateOnly);
                logBuilder.AppendLine($"命令: dotnet {dllPath}");
                logBuilder.AppendLine($"参数: {args}");
                logBuilder.AppendLine("───────────────────────────────");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{dllPath}\" {args}",
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) errorBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                var exitCode = process.ExitCode;
                logBuilder.AppendLine(outputBuilder.ToString());

                if (!string.IsNullOrEmpty(errorBuilder.ToString()))
                {
                    logBuilder.AppendLine("[错误输出]");
                    logBuilder.AppendLine(errorBuilder.ToString());
                }

                logBuilder.AppendLine("───────────────────────────────");
                logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 退出码: {exitCode}");

                if (exitCode == 0)
                {
                    UpdateStatusBanner(BuildStatus.Success);
                    if (validateOnly)
                    {
                        LoadDataPreview(Path.Combine(projectRoot, "Temp/LubanValidate"), logBuilder);
                    }
                    else
                    {
                        EnsureRequiredFiles(logBuilder);
                        AssetDatabase.Refresh();
                        logBuilder.AppendLine("✓ 已刷新 Unity 资源数据库");
                    }
                }
                else
                {
                    UpdateStatusBanner(BuildStatus.Failed);
                }
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"[异常] {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                UpdateStatusBanner(BuildStatus.Failed);
                Debug.LogException(ex);
            }
            finally
            {
                mGenerateBtn.SetEnabled(true);
                mLogContent.text = logBuilder.ToString();
            }
        }

        private string BuildLubanArgs(bool validateOnly)
        {
            var sb = new StringBuilder();
            sb.Append($"-t {mTarget} ");
            sb.Append($"-d {(validateOnly ? "json" : mDataTarget)} ");
            sb.Append("--conf luban.conf ");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            if (validateOnly)
            {
                sb.Append($"-x outputDataDir=\"{Path.Combine(projectRoot, "Temp/LubanValidate")}\" ");
            }
            else
            {
                sb.Append($"-c {mCodeTarget} ");

                var dataDir = mOutputDataDir.StartsWith("Assets/")
                    ? Path.Combine(projectRoot, mOutputDataDir.TrimEnd('/'))
                    : mOutputDataDir;
                var codeDir = mOutputCodeDir.StartsWith("Assets/")
                    ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/'))
                    : mOutputCodeDir;

                sb.Append($"-x outputDataDir=\"{dataDir}\" ");
                sb.Append($"-x outputCodeDir=\"{Path.Combine(codeDir, "Luban")}\" ");
            }

            return sb.ToString();
        }

        private bool ValidateLubanConfig()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);

            if (string.IsNullOrEmpty(mLubanWorkDir) || !Directory.Exists(workDir))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban 工作目录不存在\n路径: {workDir}", "确定");
                return false;
            }

            if (!File.Exists(Path.Combine(workDir, "luban.conf")))
            {
                EditorUtility.DisplayDialog("配置错误", $"找不到 luban.conf 文件\n路径: {Path.Combine(workDir, "luban.conf")}", "确定");
                return false;
            }

            var dllPath = Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath);
            if (string.IsNullOrEmpty(mLubanDllPath) || !File.Exists(dllPath))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban.dll 路径无效\n路径: {dllPath}", "确定");
                return false;
            }

            return true;
        }

        private void OpenLubanFolder()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);

            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                EditorUtility.RevealInFinder(workDir);
            else
                EditorUtility.DisplayDialog("提示", $"Luban 工作目录未配置或不存在\n路径: {workDir}", "确定");
        }

        private void EnsureRequiredFiles(StringBuilder logBuilder)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var codeDir = mOutputCodeDir.StartsWith("Assets/")
                ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/'))
                : mOutputCodeDir;

            var lubanCodeDir = Path.Combine(codeDir, "Luban");
            if (!Directory.Exists(lubanCodeDir)) Directory.CreateDirectory(lubanCodeDir);

            logBuilder.AppendLine("正在生成 TableKit 运行时代码...");
            TableKitCodeGenerator.Generate(codeDir, mUseAssemblyDefinition, mGenerateExternalTypeUtil, mAssemblyName, "cfg", mRuntimePathPattern, mEditorDataPath, mCodeTarget);
            logBuilder.AppendLine("✓ TableKit 运行时代码生成完成");

            if (mGenerateExternalTypeUtil) logBuilder.AppendLine("✓ 已生成 ExternalTypeUtil.cs");
        }

        #endregion

        #region 数据预览

        private string[] mCachedJsonFiles;

        private void LoadDataPreview(string dataDir, StringBuilder logBuilder)
        {
            mDataPreviewContainer.Clear();
            mCurrentPreviewJsonPath = null;
            mCachedJsonFiles = null;

            if (!Directory.Exists(dataDir))
            {
                AddPreviewHint("验证数据目录不存在", Design.BrandDanger);
                return;
            }

            var jsonFiles = Directory.GetFiles(dataDir, "*.json");
            if (jsonFiles.Length == 0)
            {
                AddPreviewHint("没有找到 JSON 数据文件", Design.BrandDanger);
                return;
            }

            mCachedJsonFiles = jsonFiles;
            logBuilder.AppendLine($"✓ 找到 {jsonFiles.Length} 个数据文件");

            var fileNames = new List<string>();
            foreach (var file in jsonFiles) fileNames.Add(Path.GetFileNameWithoutExtension(file));

            // 选择下拉
            var selectRow = new VisualElement();
            selectRow.style.flexDirection = FlexDirection.Row;
            selectRow.style.alignItems = Align.Center;
            selectRow.style.marginTop = 8;
            mDataPreviewContainer.Add(selectRow);

            var selectLabel = new Label("选择配置表:");
            selectLabel.style.width = 80;
            selectLabel.style.color = new StyleColor(Design.TextSecondary);
            selectRow.Add(selectLabel);

            var dropdown = new DropdownField(fileNames, 0);
            dropdown.style.flexGrow = 1;
            selectRow.Add(dropdown);

            // 树形容器
            mDataPreviewTreeContainer = new ScrollView { name = "tree-container" };
            mDataPreviewTreeContainer.style.marginTop = 8;
            mDataPreviewTreeContainer.style.maxHeight = 300;
            mDataPreviewTreeContainer.style.backgroundColor = new StyleColor(Design.LayerConsole);
            mDataPreviewTreeContainer.style.borderTopLeftRadius = mDataPreviewTreeContainer.style.borderTopRightRadius = 4;
            mDataPreviewTreeContainer.style.borderBottomLeftRadius = mDataPreviewTreeContainer.style.borderBottomRightRadius = 4;
            mDataPreviewContainer.Add(mDataPreviewTreeContainer);

            mCurrentPreviewJsonPath = jsonFiles[0];
            LoadJsonToTreeWithSearch(jsonFiles[0], mDataPreviewTreeContainer, mDataPreviewSearchText);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = fileNames.IndexOf(evt.newValue);
                if (idx >= 0 && idx < jsonFiles.Length)
                {
                    mCurrentPreviewJsonPath = jsonFiles[idx];
                    LoadJsonToTreeWithSearch(jsonFiles[idx], mDataPreviewTreeContainer, mDataPreviewSearchText);
                }
            });
        }

        /// <summary>
        /// 刷新数据预览（带搜索）
        /// </summary>
        private void RefreshDataPreviewWithSearch()
        {
            if (mDataPreviewTreeContainer == null || string.IsNullOrEmpty(mCurrentPreviewJsonPath)) return;
            LoadJsonToTreeWithSearch(mCurrentPreviewJsonPath, mDataPreviewTreeContainer, mDataPreviewSearchText);
        }

        private void AddPreviewHint(string message, Color color)
        {
            var hint = new Label(message);
            hint.style.color = new StyleColor(color);
            hint.style.marginTop = 8;
            mDataPreviewContainer.Add(hint);
        }

        private void LoadJsonToTree(string jsonPath, ScrollView container)
        {
            LoadJsonToTreeWithSearch(jsonPath, container, "");
        }

        private void LoadJsonToTreeWithSearch(string jsonPath, ScrollView container, string searchText)
        {
            container.Clear();
            mSearchMatchCount = 0;
            mFirstMatchElement = null;

            try
            {
                var json = JSON.Parse(File.ReadAllText(jsonPath));
                if (json == null)
                {
                    AddTreeError(container, "JSON 解析失败");
                    UpdateMatchLabel(0);
                    return;
                }

                var lowerSearch = string.IsNullOrEmpty(searchText) ? "" : searchText.ToLowerInvariant();
                BuildJsonTreeWithSearch(json, container, 0, Path.GetFileNameWithoutExtension(jsonPath), lowerSearch);

                UpdateMatchLabel(mSearchMatchCount);

                // 滚动到第一个匹配项
                if (mFirstMatchElement != null && !string.IsNullOrEmpty(searchText))
                {
                    container.schedule.Execute(() =>
                    {
                        mFirstMatchElement.schedule.Execute(() =>
                        {
                            container.ScrollTo(mFirstMatchElement);
                        });
                    }).ExecuteLater(50);
                }
            }
            catch (Exception ex)
            {
                AddTreeError(container, $"加载失败: {ex.Message}");
                UpdateMatchLabel(0);
            }
        }

        private int mSearchMatchCount;
        private VisualElement mFirstMatchElement;

        private void UpdateMatchLabel(int count)
        {
            if (mDataPreviewMatchLabel == null) return;

            if (string.IsNullOrEmpty(mDataPreviewSearchText))
            {
                mDataPreviewMatchLabel.style.display = DisplayStyle.None;
            }
            else
            {
                mDataPreviewMatchLabel.style.display = DisplayStyle.Flex;
                mDataPreviewMatchLabel.text = count > 0 ? $"找到 {count} 处匹配" : "无匹配";
                mDataPreviewMatchLabel.style.color = new StyleColor(count > 0 ? Design.BrandSuccess : Design.BrandWarning);
            }
        }

        private void AddTreeError(VisualElement container, string message)
        {
            var label = new Label(message);
            label.style.color = new StyleColor(Design.BrandDanger);
            label.style.paddingLeft = 8;
            label.style.paddingTop = 4;
            container.Add(label);
        }

        private void BuildJsonTree(JSONNode node, VisualElement parent, int depth, string key = null)
        {
            BuildJsonTreeWithSearch(node, parent, depth, key, "");
        }

        private bool BuildJsonTreeWithSearch(JSONNode node, VisualElement parent, int depth, string key, string searchText)
        {
            var indent = depth * 16;
            var hasMatch = false;

            if (node.IsArray)
            {
                var foldout = new Foldout { text = string.IsNullOrEmpty(key) ? $"Array [{node.Count}]" : $"{key} [{node.Count}]", value = depth < 1 };
                foldout.style.marginLeft = indent;

                // 检查 key 是否匹配
                if (!string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText))
                {
                    hasMatch = true;
                    HighlightFoldout(foldout);
                }

                parent.Add(foldout);

                int idx = 0;
                foreach (var item in node.Children)
                {
                    var childMatch = BuildJsonTreeWithSearch(item, foldout, depth + 1, $"[{idx}]", searchText);
                    if (childMatch)
                    {
                        hasMatch = true;
                        foldout.value = true; // 展开包含匹配项的节点
                    }

                    if (++idx >= 50)
                    {
                        var more = new Label($"... 还有 {node.Count - 50} 项");
                        more.style.color = new StyleColor(Design.TextTertiary);
                        more.style.marginLeft = (depth + 1) * 16;
                        foldout.Add(more);
                        break;
                    }
                }
            }
            else if (node.IsObject)
            {
                var foldout = new Foldout { text = string.IsNullOrEmpty(key) ? "Object" : key, value = depth < 2 };
                foldout.style.marginLeft = indent;

                // 检查 key 是否匹配
                if (!string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText))
                {
                    hasMatch = true;
                    HighlightFoldout(foldout);
                }

                parent.Add(foldout);

                foreach (var kvp in node.AsObject)
                {
                    var childMatch = BuildJsonTreeWithSearch(kvp.Value, foldout, depth + 1, kvp.Key, searchText);
                    if (childMatch)
                    {
                        hasMatch = true;
                        foldout.value = true; // 展开包含匹配项的节点
                    }
                }
            }
            else
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginLeft = indent;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                parent.Add(row);

                var keyMatched = !string.IsNullOrEmpty(searchText) && !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(searchText);
                var valueMatched = !string.IsNullOrEmpty(searchText) && node.Value.ToLowerInvariant().Contains(searchText);

                if (keyMatched || valueMatched)
                {
                    hasMatch = true;
                    mSearchMatchCount++;

                    // 高亮整行背景
                    row.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0f, 0.15f));
                    row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 2;
                    row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 2;
                    row.style.paddingLeft = 4;
                    row.style.marginLeft = indent - 4;

                    if (mFirstMatchElement == null)
                    {
                        mFirstMatchElement = row;
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    var keyLabel = new Label($"{key}: ");
                    keyLabel.style.color = keyMatched
                        ? new StyleColor(new Color(1f, 0.6f, 0f)) // 高亮橙色
                        : new StyleColor(Design.BrandPrimary);
                    if (keyMatched) keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    row.Add(keyLabel);
                }

                var valueLabel = new Label(node.Value);
                if (valueMatched)
                {
                    valueLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0f)); // 高亮橙色
                    valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    valueLabel.style.color = GetValueColor(node);
                }
                row.Add(valueLabel);
            }

            return hasMatch;
        }

        private void HighlightFoldout(Foldout foldout)
        {
            mSearchMatchCount++;
            foldout.style.backgroundColor = new StyleColor(new Color(1f, 0.8f, 0f, 0.1f));
            foldout.style.borderTopLeftRadius = foldout.style.borderTopRightRadius = 2;
            foldout.style.borderBottomLeftRadius = foldout.style.borderBottomRightRadius = 2;

            if (mFirstMatchElement == null)
            {
                mFirstMatchElement = foldout;
            }
        }

        private StyleColor GetValueColor(JSONNode node)
        {
            if (node.IsNumber) return new StyleColor(Design.BrandSuccess);
            if (node.IsBoolean) return new StyleColor(Design.BrandDanger);
            if (node.IsNull) return new StyleColor(Design.TextTertiary);
            return new StyleColor(Design.BrandWarning);
        }

        #endregion

        #region TableKit 操作

        private void RefreshEditorCache()
        {
            var tablesType = FindTablesType();
            if (tablesType == null)
            {
                EditorUtility.DisplayDialog("TableKit", "cfg.Tables 类型不存在，请先生成配置表代码", "确定");
                return;
            }

            try
            {
                var tableKitType = FindTableKitType();
                if (tableKitType == null)
                {
                    EditorUtility.DisplayDialog("TableKit", "TableKit 类型不存在，请先生成配置表代码", "确定");
                    return;
                }

                tableKitType.GetMethod("SetEditorDataPath")?.Invoke(null, new object[] { mEditorDataPath });
                tableKitType.GetMethod("RefreshEditor")?.Invoke(null, null);

                var tables = tableKitType.GetProperty("TablesEditor")?.GetValue(null);
                RefreshTablesInfo(tables);

                mLogContent.text = $"[{DateTime.Now:HH:mm:ss}] ✓ 编辑器缓存已刷新";
                UpdateStatusBanner(BuildStatus.Success);
            }
            catch (Exception ex)
            {
                mLogContent.text = $"[{DateTime.Now:HH:mm:ss}] ✗ 加载配置表失败:\n{ex.Message}";
                UpdateStatusBanner(BuildStatus.Failed);
            }
        }

        private void RefreshTablesInfo(object tables)
        {
            mCachedTables = tables;
            RefreshTablesInfoInternal(tables, mTablesSearchText);
        }

        private void RefreshTablesInfoInternal(object tables, string searchText)
        {
            mTablesInfoContainer.Clear();

            if (tables == null)
            {
                var hint = new Label("配置表未加载");
                hint.style.color = new StyleColor(Design.BrandDanger);
                hint.style.marginTop = 8;
                mTablesInfoContainer.Add(hint);
                return;
            }

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 250;
            mTablesInfoContainer.Add(scrollView);

            var properties = tables.GetType().GetProperties();
            var matchCount = 0;
            var totalCount = 0;

            foreach (var prop in properties)
            {
                if (prop.PropertyType.Namespace != "cfg") continue;
                totalCount++;

                // 搜索过滤：匹配属性名或类型名（不区分大小写）
                if (!string.IsNullOrEmpty(searchText))
                {
                    var lowerSearch = searchText.ToLowerInvariant();
                    var matchName = prop.Name.ToLowerInvariant().Contains(lowerSearch);
                    var matchType = prop.PropertyType.Name.ToLowerInvariant().Contains(lowerSearch);
                    if (!matchName && !matchType) continue;
                }

                matchCount++;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                var nameLabel = new Label($"• {prop.Name}");
                nameLabel.style.width = 150;
                nameLabel.style.color = new StyleColor(Design.TextPrimary);
                row.Add(nameLabel);

                var typeLabel = new Label(prop.PropertyType.Name);
                typeLabel.style.color = new StyleColor(Design.BrandSuccess);
                row.Add(typeLabel);

                scrollView.Add(row);
            }

            // 显示统计信息
            var statsLabel = new Label();
            statsLabel.style.marginTop = 8;
            statsLabel.style.fontSize = Design.FontSizeSmall;
            statsLabel.style.color = new StyleColor(Design.TextTertiary);
            
            if (string.IsNullOrEmpty(searchText))
            {
                statsLabel.text = $"共 {totalCount} 个配置表";
            }
            else
            {
                statsLabel.text = $"找到 {matchCount}/{totalCount} 个匹配项";
            }
            mTablesInfoContainer.Add(statsLabel);

            if (matchCount == 0 && !string.IsNullOrEmpty(searchText))
            {
                var noResult = new Label("没有找到匹配的配置表");
                noResult.style.color = new StyleColor(Design.BrandWarning);
                noResult.style.marginTop = 8;
                scrollView.Add(noResult);
            }
        }

        private Type FindTablesType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("cfg.Tables");
                if (type != null) return type;
            }
            return null;
        }

        private Type FindTableKitType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("TableKit");
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
#endif
