using System.Collections.Generic;

namespace YokiFrame.ECS
{
    /// <summary>
    /// ECS工具类，提供便捷的访问接口
    /// </summary>
    public static class ECSKit
    {
        private static Dictionary<string, ECSWorld> _worlds = new Dictionary<string, ECSWorld>();
        private static ECSWorld _currentWorld;
        
        /// <summary>
        /// 获取当前World
        /// </summary>
        public static ECSWorld CurrentWorld => _currentWorld;
        
        /// <summary>
        /// 创建World
        /// </summary>
        public static ECSWorld CreateWorld(string name)
        {
            if (_worlds.TryGetValue(name, out var existing))
            {
                return existing;
            }
            
            var world = new ECSWorld(name);
            _worlds[name] = world;
            
            if (_currentWorld == null)
            {
                _currentWorld = world;
            }
            
            return world;
        }
        
        /// <summary>
        /// 获取World
        /// </summary>
        public static ECSWorld GetWorld(string name)
        {
            return _worlds.TryGetValue(name, out var world) ? world : null;
        }
        
        /// <summary>
        /// 设置当前World
        /// </summary>
        public static void SetCurrentWorld(string name)
        {
            if (_worlds.TryGetValue(name, out var world))
            {
                _currentWorld = world;
            }
        }
        
        /// <summary>
        /// 销毁World
        /// </summary>
        public static void DestroyWorld(string name)
        {
            if (_worlds.TryGetValue(name, out var world))
            {
                world.Dispose();
                _worlds.Remove(name);
                
                if (_currentWorld == world)
                {
                    _currentWorld = null;
                }
            }
        }
        
        /// <summary>
        /// 销毁所有World
        /// </summary>
        public static void DestroyAllWorlds()
        {
            foreach (var world in _worlds.Values)
            {
                world.Dispose();
            }
            _worlds.Clear();
            _currentWorld = null;
        }
        
        /// <summary>
        /// 在当前World创建实体
        /// </summary>
        public static Entity CreateEntity(params ComponentType[] componentTypes)
        {
            return _currentWorld?.CreateEntity(componentTypes) ?? Entity.Null;
        }
        
        /// <summary>
        /// 在当前World添加系统
        /// </summary>
        public static T AddSystem<T>() where T : IECSSystem, new()
        {
            return _currentWorld != null ? _currentWorld.AddSystem<T>() : default;
        }
        
        /// <summary>
        /// 更新当前World
        /// </summary>
        public static void Update()
        {
            _currentWorld?.Update();
        }
    }
}
