#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    /// <summary>把 Unity Selection、Scene 与 Prefab Stage 转换为稳定的只读 DTO。</summary>
    internal static class UnityEditorSelectionResolver
    {
        private const int MAX_SELECTION_OBJECTS = 128;
        private const int MAX_TEXT_LENGTH = 512;

        /// <summary>采集当前 Editor 上下文，不创建 UIKit Root 或修改资产。</summary>
        /// <param name="revision">由上下文服务分配的当前 revision。</param>
        /// <returns>稳定、无 Unity 引用的上下文快照。</returns>
        internal static UnityEditorContextSnapshot Capture(long revision)
        {
            UnityEngine.Object[] selectedObjects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            UnityEditorSelectionSnapshot selection = CaptureSelection(selectedObjects);
            Scene activeScene = SceneManager.GetActiveScene();
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return new UnityEditorContextSnapshot
            {
                revision = revision,
                selection = selection,
                scene = new UnityEditorSceneSnapshot
                {
                    path = NormalizePath(activeScene.path),
                    name = Clamp(activeScene.name),
                    dirty = activeScene.IsValid() && activeScene.isDirty,
                    buildIndex = activeScene.IsValid() ? activeScene.buildIndex : -1
                },
                prefabStage = CapturePrefabStage(prefabStage),
                editor = new UnityEditorStateSnapshot
                {
                    mode = GetEditorMode(),
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    isBatchMode = Application.isBatchMode
                }
            };
        }

        /// <summary>创建 Selection 摘要并保留 Unity 原始顺序。</summary>
        /// <param name="selectedObjects">Unity 当前 Selection。</param>
        /// <returns>Selection 稳定 DTO。</returns>
        private static UnityEditorSelectionSnapshot CaptureSelection(UnityEngine.Object[] selectedObjects)
        {
            int totalCount = selectedObjects.Length;
            int count = Math.Min(totalCount, MAX_SELECTION_OBJECTS);
            UnityEditorObjectSnapshot[] objects = new UnityEditorObjectSnapshot[count];
            for (var index = 0; index < count; index++)
            {
                objects[index] = CreateObjectSnapshot(selectedObjects[index]);
            }

            UnityEngine.Object active = Selection.activeObject;
            UnityEditorObjectSnapshot activeSnapshot = CreateObjectSnapshot(active);
            return new UnityEditorSelectionSnapshot
            {
                count = count,
                totalCount = totalCount,
                truncated = count < totalCount,
                activeGlobalObjectId = activeSnapshot == null
                    ? string.Empty
                    : activeSnapshot.globalObjectId,
                activeObject = activeSnapshot,
                objects = objects
            };
        }

        /// <summary>把 Unity 对象映射为 GlobalObjectId、GUID、路径和层级路径。</summary>
        /// <param name="target">待映射对象。</param>
        /// <returns>映射成功时返回对象摘要，否则返回 null。</returns>
        private static UnityEditorObjectSnapshot CreateObjectSnapshot(UnityEngine.Object target)
        {
            if (target == default)
            {
                return null;
            }

            string assetPath = string.Empty;
            try
            {
                assetPath = NormalizePath(AssetDatabase.GetAssetPath(target));
            }
            catch (Exception)
            {
                assetPath = string.Empty;
            }

            string assetGuid = string.Empty;
            if (!string.IsNullOrEmpty(assetPath))
            {
                try
                {
                    assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                }
                catch (Exception)
                {
                    assetGuid = string.Empty;
                }
            }

            string globalObjectId = string.Empty;
            try
            {
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            }
            catch (Exception)
            {
                globalObjectId = string.Empty;
            }

            Transform transform = GetTransform(target);
            return new UnityEditorObjectSnapshot
            {
                globalObjectId = Clamp(globalObjectId),
                assetGuid = Clamp(assetGuid),
                assetPath = Clamp(assetPath),
                name = Clamp(target.name),
                type = Clamp(target.GetType().FullName ?? target.GetType().Name),
                hierarchyPath = transform == default ? string.Empty : Clamp(BuildHierarchyPath(transform)),
                isAsset = !string.IsNullOrEmpty(assetPath),
                isGameObject = target is GameObject || target is Component
            };
        }

        /// <summary>读取对象对应的 Transform，支持 GameObject 与 Component。</summary>
        /// <param name="target">候选 Unity 对象。</param>
        /// <returns>对应 Transform；无层级对象时返回 null。</returns>
        private static Transform GetTransform(UnityEngine.Object target)
        {
            if (target is GameObject gameObject)
            {
                return gameObject.transform;
            }

            if (target is Component component)
            {
                return component.transform;
            }

            return null;
        }

        /// <summary>从根到目标构造稳定层级路径。</summary>
        /// <param name="target">目标 Transform。</param>
        /// <returns>使用正斜杠连接的层级路径。</returns>
        private static string BuildHierarchyPath(Transform target)
        {
            List<string> names = new();
            Transform current = target;
            while (current != default)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>转换 Prefab Stage 信息，避免把 PrefabStage 对象泄漏到协议层。</summary>
        /// <param name="stage">当前 Prefab Stage。</param>
        /// <returns>Prefab Stage 稳定 DTO。</returns>
        private static UnityEditorPrefabStageSnapshot CapturePrefabStage(PrefabStage stage)
        {
            if (stage == null)
            {
                return new UnityEditorPrefabStageSnapshot();
            }

            GameObject root = stage.prefabContentsRoot;
            return new UnityEditorPrefabStageSnapshot
            {
                active = true,
                assetPath = Clamp(NormalizePath(stage.assetPath)),
                scenePath = Clamp(NormalizePath(stage.scene.path)),
                rootName = root == default ? string.Empty : Clamp(root.name)
            };
        }

        /// <summary>获取当前 Editor 模式名称，供快照和诊断统一使用。</summary>
        /// <returns>稳定模式文本。</returns>
        private static string GetEditorMode()
        {
            if (EditorApplication.isPlaying)
            {
                return EditorApplication.isPaused ? "Pause" : "PlayMode";
            }

            return "EditMode";
        }

        /// <summary>规范化项目相对路径，防止平台分隔符进入协议。</summary>
        /// <param name="path">Unity 返回的路径。</param>
        /// <returns>使用正斜杠的项目相对路径。</returns>
        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        /// <summary>限制协议文本长度，避免异常对象名称膨胀上下文 payload。</summary>
        /// <param name="value">待裁剪文本。</param>
        /// <returns>有界文本。</returns>
        private static string Clamp(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MAX_TEXT_LENGTH)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, MAX_TEXT_LENGTH);
        }
    }
}
#endif
