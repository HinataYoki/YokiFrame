#if UNITY_EDITOR || UNITY_5_3_OR_NEWER
namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Runtime Settings 的路径常量权威源，确保 Editor 写入路径与 Runtime 读取路径保持一致。
    /// </summary>
    public static class UnityYokiFrameRuntimeSettingsPaths
    {
        /// <summary>Resources 相对路径，不包含文件扩展名。</summary>
        private const string RESOURCES_RELATIVE_PATH = "YokiFrame/runtime-settings";

        /// <summary>Unity 项目内运行时配置的稳定 Asset 路径。</summary>
        public const string ASSET_PATH = "Assets/Settings/Resources/" + RESOURCES_RELATIVE_PATH + ".json";

        /// <summary>Runtime 通过 Resources.Load 使用的路径（不含扩展名）。</summary>
        public const string RESOURCES_PATH = RESOURCES_RELATIVE_PATH;
    }
}
#endif
