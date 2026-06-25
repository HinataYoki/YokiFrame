#if !GODOT
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Runtime Settings 中保存的通用 Kit 配置项。
    /// </summary>
    [Serializable]
    public sealed class YokiFrameRuntimeSettingEntry
    {
        /// <summary>
        /// Kit 名称。
        /// </summary>
        public string Kit;

        /// <summary>
        /// 配置 key。
        /// </summary>
        public string Key;

        /// <summary>
        /// 配置值。
        /// </summary>
        public string Value;
    }
}
#endif
