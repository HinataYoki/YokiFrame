using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Architecture 的架构单例、服务生命周期和诊断快照行为。
    /// </summary>
    public sealed class YokiFrameArchitectureRuntimeTests
    {
        private List<IArchitecture> mCreatedArchitectures;

        /// <summary>
        /// 每个测试前清理诊断状态和测试计数器，避免静态状态污染断言。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            mCreatedArchitectures = new List<IArchitecture>();
            ArchitectureRegistry.Clear();
            LifecycleArchitecture.ResetCounters();
            CountingService.ResetCounters();
            CountingModel.ResetCounters();
            ReplacementService.ResetCounters();
            ForcedService.ResetCounters();
            FirstCleanupFailureService.ResetCounters();
            SecondCleanupFailureService.ResetCounters();
        }

        /// <summary>
        /// 每个测试后释放已创建的测试架构，确保重复运行测试时不会复用旧实例。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            DisposeCreatedArchitectures();
            ArchitectureRegistry.Clear();
        }

        /// <summary>
        /// 验证首次访问 Interface 会创建类型级单例，执行 OnInit，并初始化已注册服务。
        /// </summary>
        [Test]
        public void InterfaceCreatesSingletonAndInitializesRegisteredServicesOnce()
        {
            IArchitecture first = GetArchitecture<LifecycleArchitecture>();
            IArchitecture second = GetArchitecture<LifecycleArchitecture>();
            CountingService service = first.GetService<CountingService>();
            CountingModel model = first.GetService<CountingModel>();

            Assert.AreSame(first, second);
            Assert.IsTrue(first.Initialized);
            Assert.AreEqual(1, LifecycleArchitecture.InitCount);
            Assert.AreEqual(1, CountingService.InitCount);
            Assert.AreEqual(1, CountingModel.InitCount);
            Assert.AreSame(first, service.Architecture);
            Assert.AreSame(model, service.ModelDuringInit);
            Assert.IsTrue(service.Initialized);
            Assert.IsTrue(model.Initialized);
        }

        /// <summary>
        /// 验证重复注册同一服务类型会释放旧实例，并只保留最后注册的服务。
        /// </summary>
        [Test]
        public void RegisterReplacesExistingServiceAndDisposesOldService()
        {
            IArchitecture architecture = GetArchitecture<ReplacementArchitecture>();
            ReplacementService service = architecture.GetService<ReplacementService>();

            Assert.IsNotNull(service);
            Assert.AreEqual(2, ReplacementService.CreatedCount);
            Assert.AreEqual(1, ReplacementService.DisposedCount);
            Assert.AreEqual(2, service.Id);
            Assert.AreEqual(1, service.InitCount);
        }

        /// <summary>
        /// 验证 force 获取未注册服务时会创建、注册并初始化服务。
        /// </summary>
        [Test]
        public void GetServiceForceCreatesRegistersAndInitializesMissingService()
        {
            IArchitecture architecture = GetArchitecture<EmptyArchitecture>();
            ForcedService service = architecture.GetService<ForcedService>(true);

            Assert.IsNotNull(service);
            Assert.AreSame(service, architecture.GetService<ForcedService>());
            Assert.AreSame(architecture, service.Architecture);
            Assert.IsTrue(service.Initialized);
            Assert.AreEqual(1, ForcedService.InitCount);
        }

        /// <summary>
        /// 验证普通获取未注册服务时返回 null，不偷偷创建生命周期不清的服务。
        /// </summary>
        [Test]
        public void GetServiceWithoutForceReturnsNullWhenServiceMissing()
        {
            IArchitecture architecture = GetArchitecture<EmptyArchitecture>();

            Assert.IsNull(architecture.GetService<ForcedService>());
            Assert.AreEqual(0, ForcedService.InitCount);
        }

        /// <summary>
        /// 验证架构释放会继续处理其它服务，并在全部清理后汇总多个异常。
        /// </summary>
        [Test]
        public void DisposeContinuesAfterIndividualServiceFailures()
        {
            IArchitecture architecture = GetArchitecture<CleanupFailureArchitecture>();

            Assert.Throws<AggregateException>(() => architecture.Dispose());
            Assert.AreEqual(1, FirstCleanupFailureService.DisposeCount);
            Assert.AreEqual(1, SecondCleanupFailureService.DisposeCount);
            Assert.IsFalse(architecture.Initialized);
        }

        /// <summary>
        /// 验证诊断注册表记录架构和服务状态，并且返回的是可安全修改的副本。
        /// </summary>
        [Test]
        public void RegistryReportsArchitectureAndReturnsCopies()
        {
            IArchitecture architecture = GetArchitecture<RegistryArchitecture>();
            var infos = new List<ArchitectureDebugInfo>();

            ArchitectureRegistry.GetAll(infos);

            ArchitectureDebugInfo info = FindArchitecture(infos, typeof(RegistryArchitecture));
            Assert.IsNotNull(info);
            Assert.AreEqual(typeof(RegistryArchitecture).Name, info.TypeName);
            Assert.AreEqual(typeof(RegistryArchitecture).FullName, info.FullName);
            Assert.AreEqual(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(architecture), info.InstanceHash);
            Assert.IsTrue(info.IsAlive);
            Assert.IsTrue(info.Initialized);
            Assert.AreEqual(1, info.ServiceCount);
            Assert.AreEqual(typeof(CountingService).Name, info.Services[0].TypeName);
            Assert.AreEqual(typeof(CountingService).FullName, info.Services[0].FullName);
            Assert.AreEqual(typeof(CountingService).Name, info.Services[0].ImplementationTypeName);
            Assert.IsTrue(info.Services[0].Initialized);

            info.Services.Clear();
            ArchitectureRegistry.GetAll(infos);

            ArchitectureDebugInfo copiedAgain = FindArchitecture(infos, typeof(RegistryArchitecture));
            Assert.AreEqual(1, copiedAgain.Services.Count);

            architecture.Dispose();
            ArchitectureRegistry.GetAll(infos);

            ArchitectureDebugInfo disposedInfo = FindArchitecture(infos, typeof(RegistryArchitecture));
            Assert.IsFalse(disposedInfo.IsAlive);
        }

        /// <summary>
        /// 获取测试架构实例，并记录到测试结束时需要释放的列表中。
        /// </summary>
        /// <typeparam name="T">测试架构类型。</typeparam>
        /// <returns>测试架构实例。</returns>
        private IArchitecture GetArchitecture<T>() where T : Architecture<T>, new()
        {
            IArchitecture architecture = Architecture<T>.Interface;
            if (!ContainsCreatedArchitecture(architecture))
            {
                mCreatedArchitectures.Add(architecture);
            }

            return architecture;
        }

        /// <summary>
        /// 判断指定架构是否已经记录到当前测试的释放列表中。
        /// </summary>
        /// <param name="architecture">待检查架构。</param>
        /// <returns>已记录时返回 true。</returns>
        private bool ContainsCreatedArchitecture(IArchitecture architecture)
        {
            for (var index = 0; index < mCreatedArchitectures.Count; index++)
            {
                if (ReferenceEquals(mCreatedArchitectures[index], architecture))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 释放当前测试创建过的架构实例，避免泛型静态单例污染后续测试。
        /// </summary>
        private void DisposeCreatedArchitectures()
        {
            for (var index = 0; index < mCreatedArchitectures.Count; index++)
            {
                mCreatedArchitectures[index].Dispose();
            }

            mCreatedArchitectures.Clear();
        }

        /// <summary>
        /// 从诊断列表中查找指定架构类型的记录。
        /// </summary>
        /// <param name="infos">诊断列表。</param>
        /// <param name="architectureType">架构类型。</param>
        /// <returns>匹配记录；未找到时返回 null。</returns>
        private static ArchitectureDebugInfo FindArchitecture(List<ArchitectureDebugInfo> infos, Type architectureType)
        {
            for (var index = 0; index < infos.Count; index++)
            {
                ArchitectureDebugInfo info = infos[index];
                if (info.FullName == architectureType.FullName)
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// 测试用基础架构，注册一个服务和一个模型。
        /// </summary>
        public sealed class LifecycleArchitecture : Architecture<LifecycleArchitecture>
        {
            public static int InitCount;

            /// <summary>
            /// 注册测试服务和模型。
            /// </summary>
            protected override void OnInit()
            {
                InitCount++;
                Register(new CountingModel());
                Register(new CountingService());
            }

            /// <summary>
            /// 重置静态计数器。
            /// </summary>
            public static void ResetCounters()
            {
                InitCount = 0;
            }
        }

        /// <summary>
        /// 测试用替换架构，连续注册同一服务类型。
        /// </summary>
        public sealed class ReplacementArchitecture : Architecture<ReplacementArchitecture>
        {
            /// <summary>
            /// 注册两个同类型服务，用于验证旧实例释放。
            /// </summary>
            protected override void OnInit()
            {
                Register(new ReplacementService());
                Register(new ReplacementService());
            }
        }

        /// <summary>
        /// 测试用空架构，不主动注册任何服务。
        /// </summary>
        public sealed class EmptyArchitecture : Architecture<EmptyArchitecture>
        {
            /// <summary>
            /// 保持空初始化，用于验证 GetService 的缺省和 force 行为。
            /// </summary>
            protected override void OnInit()
            {
            }
        }

        /// <summary>
        /// 测试用诊断架构，只注册一个服务。
        /// </summary>
        public sealed class RegistryArchitecture : Architecture<RegistryArchitecture>
        {
            /// <summary>
            /// 注册一个服务，用于验证诊断快照。
            /// </summary>
            protected override void OnInit()
            {
                Register(new CountingService());
            }
        }

        /// <summary>
        /// 测试用服务，初始化时尝试解析同架构内的模型。
        /// </summary>
        public sealed class CountingService : AbstractService
        {
            public static int InitCount;

            /// <summary>
            /// 获取初始化期间解析到的模型。
            /// </summary>
            public CountingModel ModelDuringInit { get; private set; }

            /// <summary>
            /// 初始化服务并解析模型依赖。
            /// </summary>
            protected override void OnInit()
            {
                InitCount++;
                ModelDuringInit = GetService<CountingModel>();
            }

            /// <summary>
            /// 重置静态计数器。
            /// </summary>
            public static void ResetCounters()
            {
                InitCount = 0;
            }
        }

        /// <summary>
        /// 测试用模型，实现旧版 IModel 的序列化契约。
        /// </summary>
        public sealed class CountingModel : AbstractModel
        {
            public static int InitCount;

            /// <summary>
            /// 初始化模型并记录次数。
            /// </summary>
            protected override void OnInit()
            {
                InitCount++;
            }

            /// <summary>
            /// 写入测试模型序列化数据；本测试只验证契约存在。
            /// </summary>
            /// <param name="info">序列化信息容器。</param>
            /// <param name="context">序列化上下文。</param>
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("initCount", InitCount);
            }

            /// <summary>
            /// 重置静态计数器。
            /// </summary>
            public static void ResetCounters()
            {
                InitCount = 0;
            }
        }

        /// <summary>
        /// 测试用可替换服务，记录创建、初始化和释放次数。
        /// </summary>
        public sealed class ReplacementService : AbstractService
        {
            public static int CreatedCount;
            public static int DisposedCount;

            /// <summary>
            /// 创建服务并分配递增编号。
            /// </summary>
            public ReplacementService()
            {
                Id = ++CreatedCount;
            }

            /// <summary>
            /// 获取服务实例编号。
            /// </summary>
            public int Id { get; private set; }

            /// <summary>
            /// 获取当前实例初始化次数。
            /// </summary>
            public int InitCount { get; private set; }

            /// <summary>
            /// 初始化当前服务实例。
            /// </summary>
            protected override void OnInit()
            {
                InitCount++;
            }

            /// <summary>
            /// 释放当前服务实例。
            /// </summary>
            protected override void OnDispose()
            {
                DisposedCount++;
            }

            /// <summary>
            /// 重置静态计数器。
            /// </summary>
            public static void ResetCounters()
            {
                CreatedCount = 0;
                DisposedCount = 0;
            }
        }

        /// <summary>
        /// 测试用 force 创建服务。
        /// </summary>
        public sealed class ForcedService : AbstractService
        {
            public static int InitCount;

            /// <summary>
            /// 初始化 force 创建的服务。
            /// </summary>
            protected override void OnInit()
            {
                InitCount++;
            }

            /// <summary>
            /// 重置静态计数器。
            /// </summary>
            public static void ResetCounters()
            {
                InitCount = 0;
            }
        }

        /// <summary>注册两个会在释放时失败的服务，用于验证架构清理的 best-effort 语义。</summary>
        public sealed class CleanupFailureArchitecture : Architecture<CleanupFailureArchitecture>
        {
            /// <summary>注册两个不同类型的故障服务。</summary>
            protected override void OnInit()
            {
                Register(new FirstCleanupFailureService());
                Register(new SecondCleanupFailureService());
            }
        }

        /// <summary>第一个故障释放服务。</summary>
        public sealed class FirstCleanupFailureService : AbstractService
        {
            public static int DisposeCount;

            /// <summary>记录初始化；该服务无额外初始化逻辑。</summary>
            protected override void OnInit()
            {
            }

            /// <summary>记录释放并抛出模拟资源错误。</summary>
            protected override void OnDispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("first cleanup failed");
            }

            /// <summary>重置释放计数。</summary>
            public static void ResetCounters()
            {
                DisposeCount = 0;
            }
        }

        /// <summary>第二个故障释放服务。</summary>
        public sealed class SecondCleanupFailureService : AbstractService
        {
            public static int DisposeCount;

            /// <summary>记录初始化；该服务无额外初始化逻辑。</summary>
            protected override void OnInit()
            {
            }

            /// <summary>记录释放并抛出模拟资源错误。</summary>
            protected override void OnDispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("second cleanup failed");
            }

            /// <summary>重置释放计数。</summary>
            public static void ResetCounters()
            {
                DisposeCount = 0;
            }
        }
    }
}
