#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>Owns transient versions; adapters own objects, frame callbacks and persistence.</summary>
    public sealed partial class LiveCodeManager
    {
        private sealed class Entry
        {
            internal LiveCodeHandle Handle;
            internal MethodInfo Original;
            internal MethodInfo Patch;
            internal string Owner;
            internal IDisposable Attachment;
            internal object Target;
            internal string Members;
            internal string ClassName;
        }

        private readonly Dictionary<string, Entry> mEntries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> mPending = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> mRevisions = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly RoslynCompilerLoader mCompiler;
        private readonly RoslynLoadBudget mBudget;
        private readonly MethodPatchBackend mPatches;
        private readonly ILiveCodeHost mHost;
        private readonly Func<RoslynDomainState> mState;
        private readonly Func<bool> mPermitted;
        private readonly int mThreadId = Thread.CurrentThread.ManagedThreadId;
        private int mEpoch;

        /// <summary>绑定编译、预算、补丁和宿主。有项目根才创建快照存储；实例绑定构造时所在线程。</summary>
        /// <param name="compiler">内存编译与程序集加载器。</param>
        /// <param name="budget">已加载程序集预算。</param>
        /// <param name="patches">方法补丁后端。</param>
        /// <param name="host">对象、行为与持久化宿主。</param>
        /// <param name="state">读取当前域会话。</param>
        /// <param name="permitted">是否允许热更。</param>
        /// <param name="projectRoot">项目根；null 时不启用快照存储。</param>
        public LiveCodeManager(RoslynCompilerLoader compiler, RoslynLoadBudget budget,
            MethodPatchBackend patches, ILiveCodeHost host,
            Func<RoslynDomainState> state, Func<bool> permitted, string projectRoot = null)
        {
            mCompiler = compiler;
            mBudget = budget;
            mPatches = patches;
            mHost = host;
            mState = state;
            mPermitted = permitted;
            mSnapshots = projectRoot == null ? null : new LiveSnapshotStore(projectRoot);
        }

        public bool PatchInstalled => mPatches != null && mPatches.Installed;

        /// <summary>列出已登记句柄，并按附件存活刷新状态和预算。须在构造线程调用。</summary>
        /// <returns>当前句柄副本。</returns>
        public IReadOnlyList<LiveCodeHandle> List()
        {
            RequireThread();
            var handles = new List<LiveCodeHandle>(mEntries.Count);
            foreach (Entry entry in mEntries.Values)
            {
                entry.Handle.Status = entry.Attachment == null || mHost.IsAlive(entry.Attachment)
                    ? "active" : "unavailable";
                entry.Handle.Budget = mBudget.ReadStatus();
                handles.Add(entry.Handle);
            }
            return handles;
        }

        /// <summary>编译并安装静态方法补丁。先挂新补丁再卸旧补丁；卸旧失败会卸掉新补丁后重新抛出。操作期间占用标识，结束时释放。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="original">被补丁的托管方法。</param>
        /// <param name="source">补丁源码。</param>
        /// <param name="patchType">补丁类型全名。</param>
        /// <param name="patchMethod">补丁类型上的静态方法名。</param>
        /// <param name="mode">prefix、postfix 或 replace。</param>
        /// <param name="guard">调用前执行的宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>安装后的补丁句柄。</returns>
        public async Task<LiveCodeHandle> Patch(string id, MethodInfo original, string source,
            string patchType, string patchMethod, string mode, Action guard, CancellationToken token)
        {
            ValidatePatchTarget(original);
            if (mode != "prefix" && mode != "postfix" && mode != "replace")
                throw new ArgumentException("Expected prefix, postfix or replace.", nameof(mode));
            if (!PatchInstalled) throw new InvalidOperationException("HarmonyX patch bundle is not installed.");
            var scope = Begin(id, guard);
            int epoch = mEpoch;
            try
            {
                Assembly compiled = await Compile(source, scope, epoch, guard, token);
                Check(scope, epoch, guard);
                MethodInfo patch = compiled.GetType(patchType, true).GetMethod(patchMethod,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (patch == null || patch.ContainsGenericParameters)
                    throw new ArgumentException("A unique static patch method is required.");
                if (mode == "replace" && patch.ReturnType != typeof(bool))
                    throw new ArgumentException("Replacement is a bool prefix: return false to skip the original.");
                mEntries.TryGetValue(id, out Entry previous);
                if (previous != null && previous.Handle.Kind != "patch")
                    throw new InvalidOperationException("The ID belongs to a behaviour.");
                string owner = "YokiFrame.Live." + Guid.NewGuid().ToString("N");
                mPatches.Apply(owner, original, patch, mode);
                try
                {
                    if (previous != null) mPatches.Remove(previous.Owner, previous.Original, previous.Patch);
                }
                catch
                {
                    mPatches.Remove(owner, original, patch);
                    throw;
                }
                var entry = new Entry
                {
                    Handle = MakeHandle(id, "patch", source, scope, previous),
                    Original = original, Patch = patch, Owner = owner
                };
                mEntries[id] = entry;
                mRevisions[id] = entry.Handle.Revision;
                return entry.Handle;
            }
            finally { mPending.Remove(id); }
        }

        /// <summary>编译行为并挂到目标。先准备新附件，再挂起旧附件并激活新附件；激活失败则释放新附件并尽量恢复旧附件。标识不能跨种类或跨对象复用。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="target">挂接对象，不可为 null。</param>
        /// <param name="className">生成的行为类名。</param>
        /// <param name="members">行为成员源码。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>挂接后的行为句柄。</returns>
        public async Task<LiveCodeHandle> Attach(string id, object target, string className,
            string members, Action guard, CancellationToken token)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            ValidateName(className);
            if (mHost == null) throw new NotSupportedException("This engine has no live behaviour adapter.");
            var scope = Begin(id, guard);
            int epoch = mEpoch;
            try
            {
                mEntries.TryGetValue(id, out Entry previous);
                if (previous != null && previous.Handle.Kind != "behaviour")
                    throw new InvalidOperationException("The ID belongs to a patch.");
                if (previous != null && !ReferenceEquals(previous.Target, target))
                    throw new InvalidOperationException("A behaviour ID cannot silently move to another object.");
                Assembly compiled = await Compile(mHost.WrapBehaviour(className, members, false), scope, epoch, guard, token);
                Check(scope, epoch, guard);
                bool previousAlive = previous != null && mHost.IsAlive(previous.Attachment);
                string state = previousAlive ? mHost.CaptureState(previous.Attachment) : string.Empty;
                IDisposable attachment = mHost.Prepare(id, target, compiled.GetType(className, true), state);
                try
                {
                    if (previousAlive) mHost.Suspend(previous.Attachment);
                    mHost.Activate(attachment);
                }
                catch
                {
                    attachment.Dispose();
                    if (previous != null && mHost.IsAlive(previous.Attachment)) mHost.Activate(previous.Attachment);
                    throw;
                }
                if (previous != null) previous.Attachment.Dispose();
                var entry = new Entry
                {
                    Handle = MakeHandle(id, "behaviour", members, scope, previous),
                    Attachment = attachment, Target = target, Members = members, ClassName = className
                };
                mEntries[id] = entry;
                mRevisions[id] = entry.Handle.Revision;
                return entry.Handle;
            }
            finally { mPending.Remove(id); }
        }

        /// <summary>导出活动行为。宿主支持版本导出时走批量暂存并提交，否则只编译持久源码后交给宿主写出。不改内存登记。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="outputPath">目标脚本路径。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>导出标识。</returns>
        public async Task<string> Export(string id, string outputPath, Action guard, CancellationToken token)
        {
            if (mHost is ILiveExportHost)
            {
                var batch = await ExportMany(new[] { new LiveExportRequest(id, outputPath) }, null, guard, token);
                CommitExport(batch.BatchId, guard);
                return batch.ExportIds[0];
            }
            var scope = Begin(id, guard);
            int epoch = mEpoch;
            try
            {
                if (!mEntries.TryGetValue(id, out Entry entry) || entry.Handle.Kind != "behaviour")
                    throw new ArgumentException("Export requires an active behaviour ID.");
                if (!mHost.IsAlive(entry.Attachment))
                    throw new InvalidOperationException("The behaviour is detached or faulted.");
                string source = mHost.WrapBehaviour(entry.ClassName, entry.Members, true);
                await Compile(source, scope, epoch, guard, token, false);
                Check(scope, epoch, guard);
                return mHost.Export(id, entry.ClassName, source, Hash(source), entry.Attachment, outputPath);
            }
            finally { mPending.Remove(id); }
        }

        /// <summary>把已导出行为绑定到目标。无宿主时不支持。</summary>
        /// <param name="exportId">导出标识。</param>
        /// <param name="target">绑定目标，由宿主解释。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <returns>宿主返回的绑定结果。</returns>
        public string Bind(string exportId, object target, Action guard)
        {
            Guard(guard);
            if (mHost == null) throw new NotSupportedException("This engine has no persistent binding adapter.");
            return mHost.Bind(exportId, target);
        }

        /// <summary>卸下补丁或释放行为并移出登记。批量进行中或该标识仍有操作时拒绝；不存在返回 false。</summary>
        /// <param name="id">热更标识。</param>
        /// <returns>是否移除了已有条目。</returns>
        public bool Remove(string id)
        {
            RequireThread();
            if (mRestoring || mBatchAttaching) throw new InvalidOperationException("A live batch is in progress.");
            if (mPending.Contains(id)) throw new InvalidOperationException("The ID has an operation in progress.");
            if (!mEntries.TryGetValue(id, out Entry entry)) return false;
            DisposeEntry(entry);
            mEntries.Remove(id);
            return true;
        }

        /// <summary>递增纪元作废进行中的编译，并逐条释放。单条失败交给 report，继续其余条目。</summary>
        /// <param name="report">接收单条释放异常。</param>
        public void Clear(Action<Exception> report)
        {
            RequireThread();
            mEpoch++;
            foreach (string id in new List<string>(mEntries.Keys))
            {
                try
                {
                    DisposeEntry(mEntries[id]);
                    mEntries.Remove(id);
                }
                catch (Exception exception) { report(exception); }
            }
        }

        /// <summary>读取活动行为上的字段。标识有进行中操作时拒绝。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="name">字段名。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <returns>宿主读到的字段值。</returns>
        public object ReadField(string id, string name, Action guard)
        {
            return mHost.ReadField(RequireBehaviour(id, guard).Attachment, name);
        }

        /// <summary>写入活动行为上的字段。不在此处做批量回滚。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="name">字段名。</param>
        /// <param name="value">新值。</param>
        /// <param name="guard">宿主守卫。</param>
        public void SetField(string id, string name, object value, Action guard)
        {
            mHost.SetField(RequireBehaviour(id, guard).Attachment, name, value);
        }

        /// <summary>调用活动行为上的方法。参数匹配由宿主决定。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="method">方法名。</param>
        /// <param name="arguments">实参；可为 null，由宿主解释。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <returns>方法返回值。</returns>
        public object Invoke(string id, string method, object[] arguments, Action guard)
        {
            return mHost.Invoke(RequireBehaviour(id, guard).Attachment, method, arguments);
        }

        /// <summary>确认标识对应活动行为，并先执行守卫。进行中的操作不能同时读写。</summary>
        /// <param name="id">行为标识。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <returns>行为条目。</returns>
        private Entry RequireBehaviour(string id, Action guard)
        {
            Guard(guard);
            if (mPending.Contains(id)) throw new InvalidOperationException("The ID has an operation in progress.");
            if (!mEntries.TryGetValue(id, out Entry entry) || entry.Handle.Kind != "behaviour")
                throw new ArgumentException("An active behaviour ID is required.");
            return entry;
        }

        /// <summary>按种类释放一条热更：补丁走后端卸载，行为释放附件。</summary>
        /// <param name="entry">已登记条目。</param>
        private void DisposeEntry(Entry entry)
        {
            if (entry.Handle.Kind == "patch") mPatches.Remove(entry.Owner, entry.Original, entry.Patch);
            else entry.Attachment.Dispose();
        }

        /// <summary>占用标识并复制当前会话。超过 64 条时先退出占用再抛错。批量进行中不能开始新操作。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <returns>开始时的会话、代际和目标副本。</returns>
        private RoslynDomainState Begin(string id, Action guard)
        {
            ValidateName(id);
            Guard(guard);
            if (mRestoring || mBatchAttaching) throw new InvalidOperationException("A live batch is in progress.");
            if (!mPending.Add(id)) throw new InvalidOperationException("The ID already has an operation in progress.");
            if (mEntries.Count >= 64 && !mEntries.ContainsKey(id))
            {
                mPending.Remove(id);
                throw new InvalidOperationException("The live code handle limit is 64.");
            }
            var state = mState();
            return new RoslynDomainState
            {
                SessionId = state.SessionId, Generation = state.Generation, ActiveTarget = state.ActiveTarget
            };
        }

        /// <summary>要求构造线程、执行守卫，并确认授权且会话空闲。否则取消操作。</summary>
        /// <param name="guard">宿主守卫，由调用方提供。</param>
        private void Guard(Action guard)
        {
            RequireThread();
            guard();
            var state = mState();
            if (!mPermitted() || !state.SessionIdentityAvailable || state.IsCompiling || state.IsBusy)
                throw new OperationCanceledException("Live code requires an authorized idle host session.");
        }

        /// <summary>再次执行守卫，并确认纪元、会话、代际和目标都未变。</summary>
        /// <param name="scope">操作开始时复制的域状态。</param>
        /// <param name="epoch">操作开始时的纪元。</param>
        /// <param name="guard">宿主守卫。</param>
        private void Check(RoslynDomainState scope, int epoch, Action guard)
        {
            Guard(guard);
            var current = mState();
            if (epoch != mEpoch || scope.SessionId != current.SessionId || scope.Generation != current.Generation
                || scope.ActiveTarget != current.ActiveTarget)
                throw new OperationCanceledException("The host session changed while compiling.");
        }

        /// <summary>编译行为映像。load 为 true 时才占用预算并加载程序集；否则返回 null。</summary>
        /// <param name="source">待编译源码。</param>
        /// <param name="scope">开始时的域状态。</param>
        /// <param name="epoch">开始时的纪元。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <param name="load">是否加载编译结果。</param>
        /// <returns>已加载程序集；不加载时为 null。</returns>
        private async Task<Assembly> Compile(string source, RoslynDomainState scope, int epoch,
            Action guard, CancellationToken token, bool load = true)
        {
            CompiledBehaviour image = await CompileImage(source, scope, epoch, guard, token);
            if (!load) return null;
            mBudget.Reserve(image.Bytes);
            return mCompiler.LoadAssembly(image.Pe, image.Symbols);
        }

        private sealed class CompiledBehaviour
        {
            internal byte[] Pe;
            internal byte[] Symbols;
            /// <summary>计算映像占用。没有符号时只计 PE，避免把缺失符号当成额外预算。</summary>
            internal long Bytes => Pe.LongLength + (Symbols == null ? 0 : Symbols.LongLength);
        }

        /// <summary>在会话未变时编译源码。失败诊断拼进 ArgumentException，不加载程序集。</summary>
        /// <param name="source">待编译源码，非空且不超过 128 KiB。</param>
        /// <param name="scope">开始时的域状态。</param>
        /// <param name="epoch">开始时的纪元。</param>
        /// <param name="guard">宿主守卫。</param>
        /// <param name="token">编译取消令牌。</param>
        /// <returns>PE 与可选符号。</returns>
        private async Task<CompiledBehaviour> CompileImage(string source, RoslynDomainState scope, int epoch,
            Action guard, CancellationToken token)
        {
            Check(scope, epoch, guard);
            if (string.IsNullOrWhiteSpace(source) || Encoding.UTF8.GetByteCount(source) > 128 * 1024)
                throw new ArgumentException("Source must be nonempty and at most 128 KiB.");
            string[] references = mCompiler.CaptureReferences();
            byte[] symbols = null;
            string[][] diagnostics = null;
            byte[] pe = await Task.Run(() => mCompiler.Compile(source, references, token,
                out symbols, out diagnostics), token);
            Check(scope, epoch, guard);
            token.ThrowIfCancellationRequested();
            if (pe == null)
            {
                var text = new StringBuilder("Live code compilation failed:");
                foreach (string[] item in diagnostics ?? Array.Empty<string[]>())
                    text.Append("\n").Append(item[0]).Append(" (").Append(item[4]).Append(",")
                        .Append(item[5]).Append("): ").Append(item[2]);
                throw new ArgumentException(text.ToString());
            }
            return new CompiledBehaviour { Pe = pe, Symbols = symbols };
        }

        /// <summary>拒绝非构造线程的热更调用。</summary>
        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mThreadId)
                throw new InvalidOperationException("Live code operations require the host main thread.");
        }

        /// <summary>组装活动句柄。修订从已有值加一，否则为 1；此处不写修订表。</summary>
        /// <param name="id">热更标识。</param>
        /// <param name="kind">patch 或 behaviour。</param>
        /// <param name="source">参与哈希的源码。</param>
        /// <param name="scope">会话与目标来源。</param>
        /// <param name="previous">调用方传入的旧条目；本方法不读取。</param>
        /// <returns>状态为 active 的新句柄。</returns>
        private LiveCodeHandle MakeHandle(string id, string kind, string source,
            RoslynDomainState scope, Entry previous) => new LiveCodeHandle
        {
            Id = id, Kind = kind, Revision = mRevisions.TryGetValue(id, out int revision) ? revision + 1 : 1,
            SourceHash = Hash(source), SessionId = scope.SessionId, Target = scope.ActiveTarget, Status = "active",
            Budget = mBudget.ReadStatus()
        };

        /// <summary>计算 UTF-8 文本的 SHA-256，返回无连字符的小写十六进制。</summary>
        /// <param name="source">原始文本。</param>
        /// <returns>64 位十六进制哈希。</returns>
        public static string Hash(string source)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>要求不超过 80 个字符的 ASCII 标识符，首字符为字母或下划线。</summary>
        /// <param name="name">待校验名称。</param>
        public static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80 || !IsLetter(name[0]))
                throw new ArgumentException("Use an ASCII identifier with at most 80 characters.");
            for (int index = 1; index < name.Length; index++)
                if (!IsLetter(name[index]) && (name[index] < '0' || name[index] > '9'))
                    throw new ArgumentException("Use an ASCII identifier.");
        }

        /// <summary>只允许非泛型引用类型上有方法体的托管方法。引擎、运行时和补丁设施程序集不能作为目标。</summary>
        /// <param name="original">待补丁方法。</param>
        public static void ValidatePatchTarget(MethodInfo original)
        {
            if (original == null || original.ContainsGenericParameters || original.IsAbstract
                || original.GetMethodBody() == null || original.DeclaringType.IsValueType
                || original.ReturnType.IsByRef || original.IsGenericMethod || original.DeclaringType.IsGenericType)
                throw new NotSupportedException("Patch requires a non-generic managed method on a reference type.");
            string assemblyName = original.DeclaringType.Assembly.GetName().Name;
            if (assemblyName == "mscorlib" || assemblyName == "netstandard"
                || assemblyName.StartsWith("System", StringComparison.Ordinal)
                || assemblyName.StartsWith("Unity", StringComparison.Ordinal)
                || assemblyName.StartsWith("Mono", StringComparison.Ordinal)
                || assemblyName.StartsWith("YokiFrame", StringComparison.Ordinal)
                || assemblyName.StartsWith("0Harmony", StringComparison.Ordinal))
                throw new NotSupportedException("Engine, runtime and patch infrastructure methods are not patch targets.");
        }

        /// <summary>判断 ASCII 字母或下划线。</summary>
        /// <param name="value">待测字符。</param>
        /// <returns>是字母或下划线时为 true。</returns>
        private static bool IsLetter(char value) =>
            value == '_' || (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
    }
}
#endif
