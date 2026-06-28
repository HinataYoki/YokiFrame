#if !GODOT
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 命令桥处理器。
    /// 只输出面板缓存和面板栈诊断，不通过文件桥打开、关闭或切换 UI，避免 AI/编辑器误改运行时界面。
    /// </summary>
    public sealed class UIKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        public string KitName
        {
            get { return "UIKit"; }
        }

        public string[] SupportedActions
        {
            get
            {
                return new[]
                {
                    "stats",
                    "list_panels",
                    "list_stacks",
                    "get_workbench_snapshot"
                };
            }
        }

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return BuildStatsJson(UIKit.CreateDiagnosticsSnapshot());
        }

        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson(UIKit.CreateDiagnosticsSnapshot());
                case "list_panels":
                    return BuildPanelsJson(UIKit.CreateDiagnosticsSnapshot().Panels);
                case "list_stacks":
                    return BuildStacksJson(UIKit.CreateDiagnosticsSnapshot().Stacks);
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                default:
                    throw new NotSupportedException("Unknown UIKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            UIKitDiagnosticsSnapshot snapshot = UIKit.CreateDiagnosticsSnapshot();
            string stats = BuildStatsJson(snapshot);
            string panels = BuildPanelsJson(snapshot.Panels);
            string stacks = BuildStacksJson(snapshot.Stacks);

            var sb = new StringBuilder(stats.Length + panels.Length + stacks.Length + 64);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"panels\":");
            sb.Append(panels);
            sb.Append(",\"stacks\":");
            sb.Append(stacks);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson(UIKitDiagnosticsSnapshot snapshot)
        {
            int openCount = 0;
            int hiddenCount = 0;
            int closedCount = 0;
            int cachedCount = 0;
            int totalStackDepth = 0;
            string defaultTopPanelName = string.Empty;

            for (var i = 0; i < snapshot.Panels.Count; i++)
            {
                UIPanelDiagnosticsSnapshot panel = snapshot.Panels[i];
                if (panel.State == PanelState.Open)
                    openCount++;
                else if (panel.State == PanelState.Hide)
                    hiddenCount++;
                else if (panel.State == PanelState.Close)
                    closedCount++;

                if (panel.IsCached)
                    cachedCount++;
            }

            for (var i = 0; i < snapshot.Stacks.Count; i++)
            {
                UIStackDiagnosticsSnapshot stack = snapshot.Stacks[i];
                totalStackDepth += stack.Depth;
                if (string.Equals(stack.StackName, UIRoot.DEFAULT_STACK, StringComparison.Ordinal))
                    defaultTopPanelName = stack.TopPanelName;
            }

            var sb = new StringBuilder(192);
            sb.Append("{\"isInitialized\":");
            sb.Append(snapshot.IsInitialized ? "true" : "false");
            sb.Append(",\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.BackendName));
            sb.Append("\",\"panelCount\":");
            sb.Append(snapshot.Panels.Count);
            sb.Append(",\"cachedPanelCount\":");
            sb.Append(cachedCount);
            sb.Append(",\"openPanelCount\":");
            sb.Append(openCount);
            sb.Append(",\"hiddenPanelCount\":");
            sb.Append(hiddenCount);
            sb.Append(",\"closedPanelCount\":");
            sb.Append(closedCount);
            sb.Append(",\"stackCount\":");
            sb.Append(snapshot.Stacks.Count);
            sb.Append(",\"totalStackDepth\":");
            sb.Append(totalStackDepth);
            sb.Append(",\"defaultStackDepth\":");
            sb.Append(GetDefaultStackDepth(snapshot.Stacks));
            sb.Append(",\"defaultTopPanelName\":\"");
            sb.Append(JsonHelper.EscapeString(defaultTopPanelName));
            sb.Append("\",\"rootSettings\":");
            sb.Append(BuildRootSettingsJson());
            sb.Append("}");
            return sb.ToString();
        }

        private static string BuildRootSettingsJson()
        {
            var settings = Resources.Load<UIKitSettings>("UIKitSettings");
            var shouldDestroy = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UIKitSettings>();
                shouldDestroy = true;
            }

            try
            {
                var sb = new StringBuilder(192);
                sb.Append("{\"renderMode\":\"");
                sb.Append(JsonHelper.EscapeString(settings.RenderMode.ToString()));
                sb.Append("\",\"sortOrder\":");
                sb.Append(settings.SortOrder);
                sb.Append(",\"targetDisplay\":");
                sb.Append(settings.TargetDisplay);
                sb.Append(",\"pixelPerfect\":");
                sb.Append(settings.PixelPerfect ? "true" : "false");
                sb.Append(",\"scaleMode\":\"");
                sb.Append(JsonHelper.EscapeString(settings.ScaleMode.ToString()));
                sb.Append("\",\"referenceResolution\":\"");
                sb.Append(JsonHelper.EscapeString(FormatVector2(settings.ReferenceResolution)));
                sb.Append("\",\"screenMatchMode\":\"");
                sb.Append(JsonHelper.EscapeString(settings.ScreenMatchMode.ToString()));
                sb.Append("\",\"matchWidthOrHeight\":");
                sb.Append(settings.MatchWidthOrHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"referencePixelsPerUnit\":");
                sb.Append(settings.ReferencePixelsPerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"blockingObjects\":\"");
                sb.Append(JsonHelper.EscapeString(settings.BlockingObjects.ToString()));
                sb.Append("\"}");
                return sb.ToString();
            }
            finally
            {
                if (shouldDestroy && settings != null)
                    DestroyTemporarySettings(settings);
            }
        }

        private static string FormatVector2(Vector2 value)
        {
            return value.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + " x " +
                   value.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void DestroyTemporarySettings(UIKitSettings settings)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(settings);
                return;
            }
