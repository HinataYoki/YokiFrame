#if UNITY_2022_3_OR_NEWER
using System;
using System.Reflection;
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
                Assert.IsNotNull(loader);
                Assert.IsFalse(loader.UseAddressableLocation);

                MethodInfo buildLocation = typeof(ResKitPanelLoader).GetMethod(
                    "BuildLocation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(buildLocation, "默认 loader 必须保持统一的 ResKit location 构建路径。");
                Assert.AreEqual(
                    "Art/UIPrefab/UIKitLifecycleTestPanel",
                    buildLocation.Invoke(loader, new object[] { typeof(UIKitLifecycleTestPanel) }));

                loader.UseAddressableLocation = true;

                Assert.AreEqual(
                    nameof(UIKitLifecycleTestPanel),
                    buildLocation.Invoke(loader, new object[] { typeof(UIKitLifecycleTestPanel) }));
            }
            finally
            {
                UIRoot.Dispose();
            }
        }
    }
}
#endif
