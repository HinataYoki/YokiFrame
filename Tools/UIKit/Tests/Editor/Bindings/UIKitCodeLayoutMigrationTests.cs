#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace YokiFrame.Tests
{
    /// <summary>使用隔离资源验证纯目录迁移不改字节、不丢 GUID，冲突与失败保持可恢复。</summary>
    public sealed class UIKitCodeLayoutMigrationTests
    {
        private const string TEST_ROOT = "Assets/__UIKitLayoutMigrationTests__";

        /// <summary>每项测试结束清除测试事务与其精确资源根，避免污染后续测试或用户迁移。</summary>
        [TearDown]
        public void TearDown()
        {
            UIKitCodeLayoutMigrationTransaction.Save(null);
            if (AssetDatabase.IsValidFolder(TEST_ROOT)) AssetDatabase.DeleteAsset(TEST_ROOT);
        }

        /// <summary>父目录与内部 Element 分步移动后，伴随文件和各级 .meta 均保持原字节与 GUID。</summary>
        [Test]
        public void MovesPreserveBytesGuidsAndCanRollback()
        {
            var plan = CreatePlan();
            string guid = AssetDatabase.AssetPathToGUID(TEST_ROOT + "/InventoryPanel/UIElement/Item.txt");
            var record = new UIKitCodeLayoutMigrationTransaction.Record { moves = plan.Moves, files = plan.Files };
            try
            {
                UIKitCodeLayoutMigrationTransaction.Apply(record);
                UIKitCodeLayoutMigrationTransaction.Verify(record);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(TEST_ROOT + "/Panel/InventoryPanel/Element/Item.txt"));
                UIKitCodeLayoutMigrationTransaction.Restore(record);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(TEST_ROOT + "/InventoryPanel/UIElement/Item.txt"));
                Assert.IsFalse(Directory.Exists(TEST_ROOT + "/Panel"));
            }
            finally { UIKitCodeLayoutMigrationTransaction.Save(null); }
        }

        /// <summary>目标冲突在计划构建阶段就被拒绝，源文件内容和位置完全不变。</summary>
        [Test]
        public void ConflictRejectsBeforeAnyMove()
        {
            CreatePlan();
            Directory.CreateDirectory(TEST_ROOT + "/Panel/InventoryPanel");
            File.WriteAllText(TEST_ROOT + "/Panel/InventoryPanel/existing.txt", "target");
            AssetDatabase.Refresh();
            var plan = new UIKitCodeLayoutMigrationPlan(TEST_ROOT);
            Assert.Throws<InvalidOperationException>(() => plan.Add(TEST_ROOT + "/InventoryPanel", TEST_ROOT + "/Panel/InventoryPanel"));
            Assert.AreEqual("owner", File.ReadAllText(TEST_ROOT + "/InventoryPanel/Owner.txt"));
        }

        /// <summary>首步成功后第二步意外冲突，记录仅含成功移动，移除故障后可以完整逆序恢复。</summary>
        [Test]
        public void FailedLaterMoveRestoresEarlierMove()
        {
            var plan = CreatePlan();
            var record = new UIKitCodeLayoutMigrationTransaction.Record { moves = plan.Moves, files = plan.Files };
            record.moves[1].destination = record.moves[1].source;
            try
            {
                Assert.Throws<IOException>(() => UIKitCodeLayoutMigrationTransaction.Apply(record));
                Assert.AreEqual(1, record.completed);
                UIKitCodeLayoutMigrationTransaction.Restore(record);
                Assert.AreEqual("element", File.ReadAllText(TEST_ROOT + "/InventoryPanel/UIElement/Item.txt"));
            }
            finally { UIKitCodeLayoutMigrationTransaction.Save(null); }
        }

        /// <summary>含程序集定义的目录不能自动迁移，防止改变脚本所属程序集。</summary>
        [Test]
        public void AssemblyBoundaryFileRejectsPlan()
        {
            CreatePlan();
            File.WriteAllText(TEST_ROOT + "/InventoryPanel/custom.asmref", "{}");
            var plan = new UIKitCodeLayoutMigrationPlan(TEST_ROOT);
            plan.Add(TEST_ROOT + "/InventoryPanel", TEST_ROOT + "/Panel/InventoryPanel");
            Assert.That(Assert.Throws<InvalidOperationException>(() => plan.Validate()).Message, Does.Contain("程序集定义"));
            File.Delete(TEST_ROOT + "/InventoryPanel/custom.asmref");
        }

        /// <summary>新布局不再产生移动计划，重复迁移保持幂等。</summary>
        [Test]
        public void NewLayoutProducesNoMoves()
        {
            Directory.CreateDirectory(TEST_ROOT + "/Panel/InventoryPanel/Element");
            File.WriteAllText(TEST_ROOT + "/Panel/InventoryPanel/Element/Item.txt", "unchanged");
            AssetDatabase.Refresh();
            UIKitCodeLayoutMigrationPlan plan = UIKitCodeLayoutMigration.Build(TEST_ROOT);
            Assert.AreEqual(0, plan.Moves.Count);
            Assert.AreEqual(0, plan.Files.Count);
        }

        /// <summary>建立最小真实 AssetDatabase 测试目录并冻结两步移动的原始文件证据。</summary>
        private static UIKitCodeLayoutMigrationPlan CreatePlan()
        {
            Directory.CreateDirectory(TEST_ROOT + "/InventoryPanel/UIElement");
            File.WriteAllText(TEST_ROOT + "/InventoryPanel/Owner.txt", "owner");
            File.WriteAllText(TEST_ROOT + "/InventoryPanel/UIElement/Item.txt", "element");
            AssetDatabase.Refresh();
            var plan = new UIKitCodeLayoutMigrationPlan(TEST_ROOT);
            plan.Add(TEST_ROOT + "/InventoryPanel", TEST_ROOT + "/Panel/InventoryPanel");
            plan.Add(TEST_ROOT + "/Panel/InventoryPanel/UIElement", TEST_ROOT + "/Panel/InventoryPanel/Element");
            plan.Validate();
            return plan;
        }
    }
}
#endif
