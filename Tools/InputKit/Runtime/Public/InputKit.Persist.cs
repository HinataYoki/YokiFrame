#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 绑定持久化
    /// </summary>
    public static partial class InputKit
    {
        /// <summary>是否有保存的绑定</summary>
        public static bool HasSavedBindings => sPersistence != default && sPersistence.Exists(sPersistenceKey);

        /// <summary>
        /// 保存当前绑定覆盖
        /// </summary>
        public static void SaveBindings()
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset，无法保存绑定");
                return;
            }

            if (sPersistence == default)
            {
                Debug.LogWarning("[InputKit] 未设置持久化实现，无法保存绑定");
                return;
            }

            var json = sActionAsset.SaveBindingOverridesAsJson();
            sPersistence.Save(sPersistenceKey, json);
        }

        /// <summary>
        /// 加载绑定覆盖
        /// </summary>
        public static void LoadBindings()
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset，无法加载绑定");
                return;
            }

            if (sPersistence == default)
            {
                Debug.LogWarning("[InputKit] 未设置持久化实现，无法加载绑定");
                return;
            }

            if (!sPersistence.Exists(sPersistenceKey)) return;

            var json = sPersistence.Load(sPersistenceKey);
            if (!string.IsNullOrEmpty(json))
            {
                sActionAsset.LoadBindingOverridesFromJson(json);
            }
        }

        /// <summary>
        /// 清除保存的绑定
        /// </summary>
        public static void ClearSavedBindings()
        {
            if (sPersistence == default)
            {
                Debug.LogWarning("[InputKit] 未设置持久化实现");
                return;
            }

            sPersistence.Delete(sPersistenceKey);
        }

        /// <summary>
        /// 导出绑定为 JSON
        /// </summary>
        /// <returns>绑定覆盖的 JSON 字符串</returns>
        public static string ExportBindingsJson()
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset");
                return string.Empty;
            }

            return sActionAsset.SaveBindingOverridesAsJson();
        }

        /// <summary>
        /// 从 JSON 导入绑定
        /// </summary>
        /// <param name="json">绑定覆盖的 JSON 字符串</param>
        public static void ImportBindingsJson(string json)
        {
            if (sActionAsset == default)
            {
                Debug.LogWarning("[InputKit] 未设置 InputActionAsset");
                return;
            }

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[InputKit] JSON 字符串为空");
                return;
            }

            sActionAsset.LoadBindingOverridesFromJson(json);
            SaveBindings();
        }
    }
}

#endif