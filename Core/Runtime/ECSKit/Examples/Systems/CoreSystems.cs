using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 射击系统
    /// 处理所有带Shooter和Position组件的实体，自动发射子弹
    /// </summary>
    public class ShooterSystem : SystemBase
    {
        private EntityQuery _query;
        private List<BulletSpawnData> _bulletsToSpawn = new List<BulletSpawnData>();
        
        private struct BulletSpawnData
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Lifetime;
        }
        
        public override SystemPhase Phase => SystemPhase.Logic;
        
        public override void OnCreate()
        {
            _query = World.Query().With<Shooter>().With<Position>();
        }
        
        public override unsafe void OnUpdate()
        {
            float dt = Time.deltaTime;
            _bulletsToSpawn.Clear();
            
            var archetypes = _query.GetMatchingArchetypes();
            if (archetypes.Count == 0) return;
            
            foreach (var archetype in archetypes)
            {
                int shooterIndex = archetype.GetStaticComponentIndex<Shooter>();
                int posIndex = archetype.GetStaticComponentIndex<Position>();
                if (shooterIndex < 0 || posIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var shooters = chunk.GetStaticComponentArray<Shooter>(shooterIndex);
                    var positions = chunk.GetStaticComponentArray<Position>(posIndex);
                    
                    for (int i = 0; i < count; i++)
                    {
                        shooters[i].TimeSinceLastShot += dt;
                        
                        // 使用默认值防止配置为0时无法射击
                        float fireRate = shooters[i].FireRate > 0 ? shooters[i].FireRate : 0.5f;
                        int bulletsPerShot = shooters[i].BulletsPerShot > 0 ? shooters[i].BulletsPerShot : 1;
                        
                        if (shooters[i].TimeSinceLastShot >= fireRate)
                        {
                            shooters[i].TimeSinceLastShot = 0;
                            
                            var pos = positions[i].ToVector3();
                            var speed = shooters[i].BulletSpeed > 0 ? shooters[i].BulletSpeed : 15f;
                            var lifetime = shooters[i].BulletLifetime > 0 ? shooters[i].BulletLifetime : 3f;
                            
                            // 根据配置的方向计算子弹速度
                            Vector3 velocity;
                            switch (shooters[i].ShootDirection)
                            {
                                case 1: velocity = Vector3.up * speed; break;
                                case 2: velocity = Vector3.right * speed; break;
                                default: velocity = Vector3.forward * speed; break;
                            }
                            
                            for (int b = 0; b < bulletsPerShot; b++)
                            {
                                _bulletsToSpawn.Add(new BulletSpawnData
                                {
                                    Position = pos + velocity.normalized * 0.5f,
                                    Velocity = velocity,
                                    Lifetime = lifetime
                                });
                            }
                        }
                    }
                }
            }
            
            // 遍历结束后统一创建子弹，避免遍历时修改集合
            foreach (var data in _bulletsToSpawn)
            {
                CreateBullet(data.Position, data.Velocity, data.Lifetime);
            }
        }
        
        private void CreateBullet(Vector3 position, Vector3 velocity, float lifetime)
        {
            var entity = World.CreateEntity<BulletTag, Position, Velocity, Lifetime, RenderRef>();
            
            World.SetComponent(entity, new Position(position));
            World.SetComponent(entity, new Velocity(velocity));
            World.SetComponent(entity, new Lifetime(lifetime));
            
            // 创建子弹的可视化表示
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Bullet_{entity.Id}";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().material.color = Color.yellow;
            
            // 移除碰撞体，由ECS处理碰撞
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            
            World.SetComponent(entity, new RenderRef(go.GetInstanceID()));
            ECSGameObjectManager.Register(entity.Id, go);
        }
    }
    
    /// <summary>
    /// 移动系统
    /// 根据Velocity组件更新Position组件
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
            var archetypes = _query.GetMatchingArchetypes();
            
            foreach (var archetype in archetypes)
            {
                int posIndex = archetype.GetStaticComponentIndex<Position>();
                int velIndex = archetype.GetStaticComponentIndex<Velocity>();
                if (posIndex < 0 || velIndex < 0) continue;
                
                int chunkCount = archetype.GetChunkCount();
                for (int c = 0; c < chunkCount; c++)
                {
                    ref var chunk = ref archetype.GetChunk(c);
                    int count = chunk.EntityCount;
                    var positions = chunk.GetStaticComponentArray<Position>(posIndex);
                    var velocities = chunk.GetStaticComponentArray<Velocity>(velIndex);
                    
                    // 批量更新位置
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
    
    /// <summary>
    /// 生命周期系统
    /// 处理带Lifetime组件的实体，超时后自动销毁
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
            var archetypes = _query.GetMatchingArchetypes();
            
            foreach (var archetype in archetypes)
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
                        
                        // 超时则标记销毁
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
    
    /// <summary>
    /// 渲染同步系统
    /// 将ECS中的Position同步到关联的GameObject Transform
    /// </summary>
    public class RenderSyncSystem : SystemBase
    {
        public override SystemPhase Phase => SystemPhase.Sync;
        
        public override unsafe void OnUpdate()
        {
            var allArchetypes = World.GetAllArchetypes();
            
            foreach (var archetype in allArchetypes)
            {
                // 只处理同时有Position和RenderRef的实体
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
                        var go = ECSGameObjectManager.Get(renderRefs[i].GameObjectInstanceId);
                        if (go != null)
                        {
                            go.transform.position = positions[i].ToVector3();
                        }
                    }
                }
            }
        }
    }
}
