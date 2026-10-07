#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame
{
    public sealed class LiveTuningStatus
    {
        public string Id { get; internal set; }
        public string Path { get; internal set; }
        public string State { get; internal set; }
        public string Error { get; internal set; } = "";
        public int ApplyCount { get; internal set; }
        public int AppliedFields { get; internal set; }
        public string ContentHash { get; internal set; }
    }

    /// <summary>Opt-in, session-scoped file tuning. A file grants no authority beyond the bound field set.</summary>
    public sealed class LiveTuningBinder
    {
        public const double PollSeconds = 0.25;
        public const double DebounceSeconds = 0.5;
        private sealed class Binding
        {
            internal LiveTuningStatus Status;
            internal LiveFieldRequest Approved;
            internal string Shape;
            internal long Stamp;
            internal long Size;
            internal bool Pending;
            internal double ChangedAt;
        }

        private readonly string mRoot;
        private readonly LiveCodeManager mManager;
        private readonly Func<bool> mPermitted;
        private readonly Func<RoslynDomainState> mState;
        private readonly Dictionary<string, Binding> mBindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private double mNextPoll;
        private readonly int mThread = System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>绑定项目根、热更管理器和会话许可。实例只接受构造时所在线程的调用。</summary>
        /// <param name="projectRoot">项目根，会规范化为绝对路径。</param>
        /// <param name="manager">执行字段写入的热更管理器。</param>
        /// <param name="permitted">是否同时具备引擎和受信任 C# 许可。</param>
        /// <param name="state">读取当前域会话。</param>
        public LiveTuningBinder(string projectRoot, LiveCodeManager manager,
            Func<bool> permitted, Func<RoslynDomainState> state)
        {
            mRoot = System.IO.Path.GetFullPath(projectRoot);
            mManager = manager; mPermitted = permitted; mState = state;
        }

        /// <summary>绑定调参文件并立即应用一次。同一标识不能重复绑定，同时最多 8 个。文件形状成为后续自动应用的授权边界。</summary>
        /// <param name="id">调参标识。</param>
        /// <param name="relativePath">项目内 .yokiframe/tuning 下的 json 相对路径。</param>
        /// <returns>状态为 watching 的绑定状态，由后续刷新原地更新。</returns>
        public LiveTuningStatus Bind(string id, string relativePath)
        {
            RequireThread();
            RequirePermission();
            LiveCodeManager.ValidateName(id);
            if (mBindings.ContainsKey(id)) throw new InvalidOperationException("Unbind the existing tuning ID before rebinding.");
            if (mBindings.Count >= 8) throw new InvalidOperationException("Tuning binding limit is 8.");
            string path = ResolvePath(relativePath);
            var content = ReadStable(path);
            var request = LiveFieldRequest.Parse(content.Text);
            var binding = new Binding
            {
                Approved = request, Shape = Shape(request), Stamp = content.Stamp, Size = content.Size,
                Status = new LiveTuningStatus { Id = id, Path = relativePath.Replace('\\', '/'), State = "watching" }
            };
            binding.Status.AppliedFields = mManager.SetFields(request, () => RequirePermission());
            binding.Status.ContentHash = LiveCodeManager.Hash(content.Text);
            binding.Status.ApplyCount = 1;
            mBindings.Add(id, binding);
            return binding.Status;
        }

        /// <summary>移除绑定。不回滚已经写入的字段。</summary>
        /// <param name="id">调参标识。</param>
        /// <returns>存在并已移除时为 true。</returns>
        public bool Unbind(string id) { RequireThread(); return mBindings.Remove(id); }

        /// <summary>返回当前绑定状态。返回的是内部状态对象，后续刷新会改同一实例。</summary>
        /// <returns>绑定状态列表。</returns>
        public IReadOnlyList<LiveTuningStatus> List()
        {
            RequireThread();
            var result = new List<LiveTuningStatus>();
            foreach (var binding in mBindings.Values) result.Add(binding.Status);
            return result;
        }

        /// <summary>把全部分绑定标为 stopped 并记下原因，同时清掉待应用标记。不写字段。</summary>
        /// <param name="reason">停止原因。</param>
        public void StopAll(string reason)
        {
            RequireThread();
            foreach (var binding in mBindings.Values)
            {
                binding.Status.State = "stopped";
                binding.Status.Error = reason;
                binding.Pending = false;
            }
        }

        /// <summary>立即重读并应用一个未停止的绑定。停止后的绑定必须显式重新绑定。</summary>
        /// <param name="id">调参标识。</param>
        /// <returns>刷新后的同一状态对象。</returns>
        public LiveTuningStatus Refresh(string id)
        {
            RequireThread();
            RequirePermission();
            if (!mBindings.TryGetValue(id, out var binding)) throw new ArgumentException("Unknown tuning ID.");
            if (binding.Status.State == "stopped") throw new InvalidOperationException("Rebind stopped tuning explicitly.");
            Apply(binding);
            return binding.Status;
        }

        /// <summary>按轮询间隔检查文件时间戳和长度。变化后等待防抖再应用；上下文失效则停止该绑定。单个绑定的异常只记入其状态。</summary>
        /// <param name="seconds">宿主单调时钟，单位秒。</param>
        public void Tick(double seconds)
        {
            RequireThread();
            if (seconds < mNextPoll) return;
            mNextPoll = seconds + PollSeconds;
            foreach (var binding in mBindings.Values)
            {
                if (binding.Status.State == "stopped") continue;
                if (!ContextMatches(binding))
                {
                    binding.Status.State = "stopped";
                    binding.Status.Error = "Permission, session, target or behaviour revision changed; rebind explicitly.";
                    binding.Pending = false;
                    continue;
                }
                try
                {
                    var info = new FileInfo(ResolvePath(binding.Status.Path));
                    if (!info.Exists) throw new FileNotFoundException("Tuning file is missing.");
                    long stamp = info.LastWriteTimeUtc.Ticks;
                    if (stamp != binding.Stamp || info.Length != binding.Size)
                    {
                        binding.Stamp = stamp; binding.Size = info.Length;
                        binding.Pending = true; binding.ChangedAt = seconds;
                    }
                    if (binding.Pending && seconds - binding.ChangedAt >= DebounceSeconds)
                    {
                        binding.Pending = false;
                        Apply(binding);
                    }
                }
                catch (Exception error)
                {
                    binding.Status.State = "error";
                    binding.Status.Error = error.Message;
                }
            }
        }

        /// <summary>稳定读取后按内容哈希决定是否写字段。形状变化只记错误，不扩大授权；上下文失效则停止。</summary>
        /// <param name="binding">已有绑定。</param>
        private void Apply(Binding binding)
        {
            if (!ContextMatches(binding))
            {
                binding.Status.State = "stopped";
                binding.Status.Error = "Tuning context expired; rebind explicitly.";
                return;
            }
            try
            {
                var content = ReadStable(ResolvePath(binding.Status.Path));
                binding.Stamp = content.Stamp; binding.Size = content.Size;
                string hash = LiveCodeManager.Hash(content.Text);
                if (hash == binding.Status.ContentHash)
                {
                    binding.Status.State = "watching"; binding.Status.Error = "";
                    return;
                }
                var request = LiveFieldRequest.Parse(content.Text);
                if (Shape(request) != binding.Shape)
                    throw new InvalidOperationException("Tuning file changed its authorized context/IDs/revisions/field names/types. Rebind explicitly.");
                binding.Status.AppliedFields = mManager.SetFields(request, () => RequirePermission());
                binding.Status.ContentHash = hash;
                binding.Status.ApplyCount++;
                binding.Status.State = "watching"; binding.Status.Error = "";
            }
            catch (Exception error)
            {
                binding.Status.State = "error"; binding.Status.Error = error.Message;
            }
        }

        /// <summary>核对许可、会话、代际、目标和每个行为的修订与活动状态。不修改绑定。</summary>
        /// <param name="binding">待核对绑定。</param>
        /// <returns>授权上下文仍有效时为 true。</returns>
        private bool ContextMatches(Binding binding)
        {
            if (!mPermitted()) return false;
            var state = mState();
            if (!state.SessionIdentityAvailable || state.SessionId != binding.Approved.SessionId
                || state.Generation != binding.Approved.Generation || state.ActiveTarget != binding.Approved.Target
                || state.IsCompiling || state.IsBusy) return false;
            var handles = mManager.List();
            foreach (var update in binding.Approved.Updates)
            {
                bool found = false;
                foreach (var handle in handles)
                    if (handle.Id == update.Id && handle.Revision == update.Revision && handle.Status == "active") { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>没有引擎和受信任 C# 许可时拒绝调参。</summary>
        private void RequirePermission()
        {
            if (!mPermitted()) throw new InvalidOperationException("Live tuning requires Engine and trusted C# permission.");
        }

        /// <summary>拒绝非构造线程的调参调用。</summary>
        private void RequireThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != mThread)
                throw new InvalidOperationException("Live tuning requires the host main thread.");
        }

        /// <summary>只接受项目 .yokiframe/tuning 目录下的 json，并限制在该目录内。</summary>
        /// <param name="path">项目相对路径。</param>
        /// <returns>规范化后的绝对路径。</returns>
        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.Replace('\\', '/').StartsWith(".yokiframe/tuning/", StringComparison.Ordinal)
                || System.IO.Path.GetExtension(path) != ".json")
                throw new ArgumentException("Tuning files must be .yokiframe/tuning/...json.");
            string root = YokiFrameFilePathPolicy.CombineInside(mRoot, ".yokiframe", "tuning");
            return YokiFrameFilePathPolicy.EnsureInside(root, YokiFrameFilePathPolicy.CombineInside(mRoot, path));
        }

        private sealed class Content { internal string Text; internal long Stamp; internal long Size; }
        /// <summary>读取未在读取期间变化的文件。超过调参载荷上限，或时间戳、长度发生变化时拒绝。</summary>
        /// <param name="path">调参文件绝对路径。</param>
        /// <returns>文本、写入时间和长度。</returns>
        private static Content ReadStable(string path)
        {
            var info = new FileInfo(path);
            long stamp = info.LastWriteTimeUtc.Ticks;
            string text;
            long size;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                size = stream.Length;
                if (size > LiveFieldRequest.MaxBytes) throw new InvalidDataException("Tuning file exceeds 48 KiB.");
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true))) text = reader.ReadToEnd();
            }
            info.Refresh();
            if (info.LastWriteTimeUtc.Ticks != stamp || info.Length != size)
                throw new IOException("Tuning file changed during read; wait for a stable write or refresh explicitly.");
            return new Content { Text = text, Stamp = stamp, Size = size };
        }

        /// <summary>对会话、代际、目标以及行为标识、修订、字段名和类型做稳定哈希。值变化不改变形状。</summary>
        /// <param name="request">已解析的字段请求。</param>
        /// <returns>授权形状哈希。</returns>
        private static string Shape(LiveFieldRequest request)
        {
            var json = new RoslynJsonBuilder().StartObject().Property("session", request.SessionId)
                .Property("generation", request.Generation).Property("target", request.Target).Name("updates").StartArray();
            foreach (var update in request.Updates)
            {
                json.StartObject().Property("id", update.Id).Property("revision", update.Revision).Name("fields").StartArray();
                foreach (var field in update.Fields)
                    json.StartObject().Property("name", field.Name).Property("type", field.TypeName).EndObject();
                json.EndArray().EndObject();
            }
            return LiveCodeManager.Hash(json.EndArray().EndObject().ToString());
        }
    }
}
#endif
