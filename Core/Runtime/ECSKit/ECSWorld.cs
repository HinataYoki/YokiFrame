using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace YokiFrame.ECS
{
    /// <summary>
    /// ECS World，管理所有实体、Archetype和System
    /// 是ECS架构的核心容器
    /// </summary>
    public unsafe class ECSWorld : IDisposable
    {
        private static int _worldIdCounter = 0;
        
        /// <summary>
        /// World的唯一标识
        /// </summary>
        public int WorldId { get; }
        
        /// <summary>
        /// World名称
        /// </summary>
        public string Name { get; }
        
        private EntityMetadata[] _entityMetadata;
        private int _entityCapacity;
        private long _nextEntityId;
        private Stack<long> _freeEntityIds;
        
        private List<Archetype> _archetypes;
        private Dictionary<int, Archetype> _archetypeHashMap;
        
        private List<IECSSystem> _systems;
        
        private const int InitialEntityCapacity = 1024;
        
        /// <summary>
        /// 当前存活的实体数量
        /// </summary>
        public int EntityCount { get; private set; }
        
        /// <summary>
        /// Archetype数量
        /// </summary>
        public int ArchetypeCount => _archetypes.Count;
        
        public ECSWorld(string name = "DefaultWorld")
        {
            WorldId = _worldIdCounter++;
            Name = name;
            
            _entityMetadata = new EntityMetadata[InitialEntityCapacity];
            _entityCapacity = InitialEntityCapacity;
            _nextEntityId = 0;
            _freeEntityIds = new Stack<long>(256);
            
            _archetypes = new List<Archetype>();
            _archetypeHashMap = new Dictionary<int, Archetype>();
            
            _systems = new List<IECSSystem>();
        }
        
        #region 实体管理
        
        /// <summary>
        /// 创建实体，指定静态组件类型
        /// </summary>
        public Entity CreateEntity(params ComponentType[] staticComponentTypes)
        {
            var archetype = GetOrCreateArchetype(staticComponentTypes);
            return CreateEntityInArchetype(archetype);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Entity CreateEntityInArchetype(Archetype archetype)
        {
            long entityId;
            if (_freeEntityIds.Count > 0)
            {
                entityId = _freeEntityIds.Pop();
            }
            else
            {
                entityId = _nextEntityId++;
                EnsureEntityCapacity(entityId);
            }
            
            var location = archetype.AddEntity(entityId);
            
            ref var metadata = ref _entityMetadata[entityId];
            metadata.Location = location;
            metadata.Version++;
            metadata.IsAlive = true;
            
            EntityCount++;
            
            return new Entity(entityId, metadata.Version);
        }
        
        // 泛型Archetype缓存，避免重复查找
        private static class ArchetypeCache<T1> where T1 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2> where T1 : unmanaged where T2 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2, T3> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2, T3, T4> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2, T3, T4, T5> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2, T3, T4, T5, T6> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        private static class ArchetypeCache<T1, T2, T3, T4, T5, T6, T7> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            public static Archetype Archetype;
            public static int WorldId = -1;
        }
        
        /// <summary>
        /// 创建带1个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1>() where T1 : unmanaged
        {
            if (ArchetypeCache<T1>.WorldId != WorldId)
            {
                ArchetypeCache<T1>.Archetype = GetOrCreateArchetype(new[] { ComponentTypeRegistry.Get<T1>() });
                ArchetypeCache<T1>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1>.Archetype);
        }
        
        /// <summary>
        /// 创建带2个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2>() 
            where T1 : unmanaged 
            where T2 : unmanaged
        {
            if (ArchetypeCache<T1, T2>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>() });
                ArchetypeCache<T1, T2>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2>.Archetype);
        }
        
        /// <summary>
        /// 创建带3个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2, T3>() 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
        {
            if (ArchetypeCache<T1, T2, T3>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2, T3>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>(),
                    ComponentTypeRegistry.Get<T3>() });
                ArchetypeCache<T1, T2, T3>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2, T3>.Archetype);
        }
        
        /// <summary>
        /// 创建带4个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2, T3, T4>() 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            if (ArchetypeCache<T1, T2, T3, T4>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2, T3, T4>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>(),
                    ComponentTypeRegistry.Get<T3>(),
                    ComponentTypeRegistry.Get<T4>() });
                ArchetypeCache<T1, T2, T3, T4>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2, T3, T4>.Archetype);
        }
        
        /// <summary>
        /// 创建带5个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2, T3, T4, T5>() 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
        {
            if (ArchetypeCache<T1, T2, T3, T4, T5>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2, T3, T4, T5>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>(),
                    ComponentTypeRegistry.Get<T3>(),
                    ComponentTypeRegistry.Get<T4>(),
                    ComponentTypeRegistry.Get<T5>() });
                ArchetypeCache<T1, T2, T3, T4, T5>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2, T3, T4, T5>.Archetype);
        }
        
        /// <summary>
        /// 创建带6个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2, T3, T4, T5, T6>() 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
        {
            if (ArchetypeCache<T1, T2, T3, T4, T5, T6>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2, T3, T4, T5, T6>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>(),
                    ComponentTypeRegistry.Get<T3>(),
                    ComponentTypeRegistry.Get<T4>(),
                    ComponentTypeRegistry.Get<T5>(),
                    ComponentTypeRegistry.Get<T6>() });
                ArchetypeCache<T1, T2, T3, T4, T5, T6>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2, T3, T4, T5, T6>.Archetype);
        }
        
        /// <summary>
        /// 创建带7个组件的实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<T1, T2, T3, T4, T5, T6, T7>() 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
            where T5 : unmanaged
            where T6 : unmanaged
            where T7 : unmanaged
        {
            if (ArchetypeCache<T1, T2, T3, T4, T5, T6, T7>.WorldId != WorldId)
            {
                ArchetypeCache<T1, T2, T3, T4, T5, T6, T7>.Archetype = GetOrCreateArchetype(new[] {
                    ComponentTypeRegistry.Get<T1>(),
                    ComponentTypeRegistry.Get<T2>(),
                    ComponentTypeRegistry.Get<T3>(),
                    ComponentTypeRegistry.Get<T4>(),
                    ComponentTypeRegistry.Get<T5>(),
                    ComponentTypeRegistry.Get<T6>(),
                    ComponentTypeRegistry.Get<T7>() });
                ArchetypeCache<T1, T2, T3, T4, T5, T6, T7>.WorldId = WorldId;
            }
            return CreateEntityInArchetype(ArchetypeCache<T1, T2, T3, T4, T5, T6, T7>.Archetype);
        }
        
        /// <summary>
        /// 立即销毁实体
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            if (!IsAlive(entity)) return;
            
            ref var metadata = ref _entityMetadata[entity.Id];
            var location = metadata.Location;
            
            var archetype = _archetypes[location.ArchetypeIndex];
            archetype.RemoveEntity(location.ChunkIndex, location.IndexInChunk);
            
            metadata.IsAlive = false;
            metadata.Location = EntityLocation.Invalid;
            
            _freeEntityIds.Push(entity.Id);
            
            EntityCount--;
        }
        
        /// <summary>
        /// 检查实体是否存活
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(Entity entity)
        {
            if (entity.Id < 0 || entity.Id >= _entityCapacity) return false;
            ref var metadata = ref _entityMetadata[entity.Id];
            return metadata.IsAlive && metadata.Version == entity.Version;
        }
        
        /// <summary>
        /// 获取实体在Archetype中的位置
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityLocation GetEntityLocation(Entity entity)
        {
            if (!IsAlive(entity)) return EntityLocation.Invalid;
            return _entityMetadata[entity.Id].Location;
        }
        
        private void EnsureEntityCapacity(long entityId)
        {
            if (entityId < _entityCapacity) return;
            
            int newCapacity = _entityCapacity;
            while (newCapacity <= entityId) newCapacity *= 2;
            
            var newMetadata = new EntityMetadata[newCapacity];
            Array.Copy(_entityMetadata, newMetadata, _entityCapacity);
            _entityMetadata = newMetadata;
            _entityCapacity = newCapacity;
        }
        
        private void OnEntityMoved(long entityId, EntityLocation newLocation)
        {
            if (entityId >= 0 && entityId < _entityCapacity)
            {
                _entityMetadata[entityId].Location = newLocation;
            }
        }
        
        #endregion
        
        #region Archetype管理
        
        private Archetype GetOrCreateArchetype(ComponentType[] componentTypes)
        {
            int hash = ComputeArchetypeHash(componentTypes);
            
            if (_archetypeHashMap.TryGetValue(hash, out var existing))
            {
                return existing;
            }
            
            var archetype = new Archetype(_archetypes.Count, componentTypes);
            archetype.OnEntityMoved = OnEntityMoved;
            
            _archetypes.Add(archetype);
            _archetypeHashMap[hash] = archetype;
            
            return archetype;
        }
        
        private static int ComputeArchetypeHash(ComponentType[] types)
        {
            var sorted = new ComponentType[types.Length];
            Array.Copy(types, sorted, types.Length);
            Array.Sort(sorted);
            
            int hash = 17;
            foreach (var t in sorted)
            {
                hash = hash * 31 + t.TypeIndex;
            }
            return hash;
        }
        
        /// <summary>
        /// 获取指定索引的Archetype
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(int index) => _archetypes[index];
        
        /// <summary>
        /// 获取所有Archetype
        /// </summary>
        public IReadOnlyList<Archetype> GetAllArchetypes() => _archetypes;
        
        #endregion
        
        #region 组件访问
        
        /// <summary>
        /// 设置实体的组件值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetComponent<T>(Entity entity, T value) where T : unmanaged
        {
            if (!IsAlive(entity)) return;
            
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            
            if (archetype.HasStaticComponent<T>())
            {
                archetype.SetStaticComponent(location.ChunkIndex, location.IndexInChunk, value);
            }
            else
            {
                ref var chunk = ref archetype.GetChunk(location.ChunkIndex);
                chunk.SetDynamicComponent(location.IndexInChunk, value);
            }
        }
        
        /// <summary>
        /// 获取实体组件的引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponent<T>(Entity entity) where T : unmanaged
        {
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            return ref archetype.GetStaticComponent<T>(location.ChunkIndex, location.IndexInChunk);
        }
        
        /// <summary>
        /// 尝试获取实体的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetComponent<T>(Entity entity, out T value) where T : unmanaged
        {
            value = default;
            if (!IsAlive(entity)) return false;
            
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            
            if (archetype.HasStaticComponent<T>())
            {
                value = archetype.GetStaticComponent<T>(location.ChunkIndex, location.IndexInChunk);
                return true;
            }
            
            return archetype.TryGetDynamicComponent<T>(location.ChunkIndex, location.IndexInChunk, out value);
        }
        
        /// <summary>
        /// 检查实体是否有指定组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent<T>(Entity entity) where T : unmanaged
        {
            if (!IsAlive(entity)) return false;
            
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            
            if (archetype.HasStaticComponent<T>()) return true;
            
            ref var chunk = ref archetype.GetChunk(location.ChunkIndex);
            return chunk.HasDynamicComponent<T>(location.IndexInChunk);
        }
        
        /// <summary>
        /// 添加动态组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddComponent<T>(Entity entity, T value) where T : unmanaged
        {
            if (!IsAlive(entity)) return false;
            
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            
            if (archetype.HasStaticComponent<T>())
            {
                archetype.SetStaticComponent(location.ChunkIndex, location.IndexInChunk, value);
                return true;
            }
            
            return archetype.AddDynamicComponent(location.ChunkIndex, location.IndexInChunk, value);
        }
        
        /// <summary>
        /// 移除动态组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveComponent<T>(Entity entity) where T : unmanaged
        {
            if (!IsAlive(entity)) return false;
            
            var location = _entityMetadata[entity.Id].Location;
            var archetype = _archetypes[location.ArchetypeIndex];
            
            if (archetype.HasStaticComponent<T>()) return false;
            
            return archetype.RemoveDynamicComponent<T>(location.ChunkIndex, location.IndexInChunk);
        }
        
        #endregion
        
        #region System管理
        
        private List<IECSSystem>[] _systemsByPhase;
        private const int PhaseCount = 4;
        
        private void InitSystemPhases()
        {
            if (_systemsByPhase == null)
            {
                _systemsByPhase = new List<IECSSystem>[PhaseCount];
                for (int i = 0; i < PhaseCount; i++)
                {
                    _systemsByPhase[i] = new List<IECSSystem>();
                }
            }
        }
        
        /// <summary>
        /// 添加System到World
        /// </summary>
        public T AddSystem<T>() where T : IECSSystem, new()
        {
            InitSystemPhases();
            
            var system = new T();
            system.World = this;
            _systems.Add(system);
            
            int phaseIndex = (int)system.Phase;
            _systemsByPhase[phaseIndex].Add(system);
            
            system.OnCreate();
            return system;
        }
        
        /// <summary>
        /// 更新所有System，按阶段顺序执行
        /// Creation -> Logic -> Destruction -> Sync
        /// </summary>
        public void Update()
        {
            if (_systemsByPhase == null) return;
            
            for (int phase = 0; phase < PhaseCount; phase++)
            {
                var systems = _systemsByPhase[phase];
                for (int i = 0; i < systems.Count; i++)
                {
                    systems[i].OnUpdate();
                }
                
                // Creation阶段后处理SpawnRequest
                if (phase == (int)SystemPhase.Creation)
                {
                    ProcessSpawnRequests();
                }
                // Destruction阶段后处理DestroyTag
                else if (phase == (int)SystemPhase.Destruction)
                {
                    ProcessDestroyTags();
                }
            }
        }
        
        #endregion
        
        #region 延迟销毁
        
        private HashSet<long> _pendingDestroyEntities = new HashSet<long>();
        
        /// <summary>
        /// 实体销毁时的回调，参数为(entityId, gameObjectInstanceId)
        /// </summary>
        public Action<long, int> OnEntityDestroyed;
        
        /// <summary>
        /// SpawnRequest处理回调
        /// </summary>
        public Action<SpawnRequest> OnSpawnRequest;
        
        /// <summary>
        /// 标记实体为待销毁（延迟到Destruction阶段执行）
        /// </summary>
        public void DestroyEntityDeferred(Entity entity)
        {
            if (IsAlive(entity))
            {
                _pendingDestroyEntities.Add(entity.Id);
            }
        }
        
        /// <summary>
        /// 标记实体为待销毁（通过ID）
        /// </summary>
        public void DestroyEntityDeferred(long entityId)
        {
            if (entityId >= 0 && entityId < _entityCapacity && _entityMetadata[entityId].IsAlive)
            {
                _pendingDestroyEntities.Add(entityId);
            }
        }
        
        private EntityQuery _destroyTagQuery;
        
        private unsafe void ProcessDestroyTags()
        {
            // 查询所有带DestroyTag的实体
            if (_destroyTagQuery == null)
            {
                _destroyTagQuery = Query().With<DestroyTag>();
            }
            
            var archetypes = _destroyTagQuery.GetMatchingArchetypes();
            foreach (var archetype in archetypes)
            {
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    
                    for (int i = 0; i < count; i++)
                    {
                        long entityId = chunk.GetEntityId(i);
                        _pendingDestroyEntities.Add(entityId);
                    }
                }
            }
            
            // 执行销毁
            foreach (var entityId in _pendingDestroyEntities)
            {
                if (entityId >= 0 && entityId < _entityCapacity)
                {
                    ref var metadata = ref _entityMetadata[entityId];
                    if (metadata.IsAlive)
                    {
                        var location = metadata.Location;
                        if (location.ArchetypeIndex >= 0 && location.ArchetypeIndex < _archetypes.Count)
                        {
                            var archetype = _archetypes[location.ArchetypeIndex];
                            
                            // 通知清理GameObject
                            if (OnEntityDestroyed != null && archetype.HasStaticComponent<RenderRef>())
                            {
                                var renderRef = archetype.GetStaticComponent<RenderRef>(location.ChunkIndex, location.IndexInChunk);
                                OnEntityDestroyed(entityId, renderRef.GameObjectInstanceId);
                            }
                            
                            archetype.RemoveEntity(location.ChunkIndex, location.IndexInChunk);
                        }
                        
                        metadata.IsAlive = false;
                        metadata.Location = EntityLocation.Invalid;
                        
                        _freeEntityIds.Push(entityId);
                        EntityCount--;
                    }
                }
            }
            _pendingDestroyEntities.Clear();
        }
        
        private EntityQuery _spawnRequestQuery;
        private List<SpawnRequest> _pendingSpawnRequests = new List<SpawnRequest>();
        
        private unsafe void ProcessSpawnRequests()
        {
            if (OnSpawnRequest == null) return;
            
            if (_spawnRequestQuery == null)
            {
                _spawnRequestQuery = Query().With<SpawnRequest>();
            }
            
            var archetypes = _spawnRequestQuery.GetMatchingArchetypes();
            
            // 收集请求
            _pendingSpawnRequests.Clear();
            foreach (var archetype in archetypes)
            {
                int reqIndex = archetype.GetStaticComponentIndex<SpawnRequest>();
                if (reqIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    
                    for (int i = 0; i < count; i++)
                    {
                        var request = chunk.GetStaticComponent<SpawnRequest>(reqIndex, i);
                        _pendingSpawnRequests.Add(request);
                        
                        long entityId = chunk.GetEntityId(i);
                        _pendingDestroyEntities.Add(entityId);
                    }
                }
            }
            
            // 销毁请求实体
            foreach (var entityId in _pendingDestroyEntities)
            {
                if (entityId >= 0 && entityId < _entityCapacity)
                {
                    ref var metadata = ref _entityMetadata[entityId];
                    if (metadata.IsAlive)
                    {
                        var location = metadata.Location;
                        if (location.ArchetypeIndex >= 0 && location.ArchetypeIndex < _archetypes.Count)
                        {
                            var archetype = _archetypes[location.ArchetypeIndex];
                            archetype.RemoveEntity(location.ChunkIndex, location.IndexInChunk);
                        }
                        
                        metadata.IsAlive = false;
                        metadata.Location = EntityLocation.Invalid;
                        
                        _freeEntityIds.Push(entityId);
                        EntityCount--;
                    }
                }
            }
            _pendingDestroyEntities.Clear();
            
            // 处理生成请求
            foreach (var request in _pendingSpawnRequests)
            {
                OnSpawnRequest(request);
            }
            _pendingSpawnRequests.Clear();
        }
        
        #endregion
        
        #region 查询
        
        /// <summary>
        /// 创建实体查询
        /// </summary>
        public EntityQuery Query()
        {
            return new EntityQuery(this);
        }
        
        #endregion
        
        public void Dispose()
        {
            foreach (var system in _systems)
            {
                system.OnDestroy();
            }
            _systems.Clear();
            
            foreach (var archetype in _archetypes)
            {
                archetype.Dispose();
            }
            _archetypes.Clear();
            _archetypeHashMap.Clear();
            
            EntityCount = 0;
        }
    }
}
