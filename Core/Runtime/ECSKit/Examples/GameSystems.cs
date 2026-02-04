using UnityEngine;
using System.Collections.Generic;
using YokiFrame.ECS;

namespace YokiFrame.ECS.Examples
{
    /// <summary>
    /// GameObject 管理器
    /// </summary>
    public static class GameObjectManager
    {
        private static Dictionary<int, GameObject> _instanceIdToGO = new Dictionary<int, GameObject>(256);
        private static Dictionary<long, int> _entityIdToInstanceId = new Dictionary<long, int>(256);
        
        public static void Register(long entityId, GameObject go)
        {
            if (go == null) return;
            int instanceId = go.GetInstanceID();
            _instanceIdToGO[instanceId] = go;
            _entityIdToInstanceId[entityId] = instanceId;
        }
        
        public static void Unregister(long entityId)
        {
            if (_entityIdToInstanceId.TryGetValue(entityId, out int instanceId))
            {
                _instanceIdToGO.Remove(instanceId);
                _entityIdToInstanceId.Remove(entityId);
            }
        }
        
        public static GameObject Get(int instanceId)
        {
            return _instanceIdToGO.TryGetValue(instanceId, out var go) ? go : null;
        }
        
        public static void Clear()
        {
            foreach (var go in _instanceIdToGO.Values)
            {
                if (go != null) Object.Destroy(go);
            }
            _instanceIdToGO.Clear();
            _entityIdToInstanceId.Clear();
        }
    }
    
    #region Logic Phase Systems
    
    /// <summary>
    /// 玩家输入系统 - 逻辑阶段
    /// </summary>
    public class PlayerInputSystem : SystemBase
    {
        private EntityQuery _query;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<PlayerTag>().With<InputData>();
        }
        
