#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
    /// <summary>
    /// 场景侧 YooAsset 初始化组件。
    /// </summary>
    public sealed class YooInitBehaviour : MonoBehaviour
    {
        [SerializeField] private YooInitConfig mConfig = new YooInitConfig();
        [SerializeField] private bool mInitOnStart = true;

        public YooInitConfig Config => mConfig;

        private void Start()
        {
            if (!mInitOnStart)
                return;

            StartInit();
        }

        public void StartInit()
        {
#if YOKIFRAME_UNITASK_SUPPORT
            InitWithUniTask().Forget();
#else
            _ = InitWithTask();
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private async UniTaskVoid InitWithUniTask()
        {
            try
            {
                await YooInit.InitAsync(mConfig, this.GetCancellationTokenOnDestroy());
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception, this);
            }
        }
#else
        private async System.Threading.Tasks.Task InitWithTask()
        {
            try
            {
                await YooInit.InitAsync(mConfig);
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception, this);
            }
        }
#endif
    }
}
#endif
#endif
