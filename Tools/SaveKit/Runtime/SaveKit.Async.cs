using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class SaveKit
    {
        /// <summary>异步保存到显式目标，并在操作前后检查取消令牌。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="displayName">可选显示名称。</param>
        /// <param name="token">当前保存调用的取消令牌。</param>
        /// <returns>写入成功时返回 true。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> SaveAsync(
#else
        public static Task<bool> SaveAsync(
#endif
            SaveTarget target,
            SaveData data,
            string displayName = null,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var saved = Save(target, data, displayName);
            token.ThrowIfCancellationRequested();
#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(saved);
#else
            return Task.FromResult(saved);
#endif
        }

        /// <summary>异步保存到显式目标的取消令牌便捷重载。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="token">当前保存调用的取消令牌。</param>
        /// <returns>写入成功时返回 true。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> SaveAsync(SaveTarget target, SaveData data, CancellationToken token)
#else
        public static Task<bool> SaveAsync(SaveTarget target, SaveData data, CancellationToken token)
#endif
        {
            return SaveAsync(target, data, null, token);
        }

        /// <summary>异步保存到数字槽位。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="displayName">可选显示名称。</param>
        /// <param name="token">当前保存调用的取消令牌。</param>
        /// <returns>写入成功时返回 true。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> SaveAsync(
#else
        public static Task<bool> SaveAsync(
#endif
            int slotId,
            SaveData data,
            string displayName = null,
            CancellationToken token = default)
        {
            return SaveAsync(SaveTarget.Slot(slotId), data, displayName, token);
        }

        /// <summary>异步保存到数字槽位的取消令牌便捷重载。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="token">当前保存调用的取消令牌。</param>
        /// <returns>写入成功时返回 true。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> SaveAsync(int slotId, SaveData data, CancellationToken token)
#else
        public static Task<bool> SaveAsync(int slotId, SaveData data, CancellationToken token)
#endif
        {
            return SaveAsync(SaveTarget.Slot(slotId), data, null, token);
        }

        /// <summary>异步读取显式目标；缺失或无效时返回空数据。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <param name="token">当前读取调用的取消令牌。</param>
        /// <returns>读取到的保存数据；失败时返回空。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<SaveData> LoadAsync(SaveTarget target, CancellationToken token = default)
#else
        public static Task<SaveData> LoadAsync(SaveTarget target, CancellationToken token = default)
#endif
        {
            token.ThrowIfCancellationRequested();
            var data = Load(target);
            token.ThrowIfCancellationRequested();
#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(data);
#else
            return Task.FromResult(data);
#endif
        }

        /// <summary>异步读取数字槽位；缺失或无效时返回空数据。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="token">当前读取调用的取消令牌。</param>
        /// <returns>读取到的保存数据；失败时返回空。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<SaveData> LoadAsync(int slotId, CancellationToken token = default)
#else
        public static Task<SaveData> LoadAsync(int slotId, CancellationToken token = default)
#endif
        {
            return LoadAsync(SaveTarget.Slot(slotId), token);
        }

        /// <summary>异步读取显式目标并返回结构化状态。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <param name="token">当前读取调用的取消令牌。</param>
        /// <returns>包含成功数据或失败状态的读档结果。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<SaveLoadResult> TryLoadAsync(SaveTarget target, CancellationToken token = default)
#else
        public static Task<SaveLoadResult> TryLoadAsync(SaveTarget target, CancellationToken token = default)
#endif
        {
            token.ThrowIfCancellationRequested();
            var result = TryLoad(target);
            token.ThrowIfCancellationRequested();
#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(result);
#else
            return Task.FromResult(result);
#endif
        }

        /// <summary>异步读取数字槽位并返回结构化状态。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="token">当前读取调用的取消令牌。</param>
        /// <returns>包含成功数据或失败状态的读档结果。</returns>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<SaveLoadResult> TryLoadAsync(int slotId, CancellationToken token = default)
#else
        public static Task<SaveLoadResult> TryLoadAsync(int slotId, CancellationToken token = default)
#endif
        {
            return TryLoadAsync(SaveTarget.Slot(slotId), token);
        }
    }
}
