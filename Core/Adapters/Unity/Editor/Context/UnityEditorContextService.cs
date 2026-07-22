#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>维护 Unity Editor 公共只读上下文的 revision 与事件订阅。</summary>
    [InitializeOnLoad]
    public static class UnityEditorContextService
    {
        private static long sRevision = 1L;
        private static bool sSubscribed;
        private static int sSelectionSignature;
        private static bool sSelectionSignatureInitialized;

        /// <summary>静态初始化时订阅会影响 Selection/Scene/Prefab 状态的 Editor 事件。</summary>
        static UnityEditorContextService()
        {
            SubscribeEvents();
            sSelectionSignature = ComputeSelectionSignature();
            sSelectionSignatureInitialized = true;
        }

        /// <summary>获取当前 Editor 上下文的单调 revision。</summary>
        public static long Revision
        {
            get
            {
                RefreshObservedSelection();
                return sRevision;
            }
        }

        /// <summary>读取当前上下文快照；不会创建 UIKit Root 或修改 Unity 资源。</summary>
        /// <returns>稳定的只读上下文 DTO。</returns>
        public static UnityEditorContextSnapshot Capture()
        {
            RefreshObservedSelection();
            return UnityEditorSelectionResolver.Capture(sRevision);
        }

        /// <summary>判断调用方携带的 revision 是否仍代表当前选择上下文。</summary>
        /// <param name="expectedRevision">调用方读取上下文时记录的 revision；小于等于零表示不校验。</param>
        /// <returns>revision 未过期或调用方未要求校验时返回 true。</returns>
        public static bool MatchesRevision(long expectedRevision)
        {
            RefreshObservedSelection();
            return expectedRevision <= 0L || expectedRevision == sRevision;
        }

        /// <summary>判断稳定 GlobalObjectId 当前是否仍在 Selection 中。</summary>
        /// <param name="globalObjectId">调用方记录的稳定对象 ID。</param>
        /// <returns>对象仍被选中时返回 true。</returns>
        public static bool IsSelected(string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                return false;
            }

            UnityEditorContextSnapshot snapshot = Capture();
            UnityEditorSelectionSnapshot selection = snapshot.selection;
            if (selection == null || selection.objects == null)
            {
                return false;
            }

            for (var index = 0; index < selection.objects.Length; index++)
            {
                UnityEditorObjectSnapshot item = selection.objects[index];
                if (item != null && string.Equals(item.globalObjectId, globalObjectId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>订阅 Unity Editor 状态变化事件；重复调用保持幂等。</summary>
        private static void SubscribeEvents()
        {
            if (sSubscribed)
            {
                return;
            }

            sSubscribed = true;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        /// <summary>Selection 改变时推进上下文 revision。</summary>
        private static void OnSelectionChanged()
        {
            sSelectionSignature = ComputeSelectionSignature();
            sSelectionSignatureInitialized = true;
            MarkChanged();
        }

        /// <summary>项目资产变化时推进上下文 revision。</summary>
        private static void OnProjectChanged() => MarkChanged();

        /// <summary>层级变化时推进上下文 revision。</summary>
        private static void OnHierarchyChanged() => MarkChanged();

        /// <summary>Play/Edit/Pause 状态变化时推进上下文 revision。</summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange _) => MarkChanged();

        /// <summary>活动 Scene 变化时推进上下文 revision。</summary>
        private static void OnActiveSceneChanged(Scene _, Scene __) => MarkChanged();

        /// <summary>Scene 打开时推进上下文 revision。</summary>
        private static void OnSceneOpened(Scene _, OpenSceneMode __) => MarkChanged();

        /// <summary>Scene 关闭时推进上下文 revision。</summary>
        private static void OnSceneClosed(Scene _) => MarkChanged();

        /// <summary>进入 Prefab Stage 时推进上下文 revision。</summary>
        private static void OnPrefabStageOpened(PrefabStage _) => MarkChanged();

        /// <summary>离开 Prefab Stage 时推进上下文 revision。</summary>
        private static void OnPrefabStageClosing(PrefabStage _) => MarkChanged();

        /// <summary>推进单调 revision，避免旧上下文在 ABA 场景下被误用。</summary>
        private static void MarkChanged()
        {
            unchecked
            {
                sRevision++;
                if (sRevision <= 0L)
                {
                    sRevision = 1L;
                }
            }
        }

        /// <summary>轮询 Unity Selection 签名，覆盖测试或批处理环境中延迟的 selectionChanged 回调。</summary>
        private static void RefreshObservedSelection()
        {
            int signature = ComputeSelectionSignature();
            if (!sSelectionSignatureInitialized)
            {
                sSelectionSignature = signature;
                sSelectionSignatureInitialized = true;
                return;
            }

            if (signature == sSelectionSignature)
            {
                return;
            }

            sSelectionSignature = signature;
            MarkChanged();
        }

        /// <summary>以 Selection 顺序和当前 Unity 对象哈希计算轻量变化签名。</summary>
        /// <returns>当前 Selection 的确定性哈希。</returns>
        private static int ComputeSelectionSignature()
        {
            unchecked
            {
                int hash = 17;
                UnityEngine.Object active = Selection.activeObject;
                hash = hash * 31 + (active == default ? 0 : active.GetHashCode());
                UnityEngine.Object[] objects = Selection.objects;
                if (objects == null) return hash;
                hash = hash * 31 + objects.Length;
                for (var index = 0; index < objects.Length; index++)
                {
                    UnityEngine.Object target = objects[index];
                    hash = hash * 31 + (target == default ? 0 : target.GetHashCode());
                }

                return hash;
            }
        }
    }
}
#endif
