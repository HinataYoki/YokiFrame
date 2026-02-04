using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// ECS组件引用，用于序列化存储组件类型和初始值
    /// </summary>
    [Serializable]
    public class ECSComponentReference
    {
        [SerializeField] private string _assemblyQualifiedName;
        [SerializeField] private string _jsonData;
        
        public string AssemblyQualifiedName
        {
            get => _assemblyQualifiedName;
            set => _assemblyQualifiedName = value;
        }
        
        public string JsonData
        {
            get => _jsonData;
            set => _jsonData = value;
        }
        
        public Type GetComponentType()
        {
            if (string.IsNullOrEmpty(_assemblyQualifiedName)) return null;
            return Type.GetType(_assemblyQualifiedName);
        }
        
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(_assemblyQualifiedName)) return "(None)";
                var type = GetComponentType();
                return type?.Name ?? _assemblyQualifiedName;
            }
        }
    }
    
    /// <summary>
    /// ECS实体绑定器，将GameObject与ECS实体关联
    /// 挂载到需要参与ECS的GameObject上，在Inspector中配置组件
    /// </summary>
    public class ECSEntityBinder : MonoBehaviour
    {
        [Tooltip("要绑定的World名称")]
        [SerializeField] private string _worldName = "DefaultWorld";
        
        [Tooltip("是否在Start时自动创建实体")]
        [SerializeField] private bool _autoCreateEntity = true;
        
        [Tooltip("每帧将Transform位置同步到ECS Position组件")]
        [SerializeField] private bool _syncTransformToECS = true;
        
        [Tooltip("每帧将ECS Position组件同步到Transform位置")]
        [SerializeField] private bool _syncECSToTransform = false;
        
        [Tooltip("要添加到实体的ECS组件列表")]
        [SerializeField] private List<ECSComponentReference> _components = new List<ECSComponentReference>();
        
        private ECSWorld _world;
        private Entity _entity = Entity.Null;
        private bool _initialized;
        
        /// <summary>
        /// 绑定的ECS实体
        /// </summary>
        public Entity Entity => _entity;
        
        /// <summary>
        /// 所属的ECS World
        /// </summary>
        public ECSWorld World => _world;
        
        /// <summary>
        /// 实体是否有效（已创建且未销毁）
        /// </summary>
        public bool IsValid => !_entity.IsNull && _world != null && _world.IsAlive(_entity);
        
        private void Start()
        {
            Initialize();
        }
        
        private void LateUpdate()
        {
            if (!IsValid) return;
            
            // 根据配置同步Transform和ECS Position
            if (_syncTransformToECS)
            {
                SyncTransformToECS();
            }
            else if (_syncECSToTransform)
            {
                SyncECSToTransform();
            }
        }
        
        private void OnDestroy()
        {
            if (IsValid)
            {
                ECSGameObjectManager.Unregister(_entity.Id);
                _world.DestroyEntity(_entity);
            }
            _entity = Entity.Null;
            _initialized = false;
        }
        
        /// <summary>
        /// 初始化绑定器，获取World并创建实体
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            
            _world = ECSKit.GetWorld(_worldName);
            if (_world == null)
            {
                Debug.LogWarning($"[ECSEntityBinder] 找不到World: {_worldName}，请确保ECSSystemRunner先初始化");
                return;
            }
            
            if (_autoCreateEntity)
            {
                CreateEntity();
            }
            
            _initialized = true;
        }
        
        /// <summary>
        /// 使用指定的World初始化
        /// </summary>
        public void Initialize(ECSWorld world)
        {
            if (_initialized) return;
            
            _world = world;
            
            if (_autoCreateEntity)
            {
                CreateEntity();
            }
            
            _initialized = true;
        }
        
        /// <summary>
        /// 根据配置的组件列表创建ECS实体
        /// </summary>
        private void CreateEntity()
        {
            var componentTypes = new List<ComponentType>();
            
            // 收集所有配置的组件类型
            foreach (var compRef in _components)
            {
                var type = compRef.GetComponentType();
                if (type == null) continue;
                
                // 通过反射获取ComponentType
                var method = typeof(ComponentTypeRegistry).GetMethod("Get");
                var genericMethod = method.MakeGenericMethod(type);
                var componentType = (ComponentType)genericMethod.Invoke(null, null);
                componentTypes.Add(componentType);
            }
            
            // 添加RenderRef组件用于关联GameObject
            componentTypes.Add(ComponentTypeRegistry.Get<RenderRef>());
            
            // 如果需要同步Transform，确保有Position组件
            if (_syncTransformToECS || _syncECSToTransform)
            {
                if (!componentTypes.Exists(ct => ct.TypeIndex == ComponentTypeRegistry.GetIndex<Position>()))
                {
                    componentTypes.Add(ComponentTypeRegistry.Get<Position>());
                }
            }
            
            // 创建实体
            _entity = _world.CreateEntity(componentTypes.ToArray());
            
            // 设置RenderRef并注册到管理器
            _world.SetComponent(_entity, new RenderRef(gameObject.GetInstanceID()));
            ECSGameObjectManager.Register(_entity.Id, gameObject);
            
            // 设置初始Position
            if (_syncTransformToECS)
            {
                var pos = transform.position;
                _world.SetComponent(_entity, new Position { X = pos.x, Y = pos.y, Z = pos.z });
            }
            
            // 设置各组件的初始值
            foreach (var compRef in _components)
            {
                SetComponentFromReference(compRef);
            }
        }
        
        /// <summary>
        /// 从序列化数据设置组件值
        /// </summary>
        private void SetComponentFromReference(ECSComponentReference compRef)
        {
            var type = compRef.GetComponentType();
            if (type == null) return;
            
            try
            {
                object value;
                if (!string.IsNullOrEmpty(compRef.JsonData))
                {
                    value = JsonUtility.FromJson(compRef.JsonData, type);
                }
                else
                {
                    value = Activator.CreateInstance(type);
                }
                
                var method = typeof(ECSWorld).GetMethod("SetComponent");
                var genericMethod = method.MakeGenericMethod(type);
                genericMethod.Invoke(_world, new object[] { _entity, value });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ECSEntityBinder] 设置组件失败 {type.Name}: {e.Message}");
            }
        }
        
        private void SyncTransformToECS()
        {
            if (!_world.HasComponent<Position>(_entity)) return;
            
            var pos = transform.position;
            _world.SetComponent(_entity, new Position { X = pos.x, Y = pos.y, Z = pos.z });
        }
        
        private void SyncECSToTransform()
        {
            if (!_world.TryGetComponent<Position>(_entity, out var pos)) return;
            
            transform.position = new Vector3(pos.X, pos.Y, pos.Z);
        }
        
        /// <summary>
        /// 绑定到已存在的ECS实体
        /// </summary>
        public void BindToEntity(Entity entity, ECSWorld world)
        {
            _world = world;
            _entity = entity;
            _initialized = true;
            
            if (_world.HasComponent<RenderRef>(_entity))
            {
                _world.SetComponent(_entity, new RenderRef(gameObject.GetInstanceID()));
            }
        }
        
        /// <summary>
        /// 获取ECS组件的引用
        /// </summary>
        public ref T GetComponent<T>() where T : unmanaged
        {
            return ref _world.GetComponent<T>(_entity);
        }
        
        /// <summary>
        /// 尝试获取ECS组件
        /// </summary>
        public bool TryGetECSComponent<T>(out T value) where T : unmanaged
        {
            return _world.TryGetComponent(_entity, out value);
        }
        
        /// <summary>
        /// 设置ECS组件值
        /// </summary>
        public void SetComponent<T>(T value) where T : unmanaged
        {
            _world.SetComponent(_entity, value);
        }
        
        /// <summary>
        /// 检查实体是否有指定组件
        /// </summary>
        public bool HasComponent<T>() where T : unmanaged
        {
            return _world.HasComponent<T>(_entity);
        }
        
        /// <summary>
        /// 添加动态组件
        /// </summary>
        public bool AddComponent<T>(T value) where T : unmanaged
        {
            return _world.AddComponent(_entity, value);
        }
        
        /// <summary>
        /// 移除动态组件
        /// </summary>
        public bool RemoveComponent<T>() where T : unmanaged
        {
            return _world.RemoveComponent<T>(_entity);
        }
        
        /// <summary>
        /// 添加组件引用（编辑器使用）
        /// </summary>
        public void AddComponentReference<T>() where T : unmanaged
        {
            var reference = new ECSComponentReference
            {
                AssemblyQualifiedName = typeof(T).AssemblyQualifiedName
            };
            _components.Add(reference);
        }
    }
}
