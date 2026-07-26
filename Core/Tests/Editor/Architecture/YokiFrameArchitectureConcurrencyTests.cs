using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Architecture 延迟创建服务在多线程竞争下仍保持单实例和单次生命周期。
    /// </summary>
    public sealed class YokiFrameArchitectureConcurrencyTests
    {
        private const int CONSTRUCTOR_WAIT_MILLISECONDS = 2000;
        private const int TASK_WAIT_MILLISECONDS = 8000;

        private IArchitecture mArchitecture;

        /// <summary>
        /// 创建独立测试架构并清空并发服务计数，避免其它测试的泛型静态实例污染结果。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ConcurrentForcedService.Reset(default);
            mArchitecture = Architecture<ConcurrentForceArchitecture>.Interface;
        }

        /// <summary>
        /// 释放测试架构和静态同步对象，确保失败用例也不会影响后续测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ConcurrentForcedService.Reset(default);
            if (mArchitecture != default)
            {
                mArchitecture.Dispose();
                mArchitecture = default;
            }
        }

        /// <summary>
        /// 让两个后台调用同时强制获取同一服务，验证只构造、初始化并返回一个实例。
        /// 构造屏障会让旧实现的两个候选稳定重叠，同时让修复后的单个候选在超时后继续。
        /// </summary>
        [Test]
        public void ForceCreationIsSharedAcrossConcurrentCallers()
        {
            using (var readyGate = new CountdownEvent(2))
            using (var startGate = new ManualResetEventSlim(false))
            using (var constructorBarrier = new Barrier(2))
            {
                ConcurrentForcedService.Reset(constructorBarrier);
                try
                {
                    Task<ConcurrentForcedService> firstTask = CreateGetServiceTask(readyGate, startGate);
                    Task<ConcurrentForcedService> secondTask = CreateGetServiceTask(readyGate, startGate);

                    Assert.IsTrue(
                        readyGate.Wait(TASK_WAIT_MILLISECONDS),
                        "并发调用未能在限定时间内进入起跑门。");
                    startGate.Set();

                    Task[] tasks = { firstTask, secondTask };
                    Assert.IsTrue(
                        Task.WaitAll(tasks, TASK_WAIT_MILLISECONDS),
                        "并发 force 获取服务未能在限定时间内完成。");
                    Assert.AreSame(firstTask.Result, secondTask.Result);
                    Assert.AreSame(firstTask.Result, mArchitecture.GetService<ConcurrentForcedService>());
                    Assert.AreEqual(1, ConcurrentForcedService.CreatedCount);
                    Assert.AreEqual(1, ConcurrentForcedService.InitCount);
                    Assert.AreEqual(0, ConcurrentForcedService.DisposeCount);
                }
                finally
                {
                    ConcurrentForcedService.ClearConstructorBarrier();
                }
            }
        }

        /// <summary>
        /// 验证 force 候选构造期间完成的显式注册优先，候选释放后所有查询返回显式实例。
        /// </summary>
        [Test]
        public void ExplicitRegistrationWinsWhileForceCandidateIsBeingCreated()
        {
            var explicitService = new ConcurrentForcedService();
            using (var constructorBarrier = new Barrier(
                2,
                _ => mArchitecture.Register(explicitService)))
            {
                ConcurrentForcedService.Reset(constructorBarrier);
                try
                {
                    Task<ConcurrentForcedService> forceTask = Task.Run(
                        () => mArchitecture.GetService<ConcurrentForcedService>(true));

                    Assert.IsTrue(
                        constructorBarrier.SignalAndWait(TASK_WAIT_MILLISECONDS),
                        "force 候选未能在限定时间内进入构造屏障。");
                    Assert.IsTrue(
                        forceTask.Wait(TASK_WAIT_MILLISECONDS),
                        "显式注册完成后 force 获取未能在限定时间内结束。");
                    Assert.AreSame(explicitService, forceTask.Result);
                    Assert.AreSame(explicitService, mArchitecture.GetService<ConcurrentForcedService>());
                    Assert.AreEqual(1, ConcurrentForcedService.CreatedCount);
                    Assert.AreEqual(1, ConcurrentForcedService.InitCount);
                    Assert.AreEqual(1, ConcurrentForcedService.DisposeCount);
                }
                finally
                {
                    ConcurrentForcedService.ClearConstructorBarrier();
                }
            }
        }

        /// <summary>
        /// 让服务初始化在持有架构锁期间重入 force 获取目标服务，同时后台线程并发 force 同型服务。
        /// 门闩让后台候选稳定停在构造阶段放大互等时序，验证持锁线程绕过共享创建后双方均能完成。
        /// </summary>
        [Test]
        public void ReentrantForceDuringRegisterDoesNotDeadlockWithConcurrentForce()
        {
            IArchitecture architecture = Architecture<ReentrantForceArchitecture>.Interface;
            using (var constructorEntered = new ManualResetEventSlim(false))
            using (var constructorGate = new ManualResetEventSlim(false))
            {
                ReentrantTargetService.Reset(constructorEntered, constructorGate);
                try
                {
                    Task<ReentrantTargetService> backgroundTask = Task.Run(
                        () => architecture.GetService<ReentrantTargetService>(true));

                    Assert.IsTrue(
                        constructorEntered.Wait(TASK_WAIT_MILLISECONDS),
                        "后台 force 候选未能在限定时间内进入构造阶段。");

                    var initService = new ReentrantInitService();
                    Task registerTask = Task.Run(() => architecture.Register(initService));

                    Task[] tasks = { backgroundTask, registerTask };
                    Assert.IsTrue(
                        Task.WaitAll(tasks, TASK_WAIT_MILLISECONDS),
                        "持锁重入 force 与后台并发 force 未能在限定时间内完成。");
                    Assert.IsNotNull(initService.TargetDuringInit);
                    Assert.AreSame(initService.TargetDuringInit, backgroundTask.Result);
                    Assert.AreSame(
                        initService.TargetDuringInit,
                        architecture.GetService<ReentrantTargetService>());
                    Assert.AreEqual(2, ReentrantTargetService.CreatedCount);
                    Assert.AreEqual(1, ReentrantTargetService.InitCount);
                    Assert.AreEqual(1, ReentrantTargetService.DisposeCount);
                }
                finally
                {
                    ReentrantTargetService.ClearGates();
                    architecture.Dispose();
                }
            }
        }

        /// <summary>
        /// 创建一个等待共同起跑信号后强制获取服务的后台任务。
        /// </summary>
        /// <param name="readyGate">记录后台任务已经就绪的计数门。</param>
        /// <param name="startGate">同时释放后台任务的起跑门。</param>
        /// <returns>最终由架构选中的服务实例任务。</returns>
        private Task<ConcurrentForcedService> CreateGetServiceTask(
            CountdownEvent readyGate,
            ManualResetEventSlim startGate)
        {
            return Task.Run(() =>
            {
                readyGate.Signal();
                startGate.Wait();
                return mArchitecture.GetService<ConcurrentForcedService>(true);
            });
        }

        /// <summary>
        /// 提供不主动注册服务的测试架构，使 force 路径成为唯一创建入口。
        /// </summary>
        public sealed class ConcurrentForceArchitecture : Architecture<ConcurrentForceArchitecture>
        {
            /// <summary>
            /// 保持空初始化，等待并发测试显式触发延迟服务创建。
            /// </summary>
            protected override void OnInit()
            {
            }
        }

        /// <summary>
        /// 记录构造、初始化和释放次数，并用屏障稳定放大旧实现的创建竞态。
        /// </summary>
        public sealed class ConcurrentForcedService : AbstractService
        {
            private static Barrier sConstructorBarrier;
            private static int sCreatedCount;
            private static int sInitCount;
            private static int sDisposeCount;

            /// <summary>
            /// 创建测试服务并等待潜在的第二个并发候选；修复后只有当前候选会等待到超时。
            /// </summary>
            public ConcurrentForcedService()
            {
                Interlocked.Increment(ref sCreatedCount);
                Barrier constructorBarrier = Volatile.Read(ref sConstructorBarrier);
                if (constructorBarrier != default)
                {
                    constructorBarrier.SignalAndWait(CONSTRUCTOR_WAIT_MILLISECONDS);
                }
            }

            /// <summary>获取当前测试轮次的构造次数。</summary>
            internal static int CreatedCount => Volatile.Read(ref sCreatedCount);

            /// <summary>获取当前测试轮次的初始化次数。</summary>
            internal static int InitCount => Volatile.Read(ref sInitCount);

            /// <summary>获取当前测试轮次的释放次数。</summary>
            internal static int DisposeCount => Volatile.Read(ref sDisposeCount);

            /// <summary>
            /// 重置测试计数并安装当前测试使用的构造屏障；传入空值时同时解除静态引用。
            /// </summary>
            /// <param name="constructorBarrier">用于同步并发候选构造的屏障。</param>
            internal static void Reset(Barrier constructorBarrier)
            {
                Volatile.Write(ref sConstructorBarrier, constructorBarrier);
                Interlocked.Exchange(ref sCreatedCount, 0);
                Interlocked.Exchange(ref sInitCount, 0);
                Interlocked.Exchange(ref sDisposeCount, 0);
            }

            /// <summary>
            /// 清除构造屏障引用，避免测试结束后保留已经释放的同步对象。
            /// </summary>
            internal static void ClearConstructorBarrier()
            {
                Volatile.Write(ref sConstructorBarrier, default);
            }

            /// <summary>
            /// 记录架构对最终服务实例执行的一次初始化。
            /// </summary>
            protected override void OnInit()
            {
                Interlocked.Increment(ref sInitCount);
            }

            /// <summary>
            /// 记录被替换候选或测试架构释放时执行的服务释放。
            /// </summary>
            protected override void OnDispose()
            {
                Interlocked.Increment(ref sDisposeCount);
            }
        }

        /// <summary>
        /// 提供不主动注册服务的测试架构，用于验证持锁重入 force 与后台并发 force 不互等。
        /// </summary>
        public sealed class ReentrantForceArchitecture : Architecture<ReentrantForceArchitecture>
        {
            /// <summary>
            /// 保持空初始化，等待测试在注册阶段触发重入 force 获取。
            /// </summary>
            protected override void OnInit()
            {
            }
        }

        /// <summary>
        /// 初始化期间重入 force 获取目标服务的测试服务，先放行后台候选构造以放大互等时序。
        /// </summary>
        public sealed class ReentrantInitService : AbstractService
        {
            /// <summary>
            /// 获取初始化期间重入 force 获取到的目标服务。
            /// </summary>
            public ReentrantTargetService TargetDuringInit { get; private set; }

            /// <summary>
            /// 放行后台候选构造后，在持有架构锁的初始化中重入 force 获取同型目标服务。
            /// </summary>
            protected override void OnInit()
            {
                ReentrantTargetService.OpenConstructorGate();
                TargetDuringInit = Architecture.GetService<ReentrantTargetService>(true);
            }
        }

        /// <summary>
        /// 记录构造、初始化和释放次数，并用门闩让后台 force 候选稳定停在构造阶段。
        /// </summary>
        public sealed class ReentrantTargetService : AbstractService
        {
            private static ManualResetEventSlim sConstructorEntered;
            private static ManualResetEventSlim sConstructorGate;
            private static int sCreatedCount;
            private static int sInitCount;
            private static int sDisposeCount;

            /// <summary>
            /// 创建测试服务，先宣告已进入构造阶段，再等待测试放行；放行后的候选立即通过。
            /// </summary>
            public ReentrantTargetService()
            {
                Interlocked.Increment(ref sCreatedCount);
                ManualResetEventSlim constructorEntered = Volatile.Read(ref sConstructorEntered);
                if (constructorEntered != default)
                {
                    constructorEntered.Set();
                }

                ManualResetEventSlim constructorGate = Volatile.Read(ref sConstructorGate);
                if (constructorGate != default)
                {
                    constructorGate.Wait(CONSTRUCTOR_WAIT_MILLISECONDS);
                }
            }

            /// <summary>获取当前测试轮次的构造次数。</summary>
            internal static int CreatedCount => Volatile.Read(ref sCreatedCount);

            /// <summary>获取当前测试轮次的初始化次数。</summary>
            internal static int InitCount => Volatile.Read(ref sInitCount);

            /// <summary>获取当前测试轮次的释放次数。</summary>
            internal static int DisposeCount => Volatile.Read(ref sDisposeCount);

            /// <summary>
            /// 重置测试计数并安装当前测试使用的构造宣告与放行门闩。
            /// </summary>
            /// <param name="constructorEntered">候选进入构造阶段时置位的宣告门闩。</param>
            /// <param name="constructorGate">候选构造等待放行的门闩。</param>
            internal static void Reset(
                ManualResetEventSlim constructorEntered,
                ManualResetEventSlim constructorGate)
            {
                Volatile.Write(ref sConstructorEntered, constructorEntered);
                Volatile.Write(ref sConstructorGate, constructorGate);
                Interlocked.Exchange(ref sCreatedCount, 0);
                Interlocked.Exchange(ref sInitCount, 0);
                Interlocked.Exchange(ref sDisposeCount, 0);
            }

            /// <summary>
            /// 放行所有等待构造门闩的候选。
            /// </summary>
            internal static void OpenConstructorGate()
            {
                ManualResetEventSlim constructorGate = Volatile.Read(ref sConstructorGate);
                if (constructorGate != default)
                {
                    constructorGate.Set();
                }
            }

            /// <summary>
            /// 清除门闩引用，避免测试结束后保留已经释放的同步对象。
            /// </summary>
            internal static void ClearGates()
            {
                Volatile.Write(ref sConstructorEntered, default);
                Volatile.Write(ref sConstructorGate, default);
            }

            /// <summary>
            /// 记录架构对最终服务实例执行的一次初始化。
            /// </summary>
            protected override void OnInit()
            {
                Interlocked.Increment(ref sInitCount);
            }

            /// <summary>
            /// 记录被替换候选或测试架构释放时执行的服务释放。
            /// </summary>
            protected override void OnDispose()
            {
                Interlocked.Increment(ref sDisposeCount);
            }
        }
    }
}
