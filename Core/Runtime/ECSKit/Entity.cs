using System;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 实体句柄 - 轻量级实体引用
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly long Id;
        public readonly int Version;
        
        public Entity(long id, int version)
        {
            Id = id;
            Version = version;
        }
        
        public static readonly Entity Null = new Entity(-1, 0);
        public bool IsNull => Id < 0;
        
        public bool Equals(Entity other) => Id == other.Id && Version == other.Version;
        public override bool Equals(object obj) => obj is Entity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Version);
        public static bool operator ==(Entity left, Entity right) => left.Equals(right);
        public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
        public override string ToString() => $"Entity({Id}:{Version})";
    }
    
    /// <summary>
    /// 实体在 Archetype 中的位置
    /// </summary>
    public struct EntityLocation
    {
        public int ArchetypeIndex;
        public int ChunkIndex;
        public int IndexInChunk;
        
        public static readonly EntityLocation Invalid = new EntityLocation 
        { 
            ArchetypeIndex = -1, 
            ChunkIndex = -1, 
            IndexInChunk = -1 
        };
        
        public bool IsValid => ArchetypeIndex >= 0;
    }
    
    /// <summary>
    /// 实体元数据（内部使用）
    /// </summary>
    internal struct EntityMetadata
    {
        public EntityLocation Location;
        public int Version;
        public bool IsAlive;
    }
}
