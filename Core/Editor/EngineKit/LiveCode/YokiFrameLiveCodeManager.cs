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
    public sealed partial class YokiFrameLiveCodeManager
    {
        private sealed class Entry
        {
            internal YokiFrameLiveCodeHandle Handle;
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
        private readonly YokiFrameRoslynCompilerLoader mCompiler;
        private readonly YokiFrameRoslynLoadBudget mBudget;
        private readonly YokiFrameMethodPatchBackend mPatches;
        private readonly IYokiFrameLiveCodeHost mHost;
        private readonly Func<YokiFrameEngineDomainState> mState;
        private readonly Func<bool> mPermitted;
        private readonly int mThreadId = Thread.CurrentThread.ManagedThreadId;
        private int mEpoch;

        public YokiFrameLiveCodeManager(YokiFrameRoslynCompilerLoader compiler, YokiFrameRoslynLoadBudget budget,
            YokiFrameMethodPatchBackend patches, IYokiFrameLiveCodeHost host,
            Func<YokiFrameEngineDomainState> state, Func<bool> permitted, string projectRoot = null)
        {
            mCompiler = compiler;
            mBudget = budget;
            mPatches = patches;
            mHost = host;
            mState = state;
            mPermitted = permitted;
            mSnapshots = projectRoot == null ? null : new YokiFrameLiveSnapshotStore(projectRoot);
        }

        public bool PatchInstalled => mPatches != null && mPatches.Installed;

        public IReadOnlyList<YokiFrameLiveCodeHandle> List()
        {
            RequireThread();
            var handles = new List<YokiFrameLiveCodeHandle>(mEntries.Count);
            foreach (Entry entry in mEntries.Values)
            {
                entry.Handle.Status = entry.Attachment == null || mHost.IsAlive(entry.Attachment)
                    ? "active" : "unavailable";
                entry.Handle.Budget = mBudget.ReadStatus();
                handles.Add(entry.Handle);
            }
            return handles;
        }

        public async Task<YokiFrameLiveCodeHandle> Patch(string id, MethodInfo original, string source,
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

        public async Task<YokiFrameLiveCodeHandle> Attach(string id, object target, string className,
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

        public async Task<string> Export(string id, string outputPath, Action guard, CancellationToken token)
        {
            if (mHost is IYokiFrameLiveExportHost)
            {
                var batch = await ExportMany(new[] { new YokiFrameLiveExportRequest(id, outputPath) }, null, guard, token);
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

        public string Bind(string exportId, object target, Action guard)
        {
            Guard(guard);
            if (mHost == null) throw new NotSupportedException("This engine has no persistent binding adapter.");
            return mHost.Bind(exportId, target);
        }

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

        public object ReadField(string id, string name, Action guard)
        {
            return mHost.ReadField(RequireBehaviour(id, guard).Attachment, name);
        }

        public void SetField(string id, string name, object value, Action guard)
        {
            mHost.SetField(RequireBehaviour(id, guard).Attachment, name, value);
        }

        public object Invoke(string id, string method, object[] arguments, Action guard)
        {
            return mHost.Invoke(RequireBehaviour(id, guard).Attachment, method, arguments);
        }

        private Entry RequireBehaviour(string id, Action guard)
        {
            Guard(guard);
            if (mPending.Contains(id)) throw new InvalidOperationException("The ID has an operation in progress.");
            if (!mEntries.TryGetValue(id, out Entry entry) || entry.Handle.Kind != "behaviour")
                throw new ArgumentException("An active behaviour ID is required.");
            return entry;
        }

        private void DisposeEntry(Entry entry)
        {
            if (entry.Handle.Kind == "patch") mPatches.Remove(entry.Owner, entry.Original, entry.Patch);
            else entry.Attachment.Dispose();
        }

        private YokiFrameEngineDomainState Begin(string id, Action guard)
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
            return new YokiFrameEngineDomainState
            {
                SessionId = state.SessionId, Generation = state.Generation, ActiveTarget = state.ActiveTarget
            };
        }

        private void Guard(Action guard)
        {
            RequireThread();
            guard();
            var state = mState();
            if (!mPermitted() || !state.SessionIdentityAvailable || state.IsCompiling || state.IsBusy)
                throw new OperationCanceledException("Live code requires an authorized idle host session.");
        }

        private void Check(YokiFrameEngineDomainState scope, int epoch, Action guard)
        {
            Guard(guard);
            var current = mState();
            if (epoch != mEpoch || scope.SessionId != current.SessionId || scope.Generation != current.Generation
                || scope.ActiveTarget != current.ActiveTarget)
                throw new OperationCanceledException("The host session changed while compiling.");
        }

        private async Task<Assembly> Compile(string source, YokiFrameEngineDomainState scope, int epoch,
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
            internal long Bytes => Pe.LongLength + (Symbols == null ? 0 : Symbols.LongLength);
        }

        private async Task<CompiledBehaviour> CompileImage(string source, YokiFrameEngineDomainState scope, int epoch,
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

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mThreadId)
                throw new InvalidOperationException("Live code operations require the host main thread.");
        }

        private YokiFrameLiveCodeHandle MakeHandle(string id, string kind, string source,
            YokiFrameEngineDomainState scope, Entry previous) => new YokiFrameLiveCodeHandle
        {
            Id = id, Kind = kind, Revision = mRevisions.TryGetValue(id, out int revision) ? revision + 1 : 1,
            SourceHash = Hash(source), SessionId = scope.SessionId, Target = scope.ActiveTarget, Status = "active",
            Budget = mBudget.ReadStatus()
        };

        public static string Hash(string source)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "").ToLowerInvariant();
        }

        public static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80 || !IsLetter(name[0]))
                throw new ArgumentException("Use an ASCII identifier with at most 80 characters.");
            for (int index = 1; index < name.Length; index++)
                if (!IsLetter(name[index]) && (name[index] < '0' || name[index] > '9'))
                    throw new ArgumentException("Use an ASCII identifier.");
        }

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

        private static bool IsLetter(char value) =>
            value == '_' || (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
    }
}
#endif
