using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace YokiFrame.ECS
{
    /// <summary>
    /// Archetype - 管理相同静态组件组合的所有实体
    /// 使用指针数组存储 Chunk，避免扩容时 struct 复制导致的引用失效
    /// </summary>
    public unsafe class Archetype : IDisposable
    {
        public int Index { get; internal set; }
        
        private ComponentType[] _staticComponentTypes;
        
        // 使用指针数组存储 Chunk，扩容时只复制指针
        private Chunk** _chunkPtrs;
        private int _chunkCapacity;
        private int _chunkCount;
        private int _totalEntityCount;
        
        private int[] _typeIndexToComponentIndex;
        private int _maxTypeIndex;
        
        public Action<long, EntityLocation> OnEntityMoved;
        
        public int ChunkCount => _chunkCount;
        public int EntityCount => _totalEntityCount;
        public ReadOnlySpan<ComponentType> StaticComponentTypes => _staticComponentTypes;
        
        private const int InitialChunkCapacity = 64;
        
        public Archetype(int index, ComponentType[] staticComponentTypes)
        {
            Index = index;
            
            _staticComponentTypes = new ComponentType[staticComponentTypes.Length];
            Array.Copy(staticComponentTypes, _staticComponentTypes, staticComponentTypes.Length);
            Array.Sort(_staticComponentTypes);
            
            _chunkCapacity = InitialChunkCapacity;
            _chunkPtrs = (Chunk**)UnsafeUtility.Malloc(sizeof(Chunk*) * _chunkCapacity, 8, Allocator.Persistent);
            UnsafeUtility.MemClear(_chunkPtrs, sizeof(Chunk*) * _chunkCapacity);
            _chunkCount = 0;
            _totalEntityCount = 0;
            
            _maxTypeIndex = 0;
            foreach (var ct in _staticComponentTypes)
            {
                if (ct.TypeIndex > _maxTypeIndex) _maxTypeIndex = ct.TypeIndex;
            }
            
            _typeIndexToComponentIndex = new int[_maxTypeIndex + 1];
            for (int i = 0; i <= _maxTypeIndex; i++) _typeIndexToComponentIndex[i] = -1;
            for (int i = 0; i < _staticComponentTypes.Length; i++)
            {
                _typeIndexToComponentIndex[_staticComponentTypes[i].TypeIndex] = i;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasStaticComponent<T>() where T : unmanaged
        {
            var typeIndex = ComponentTypeRegistry.GetIndex<T>();
            return typeIndex <= _maxTypeIndex && _typeIndexToComponentIndex[typeIndex] >= 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStaticComponentIndex<T>() where T : unmanaged
        {
            var typeIndex = ComponentTypeRegistry.GetIndex<T>();
            return typeIndex <= _maxTypeIndex ? _typeIndexToComponentIndex[typeIndex] : -1;
        }
        
        public EntityLocation AddEntity(long entityId)
        {
            if (_chunkCount > 0)
            {
                var lastChunk = _chunkPtrs[_chunkCount - 1];
                if (lastChunk->EntityCount < Chunk.MaxEntitiesPerChunk)
                {
                    int index = lastChunk->AddEntity(entityId);
                    _totalEntityCount++;
                    return new EntityLocation 
                    { 
                        ArchetypeIndex = Index, 
                        ChunkIndex = _chunkCount - 1, 
                        IndexInChunk = index 
                    };
                }
            }
            
            EnsureChunkCapacity();
            
            var newChunk = (Chunk*)UnsafeUtility.Malloc(sizeof(Chunk), 64, Allocator.Persistent);
            *newChunk = Chunk.Create(_staticComponentTypes);
            _chunkPtrs[_chunkCount] = newChunk;
            
            int newIndex = newChunk->AddEntity(entityId);
            _chunkCount++;
            _totalEntityCount++;
            
            return new EntityLocation 
            { 
                ArchetypeIndex = Index, 
                ChunkIndex = _chunkCount - 1, 
                IndexInChunk = newIndex 
            };
        }
        
        public void RemoveEntity(int chunkIndex, int indexInChunk)
        {
            if (chunkIndex < 0 || chunkIndex >= _chunkCount || _totalEntityCount == 0) return;
            
            int lastChunkIndex = _chunkCount - 1;
            var lastChunk = _chunkPtrs[lastChunkIndex];
            int lastIndexInLastChunk = lastChunk->EntityCount - 1;
            
            if (chunkIndex == lastChunkIndex && indexInChunk == lastIndexInLastChunk)
            {
                lastChunk->RemoveLastEntity();
            }
            else if (chunkIndex == lastChunkIndex)
            {
                long movedEntityId = lastChunk->GetLastEntityId();
                _chunkPtrs[chunkIndex]->SwapBackInternal(indexInChunk);
                
                OnEntityMoved?.Invoke(movedEntityId, new EntityLocation
                {
                    ArchetypeIndex = Index,
                    ChunkIndex = chunkIndex,
                    IndexInChunk = indexInChunk
                });
            }
            else
            {
                long movedEntityId = lastChunk->GetLastEntityId();
                _chunkPtrs[chunkIndex]->CopyEntityFrom(indexInChunk, ref *lastChunk, lastIndexInLastChunk);
                lastChunk->RemoveLastEntity();
                
                OnEntityMoved?.Invoke(movedEntityId, new EntityLocation
                {
                    ArchetypeIndex = Index,
                    ChunkIndex = chunkIndex,
                    IndexInChunk = indexInChunk
                });
            }
            
            _totalEntityCount--;
            
            if (lastChunk->IsEmpty && _chunkCount > 0)
            {
                lastChunk->Dispose();
                UnsafeUtility.Free(lastChunk, Allocator.Persistent);
                _chunkPtrs[lastChunkIndex] = null;
                _chunkCount--;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureChunkCapacity()
        {
            if (_chunkCount >= _chunkCapacity)
            {
                int newCapacity = _chunkCapacity * 2;
                var newPtrs = (Chunk**)UnsafeUtility.Malloc(sizeof(Chunk*) * newCapacity, 8, Allocator.Persistent);
                UnsafeUtility.MemCpy(newPtrs, _chunkPtrs, sizeof(Chunk*) * _chunkCapacity);
                UnsafeUtility.MemClear(newPtrs + _chunkCapacity, sizeof(Chunk*) * (_chunkCapacity));
                UnsafeUtility.Free(_chunkPtrs, Allocator.Persistent);
                _chunkPtrs = newPtrs;
                _chunkCapacity = newCapacity;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Chunk GetChunk(int index) => ref *_chunkPtrs[index];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChunkCount() => _chunkCount;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStaticComponent<T>(int chunkIndex, int indexInChunk, T value) where T : unmanaged
        {
            var componentIndex = GetStaticComponentIndex<T>();
            if (componentIndex < 0) return;
            _chunkPtrs[chunkIndex]->SetStaticComponent(componentIndex, indexInChunk, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetStaticComponent<T>(int chunkIndex, int indexInChunk) where T : unmanaged
        {
            var componentIndex = GetStaticComponentIndex<T>();
            return ref _chunkPtrs[chunkIndex]->GetStaticComponent<T>(componentIndex, indexInChunk);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddDynamicComponent<T>(int chunkIndex, int indexInChunk, T value) where T : unmanaged
        {
            return _chunkPtrs[chunkIndex]->AddDynamicComponent(indexInChunk, value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveDynamicComponent<T>(int chunkIndex, int indexInChunk) where T : unmanaged
        {
            return _chunkPtrs[chunkIndex]->RemoveDynamicComponent<T>(indexInChunk);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetDynamicComponent<T>(int chunkIndex, int indexInChunk, out T value) where T : unmanaged
        {
            return _chunkPtrs[chunkIndex]->TryGetDynamicComponent(indexInChunk, out value);
        }
        
        public void Dispose()
        {
            for (int i = 0; i < _chunkCount; i++)
            {
                if (_chunkPtrs[i] != null)
                {
                    _chunkPtrs[i]->Dispose();
                    UnsafeUtility.Free(_chunkPtrs[i], Allocator.Persistent);
                }
            }
            
            if (_chunkPtrs != null)
            {
                UnsafeUtility.Free(_chunkPtrs, Allocator.Persistent);
                _chunkPtrs = null;
            }
            
            _chunkCount = 0;
            _chunkCapacity = 0;
            _totalEntityCount = 0;
        }
    }
}
