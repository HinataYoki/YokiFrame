#if GODOT

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace YokiFrame
{
    /// <summary>基于 Godot 4.7 ResourceLoader 与 FileAccess 的默认 ResKit Provider。</summary>
    public sealed class GodotResourceProvider :
        IResourceProvider,
        IRawResourceProvider,
        IResSceneProvider,
        IResourceProviderCapabilities
    {
        private const int THREAD_POLL_DELAY_MS = 10;
#if TOOLS
        private const string PROVIDER_NAME = "Godot.ResourceLoader";
#endif
        private readonly Dictionary<string, Node> mAdditiveScenes = new(StringComparer.Ordinal);
        private ResSceneHandle mActiveScene;

#if TOOLS
        /// <summary>获取稳定 Provider 名称。</summary>
        public string ProviderName => PROVIDER_NAME;
#endif

        /// <summary>获取当前 Provider 支持 raw bytes。</summary>
        public bool SupportsRawBytes => true;

        /// <summary>获取当前 Provider 支持 raw 文本。</summary>
        public bool SupportsRawText => true;

        /// <summary>获取 Godot SceneTree 场景后端名称。</summary>
        public string SceneBackendName => "Godot.SceneTree";

        /// <summary>获取 Godot 当前激活场景句柄。</summary>
        public ResSceneHandle ActiveScene => mActiveScene;

        /// <summary>通过 ResourceLoader 同步加载资源；不存在或类型不匹配时返回空。</summary>
        public T Load<T>(string path) where T : class
        {
            ValidatePath(path);
            Resource resource = ResourceLoader.Load(path);
            return resource as T;
        }

        /// <summary>通过 Godot threaded loader 异步加载，不在调用线程执行同步 ResourceLoader.Load。</summary>
        public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            ValidatePath(path);
            token.ThrowIfCancellationRequested();
            Godot.Error error = ResourceLoader.LoadThreadedRequest(path, string.Empty, true);
            if (error != Godot.Error.Ok)
            {
                throw new InvalidOperationException(
                    "Godot failed to start threaded resource load for '" + path + "': " + error + ".");
            }

            Resource resource = await WaitForThreadedResource(path, token);
            return resource as T;
        }

        /// <summary>Godot ResourceLoader 缓存使用引用计数管理，ResKit 释放时无需主动 Dispose 共享资源。</summary>
        public void Release(object asset)
        {
        }

        /// <summary>同步读取 res/user 路径的完整字节内容。</summary>
        public byte[] LoadRaw(string path)
        {
            ValidatePath(path);
            return FileAccess.FileExists(path) ? FileAccess.GetFileAsBytes(path) : null;
        }

        /// <summary>同步读取 res/user 路径的 UTF-8 文本内容。</summary>
        public string LoadRawText(string path)
        {
            ValidatePath(path);
            return FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : null;
        }

        /// <summary>在线程池执行 raw 文件读取，避免阻塞 Godot 主循环。</summary>
        public Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
        {
            ValidatePath(path);
            return Task.Run(() => LoadRaw(path), token);
        }

        /// <summary>在线程池执行 raw 文本读取，避免阻塞 Godot 主循环。</summary>
        public Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
        {
            ValidatePath(path);
            return Task.Run(() => LoadRawText(path), token);
        }

        /// <summary>通过 Godot ResourceLoader 与 SceneTree 加载场景。</summary>
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            var operation = new GodotSceneLoadOperation();
            onProgress?.Invoke(0f);
            if (request.BuildIndex >= 0 || string.IsNullOrWhiteSpace(request.SceneName))
            {
                onComplete?.Invoke(CreateInvalidSceneResult(request));
                return operation;
            }

            PackedScene packedScene = ResourceLoader.Load<PackedScene>(request.SceneName);
            SceneTree tree = Engine.GetMainLoop() as SceneTree;
            if (packedScene == null || tree == null || tree.Root == null)
            {
                onComplete?.Invoke(CreateInvalidSceneResult(request));
                return operation;
            }

            bool holdActivation = request.IsPreload || request.SuspendAtProgress < 1f;
            if (holdActivation)
            {
                operation.SetSuspended(
                    request.SuspendAtProgress,
                    () => CompleteSceneLoad(
                        request,
                        packedScene,
                        tree,
                        operation,
                        onComplete,
                        onProgress));
                onProgress?.Invoke(operation.Progress);
                onSuspended?.Invoke();
                return operation;
            }

            CompleteSceneLoad(request, packedScene, tree, operation, onComplete, onProgress);
            return operation;
        }

        /// <summary>在资源已解析后提交场景实例化或替换，并发布终态回调。</summary>
        private void CompleteSceneLoad(
            ResSceneLoadRequest request,
            PackedScene packedScene,
            SceneTree tree,
            GodotSceneLoadOperation operation,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress)
        {
            bool succeeded = false;
            try
            {
                succeeded = TryLoadScene(request, packedScene, tree);
            }
            catch (Exception exception)
            {
                LogKit.Error("Godot scene load failed for '" + request.SceneName + "': " + exception.Message);
            }
            finally
            {
                operation.SetCompleted();
            }

            ResSceneHandle handle = new(request.SceneName, request.BuildIndex, succeeded);
            if (succeeded && (request.Mode == ResSceneLoadMode.Single || !mActiveScene.IsValid))
            {
                mActiveScene = handle;
            }

            onProgress?.Invoke(1f);
            onComplete?.Invoke(new ResSceneLoadResult(handle));
        }

        /// <summary>使用已解析资源与 SceneTree 按请求模式提交同步 Godot 场景加载。</summary>
        /// <param name="request">跨宿主场景加载请求。</param>
        /// <param name="packedScene">已经解析的 PackedScene。</param>
        /// <param name="tree">当前主 SceneTree。</param>
        /// <returns>场景已经成功加入或替换 SceneTree 时返回 true。</returns>
        private bool TryLoadScene(ResSceneLoadRequest request, PackedScene packedScene, SceneTree tree)
        {
            return request.Mode == ResSceneLoadMode.Single
                ? TryLoadSingleScene(tree, packedScene)
                : TryLoadAdditiveScene(tree, packedScene, request.SceneName);
        }

        /// <summary>使用 ChangeScene 替换当前场景，并清理已经失效的 additive 节点记录。</summary>
        /// <param name="tree">当前主 SceneTree。</param>
        /// <param name="packedScene">已加载的 PackedScene。</param>
        /// <returns>Godot 接受场景切换时返回 true。</returns>
        private bool TryLoadSingleScene(SceneTree tree, PackedScene packedScene)
        {
            Godot.Error error = tree.ChangeSceneToPacked(packedScene);
            if (error != Godot.Error.Ok)
            {
                return false;
            }

            ReleaseAllAdditiveScenes();
            return true;
        }

        /// <summary>实例化 PackedScene 并把节点作为 additive 场景加入根节点。</summary>
        /// <param name="tree">当前主 SceneTree。</param>
        /// <param name="packedScene">已加载的 PackedScene。</param>
        /// <param name="sceneName">用于记录节点所有权的场景路径。</param>
        /// <returns>节点成功实例化并加入树时返回 true。</returns>
        private bool TryLoadAdditiveScene(SceneTree tree, PackedScene packedScene, string sceneName)
        {
            Node node = packedScene.Instantiate<Node>();
            if (node == null)
            {
                return false;
            }

            ReleaseAdditiveScene(sceneName);
            tree.Root.AddChild(node);
            mAdditiveScenes[sceneName] = node;
            return true;
        }

        /// <summary>卸载 Godot additive 场景节点；Single 场景由下一次 ChangeScene 替换。</summary>
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            ReleaseAdditiveScene(scene.SceneName);

            if (mActiveScene == scene)
            {
                mActiveScene = default;
            }

            onComplete?.Invoke();
        }

        /// <summary>从记录中移除一个 Additive 场景，并在节点仍有效时安排其在当前帧结束后释放。</summary>
        private void ReleaseAdditiveScene(string sceneName)
        {
            if (!mAdditiveScenes.TryGetValue(sceneName, out Node node))
            {
                return;
            }

            mAdditiveScenes.Remove(sceneName);
            if (GodotObject.IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }

        /// <summary>在 Single 场景切换后释放所有仍作为 Root 子节点存在的 Additive 场景。</summary>
        private void ReleaseAllAdditiveScenes()
        {
            List<string> sceneNames = new(mAdditiveScenes.Keys);
            for (int index = 0; index < sceneNames.Count; index++)
            {
                ReleaseAdditiveScene(sceneNames[index]);
            }
        }

        /// <summary>更新 SceneKit 记录的 Godot 激活场景。</summary>
        public void SetActiveScene(ResSceneHandle scene)
        {
            mActiveScene = scene;
        }

        /// <summary>获取 SceneKit 记录的 Godot 激活场景。</summary>
        public ResSceneHandle GetActiveScene()
        {
            return mActiveScene;
        }

        /// <summary>Godot 使用引用计数管理未使用资源，因此直接完成请求。</summary>
        public void UnloadUnusedAssets(Action onComplete)
        {
            onComplete?.Invoke();
        }

        /// <summary>创建当前请求对应的无效场景结果。</summary>
        private static ResSceneLoadResult CreateInvalidSceneResult(ResSceneLoadRequest request)
        {
            return new ResSceneLoadResult(new ResSceneHandle(
                request.SceneName, request.BuildIndex, false));
        }

        /// <summary>轮询 Godot threaded loader 状态，完成后才取回资源，避免阻塞调用线程。</summary>
        private static async Task<Resource> WaitForThreadedResource(string path, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    return ResourceLoader.LoadThreadedGet(path);
                }

                if (status == ResourceLoader.ThreadLoadStatus.Failed
                    || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
                {
                    throw new InvalidOperationException(
                        "Godot threaded resource load failed for '" + path + "' with status " + status + ".");
                }

                await Task.Delay(THREAD_POLL_DELAY_MS, token);
            }
        }

        /// <summary>校验资源路径，保证直接使用 Provider 时也遵守 ResKit 参数约束。</summary>
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Resource path cannot be empty.", nameof(path));
            }
        }

        /// <summary>表示 Godot 当前同步场景操作的可查询状态。</summary>
        private sealed class GodotSceneLoadOperation : IResSceneLoadOperation
        {
            private float mProgress;
            private bool mSuspended;
            private bool mCompleted;
            private bool mRecycled;
            private Action mResumeAction;

            /// <inheritdoc />
            public float Progress => mProgress;

            /// <inheritdoc />
            public bool IsSuspended => mSuspended;

            /// <summary>标记同步场景操作已经完成。</summary>
            internal void SetCompleted()
            {
                mProgress = 1f;
                mSuspended = false;
                mCompleted = true;
                mResumeAction = null;
            }

            /// <summary>保存已加载资源的激活回调，并把同步操作暴露为可恢复挂起状态。</summary>
            /// <param name="progress">资源准备完成时报告的挂起进度。</param>
            /// <param name="resumeAction">恢复时执行一次的场景激活操作。</param>
            internal void SetSuspended(float progress, Action resumeAction)
            {
                mProgress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
                mSuspended = true;
                mResumeAction = resumeAction;
            }

            /// <inheritdoc />
            public void SuspendLoad()
            {
                if (!mCompleted && !mRecycled)
                {
                    mSuspended = true;
                }
            }

            /// <inheritdoc />
            public void ResumeLoad()
            {
                if (!mSuspended || mCompleted || mRecycled)
                {
                    return;
                }

                mSuspended = false;
                Action resumeAction = mResumeAction;
                mResumeAction = null;
                resumeAction?.Invoke();
            }

            /// <inheritdoc />
            public void Recycle()
            {
                mProgress = 0f;
                mSuspended = false;
                mRecycled = true;
                mResumeAction = null;
            }
        }
    }
}

#endif
