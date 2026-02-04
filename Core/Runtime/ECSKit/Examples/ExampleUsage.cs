using UnityEngine;
using System.IO;
using System.Text;
using UCollider = UnityEngine.Collider;

namespace YokiFrame.ECS.Examples
{
    /// <summary>
    /// ECS 使用示例 - 简单射击游戏
    /// </summary>
    public class ExampleUsage : MonoBehaviour
    {
        private ECSWorld _world;
        private int _score = 0;
        
        // 生成计时器
        private float _enemySpawnTimer = 0f;
        private float _powerUpSpawnTimer = 0f;
        
        private const float EnemySpawnInterval = 2f;
        private const float PowerUpSpawnInterval = 5f;
        
        private int _enemiesPerSpawn = 1;
        private int _powerUpsPerSpawn = 1;
        
        private float _difficultyTimer = 0f;
        private const float DifficultyIncreaseInterval = 10f;
        
        // 性能统计
        private float _gameTime = 0f;
        private float _fpsUpdateTimer = 0f;
        private int _frameCount = 0;
        private float _currentFPS = 0f;
        private float _minFPS = float.MaxValue;
        private float _maxFPS = 0f;
        private float _avgFPS = 0f;
        private float _totalFPS = 0f;
        private int _fpsSamples = 0;
        
        private float _memoryUpdateTimer = 0f;
        private long _currentMemory = 0;
        private long _peakMemory = 0;
        
        private StringBuilder _performanceLog = new StringBuilder();
        private float _logTimer = 0f;
        private const float LogInterval = 5f;
        private int _maxEntitiesReached = 0;
        private bool _dataSaved = false;
        
        private AutoShootSystem _shootSystem;
        private CollisionSystem _collisionSystem;
        
        private void Start()
        {
            _world = new ECSWorld("GameWorld");
            
            // 注册销毁回调 - 框架自动清理 GameObject
            _world.OnEntityDestroyed = (entityId, instanceId) =>
            {
                var go = GameObjectManager.Get(instanceId);
                if (go != null) Object.Destroy(go);
                GameObjectManager.Unregister(entityId);
            };
            
            // 添加系统（按阶段自动分组）
            // Logic 阶段
            _world.AddSystem<PlayerInputSystem>();
            _world.AddSystem<PlayerMovementSystem>();
            _world.AddSystem<EnemyAISystem>();
            _world.AddSystem<PowerUpAISystem>();
            _world.AddSystem<MovementSystem>();
            
            _shootSystem = _world.AddSystem<AutoShootSystem>();
            _shootSystem.GameManager = this;
            
            _collisionSystem = _world.AddSystem<CollisionSystem>();
            _collisionSystem.GameManager = this;
            
            _world.AddSystem<LifetimeSystem>();
            
            // Sync 阶段
            _world.AddSystem<RenderSyncSystem>();
            
            CreatePlayer();
            
            for (int i = 0; i < 3; i++)
            {
                CreateEnemy();
            }
            
            CreatePowerUp();
            
            _performanceLog.AppendLine("=== ECSKit Performance Report ===");
            _performanceLog.AppendLine($"Start Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _performanceLog.AppendLine($"Version: Archetype + Chunk ECS");
            _performanceLog.AppendLine("");
            _performanceLog.AppendLine("Time(s), Entities, Enemies, PowerUps, Bullets, FPS, Memory(MB)");
            
            KitLogger.Log("Game Started! A/D to move, Auto-fire enabled!");
        }
        
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            
            _gameTime += deltaTime;
            
            _frameCount++;
            _fpsUpdateTimer += deltaTime;
            if (_fpsUpdateTimer >= 0.5f)
            {
                _currentFPS = _frameCount / _fpsUpdateTimer;
                _frameCount = 0;
                _fpsUpdateTimer = 0f;
                
                if (_currentFPS < _minFPS) _minFPS = _currentFPS;
                if (_currentFPS > _maxFPS) _maxFPS = _currentFPS;
                _totalFPS += _currentFPS;
                _fpsSamples++;
                _avgFPS = _totalFPS / _fpsSamples;
            }
            
            _memoryUpdateTimer += deltaTime;
            if (_memoryUpdateTimer >= 1f)
            {
                _memoryUpdateTimer = 0f;
                _currentMemory = System.GC.GetTotalMemory(false);
                if (_currentMemory > _peakMemory) _peakMemory = _currentMemory;
            }
            
            _difficultyTimer += deltaTime;
            if (_difficultyTimer >= DifficultyIncreaseInterval)
            {
                _difficultyTimer = 0f;
                _enemiesPerSpawn++;
                if (_enemiesPerSpawn % 2 == 0)
                {
                    _powerUpsPerSpawn++;
                }
            }
            
            _enemySpawnTimer += deltaTime;
            if (_enemySpawnTimer >= EnemySpawnInterval)
            {
                _enemySpawnTimer = 0f;
                for (int i = 0; i < _enemiesPerSpawn; i++)
                {
                    CreateEnemy();
                }
            }
            
            _powerUpSpawnTimer += deltaTime;
            if (_powerUpSpawnTimer >= PowerUpSpawnInterval)
            {
                _powerUpSpawnTimer = 0f;
                for (int i = 0; i < _powerUpsPerSpawn; i++)
                {
                    CreatePowerUp();
                }
            }
            
            _world.Update();
            
            _logTimer += deltaTime;
            if (_logTimer >= LogInterval)
            {
                _logTimer = 0f;
                LogPerformanceData();
            }
        }
        
