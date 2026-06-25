#if !GODOT
using System.IO;
using UnityEngine;
using SaveKitApi = YokiFrame.SaveKit;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将 Unity 持久化目录注入 SaveKit，保持跨引擎静态入口一致。
    /// </summary>
    public static class UnitySaveKitInstaller
    {
        public static void Install(IResourceProvider provider)
        {
            var rootPath = Path.Combine(Application.persistentDataPath, "YokiFrame", "Saves");
            SaveKitApi.SetStorage(new FileSaveStorage(rootPath));
            SaveKitApi.SetSerializer(new SerializationProviderSaveSerializer(new UnityEngineSerializationProvider()));
        }

        public static bool TickAutoSave(float deltaSeconds)
        {
            return SaveKitApi.TickAutoSave(deltaSeconds);
        }
    }
}
#endif
