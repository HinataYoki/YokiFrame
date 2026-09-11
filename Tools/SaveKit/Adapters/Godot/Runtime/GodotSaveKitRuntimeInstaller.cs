#if GODOT
using System.IO;
using System.Runtime.CompilerServices;
using Godot;

#pragma warning disable CA2255
namespace YokiFrame.Godot
{
    /// <summary>在 Godot Runtime 程序集加载时注册 JSON 和用户数据目录后端工厂，并在宿主代次结束时清理静态存档状态。</summary>
    public static class GodotSaveKitRuntimeInstaller
    {
        /// <summary>
        /// 稳定帧监听者；它不接收帧更新，只用于在宿主重置时清理 SaveKit 静态状态。
        /// </summary>
        private static readonly SaveKitHostResetListener sHostResetListener = new();

        /// <summary>标记监听者是否已注册，避免重复注册。</summary>
        private static bool sHostResetListenerRegistered;

        /// <summary>模块加载时注册默认后端工厂；实例化延迟到 SaveKit 首次业务调用。</summary>
        [ModuleInitializer]
        internal static void RegisterDefaults()
        {
            EnsureInstalled();
        }

        /// <summary>显式确保默认 SaveKit 后端与宿主重置监听者已经安装。</summary>
        public static void EnsureInstalled()
        {
            SaveKit.RegisterDefaultBackendFactory(
                CreateStorage,
                () => new JsonSaveSerializer(new GodotJsonSaveCodec(), 1));
            EnsureHostResetListener();
        }

        /// <summary>
        /// 注册宿主重置监听者；重复调用保持幂等。
        /// </summary>
        /// <remarks>
        /// 为什么必须存在：Godot 侧此前只有 <c>[ModuleInitializer]</c>（每次程序集加载一次），
        /// 而 <c>GodotBootstrap._ExitTree</c> 的复位清单不含 SaveKit，且 Core Adapter 不得引用 Tools，
        /// 因此 SaveKit 的静态状态（尤其自动保存的 <c>sAutoSaveData</c>/<c>sBeforeAutoSave</c>）会在
        /// 「未发生程序集重载」时跨会话残留。SaveKit 自身又没有 OnHostReset 路径。
        /// <para>
        /// <c>YokiFrameUpdateDispatcher.ResetListeners</c> 只通知而不移除静态注册，且 Godot 侧
        /// <c>GodotBootstrap._ExitTree</c> 会调用它，故这是 Tool 与 Core 边界下的正确挂载点
        /// （与 AudioKit 的 <c>AudioKitFrameListener</c> 同构）。
        /// </para>
        /// </remarks>
        private static void EnsureHostResetListener()
        {
            if (sHostResetListenerRegistered)
            {
                return;
            }

            YokiFrameUpdateDispatcher.Register(sHostResetListener);
            sHostResetListenerRegistered = true;
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

        /// <summary>
        /// 只用于接收宿主代次结束通知的帧监听者；不参与帧更新。
        /// </summary>
        private sealed class SaveKitHostResetListener : IYokiFrameUpdateListener
        {
            /// <summary>SaveKit 不依赖宿主帧驱动，因此帧更新为空实现。</summary>
            /// <param name="scaledDeltaTime">受宿主时间缩放影响的秒数。</param>
            /// <param name="unscaledDeltaTime">不受宿主时间缩放影响的秒数。</param>
            public void OnFrameUpdate(float scaledDeltaTime, float unscaledDeltaTime)
            {
            }

            /// <summary>宿主代次结束时停用自动保存并清除上一会话的 Storage/Serializer/Encryptor。</summary>
            public void OnHostReset() => SaveKit.Reset();
        }
    }
}
#pragma warning restore CA2255
#endif
