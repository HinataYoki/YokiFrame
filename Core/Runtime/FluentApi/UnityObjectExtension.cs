using System;
using UnityEngine;

namespace YokiFrame
{
    public static class UnityObjectExtension
    {
        #region 父物体设置
        
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

        #endregion

        #region 组件操作
        
        /// <summary>
        /// 获取或添加组件
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject self) where T : Component
        {
            var comp = self.GetComponent<T>();
            return comp != default ? comp : self.AddComponent<T>();
        }

        /// <summary>
        /// 获取或添加组件（MonoBehaviour 扩展）
        /// </summary>
        public static T GetOrAddComponent<T>(this MonoBehaviour self) where T : Component
        {
            return self.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// 尝试获取组件，如果存在则执行回调
        /// </summary>
        public static GameObject TryGetComponent<T>(this GameObject self, Action<T> onGet) where T : Component
        {
            if (self.TryGetComponent<T>(out var comp))
            {
                onGet?.Invoke(comp);
            }
            return self;
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public static GameObject RemoveComponent<T>(this GameObject self) where T : Component
        {
            var comp = self.GetComponent<T>();
            if (comp != default) UnityEngine.Object.Destroy(comp);
            return self;
        }

        /// <summary>
        /// 立即移除组件（编辑器模式）
        /// </summary>
        public static GameObject RemoveComponentImmediate<T>(this GameObject self) where T : Component
        {
            var comp = self.GetComponent<T>();
            if (comp != default) UnityEngine.Object.DestroyImmediate(comp);
            return self;
        }

        #endregion

        #region 激活状态
        
        /// <summary>
        /// 设置激活状态
        /// </summary>
        public static GameObject Active(this GameObject self, bool active)
        {
            self.SetActive(active);
            return self;
        }

        /// <summary>
        /// 激活物体
        /// </summary>
        public static GameObject Show(this GameObject self)
        {
            self.SetActive(true);
            return self;
        }

        /// <summary>
        /// 隐藏物体
        /// </summary>
        public static GameObject Hide(this GameObject self)
        {
            self.SetActive(false);
            return self;
        }

        /// <summary>
        /// 切换激活状态
        /// </summary>
        public static GameObject ToggleActive(this GameObject self)
        {
            self.SetActive(!self.activeSelf);
            return self;
        }

        #endregion

        #region 层级与标签
        
        /// <summary>
        /// 设置层级
        /// </summary>
        public static GameObject Layer(this GameObject self, int layer)
        {
            self.layer = layer;
            return self;
        }

        /// <summary>
        /// 设置层级（通过名称）
        /// </summary>
        public static GameObject Layer(this GameObject self, string layerName)
        {
            self.layer = LayerMask.NameToLayer(layerName);
            return self;
        }

        /// <summary>
        /// 递归设置层级
        /// </summary>
        public static GameObject LayerRecursive(this GameObject self, int layer)
        {
            self.layer = layer;
            foreach (Transform child in self.transform)
            {
                child.gameObject.LayerRecursive(layer);
            }
            return self;
        }

        /// <summary>
        /// 设置标签
        /// </summary>
        public static GameObject Tag(this GameObject self, string tag)
        {
            self.tag = tag;
            return self;
        }

        #endregion

        #region 名称操作
        
        /// <summary>
        /// 设置名称
        /// </summary>
        public static GameObject Name(this GameObject self, string name)
        {
            self.name = name;
            return self;
        }

        /// <summary>
        /// 设置名称（Component 扩展）
        /// </summary>
        public static T Name<T>(this T self, string name) where T : Component
        {
            self.gameObject.name = name;
            return self;
        }

        #endregion

        #region 销毁操作
        
        /// <summary>
        /// 销毁物体
        /// </summary>
        public static void Destroy(this GameObject self)
        {
            UnityEngine.Object.Destroy(self);
        }

        /// <summary>
        /// 延迟销毁物体
        /// </summary>
        public static void Destroy(this GameObject self, float delay)
        {
            UnityEngine.Object.Destroy(self, delay);
        }

        /// <summary>
        /// 立即销毁物体（编辑器模式）
        /// </summary>
        public static void DestroyImmediate(this GameObject self)
        {
            UnityEngine.Object.DestroyImmediate(self);
        }

        /// <summary>
        /// 销毁组件
        /// </summary>
        public static void Destroy(this Component self)
        {
            UnityEngine.Object.Destroy(self);
        }

        /// <summary>
        /// 设置不随场景销毁
        /// </summary>
        public static GameObject DontDestroyOnLoad(this GameObject self)
        {
            UnityEngine.Object.DontDestroyOnLoad(self);
            return self;
        }

        #endregion

        #region 实例化
        
        /// <summary>
        /// 实例化物体
        /// </summary>
        public static GameObject Instantiate(this GameObject self)
        {
            return UnityEngine.Object.Instantiate(self);
        }

        /// <summary>
        /// 实例化物体到指定父物体下
        /// </summary>
        public static GameObject Instantiate(this GameObject self, Transform parent)
        {
            return UnityEngine.Object.Instantiate(self, parent);
        }

        /// <summary>
        /// 实例化物体到指定位置
        /// </summary>
        public static GameObject Instantiate(this GameObject self, Vector3 position, Quaternion rotation)
        {
            return UnityEngine.Object.Instantiate(self, position, rotation);
        }

        /// <summary>
        /// 实例化物体到指定位置和父物体下
        /// </summary>
        public static GameObject Instantiate(this GameObject self, Vector3 position, Quaternion rotation, Transform parent)
        {
            return UnityEngine.Object.Instantiate(self, position, rotation, parent);
        }

        #endregion
    }
}