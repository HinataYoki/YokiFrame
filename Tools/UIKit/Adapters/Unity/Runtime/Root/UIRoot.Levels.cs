#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class UIRoot
    {
        private readonly SortedDictionary<int, RectTransform> mLevelRoots = new();

        /// <summary>
        /// 获取或创建指定 UILevel 的全屏容器，并保持容器按 Order 升序排列。
        /// </summary>
        internal RectTransform GetOrCreateLevelRoot(UILevel level)
        {
            AssertMainThread();
            if (mLevelRoots.TryGetValue(level.Order, out RectTransform existing) && existing != default)
                return existing;
            RectTransform created = CreateLevelRoot(level);
            mLevelRoots[level.Order] = created;
            SortLevelRoots();
            return created;
        }

        /// <summary>
        /// 创建一个铺满 Canvas 的层级容器。
        /// </summary>
        private RectTransform CreateLevelRoot(UILevel level)
        {
            var levelObject = new GameObject("Level_" + level, typeof(RectTransform));
            var levelRoot = levelObject.GetComponent<RectTransform>();
            levelRoot.SetParent(mCanvasRoot, false);
            levelRoot.anchorMin = Vector2.zero;
            levelRoot.anchorMax = Vector2.one;
            levelRoot.anchoredPosition = Vector2.zero;
            levelRoot.sizeDelta = Vector2.zero;
            levelRoot.localScale = Vector3.one;
            return levelRoot;
        }

        /// <summary>
        /// 把所有已创建层级容器按排序值提交到 Canvas sibling 顺序。
        /// </summary>
        private void SortLevelRoots()
        {
            var siblingIndex = 0;
            foreach (RectTransform levelRoot in mLevelRoots.Values)
            {
                if (levelRoot == default) continue;
                levelRoot.SetSiblingIndex(siblingIndex++);
            }
        }
    }
}
#endif
