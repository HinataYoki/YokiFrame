using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频管理工具 - 静态入口类
    /// </summary>
    public static partial class AudioKit
    {
        /// <summary>
        /// 当前音频后端实例，负责实际的音频播放逻辑
        /// </summary>
        private static IAudioBackend sBackend;

        /// <summary>
        /// 全局配置，包含最大并发数、对象池大小、各通道默认音量等
        /// </summary>
        private static AudioKitConfig sConfig;

        /// <summary>
        /// 路径解析器，将 audioId 转换为资源路径
        /// </summary>
        private static Func<int, string> sPathResolver;

        /// <summary>
        /// 音频加载池，用于自定义资源加载方式
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static IAudioLoaderPool sLoaderPool = new DefaultAudioLoaderUniTaskPool();
#else
        private static IAudioLoaderPool sLoaderPool = new DefaultAudioLoaderPool();
#endif

        /// <summary>
        /// 各通道音量缓存，key 为通道 ID（0-4 内置，5+ 自定义）
        /// </summary>
        private static readonly Dictionary<int, float> sChannelVolumes = new()
        {
            { 0, 1f }, { 1, 1f }, { 2, 1f }, { 3, 1f }, { 4, 1f }
        };

        /// <summary>
        /// 各通道静音状态缓存，key 为通道 ID
        /// </summary>
        private static readonly Dictionary<int, bool> sChannelMuted = new();

        /// <summary>
        /// 全局音量，影响所有通道的最终音量
        /// </summary>
        private static float sGlobalVolume = 1f;

        /// <summary>
        /// 全局静音状态
        /// </summary>
        private static bool sGlobalMuted;

        /// <summary>
        /// 是否已初始化后端
        /// </summary>
        private static bool sIsInitialized;

        /// <summary>
        /// 缓存的句柄列表，用于通道操作时避免 GC
        /// </summary>
        private static readonly List<IAudioHandle> sCachedHandleList = new(32);

        #region 配置

        /// <summary>
        /// 设置音频后端
        /// </summary>
        public static void SetBackend(IAudioBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            // 停止当前所有音频并释放资源
            if (sBackend != null)
            {
                sBackend.StopAll();
                sBackend.Dispose();
            }

            sBackend = backend;
            sBackend.Initialize(sConfig ?? AudioKitConfig.Default);
            sIsInitialized = true;

            KitLogger.Log($"[AudioKit] 后端已切换为: {backend.GetType().Name}");
        }

        /// <summary>
        /// 设置全局配置
        /// </summary>
        public static void SetConfig(AudioKitConfig config)
        {
            sConfig = config ?? AudioKitConfig.Default;

            // 同步通道音量
            sChannelVolumes[(int)AudioChannel.Bgm] = sConfig.BgmVolume;
            sChannelVolumes[(int)AudioChannel.Sfx] = sConfig.SfxVolume;
            sChannelVolumes[(int)AudioChannel.Voice] = sConfig.VoiceVolume;
            sChannelVolumes[(int)AudioChannel.Ambient] = sConfig.AmbientVolume;
            sChannelVolumes[(int)AudioChannel.UI] = sConfig.UIVolume;
            sGlobalVolume = sConfig.GlobalVolume;

            // 如果后端已初始化，重新初始化
            if (sBackend != null)
            {
                sBackend.Initialize(sConfig);
            }
        }

        /// <summary>
        /// 设置路径解析器
        /// </summary>
        public static void SetPathResolver(Func<int, string> resolver)
        {
            sPathResolver = resolver;
        }

        /// <summary>
        /// 设置自定义音频加载池（用于 YooAsset、Addressables 等扩展）
        /// </summary>
        public static void SetLoaderPool(IAudioLoaderPool loaderPool)
        {
            sLoaderPool = loaderPool ?? throw new ArgumentNullException(nameof(loaderPool));
            KitLogger.Log($"[AudioKit] 加载池已切换为: {loaderPool.GetType().Name}");
        }

        /// <summary>
        /// 获取当前音频加载池
        /// </summary>
        public static IAudioLoaderPool GetLoaderPool() => sLoaderPool;

        #endregion

        #region 更新

        /// <summary>
        /// 更新（驱动淡入淡出和 3D 跟随）
        /// </summary>
        public static void Update(float deltaTime)
        {
            sBackend?.Update(deltaTime);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置（测试用）
        /// </summary>
        public static void Reset()
        {
            if (sBackend != null)
            {
                sBackend.StopAll();
                sBackend.UnloadAll();
                sBackend.Dispose();
                sBackend = null;
            }

            sConfig = null;
            sPathResolver = null;
            sGlobalVolume = 1f;
            sGlobalMuted = false;
            sIsInitialized = false;

            // 重置加载池为默认实现
#if YOKIFRAME_UNITASK_SUPPORT
            sLoaderPool = new DefaultAudioLoaderUniTaskPool();
#else
            sLoaderPool = new DefaultAudioLoaderPool();
#endif

            // 重置通道状态
            sChannelVolumes.Clear();
            sChannelMuted.Clear();
            for (var i = 0; i < 5; i++)
            {
                sChannelVolumes[i] = 1f;
            }
        }

        #endregion

        #region 调试支持

#if UNITY_EDITOR
        /// <summary>
        /// [编辑器专用] 获取所有正在播放的音频句柄
        /// </summary>
        public static void GetAllPlayingHandles(List<IAudioHandle> result)
        {
            if (sBackend == null)
            {
                result.Clear();
                return;
            }
            sBackend.GetAllPlayingHandles(result);
        }
#endif

        #endregion

        #region 辅助方法

        private static void EnsureInitialized()
        {
            if (sIsInitialized) return;

#if UNITY_EDITOR
            // 编辑器模式下使用 EditorResLoaderPool，支持从 Assets 路径直接加载
            if (!(ResKit.GetLoaderPool() is EditorResLoaderPool))
            {
                ResKit.SetLoaderPool(new EditorResLoaderPool());
            }
#endif

            // 自动初始化默认后端
            SetBackend(new UnityAudioBackend());
        }

        private static string ResolvePath(int audioId)
        {
            if (sPathResolver != null)
            {
                return sPathResolver(audioId);
            }

            // 默认路径解析：假设 audioId 对应 Resources 下的路径
            // 实际项目中应该通过配置表获取路径
            return $"Audio/{audioId}";
        }

        private static float GetEffectiveGlobalVolume()
        {
            return sGlobalMuted ? 0f : sGlobalVolume;
        }

        #endregion
    }
}
