using System;
using UnityEngine;

namespace YokiFrame
{
    public static class TransformExtension
    {
        #region 查找组件
        
        /// <summary>
        /// 通过名字查找指定名字的子物体，然后获取挂载的Component
        /// </summary>
        public static T FindComponent<T>(this GameObject parent, string targetName) where T : Component 
            => parent.transform.FindComponent<T>(targetName);
        
        /// <summary>
        /// 通过名字查找指定名字的子物体，然后获取挂载的Component
        /// </summary>
        public static T FindComponent<T>(this Component parent, string targetName) where T : Component
        {
            if (parent.name == targetName && parent.TryGetComponent<T>(out var component))
                return component;
            foreach (Transform child in parent.transform)
            {
                T result = child.FindComponent<T>(targetName);
                if (result != default) return result;
            }
            return default;
        }

        /// <summary>
        /// 通过路径查找子物体
        /// </summary>
        public static Transform FindByPath(this Transform self, string path)
        {
            return self.Find(path);
        }

        /// <summary>
        /// 通过路径查找子物体并获取组件
        /// </summary>
        public static T FindByPath<T>(this Transform self, string path) where T : Component
        {
            var child = self.Find(path);
            return child != default ? child.GetComponent<T>() : default;
        }

        #endregion

        #region 重置变换
        
        /// <summary>
        /// 在局部空间中，复位Transform到默认位置
        /// </summary>
        public static Transform ResetTransform(this Transform self, bool resetScale = true)
        {
            self.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (resetScale) self.localScale = Vector3.one;
            return self;
        }
        
        /// <summary>
        /// 在UI的局部空间中，复位RectTransform到默认位置
        /// </summary>
        public static RectTransform ResetRectTransform(this RectTransform self, bool resetScale = true)
        {
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (resetScale) self.localScale = Vector3.one;
            return self;
        }

        #endregion

        #region 位置操作
        
        /// <summary>
        /// 获取 2D 位置 (x, y)
        /// </summary>
        public static Vector2 Position2D(this Transform self) => new Vector2(self.position.x, self.position.y);

        /// <summary>
        /// 设置 2D 位置，保持 z 不变
        /// </summary>
        public static Transform Position2D(this Transform self, Vector2 pos)
        {
            self.position = new Vector3(pos.x, pos.y, self.position.z);
            return self;
        }

        /// <summary>
        /// 设置世界坐标位置
        /// </summary>
        public static Transform Position(this Transform self, Vector3 position)
        {
            self.position = position;
            return self;
        }

        /// <summary>
        /// 设置世界坐标位置（分量）
        /// </summary>
        public static Transform Position(this Transform self, float x, float y, float z)
        {
            self.position = new Vector3(x, y, z);
            return self;
        }

        /// <summary>
        /// 设置局部坐标位置
        /// </summary>
        public static Transform LocalPosition(this Transform self, Vector3 localPosition)
        {
            self.localPosition = localPosition;
            return self;
        }

        /// <summary>
        /// 设置局部坐标位置（分量）
        /// </summary>
        public static Transform LocalPosition(this Transform self, float x, float y, float z)
        {
            self.localPosition = new Vector3(x, y, z);
            return self;
        }

        /// <summary>
        /// 设置局部坐标 X
        /// </summary>
        public static Transform LocalPositionX(this Transform self, float x)
        {
            var pos = self.localPosition;
            pos.x = x;
            self.localPosition = pos;
            return self;
        }

        /// <summary>
        /// 设置局部坐标 Y
        /// </summary>
        public static Transform LocalPositionY(this Transform self, float y)
        {
            var pos = self.localPosition;
            pos.y = y;
            self.localPosition = pos;
            return self;
        }

        /// <summary>
        /// 设置局部坐标 Z
        /// </summary>
        public static Transform LocalPositionZ(this Transform self, float z)
        {
            var pos = self.localPosition;
            pos.z = z;
            self.localPosition = pos;
            return self;
        }

        #endregion

        #region 旋转操作
        
        /// <summary>
        /// 设置世界旋转
        /// </summary>
        public static Transform Rotation(this Transform self, Quaternion rotation)
        {
            self.rotation = rotation;
            return self;
        }

        /// <summary>
        /// 设置局部旋转
        /// </summary>
        public static Transform LocalRotation(this Transform self, Quaternion localRotation)
        {
            self.localRotation = localRotation;
            return self;
        }

        /// <summary>
        /// 设置欧拉角旋转
        /// </summary>
        public static Transform EulerAngles(this Transform self, Vector3 eulerAngles)
        {
            self.eulerAngles = eulerAngles;
            return self;
        }

        /// <summary>
        /// 设置局部欧拉角旋转
        /// </summary>
        public static Transform LocalEulerAngles(this Transform self, Vector3 localEulerAngles)
        {
            self.localEulerAngles = localEulerAngles;
            return self;
        }

        #endregion

