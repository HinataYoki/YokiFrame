#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>描述 Unity Editor 当前可供工具消费的只读上下文。</summary>
    [Serializable]
    public sealed class UnityEditorContextSnapshot
    {
        /// <summary>当前上下文协议版本。</summary>
        public int schemaVersion = 1;

        /// <summary>当前宿主是否可以提供上下文。</summary>
        public bool available = true;

        /// <summary>上下文变化版本；写操作可用它检测过期选择。</summary>
        public long revision;

        /// <summary>当前 Unity Selection 摘要。</summary>
        public UnityEditorSelectionSnapshot selection = new();

        /// <summary>当前活动 Scene 摘要。</summary>
        public UnityEditorSceneSnapshot scene = new();

        /// <summary>当前 Prefab Stage 摘要。</summary>
        public UnityEditorPrefabStageSnapshot prefabStage = new();

        /// <summary>当前 Editor 生命周期与模式摘要。</summary>
        public UnityEditorStateSnapshot editor = new();
    }

    /// <summary>描述当前 Selection 中的对象及其稳定标识。</summary>
    [Serializable]
    public sealed class UnityEditorSelectionSnapshot
    {
        /// <summary>Selection 中有效对象数量。</summary>
        public int count;

        /// <summary>Unity 报告的原始 Selection 数量。</summary>
        public int totalCount;

        /// <summary>对象数量超过上限时为 true。</summary>
        public bool truncated;

        /// <summary>当前活动对象的 GlobalObjectId。</summary>
        public string activeGlobalObjectId = string.Empty;

        /// <summary>当前活动对象摘要。</summary>
        public UnityEditorObjectSnapshot activeObject;

        /// <summary>按 Unity Selection 顺序排列的对象摘要。</summary>
        public UnityEditorObjectSnapshot[] objects = Array.Empty<UnityEditorObjectSnapshot>();
    }

    /// <summary>描述一个不携带 Unity 对象引用的稳定对象事实。</summary>
    [Serializable]
    public sealed class UnityEditorObjectSnapshot
    {
        /// <summary>Unity GlobalObjectId 文本。</summary>
        public string globalObjectId = string.Empty;

        /// <summary>资产 GUID；场景对象为空。</summary>
        public string assetGuid = string.Empty;

        /// <summary>项目相对资产路径；场景对象为空。</summary>
        public string assetPath = string.Empty;

        /// <summary>对象名称。</summary>
        public string name = string.Empty;

        /// <summary>对象的完整 CLR 类型名。</summary>
        public string type = string.Empty;

        /// <summary>从所属根到对象的层级路径。</summary>
        public string hierarchyPath = string.Empty;

        /// <summary>对象是否来自资产。</summary>
        public bool isAsset;

        /// <summary>对象是否为 GameObject 或其 Component。</summary>
        public bool isGameObject;
    }

    /// <summary>描述当前活动 Scene 的稳定信息。</summary>
    [Serializable]
    public sealed class UnityEditorSceneSnapshot
    {
        /// <summary>Scene 路径。</summary>
        public string path = string.Empty;

        /// <summary>Scene 名称。</summary>
        public string name = string.Empty;

        /// <summary>Scene 是否有未保存修改。</summary>
        public bool dirty;

        /// <summary>Scene 在 Build Settings 中的索引。</summary>
        public int buildIndex = -1;
    }

    /// <summary>描述当前 Prefab Stage 的稳定信息。</summary>
    [Serializable]
    public sealed class UnityEditorPrefabStageSnapshot
    {
        /// <summary>当前是否处于 Prefab Stage。</summary>
        public bool active;

        /// <summary>正在编辑的 Prefab 资产路径。</summary>
        public string assetPath = string.Empty;

        /// <summary>Prefab Stage 场景路径。</summary>
        public string scenePath = string.Empty;

        /// <summary>Prefab 内容根节点名称。</summary>
        public string rootName = string.Empty;
    }

    /// <summary>描述 Unity Editor 当前模式和生命周期状态。</summary>
    [Serializable]
    public sealed class UnityEditorStateSnapshot
    {
        /// <summary>当前模式，例如 EditMode、PlayMode 或 Pause。</summary>
        public string mode = string.Empty;

        /// <summary>是否正在 Play Mode。</summary>
        public bool isPlaying;

        /// <summary>是否处于暂停状态。</summary>
        public bool isPaused;

        /// <summary>是否正在编译脚本。</summary>
        public bool isCompiling;

        /// <summary>是否正在处理资源更新。</summary>
        public bool isUpdating;

        /// <summary>是否为 Batch Mode。</summary>
        public bool isBatchMode;
    }

    /// <summary>把上下文 DTO 序列化为有界 JSON，供 FileBridge 与 Telemetry 共用。</summary>
    internal static class UnityEditorContextSnapshotWriter
    {
        private const int MAX_SELECTION_OBJECTS = 128;
        private static readonly UTF8Encoding sUtf8 = new(false);

        /// <summary>序列化快照并在异常大时按确定性顺序裁剪 Selection。</summary>
        /// <param name="snapshot">待写出的上下文快照。</param>
        /// <returns>不超过共享 Telemetry 上限的 JSON。</returns>
        internal static string Write(UnityEditorContextSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            UnityEditorSelectionSnapshot selection = snapshot.selection;
            if (selection != null && selection.objects != null
                && selection.objects.Length > MAX_SELECTION_OBJECTS)
            {
                Array.Resize(ref selection.objects, MAX_SELECTION_OBJECTS);
                selection.count = selection.objects.Length;
                selection.truncated = true;
            }

            string json = JsonUtility.ToJson(snapshot);
            int maxBytes = YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES;
            while (sUtf8.GetByteCount(json) > maxBytes
                && selection != null
                && selection.objects != null
                && selection.objects.Length > 0)
            {
                int nextCount = selection.objects.Length <= 1 ? 0 : selection.objects.Length / 2;
                Array.Resize(ref selection.objects, nextCount);
                selection.count = nextCount;
                selection.truncated = true;
                json = JsonUtility.ToJson(snapshot);
            }

            return json;
        }
    }
}
#endif
