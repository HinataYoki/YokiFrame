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
    }
}