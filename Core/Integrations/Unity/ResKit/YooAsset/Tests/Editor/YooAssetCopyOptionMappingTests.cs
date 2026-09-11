#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using YokiFrame.Unity;
using YooAsset.Editor;

namespace YokiFrame.Unity.Tests
{
    /// <summary>
    /// 守护首包拷贝策略的「下拉索引 == 枚举值」隐含前提。
    /// </summary>
    /// <remarks>
    /// 该集成把下拉索引经 <c>(EBundledCopyOption)value</c> 直接强转为枚举值，
    /// 因此枚举的声明顺序必须与 <c>sCopyOptionNames</c> 的中文标签顺序一一对应；
    /// 若 YooAsset 次版本增删或调整枚举成员，映射会**静默指向错误的拷贝策略**（不抛错、无提示）。
    /// 本测试把该先前只存在于注释里的前提固化为断言，使漂移在编译期后立即可见。
    /// </remarks>
    public sealed class YooAssetCopyOptionMappingTests
    {
        /// <summary>下拉标签所在的私有静态字段名。</summary>
        private const string LABELS_FIELD_NAME = "sCopyOptionNames";

        /// <summary>V3 枚举的期望声明顺序（与中文标签顺序对应）。</summary>
        private static readonly string[] EXPECTED_V3_NAMES =
        {
            "None",
            "ClearAndCopyAll",
            "ClearAndCopyByTags",
            "OnlyCopyAll",
            "OnlyCopyByTags"
        };

        /// <summary>读取 Drawer 中硬编码的拷贝策略中文标签。</summary>
        /// <returns>按索引排列的标签列表。</returns>
        private static IList<string> ReadCopyOptionLabels()
        {
            FieldInfo field = typeof(YooAssetInitializationOptionsDrawer).GetField(
                LABELS_FIELD_NAME,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "未找到 " + LABELS_FIELD_NAME);
            return (IList<string>)field.GetValue(null);
        }

        /// <summary>
        /// 标签数量必须等于枚举成员数量，否则存在索引越界或成员未覆盖。
        /// </summary>
        [Test]
        public void CopyOptionLabelCountMatchesEnumMemberCount()
        {
            int enumCount = Enum.GetNames(typeof(EBundledCopyOption)).Length;
            IList<string> labels = ReadCopyOptionLabels();

            Assert.AreEqual(
                enumCount,
                labels.Count,
                "下拉标签数量与 EBundledCopyOption 成员数量不一致，索引映射可能已失效");
        }

        /// <summary>
        /// 锁定 V3 枚举的声明顺序，使 YooAsset 侧顺序漂移立即失败而不是静默错配。
        /// </summary>
        [Test]
        public void BundledCopyOptionDeclarationOrderIsLocked()
        {
            CollectionAssert.AreEqual(
                EXPECTED_V3_NAMES,
                Enum.GetNames(typeof(EBundledCopyOption)),
                "EBundledCopyOption 声明顺序发生变化，必须同步复核下拉标签顺序");
        }

        /// <summary>校验首个成员必须为 None 且值为 0，保证「不拷贝」映射到默认值。</summary>
        [Test]
        public void FirstCopyOptionIsNoneWithZeroValue()
        {
            Assert.AreEqual(0, (int)EBundledCopyOption.None, "None 必须为 0，否则下拉默认项含义改变");
        }
    }
}
#endif
