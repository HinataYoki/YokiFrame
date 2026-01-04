using UnityEngine;

namespace YokiFrame
{
    public static class UnityObjectExtension
    {
        public static GameObject Parent(this GameObject self, GameObject parent)
        {
            self.transform.parent = parent.transform;
            return self;
        }

        public static GameObject Parent(this GameObject self, Transform parent)
        {
            self.transform.parent = parent;
            return self;
        }

        public static GameObject Parent(this MonoBehaviour self, GameObject parent)
        {
            self.transform.parent = parent.transform;
            return self.gameObject;
        }

        public static GameObject Parent(this MonoBehaviour self, Transform parent)
        {
            self.transform.parent = parent.transform;
            return self.gameObject;
        }

        /// <summary>
        /// 获取或添加组件（通用扩展方法）
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject self) where T : Component
        {
            var comp = self.GetComponent<T>();
            return comp ? comp : self.AddComponent<T>();
        }

        /// <summary>
        /// 获取或添加组件（MonoBehaviour 扩展）
        /// </summary>
        public static T GetOrAddComponent<T>(this MonoBehaviour self) where T : Component
        {
            return self.gameObject.GetOrAddComponent<T>();
        }
    }
}