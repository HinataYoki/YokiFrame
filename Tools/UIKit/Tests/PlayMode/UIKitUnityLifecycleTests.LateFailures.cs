#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit Root teardown 后迟到 loader 与 lease 失败仍保留可诊断证据。
    /// </summary>
    public sealed partial class UIKitUnityLifecycleTests
    {
        /// <summary>验证公开等待已取消后，迟到 loader fault 不会被完成状态静默吞掉。</summary>
        [UnityTest]
        public IEnumerator LateLoaderFailureAfterDisposeIsLogged()
        {
            var deferredLoader = new IgnoringCancellationPanelLoader(mPrefab);
            UIKit.SetPanelLoader(deferredLoader);
            Task<UIKitPlayModePanel> pending = AsTask(UIKit.OpenPanelAsync<UIKitPlayModePanel>());

            UIRoot.Dispose();
            LogAssert.Expect(LogType.Error, new Regex("UIKit late loader failure", RegexOptions.CultureInvariant));
            deferredLoader.Fail(new InvalidOperationException("UIKit late loader failure"));
            yield return new WaitUntil(() => pending.IsCompleted);
            yield return null;

            Assert.IsTrue(pending.IsCanceled);
            Assert.IsFalse(UIKit.HasRoot);
        }

        /// <summary>验证迟到成功 lease 在清理时抛错会记录诊断，而不是被已取消 Task 吞掉。</summary>
        [UnityTest]
        public IEnumerator LateLeaseReleaseFailureAfterDisposeIsLogged()
        {
            var deferredLoader = new IgnoringCancellationPanelLoader(mPrefab);
            UIKit.SetPanelLoader(deferredLoader);
            Task<UIKitPlayModePanel> pending = AsTask(UIKit.OpenPanelAsync<UIKitPlayModePanel>());

            UIRoot.Dispose();
            LogAssert.Expect(LogType.Error, new Regex("UIKit late lease release failure", RegexOptions.CultureInvariant));
            deferredLoader.CompleteWithThrowingLease(
                new InvalidOperationException("UIKit late lease release failure"));
            yield return new WaitUntil(() => pending.IsCompleted);
            yield return null;

            Assert.IsTrue(pending.IsCanceled);
            Assert.IsFalse(UIKit.HasRoot);
        }
    }
}
#endif
