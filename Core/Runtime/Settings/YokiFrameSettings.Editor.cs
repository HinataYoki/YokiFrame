#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrameSettings 编辑器部分
    /// </summary>
    public partial class YokiFrameSettings
    {
        private static YokiFrameSettings LoadOrCreateInEditor()
        {
            var settings = AssetDatabase.LoadAssetAtPath<YokiFrameSettings>(ASSET_PATH);
            if (settings == default)
            {
                settings = CreateInstance<YokiFrameSettings>();

                // 确保目录存在
                if (!Directory.Exists(ASSET_DIR))
                {
                    Directory.CreateDirectory(ASSET_DIR);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(settings, ASSET_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[YokiFrame] 配置文件已创建: {ASSET_PATH}");
            }
            return settings;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        internal void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
    }
}
#endif
