using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 PoolKit 统一对象池、共享注册表和诊断跟踪行为。
    /// </summary>
    public sealed class YokiFramePoolKitRuntimeTests
    {
        /// <summary>
        /// 每个测试前清理共享池和诊断状态，避免泛型全局状态跨测试污染。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            PoolKit.Shared.Clear();
            PoolDebugger.Clear();
            PoolDebugger.EnableTracking = false;
            PoolDebugger.EnableEventHistory = false;
            PoolDebugger.EnableStackTrace = false;
            PoolToken.NextId = 0;
            ReusableToken.ResetCounters();
        }

        /// <summary>
        /// 验证容量配置是零堆分配的值类型，且 C# 9 的默认值仍表示零预热和无限缓存。
        /// </summary>
        [Test]
        public void PoolOptionsIsValueTypeAndDefaultKeepsUnboundedCache()
        {
            PoolOptions defaultValue = default;
            PoolOptions implicitDefault = new();
            PoolOptions zeroRetained = new(maxRetained: 0);

            Assert.IsTrue(typeof(PoolOptions).IsValueType);
            Assert.AreEqual(0, defaultValue.InitialCount);
            Assert.AreEqual(PoolOptions.UNBOUNDED, defaultValue.MaxRetained);
            Assert.AreEqual(0, implicitDefault.InitialCount);
            Assert.AreEqual(PoolOptions.UNBOUNDED, implicitDefault.MaxRetained);
            Assert.AreEqual(0, zeroRetained.MaxRetained);

            // 验证 IEquatable<PoolOptions> 与值相等运算符
            PoolOptions explicitDefault = new(0, PoolOptions.UNBOUNDED);
            Assert.AreEqual(defaultValue, explicitDefault);
            Assert.AreEqual(implicitDefault, explicitDefault);
            Assert.IsTrue(defaultValue == explicitDefault);
            Assert.IsTrue(implicitDefault == PoolOptions.Default);
            Assert.IsFalse(defaultValue != explicitDefault);
            Assert.IsFalse(defaultValue == zeroRetained);
            Assert.AreEqual(defaultValue.GetHashCode(), explicitDefault.GetHashCode());
        }

        /// <summary>
        /// 验证容量配置继续在构造时拒绝无效的预热数量和容量组合。
        /// </summary>
        [Test]
        public void PoolOptionsRejectsInvalidCapacityCombinations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new PoolOptions(initialCount: -1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new PoolOptions(maxRetained: PoolOptions.UNBOUNDED - 1); });
            Assert.Throws<ArgumentException>(() => { _ = new PoolOptions(initialCount: 1, maxRetained: 0); });
        }

        /// <summary>
        /// 验证委托池会预热、执行借出和回收回调，并拒绝同一对象重复回收。
        /// </summary>
        [Test]
        public void DelegatePoolPrewarmsRunsLifecycleAndRejectsDuplicateRecycle()
        {
            var pool = PoolKit.Create(
                factory: static () => new PoolToken(),
                onAllocated: static token => token.Activate(),
                onRecycled: static token => token.Reset(),
                options: new PoolOptions(initialCount: 2, maxRetained: 4));

            PoolToken token = pool.Allocate();
            token.Value = 42;

            Assert.AreEqual(1, token.AllocatedCount);
            Assert.AreEqual(1, pool.CurCount);
            Assert.IsTrue(pool.Recycle(token));
            Assert.AreEqual(0, token.Value);
            Assert.AreEqual(2, pool.CurCount);
            Assert.IsFalse(pool.Recycle(token));
            Assert.AreEqual(2, pool.CurCount);
        }

        /// <summary>
        /// 验证借出生命周期失败时，调用方无法取得的对象会被确定性释放，且保留原始异常。
        /// </summary>
        [Test]
        public void AllocateLifecycleFailureDisposesUnreturnedItem()
        {
            FailingAllocationToken created = null;
            var pool = PoolKit.Create(
                () => created = new FailingAllocationToken(),
                _ => throw new InvalidOperationException("Allocate callback failed."));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Allocate());

            Assert.AreEqual("Allocate callback failed.", exception.Message);
            Assert.IsNotNull(created);
            Assert.AreEqual(1, created.DisposedCount);
            Assert.AreEqual(0, pool.CurCount);
        }

        /// <summary>
        /// 验证预热工厂中途失败时已经创建的对象会被释放，避免构造异常泄漏资源。
        /// </summary>
        [Test]
        public void WarmUpFailureDisposesAlreadyCreatedItems()
        {
            var created = new List<TrackingDisposeToken>();
            int factoryCalls = 0;

            Assert.Throws<InvalidOperationException>(() => PoolKit.Create(
                () =>
                {
                    factoryCalls++;
                    if (factoryCalls == 2)
                    {
                        throw new InvalidOperationException("Warm-up factory failed.");
                    }

                    var item = new TrackingDisposeToken();
                    created.Add(item);
                    return item;
                },
                options: new PoolOptions(initialCount: 3)));

            Assert.AreEqual(1, created.Count);
            Assert.AreEqual(1, created[0].DisposeCount);
        }

        /// <summary>
        /// 验证批量清理不会因第一个对象释放失败而跳过后续对象。
        /// </summary>
        [Test]
        public void ClearAttemptsEveryObjectWhenDisposeFails()
        {
            var first = new TrackingDisposeToken(throwOnDispose: true);
            var second = new TrackingDisposeToken(throwOnDispose: true);
            var pending = new Queue<TrackingDisposeToken>(new[] { first, second });
            var pool = PoolKit.Create(
                () => pending.Dequeue(),
                options: new PoolOptions(initialCount: 2));

            Assert.Throws<AggregateException>(() => pool.Clear());
            Assert.AreEqual(1, first.DisposeCount);
            Assert.AreEqual(1, second.DisposeCount);
        }

        /// <summary>
        /// 验证 IPoolable 约定池自动绑定标准生命周期，并在容量满时清理和释放对象但不缓存。
        /// </summary>
        [Test]
        public void PoolablePoolRunsStandardLifecycleAndDiscardsOverflow()
        {
            var pool = PoolKit.Create<ReusableToken>(
                new PoolOptions(initialCount: 2, maxRetained: 2));

            ReusableToken first = pool.Allocate();
            ReusableToken second = pool.Allocate();

            Assert.AreEqual(1, first.AllocatedCount);
            Assert.AreEqual(1, second.AllocatedCount);
            Assert.IsTrue(pool.Recycle(first));
            Assert.AreEqual(1, first.RecycledCount);
            Assert.IsFalse(pool.Recycle(first));
            Assert.IsTrue(pool.Recycle(second));

            var overflow = new ReusableToken();
            Assert.IsFalse(pool.Recycle(overflow));
            Assert.AreEqual(1, overflow.RecycledCount);
            Assert.AreEqual(1, overflow.DisposedCount);
            Assert.AreEqual(2, pool.CurCount);
        }

        /// <summary>
        /// 验证显式委托重载不会因为对象实现 IPoolable 而隐式重复调用接口生命周期。
        /// </summary>
        [Test]
        public void DelegatePoolDoesNotImplicitlyInvokePoolableLifecycle()
        {
            var pool = PoolKit.Create(
                factory: static () => new ReusableToken(),
                onAllocated: static token => token.CustomAllocatedCount++,
                onRecycled: static token => token.CustomRecycledCount++);

            ReusableToken token = pool.Allocate();

            Assert.AreEqual(1, token.CustomAllocatedCount);
            Assert.AreEqual(0, token.AllocatedCount);
            Assert.IsTrue(pool.Recycle(token));
            Assert.AreEqual(1, token.CustomRecycledCount);
            Assert.AreEqual(0, token.RecycledCount);
        }

        /// <summary>
        /// 验证共享注册表显式登记每种类型的唯一全局池，并支持确定性移除。
        /// </summary>
        [Test]
        public void SharedRegistryRegistersGetsAndRemovesSinglePoolPerType()
        {
            ObjectPool<ReusableToken> registered = PoolKit.Shared.Register<ReusableToken>(
                new PoolOptions(initialCount: 1, maxRetained: 3));

            ObjectPool<ReusableToken> resolved = PoolKit.Shared.Get<ReusableToken>();
            ReusableToken token = resolved.Allocate();
            Assert.IsTrue(resolved.Recycle(token));

            Assert.AreSame(registered, resolved);
            Assert.Throws<InvalidOperationException>(
                () => PoolKit.Shared.Register<ReusableToken>());
            Assert.IsTrue(PoolKit.Shared.Remove<ReusableToken>());
            Assert.AreEqual(1, token.DisposedCount);
            Assert.IsFalse(PoolKit.Shared.Remove<ReusableToken>());
            Assert.Throws<InvalidOperationException>(
                () => PoolKit.Shared.Get<ReusableToken>());
        }

        /// <summary>
        /// 验证旧对象池类型和已移除集合池门面不再扩大 Core API 面。
        /// </summary>
        [Test]
        public void LegacyPoolApiTypesAreNotExposed()
        {
            Assert.IsNull(Type.GetType("YokiFrame.PoolKit`1, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.SimplePoolKit`1, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.SafePoolKit`1, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.Pool, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.ListPool`1, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.DictPool`2, YokiFrame"));
            Assert.IsNull(Type.GetType("YokiFrame.SetPool`1, YokiFrame"));
        }

        /// <summary>
        /// 验证 PoolDebugger 只在显式开启时记录活跃对象和事件，并返回隔离的诊断快照。
        /// </summary>
        [Test]
        public void PoolDebuggerTracksActiveObjectsEventsAndReturnsCopies()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pool = PoolKit.Create(
                factory: static () => new PoolToken(),
                options: new PoolOptions(initialCount: 1));

            PoolToken token = pool.Allocate();
            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);

            Assert.AreEqual(1, pools.Count);
            Assert.AreEqual(nameof(PoolToken), pools[0].Name);
            Assert.AreEqual(1, pools[0].ActiveCount);
            Assert.AreEqual(1, pools[0].ActiveObjects.Count);

            pools[0].ActiveObjects.Clear();
            PoolDebugger.GetAllPools(pools);

            Assert.AreEqual(1, pools[0].ActiveObjects.Count);
            Assert.IsTrue(pool.Recycle(token));

            var events = new List<PoolEvent>();
            PoolDebugger.GetEventHistory(events);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(PoolEventType.Return, events[0].EventType);
            Assert.AreEqual(PoolEventType.Spawn, events[1].EventType);
        }

        /// <summary>
        /// 验证同类型局部池在当前诊断会话内具有不同稳定标识，且缓存明细只在快照读取时按预算复制。
        /// </summary>
        [Test]
        public void PoolDebuggerUsesDistinctPoolIdsAndBoundedInactiveSnapshots()
        {
            PoolDebugger.EnableTracking = true;
            var firstPool = PoolKit.Create(static () => new PoolToken());
            var secondPool = PoolKit.Create(static () => new PoolToken());
            PoolToken first = firstPool.Allocate();
            PoolToken second = secondPool.Allocate();

            Assert.IsTrue(firstPool.Recycle(first));
            Assert.IsTrue(secondPool.Recycle(second));

            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools, maxActiveObjectsPerPool: 0, maxInactiveObjectsPerPool: 0);

            Assert.AreEqual(2, pools.Count);
            Assert.AreNotEqual(pools[0].PoolId, pools[1].PoolId);
            Assert.AreEqual(1, pools[0].InactiveObjectTotal);
            Assert.AreEqual(1, pools[1].InactiveObjectTotal);
            Assert.AreEqual(0, pools[0].InactiveObjects.Count);
            Assert.AreEqual(0, pools[1].InactiveObjects.Count);

            PoolDebugger.GetAllPools(pools);
            Assert.AreEqual(1, pools[0].InactiveObjects.Count);
            Assert.AreEqual(1, pools[1].InactiveObjects.Count);
        }

        /// <summary>
        /// 验证诊断事件中的对象显示名异常不会反向中断对象池借还。
        /// </summary>
        [Test]
        public void DiagnosticObjectNameFailureDoesNotInterruptPoolLifecycle()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pool = PoolKit.Create(static () => new ThrowingToStringToken());

            ThrowingToStringToken token = pool.Allocate();

            Assert.IsTrue(pool.Recycle(token));
            var events = new List<PoolEvent>();
            PoolDebugger.GetEventHistory(events);
            Assert.AreEqual(typeof(ThrowingToStringToken).FullName, events[0].ObjectName);
        }

        /// <summary>
        /// 验证关闭跟踪期间的借还不会在重新开启后保留过期的活跃对象记录。
        /// </summary>
        [Test]
        public void TrackingTransitionClearsStaleActiveObjects()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pool = PoolKit.Create(static () => new PoolToken());
            PoolToken token = pool.Allocate();

            PoolDebugger.EnableTracking = false;
            Assert.IsTrue(pool.Recycle(token));
            PoolDebugger.EnableTracking = true;

            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);
            Assert.AreEqual(0, pools[0].ActiveCount);
            Assert.AreEqual(1, pools[0].InactiveObjects.Count);
        }

        /// <summary>
        /// 验证 PoolDebugger 能通过内部诊断契约把仍借出的对象强制归还原池。
        /// </summary>
        [Test]
        public void PoolDebuggerForceReturnRecyclesTrackedObject()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pool = PoolKit.Create(static () => new PoolToken());

            PoolToken token = pool.Allocate();

            Assert.IsTrue(PoolDebugger.IsObjectTracked(token));
            Assert.IsTrue(PoolDebugger.ForceReturn(pool, token));
            Assert.AreEqual(1, pool.CurCount);
            Assert.IsFalse(PoolDebugger.IsObjectTracked(token));
        }

        /// <summary>
        /// 测试用普通对象池元素。
        /// </summary>
        private sealed class PoolToken
        {
            public static int NextId;
            private readonly int mId;

            /// <summary>
            /// 创建测试对象并分配稳定编号，便于诊断输出。
            /// </summary>
            public PoolToken()
            {
                mId = ++NextId;
            }

            /// <summary>
            /// 获取测试对象被借出的次数。
            /// </summary>
            public int AllocatedCount { get; private set; }

            /// <summary>
            /// 获取或设置测试用业务值。
            /// </summary>
            public int Value { get; set; }

            /// <summary>
            /// 记录对象被借出。
            /// </summary>
            public void Activate()
            {
                AllocatedCount++;
            }

            /// <summary>
            /// 回收时清理测试对象状态。
            /// </summary>
            public void Reset()
            {
                Value = 0;
            }

            /// <summary>
            /// 输出稳定对象名，便于 PoolDebugger 事件记录。
            /// </summary>
            /// <returns>对象调试名。</returns>
            public override string ToString()
            {
                return "PoolToken#" + mId;
            }
        }

        /// <summary>
        /// 测试借出回调失败后的确定性释放。
        /// </summary>
        private sealed class FailingAllocationToken : IDisposable
        {
            /// <summary>
            /// 获取对象被释放的次数。
            /// </summary>
            public int DisposedCount { get; private set; }

            /// <summary>
            /// 记录对象池在失败路径中执行的资源释放。
            /// </summary>
            public void Dispose()
            {
                DisposedCount++;
            }
        }

        /// <summary>
        /// 支持记录并模拟释放失败的对象池测试对象。
        /// </summary>
        private sealed class TrackingDisposeToken : IDisposable
        {
            private readonly bool mThrowOnDispose;

            /// <summary>创建测试对象。</summary>
            /// <param name="throwOnDispose">是否在释放时抛出异常。</param>
            public TrackingDisposeToken(bool throwOnDispose = false)
            {
                mThrowOnDispose = throwOnDispose;
            }

            /// <summary>获取释放尝试次数。</summary>
            public int DisposeCount { get; private set; }

            /// <summary>记录释放尝试，并按配置模拟底层资源异常。</summary>
            public void Dispose()
            {
                DisposeCount++;
                if (mThrowOnDispose)
                {
                    throw new InvalidOperationException("Dispose failed.");
                }
            }
        }

        /// <summary>
        /// 提供会抛出显示名异常的测试对象，验证诊断不能影响业务流程。
        /// </summary>
        private sealed class ThrowingToStringToken
        {
            /// <summary>
            /// 模拟业务对象的显示名实现发生异常。
            /// </summary>
            /// <returns>该方法始终抛出异常。</returns>
            public override string ToString()
            {
                throw new InvalidOperationException("Display name failed.");
            }
        }

        /// <summary>
        /// 测试用标准池化对象。
        /// </summary>
        private sealed class ReusableToken : IPoolable, IDisposable
        {
            public static int CreatedCount;

            /// <summary>
            /// 创建测试对象并记录构造次数。
            /// </summary>
            public ReusableToken()
            {
                CreatedCount++;
            }

            /// <summary>
            /// 获取标准借出生命周期触发次数。
            /// </summary>
            public int AllocatedCount { get; private set; }

            /// <summary>
            /// 获取标准回收生命周期触发次数。
            /// </summary>
            public int RecycledCount { get; private set; }

            /// <summary>
            /// 获取对象释放次数。
            /// </summary>
            public int DisposedCount { get; private set; }

            /// <summary>
            /// 获取或设置自定义借出委托触发次数。
            /// </summary>
            public int CustomAllocatedCount { get; set; }

            /// <summary>
            /// 获取或设置自定义回收委托触发次数。
            /// </summary>
            public int CustomRecycledCount { get; set; }

            /// <summary>
            /// 标准池借出时记录生命周期调用。
            /// </summary>
            public void OnAllocated()
            {
                AllocatedCount++;
            }

            /// <summary>
            /// 标准池回收时记录生命周期调用。
            /// </summary>
            public void OnRecycled()
            {
                RecycledCount++;
            }

            /// <summary>
            /// 对象被池丢弃时记录资源释放调用。
            /// </summary>
            public void Dispose()
            {
                DisposedCount++;
            }

            /// <summary>
            /// 重置静态测试计数器。
            /// </summary>
            public static void ResetCounters()
            {
                CreatedCount = 0;
            }
        }
    }
}
