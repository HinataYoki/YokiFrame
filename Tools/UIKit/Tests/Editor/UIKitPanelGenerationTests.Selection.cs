using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    public sealed partial class UIKitPanelGenerationTests
    {
        /// <summary>验证通用 selection action 拒绝把 Element/Component Prefab 生成成 Panel。</summary>
        [TestCase(typeof(UIKitStandaloneElementTest), "UIElement")]
        [TestCase(typeof(UIKitStandaloneComponentTest), "UIComponent")]
        public void GenericSelectionGenerationRejectsStandaloneOwnerPrefab(
            Type ownerType,
            string ownerLabel)
        {
            GameObject root = new(ownerType.Name, typeof(RectTransform), ownerType);
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.RequirePanelPrefab(root));
                StringAssert.Contains(ownerLabel, exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>向测试 Bind 追加一个有稳定字段名的 Member 目标。</summary>
        private static void AddMemberTarget(Bind bind, Component target, string fieldName)
        {
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = target,
                Name = fieldName,
            });
            if (bind.MemberTargets.Count != 1)
                return;
            bind.Target = target;
            bind.Name = fieldName;
            bind.Type = target.GetType().FullName;
            bind.AutoType = bind.Type;
        }

        /// <summary>测试统一 Editor Settings JSON 的最小解析结构。</summary>
        [Serializable]
        private sealed class EditorSettingsDocument
        {
            public EditorSettingsEntry[] settings;
        }

        /// <summary>测试统一 Editor Settings 的单条键值。</summary>
        [Serializable]
        private sealed class EditorSettingsEntry
        {
            public string kit;
            public string key;
            public string value;
        }
    }
}
