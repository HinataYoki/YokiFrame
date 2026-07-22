#if GODOT
using System.IO;
using System.Runtime.CompilerServices;
using Godot;

#pragma warning disable CA2255
namespace YokiFrame.Godot
{
    /// <summary>在 Godot Runtime 程序集加载时注册 JSON 和用户数据目录后端工厂。</summary>
    public static class GodotSaveKitRuntimeInstaller
    {
        /// <summary>模块加载时注册默认后端工厂；实例化延迟到 SaveKit 首次业务调用。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaults()
        {
            EnsureInstalled();
        }

        /// <summary>显式确保默认 SaveKit 后端已经安装。</summary>
        public static void EnsureInstalled()
        {
            SaveKit.RegisterDefaultBackendFactory(
                CreateStorage,
                () => new JsonSaveSerializer(new GodotJsonSaveCodec(), 1));
        }

        /// <summary>读取 Godot ProjectSettings 并创建当前项目的默认存档目录。</summary>
        private static ISaveStorage CreateStorage()
        {
            string configuredPath = KitSettings.GetString("SaveKit", "storagePath", "");
            string extension = KitSettings.GetString("SaveKit", "fileExtension", ".yoki");
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
