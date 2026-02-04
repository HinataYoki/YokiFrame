using System;
using System.Collections.Generic;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 实体查询 - 用于高效遍历符合条件的实体
    /// </summary>
    public class EntityQuery
    {
        private ECSWorld _world;
        private List<int> _requiredTypeIndices;
        private List<Archetype> _matchingArchetypes;
        private int _lastCheckedArchetypeCount;
        
        internal EntityQuery(ECSWorld world)
        {
            _world = world;
            _requiredTypeIndices = new List<int>();
            _matchingArchetypes = new List<Archetype>();
            _lastCheckedArchetypeCount = 0;
        }
        
        public EntityQuery With<T>() where T : unmanaged
        {
            _requiredTypeIndices.Add(ComponentTypeRegistry.GetIndex<T>());
            _matchingArchetypes.Clear();
            _lastCheckedArchetypeCount = 0;
            return this;
        }
        
        public IReadOnlyList<Archetype> GetMatchingArchetypes()
        {
            var allArchetypes = _world.GetAllArchetypes();
            int currentCount = allArchetypes.Count;
            
            if (_lastCheckedArchetypeCount < currentCount)
            {
                for (int i = _lastCheckedArchetypeCount; i < currentCount; i++)
                {
                    if (MatchesArchetype(allArchetypes[i]))
                    {
                        _matchingArchetypes.Add(allArchetypes[i]);
                    }
                }
                _lastCheckedArchetypeCount = currentCount;
            }
            
            return _matchingArchetypes;
        }
        
        private bool MatchesArchetype(Archetype archetype)
        {
            foreach (var typeIndex in _requiredTypeIndices)
            {
                bool found = false;
                foreach (var ct in archetype.StaticComponentTypes)
                {
                    if (ct.TypeIndex == typeIndex)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            return true;
        }
        
        #region ForEach (组件引用)
        
        public unsafe void ForEach<T1>(ActionRef1<T1> action) where T1 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                if (compIndex1 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    
                    for (int i = 0; i < count; i++)
                    {
                        action(ref array1[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEach<T1, T2>(ActionRef2<T1, T2> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                if (compIndex1 < 0 || compIndex2 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    
                    for (int i = 0; i < count; i++)
                    {
                        action(ref array1[i], ref array2[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEach<T1, T2, T3>(ActionRef3<T1, T2, T3> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                int compIndex3 = archetype.GetStaticComponentIndex<T3>();
                if (compIndex1 < 0 || compIndex2 < 0 || compIndex3 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    var array3 = chunk.GetStaticComponentArray<T3>(compIndex3);
                    
                    for (int i = 0; i < count; i++)
                    {
                        action(ref array1[i], ref array2[i], ref array3[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEach<T1, T2, T3, T4>(ActionRef4<T1, T2, T3, T4> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                int compIndex3 = archetype.GetStaticComponentIndex<T3>();
                int compIndex4 = archetype.GetStaticComponentIndex<T4>();
                if (compIndex1 < 0 || compIndex2 < 0 || compIndex3 < 0 || compIndex4 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    var array3 = chunk.GetStaticComponentArray<T3>(compIndex3);
                    var array4 = chunk.GetStaticComponentArray<T4>(compIndex4);
                    
                    for (int i = 0; i < count; i++)
                    {
                        action(ref array1[i], ref array2[i], ref array3[i], ref array4[i]);
                    }
                }
            }
        }
        
        #endregion
        
        #region ForEach (带EntityId)
        
        public unsafe void ForEachWithEntity<T1>(ActionRefWithEntity1<T1> action) where T1 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                if (compIndex1 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    
                    for (int i = 0; i < count; i++)
                    {
                        long entityId = chunk.GetEntityId(i);
                        action(entityId, ref array1[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEachWithEntity<T1, T2>(ActionRefWithEntity2<T1, T2> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                if (compIndex1 < 0 || compIndex2 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    
                    for (int i = 0; i < count; i++)
                    {
                        long entityId = chunk.GetEntityId(i);
                        action(entityId, ref array1[i], ref array2[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEachWithEntity<T1, T2, T3>(ActionRefWithEntity3<T1, T2, T3> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                int compIndex3 = archetype.GetStaticComponentIndex<T3>();
                if (compIndex1 < 0 || compIndex2 < 0 || compIndex3 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    var array3 = chunk.GetStaticComponentArray<T3>(compIndex3);
                    
                    for (int i = 0; i < count; i++)
                    {
                        long entityId = chunk.GetEntityId(i);
                        action(entityId, ref array1[i], ref array2[i], ref array3[i]);
                    }
                }
            }
        }
        
        public unsafe void ForEachWithEntity<T1, T2, T3, T4>(ActionRefWithEntity4<T1, T2, T3, T4> action) 
            where T1 : unmanaged 
            where T2 : unmanaged
            where T3 : unmanaged
            where T4 : unmanaged
        {
            var archetypes = GetMatchingArchetypes();
            int archetypeCount = archetypes.Count;
            
            for (int a = 0; a < archetypeCount; a++)
            {
                var archetype = archetypes[a];
                int compIndex1 = archetype.GetStaticComponentIndex<T1>();
                int compIndex2 = archetype.GetStaticComponentIndex<T2>();
                int compIndex3 = archetype.GetStaticComponentIndex<T3>();
                int compIndex4 = archetype.GetStaticComponentIndex<T4>();
                if (compIndex1 < 0 || compIndex2 < 0 || compIndex3 < 0 || compIndex4 < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var array1 = chunk.GetStaticComponentArray<T1>(compIndex1);
                    var array2 = chunk.GetStaticComponentArray<T2>(compIndex2);
                    var array3 = chunk.GetStaticComponentArray<T3>(compIndex3);
                    var array4 = chunk.GetStaticComponentArray<T4>(compIndex4);
                    
                    for (int i = 0; i < count; i++)
                    {
                        long entityId = chunk.GetEntityId(i);
                        action(entityId, ref array1[i], ref array2[i], ref array3[i], ref array4[i]);
                    }
                }
            }
        }
        
        #endregion
    }
    
    // 委托定义 - 仅组件引用
    public delegate void ActionRef1<T1>(ref T1 c1) where T1 : unmanaged;
    
    public delegate void ActionRef2<T1, T2>(ref T1 c1, ref T2 c2) 
        where T1 : unmanaged 
        where T2 : unmanaged;
    
    public delegate void ActionRef3<T1, T2, T3>(ref T1 c1, ref T2 c2, ref T3 c3) 
        where T1 : unmanaged 
        where T2 : unmanaged
        where T3 : unmanaged;
    
    public delegate void ActionRef4<T1, T2, T3, T4>(ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4) 
        where T1 : unmanaged 
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged;
    
    // 委托定义 - 带EntityId
    public delegate void ActionRefWithEntity1<T1>(long entityId, ref T1 c1) 
        where T1 : unmanaged;
    
    public delegate void ActionRefWithEntity2<T1, T2>(long entityId, ref T1 c1, ref T2 c2) 
        where T1 : unmanaged 
        where T2 : unmanaged;
    
    public delegate void ActionRefWithEntity3<T1, T2, T3>(long entityId, ref T1 c1, ref T2 c2, ref T3 c3) 
        where T1 : unmanaged 
        where T2 : unmanaged
        where T3 : unmanaged;
    
    public delegate void ActionRefWithEntity4<T1, T2, T3, T4>(long entityId, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4) 
        where T1 : unmanaged 
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged;
}
