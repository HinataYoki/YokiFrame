using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// System类型引用，用于序列化存储System类型信息
    /// </summary>
    [Serializable]
    public class ECSSystemReference
    {
        [SerializeField] private string _assemblyQualifiedName;
        [SerializeField] private bool _enabled = true;
        
        public string AssemblyQualifiedName
        {
            get => _assemblyQualifiedName;
            set => _assemblyQualifiedName = value;
        }
        
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
        
        public Type GetSystemType()
        {
            if (string.IsNullOrEmpty(_assemblyQualifiedName)) return null;
            return Type.GetType(_assemblyQualifiedName);
        }
        
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(_assemblyQualifiedName)) return "(None)";
                var type = GetSystemType();
                return type?.Name ?? _assemblyQualifiedName;
            }
        }
    }
    
    /// <summary>
    /// ECS系统运行器，作为ECS的入口点
    /// 挂载到场景中的GameObject上，在Inspector中配置需要运行的System
    /// </summary>
    public class ECSSystemRunner : MonoBehaviour
    {
        [Tooltip("ECS World的名称，用于多World场景")]
        [SerializeField] private string _worldName = "DefaultWorld";
        
        [Tooltip("是否在Awake时自动创建World")]
        [SerializeField] private bool _autoCreateWorld = true;
        
        [Tooltip("是否在Update中自动调用World.Update")]
        [SerializeField] private bool _autoUpdate = true;
        
        [Tooltip("需要运行的System列表")]
        [SerializeField] private List<ECSSystemReference> _systems = new List<ECSSystemReference>();
        
        private ECSWorld _world;
        private List<IECSSystem> _instantiatedSystems = new List<IECSSystem>();
        private bool _initialized;
        
        /// <summary>
        /// 当前World实例
        /// </summary>
        public ECSWorld World => _world;
        
        /// <summary>
        /// World名称
        /// </summary>
        public string WorldName => _worldName;
        
        /// <summary>
        /// 配置的System列表
        /// </summary>
        public IReadOnlyList<ECSSystemReference> Systems => _systems;
        
        /// <summary>
        /// 实体销毁时的回调，参数为(entityId, gameObjectInstanceId)
        /// </summary>
        public Action<long, int> OnEntityDestroyed;
        
        /// <summary>
        /// SpawnRequest处理回调
        /// </summary>
        public Action<SpawnRequest> OnSpawnRequest;
        
        private void Awake()
        {
            Initialize();
        }
        
        private void Update()
        {
            if (_autoUpdate && _world != null)
            {
                _world.Update();
            }
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        
        /// <summary>
        /// 初始化ECS World和所有配置的System
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            
            // 创建或获取World
            if (_autoCreateWorld)
            {
                _world = ECSKit.CreateWorld(_worldName);
            }
            else
            {
                _world = ECSKit.GetWorld(_worldName);
            }
            
            if (_world == null)
            {
                Debug.LogError($"[ECSSystemRunner] 无法创建或获取World: {_worldName}");
                return;
            }
            
            // 注册实体销毁回调，自动清理关联的GameObject
            _world.OnEntityDestroyed = (entityId, instanceId) =>
            {
                ECSGameObjectManager.DestroyAndUnregister(entityId);
                OnEntityDestroyed?.Invoke(entityId, instanceId);
            };
            _world.OnSpawnRequest = (request) => OnSpawnRequest?.Invoke(request);
            
            // 实例化所有配置的System
            InstantiateSystems();
            _initialized = true;
        }
        
        /// <summary>
        /// 根据配置实例化所有System
        /// </summary>
        private void InstantiateSystems()
        {
            foreach (var systemRef in _systems)
            {
                if (!systemRef.Enabled) continue;
                
                var type = systemRef.GetSystemType();
                
                // 如果通过完整类型名找不到，尝试在所有程序集中搜索
                if (type == null && !string.IsNullOrEmpty(systemRef.AssemblyQualifiedName))
                {
                    string typeName = systemRef.AssemblyQualifiedName.Split(',')[0].Trim();
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(typeName);
                        if (type != null) break;
                    }
                }
                
                if (type == null)
                {
                    Debug.LogWarning($"[ECSSystemRunner] 找不到System类型: {systemRef.AssemblyQualifiedName}");
                    continue;
                }
                
                if (!typeof(IECSSystem).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"[ECSSystemRunner] {type.Name} 未实现 IECSSystem 接口");
                    continue;
                }
                
                try
                {
                    var system = (IECSSystem)Activator.CreateInstance(type);
                    system.World = _world;
                    system.OnCreate();
                    _instantiatedSystems.Add(system);
                    AddSystemToWorld(system);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ECSSystemRunner] 实例化System失败 {type.Name}: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// 将System注册到World的调度列表中
        /// </summary>
        private void AddSystemToWorld(IECSSystem system)
        {
            // 通过反射访问World的私有字段
            var systemsField = typeof(ECSWorld).GetField("_systems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var phaseField = typeof(ECSWorld).GetField("_systemsByPhase", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (systemsField != null)
            {
                var systems = (List<IECSSystem>)systemsField.GetValue(_world);
                systems.Add(system);
            }
            
            if (phaseField != null)
            {
                var systemsByPhase = (List<IECSSystem>[])phaseField.GetValue(_world);
                if (systemsByPhase == null)
                {
                    systemsByPhase = new List<IECSSystem>[4];
                    for (int i = 0; i < 4; i++)
                    {
                        systemsByPhase[i] = new List<IECSSystem>();
                    }
                    phaseField.SetValue(_world, systemsByPhase);
                }
                systemsByPhase[(int)system.Phase].Add(system);
            }
        }
        
        /// <summary>
        /// 手动触发一次World更新
        /// </summary>
        public void ManualUpdate()
        {
            _world?.Update();
        }
        
        /// <summary>
        /// 获取指定类型的System实例
        /// </summary>
        public T GetSystem<T>() where T : class, IECSSystem
        {
            foreach (var system in _instantiatedSystems)
            {
                if (system is T typed) return typed;
            }
            return null;
        }
        
        /// <summary>
        /// 运行时添加System引用
        /// </summary>
        public void AddSystemReference<T>() where T : IECSSystem
        {
            var reference = new ECSSystemReference
            {
                AssemblyQualifiedName = typeof(T).AssemblyQualifiedName,
                Enabled = true
            };
            _systems.Add(reference);
        }
        
        private void Cleanup()
        {
            foreach (var system in _instantiatedSystems)
            {
                system.OnDestroy();
            }
            _instantiatedSystems.Clear();
            
            if (_autoCreateWorld && !string.IsNullOrEmpty(_worldName))
            {
                ECSKit.DestroyWorld(_worldName);
            }
            
            _world = null;
            _initialized = false;
        }
    }
}
