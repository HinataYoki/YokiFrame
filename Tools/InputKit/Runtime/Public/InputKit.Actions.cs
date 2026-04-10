#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - ActionMap 管理与显示名称
    /// </summary>
    public static partial class InputKit
    {
        private static readonly List<string> sEnabledActionMaps = new();

        #region ActionMap 管理

        /// <summary>
        /// 切换到指定 ActionMap（禁用其他）
        /// </summary>
        /// <param name="mapName">ActionMap 名称</param>
        public static void SwitchActionMap(string mapName)
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset");
                return;
            }

            sEnabledActionMaps.Clear();

            foreach (var map in sActionAsset.actionMaps)
            {
                if (map.name == mapName)
                {
                    map.Enable();
                    sEnabledActionMaps.Add(map.name);
                }
                else
                {
                    map.Disable();
                }
            }
        }

        /// <summary>
        /// 启用多个 ActionMap
        /// </summary>
        /// <param name="mapNames">ActionMap 名称列表</param>
        public static void EnableActionMaps(params string[] mapNames)
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset");
                return;
            }

            var nameSet = new HashSet<string>(mapNames);
            sEnabledActionMaps.Clear();

            foreach (var map in sActionAsset.actionMaps)
            {
                if (nameSet.Contains(map.name))
                {
                    map.Enable();
                    sEnabledActionMaps.Add(map.name);
                }
                else
                {
                    map.Disable();
                }
            }
        }

        /// <summary>
        /// 禁用所有 ActionMap
        /// </summary>
        public static void DisableAllActionMaps()
        {
            if (sActionAsset == default) return;

            foreach (var map in sActionAsset.actionMaps)
            {
                map.Disable();
            }

            sEnabledActionMaps.Clear();
        }

        /// <summary>
        /// 启用所有 ActionMap
        /// </summary>
        public static void EnableAllActionMaps()
        {
            if (sActionAsset == default) return;

            sEnabledActionMaps.Clear();

            foreach (var map in sActionAsset.actionMaps)
            {
                map.Enable();
                sEnabledActionMaps.Add(map.name);
            }
        }

        /// <summary>
        /// 获取当前启用的 ActionMap 名称
        /// </summary>
        /// <returns>启用的 ActionMap 名称列表</returns>
        public static IReadOnlyList<string> GetEnabledActionMaps()
        {
            return sEnabledActionMaps;
        }

        #endregion

        #region 显示名称

        /// <summary>
        /// 获取绑定显示名称
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <param name="bindingIndex">绑定索引</param>
        /// <returns>显示名称</returns>
        public static string GetBindingDisplayString(InputAction action, int bindingIndex = 0)
        {
            if (action == default) return string.Empty;
            return action.GetBindingDisplayString(bindingIndex);
        }

        /// <summary>
        /// 获取指定控制方案的绑定显示名称
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <param name="controlScheme">控制方案名称</param>
        /// <returns>显示名称</returns>
        public static string GetBindingDisplayString(InputAction action, string controlScheme)
        {
            if (action == default) return string.Empty;

            var bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(controlScheme));
            return bindingIndex >= 0
                ? action.GetBindingDisplayString(bindingIndex)
                : string.Empty;
        }

        #endregion

        #region 备用字符串 API

        /// <summary>
        /// 按名称获取 InputAction（备用 API）
        /// </summary>
        /// <param name="actionNameOrId">Action 名称或 ID</param>
        /// <returns>InputAction，未找到返回 default</returns>
        public static InputAction FindAction(string actionNameOrId)
        {
            if (sActionAsset == default) return default;
            return sActionAsset.FindAction(actionNameOrId);
        }

        /// <summary>
        /// 按名称获取 ActionMap（备用 API）
        /// </summary>
        /// <param name="mapName">ActionMap 名称</param>
        /// <returns>InputActionMap，未找到返回 default</returns>
        public static InputActionMap FindActionMap(string mapName)
        {
            if (sActionAsset == default) return default;
            return sActionAsset.FindActionMap(mapName);
        }

        #endregion
    }
}

#endif