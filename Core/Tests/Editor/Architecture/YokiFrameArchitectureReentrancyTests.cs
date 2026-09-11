using System.Threading;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证架构初始化期间重入同型单例时不会重复执行 OnInit 与服务初始化。
    /// </summary>
    public sealed class YokiFrameArchitectureReentrancyTests
    {
        /// <summary>
        /// 验证 OnInit 内部取回同型 Interface 时，初始化只发布一次。
        /// </summary>
        [Test]
        public void InterfaceAccessDuringOnInitRunsOnInitOnce()
        {
            // Architecture<T> 是进程级单例，同一 Editor 域内跨次运行会保留已发布实例，
            // 因此这里分别断言「首次初始化」与「已发布后再次取回」两种情况，保证用例可重复执行。
            int before = ReentrancedByOnInitArchitecture.OnInitCount;

            IArchitecture architecture = ReentrancedByOnInitArchitecture.Interface;

            Assert.IsNotNull(architecture);
            int after = ReentrancedByOnInitArchitecture.OnInitCount;
            if (before == 0)
            {
                Assert.AreEqual(1, after, "首次初始化必须恰好执行一次 OnInit，初始化期间的重入不得使其翻倍");
            }
            else
            {
                Assert.AreEqual(before, after, "单例已发布后重入取回不得再次执行 OnInit");
            }
        }

        /// <summary>
        /// 验证服务 OnInit 内部取回所属架构 Interface 时，架构与服务的初始化各自只发布一次。
        /// </summary>
        [Test]
        public void InterfaceAccessDuringServiceInitRunsEachInitOnce()
        {
            int architectureBefore = ReentrancedByServiceArchitecture.OnInitCount;
            int serviceBefore = ReentrancedByServiceArchitecture.ServiceInitCount;

            IArchitecture architecture = ReentrancedByServiceArchitecture.Interface;

            Assert.IsNotNull(architecture);
            int architectureAfter = ReentrancedByServiceArchitecture.OnInitCount;
            int serviceAfter = ReentrancedByServiceArchitecture.ServiceInitCount;
            if (architectureBefore == 0)
            {
                Assert.AreEqual(1, architectureAfter, "服务初始化期间重入架构时，架构 OnInit 不得重复执行");
                Assert.AreEqual(1, serviceAfter, "服务 OnInit 不得重复执行");
            }
            else
            {
                Assert.AreEqual(architectureBefore, architectureAfter, "已发布后不得再次执行架构 OnInit");
                Assert.AreEqual(serviceBefore, serviceAfter, "已发布后不得再次执行服务 OnInit");
            }
        }

        /// <summary>
        /// 在 OnInit 内取回同型单例的架构；用于暴露初始化尚未发布时的重入。
        /// </summary>
        public sealed class ReentrancedByOnInitArchitecture : Architecture<ReentrancedByOnInitArchitecture>
        {
            private static int sOnInitCount;

            /// <summary>获取当前轮次的架构初始化次数。</summary>
            internal static int OnInitCount => Volatile.Read(ref sOnInitCount);

            /// <summary>首次进入时重入同型单例，其余层直接返回以限定递归深度。</summary>
            protected override void OnInit()
            {
                int count = Interlocked.Increment(ref sOnInitCount);
                if (count == 1)
                {
                    _ = ReentrancedByOnInitArchitecture.Interface;
                }
            }
        }

        /// <summary>
        /// 通过服务 OnInit 重入架构的测试架构；服务在 OnInit 中注册自身。
        /// </summary>
        public sealed class ReentrancedByServiceArchitecture : Architecture<ReentrancedByServiceArchitecture>
        {
            private static int sOnInitCount;

            /// <summary>获取当前轮次的架构初始化次数。</summary>
            internal static int OnInitCount => Volatile.Read(ref sOnInitCount);

            /// <summary>获取当前轮次的服务初始化次数。</summary>
            internal static int ServiceInitCount => Volatile.Read(ref ReentrancedByServiceService.sInitCount);

            /// <summary>注册在初始化期间重入架构的服务。</summary>
            protected override void OnInit()
            {
                Interlocked.Increment(ref sOnInitCount);
                Register(new ReentrancedByServiceService());
            }
        }

        /// <summary>
        /// 在自身 OnInit 中取回所属架构单例的服务，用于覆盖服务初始化阶段的重入。
        /// </summary>
        public sealed class ReentrancedByServiceService : AbstractService
        {
            /// <summary>当前轮次的服务初始化次数；供外层测试架构读取。</summary>
            internal static int sInitCount;

            /// <summary>首次进入时重入所属架构，其余层直接返回以限定递归深度。</summary>
            protected override void OnInit()
            {
                int count = Interlocked.Increment(ref sInitCount);
                if (count == 1)
                {
                    _ = ReentrancedByServiceArchitecture.Interface;
                }
            }
        }
    }
}
