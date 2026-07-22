#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Threading;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    /// <summary>
    /// 场景侧 YooAsset 初始化编排组件。
    /// 它只负责生命周期和取消，不把初始化规则复制到 MonoBehaviour。
    /// </summary>
    public sealed class YooAssetInitializationBehaviour : MonoBehaviour
    {
        [SerializeField] private YooAssetInitializationOptions mOptions = new();
        [SerializeField] private bool mInitializeOnStart = true;

        private CancellationTokenSource mCancellationTokenSource;

        /// <summary>获取当前场景组件使用的 YooAsset 初始化参数。</summary>
        public YooAssetInitializationOptions Options => mOptions;

        /// <summary>获取或设置组件是否在 Start 时自动初始化。</summary>
        public bool InitializeOnStart
        {
            get => mInitializeOnStart;
            set => mInitializeOnStart = value;
        }

        /// <summary>创建绑定当前组件生命周期的取消源。</summary>
        private void Awake()
        {
            mCancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>按组件配置决定是否在场景启动时开始 YooAsset 初始化。</summary>
        private void Start()
        {
            if (mInitializeOnStart)
                StartInitialization();
        }

        /// <summary>启动一次异步初始化；重复调用由 YooAssetInitializer 统一拒绝。</summary>
        public void StartInitialization()
        {
#if YOKIFRAME_UNITASK_SUPPORT
            InitializeWithUniTask().Forget();
#else
            _ = InitializeWithTask();
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>执行 UniTask 初始化并在组件销毁时取消等待。</summary>
        private async UniTaskVoid InitializeWithUniTask()
#else
        /// <summary>执行 Task 初始化并在组件销毁时取消等待。</summary>
        private async Task InitializeWithTask()
#endif
        {
            try
            {
                CancellationToken token = mCancellationTokenSource == null
                    ? CancellationToken.None
                    : mCancellationTokenSource.Token;
                await YooAssetInitializer.InitializeAsync(mOptions, token);
            }
            catch (OperationCanceledException)
            {
                // 组件销毁时取消属于正常生命周期路径，不记录为初始化错误。
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception, this);
            }
        }

        /// <summary>销毁组件时取消仍在等待的 YooAsset 操作。</summary>
        private void OnDestroy()
        {
            if (mCancellationTokenSource == null)
                return;

            mCancellationTokenSource.Cancel();
            mCancellationTokenSource.Dispose();
            mCancellationTokenSource = null;
        }
    }
}
#endif
