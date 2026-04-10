using UnityEngine;

namespace YokiFrame.Editor
{
    /// <summary>
    /// 空间索引调试组件
    /// </summary>
    /// <remarks>
    /// 挂载到场景中的 GameObject 上，用于在 Scene 视图中可视化空间索引结构。
    /// 需要在运行时通过代码设置要调试的空间索引实例。
    /// </remarks>
    public class SpatialKitDebugComponent : MonoBehaviour
    {
        [Header("显示设置")]
        [SerializeField] private bool mShowGizmos = true;
        [SerializeField] private Color mGizmoColor = new(0.2f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private bool mOnlyNonEmpty = true;

        private object mSpatialIndex;
        private System.Type mEntityType;

        /// <summary>
        /// 设置要调试的四叉树
        /// </summary>
        public void SetQuadtree<T>(Quadtree<T> quadtree) where T : ISpatialEntity
        {
            mSpatialIndex = quadtree;
            mEntityType = typeof(T);
        }

        /// <summary>
        /// 设置要调试的八叉树
        /// </summary>
        public void SetOctree<T>(Octree<T> octree) where T : ISpatialEntity
        {
            mSpatialIndex = octree;
            mEntityType = typeof(T);
        }

        /// <summary>
        /// 设置要调试的空间哈希网格
        /// </summary>
        public void SetHashGrid<T>(SpatialHashGrid<T> grid) where T : ISpatialEntity
        {
            mSpatialIndex = grid;
            mEntityType = typeof(T);
        }

        /// <summary>
        /// 清除调试目标
        /// </summary>
        public void ClearTarget()
        {
            mSpatialIndex = null;
            mEntityType = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!mShowGizmos || mSpatialIndex == null) return;

            // 使用反射调用泛型方法
            var indexType = mSpatialIndex.GetType();

            if (indexType.IsGenericType)
            {
                var genericDef = indexType.GetGenericTypeDefinition();

                if (genericDef == typeof(Quadtree<>))
                {
                    var method = typeof(SpatialKitGizmos).GetMethod(nameof(SpatialKitGizmos.DrawQuadtree));
                    var genericMethod = method.MakeGenericMethod(mEntityType);
                    genericMethod.Invoke(null, new object[] { mSpatialIndex, mGizmoColor, mOnlyNonEmpty });
                }
                else if (genericDef == typeof(Octree<>))
                {
                    var method = typeof(SpatialKitGizmos).GetMethod(nameof(SpatialKitGizmos.DrawOctree));
                    var genericMethod = method.MakeGenericMethod(mEntityType);
                    genericMethod.Invoke(null, new object[] { mSpatialIndex, mGizmoColor, mOnlyNonEmpty });
                }
                else if (genericDef == typeof(SpatialHashGrid<>))
                {
                    var method = typeof(SpatialKitGizmos).GetMethod(nameof(SpatialKitGizmos.DrawHashGrid));
                    var genericMethod = method.MakeGenericMethod(mEntityType);
                    genericMethod.Invoke(null, new object[] { mSpatialIndex, mGizmoColor, mOnlyNonEmpty });
                }
            }
        }
#endif
    }
}