        private void OnGUI()
        {
            GUI.Box(new Rect(5, 5, 220, 200), "Game Stats");
            GUI.Label(new Rect(15, 30, 200, 25), $"Score: {_score}");
            
            int totalEntities = _world.EntityCount;
            int enemyCount = 0, powerUpCount = 0, bulletCount = 0;
            
            foreach (var archetype in _world.GetAllArchetypes())
            {
                if (archetype.HasStaticComponent<EnemyTag>())
                    enemyCount += archetype.EntityCount;
                else if (archetype.HasStaticComponent<PowerUpTag>())
                    powerUpCount += archetype.EntityCount;
                else if (archetype.HasStaticComponent<BulletTag>())
                    bulletCount += archetype.EntityCount;
            }
            
            GUI.Label(new Rect(15, 55, 200, 25), $"Total Entities: {totalEntities}");
            GUI.Label(new Rect(15, 80, 200, 25), $"Enemies: {enemyCount}");
            GUI.Label(new Rect(15, 105, 200, 25), $"PowerUps: {powerUpCount}");
            GUI.Label(new Rect(15, 130, 200, 25), $"Bullets: {bulletCount}");
            GUI.Label(new Rect(15, 155, 200, 25), $"Archetypes: {_world.ArchetypeCount}");
            GUI.Label(new Rect(15, 180, 200, 25), $"Spawn: {_enemiesPerSpawn}e / {_powerUpsPerSpawn}p");
            
            GUI.Box(new Rect(Screen.width - 255, 5, 250, 200), "Performance Stats");
            
            int minutes = (int)(_gameTime / 60);
            int seconds = (int)(_gameTime % 60);
            GUI.Label(new Rect(Screen.width - 245, 30, 230, 25), $"Play Time: {minutes:00}:{seconds:00}");
            
            Color originalColor = GUI.color;
            if (_currentFPS < 30) GUI.color = Color.red;
            else if (_currentFPS < 60) GUI.color = Color.yellow;
            else GUI.color = Color.green;
            GUI.Label(new Rect(Screen.width - 245, 55, 230, 25), $"FPS: {_currentFPS:F1}");
            GUI.color = originalColor;
            
            GUI.Label(new Rect(Screen.width - 245, 80, 230, 25), $"Min FPS: {(_minFPS == float.MaxValue ? 0 : _minFPS):F1}");
            GUI.Label(new Rect(Screen.width - 245, 105, 230, 25), $"Max FPS: {_maxFPS:F1}");
            GUI.Label(new Rect(Screen.width - 245, 130, 230, 25), $"Avg FPS: {_avgFPS:F1}");
            
            float memoryMB = _currentMemory / (1024f * 1024f);
            float peakMemoryMB = _peakMemory / (1024f * 1024f);
            GUI.Label(new Rect(Screen.width - 245, 155, 230, 25), $"Memory: {memoryMB:F2} MB");
            GUI.Label(new Rect(Screen.width - 245, 180, 230, 25), $"Peak Memory: {peakMemoryMB:F2} MB");
            
            GUI.Box(new Rect(5, Screen.height - 35, 300, 30), "Controls: A/D Move | Auto-Fire");
            
            if (GUI.Button(new Rect(Screen.width - 150, Screen.height - 40, 140, 35), "Save Report"))
            {
                _dataSaved = false;
                SavePerformanceReport();
            }
        }
        
        #region Entity Creation
        
        private void CreatePlayer()
        {
            var entity = _world.CreateEntity<PlayerTag, Position, Velocity, InputData, Health, Shooter, RenderRef>();
            
            _world.SetComponent(entity, new Position(0, 0, 0));
            _world.SetComponent(entity, new Velocity(0, 0, 0));
            _world.SetComponent(entity, new InputData());
            _world.SetComponent(entity, new Health(100));
            _world.SetComponent(entity, new Shooter(0.5f, 1));
            
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = Vector3.zero;
            go.GetComponent<Renderer>().material.color = Color.green;
            Object.Destroy(go.GetComponent<UCollider>());
            
            _world.SetComponent(entity, new RenderRef(go.GetInstanceID()));
            GameObjectManager.Register(entity.Id, go);
        }
        
