#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 跨 Domain Reload 保存待回填 Prefab，并在脚本编译完成后重试。
    /// </summary>
    [InitializeOnLoad]
    internal static class UIKitPendingBindingService
    {
        private const string SESSION_KEY = "YokiFrame.UIKit.PendingBindings";
        private const int MAX_ATTEMPTS = 20;
        private const int PANEL_OWNER_KIND = 0;
        private static bool sProcessScheduled;

        /// <summary>注册 Domain Reload 后的延迟处理入口。</summary>
        static UIKitPendingBindingService()
        {
            ScheduleProcess();
        }

        /// <summary>登记一个已生成源码、等待类型编译的 Prefab。</summary>
        internal static void Queue(UIKitPanelCodeLayout layout)
        {
            QueueCore(layout, default, default, PANEL_OWNER_KIND, string.Empty);
        }

        /// <summary>登记 Prefab 层级内具体 UIElement/UIComponent owner 的编译后回填。</summary>
        internal static void QueueOwner(
            UIKitPanelCodeLayout layout,
            Type ownerType,
            UIKitGeneratedOwnerKind ownerKind,
            string ownerPath)
        {
            if (ownerType == null) throw new ArgumentNullException(nameof(ownerType));
            QueueCore(
                layout,
                ownerType.FullName,
                ownerType.Assembly.GetName().Name,
                (int)ownerKind,
                ownerPath);
        }

        /// <summary>按 Prefab 路径替换或追加一个稳定待回填条目。</summary>
        private static void QueueCore(
            UIKitPanelCodeLayout layout,
            string ownerTypeName,
            string ownerAssemblyName,
            int ownerKind,
            string ownerPath)
        {
            PendingCollection collection = Load();
            for (var index = 0; index < collection.items.Count; index++)
            {
                if (!IsSameBindingTarget(
                        collection.items[index],
                        layout.PrefabPath,
                        ownerTypeName,
                        ownerAssemblyName,
                        ownerKind,
                        ownerPath))
                    continue;
                collection.items[index] = CreateEntry(
                    layout,
                    ownerTypeName,
                    ownerAssemblyName,
                    ownerKind,
                    ownerPath);
                Save(collection);
                ScheduleProcess();
                return;
            }

            collection.items.Add(CreateEntry(
                layout,
                ownerTypeName,
                ownerAssemblyName,
                ownerKind,
                ownerPath));
            Save(collection);
            ScheduleProcess();
        }

        /// <summary>在 Editor 空闲时处理全部待回填项，并保留仍等待编译的条目。</summary>
        internal static void Process()
        {
            sProcessScheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleProcess();
                return;
            }

            PendingCollection collection = Load();
            if (collection.items.Count == 0)
            {
                SessionState.EraseString(SESSION_KEY);
                return;
            }

            List<PendingEntry> remaining = null;
            for (var index = 0; index < collection.items.Count; index++)
            {
                PendingEntry entry = collection.items[index];
                if (TryProcessEntry(entry))
                {
                    if (remaining == null) remaining = new List<PendingEntry>(collection.items.Count);
                    remaining.Add(entry);
                }
            }

            if (remaining == null || remaining.Count == 0)
            {
                SessionState.EraseString(SESSION_KEY);
                return;
            }

            collection.items = remaining;
            Save(collection);
        }

        /// <summary>处理单条回填请求，避免单个损坏条目中断整个队列。</summary>
        private static bool TryProcessEntry(PendingEntry entry)
        {
            try
            {
                UIKitPanelCodeLayout layout = CreateLayout(entry);
                UIKitPrefabBindingStatus status = entry.ownerKind == PANEL_OWNER_KIND
                    ? UIKitPrefabBindingProcessor.Bind(layout, out string error)
                    : UIKitPrefabBindingProcessor.BindOwner(
                        layout,
                        entry.ownerTypeName,
                        entry.ownerAssemblyName,
                        (UIKitGeneratedOwnerKind)entry.ownerKind,
                        entry.ownerPath,
                        out error);
                if (status == UIKitPrefabBindingStatus.Success) return false;
                entry.attempts++;
                bool shouldRetry = status == UIKitPrefabBindingStatus.Pending && entry.attempts < MAX_ATTEMPTS;
                if (!shouldRetry)
                    Debug.LogError("[UIKit] Prefab 回填失败: " + entry.prefabPath + " | " + error);
                return shouldRetry;
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIKit] Prefab 回填条目无效: " + entry.prefabPath + " | " + exception.Message);
                return false;
            }
        }

        /// <summary>至多登记一个空闲回调，避免多次入队造成重复 SessionState 读取和空队列扫描。</summary>
        private static void ScheduleProcess()
        {
            if (sProcessScheduled) return;
            sProcessScheduled = true;
            EditorApplication.delayCall += Process;
        }

        /// <summary>从稳定 SessionState JSON 读取待处理集合。</summary>
        private static PendingCollection Load()
        {
            string json = SessionState.GetString(SESSION_KEY, string.Empty);
            PendingCollection collection = string.IsNullOrWhiteSpace(json)
                ? new PendingCollection()
                : JsonUtility.FromJson<PendingCollection>(json);
            if (collection == null) collection = new PendingCollection();
            if (collection.items == null) collection.items = new List<PendingEntry>();
            return collection;
        }

        /// <summary>把完整待处理集合写回当前 Unity Session。</summary>
        private static void Save(PendingCollection collection)
        {
            SessionState.SetString(SESSION_KEY, JsonUtility.ToJson(collection));
        }

        /// <summary>从验证布局创建可跨 Domain Reload 的纯数据条目。</summary>
        private static PendingEntry CreateEntry(
            UIKitPanelCodeLayout layout,
            string ownerTypeName,
            string ownerAssemblyName,
            int ownerKind,
            string ownerPath)
        {
            return new PendingEntry
            {
                panelName = layout.PanelName,
                prefabFolder = layout.PrefabFolder,
                scriptFolder = layout.ScriptFolder,
                scriptNamespace = layout.ScriptNamespace,
                assemblyName = layout.AssemblyName,
                codeTemplate = layout.CodeTemplate,
                prefabPath = layout.PrefabPath,
                ownerTypeName = ownerTypeName,
                ownerAssemblyName = ownerAssemblyName,
                ownerKind = ownerKind,
                ownerPath = ownerPath ?? string.Empty,
            };
        }

        /// <summary>判断待回填条目是否指向同一个 Panel 或层级 owner。</summary>
        private static bool IsSameBindingTarget(
            PendingEntry entry,
            string prefabPath,
            string ownerTypeName,
            string ownerAssemblyName,
            int ownerKind,
            string ownerPath)
        {
            return string.Equals(entry.prefabPath, prefabPath, StringComparison.Ordinal)
                && entry.ownerKind == ownerKind
                && string.Equals(entry.ownerTypeName, ownerTypeName, StringComparison.Ordinal)
                && string.Equals(entry.ownerAssemblyName, ownerAssemblyName, StringComparison.Ordinal)
                && string.Equals(entry.ownerPath ?? string.Empty, ownerPath ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>把持久条目重新校验为生成布局。</summary>
        private static UIKitPanelCodeLayout CreateLayout(PendingEntry entry)
        {
            return new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            {
                panelName = entry.panelName,
                prefabFolder = entry.prefabFolder,
                scriptFolder = entry.scriptFolder,
                scriptNamespace = entry.scriptNamespace,
                assemblyName = entry.assemblyName,
                codeTemplate = entry.codeTemplate,
                prefabPath = entry.prefabPath,
            });
        }

        /// <summary>JsonUtility 可序列化的待处理集合。</summary>
        [Serializable]
        private sealed class PendingCollection
        {
            public List<PendingEntry> items = new();
        }

        /// <summary>JsonUtility 可序列化的单个待回填请求。</summary>
        [Serializable]
        private sealed class PendingEntry
        {
            public string panelName;
            public string prefabFolder;
            public string scriptFolder;
            public string scriptNamespace;
            public string assemblyName;
            public string codeTemplate;
            public string prefabPath;
            public string ownerTypeName;
            public string ownerAssemblyName;
            public string ownerPath;
            public int ownerKind;
            public int attempts;
        }
    }
}
#endif
