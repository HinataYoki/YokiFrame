#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// BindService 中负责批量添加 Bind 的逻辑。
    /// </summary>
    public static partial class BindService
    {
        #region 批量绑定

        /// <summary>
        /// 为一组对象批量添加 Bind 组件。
        /// </summary>
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
        /// 处理单个对象的绑定添加流程。
        /// </summary>
        private static void ProcessGameObjectForBind(
            GameObject go,
            bool recursive,
            BindType defaultType,
            bool autoSuggestName,
            BatchBindResult result)
        {
            var existingBind = go.GetComponent<AbstractBind>();
            if (existingBind != null)
            {
                result.RecordSkipped(GetGameObjectPath(go));
            }
            else
            {
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
        /// 在指定对象上添加 Bind 组件。
        /// </summary>
        private static AbstractBind AddBindComponent(
            GameObject go,
            BindType bindType,
            bool autoSuggestName)
        {
            var bind = Undo.AddComponent<Bind>(go);
            if (bind == null)
                return null;

            bind.bind = bindType;

            if (autoSuggestName)
            {
                var componentType = GetPrimaryUIComponentType(go);
                string suggestedName = BindNameSuggester.SuggestName(go, bindType, componentType);
                bind.mName = suggestedName;

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
        /// 获取对象上最适合作为绑定类型的 UI 组件类型。
        /// </summary>
        private static System.Type GetPrimaryUIComponentType(GameObject go)
        {
            if (go == null)
                return null;

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
        /// 获取对象的层级路径。
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
