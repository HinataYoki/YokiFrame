#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using NUnit.Framework;
using YokiFrame.Unity;
using YooAsset.Editor;
#if YOKIFRAME_YOOASSET_3
using CopyOption = YooAsset.Editor.EBundledCopyOption;
#else
using CopyOption = YooAsset.Editor.EBuildinFileCopyOption;
#endif

namespace YokiFrame.Unity.Tests
{
    /// <summary>
    /// 守护首包拷贝策略的「下拉索引 == 枚举值」前提。
    /// 适配器把下拉索引直接强转为当前主版本的拷贝枚举，标签顺序和底层值漂移都会静默错配。
    /// </summary>
    public sealed class YooAssetCopyOptionMappingTests
    {
        /// <summary>与 Drawer 中文标签一一对应的枚举声明顺序。V2.3 与 V3 当前成员名相同。</summary>
        private static readonly string[] EXPECTED_ENUM_NAMES =
        {
            "None",
            "ClearAndCopyAll",
            "ClearAndCopyByTags",
            "OnlyCopyAll",
            "OnlyCopyByTags"
        };

        /// <summary>Drawer 下拉必须按该顺序展示，索引才能强转到对应枚举值。</summary>
        private static readonly string[] EXPECTED_LABELS =
        {
            "不拷贝",
            "清空后拷贝全部",
            "清空后按标签拷贝",
            "直接拷贝全部",
            "直接按标签拷贝"
        };

        /// <summary>验证中文标签顺序固定，且数量等于当前拷贝枚举成员数。</summary>
        [Test]
        public void sCopyOptionNames_当前拷贝枚举_数量一致且标签顺序固定()
        {
            IList<string> labels = YooAssetInitializationOptionsDrawer.sCopyOptionNames;

            Assert.AreEqual(
                Enum.GetNames(typeof(CopyOption)).Length,
                labels.Count,
                "下拉标签数量与当前 YooAsset 拷贝枚举成员数量不一致，索引映射可能已失效");
            CollectionAssert.AreEqual(EXPECTED_LABELS, labels);
        }

        /// <summary>
        /// 验证声明顺序和底层值同时锁定。
        /// 每个成员的底层值必须等于声明索引，因此首项 None 为 0；否则下拉强转会指向别的策略。
        /// </summary>
        [Test]
        public void CopyOption_声明顺序_底层值等于索引()
        {
            string[] names = Enum.GetNames(typeof(CopyOption));
            CollectionAssert.AreEqual(
                EXPECTED_ENUM_NAMES,
                names,
                "YooAsset 拷贝枚举声明顺序发生变化，必须同步复核下拉标签顺序");

            for (int index = 0; index < names.Length; index++)
            {
                CopyOption value = (CopyOption)Enum.Parse(typeof(CopyOption), names[index]);
                Assert.AreEqual(index, (int)value, names[index] + " 的底层值必须等于声明索引");
            }
        }
    }
}
#endif