        public override void OnUpdate()
        {
            float horizontal = Input.GetAxis("Horizontal");
            
            _query.ForEach<PlayerTag, InputData>((ref PlayerTag _, ref InputData input) =>
            {
                input.Horizontal = horizontal;
                input.Vertical = 0;
            });
        }
    }
    
    /// <summary>
    /// 玩家移动计算系统 - 逻辑阶段
    /// </summary>
    public class PlayerMovementSystem : SystemBase
    {
        private EntityQuery _query;
        private const float MoveSpeed = 8f;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<PlayerTag>().With<InputData>().With<Velocity>();
        }
        
        public override void OnUpdate()
        {
            _query.ForEach<InputData, Velocity>((ref InputData input, ref Velocity vel) =>
            {
                vel.X = input.Horizontal * MoveSpeed;
                vel.Y = 0;
                vel.Z = 0;
            });
        }
    }
    
    /// <summary>
    /// 敌人AI系统 - 逻辑阶段
    /// </summary>
    public class EnemyAISystem : SystemBase
    {
        private EntityQuery _enemyQuery;
        private EntityQuery _playerQuery;
        private const float MoveSpeed = 3f;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _enemyQuery = World.Query().With<EnemyTag>().With<Position>().With<Velocity>();
            _playerQuery = World.Query().With<PlayerTag>().With<Position>();
        }
        
        public override unsafe void OnUpdate()
        {
            Position playerPos = default;
            bool hasPlayer = false;
            
            foreach (var archetype in _playerQuery.GetMatchingArchetypes())
            {
                int chunkCount = archetype.GetChunkCount();
                if (chunkCount > 0 && archetype.GetChunk(0).EntityCount > 0)
                {
                    int posIndex = archetype.GetStaticComponentIndex<Position>();
                    playerPos = archetype.GetChunk(0).GetStaticComponent<Position>(posIndex, 0);
                    hasPlayer = true;
                    break;
                }
            }
            
            if (!hasPlayer) return;
            
            _enemyQuery.ForEach<Position, Velocity>((ref Position pos, ref Velocity vel) =>
            {
                float dx = playerPos.X - pos.X;
                float dy = playerPos.Y - pos.Y;
                float dz = playerPos.Z - pos.Z;
                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                
                if (dist > 0.01f)
                {
                    float invDist = 1f / dist;
                    vel.X = dx * invDist * MoveSpeed;
                    vel.Y = dy * invDist * MoveSpeed;
                    vel.Z = dz * invDist * MoveSpeed;
                }
            });
        }
    }
    
    /// <summary>
    /// 道具AI系统 - 逻辑阶段
    /// </summary>
    public class PowerUpAISystem : SystemBase
    {
        private EntityQuery _powerUpQuery;
        private EntityQuery _playerQuery;
        private const float MoveSpeed = 2.5f;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _powerUpQuery = World.Query().With<PowerUpTag>().With<Position>().With<Velocity>();
            _playerQuery = World.Query().With<PlayerTag>().With<Position>();
        }
        
        public override unsafe void OnUpdate()
        {
            Position playerPos = default;
            bool hasPlayer = false;
            
            foreach (var archetype in _playerQuery.GetMatchingArchetypes())
            {
                int chunkCount = archetype.GetChunkCount();
                if (chunkCount > 0 && archetype.GetChunk(0).EntityCount > 0)
                {
                    int posIndex = archetype.GetStaticComponentIndex<Position>();
                    playerPos = archetype.GetChunk(0).GetStaticComponent<Position>(posIndex, 0);
                    hasPlayer = true;
                    break;
                }
            }
            
            if (!hasPlayer) return;
            
            _powerUpQuery.ForEach<Position, Velocity>((ref Position pos, ref Velocity vel) =>
            {
                float dx = playerPos.X - pos.X;
                float dy = playerPos.Y - pos.Y;
                float dz = playerPos.Z - pos.Z;
                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                
                if (dist > 0.01f)
                {
                    float invDist = 1f / dist;
                    vel.X = dx * invDist * MoveSpeed;
                    vel.Y = dy * invDist * MoveSpeed;
                    vel.Z = dz * invDist * MoveSpeed;
                }
            });
        }
    }
    
    /// <summary>
    /// 自动射击系统 - 逻辑阶段
    /// 使用 EntityCommandBuffer 延迟创建子弹，避免遍历过程中修改集合
    /// </summary>
    public class AutoShootSystem : SystemBase
    {
        private EntityQuery _query;
        public ExampleUsage GameManager;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<PlayerTag>().With<Position>().With<Shooter>();
        }
        
        public override unsafe void OnUpdate()
        {
            if (GameManager == null) return;
            
            float dt = Time.deltaTime;
            
            // 收集需要创建的子弹信息
            var bulletsToCreate = new List<Vector3>();
            
            foreach (var archetype in _query.GetMatchingArchetypes())
            {
                int posIndex = archetype.GetStaticComponentIndex<Position>();
                int shooterIndex = archetype.GetStaticComponentIndex<Shooter>();
                if (posIndex < 0 || shooterIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var positions = chunk.GetStaticComponentArray<Position>(posIndex);
                    var shooters = chunk.GetStaticComponentArray<Shooter>(shooterIndex);
                    
                    for (int i = 0; i < count; i++)
                    {
                        shooters[i].TimeSinceLastShot += dt;
                        
                        if (shooters[i].CanFire)
                        {
                            shooters[i].TimeSinceLastShot = 0;
                            
                            for (int b = 0; b < shooters[i].BulletsPerShot; b++)
                            {
                                bulletsToCreate.Add(positions[i].ToVector3());
                            }
                        }
                    }
                }
            }
            
            // 遍历结束后再创建子弹
            foreach (var pos in bulletsToCreate)
            {
                GameManager.CreateBullet(pos);
            }
        }
    }
    
    /// <summary>
    /// 碰撞检测系统 - 逻辑阶段
    /// 使用 DestroyTag 标记销毁，由框架统一处理
    /// </summary>
    public class CollisionSystem : SystemBase
    {
        private EntityQuery _bulletQuery;
        private EntityQuery _enemyQuery;
        private EntityQuery _powerUpQuery;
        private EntityQuery _playerQuery;
        
        private const float CollisionDistSqr = 0.25f;
        
        private HashSet<long> _bulletsToDestroy = new HashSet<long>();
        private HashSet<long> _enemiesToDestroy = new HashSet<long>();
        private HashSet<long> _powerUpsToDestroy = new HashSet<long>();
        
        public ExampleUsage GameManager;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _bulletQuery = World.Query().With<BulletTag>().With<Position>();
            _enemyQuery = World.Query().With<EnemyTag>().With<Position>().With<Health>();
            _powerUpQuery = World.Query().With<PowerUpTag>().With<Position>().With<Health>();
            _playerQuery = World.Query().With<PlayerTag>().With<Shooter>();
        }
        
        public override unsafe void OnUpdate()
        {
            _bulletsToDestroy.Clear();
            _enemiesToDestroy.Clear();
            _powerUpsToDestroy.Clear();
            
            var bulletArchetypes = _bulletQuery.GetMatchingArchetypes();
            var enemyArchetypes = _enemyQuery.GetMatchingArchetypes();
            var powerUpArchetypes = _powerUpQuery.GetMatchingArchetypes();
            
            foreach (var bulletArchetype in bulletArchetypes)
            {
                int bulletPosIndex = bulletArchetype.GetStaticComponentIndex<Position>();
                if (bulletPosIndex < 0) continue;
                
                int bulletChunkCount = bulletArchetype.GetChunkCount();
                for (int bc = 0; bc < bulletChunkCount; bc++)
                {
                    ref var bulletChunk = ref bulletArchetype.GetChunk(bc);
                    var bulletPositions = bulletChunk.GetStaticComponentArray<Position>(bulletPosIndex);
                    
                    for (int bi = 0; bi < bulletChunk.EntityCount; bi++)
                    {
                        var bulletPos = bulletPositions[bi];
                        long bulletId = bulletChunk.GetEntityId(bi);
                        bool bulletHit = false;
                        
                        // vs 敌人
                        foreach (var enemyArchetype in enemyArchetypes)
                        {
                            if (bulletHit) break;
                            
                            int enemyPosIndex = enemyArchetype.GetStaticComponentIndex<Position>();
                            int enemyHealthIndex = enemyArchetype.GetStaticComponentIndex<Health>();
                            if (enemyPosIndex < 0 || enemyHealthIndex < 0) continue;
                            
                            int enemyChunkCount = enemyArchetype.GetChunkCount();
                            for (int ec = 0; ec < enemyChunkCount; ec++)
                            {
                                if (bulletHit) break;
                                
                                ref var enemyChunk = ref enemyArchetype.GetChunk(ec);
                                var enemyPositions = enemyChunk.GetStaticComponentArray<Position>(enemyPosIndex);
                                var enemyHealths = enemyChunk.GetStaticComponentArray<Health>(enemyHealthIndex);
                                
                                for (int ei = 0; ei < enemyChunk.EntityCount; ei++)
                                {
                                    long enemyId = enemyChunk.GetEntityId(ei);
                                    if (_enemiesToDestroy.Contains(enemyId)) continue;
                                    
                                    float dx = bulletPos.X - enemyPositions[ei].X;
                                    float dy = bulletPos.Y - enemyPositions[ei].Y;
                                    float dz = bulletPos.Z - enemyPositions[ei].Z;
                                    
                                    if (dx * dx + dy * dy + dz * dz < CollisionDistSqr)
                                    {
                                        enemyHealths[ei].TakeDamage(10);
                                        
                                        if (enemyHealths[ei].IsDead && _enemiesToDestroy.Add(enemyId))
                                        {
                                            if (GameManager != null) GameManager.AddScore(10);
                                        }
                                        
                                        _bulletsToDestroy.Add(bulletId);
                                        bulletHit = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // vs 道具
                        if (!bulletHit)
                        {
                            foreach (var powerUpArchetype in powerUpArchetypes)
                            {
                                if (bulletHit) break;
                                
                                int puPosIndex = powerUpArchetype.GetStaticComponentIndex<Position>();
                                int puHealthIndex = powerUpArchetype.GetStaticComponentIndex<Health>();
                                if (puPosIndex < 0 || puHealthIndex < 0) continue;
                                
                                int puChunkCount = powerUpArchetype.GetChunkCount();
                                for (int pc = 0; pc < puChunkCount; pc++)
                                {
                                    if (bulletHit) break;
                                    
                                    ref var puChunk = ref powerUpArchetype.GetChunk(pc);
                                    var puPositions = puChunk.GetStaticComponentArray<Position>(puPosIndex);
                                    var puHealths = puChunk.GetStaticComponentArray<Health>(puHealthIndex);
                                    
                                    for (int pi = 0; pi < puChunk.EntityCount; pi++)
                                    {
                                        long puId = puChunk.GetEntityId(pi);
                                        if (_powerUpsToDestroy.Contains(puId)) continue;
                                        
                                        float dx = bulletPos.X - puPositions[pi].X;
                                        float dy = bulletPos.Y - puPositions[pi].Y;
                                        float dz = bulletPos.Z - puPositions[pi].Z;
                                        
                                        if (dx * dx + dy * dy + dz * dz < CollisionDistSqr)
                                        {
                                            puHealths[pi].TakeDamage(10);
                                            
                                            if (puHealths[pi].IsDead && _powerUpsToDestroy.Add(puId))
                                            {
                                                ApplyPowerUp();
                                            }
                                            
                                            _bulletsToDestroy.Add(bulletId);
                                            bulletHit = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 标记销毁 - 使用 DestroyEntityDeferred，框架会自动清理 GameObject
            foreach (var id in _bulletsToDestroy)
            {
                World.DestroyEntityDeferred(id);
            }
            foreach (var id in _enemiesToDestroy)
            {
                World.DestroyEntityDeferred(id);
            }
            foreach (var id in _powerUpsToDestroy)
            {
                World.DestroyEntityDeferred(id);
            }
        }
        
        private unsafe void ApplyPowerUp()
        {
            foreach (var archetype in _playerQuery.GetMatchingArchetypes())
            {
                int shooterIndex = archetype.GetStaticComponentIndex<Shooter>();
                if (shooterIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    var shooters = chunk.GetStaticComponentArray<Shooter>(shooterIndex);
                    
                    for (int i = 0; i < chunk.EntityCount; i++)
                    {
                        shooters[i].BulletsPerShot *= 2;
                        KitLogger.Log($"Power Up! Bullets: {shooters[i].BulletsPerShot}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 生命周期系统 - 逻辑阶段
    /// </summary>
    public class LifetimeSystem : SystemBase
    {
        private EntityQuery _query;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<Lifetime>();
        }
        
        public override unsafe void OnUpdate()
        {
            float dt = Time.deltaTime;
            
            foreach (var archetype in _query.GetMatchingArchetypes())
            {
                int lifetimeIndex = archetype.GetStaticComponentIndex<Lifetime>();
                if (lifetimeIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var lifetimes = chunk.GetStaticComponentArray<Lifetime>(lifetimeIndex);
                    
                    for (int i = 0; i < count; i++)
                    {
                        lifetimes[i].Elapsed += dt;
                        
                        if (lifetimes[i].IsExpired)
                        {
                            long entityId = chunk.GetEntityId(i);
                            World.DestroyEntityDeferred(entityId);
                        }
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Logic Phase Systems (Movement)
    
    /// <summary>
    /// 移动系统 - 逻辑阶段
    /// </summary>
    public class MovementSystem : SystemBase
    {
        private EntityQuery _query;
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<Position>().With<Velocity>();
        }
        
        public override unsafe void OnUpdate()
        {
            float dt = Time.deltaTime;
            
            var allArchetypes = World.GetAllArchetypes();
            
            for (int a = 0; a < allArchetypes.Count; a++)
            {
                var archetype = allArchetypes[a];
                if (!archetype.HasStaticComponent<Position>()) continue;
                if (!archetype.HasStaticComponent<Velocity>()) continue;
                
                int posIndex = archetype.GetStaticComponentIndex<Position>();
                int velIndex = archetype.GetStaticComponentIndex<Velocity>();
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var positions = chunk.GetStaticComponentArray<Position>(posIndex);
                    var velocities = chunk.GetStaticComponentArray<Velocity>(velIndex);
                    
                    for (int i = 0; i < count; i++)
                    {
                        positions[i].X += velocities[i].X * dt;
                        positions[i].Y += velocities[i].Y * dt;
                        positions[i].Z += velocities[i].Z * dt;
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Sync Phase Systems
    
    /// <summary>
    /// 渲染同步系统 - 同步阶段
    /// </summary>
    public class RenderSyncSystem : SystemBase
    {
        public override SystemPhase Phase => SystemPhase.Sync;
        
        public override void OnCreate() { }
        
        public override unsafe void OnUpdate()
        {
            var allArchetypes = World.GetAllArchetypes();
            
            for (int a = 0; a < allArchetypes.Count; a++)
            {
                var archetype = allArchetypes[a];
                if (!archetype.HasStaticComponent<Position>()) continue;
                if (!archetype.HasStaticComponent<RenderRef>()) continue;
                
                int posIndex = archetype.GetStaticComponentIndex<Position>();
                int refIndex = archetype.GetStaticComponentIndex<RenderRef>();
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var positions = chunk.GetStaticComponentArray<Position>(posIndex);
                    var renderRefs = chunk.GetStaticComponentArray<RenderRef>(refIndex);
                    
                    for (int i = 0; i < count; i++)
                    {
                        var go = GameObjectManager.Get(renderRefs[i].GameObjectInstanceId);
                        if (go != null)
                        {
                            go.transform.position = positions[i].ToVector3();
                        }
                    }
                }
            }
        }
    }
    
    #endregion
}
