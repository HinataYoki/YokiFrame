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
        /// <summary>在 Unity 子系统重建时注册默认后端；实例化延迟到 SaveKit 首次业务调用。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefaults()
        {
            SaveKit.RegisterDefaultBackendFactory(
                CreateStorage,
                () => new JsonSaveSerializer(new UnityJsonSaveCodec(), 1));
        }

        /// <summary>读取 Runtime Settings 并创建当前 Unity 项目的默认存档目录。</summary>
        private static ISaveStorage CreateStorage()
        {
            string configuredPath = KitSettings.GetString("SaveKit", "storagePath", "");
            string extension = KitSettings.GetString("SaveKit", "fileExtension", ".yoki");
            string root = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Application.persistentDataPath, "YokiFrame", "Saves")
                : configuredPath.Replace("${persistentDataPath}", Application.persistentDataPath);
            if (!Path.IsPathRooted(root)) root = Path.Combine(Application.persistentDataPath, root);
            return new FileSaveStorage(root, extension);
        }
    }
}
#endif
