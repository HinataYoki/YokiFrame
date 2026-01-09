namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 与 SaveKit 的集成
    /// 提供语言偏好的持久化功能
    /// </summary>
    public static class LocalizationKitSaveIntegration
    {
        /// <summary>
        /// 保存当前语言设置到 SaveData
        /// </summary>
        /// <param name="saveData">存档数据</param>
        public static void SaveLanguagePreference(SaveData saveData)
        {
            if (saveData == null) return;

            var data = LocalizationSaveData.FromCurrentSettings();
            saveData.RegisterModule(data);
        }

        /// <summary>
        /// 从 SaveData 加载语言设置
        /// </summary>
        /// <param name="saveData">存档数据</param>
        /// <returns>是否成功加载</returns>
        public static bool LoadLanguagePreference(SaveData saveData)
        {
            if (saveData == null) return false;

            var data = saveData.GetModule<LocalizationSaveData>();
            if (data == null) return false;

            data.Apply();
            return true;
        }

        /// <summary>
        /// 检查 SaveData 是否包含语言设置
        /// </summary>
        public static bool HasLanguagePreference(SaveData saveData)
        {
            return saveData?.HasModule<LocalizationSaveData>() ?? false;
        }

        /// <summary>
        /// 清除 SaveData 中的语言设置
        /// </summary>
        public static void ClearLanguagePreference(SaveData saveData)
        {
            saveData?.RemoveModule<LocalizationSaveData>();
        }
    }
}
