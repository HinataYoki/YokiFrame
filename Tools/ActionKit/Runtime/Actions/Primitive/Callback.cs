using System;

namespace YokiFrame
{
    /// <summary>
    /// 在首次执行时调用一次委托并立即完成。
    /// </summary>
    internal sealed class Callback : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<Callback> sPool = PoolKit.Create(
            static () => new Callback(),
            null,
            static action => action.ResetForPool(),
            ActionPoolSettings.Default);

        private Action mCallback;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private Callback() { }

        /// <summary>
        /// 分配并配置一个立即回调 Action；null 回调保持兼容并作为空操作完成。
        /// </summary>
        /// <param name="callback">首次执行时调用的委托。</param>
        /// <returns>新的执行租约。</returns>
        internal static Callback Allocate(Action callback)
        {
            Callback action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            action.mCallback = callback;
            return action;
        }

        /// <summary>调用业务委托后把当前 Action 标记为完成。</summary>
        public override void OnStart()
        {
            mCallback?.Invoke();
            this.Finish();
        }

        /// <summary>释放业务委托，防止池长期持有目标对象。</summary>
        public override void OnDeinit() => mCallback = null;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>仅在诊断请求时描述回调目标。</summary>
        public override string GetDebugInfo() => mCallback == null
            ? "Callback"
            : "Callback -> " + mCallback.Method.DeclaringType + "." + mCallback.Method.Name;
#endif

        /// <summary>把已释放实例归还 Callback 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>回池前再次清理委托，覆盖异常释放路径。</summary>
        private void ResetForPool() => mCallback = null;
    }
}
