namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 与 SaveKit 的集成
    /// 提供语言偏好的持久化功能
    /// </summary>
    public static class LocalizationKitSaveIntegration
    {
        /// <summary>
        /// 模块 Key（用于 SaveData）
        /// </summary>
        public const int MODULE_KEY = 0x4C4F4341; // "LOCA" in hex

        /// <summary>
        /// 保存当前语言设置到 SaveData
        /// </summary>
        /// <param name="saveData">存档数据</param>
        public static void SaveLanguagePreference(SaveData saveData)
        {
            if (saveData == null) return;

            var data = LocalizationSaveData.FromCurrentSettings();
            var serializer = SaveKit.GetSerializer();
            var bytes = serializer.Serialize(data);
            saveData.SetRawModule(MODULE_KEY, bytes);
        }

        /// <summary>
        /// 从 SaveData 加载语言设置
        /// </summary>
        /// <param name="saveData">存档数据</param>
        /// <returns>是否成功加载</returns>
        public static bool LoadLanguagePreference(SaveData saveData)
        {
            if (saveData == null) return false;

            var bytes = saveData.GetRawModule(MODULE_KEY);
            if (bytes == null) return false;

            var serializer = SaveKit.GetSerializer();
            var data = serializer.Deserialize<LocalizationSaveData>(bytes);
            data.Apply();
            return true;
        }

        /// <summary>
        /// 检查 SaveData 是否包含语言设置
        /// </summary>
        public static bool HasLanguagePreference(SaveData saveData)
        {
            return saveData?.HasRawModule(MODULE_KEY) ?? false;
        }

        /// <summary>
        /// 清除 SaveData 中的语言设置
        /// </summary>
        public static void ClearLanguagePreference(SaveData saveData)
        {
            saveData?.RemoveRawModule(MODULE_KEY);
        }
    }
}
