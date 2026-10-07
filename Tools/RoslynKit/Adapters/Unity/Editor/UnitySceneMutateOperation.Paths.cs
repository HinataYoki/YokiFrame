#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YokiFrame
{
    internal sealed partial class UnitySceneMutateOperation
    {
        /// <summary>在活动场景按斜杠路径查找物体。无场景、空白段之外未命中时返回 null。</summary>
        /// <param name="path">层级路径。</param>
        /// <returns>命中的物体；路径只有分隔符时返回 null。</returns>
        private static GameObject FindByPath(string path)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            string[] segments = path.Split('/');
            GameObject[] level = roots;
            GameObject current = null;
            var found = false;
            for (var index = 0; index < segments.Length; index++)
            {
                string segment = segments[index].Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                GameObject match = null;
                for (var candidate = 0; candidate < level.Length; candidate++)
                {
                    if (string.Equals(level[candidate].name, segment, StringComparison.Ordinal))
                    {
                        match = level[candidate];
                        break;
                    }
                }

                if (match == null)
                {
                    return null;
                }

                current = match;
                found = true;
                var children = new List<GameObject>(match.transform.childCount);
                for (var childIndex = 0; childIndex < match.transform.childCount; childIndex++)
                {
                    children.Add(match.transform.GetChild(childIndex).gameObject);
                }

                level = children.ToArray();
            }

            return found ? current : null;
        }
    }
}
#endif
