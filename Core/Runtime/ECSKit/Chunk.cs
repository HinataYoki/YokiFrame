using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace YokiFrame.ECS
{
    /// <summary>
    /// Chunk - 存储固定数量实体的连续内存块
    /// 内存布局紧凑，Cache友好，支持Prefetching
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct Chunk : IDisposable
    {
        public const int MaxEntitiesPerChunk = 128;
        public const int MaxStaticComponents = 16;
        
        private fixed long _entityIds[MaxEntitiesPerChunk];
        private fixed long _staticComponentArrays[MaxStaticComponents];
        private fixed int _staticComponentSizes[MaxStaticComponents];
        private fixed int _staticComponentTypeIndices[MaxStaticComponents];
        private int _staticComponentCount;
        private int _entityCount;
        
        // 移除 _dynamicStorages，太大了导致问题
        // private fixed byte _dynamicStorages[MaxEntitiesPerChunk * 128];
        private long _dynamicStoragePtr; // 改用指针
        
        public int EntityCount => _entityCount;
        public bool IsFull => _entityCount >= MaxEntitiesPerChunk;
        public bool IsEmpty => _entityCount == 0;
        
        public static Chunk Create(ComponentType[] staticComponentTypes)
        {
            var chunk = new Chunk();
            chunk._entityCount = 0;
            chunk._staticComponentCount = Math.Min(staticComponentTypes.Length, MaxStaticComponents);
            
            for (int i = 0; i < chunk._staticComponentCount; i++)
            {
                var ct = staticComponentTypes[i];
                chunk._staticComponentSizes[i] = ct.Size;
                chunk._staticComponentTypeIndices[i] = ct.TypeIndex;
                
                int alignment = Math.Max(ct.Alignment, 64);
                var ptr = UnsafeUtility.Malloc(ct.Size * MaxEntitiesPerChunk, alignment, Allocator.Persistent);
                UnsafeUtility.MemClear(ptr, ct.Size * MaxEntitiesPerChunk);
                chunk._staticComponentArrays[i] = (long)ptr;
            }
            
            // 分配动态组件存储
            int dynamicStorageSize = UnsafeUtility.SizeOf<DynamicComponentStorage>() * MaxEntitiesPerChunk;
            var dynamicPtr = UnsafeUtility.Malloc(dynamicStorageSize, 8, Allocator.Persistent);
            UnsafeUtility.MemClear(dynamicPtr, dynamicStorageSize);
            chunk._dynamicStoragePtr = (long)dynamicPtr;
            
            return chunk;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AddEntity(long entityId)
        {
            if (_entityCount >= MaxEntitiesPerChunk) return -1;
            int index = _entityCount++;
            _entityIds[index] = entityId;
            GetDynamicStorage(index) = default;
            return index;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetEntityId(int index) => _entityIds[index];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetLastEntityId() => _entityCount > 0 ? _entityIds[_entityCount - 1] : -1;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long RemoveLastEntity()
        {
            if (_entityCount == 0) return -1;
            int lastIndex = --_entityCount;
            GetDynamicStorage(lastIndex).Dispose();
            return _entityIds[lastIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwapBackInternal(int index)
        {
            int lastIndex = _entityCount - 1;
            GetDynamicStorage(index).Dispose();
            
            if (index < lastIndex)
            {
                _entityIds[index] = _entityIds[lastIndex];
                
                for (int i = 0; i < _staticComponentCount; i++)
                {
                    var size = _staticComponentSizes[i];
                    var ptr = (byte*)_staticComponentArrays[i];
                    UnsafeUtility.MemCpy(ptr + index * size, ptr + lastIndex * size, size);
                }
                
                GetDynamicStorage(index) = GetDynamicStorage(lastIndex);
                GetDynamicStorage(lastIndex) = default;
            }
            
            _entityCount--;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyEntityFrom(int destIndex, ref Chunk srcChunk, int srcIndex)
        {
            GetDynamicStorage(destIndex).Dispose();
            _entityIds[destIndex] = srcChunk._entityIds[srcIndex];
            
            for (int i = 0; i < _staticComponentCount; i++)
            {
                var size = _staticComponentSizes[i];
                var destPtr = (byte*)_staticComponentArrays[i] + destIndex * size;
                var srcPtr = (byte*)srcChunk._staticComponentArrays[i] + srcIndex * size;
                UnsafeUtility.MemCpy(destPtr, srcPtr, size);
            }
            
            GetDynamicStorage(destIndex) = srcChunk.GetDynamicStorage(srcIndex);
            srcChunk.GetDynamicStorage(srcIndex) = default;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStaticComponentIndex(int typeIndex)
        {
            for (int i = 0; i < _staticComponentCount; i++)
            {
                if (_staticComponentTypeIndices[i] == typeIndex) return i;
            }
            return -1;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetStaticComponentArray<T>(int componentIndex) where T : unmanaged
        {
            return (T*)_staticComponentArrays[componentIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetStaticComponent<T>(int componentIndex, int entityIndex) where T : unmanaged
        {
            var ptr = (T*)_staticComponentArrays[componentIndex];
            return ref ptr[entityIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStaticComponent<T>(int componentIndex, int entityIndex, T value) where T : unmanaged
        {
            var ptr = (T*)_staticComponentArrays[componentIndex];
            ptr[entityIndex] = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref DynamicComponentStorage GetDynamicStorage(int entityIndex)
        {
            var ptr = (DynamicComponentStorage*)_dynamicStoragePtr;
            return ref ptr[entityIndex];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDynamicComponent<T>(int entityIndex) where T : unmanaged
        {
            return GetDynamicStorage(entityIndex).HasComponent(ComponentTypeRegistry.GetIndex<T>());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetDynamicComponent<T>(int entityIndex, out T value) where T : unmanaged
        {
            return GetDynamicStorage(entityIndex).TryGetComponent(out value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddDynamicComponent<T>(int entityIndex, T value) where T : unmanaged
        {
            return GetDynamicStorage(entityIndex).AddComponent(value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveDynamicComponent<T>(int entityIndex) where T : unmanaged
        {
            return GetDynamicStorage(entityIndex).RemoveComponent<T>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDynamicComponent<T>(int entityIndex, T value) where T : unmanaged
        {
            GetDynamicStorage(entityIndex).SetComponent(value);
        }
        
        public void Dispose()
        {
            for (int i = 0; i < _staticComponentCount; i++)
            {
                if (_staticComponentArrays[i] != 0)
                {
                    UnsafeUtility.Free((void*)_staticComponentArrays[i], Allocator.Persistent);
                    _staticComponentArrays[i] = 0;
                }
            }
            
            for (int i = 0; i < _entityCount; i++)
            {
                GetDynamicStorage(i).Dispose();
            }
            
            // 释放动态存储
            if (_dynamicStoragePtr != 0)
            {
                UnsafeUtility.Free((void*)_dynamicStoragePtr, Allocator.Persistent);
                _dynamicStoragePtr = 0;
            }
            
            _entityCount = 0;
        }
    }
    
    /// <summary>
    /// 动态组件存储 - 用于运行时动态添加的组件
    /// </summary>
    public unsafe struct DynamicComponentStorage : IDisposable
    {
        private const int MaxDynamicComponents = 8;
        
        private fixed int _typeIndices[MaxDynamicComponents];
        private fixed int _sizes[MaxDynamicComponents];
        private fixed long _dataPointers[MaxDynamicComponents];
        private int _count;
        
        public int Count => _count;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent(int typeIndex)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_typeIndices[i] == typeIndex) return true;
            }
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetComponent<T>(out T value) where T : unmanaged
        {
            int typeIndex = ComponentTypeRegistry.GetIndex<T>();
            for (int i = 0; i < _count; i++)
            {
                if (_typeIndices[i] == typeIndex)
                {
                    value = *(T*)_dataPointers[i];
                    return true;
                }
            }
            value = default;
            return false;
        }
        
        public bool AddComponent<T>(T value) where T : unmanaged
        {
            if (_count >= MaxDynamicComponents) return false;
            
            int typeIndex = ComponentTypeRegistry.GetIndex<T>();
            if (HasComponent(typeIndex)) return false;
            
            int size = UnsafeUtility.SizeOf<T>();
            void* ptr = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), Allocator.Persistent);
            *(T*)ptr = value;
            
            _typeIndices[_count] = typeIndex;
            _sizes[_count] = size;
            _dataPointers[_count] = (long)ptr;
            _count++;
            
            return true;
        }
        
        public bool RemoveComponent<T>() where T : unmanaged
        {
            int typeIndex = ComponentTypeRegistry.GetIndex<T>();
            
            for (int i = 0; i < _count; i++)
            {
                if (_typeIndices[i] == typeIndex)
                {
                    UnsafeUtility.Free((void*)_dataPointers[i], Allocator.Persistent);
                    
                    int lastIndex = _count - 1;
                    if (i < lastIndex)
                    {
                        _typeIndices[i] = _typeIndices[lastIndex];
                        _sizes[i] = _sizes[lastIndex];
                        _dataPointers[i] = _dataPointers[lastIndex];
                    }
                    _count--;
                    return true;
                }
            }
            return false;
        }
        
        public void SetComponent<T>(T value) where T : unmanaged
        {
            int typeIndex = ComponentTypeRegistry.GetIndex<T>();
            for (int i = 0; i < _count; i++)
            {
                if (_typeIndices[i] == typeIndex)
                {
                    *(T*)_dataPointers[i] = value;
                    return;
                }
            }
            AddComponent(value);
        }
        
        public void Dispose()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_dataPointers[i] != 0)
                {
                    UnsafeUtility.Free((void*)_dataPointers[i], Allocator.Persistent);
                    _dataPointers[i] = 0;
                }
            }
            _count = 0;
        }
    }
}
