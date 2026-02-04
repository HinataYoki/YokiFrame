using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// GameObject 管理器 - 管理ECS实体与GameObject的映射
    /// </summary>
    public static class ECSGameObjectManager
    {
        private static Dictionary<int, GameObject> _instanceIdToGO = new Dictionary<int, GameObject>(256);
        private static Dictionary<long, int> _entityIdToInstanceId = new Dictionary<long, int>(256);
        
        /// <summary>
        /// 注册实体与GameObject的关联
        /// </summary>
        public static void Register(long entityId, GameObject go)
        {
            if (go == null) return;
            int instanceId = go.GetInstanceID();
            _instanceIdToGO[instanceId] = go;
            _entityIdToInstanceId[entityId] = instanceId;
        }
        
        /// <summary>
        /// 取消注册
        /// </summary>
        public static void Unregister(long entityId)
        {
            if (_entityIdToInstanceId.TryGetValue(entityId, out int instanceId))
            {
                _instanceIdToGO.Remove(instanceId);
                _entityIdToInstanceId.Remove(entityId);
            }
        }
        
        /// <summary>
        /// 通过InstanceId获取GameObject
        /// </summary>
        public static GameObject Get(int instanceId)
        {
            return _instanceIdToGO.TryGetValue(instanceId, out var go) ? go : null;
        }
        
        /// <summary>
        /// 通过EntityId获取GameObject
        /// </summary>
        public static GameObject GetByEntityId(long entityId)
        {
            if (_entityIdToInstanceId.TryGetValue(entityId, out int instanceId))
            {
                return Get(instanceId);
            }
            return null;
        }
        
        /// <summary>
        /// 清理所有
        /// </summary>
        public static void Clear()
        {
            foreach (var go in _instanceIdToGO.Values)
            {
                if (go != null) Object.Destroy(go);
            }
            _instanceIdToGO.Clear();
            _entityIdToInstanceId.Clear();
        }
        
        /// <summary>
        /// 销毁并取消注册
        /// </summary>
        public static void DestroyAndUnregister(long entityId)
        {
            var go = GetByEntityId(entityId);
            if (go != null)
            {
                Object.Destroy(go);
            }
            Unregister(entityId);
        }
    }
}
