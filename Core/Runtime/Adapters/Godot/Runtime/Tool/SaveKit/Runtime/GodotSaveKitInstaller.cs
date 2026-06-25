#if GODOT
using System.IO;
using Godot;
using YokiFrame;
using YokiFrame;
using SaveKitApi = YokiFrame.SaveKit;

namespace YokiFrame.Godot
{
    /// <summary>
    /// 将 Godot 用户数据目录注入 SaveKit，保持 Unity/Godot 共用静态入口。
    /// </summary>
    public static class GodotSaveKitInstaller
    {
        public static void Install(IResourceProvider provider)
        {
            var rootPath = Path.Combine(OS.GetUserDataDir(), "YokiFrame", "Saves");
            SaveKitApi.SetStorage(new FileSaveStorage(rootPath));
            SaveKitApi.SetSerializer(new SerializationProviderSaveSerializer(new GodotEngineSerializationProvider()));
        }

        public static bool TickAutoSave(float deltaSeconds)
        {
            return SaveKitApi.TickAutoSave(deltaSeconds);
        }
    }
}
#endif
