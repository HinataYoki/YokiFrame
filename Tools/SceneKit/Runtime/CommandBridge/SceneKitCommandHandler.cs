using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit 命令桥处理器。
    /// 默认只输出场景诊断快照；卸载场景是显式维护动作，仍通过 SceneKit 统一静态入口执行。
    /// </summary>
    public sealed class SceneKitCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName
        {
            get { return "SceneKit"; }
        }

        /// <inheritdoc />
        public string[] SupportedActions
        {
            get
            {
                return new[]
                {
                    "stats",
                    "list_scenes",
                    "get_workbench_snapshot",
                    "unload_scene"
                };
            }
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return BuildStatsJson(SceneKit.CreateDiagnosticsSnapshot());
                case "list_scenes":
                    return BuildScenesJson(SceneKit.CreateDiagnosticsSnapshot().Scenes);
                case "get_workbench_snapshot":
                    return BuildWorkbenchSnapshotJson();
                case "unload_scene":
                    return UnloadScene(payloadJson);
                default:
                    throw new NotSupportedException("Unknown SceneKit action '" + action + "'");
            }
        }

        private static string BuildWorkbenchSnapshotJson()
        {
            SceneKitDiagnosticsSnapshot snapshot = SceneKit.CreateDiagnosticsSnapshot();
            string stats = BuildStatsJson(snapshot);
            string scenes = BuildScenesJson(snapshot.Scenes);

            var sb = new StringBuilder(stats.Length + scenes.Length + 48);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"scenes\":");
            sb.Append(scenes);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildStatsJson(SceneKitDiagnosticsSnapshot snapshot)
        {
            var sb = new StringBuilder(192);
            sb.Append("{\"backendName\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.BackendName));
            sb.Append("\",\"backendType\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.BackendType));
            sb.Append("\",\"activeSceneName\":\"");
            sb.Append(JsonHelper.EscapeString(snapshot.ActiveSceneName));
            sb.Append("\",\"loadedSceneCount\":");
            sb.Append(snapshot.Scenes.Count);
            sb.Append(",\"isTransitioning\":");
            sb.Append(snapshot.IsTransitioning ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildScenesJson(List<SceneKitSceneDiagnosticsSnapshot> scenes)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"scenes\":[");
            for (int i = 0; i < scenes.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                AppendScene(sb, scenes[i]);
            }

            sb.Append("],\"count\":");
            sb.Append(scenes.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string UnloadScene(string payloadJson)
        {
            string sceneName = JsonHelper.ExtractString(payloadJson ?? string.Empty, "sceneName");
            if (string.IsNullOrEmpty(sceneName))
                sceneName = JsonHelper.ExtractString(payloadJson ?? string.Empty, "name");

            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("Missing 'sceneName' in payload");

            bool existed = SceneKit.GetSceneHandler(sceneName) != null;
            SceneKit.UnloadSceneAsync(sceneName);
            bool stillLoaded = SceneKit.IsSceneLoaded(sceneName);

            var sb = new StringBuilder(96);
            sb.Append("{\"unloaded\":");
            sb.Append(existed && !stillLoaded ? "true" : "false");
            sb.Append(",\"sceneName\":\"");
            sb.Append(JsonHelper.EscapeString(sceneName));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static void AppendScene(StringBuilder sb, SceneKitSceneDiagnosticsSnapshot scene)
        {
            sb.Append("{\"sceneName\":\"");
            sb.Append(JsonHelper.EscapeString(scene.SceneName));
            sb.Append("\",\"buildIndex\":");
            sb.Append(scene.BuildIndex);
            sb.Append(",\"state\":\"");
            sb.Append(JsonHelper.EscapeString(scene.State.ToString()));
            sb.Append("\",\"progress\":");
            AppendFloat(sb, scene.Progress);
            sb.Append(",\"isSuspended\":");
            sb.Append(scene.IsSuspended ? "true" : "false");
            sb.Append(",\"isPreloaded\":");
            sb.Append(scene.IsPreloaded ? "true" : "false");
            sb.Append(",\"loadMode\":\"");
            sb.Append(JsonHelper.EscapeString(scene.LoadMode.ToString()));
            sb.Append("\",\"isValid\":");
            sb.Append(scene.IsValid ? "true" : "false");
            sb.Append(",\"isActive\":");
            sb.Append(scene.IsActive ? "true" : "false");
            sb.Append(",\"dataType\":\"");
            sb.Append(JsonHelper.EscapeString(scene.DataType));
            sb.Append("\"}");
        }

        private static void AppendFloat(StringBuilder sb, float value)
        {
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
