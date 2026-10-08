#if UNITY_2022_3_OR_NEWER
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit 默认 ResKit loader 的资源 location 选择契约。
    /// </summary>
    public sealed class UIKitPanelLoaderTests
    {
        /// <summary>
        /// 验证默认 loader 可切换为仅使用 Panel 类型名的可寻址 location。
        /// </summary>
        [Test]
        public void ResKitPanelLoaderCanUsePanelTypeNameAsAddressableLocation()
        {
            UIRoot.Dispose();
            try
            {
                UIKit.SetPanelLoader(new ResKitPanelLoader("Art/UIPrefab", false));
                IPanelLoader loader = UIKit.GetPanelLoader();
                var resKitLoader = loader as ResKitPanelLoader;
                Assert.IsNotNull(resKitLoader);
                Assert.IsFalse(loader.UseAddressableLocation);
                Assert.AreEqual(
                    "Art/UIPrefab/UIKitLifecycleTestPanel",
                    resKitLoader.BuildLocation(typeof(UIKitLifecycleTestPanel)));

                loader.UseAddressableLocation = true;

                Assert.AreEqual(
                    nameof(UIKitLifecycleTestPanel),
                    resKitLoader.BuildLocation(typeof(UIKitLifecycleTestPanel)));
            }
            finally
            {
                UIRoot.Dispose();
            }
        }
    }
}
#endif
