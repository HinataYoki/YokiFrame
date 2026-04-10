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
    /// PoolKit 堆栈解析与交互处理。
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region Stack Frame Regex

        private static readonly Regex sStackFrameRegex = new(
            @"^(.+?)\s*\(.*?\)\s*\(at\s+(.+?):(\d+)\)$",
            RegexOptions.Compiled);

        private static readonly Regex sStackFrameRegexMono = new(
            @"^(.+?)\s*\(.*?\)\s*\[0x[0-9a-fA-F]+\]\s*in\s+(.+?):(\d+)$",
            RegexOptions.Compiled);

        private static readonly Regex sStackFrameRegexAlt = new(
            @"^\s*(?:at\s+)?(.+?)\s*(?:\(.*?\))?\s*(?:in\s+(.+?):line\s+(\d+))?$",
            RegexOptions.Compiled);

        #endregion

        #region Stack Frame Model

        private struct StackFrameInfo
        {
            public string MethodName;
            public string FilePath;
            public int LineNumber;
        }

        #endregion

        #region Stack Parser

        /// <summary>
        /// 解析堆栈文本，提取可展示的帧信息。
        /// </summary>
        private List<StackFrameInfo> ParseStackFrames(string stackTrace)
        {
            var frames = new List<StackFrameInfo>();
            if (string.IsNullOrEmpty(stackTrace))
            {
                return frames;
            }

            var lines = stackTrace.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (ShouldSkipStackFrame(line))
                {
                    continue;
                }

                var frame = ParseSingleFrame(line);
                if (!string.IsNullOrEmpty(frame.MethodName))
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        /// <summary>
        /// 解析单条堆栈帧。
        /// </summary>
        private StackFrameInfo ParseSingleFrame(string line)
        {
            var frame = new StackFrameInfo();
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                return frame;
            }

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

        /// <summary>
        /// 过滤掉无需展示的内部帧。
        /// </summary>
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

        #region Interaction

        /// <summary>
        /// 切换卡片展开状态。
        /// </summary>
        private void ToggleCardExpansion(VisualElement card, int cardId)
        {
            if (card == default)
            {
                return;
            }

            var stackContent = card.Q("stack-content");
            var arrow = card.Q<Label>("arrow");
            if (stackContent == default)
            {
                return;
            }

            bool isExpanded = mExpandedCards.Contains(cardId);
            if (isExpanded)
            {
                mExpandedCards.Remove(cardId);
                stackContent.style.display = DisplayStyle.None;
                if (arrow != default)
                {
                    arrow.text = ">";
                }
            }
            else
            {
                mExpandedCards.Add(cardId);
                stackContent.style.display = DisplayStyle.Flex;
                if (arrow != default)
                {
                    arrow.text = "v";
                }
            }
        }

        /// <summary>
        /// 跳转到借出对象的源代码位置。
        /// </summary>
        private void OnGotoSourceCode(ActiveObjectInfo info)
        {
            if (string.IsNullOrEmpty(info.StackTrace))
            {
                Debug.LogWarning("[PoolKit] 无堆栈信息，请在工具栏启用“堆栈”开关。");
                return;
            }

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

            Debug.LogWarning("[PoolKit] 未找到有效的代码位置，当前堆栈可能不包含项目脚本。");
        }

        /// <summary>
        /// 打开指定文件与行号。
        /// </summary>
        private void OpenFileAtLine(string filePath, int lineNumber)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var assetPath = filePath;
            int assetsIndex = filePath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// 复制完整堆栈文本。
        /// </summary>
        private static void CopyStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = stackTrace;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 获取对象的显示名称。
        /// </summary>
        private string GetObjectDisplayName(object obj)
        {
            if (obj == default)
            {
                return "空对象";
            }

            if (obj is UnityEngine.Object unityObj)
            {
                return unityObj != default ? unityObj.name : "已销毁对象";
            }

            return obj.ToString();
        }

        /// <summary>
        /// 安全提取文件名，避免路径非法导致异常。
        /// </summary>
        private static string GetSafeFileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            try
            {
                return System.IO.Path.GetFileName(filePath);
            }
            catch (ArgumentException)
            {
                int lastSlash = filePath.LastIndexOfAny(new[] { '/', '\\' });
                return lastSlash >= 0 ? filePath.Substring(lastSlash + 1) : filePath;
            }
        }

        #endregion

        #region Stack UI

        /// <summary>
        /// 创建卡片下方的堆栈内容区。
        /// </summary>
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
                    content.Add(CreateStackFrameElement(frames[i]));
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

        /// <summary>
        /// 创建单条堆栈帧视图。
        /// </summary>
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

                locationLabel.RegisterCallback<ClickEvent>(_ => OpenFileAtLine(frame.FilePath, frame.LineNumber));
                locationLabel.RegisterCallback<MouseEnterEvent>(_ =>
                    locationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimaryHover));
                locationLabel.RegisterCallback<MouseLeaveEvent>(_ =>
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
