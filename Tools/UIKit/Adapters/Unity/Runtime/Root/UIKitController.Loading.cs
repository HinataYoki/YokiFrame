#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// 同步获取或物化指定 Panel；异步同类型请求进行中时拒绝阻塞主线程。
        /// </summary>
        internal PanelEntry GetOrCreate(Type panelType, UILevel level, PanelCachePolicy policy, IUIData data)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            if (TryGetLiveEntry(panelType, out PanelEntry existing)) return existing;
            if (mPendingLoads.ContainsKey(panelType))
                throw new InvalidOperationException("An asynchronous UIKit load for this panel type is in progress. Await it instead.");
            IPanelPrefabLease lease = null;
            try
            {
                lease = mLoader.Load(panelType);
                return CreateEntry(panelType, lease, level, policy, data);
            }
            catch
            {
                if (lease != null) lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 异步加入同类型 single-flight，并以调用方独立令牌等待结果。
        /// </summary>
        internal async Task<PanelEntry> GetOrCreateAsync(
            Type panelType,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data,
            CancellationToken token)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            token.ThrowIfCancellationRequested();
            if (TryGetLiveEntry(panelType, out PanelEntry existing)) return existing;
            PanelLoadOperation operation = GetOrJoinLoad(panelType, level, policy, data);
            return await operation.WaitAsync(token);
        }

        /// <summary>
        /// 获取既有 single-flight，或创建并立即启动唯一底层加载。
        /// </summary>
        private PanelLoadOperation GetOrJoinLoad(
            Type panelType,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data)
        {
            if (mPendingLoads.TryGetValue(panelType, out PanelLoadOperation existing))
            {
                existing.Join(data);
                return existing;
            }
            var operation = new PanelLoadOperation(panelType, mLoadGeneration, OnLoadAbandoned);
            operation.Join(data);
            mPendingLoads.Add(panelType, operation);
            _ = RunMaterializationAsync(operation, level, policy, data);
            return operation;
        }

        /// <summary>
        /// 执行 single-flight 的唯一底层 load、Instantiate、类型校验和 OnInit。
        /// </summary>
        private async Task<PanelEntry> RunMaterializationAsync(
            PanelLoadOperation operation,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data)
        {
            IPanelPrefabLease lease = null;
            PanelEntry result = null;
            Exception failure = null;
            bool canceled = false;
            try
            {
                lease = await mLoader.LoadAsync(operation.PanelType, operation.SharedToken);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

#if YOKIFRAME_UNITASK_SUPPORT
            await UniTask.SwitchToMainThread();
#endif
            try
            {
                if (!canceled && failure == null)
                {
                    result = CreateMaterializedEntry(operation, lease, level, policy, data);
                    lease = null;
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                CompleteMaterialization(operation, lease, result, failure, canceled);
            }

            return result;
        }

        /// <summary>
        /// 在 Unity 主线程把已加载 lease 转为唯一 PanelEntry，并拒绝 teardown 或重入造成的旧代次结果。
        /// </summary>
        /// <param name="operation">当前 single-flight。</param>
        /// <param name="lease">loader 返回且尚未转交的独占 lease。</param>
        /// <param name="level">目标 UI 层级。</param>
        /// <param name="policy">关闭后的缓存策略。</param>
        /// <param name="data">首个请求的初始化数据。</param>
        /// <returns>仍属于当前 controller 会话的已登记 entry。</returns>
        private PanelEntry CreateMaterializedEntry(
            PanelLoadOperation operation,
            IPanelPrefabLease lease,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data)
        {
            EnsureLoadOperationCurrent(operation);
            operation.SharedToken.ThrowIfCancellationRequested();
            IUIData initializationData = operation.InitializationData ?? data;
            PanelEntry result = CreateEntry(
                operation.PanelType,
                lease,
                level,
                policy,
                initializationData,
                operation);
            if (IsLoadOperationCurrent(operation)) return result;
            DisposeEntry(result);
            throw new OperationCanceledException();
        }

        /// <summary>
        /// 释放未转交的 lease、摘除当前 flight，并在底层结束后提交唯一公开终态。
        /// </summary>
        private void CompleteMaterialization(
            PanelLoadOperation operation,
            IPanelPrefabLease lease,
            PanelEntry result,
            Exception failure,
            bool canceled)
        {
            failure = ReleaseUnclaimedLease(lease, failure);
            if (mPendingLoads.TryGetValue(operation.PanelType, out PanelLoadOperation current)
                && ReferenceEquals(current, operation)) mPendingLoads.Remove(operation.PanelType);
            try
            {
                if (result != null) operation.SetResult(result);
                else if (failure != null)
                {
                    if (!operation.TrySetException(failure)) LogKit.Exception(failure);
                }
                else if (canceled) operation.SetCanceled();
            }
            finally
            {
                operation.MarkMaterializationCompleted();
            }
        }

        /// <summary>
        /// 释放尚未转交给 PanelEntry 的 lease；释放异常作为本次加载失败返回。
        /// </summary>
        private static Exception ReleaseUnclaimedLease(IPanelPrefabLease lease, Exception failure)
        {
            if (lease == null) return failure;
            try
            {
                lease.Dispose();
            }
            catch (Exception exception)
            {
                if (failure == null) return exception;
                LogKit.Exception(exception);
            }

            return failure;
        }

        /// <summary>
        /// 最后一个等待者取消后立即摘除当前 flight，再撤销底层加载，允许后续请求建立新 flight。
        /// </summary>
        private void OnLoadAbandoned(PanelLoadOperation operation)
        {
            if (operation == null) return;
            if (mPendingLoads.TryGetValue(operation.PanelType, out PanelLoadOperation current)
                && ReferenceEquals(current, operation)) mPendingLoads.Remove(operation.PanelType);
            CancelLoadOperation(operation);
        }

        /// <summary>
        /// 判断异步物化仍属于当前 controller 会话，避免 Root teardown 或旧 flight 复活孤儿实例。
        /// </summary>
        private bool IsLoadOperationCurrent(PanelLoadOperation operation)
        {
            return operation != null
                && !mDisposed
                && operation.Generation == mLoadGeneration
                && mPendingLoads.TryGetValue(operation.PanelType, out PanelLoadOperation current)
                && ReferenceEquals(current, operation);
        }

        /// <summary>
        /// 在 Unity Instantiate 前拒绝已经脱离当前会话的异步物化请求。
        /// </summary>
        private void EnsureLoadOperationCurrent(PanelLoadOperation operation)
        {
            if (operation != null && !IsLoadOperationCurrent(operation))
                throw new OperationCanceledException();
        }

        /// <summary>
        /// 在禁用暂存根下实例化 Prefab、恢复资产名称，并把 lease 原子转交给新 entry。
        /// </summary>
        private PanelEntry CreateEntry(
            Type panelType,
            IPanelPrefabLease lease,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data,
            PanelLoadOperation operation = null)
        {
            if (lease == null) throw new InvalidOperationException("UIKit panel loader returned no prefab lease.");
            GameObject prefab = lease.Prefab;
            if (prefab == default) throw new InvalidOperationException("UIKit panel loader returned an invalid prefab.");
            EnsureLoadOperationCurrent(operation);
            GameObject instance = UnityObject.Instantiate(prefab, mRoot.StorageRoot, false);
            try
            {
                EnsureLoadOperationCurrent(operation);
                instance.name = prefab.name;
                UIPanel panel = instance.GetComponent(panelType) as UIPanel;
                if (panel == default) throw CreateMissingPanelException(panelType, lease);
                instance.SetActive(false);
                return RegisterEntry(panelType, panel, lease, level, policy, data);
            }
            catch
            {
                DestroyObject(instance);
                throw;
            }
        }

        /// <summary>
        /// 登记唯一 entry 后执行一次 OnInit，使钩子内查询能看到当前实例。
        /// </summary>
        private PanelEntry RegisterEntry(
            Type panelType,
            UIPanel panel,
            IPanelPrefabLease lease,
            UILevel level,
            PanelCachePolicy policy,
            IUIData data)
        {
            var entry = new PanelEntry(this, panelType, panel, lease, level, policy);
            mEntries.Add(panelType, entry);
            panel.AttachOwner(entry);
            entry.LifetimeSentinel = panel.gameObject.AddComponent<PanelLifetimeSentinel>();
            entry.LifetimeSentinel.Initialize(entry);
            panel.InvokeInit(data);
            OnStateChanged();
            return entry;
        }

        /// <summary>
        /// 读取有效 entry；发现 Unity fake-null 时先回收残留所有权。
        /// </summary>
        private bool TryGetLiveEntry(Type panelType, out PanelEntry entry)
        {
            if (!mEntries.TryGetValue(panelType, out entry)) return false;
            if (entry.Panel != default) return true;
            ReleaseDestroyedEntry(entry);
            entry = null;
            return false;
        }

        /// <summary>
        /// 构造包含 location 的 Prefab 组件缺失异常。
        /// </summary>
        private static InvalidOperationException CreateMissingPanelException(Type panelType, IPanelPrefabLease lease)
        {
            return new InvalidOperationException(
                "UIKit prefab '" + lease.Location + "' does not contain panel component " + panelType.FullName + ".");
        }

        /// <summary>
        /// 按 Unity 当前模式销毁临时或受管 GameObject。
        /// </summary>
        private static void DestroyObject(GameObject target)
        {
            if (target == default) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityObject.DestroyImmediate(target);
                return;
            }
#endif
            UnityObject.Destroy(target);
        }
    }
}
#endif
