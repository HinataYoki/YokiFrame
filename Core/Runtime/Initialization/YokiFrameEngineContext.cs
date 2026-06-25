using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 一次 YokiFrame 初始化过程中传递给各 Kit installer 的宿主上下文。
    /// </summary>
    public sealed class YokiFrameEngineContext
    {
        private readonly Dictionary<Type, object> mServices = new Dictionary<Type, object>();

        public YokiFrameEngineContext(YokiFrameEngine engine)
            : this(engine, GetDefaultEngineId(engine))
        {
        }

        public YokiFrameEngineContext(YokiFrameEngine engine, string engineId)
        {
            Engine = engine;
            EngineId = string.IsNullOrEmpty(engineId) ? GetDefaultEngineId(engine) : engineId;
        }

        public YokiFrameEngine Engine { get; private set; }

        public string EngineId { get; private set; }

        public void SetService<T>(T service) where T : class
        {
            var type = typeof(T);
            if (service == null)
            {
                mServices.Remove(type);
                return;
            }

            mServices[type] = service;
        }

        public T GetService<T>() where T : class
        {
            object service;
            if (mServices.TryGetValue(typeof(T), out service))
                return service as T;

            return null;
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            service = GetService<T>();
            return service != null;
        }

        private static string GetDefaultEngineId(YokiFrameEngine engine)
        {
            switch (engine)
            {
                case YokiFrameEngine.Unity:
                    return "unity";
                case YokiFrameEngine.Godot:
                    return "godot";
                case YokiFrameEngine.Custom:
                    return "custom";
                default:
                    return "unknown";
            }
        }
    }
}