#endif
            UnityEngine.Object.Destroy(settings);
        }

        private static string BuildPanelsJson(List<UIPanelDiagnosticsSnapshot> panels)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"panels\":[");
            for (var i = 0; i < panels.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendPanel(sb, panels[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(panels.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStacksJson(List<UIStackDiagnosticsSnapshot> stacks)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"stacks\":[");
            for (var i = 0; i < stacks.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendStack(sb, stacks[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(stacks.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendPanel(StringBuilder sb, UIPanelDiagnosticsSnapshot panel)
        {
            sb.Append("{\"panelName\":\"");
            sb.Append(JsonHelper.EscapeString(panel.PanelName));
            sb.Append("\",\"panelTypeName\":\"");
            sb.Append(JsonHelper.EscapeString(panel.PanelTypeName));
            sb.Append("\",\"state\":\"");
            sb.Append(JsonHelper.EscapeString(panel.State.ToString()));
            sb.Append("\",\"level\":\"");
            sb.Append(JsonHelper.EscapeString(panel.LevelName));
            sb.Append("\",\"levelOrder\":");
            sb.Append(panel.LevelOrder);
            sb.Append(",\"tag\":\"");
            sb.Append(JsonHelper.EscapeString(panel.Tag));
            sb.Append("\",\"dataTypeName\":\"");
            sb.Append(JsonHelper.EscapeString(panel.DataTypeName));
            sb.Append("\",\"isCached\":");
            sb.Append(panel.IsCached ? "true" : "false");
            sb.Append(",\"stackNames\":[");
            AppendStringArray(sb, panel.StackNames);
            sb.Append("]}");
        }

        private static void AppendStack(StringBuilder sb, UIStackDiagnosticsSnapshot stack)
        {
            sb.Append("{\"stackName\":\"");
            sb.Append(JsonHelper.EscapeString(stack.StackName));
            sb.Append("\",\"depth\":");
            sb.Append(stack.Depth);
            sb.Append(",\"topPanelName\":\"");
            sb.Append(JsonHelper.EscapeString(stack.TopPanelName));
            sb.Append("\",\"panelNames\":[");
            AppendStringArray(sb, stack.PanelNames);
            sb.Append("]}");
        }

        private static void AppendStringArray(StringBuilder sb, string[] values)
        {
            if (values == null)
                return;

            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append('"');
                sb.Append(JsonHelper.EscapeString(values[i] ?? string.Empty));
                sb.Append('"');
            }
        }

        private static int GetDefaultStackDepth(List<UIStackDiagnosticsSnapshot> stacks)
        {
            if (stacks == null)
                return 0;

            for (var i = 0; i < stacks.Count; i++)
            {
                if (string.Equals(stacks[i].StackName, UIRoot.DEFAULT_STACK, StringComparison.Ordinal))
                    return stacks[i].Depth;
            }

            return 0;
        }
    }
}
#endif
