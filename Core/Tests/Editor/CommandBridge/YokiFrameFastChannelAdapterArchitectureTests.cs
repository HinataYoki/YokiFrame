using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity FastChannel listener 保持 Windows 宿主边界、后台入队和主线程 dispatcher 约束。
    /// </summary>
    public sealed class YokiFrameFastChannelAdapterArchitectureTests
    {
        /// <summary>
        /// 验证 Named Pipe listener 位于 Unity Editor Adapter、使用整文件 Windows 宏，并通过共享请求队列交给主线程。
        /// </summary>
        [Test]
        public void UnityNamedPipeListenerUsesHostGuardAndRequestQueue()
        {
            string source = ReadPackageSource("Core/Adapters/Unity/Editor/FastChannel/YokiFrameEditorNamedPipeFastChannelHost.cs");

            Assert.IsTrue(source.TrimStart().StartsWith("#if UNITY_EDITOR_WIN"));
            Assert.IsTrue(source.TrimEnd().EndsWith("#endif"));
            Assert.IsTrue(source.Contains("NamedPipeServerStream"));
            Assert.IsTrue(source.Contains("YokiFrameFastChannelFrameStream"));
            Assert.IsTrue(source.Contains("YokiFrameFastChannelRequestQueue"));
            Assert.IsTrue(source.Contains("YokiFrameEditorNamedPipeSecurity.CreateServer"));
            Assert.IsFalse(source.Contains("YokiFrameCommandDispatcher"));
        }

        /// <summary>
        /// 验证 Pump 在 Editor 主线程 drain FastChannel 队列，并在 PlayMode、Assembly Reload 与退出时轮换或清理 listener。
        /// </summary>
        [Test]
        public void UnityPumpOwnsFastChannelLifecycleAndMainThreadDispatch()
        {
            string pumpSource = ReadPackageSource("Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.cs");
            string publishingSource = ReadPackageSource(
                "Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.Publishing.cs");
            string fastChannelSource = ReadPackageSource("Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.FastChannel.cs");

            Assert.IsTrue(pumpSource.Contains("ProcessFastChannelRequestsSafely"));
            Assert.IsTrue(pumpSource.Contains("WriteChangedKitInteractionTelemetrySafely"));
            Assert.IsTrue(publishingSource.Contains("IYokiFrameVersionedKitInteractionProvider"));
            Assert.IsTrue(publishingSource.Contains("IYokiFrameSnapshotVersionedKitInteractionProvider"));
            Assert.IsTrue(publishingSource.Contains("WriteChangedSnapshots"));
            Assert.IsFalse(publishingSource.Contains("WriteChangedFallbackSnapshots"));
            Assert.IsTrue(fastChannelSource.Contains("EditorApplication.playModeStateChanged"));
            Assert.IsTrue(fastChannelSource.Contains("AssemblyReloadEvents.beforeAssemblyReload"));
            Assert.IsTrue(fastChannelSource.Contains("SessionState.SetBool(FAST_CHANNEL_TRANSITION_PENDING_KEY, true)"));
            Assert.IsTrue(pumpSource.Contains("IsFastChannelTransitionPending()"));
            Assert.IsTrue(fastChannelSource.Contains("host.ProcessPending"));
            Assert.IsTrue(fastChannelSource.Contains("ExecuteCommand("));
            Assert.IsTrue(pumpSource.Contains("sCommandDispatcher.Dispatch"));
            Assert.IsTrue(fastChannelSource.Contains("CreateFastChannelEndpoints"));
        }

        /// <summary>
        /// 验证 AssetImportWorker 不会注册项目级 Host，且 owner gate 早于任何生命周期或文件写入。
        /// </summary>
        [Test]
        public void AssetImportWorkerCannotOwnProjectFileBridge()
        {
            string source = ReadPackageSource(
                "Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.cs");
            int gateIndex = source.IndexOf(
                "ShouldOwnBridge(AssetDatabase.IsAssetImportWorkerProcess())",
                System.StringComparison.Ordinal);
            int lifecycleIndex = source.IndexOf(
                "YokiFrameEditorTelemetryWriter.RegisterLifecycleHooks()",
                System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gateIndex, 0);
            Assert.Greater(lifecycleIndex, gateIndex);

            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            System.Type pumpType = assembly.GetType("YokiFrame.YokiFrameEditorFileBridgePump");
            MethodInfo shouldOwnBridge = pumpType.GetMethod(
                "ShouldOwnBridge",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsTrue((bool)shouldOwnBridge.Invoke(null, new object[] { false }));
            Assert.IsFalse((bool)shouldOwnBridge.Invoke(null, new object[] { true }));
        }

        /// <summary>验证磁盘 registry 与 heartbeat 都由当前主 Editor Pump 身份发布。</summary>
        [Test]
        public void MainEditorPumpIdentityMatchesPublishedFiles()
        {
            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            System.Type pumpType = assembly.GetType("YokiFrame.YokiFrameEditorFileBridgePump");
            const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.NonPublic;
            string sessionId = (string)pumpType.GetField("sSessionId", FLAGS).GetValue(null);
            long generation = (long)pumpType.GetField("sGeneration", FLAGS).GetValue(null);

            string registryJson = File.ReadAllText(YokiFrameEditorFileBridgePaths.GetEngineRegistryPath());
            string heartbeatJson = File.ReadAllText(YokiFrameEditorFileBridgePaths.GetHeartbeatPath());
            var registry = JsonUtility.FromJson<YokiFrameEditorEngineRegistry>(registryJson);
            var heartbeat = JsonUtility.FromJson<YokiFrameEditorHeartbeat>(heartbeatJson);

            Assert.AreEqual(sessionId, registry.sessionId, "engine.json 不是当前主 Editor Pump 身份。");
            Assert.AreEqual(generation, registry.generation, "engine.json generation 已被其它进程覆盖。");
            Assert.AreEqual(sessionId, heartbeat.sessionId, "heartbeat 不是当前主 Editor Pump 身份。");
            Assert.AreEqual(generation, heartbeat.generation, "heartbeat generation 已被其它进程覆盖。");
        }

        /// <summary>
        /// 验证 Unity FastChannel 复用 Core 项目作用域，不保留未归一化且碰撞空间较小的本地 FNV32 实现。
        /// </summary>
        [Test]
        public void UnityFastChannelUsesSharedProjectScopeIdentity()
        {
            string source = ReadPackageSource(
                "Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.FastChannel.cs");

            Assert.IsTrue(source.Contains("YokiFrameSharedMemoryTelemetryProjectScopeId.Compute"));
            Assert.IsFalse(source.Contains("ComputeProjectScopeHash"));
            Assert.IsFalse(source.Contains("2166136261u"));
        }

        /// <summary>
        /// 验证 Unity 当前编译出的 Named Pipe 名称包含项目 scope，且不会把项目绝对路径写入系统 endpoint。
        /// </summary>
        [Test]
        public void UnityFastChannelPipeNameContainsHashedProjectScopeOnly()
        {
            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            System.Type pumpType = assembly.GetType("YokiFrame.YokiFrameEditorFileBridgePump");
            MethodInfo createPipeName = pumpType.GetMethod(
                "CreateFastChannelPipeName",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipeName = (string)createPipeName.Invoke(null, null);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);

            StringAssert.Contains(projectScopeId, pipeName);
            StringAssert.DoesNotContain(projectRoot, pipeName);
        }

        /// <summary>
        /// 验证 Editor 周期保活只写 heartbeat，不会重新提交 registry 与全部 snapshot。
        /// </summary>
        [Test]
        public void UnityPeriodicUpdateUsesHeartbeatOnlyFilePublishing()
        {
            string source = ReadPackageSource(
                "Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgePump.cs");
            int methodStart = source.IndexOf("private static void OnEditorUpdate()", System.StringComparison.Ordinal);
            int methodEnd = source.IndexOf(
                "private static void RefreshToolKitInteractions()",
                methodStart,
                System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            Assert.Greater(methodEnd, methodStart);
            string methodSource = source.Substring(methodStart, methodEnd - methodStart);

            Assert.IsTrue(methodSource.Contains("RefreshToolKitInteractions()"));
            Assert.IsTrue(methodSource.Contains("WriteHeartbeatStateSafely()"));
            Assert.IsFalse(methodSource.Contains("WriteCompleteBridgeStateSafely()"));
        }

        /// <summary>
        /// 验证 Unity registry 模型显式发布 FastChannel endpoint，而不是把连接信息藏入未建模扩展字段。
        /// </summary>
        [Test]
        public void UnityRegistryModelDeclaresFastChannelEndpoints()
        {
            string source = ReadPackageSource("Core/Adapters/Unity/Editor/FileBridge/YokiFrameEditorFileBridgeModels.cs");

            Assert.IsTrue(source.Contains("YokiFrameEditorFastChannelEndpoint[] fastChannels"));
            Assert.IsTrue(source.Contains("class YokiFrameEditorFastChannelEndpoint"));
        }

        /// <summary>
        /// 验证 Windows ACL 工厂通过当前进程 token SID 和受保护 DACL 创建 Pipe，不会退回 Mono 不支持的 CurrentUserOnly 或默认访问控制。
        /// </summary>
        [Test]
        public void UnityNamedPipeSecurityUsesProtectedCurrentUserAcl()
        {
            string source = ReadPackageSource("Core/Adapters/Unity/Editor/FastChannel/YokiFrameEditorNamedPipeSecurity.cs");

            Assert.IsTrue(source.TrimStart().StartsWith("#if UNITY_EDITOR_WIN"));
            Assert.IsTrue(source.TrimEnd().EndsWith("#endif"));
            Assert.IsTrue(source.Contains("OpenProcessToken"));
            Assert.IsTrue(source.Contains("GetTokenInformation"));
            Assert.IsTrue(source.Contains("SetAccessRuleProtection(true, false)"));
            Assert.IsTrue(source.Contains("PipeAccessRights.FullControl"));
            Assert.IsTrue(source.Contains("HandleInheritability.None"));
            Assert.IsFalse(source.Contains("PipeOptions.CurrentUserOnly"));
        }

        /// <summary>
        /// 验证 Windows Editor 实际编入 Named Pipe host 类型，避免仅靠源码宏文本误判当前平台可执行性。
        /// </summary>
        [Test]
        public void WindowsEditorAssemblyContainsNamedPipeFastChannelHost()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，Named Pipe host 类型不应参与本平台断言。");
            }

            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            Assert.IsNotNull(assembly.GetType("YokiFrame.YokiFrameEditorNamedPipeFastChannelHost"));
        }

        /// <summary>
        /// 验证当前 Windows Editor listener 实际达到 ready 状态；失败时输出 Host 记录的启动原因。
        /// </summary>
        [Test]
        public void WindowsEditorNamedPipeFastChannelHostIsReady()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("当前不是 Windows Unity Editor，Named Pipe listener 不应启动。");
            }

            Assembly assembly = Assembly.Load("YokiFrame.Unity.Editor");
            System.Type pumpType = assembly.GetType("YokiFrame.YokiFrameEditorFileBridgePump");
            FieldInfo hostField = pumpType.GetField("sFastChannelHost", BindingFlags.Static | BindingFlags.NonPublic);
            object host = hostField.GetValue(null);
            FieldInfo startErrorField = pumpType.GetField("sFastChannelStartError", BindingFlags.Static | BindingFlags.NonPublic);
            string startError = (string)startErrorField.GetValue(null);
            Assert.IsNotNull(host, "Windows Editor 应创建 FastChannel Named Pipe Host: " + startError);

            System.Type hostType = host.GetType();
            bool isReady = (bool)hostType.GetProperty("IsReady").GetValue(host, null);
            string lastError = (string)hostType.GetProperty("LastError").GetValue(host, null);
            Assert.IsTrue(isReady, "FastChannel listener 未 ready: " + lastError);
        }

        /// <summary>
        /// 从当前 Unity 项目的包根读取待验证源码。
        /// </summary>
        /// <param name="relativePath">相对于 Assets/YokiFrame 的源码路径。</param>
        /// <returns>完整源码文本。</returns>
        private static string ReadPackageSource(string relativePath)
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string sourcePath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(sourcePath), "FastChannel Adapter 缺少源码: " + sourcePath);
            return File.ReadAllText(sourcePath);
        }
    }
}