        #region 缩放操作
        
        /// <summary>
        /// 设置局部缩放
        /// </summary>
        public static Transform LocalScale(this Transform self, Vector3 localScale)
        {
            self.localScale = localScale;
            return self;
        }

        /// <summary>
        /// 设置统一局部缩放
        /// </summary>
        public static Transform LocalScale(this Transform self, float scale)
        {
            self.localScale = new Vector3(scale, scale, scale);
            return self;
        }

        #endregion

        #region 层级操作
        
        /// <summary>
        /// 设置父物体并返回自身
        /// </summary>
        public static Transform Parent(this Transform self, Transform parent)
        {
            self.SetParent(parent);
            return self;
        }

        /// <summary>
        /// 设置父物体（可选保持世界坐标）并返回自身
        /// </summary>
        public static Transform Parent(this Transform self, Transform parent, bool worldPositionStays)
        {
            self.SetParent(parent, worldPositionStays);
            return self;
        }

        /// <summary>
        /// 设置为第一个子物体
        /// </summary>
        public static Transform AsFirstSibling(this Transform self)
        {
            self.SetAsFirstSibling();
            return self;
        }

        /// <summary>
        /// 设置为最后一个子物体
        /// </summary>
        public static Transform AsLastSibling(this Transform self)
        {
            self.SetAsLastSibling();
            return self;
        }

        /// <summary>
        /// 设置同级索引
        /// </summary>
        public static Transform SiblingIndex(this Transform self, int index)
        {
            self.SetSiblingIndex(index);
            return self;
        }

        /// <summary>
        /// 销毁所有子物体
        /// </summary>
        public static Transform DestroyAllChildren(this Transform self)
        {
            for (int i = self.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(self.GetChild(i).gameObject);
            }
            return self;
        }

        /// <summary>
        /// 立即销毁所有子物体（编辑器模式）
        /// </summary>
        public static Transform DestroyAllChildrenImmediate(this Transform self)
        {
            for (int i = self.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(self.GetChild(i).gameObject);
            }
            return self;
        }

        /// <summary>
        /// 遍历所有子物体
        /// </summary>
        public static Transform ForEachChild(this Transform self, Action<Transform> action)
        {
            for (int i = 0; i < self.childCount; i++)
            {
                action?.Invoke(self.GetChild(i));
            }
            return self;
        }

        /// <summary>
        /// 遍历所有子物体（带索引）
        /// </summary>
        public static Transform ForEachChild(this Transform self, Action<Transform, int> action)
        {
            for (int i = 0; i < self.childCount; i++)
            {
                action?.Invoke(self.GetChild(i), i);
            }
            return self;
        }

        #endregion

        #region RectTransform 扩展
        
        /// <summary>
        /// 设置锚点位置
        /// </summary>
        public static RectTransform AnchoredPosition(this RectTransform self, Vector2 anchoredPosition)
        {
            self.anchoredPosition = anchoredPosition;
            return self;
        }

        /// <summary>
        /// 设置锚点位置（分量）
        /// </summary>
        public static RectTransform AnchoredPosition(this RectTransform self, float x, float y)
        {
            self.anchoredPosition = new Vector2(x, y);
            return self;
        }

        /// <summary>
        /// 设置锚点位置 X
        /// </summary>
        public static RectTransform AnchoredPositionX(this RectTransform self, float x)
        {
            var pos = self.anchoredPosition;
            pos.x = x;
            self.anchoredPosition = pos;
            return self;
        }

        /// <summary>
        /// 设置锚点位置 Y
        /// </summary>
        public static RectTransform AnchoredPositionY(this RectTransform self, float y)
        {
            var pos = self.anchoredPosition;
            pos.y = y;
            self.anchoredPosition = pos;
            return self;
        }

        /// <summary>
        /// 设置尺寸
        /// </summary>
        public static RectTransform SizeDelta(this RectTransform self, Vector2 sizeDelta)
        {
            self.sizeDelta = sizeDelta;
            return self;
        }

        /// <summary>
        /// 设置尺寸（分量）
        /// </summary>
        public static RectTransform SizeDelta(this RectTransform self, float width, float height)
        {
            self.sizeDelta = new Vector2(width, height);
            return self;
        }

        /// <summary>
        /// 设置锚点
        /// </summary>
        public static RectTransform Anchors(this RectTransform self, Vector2 min, Vector2 max)
        {
            self.anchorMin = min;
            self.anchorMax = max;
            return self;
        }

        /// <summary>
        /// 设置轴心点
        /// </summary>
        public static RectTransform Pivot(this RectTransform self, Vector2 pivot)
        {
            self.pivot = pivot;
            return self;
        }

        /// <summary>
        /// 设置轴心点（分量）
        /// </summary>
        public static RectTransform Pivot(this RectTransform self, float x, float y)
        {
            self.pivot = new Vector2(x, y);
            return self;
        }

        #endregion
    }
}