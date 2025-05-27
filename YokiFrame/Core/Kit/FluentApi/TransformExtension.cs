using UnityEngine;

namespace YokiFrame
{
    public static class TransformExtension
    {
        /// <summary> 通过名字查找指定名字的子物体，然后获取挂载的Component </summary>
        /// <param name="targetName">物体的名字</param>
        /// <returns>返回找到的该物体的组件T，没有找到则返回null</returns>
        public static T FindComponent<T>(this GameObject parent, string targetName) where T : Component => parent.transform.FindComponent<T>(targetName);
        /// <summary> 通过名字查找指定名字的子物体，然后获取挂载的Component </summary>
        /// <param name="targetName">物体的名字</param>
        /// <returns>返回找到的该物体的组件T，没有找到则返回null</returns>
        public static T FindComponent<T>(this Component parent, string targetName) where T : Component
        {
            if (parent.name == targetName && parent.TryGetComponent<T>(out var component))
                return component;
            foreach (Transform child in parent.transform)
            {
                T result = child.FindComponent<T>(targetName);
                if (result) return result;
            }
            return null;
        }
        /// <summary> 在局部空间中，复位Transform到默认位置 </summary>
        public static void ResetTransform(this Transform self, bool resetScale = true)
        {
            self.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (resetScale)
                self.localScale = Vector3.one;
        }
        /// <summary> 在UI的局部空间中，复位RectTransform到默认位置 </summary>
        public static void ResetRectTransform(this RectTransform self, bool resetScale = true)
        {
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            ResetTransform(self, resetScale);
        }

        public static Vector2 Position2D(this Transform self)
        {
            return new Vector2(self.position.x, self.position.y);
        }

        public static void Position2D(this Transform self, Vector2 pos)
        {
            self.position = new Vector3(pos.x, pos.y, self.position.z);
        }
    }
}