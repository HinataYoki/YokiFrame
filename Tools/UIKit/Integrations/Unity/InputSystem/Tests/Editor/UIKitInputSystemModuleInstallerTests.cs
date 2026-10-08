#if UNITY_2022_3_OR_NEWER && YOKIFRAME_INPUTSYSTEM_SUPPORT && !ENABLE_LEGACY_INPUT_MANAGER
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YokiFrame.Tests
{
    /// <summary>验证 Input System-only 项目中 UIKit 输入模块安装器的真实行为。</summary>
    public sealed class UIKitInputSystemModuleInstallerTests
    {
        /// <summary>每个测试释放 UIRoot，避免复用上一轮 EventSystem。</summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
        }

        /// <summary>每个测试释放 UIRoot，避免残留输入模块。</summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
        }

        /// <summary>
        /// 验证安装器注册的工厂会在 Root 初始化时创建 Input System UI 模块，并带上动作资源。
        /// actionsAsset 通过公开属性反射读取，因为这里只持有 BaseInputModule，具体类型属于 Unity Input System。
        /// </summary>
        [Test]
        public void InstallForTests_仅新输入系统_创建输入模块并带动作资源()
        {
            UIKitInputSystemModuleInstaller.InstallForTests();

            UIRoot root = UIRoot.Instance;
            BaseInputModule inputModule = root.EventSystem.GetComponent<BaseInputModule>();

            Assert.IsNotNull(inputModule);
            Assert.AreEqual(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                inputModule.GetType().FullName);
            PropertyInfo actionsAssetProperty = inputModule.GetType().GetProperty("actionsAsset");
            Assert.IsNotNull(actionsAssetProperty);
            Assert.IsNotNull(actionsAssetProperty.GetValue(inputModule));
        }
    }
}
#endif
