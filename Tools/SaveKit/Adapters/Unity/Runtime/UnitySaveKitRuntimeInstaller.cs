#if UNITY_2022_3_OR_NEWER
using System.IO;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 为 Unity 注册默认 JSON 序列化器和持久化文件存储工厂。
    /// </summary>
    internal static class UnitySaveKitRuntimeInstaller
    {
        /// <summary>登记会话重置和默认后端工厂。工厂本身跨会话保留，实例在重置后按需重建。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefaults()
        {
            YokiFrameSession.Register(YokiFrameSession.RELEASE_HOSTS_ORDER, "savekit", SaveKit.Reset);
            SaveKit.RegisterDefaultBackendFactory(
                CreateStorage,
                () => new JsonSaveSerializer(new UnityJsonSaveCodec(), 1));
        }

        /// <summary>读取 Runtime Settings 并创建当前 Unity 项目的默认存档目录。</summary>
        private static ISaveStorage CreateStorage()
        {
            string configuredPath = KitSettings.GetString(SaveKitSettings.KIT_NAME, SaveKitSettings.STORAGE_PATH_KEY, "");
            string extension = KitSettings.GetString(SaveKitSettings.KIT_NAME, SaveKitSettings.FILE_EXTENSION_KEY, SaveKitSettings.DEFAULT_FILE_EXTENSION);
            string root = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Application.persistentDataPath, "YokiFrame", "Saves")
                : configuredPath.Replace("${persistentDataPath}", Application.persistentDataPath);
            if (!Path.IsPathRooted(root)) root = Path.Combine(Application.persistentDataPath, root);
            return new FileSaveStorage(root, extension);
        }
    }
}
#endif
