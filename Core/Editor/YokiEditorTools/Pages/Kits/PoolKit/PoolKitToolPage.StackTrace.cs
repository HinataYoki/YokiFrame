#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - 堆栈解析与交互处理
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region 堆栈常量

        // Unity 堆栈格式: "ClassName.Method () (at Assets/path/file.cs:123)"
        private static readonly Regex sStackFrameRegex = new(
            @"^(.+?)\s*\(.*?\)\s*\(at\s+(.+?):(\d+)\)$",
            RegexOptions.Compiled);
        
        // Mono 堆栈格式: "ClassName.Method () [0x00000] in path/file.cs:123"
        private static readonly Regex sStackFrameRegexMono = new(
            @"^(.+?)\s*\(.*?\)\s*\[0x[0-9a-fA-F]+\]\s*in\s+(.+?):(\d+)$",
            RegexOptions.Compiled);
        
        // 备用格式: "at ClassName.Method() in path/file.cs:line 123"
        private static readonly Regex sStackFrameRegexAlt = new(
            @"^\s*(?:at\s+)?(.+?)\s*(?:\(.*?\))?\s*(?:in\s+(.+?):line\s+(\d+))?$",
            RegexOptions.Compiled);

        #endregion

        #region 堆栈解析

        private struct StackFrameInfo
        {
            public string MethodName;
            public string FilePath;
            public int LineNumber;
        }

        private List<StackFrameInfo> ParseStackFrames(string stackTrace)
        {
            var frames = new List<StackFrameInfo>();
            if (string.IsNullOrEmpty(stackTrace)) return frames;

            var lines = stackTrace.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (ShouldSkipStackFrame(line)) continue;

                var frame = ParseSingleFrame(line);
                if (!string.IsNullOrEmpty(frame.MethodName))
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        private StackFrameInfo ParseSingleFrame(string line)
        {
            var frame = new StackFrameInfo();
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed)) return frame;

            // 尝试 Unity 格式: "ClassName.Method () (at Assets/path/file.cs:123)"
            var match = sStackFrameRegex.Match(trimmed);
            if (match.Success && match.Groups[2].Success)
            {
                frame.MethodName = match.Groups[1].Value;
                frame.FilePath = match.Groups[2].Value;
                if (int.TryParse(match.Groups[3].Value, out var lineNum))
                {
                    frame.LineNumber = lineNum;
                }
                return frame;
            }

            // 尝试 Mono 格式: "ClassName.Method () [0x00000] in path/file.cs:123"
            var matchMono = sStackFrameRegexMono.Match(trimmed);
            if (matchMono.Success && matchMono.Groups[2].Success)
            {
                frame.MethodName = matchMono.Groups[1].Value;
                frame.FilePath = matchMono.Groups[2].Value;
                if (int.TryParse(matchMono.Groups[3].Value, out var lineNum))
                {
                    frame.LineNumber = lineNum;
                }
                return frame;
            }

            // 尝试备用格式: "at ClassName.Method() in path/file.cs:line 123"
            if (trimmed.StartsWith("at "))
            {
                trimmed = trimmed.Substring(3);
            }

            var matchAlt = sStackFrameRegexAlt.Match(trimmed);
            if (matchAlt.Success)
            {
                frame.MethodName = matchAlt.Groups[1].Value;
                if (matchAlt.Groups[2].Success)
                {
                    frame.FilePath = matchAlt.Groups[2].Value;
                }
                if (matchAlt.Groups[3].Success && int.TryParse(matchAlt.Groups[3].Value, out var lineNum))
                {
                    frame.LineNumber = lineNum;
                }
            }
            else
            {
                var parenIndex = trimmed.IndexOf('(');
                frame.MethodName = parenIndex > 0 ? trimmed.Substring(0, parenIndex) : trimmed;
            }

            return frame;
        }

        private static bool ShouldSkipStackFrame(string line)
        {
            if (string.IsNullOrEmpty(line)) return true;
            if (line.Contains("System.Environment")) return true;
            if (line.Contains("PoolDebugger")) return true;
            if (line.Contains("SafePoolKit")) return true;
            if (line.Contains("SimplePoolKit")) return true;
            if (line.Contains("UnityEngine.")) return true;
            if (line.Contains("UnityEditor.")) return true;
            return false;
        }

        #endregion

        #region 交互处理

        private void ToggleCardExpansion(VisualElement card, int cardId)
        {
            if (card == default) return;

            var stackContent = card.Q("stack-content");
            var arrow = card.Q<Label>("arrow");
            if (stackContent == default) return;

            var isExpanded = mExpandedCards.Contains(cardId);
            if (isExpanded)
            {
                mExpandedCards.Remove(cardId);
                stackContent.style.display = DisplayStyle.None;
                if (arrow != default) arrow.text = ">";
            }
            else
            {
                mExpandedCards.Add(cardId);
                stackContent.style.display = DisplayStyle.Flex;
                if (arrow != default) arrow.text = "v";
            }
        }

        /// <summary>
        /// 跳转到借出代码位置
        /// </summary>
        private void OnGotoSourceCode(ActiveObjectInfo info)
        {
            if (string.IsNullOrEmpty(info.StackTrace))
            {
                Debug.LogWarning("[PoolKit] 无堆栈信息，请在工具栏启用「堆栈」开关");
                return;
            }

            // 解析堆栈找到第一个有效的代码位置
            var frames = ParseStackFrames(info.StackTrace);
            
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                
                if (!string.IsNullOrEmpty(frame.FilePath) && frame.LineNumber > 0)
                {
                    OpenFileAtLine(frame.FilePath, frame.LineNumber);
                    return;
                }
            }

            Debug.LogWarning("[PoolKit] 未找到有效的代码位置，堆栈可能不包含项目代码");
        }

        private void OpenFileAtLine(string filePath, int lineNumber)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var assetPath = filePath;
            var assetsIndex = filePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                assetPath = filePath.Substring(assetsIndex);
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script != default)
            {
                AssetDatabase.OpenAsset(script, lineNumber);
            }
            else
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
            }
        }

        private static void CopyStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return;
            EditorGUIUtility.systemCopyBuffer = stackTrace;
        }

        #endregion

        #region 工具方法

        private string GetObjectDisplayName(object obj)
        {
            if (obj == default) return "null";

            if (obj is UnityEngine.Object unityObj)
            {
                return unityObj != default ? unityObj.name : "(Destroyed)";
            }

            return obj.ToString();
        }

        /// <summary>
        /// 安全获取文件名，避免非法路径字符导致异常
        /// </summary>
        private static string GetSafeFileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;

            try
            {
                return System.IO.Path.GetFileName(filePath);
            }
            catch (ArgumentException)
            {
                // 路径包含非法字符，手动提取最后一段
                var lastSlash = filePath.LastIndexOfAny(new[] { '/', '\\' });
                return lastSlash >= 0 ? filePath.Substring(lastSlash + 1) : filePath;
            }
        }

        #endregion

        #region 堆栈 UI 构建

        private VisualElement CreateStackContent(ActiveObjectInfo info)
        {
            var content = new VisualElement
            {
                style =
                {
                    paddingLeft = 24,
                    paddingRight = 8,
                    paddingBottom = 8,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f)),
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };

            if (!string.IsNullOrEmpty(info.StackTrace))
            {
                var frames = ParseStackFrames(info.StackTrace);
                for (int i = 0; i < frames.Count; i++)
                {
                    var frameElement = CreateStackFrameElement(frames[i]);
                    content.Add(frameElement);
                }

                var copyBtn = new Button(() => CopyStackTrace(info.StackTrace))
                {
                    text = "复制堆栈",
                    style =
                    {
                        fontSize = 10,
                        marginTop = 6,
                        alignSelf = Align.FlexStart,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 2,
                        paddingBottom = 2,
                        backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard),
                        borderTopLeftRadius = 3,
                        borderTopRightRadius = 3,
                        borderBottomLeftRadius = 3,
                        borderBottomRightRadius = 3
                    }
                };
                content.Add(copyBtn);
            }
            else
            {
                var noStack = new Label("无堆栈信息（请启用堆栈追踪）")
                {
                    style =
                    {
                        fontSize = 10,
                        color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary),
                        paddingTop = 4,
                        paddingBottom = 4
                    }
                };
                content.Add(noStack);
            }

            return content;
        }

        private VisualElement CreateStackFrameElement(StackFrameInfo frame)
        {
            var element = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    paddingLeft = 4,
                    paddingTop = 3,
                    paddingBottom = 3,
                    marginBottom = 2
                }
            };

            // 方法名行
            var methodRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var methodLabel = new Label(frame.MethodName)
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary),
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.Normal,
                    overflow = Overflow.Visible
                }
            };
            methodRow.Add(methodLabel);

            if (!string.IsNullOrEmpty(frame.FilePath))
            {
                var fileName = GetSafeFileName(frame.FilePath);
                var locationLabel = new Label($"{fileName}:{frame.LineNumber}")
                {
                    style =
                    {
                        fontSize = 9,
                        color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimary),
                        marginLeft = 8,
                        flexShrink = 0
                    }
                };
                locationLabel.pickingMode = PickingMode.Position;
                locationLabel.tooltip = $"点击跳转: {frame.FilePath}";

                locationLabel.RegisterCallback<ClickEvent>(evt => OpenFileAtLine(frame.FilePath, frame.LineNumber));
                locationLabel.RegisterCallback<MouseEnterEvent>(evt =>
                    locationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimaryHover));
                locationLabel.RegisterCallback<MouseLeaveEvent>(evt =>
                    locationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimary));

                methodRow.Add(locationLabel);
            }

            element.Add(methodRow);
            return element;
        }

        #endregion
    }
}
#endif
