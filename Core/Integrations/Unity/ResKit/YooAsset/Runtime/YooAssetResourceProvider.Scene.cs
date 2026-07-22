#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetResourceProvider
    {
        /// <summary>通过当前 YooAsset ResourcePackage 异步加载场景 location。</summary>
        public IResSceneLoadOperation LoadSceneAsync(
            ResSceneLoadRequest request,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress,
            Action onSuspended)
        {
            if (request.BuildIndex >= 0)
            {
                onComplete?.Invoke(CreateInvalidSceneResult(request));
                return new YooAssetSceneLoadOperation(null, request.SuspendAtProgress, onProgress, onSuspended);
            }

            EnsureRequestPath(request.SceneName);
            LoadSceneMode mode = request.Mode == ResSceneLoadMode.Single
                ? LoadSceneMode.Single
                : LoadSceneMode.Additive;
            bool shouldSuspend = request.SuspendAtProgress < 1f;
#if YOKIFRAME_YOOASSET_3
            // V3 的第四个参数改为 allowSceneActivation，与 V2 的 suspendLoad 含义相反。
            bool allowSceneActivation = !shouldSuspend;
            YooSceneHandle handle = mPackage.LoadSceneAsync(
                request.SceneName, mode, LocalPhysicsMode.None, allowSceneActivation);
#else
            bool suspendLoad = shouldSuspend;
            YooSceneHandle handle = mPackage.LoadSceneAsync(
                request.SceneName, mode, LocalPhysicsMode.None, suspendLoad);
#endif
            var operation = new YooAssetSceneLoadOperation(
                handle, request.SuspendAtProgress, onProgress, onSuspended);
            handle.Completed += completedHandle => CompleteSceneLoad(
                request, completedHandle, operation, onComplete, onProgress);
            operation.ReportInitialProgress();
            return operation;
        }

        /// <summary>提交 YooAsset 场景完成结果，并同步 Provider 的句柄所有权与激活状态。</summary>
        private void CompleteSceneLoad(
            ResSceneLoadRequest request,
            YooSceneHandle completedHandle,
            YooAssetSceneLoadOperation operation,
            Action<ResSceneLoadResult> onComplete,
            Action<float> onProgress)
        {
            bool succeeded = IsSceneHandleSucceeded(completedHandle);
            ResSceneHandle scene = succeeded
                ? new ResSceneHandle(
                    completedHandle.SceneName,
                    completedHandle.SceneObject.buildIndex,
                    completedHandle.SceneObject.IsValid())
                : new ResSceneHandle(request.SceneName, request.BuildIndex, false);
            if (scene.IsValid)
            {
                RegisterLoadedScene(request.Mode, scene, completedHandle);
            }

            operation.MarkCompleted();
            onProgress?.Invoke(completedHandle.Progress);
            onComplete?.Invoke(new ResSceneLoadResult(scene));
        }

        /// <summary>登记有效 YooAsset 场景，并按 Single 模式移除已由引擎替换的旧句柄。</summary>
        private void RegisterLoadedScene(
            ResSceneLoadMode mode,
            ResSceneHandle scene,
            YooSceneHandle completedHandle)
        {
            mSceneHandles[scene.SceneName] = completedHandle;
            if (mode == ResSceneLoadMode.Single)
            {
                RemoveReplacedSceneHandles(scene.SceneName);
            }

            if (mode == ResSceneLoadMode.Single || !mActiveScene.IsValid)
            {
                mActiveScene = scene;
            }
        }

        /// <summary>使用 YooAsset 场景句柄卸载指定场景。</summary>
        public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
        {
            if (!mSceneHandles.TryGetValue(scene.SceneName, out YooSceneHandle handle) || handle == null)
            {
                onComplete?.Invoke();
                return;
            }

            mSceneHandles.Remove(scene.SceneName);
            if (mActiveScene == scene)
            {
                mActiveScene = default;
            }

#if YOKIFRAME_YOOASSET_3
            UnloadSceneOperation operation = handle.UnloadSceneAsync();
#else
            UnloadSceneOperation operation = handle.UnloadAsync();
#endif
            operation.Completed += _ => onComplete?.Invoke();
        }

        /// <summary>激活已经加载或挂起的 YooAsset 场景。</summary>
        public void SetActiveScene(ResSceneHandle scene)
        {
            if (mSceneHandles.TryGetValue(scene.SceneName, out YooSceneHandle handle)
                && handle != null
                && handle.ActivateScene())
            {
                mActiveScene = new ResSceneHandle(
                    handle.SceneName, handle.SceneObject.buildIndex, handle.SceneObject.IsValid());
            }
        }

        /// <summary>获取当前由 YooAsset Provider 记录的激活场景。</summary>
        public ResSceneHandle GetActiveScene()
        {
            return mActiveScene;
        }

        /// <summary>请求 Unity 卸载未使用资源。</summary>
        public void UnloadUnusedAssets(Action onComplete)
        {
            AsyncOperation operation = Resources.UnloadUnusedAssets();
            if (operation == null)
            {
                onComplete?.Invoke();
                return;
            }

            operation.completed += _ => onComplete?.Invoke();
        }

        /// <summary>创建不受支持场景请求对应的无效结果。</summary>
        private static ResSceneLoadResult CreateInvalidSceneResult(ResSceneLoadRequest request)
        {
            return new ResSceneLoadResult(new ResSceneHandle(
                request.SceneName, request.BuildIndex, false));
        }

        /// <summary>按当前 YooAsset 主版本检查场景句柄完成状态。</summary>
        private static bool IsSceneHandleSucceeded(YooSceneHandle handle)
        {
#if YOKIFRAME_YOOASSET_3
            return handle.Status == EOperationStatus.Succeeded && handle.SceneObject.IsValid();
#else
            return handle.Status == EOperationStatus.Succeed && handle.SceneObject.IsValid();
#endif
        }

        /// <summary>Single 模式完成后释放已经由引擎替换的旧场景句柄。</summary>
        private void RemoveReplacedSceneHandles(string activeSceneName)
        {
            if (mSceneHandles.Count <= 1)
            {
                return;
            }

            var names = new List<string>();
            foreach (string name in mSceneHandles.Keys)
            {
                if (!string.Equals(name, activeSceneName, StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }

            for (var index = 0; index < names.Count; index++)
            {
                string name = names[index];
                YooSceneHandle handle = mSceneHandles[name];
                mSceneHandles.Remove(name);
                ReleaseHandle(handle);
            }
        }
    }
}

#endif
