#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// 将 Godot SceneTree 后端注入到 SceneKit，保持 Unity/Godot 共用静态入口。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Godot)]
    public sealed class GodotSceneKitInstaller : IYokiFrameKitInstaller
    {
        public string KitName
        {
            get { return "Godot.SceneKit"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Godot)
                return;

            ResKit.SetSceneBackend(new GodotSceneBackend());
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            ResKit.ClearSceneBackend();
        }
    }

    public sealed class GodotSceneBackend : IResSceneBackend
    {
        private readonly Dictionary<string, Node> mLoadedNodes = new Dictionary<string, Node>();
        private ResSceneHandle mActiveScene;

        public string BackendName
        {
            get { return "Godot.SceneTree"; }
        }

        public ResSceneHandle ActiveScene
        {
            get { return mActiveScene; }
        }

        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            var operation = new GodotSceneLoadOperation();
            if (onProgress != null)
                onProgress(0f);

            var scenePath = request.SceneName;
            var packedScene = ResourceLoader.Load<PackedScene>(scenePath);
            if (packedScene == null)
            {
                if (onComplete != null)
                    onComplete(new ResSceneLoadResult(new ResSceneHandle(scenePath, request.BuildIndex, false)));
                return operation;
            }

            if (request.Mode == ResSceneLoadMode.Single)
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null)
                    tree.ChangeSceneToPacked(packedScene);
            }
            else
            {
                var node = packedScene.Instantiate<Node>();
                var tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null && tree.Root != null)
                    tree.Root.AddChild(node);
                mLoadedNodes[scenePath] = node;
            }

            operation.SetProgress(1f);
            var handle = new ResSceneHandle(scenePath, request.BuildIndex, true);
            if (request.Mode == ResSceneLoadMode.Single || !mActiveScene.IsValid)
                mActiveScene = handle;
            if (onProgress != null)
                onProgress(1f);
            if (onComplete != null)
                onComplete(new ResSceneLoadResult(handle));
            return operation;
        }

        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            Node node;
            if (!string.IsNullOrEmpty(scene.SceneName) && mLoadedNodes.TryGetValue(scene.SceneName, out node))
            {
                mLoadedNodes.Remove(scene.SceneName);
                if (GodotObject.IsInstanceValid(node))
                    node.QueueFree();
            }

            if (onComplete != null)
                onComplete();
        }

        public void SetActiveScene(ResSceneHandle scene)
        {
            mActiveScene = scene;
        }

        public ResSceneHandle GetActiveScene()
        {
            return mActiveScene;
        }

        public void UnloadUnusedAssets(Action onComplete)
        {
            if (onComplete != null)
                onComplete();
        }
    }

    public sealed class GodotSceneLoadOperation : IResSceneLoadOperation
    {
        private float mProgress;

        public bool IsSuspended { get; private set; }

        public float Progress
        {
            get { return mProgress; }
        }

        public void SetProgress(float progress)
        {
            mProgress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        public void SuspendLoad()
        {
            IsSuspended = true;
        }

        public void ResumeLoad()
        {
            IsSuspended = false;
        }

        public void Recycle()
        {
            mProgress = 0f;
            IsSuspended = false;
        }
    }
}
#endif
