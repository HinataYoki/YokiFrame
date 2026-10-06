#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame
{
    public sealed class YokiFrameLiveTuningStatus
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
    public sealed class YokiFrameLiveTuningBinder
    {
        public const double PollSeconds = 0.25;
        public const double DebounceSeconds = 0.5;
        private sealed class Binding
        {
            internal YokiFrameLiveTuningStatus Status;
            internal YokiFrameLiveFieldRequest Approved;
            internal string Shape;
            internal long Stamp;
            internal long Size;
            internal bool Pending;
            internal double ChangedAt;
        }

        private readonly string mRoot;
        private readonly YokiFrameLiveCodeManager mManager;
        private readonly Func<bool> mPermitted;
        private readonly Func<YokiFrameEngineDomainState> mState;
        private readonly Dictionary<string, Binding> mBindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private double mNextPoll;
        private readonly int mThread = System.Threading.Thread.CurrentThread.ManagedThreadId;

        public YokiFrameLiveTuningBinder(string projectRoot, YokiFrameLiveCodeManager manager,
            Func<bool> permitted, Func<YokiFrameEngineDomainState> state)
        {
            mRoot = System.IO.Path.GetFullPath(projectRoot);
            mManager = manager; mPermitted = permitted; mState = state;
        }

        public YokiFrameLiveTuningStatus Bind(string id, string relativePath)
        {
            RequireThread();
            RequirePermission();
            YokiFrameLiveCodeManager.ValidateName(id);
            if (mBindings.ContainsKey(id)) throw new InvalidOperationException("Unbind the existing tuning ID before rebinding.");
            if (mBindings.Count >= 8) throw new InvalidOperationException("Tuning binding limit is 8.");
            string path = ResolvePath(relativePath);
            var content = ReadStable(path);
            var request = YokiFrameLiveFieldRequest.Parse(content.Text);
            var binding = new Binding
            {
                Approved = request, Shape = Shape(request), Stamp = content.Stamp, Size = content.Size,
                Status = new YokiFrameLiveTuningStatus { Id = id, Path = relativePath.Replace('\\', '/'), State = "watching" }
            };
            binding.Status.AppliedFields = mManager.SetFields(request, () => RequirePermission());
            binding.Status.ContentHash = YokiFrameLiveCodeManager.Hash(content.Text);
            binding.Status.ApplyCount = 1;
            mBindings.Add(id, binding);
            return binding.Status;
        }

        public bool Unbind(string id) { RequireThread(); return mBindings.Remove(id); }

        public IReadOnlyList<YokiFrameLiveTuningStatus> List()
        {
            RequireThread();
            var result = new List<YokiFrameLiveTuningStatus>();
            foreach (var binding in mBindings.Values) result.Add(binding.Status);
            return result;
        }

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

        public YokiFrameLiveTuningStatus Refresh(string id)
        {
            RequireThread();
            RequirePermission();
            if (!mBindings.TryGetValue(id, out var binding)) throw new ArgumentException("Unknown tuning ID.");
            if (binding.Status.State == "stopped") throw new InvalidOperationException("Rebind stopped tuning explicitly.");
            Apply(binding);
            return binding.Status;
        }

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
                string hash = YokiFrameLiveCodeManager.Hash(content.Text);
                if (hash == binding.Status.ContentHash)
                {
                    binding.Status.State = "watching"; binding.Status.Error = "";
                    return;
                }
                var request = YokiFrameLiveFieldRequest.Parse(content.Text);
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

        private void RequirePermission()
        {
            if (!mPermitted()) throw new InvalidOperationException("Live tuning requires Engine and trusted C# permission.");
        }

        private void RequireThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != mThread)
                throw new InvalidOperationException("Live tuning requires the host main thread.");
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.Replace('\\', '/').StartsWith(".yokiframe/tuning/", StringComparison.Ordinal)
                || System.IO.Path.GetExtension(path) != ".json")
                throw new ArgumentException("Tuning files must be .yokiframe/tuning/...json.");
            string root = YokiFrameFilePathPolicy.CombineInside(mRoot, ".yokiframe", "tuning");
            return YokiFrameFilePathPolicy.EnsureInside(root, YokiFrameFilePathPolicy.CombineInside(mRoot, path));
        }

        private sealed class Content { internal string Text; internal long Stamp; internal long Size; }
        private static Content ReadStable(string path)
        {
            var info = new FileInfo(path);
            long stamp = info.LastWriteTimeUtc.Ticks;
            string text;
            long size;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                size = stream.Length;
                if (size > YokiFrameLiveFieldRequest.MaxBytes) throw new InvalidDataException("Tuning file exceeds 48 KiB.");
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true))) text = reader.ReadToEnd();
            }
            info.Refresh();
            if (info.LastWriteTimeUtc.Ticks != stamp || info.Length != size)
                throw new IOException("Tuning file changed during read; wait for a stable write or refresh explicitly.");
            return new Content { Text = text, Stamp = stamp, Size = size };
        }

        private static string Shape(YokiFrameLiveFieldRequest request)
        {
            var json = new YokiFrameEngineJsonBuilder().StartObject().Property("session", request.SessionId)
                .Property("generation", request.Generation).Property("target", request.Target).Name("updates").StartArray();
            foreach (var update in request.Updates)
            {
                json.StartObject().Property("id", update.Id).Property("revision", update.Revision).Name("fields").StartArray();
                foreach (var field in update.Fields)
                    json.StartObject().Property("name", field.Name).Property("type", field.TypeName).EndObject();
                json.EndArray().EndObject();
            }
            return YokiFrameLiveCodeManager.Hash(json.EndArray().EndObject().ToString());
        }
    }
}
#endif
