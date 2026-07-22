using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Tooling.Application.Models.Capabilities;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 能力目录 Builder 的可变 Kit 和 engine 中间模型。
/// </summary>
internal sealed partial class CapabilityCatalogBuilder
{
    /// <summary>维护一个 Kit 的声明和实时命令集合。</summary>
    internal sealed class KitBuilder
    {
        private readonly List<CapabilityCatalogCommand> mObservedCommands = new();
        private bool mProjectModelDeclared;
        private bool mProjectModelAvailable;
        private bool mProjectModelTrusted;
        private bool mHarnessDeclaredSnapshot;
        private bool mHarnessDeclaredCommand;

        /// <summary>创建 Kit 累加器。</summary>
        /// <param name="kit">Kit 标识。</param>
        public KitBuilder(string kit)
        {
            Kit = kit;
        }

        /// <summary>获取 Kit 标识。</summary>
        public string Kit { get; }

        /// <summary>获取或设置是否声明 snapshot。</summary>
        public bool DeclaredSnapshot { get; private set; }

        /// <summary>获取或设置是否声明 command。</summary>
        public bool DeclaredCommand { get; private set; }

        /// <summary>获取或设置本次聚合是否成功观测过当前 command catalog。</summary>
        public bool CommandCatalogObserved { get; set; }

        /// <summary>
        /// 应用 Project Model 的 Kit descriptor；正式 descriptor 优先于 bootstrap 声明。
        /// </summary>
        /// <param name="capability">静态 Kit descriptor。</param>
        /// <param name="trusted">Project Model 是否处于 Ready 状态。</param>
        public void ApplyProjectCapability(ProjectCapabilityKit capability, bool trusted)
        {
            mProjectModelDeclared = true;
            mProjectModelAvailable = string.Equals(capability.State, "Available", StringComparison.Ordinal);
            mProjectModelTrusted = trusted;
            DeclaredSnapshot = capability.SnapshotNames.Count > 0;
            DeclaredCommand = capability.CommandCatalogDeclared;
        }

        /// <summary>记录旧 harness 的 snapshot 声明，仅在缺少正式 descriptor 时生效。</summary>
        public void ApplyHarnessSnapshotDeclaration()
        {
            mHarnessDeclaredSnapshot = true;
            if (!mProjectModelDeclared)
            {
                DeclaredSnapshot = true;
            }
        }

        /// <summary>记录旧 harness 的 command 声明，仅在缺少正式 descriptor 时生效。</summary>
        public void ApplyHarnessCommandDeclaration()
        {
            mHarnessDeclaredCommand = true;
            if (!mProjectModelDeclared)
            {
                DeclaredCommand = true;
            }
        }

        /// <summary>加入一条实时观测命令。</summary>
        /// <param name="command">命令能力。</param>
        public void AddObserved(CapabilityCatalogCommand command)
        {
            if (!mObservedCommands.Any(item => item.EngineId == command.EngineId
                && item.Kit == command.Kit
                && item.Action == command.Action))
            {
                mObservedCommands.Add(command);
            }
        }

        /// <summary>转换为稳定排序的公开 Kit 模型。</summary>
        /// <returns>Kit 模型。</returns>
        public CapabilityCatalogKit ToModel()
        {
            var availability = ResolveAvailability();
            return new CapabilityCatalogKit(
                Kit,
                availability,
                DeclaredSnapshot,
                DeclaredCommand,
                mObservedCommands
                    .OrderBy(command => command.EngineId, StringComparer.Ordinal)
                    .ThenBy(command => command.Kit, StringComparer.Ordinal)
                    .ThenBy(command => command.Action, StringComparer.Ordinal)
                    .ToArray(),
                CreateSources());
        }

        /// <summary>创建当前 Kit 的来源标签。</summary>
        /// <returns>来源标签集合。</returns>
        private IReadOnlyList<string> CreateSources()
        {
            List<string> sources = new();
            if (mProjectModelDeclared)
            {
                sources.Add("project-model");
            }

            if (mHarnessDeclaredSnapshot || mHarnessDeclaredCommand)
            {
                sources.Add("harness");
            }

            if (CommandCatalogObserved || mObservedCommands.Count > 0)
            {
                sources.Add("System/list_commands");
            }

            return sources;
        }

        /// <summary>根据正式 descriptor、bootstrap 回退和实时观测计算 Kit 可用性。</summary>
        private string ResolveAvailability()
        {
            if (mProjectModelDeclared)
            {
                if (!mProjectModelTrusted)
                {
                    return "Drifted";
                }

                if (mObservedCommands.Count > 0 && !DeclaredCommand)
                {
                    return "Drifted";
                }

                if (CommandCatalogObserved && DeclaredCommand && mObservedCommands.Count == 0)
                {
                    return "Drifted";
                }

                return mProjectModelAvailable ? "Available" : "Declared";
            }

            return mObservedCommands.Count > 0
                ? DeclaredCommand ? "Available" : "Drifted"
                : CommandCatalogObserved && DeclaredCommand ? "Drifted"
                : DeclaredSnapshot || DeclaredCommand ? "Declared" : "Unavailable";
        }
    }

    /// <summary>维护单个 engine 的可变命令目录，最终转换为只读应用模型。</summary>
    internal sealed class CapabilityCatalogEngineBuilder
    {
        private readonly EngineRegistryEntry mEntry;
        private readonly HeartbeatInfo? mHeartbeat;
        private CapabilityCatalogCommandSet mCommandCatalog = new(
            "NotRequested",
            0L,
            string.Empty,
            Array.Empty<CapabilityCatalogCommand>(),
            Array.Empty<string>());

        /// <summary>创建 engine 累加器。</summary>
        /// <param name="entry">registry 条目。</param>
        /// <param name="heartbeat">heartbeat。</param>
        /// <param name="identityState">身份状态。</param>
        /// <param name="online">是否在线。</param>
        public CapabilityCatalogEngineBuilder(
            EngineRegistryEntry entry,
            HeartbeatInfo? heartbeat,
            string identityState,
            bool online)
        {
            mEntry = entry;
            mHeartbeat = heartbeat;
            IdentityState = identityState;
            Online = online;
        }

        /// <summary>获取 engine 标识。</summary>
        public string EngineId => mEntry.EngineId;

        /// <summary>获取 registry 与 heartbeat 身份状态。</summary>
        public string IdentityState { get; }

        /// <summary>获取 heartbeat 证明的在线状态。</summary>
        public bool Online { get; }

        /// <summary>设置实时命令目录。</summary>
        /// <param name="state">目录状态。</param>
        /// <param name="sequence">目录序号。</param>
        /// <param name="transport">实际传输。</param>
        /// <param name="commands">命令集合。</param>
        /// <param name="evidencePaths">证据路径。</param>
        public void SetCommandCatalog(
            string state,
            long sequence,
            string transport,
            IReadOnlyList<CapabilityCatalogCommand> commands,
            IReadOnlyList<string> evidencePaths)
        {
            mCommandCatalog = new CapabilityCatalogCommandSet(state, sequence, transport, commands, evidencePaths);
        }

        /// <summary>转换为只读公开 engine 模型。</summary>
        /// <returns>engine 模型。</returns>
        public CapabilityCatalogEngine ToModel()
        {
            return new CapabilityCatalogEngine(
                mEntry.EngineId,
                mEntry.Engine,
                mEntry.Version,
                mEntry.AdapterVersion,
                mHeartbeat?.Mode ?? mEntry.Mode,
                mEntry.SessionId,
                mEntry.Generation,
                Online,
                IdentityState,
                mEntry.Capabilities,
                mCommandCatalog);
        }
    }
}
