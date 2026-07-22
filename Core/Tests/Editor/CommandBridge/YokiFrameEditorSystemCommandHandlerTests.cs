using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Editor System handler 的 action 声明，避免策略放行后宿主 handler 缺失。
    /// </summary>
    public sealed class YokiFrameEditorSystemCommandHandlerTests
    {
        private const string EDITOR_SYSTEM_HANDLER_TYPE = "EditorSystemCommandHandler";

        /// <summary>
        /// 验证 Editor System handler 声明支持打开项目目录，但不实际触发系统窗口。
        /// </summary>
        [Test]
        public void CanHandleOpenProjectFolderWithoutExecutingWindowAction()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            Assert.IsTrue(handler.CanHandle(CreateSystemRequest("open_project_folder")));
        }

        /// <summary>
        /// 验证 Editor System handler 声明支持打开日志，但不实际触发系统窗口。
        /// </summary>
        [Test]
        public void CanHandleOpenLogWithoutExecutingWindowAction()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            Assert.IsTrue(handler.CanHandle(CreateSystemRequest("open_log")));
        }

        /// <summary>验证 Editor System handler 声明支持显式源码定位。</summary>
        [Test]
        public void CanHandleOpenCodeLocationWithoutExecutingEditorAction()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            Assert.IsTrue(handler.CanHandle(CreateSystemRequest("open_code_location")));
        }

        /// <summary>
        /// 验证 Editor System handler 声明支持读取命令目录，供 Workbench 复刻旧 Tauri 动态命令面板。
        /// </summary>
        [Test]
        public void CanHandleListCommands()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            Assert.IsTrue(handler.CanHandle(CreateSystemRequest("list_commands")));
        }

        /// <summary>
        /// 验证打开项目目录命令会调用注入的路径揭示动作，并返回成功终态 JSON。
        /// </summary>
        [Test]
        public void HandleOpenProjectFolderRevealsProjectRootWithoutLaunchingWindow()
        {
            string revealedPath = string.Empty;
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler(path => revealedPath = path);

            YokiFrameCommandResult result = handler.Handle(CreateSystemRequest("open_project_folder"));
            YokiFrameEditorOpenPathResult payload = YokiFrameEditorFileBridgeJson.FromJson<YokiFrameEditorOpenPathResult>(result.ResultJson);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(NormalizePath(YokiFrameEditorFileBridgePaths.GetProjectRoot()), NormalizePath(revealedPath));
            Assert.AreEqual("open_project_folder", payload.action);
            Assert.AreEqual(NormalizeJsonPath(revealedPath), payload.path);
            Assert.IsTrue(payload.opened);
        }

        /// <summary>
        /// 验证打开日志命令会调用注入的路径揭示动作，并返回当前 Unity console log 路径。
        /// </summary>
        [Test]
        public void HandleOpenLogRevealsConsoleLogWithoutLaunchingWindow()
        {
            string revealedPath = string.Empty;
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler(path => revealedPath = path);

            YokiFrameCommandResult result = handler.Handle(CreateSystemRequest("open_log"));
            YokiFrameEditorOpenPathResult payload = YokiFrameEditorFileBridgeJson.FromJson<YokiFrameEditorOpenPathResult>(result.ResultJson);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(NormalizePath(Application.consoleLogPath), NormalizePath(revealedPath));
            Assert.AreEqual("open_log", payload.action);
            Assert.AreEqual(NormalizeJsonPath(revealedPath), payload.path);
            Assert.IsTrue(payload.opened);
        }

        /// <summary>验证源码定位只接受 Assets 内现有 C# 文件，并向注入编辑器传递一基行号。</summary>
        [Test]
        public void HandleOpenCodeLocationUsesValidatedProjectFileAndLine()
        {
            string openedPath = string.Empty;
            int openedLine = 0;
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler(
                _ => { },
                (path, line) =>
                {
                    openedPath = path;
                    openedLine = line;
                    return true;
                });
            var request = new YokiFrameCommandRequest(
                "workbench",
                "System",
                "open_code_location",
                "{\"filePath\":\"Assets/Scripts/EventKitRuntimeSmoke/EventKitRuntimeSmokeController.cs\",\"line\":66}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);

            YokiFrameCommandResult result = handler.Handle(request);
            YokiFrameEditorOpenCodeLocationResult payload =
                YokiFrameEditorFileBridgeJson.FromJson<YokiFrameEditorOpenCodeLocationResult>(result.ResultJson);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(66, openedLine);
            Assert.AreEqual(NormalizePath(Path.Combine(
                YokiFrameEditorFileBridgePaths.GetProjectRoot(),
                payload.filePath)), NormalizePath(openedPath));
            Assert.IsTrue(payload.opened);
        }

        /// <summary>验证源码定位在调用编辑器前拒绝目录穿越。</summary>
        [Test]
        public void HandleOpenCodeLocationRejectsTraversalBeforeEditorAction()
        {
            bool opened = false;
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler(
                _ => { },
                (_, _) =>
                {
                    opened = true;
                    return true;
                });
            var request = new YokiFrameCommandRequest(
                "workbench",
                "System",
                "open_code_location",
                "{\"filePath\":\"../outside.cs\",\"line\":1}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);

            YokiFrameCommandResult result = handler.Handle(request);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(opened);
        }

        /// <summary>
        /// 验证命令目录只包含当前 System/Validation 能力，不恢复已删除的通用 Scene 自动化。
        /// </summary>
        [Test]
        public void HandleListCommandsReturnsSystemCatalog()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            YokiFrameCommandResult result = handler.Handle(CreateSystemRequest("list_commands"));
            TestCommandCatalogResult payload = YokiFrameEditorFileBridgeJson.FromJson<TestCommandCatalogResult>(result.ResultJson);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(ContainsAction(payload, "System", "ping"));
            Assert.IsTrue(ContainsAction(payload, "System", "bridge_status"));
            Assert.IsTrue(ContainsAction(payload, "System", "list_commands"));
            Assert.IsTrue(ContainsAction(payload, "System", "open_code_location"));
            Assert.IsTrue(ContainsAction(payload, "Validation", "inspect_status"));
            Assert.IsFalse(ContainsAction(payload, "Scene", "list"));
            Assert.IsFalse(ContainsAction(payload, "Scene", "inspect"));
        }

        /// <summary>
        /// 验证 bridge_status 命令结果包含 Workbench 诊断卡片需要的协议存储、背压和最近错误字段。
        /// </summary>
        [Test]
        public void HandleBridgeStatusReturnsDiagnosticsFields()
        {
            IYokiFrameCommandHandler handler = CreateEditorSystemHandler();

            YokiFrameCommandResult result = handler.Handle(CreateSystemRequest("bridge_status"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.ResultJson.Contains("\"protocolFileCount\""));
            Assert.IsTrue(result.ResultJson.Contains("\"protocolBytes\""));
            Assert.IsTrue(result.ResultJson.Contains("\"oldestProtocolFileUtc\""));
            Assert.IsTrue(result.ResultJson.Contains("\"backpressureActive\""));
            Assert.IsTrue(result.ResultJson.Contains("\"lastPollLimitReason\""));
            Assert.IsTrue(result.ResultJson.Contains("\"bridgeBusyCount\""));
            Assert.IsTrue(result.ResultJson.Contains("\"lastError\""));
        }

        /// <summary>
        /// 通过反射创建 Editor pump 的私有 System handler，只检查 action 路由声明而不调用 Handle。
        /// </summary>
        /// <returns>System 命令 handler。</returns>
        private static IYokiFrameCommandHandler CreateEditorSystemHandler()
        {
            return CreateEditorSystemHandler(_ => { });
        }

        /// <summary>
        /// 创建注入路径揭示动作且带 Unity 诊断扩展策略的 System handler，用于验证生产命令目录。
        /// </summary>
        /// <param name="revealPath">记录待打开路径的委托。</param>
        /// <returns>System 命令 handler。</returns>
        private static IYokiFrameCommandHandler CreateEditorSystemHandler(Action<string> revealPath)
        {
            return CreateEditorSystemHandler(revealPath, (_, _) => true);
        }

        /// <summary>创建同时注入路径揭示和源码定位动作的 System handler。</summary>
        /// <param name="revealPath">记录待打开路径的委托。</param>
        /// <param name="openCodeLocation">记录源码文件和行号的委托。</param>
        /// <returns>System 命令 handler。</returns>
        private static IYokiFrameCommandHandler CreateEditorSystemHandler(
            Action<string> revealPath,
            Func<string, int, bool> openCodeLocation)
        {
            Type handlerType = typeof(YokiFrameEditorFileBridgePump).GetNestedType(EDITOR_SYSTEM_HANDLER_TYPE, BindingFlags.NonPublic);
            Assert.IsNotNull(handlerType);
            return (IYokiFrameCommandHandler)Activator.CreateInstance(
                handlerType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    revealPath,
                    openCodeLocation,
                    CreateEditorPolicy()
                },
                null);
        }

        /// <summary>创建包含 Unity Harness 与源码定位 UserAction 的测试策略。</summary>
        /// <returns>与生产 Editor 命令目录一致的策略。</returns>
        private static YokiFrameCommandPolicy CreateEditorPolicy()
        {
            YokiFrameCommandDescriptor[] harness =
                YokiFrameUnityHarnessObservationCommandHandler.CreateCommandDescriptors();
            var commands = new YokiFrameCommandDescriptor[harness.Length + 1];
            Array.Copy(harness, commands, harness.Length);
            commands[commands.Length - 1] = new YokiFrameCommandDescriptor(
                "System",
                "open_code_location",
                YokiFrameCommandKind.UserAction);
            return YokiFrameCommandPolicy.CreateDefault(commands);
        }

        /// <summary>
        /// 创建用于 CanHandle 判定的 System 命令请求。
        /// </summary>
        /// <param name="action">System action 标识。</param>
        /// <returns>命令请求。</returns>
        private static YokiFrameCommandRequest CreateSystemRequest(string action)
        {
            return new YokiFrameCommandRequest(
                "cli",
                "System",
                action,
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);
        }

        /// <summary>
        /// 判断命令目录中是否包含指定 Kit/action，避免测试依赖数组顺序。
        /// </summary>
        /// <param name="catalog">命令目录结果。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <returns>包含时返回 true。</returns>
        private static bool ContainsAction(TestCommandCatalogResult catalog, string kit, string action)
        {
            if (catalog == null || catalog.kits == null)
            {
                return false;
            }

            foreach (var kitEntry in catalog.kits)
            {
                if (kitEntry == null || kitEntry.actions == null)
                {
                    continue;
                }

                if (kitEntry.kit != kit)
                {
                    continue;
                }

                foreach (var actionEntry in kitEntry.actions)
                {
                    if (actionEntry.action == action)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 测试专用命令目录 DTO，只关心 Workbench 绑定需要的 Kit/action 字段。
        /// </summary>
        [Serializable]
        private sealed class TestCommandCatalogResult
        {
            public TestCommandCatalogKit[] kits = Array.Empty<TestCommandCatalogKit>();
        }

        /// <summary>
        /// 测试专用 Kit 命令分组 DTO。
        /// </summary>
        [Serializable]
        private sealed class TestCommandCatalogKit
        {
            public string kit = string.Empty;
            public TestCommandCatalogAction[] actions = Array.Empty<TestCommandCatalogAction>();
        }

        /// <summary>
        /// 测试专用 action DTO。
        /// </summary>
        [Serializable]
        private sealed class TestCommandCatalogAction
        {
            public string action = string.Empty;
        }

        /// <summary>
        /// 归一化文件系统路径，避免 Windows 分隔符差异影响断言。
        /// </summary>
        /// <param name="path">待归一化路径。</param>
        /// <returns>绝对路径。</returns>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// 归一化返回 JSON 中的路径格式，保持与命令结果一致。
        /// </summary>
        /// <param name="path">待归一化路径。</param>
        /// <returns>使用正斜杠的绝对路径。</returns>
        private static string NormalizeJsonPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
    }
}
