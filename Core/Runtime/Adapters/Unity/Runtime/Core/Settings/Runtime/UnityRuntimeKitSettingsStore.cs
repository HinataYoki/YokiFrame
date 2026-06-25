#if !GODOT
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Base KitSettings 存储映射到 Unity Runtime Settings 资产。
    /// </summary>
    public sealed class UnityRuntimeKitSettingsStore : IKitSettingsStore
    {
        private readonly YokiFrameRuntimeSettings mSettings;
        private readonly bool mSaveAssets;
        private readonly Action mChanged;

        /// <summary>
        /// 创建 Runtime Settings 存储。
        /// </summary>
        /// <param name="settings">Runtime Settings 资产。</param>
        /// <param name="saveAssets">写入后是否保存 Unity 资产。</param>
        public UnityRuntimeKitSettingsStore(YokiFrameRuntimeSettings settings, bool saveAssets)
            : this(settings, saveAssets, null)
        {
        }

        /// <summary>
        /// 创建 Runtime Settings 存储。
        /// </summary>
        /// <param name="settings">Runtime Settings 资产。</param>
        /// <param name="saveAssets">写入后是否保存 Unity 资产。</param>
        /// <param name="changed">配置变更后的回调。</param>
        public UnityRuntimeKitSettingsStore(YokiFrameRuntimeSettings settings, bool saveAssets, Action changed)
        {
            mSettings = settings;
            mSaveAssets = saveAssets;
            mChanged = changed;
        }

        /// <summary>
        /// 尝试读取指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        /// <param name="value">读取到的配置值。</param>
        /// <returns>存在配置值时返回 true。</returns>
        public bool TryGetValue(string kit, string key, out string value)
        {
            value = null;
            return mSettings != null && mSettings.TryGetValue(kit, key, out value);
        }

        /// <summary>
        /// 设置指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        /// <param name="value">配置值。</param>
        public void SetValue(string kit, string key, string value)
        {
            if (mSettings == null)
                return;

            mSettings.SetValue(kit, key, value);
            SaveIfNeeded();
            if (mChanged != null)
                mChanged();
        }

        /// <summary>
        /// 移除指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        public void RemoveValue(string kit, string key)
        {
            if (mSettings == null)
                return;

            mSettings.RemoveValue(kit, key);
            SaveIfNeeded();
            if (mChanged != null)
                mChanged();
        }

        private void SaveIfNeeded()
        {
#if UNITY_EDITOR
            if (!mSaveAssets || mSettings == null)
                return;

            EditorUtility.SetDirty(mSettings);
            AssetDatabase.SaveAssets();
#endif
        }
    }
}
#endif
