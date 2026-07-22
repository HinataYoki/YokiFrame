using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit 命名栈恢复、层级排序和模态 blocker 的公开行为。
    /// </summary>
    public sealed class UIKitNavigationAndLevelTests
    {
        private const string FLOW_STACK = "flow";
        private UIKitTestPanelLoader mLoader;

        /// <summary>
        /// 每个测试安装全新 Root 和内存 loader，确保栈与层级索引彼此隔离。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
            mLoader = new UIKitTestPanelLoader();
            UIKit.SetPanelLoader(mLoader);
        }

        /// <summary>
        /// 每个测试先释放全部受管面板，再销毁测试 Prefab。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
            mLoader.Dispose();
            mLoader = null;
        }

        /// <summary>
        /// 验证 Push 隐藏旧栈顶，Pop 清除成员关系并恢复旧栈顶的 Show、Resume、Focus。
        /// </summary>
        [Test]
        public void PushAndPopRestorePreviousPanelAndClearMembership()
        {
            UIKitNavigationFirstTestPanel first = OpenFirstPanel();
            Assert.IsNull(first.StackName, "普通 Open 不应隐式加入默认栈。");
            Assert.IsNull(UIKit.GetPanelStackName(first));

            UIKit.PushPanel(first, FLOW_STACK);
            UIKitNavigationSecondTestPanel second = OpenSecondPanel();
            UIKit.PushPanel(second, FLOW_STACK);

            Assert.AreEqual(PanelState.Hide, first.State);
            Assert.AreEqual(1, first.FocusCount);
            Assert.AreEqual(1, first.BlurCount);
            Assert.AreEqual(PanelState.Open, second.State);
            Assert.AreEqual(FLOW_STACK, second.StackName);
            Assert.AreEqual(2, UIKit.GetStackDepth(FLOW_STACK));
            Assert.AreSame(second, UIKit.PeekPanel(FLOW_STACK));
            CollectionAssert.Contains(UIKit.GetAllStackNames().ToArray(), FLOW_STACK);

            IPanel popped = UIKit.PopPanel(FLOW_STACK, showPrevious: true, autoClose: false);

            Assert.AreSame(second, popped);
            Assert.IsNull(second.StackName);
            Assert.IsFalse(UIKit.IsInStack(second));
            Assert.AreEqual(PanelState.Open, first.State);
            Assert.AreEqual(1, first.ResumeCount);
            Assert.AreEqual(2, first.FocusCount);
            Assert.AreEqual(1, UIKit.GetStackDepth(FLOW_STACK));
            Assert.AreSame(first, UIKit.PeekPanel(FLOW_STACK));
        }

        /// <summary>
        /// 验证直接关闭当前栈顶与 Pop 使用同一恢复路径，不会遗留栈名或隐藏旧栈顶。
        /// </summary>
        [Test]
        public void ClosingStackTopRestoresPreviousPanel()
        {
            UIKitNavigationFirstTestPanel first = OpenFirstPanel();
            UIKit.PushPanel(first, FLOW_STACK);
            UIKitNavigationSecondTestPanel second = OpenSecondPanel();
            UIKit.PushPanel(second, FLOW_STACK);

            second.Close();

            Assert.AreEqual(PanelState.Cached, second.State);
            Assert.IsNull(second.StackName);
            Assert.IsFalse(UIKit.IsInStack(second));
            Assert.AreEqual(PanelState.Open, first.State);
            Assert.AreEqual(1, first.ResumeCount);
            Assert.AreEqual(2, first.FocusCount);
            Assert.AreEqual(1, UIKit.GetStackDepth(FLOW_STACK));
            Assert.AreSame(first, UIKit.PeekPanel(FLOW_STACK));
        }

        /// <summary>
        /// 验证旧栈顶在 OnBlur 自关闭时，Push 会重新读取栈索引并只登记新的有效栈顶。
        /// </summary>
        [Test]
        public void PushSurvivesPreviousTopClosingItselfDuringBlur()
        {
            UIKitNavigationFirstTestPanel first = OpenFirstPanel();
            first.CloseOnBlur = true;
            UIKit.PushPanel(first, FLOW_STACK);
            UIKitNavigationSecondTestPanel second = OpenSecondPanel();

            UIKit.PushPanel(second, FLOW_STACK);

            Assert.AreEqual(PanelState.Cached, first.State);
            Assert.IsFalse(UIKit.IsInStack(first));
            Assert.IsTrue(UIKit.IsInStack(second));
            Assert.AreEqual(FLOW_STACK, second.StackName);
            Assert.AreEqual(1, UIKit.GetStackDepth(FLOW_STACK));
            Assert.AreSame(second, UIKit.PeekPanel(FLOW_STACK));
        }

        /// <summary>
        /// 验证层内顶部与全局顶部排序，并确保模态 blocker 始终紧邻所属面板下方。
        /// </summary>
        [Test]
        public void LevelsChooseTopPanelAndModalBlockerIsAdjacentBelowOwner()
        {
            UIKitNavigationFirstTestPanel first = OpenFirstPanel();
            UIKitNavigationSecondTestPanel second = OpenSecondPanel();

            Assert.AreSame(second, UIKit.GetTopPanelAtLevel(UILevel.Common));
            UIKit.SetPanelSubLevel(first, 10);
            Assert.AreSame(first, UIKit.GetTopPanelAtLevel(UILevel.Common));

            UIKit.SetPanelLevel(second, UILevel.Toast);
            Assert.AreSame(second, UIKit.GetGlobalTopPanel());
            Assert.AreSame(first, UIKit.GetTopPanelAtLevel(UILevel.Common));
            Assert.AreSame(second, UIKit.GetTopPanelAtLevel(UILevel.Toast));

            UIKit.SetPanelModal(first, true);

            Assert.IsTrue(first.IsModal);
            Assert.IsTrue(UIKit.HasModalBlocker());
            Transform panelTransform = first.Transform;
            Transform parent = panelTransform.parent;
            int panelIndex = panelTransform.GetSiblingIndex();
            Assert.Greater(panelIndex, 0, "模态面板下方必须存在一个独立 blocker sibling。");
            Transform blocker = parent.GetChild(panelIndex - 1);
            Assert.AreEqual(first.PanelName + ".ModalBlocker", blocker.name);
            Assert.AreEqual(panelIndex - 1, blocker.GetSiblingIndex());

            UIKit.SetPanelModal(first, false);
            Assert.IsFalse(first.IsModal);
            Assert.IsFalse(UIKit.HasModalBlocker());
        }

        /// <summary>
        /// 打开第一个 Persistent 导航面板，便于关闭后继续检查公开状态。
        /// </summary>
        /// <returns>当前受管的第一个导航面板。</returns>
        private static UIKitNavigationFirstTestPanel OpenFirstPanel()
        {
            return UIKit.OpenPanel<UIKitNavigationFirstTestPanel>(
                cachePolicy: PanelCachePolicy.Persistent);
        }

        /// <summary>
        /// 打开第二个 Persistent 导航面板，便于关闭后继续检查公开状态。
        /// </summary>
        /// <returns>当前受管的第二个导航面板。</returns>
        private static UIKitNavigationSecondTestPanel OpenSecondPanel()
        {
            return UIKit.OpenPanel<UIKitNavigationSecondTestPanel>(
                cachePolicy: PanelCachePolicy.Persistent);
        }
    }
}
