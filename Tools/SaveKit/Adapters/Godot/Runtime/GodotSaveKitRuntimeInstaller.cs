#if GODOT
using System.IO;
using System.Runtime.CompilerServices;
using Godot;

#pragma warning disable CA2255
namespace YokiFrame.Godot
{
    /// <summary>在 Godot Runtime 程序集加载时注册 JSON 和用户数据目录后端工厂，并把存档状态交给统一会话重置。</summary>
    public static class GodotSaveKitRuntimeInstaller
    {
        /// <summary>模块加载时注册默认后端工厂和会话重置；实例化延迟到 SaveKit 首次业务调用。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaults()
        {
            EnsureInstalled();
        }

        /// <summary>显式确保默认 SaveKit 后端和会话重置已经登记。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameSession.Register(YokiFrameSession.RELEASE_HOSTS_ORDER, "savekit", SaveKit.Reset);
            SaveKit.RegisterDefaultBackendFactory(
                CreateStorage,
                () => new JsonSaveSerializer(new GodotJsonSaveCodec(), 1));
        }

        /// <summary>读取 Godot ProjectSettings 并创建当前项目的默认存档目录。</summary>
        private static ISaveStorage CreateStorage()
        {
            string configuredPath = KitSettings.GetString(SaveKitSettings.KIT_NAME, SaveKitSettings.STORAGE_PATH_KEY, "");
            string extension = KitSettings.GetString(SaveKitSettings.KIT_NAME, SaveKitSettings.FILE_EXTENSION_KEY, SaveKitSettings.DEFAULT_FILE_EXTENSION);
            string root = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(OS.GetUserDataDir(), "YokiFrame", "Saves")
                : configuredPath.Replace("${userDataDir}", OS.GetUserDataDir());
            if (!Path.IsPathRooted(root)) root = Path.Combine(OS.GetUserDataDir(), root);
            return new FileSaveStorage(root, extension);
        }
    }
}
#pragma warning restore CA2255
#endif
