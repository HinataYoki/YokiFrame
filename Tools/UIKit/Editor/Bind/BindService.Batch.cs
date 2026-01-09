#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定服务 - 批量绑定逻辑
    /// </summary>
    public static partial class BindService
    {
        #region 批量绑定

        /// <summary>
        /// 批量添加 Bind 组件
        /// </summary>
        /// <param name="gameObjects">目标 GameObject 列表</param>
        /// <param name="recursive">是否递归处理子物体</param>
        /// <param name="defaultType">默认绑定类型</param>
        /// <param name="autoSuggestName">是否自动设置建议名称</param>
        /// <returns>添加结果报告</returns>
        public static BatchBindResult BatchAddBind(
            IEnumerable<GameObject> gameObjects,
            bool recursive = false,
            BindType defaultType = BindType.Member,
            bool autoSuggestName = true)
        {
            BatchBindResult result = new();

            if (gameObjects == null)
                return result;

            foreach (var go in gameObjects)
            {
                if (go == null)
                    continue;

                ProcessGameObjectForBind(go, recursive, defaultType, autoSuggestName, result);
            }

            return result;
        }

        /// <summary>
        /// 处理单个 GameObject 的绑定添加
        /// </summary>
        private static void ProcessGameObjectForBind(
            GameObject go,
            bool recursive,
            BindType defaultType,
            bool autoSuggestName,
            BatchBindResult result)
        {
            // 检查是否已有 Bind 组件
            var existingBind = go.GetComponent<AbstractBind>();
            if (existingBind != null)
            {
                result.RecordSkipped(GetGameObjectPath(go));
            }
            else
            {
                // 添加 Bind 组件
                var bind = AddBindComponent(go, defaultType, autoSuggestName);
                if (bind != null)
                {
                    result.RecordSuccess(bind);
                }
                else
                {
                    result.RecordFailed(GetGameObjectPath(go), "无法添加 Bind 组件");
                }
            }

            // 递归处理子物体
            if (recursive)
            {
                int childCount = go.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    ProcessGameObjectForBind(child, true, defaultType, autoSuggestName, result);
                }
            }
        }

        /// <summary>
        /// 添加 Bind 组件到 GameObject
        /// </summary>
        private static AbstractBind AddBindComponent(
            GameObject go,
            BindType bindType,
            bool autoSuggestName)
        {
            // 使用 Undo 支持撤销
            var bind = Undo.AddComponent<Bind>(go);
            if (bind == null)
                return null;

            bind.bind = bindType;

            // 自动设置建议名称
            if (autoSuggestName)
            {
                // 尝试获取主要 UI 组件类型
                var componentType = GetPrimaryUIComponentType(go);
                string suggestedName = BindNameSuggester.SuggestName(go, bindType, componentType);
                bind.mName = suggestedName;

                // 设置组件类型
                if (componentType != null)
                {
                    bind.type = componentType.FullName;
                    bind.autoType = componentType.FullName;
                }
            }

            EditorUtility.SetDirty(go);
            return bind;
        }

        /// <summary>
        /// 获取 GameObject 上的主要 UI 组件类型
        /// </summary>
        private static System.Type GetPrimaryUIComponentType(GameObject go)
        {
            if (go == null)
                return null;

            // 按优先级检查常用 UI 组件
            var priorityTypes = new[]
            {
                typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.Toggle),
                typeof(UnityEngine.UI.Slider),
                typeof(UnityEngine.UI.InputField),
                typeof(UnityEngine.UI.Dropdown),
                typeof(UnityEngine.UI.ScrollRect),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.Text),
                typeof(UnityEngine.CanvasGroup),
                typeof(RectTransform)
            };

            foreach (var type in priorityTypes)
            {
                if (go.GetComponent(type) != null)
                    return type;
            }

            // 检查 TMP 组件（通过名称避免硬依赖）
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().FullName;
                if (typeName != null && typeName.StartsWith("TMPro."))
                    return comp.GetType();
            }

            return typeof(GameObject);
        }

        /// <summary>
        /// 获取 GameObject 的层级路径
        /// </summary>
        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null)
                return string.Empty;

            var pathParts = new List<string>(8);
            var current = go.transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        #endregion
    }
}
#endif