        public void CreateEnemy()
        {
            var entity = _world.CreateEntity<EnemyTag, Position, Velocity, Health, RenderRef>();
            
            var spawnPos = new Vector3(Random.Range(-8f, 8f), 0, 20f);
            
            _world.SetComponent(entity, new Position(spawnPos));
            _world.SetComponent(entity, new Velocity(0, 0, 0));
            _world.SetComponent(entity, new Health(20));
            
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Enemy_{entity.Id}";
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.8f;
            go.GetComponent<Renderer>().material.color = Color.red;
            Object.Destroy(go.GetComponent<UCollider>());
            
            _world.SetComponent(entity, new RenderRef(go.GetInstanceID()));
            GameObjectManager.Register(entity.Id, go);
        }
        
        public void CreatePowerUp()
        {
            var entity = _world.CreateEntity<PowerUpTag, Position, Velocity, Health, RenderRef>();
            
            var spawnPos = new Vector3(Random.Range(-8f, 8f), 0, 20f);
            
            _world.SetComponent(entity, new Position(spawnPos));
            _world.SetComponent(entity, new Velocity(0, 0, 0));
            _world.SetComponent(entity, new Health(30));
            
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"PowerUp_{entity.Id}";
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.6f;
            go.GetComponent<Renderer>().material.color = Color.cyan;
            Object.Destroy(go.GetComponent<UCollider>());
            
            _world.SetComponent(entity, new RenderRef(go.GetInstanceID()));
            GameObjectManager.Register(entity.Id, go);
        }
        
        public void CreateBullet(Vector3 position)
        {
            var entity = _world.CreateEntity<BulletTag, Position, Velocity, Lifetime, RenderRef>();
            
            var spawnPos = position + Vector3.forward * 0.5f;
            
            _world.SetComponent(entity, new Position(spawnPos));
            _world.SetComponent(entity, new Velocity(0, 0, 15f));
            _world.SetComponent(entity, new Lifetime(5f));
            
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Bullet_{entity.Id}";
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().material.color = Color.yellow;
            Object.Destroy(go.GetComponent<UCollider>());
            
            _world.SetComponent(entity, new RenderRef(go.GetInstanceID()));
            GameObjectManager.Register(entity.Id, go);
        }
        
        #endregion
        
        public void AddScore(int points)
        {
            _score += points;
        }
        
        #region Performance Logging
        
        private void LogPerformanceData()
        {
            int totalEntities = _world.EntityCount;
            int enemyCount = 0, powerUpCount = 0, bulletCount = 0;
            
            foreach (var archetype in _world.GetAllArchetypes())
            {
                if (archetype.HasStaticComponent<EnemyTag>())
                    enemyCount += archetype.EntityCount;
                else if (archetype.HasStaticComponent<PowerUpTag>())
                    powerUpCount += archetype.EntityCount;
                else if (archetype.HasStaticComponent<BulletTag>())
                    bulletCount += archetype.EntityCount;
            }
            
            if (totalEntities > _maxEntitiesReached)
                _maxEntitiesReached = totalEntities;
            
            float memoryMB = _currentMemory / (1024f * 1024f);
            _performanceLog.AppendLine($"{_gameTime:F1}, {totalEntities}, {enemyCount}, {powerUpCount}, {bulletCount}, {_currentFPS:F1}, {memoryMB:F2}");
        }
        
        private void SavePerformanceReport()
        {
            if (_dataSaved) return;
            _dataSaved = true;
            
            _performanceLog.AppendLine("");
            _performanceLog.AppendLine("=== Summary ===");
            int minutes = (int)(_gameTime / 60);
            int seconds = (int)(_gameTime % 60);
            _performanceLog.AppendLine($"Total Play Time: {minutes:00}:{seconds:00}");
            _performanceLog.AppendLine($"Max Entities: {_maxEntitiesReached}");
            _performanceLog.AppendLine($"Min FPS: {(_minFPS == float.MaxValue ? 0 : _minFPS):F1}");
            _performanceLog.AppendLine($"Max FPS: {_maxFPS:F1}");
            _performanceLog.AppendLine($"Avg FPS: {_avgFPS:F1}");
            _performanceLog.AppendLine($"Peak Memory: {_peakMemory / (1024f * 1024f):F2} MB");
            _performanceLog.AppendLine($"End Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            string fileName = $"ECSKit_Performance_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "ECSKit", "Examples", fileName);
            
            try
            {
                File.WriteAllText(filePath, _performanceLog.ToString());
                Debug.Log($"Performance report saved to: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save: {e.Message}");
            }
        }
        
        private void OnApplicationQuit()
        {
            SavePerformanceReport();
        }
        
        private void OnDestroy()
        {
            if (!_dataSaved) SavePerformanceReport();
            GameObjectManager.Clear();
            _world?.Dispose();
        }
        
        #endregion
    }
}
