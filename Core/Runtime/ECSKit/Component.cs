using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 组件数据标记接口 - 所有组件必须实现此接口
    /// </summary>
    public interface IComponentData { }
    
    /// <summary>
    /// 组件类型标识
    /// </summary>
    public readonly struct ComponentType : IEquatable<ComponentType>, IComparable<ComponentType>
    {
        public readonly int TypeIndex;
        public readonly int Size;
        public readonly int Alignment;
        
        public ComponentType(int typeIndex, int size, int alignment)
        {
            TypeIndex = typeIndex;
            Size = size;
            Alignment = alignment;
        }
        
        public bool Equals(ComponentType other) => TypeIndex == other.TypeIndex;
        public override int GetHashCode() => TypeIndex;
        public override bool Equals(object obj) => obj is ComponentType other && Equals(other);
        public int CompareTo(ComponentType other) => TypeIndex.CompareTo(other.TypeIndex);
    }
    
    /// <summary>
    /// 组件类型注册表
    /// </summary>
    public static class ComponentTypeRegistry
    {
        private static int _nextTypeIndex = 0;
        private static readonly System.Collections.Generic.Dictionary<Type, ComponentType> _typeMap = 
            new System.Collections.Generic.Dictionary<Type, ComponentType>();
        private static readonly System.Collections.Generic.Dictionary<int, Type> _indexToTypeMap = 
            new System.Collections.Generic.Dictionary<int, Type>();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentType Get<T>() where T : unmanaged
        {
            var type = typeof(T);
            if (!_typeMap.TryGetValue(type, out var componentType))
            {
                componentType = new ComponentType(
                    _nextTypeIndex++, 
                    UnsafeUtility.SizeOf<T>(),
                    UnsafeUtility.AlignOf<T>());
                _typeMap[type] = componentType;
                _indexToTypeMap[componentType.TypeIndex] = type;
            }
            return componentType;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex<T>() where T : unmanaged => Get<T>().TypeIndex;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type GetTypeByIndex(int typeIndex)
        {
            return _indexToTypeMap.TryGetValue(typeIndex, out var type) ? type : null;
        }
    }
}
