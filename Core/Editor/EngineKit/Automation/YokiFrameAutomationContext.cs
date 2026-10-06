#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>Host helpers for trusted in-memory C#; this is not a security sandbox.</summary>
    public sealed class YokiFrameAutomationContext : IYokiFrameEngineRunLifetime
    {
        private sealed class Waiter
        {
            public string Clock;
            public long End;
            public TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
        }
        private readonly int mThreadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Func<long> mGameFrame;
        private readonly Func<long> mCompletedGameFrame;
        private readonly Func<bool> mAllowed;
        private readonly Action<string> mLog;
        private readonly Func<string, string, bool, CancellationToken, Task<string>> mCapture;
        private readonly CancellationTokenSource mCancellation = new CancellationTokenSource();
        private readonly List<Waiter> mWaiters = new List<Waiter>();
        private readonly List<string> mLogs = new List<string>();
        private bool mInvalid;
        private readonly YokiFrameLiveCodeApi mLiveCode;

        public YokiFrameAutomationContext(Func<long> gameFrame, Func<bool> allowed, Action<string> log,
            Func<string, string, bool, CancellationToken, Task<string>> capture = null,
            YokiFrameLiveCodeManager liveCode = null, Func<long> completedGameFrame = null)
        {
            mGameFrame = gameFrame;
            mCompletedGameFrame = completedGameFrame ?? gameFrame;
            mAllowed = allowed ?? throw new ArgumentNullException(nameof(allowed));
            mLog = log ?? throw new ArgumentNullException(nameof(log));
            mCapture = capture;
            Test = new YokiFrameAutomationAssertions(Guard);
            if (liveCode != null) mLiveCode = new YokiFrameLiveCodeApi(liveCode, Guard, mCancellation.Token);
        }

        public CancellationToken CancellationToken { get { return mCancellation.Token; } }
        public YokiFrameAutomationAssertions Test { get; }
        public int Frames { get; private set; }
        public int StaleContextCalls { get; private set; }
        public IReadOnlyList<string> Logs { get { return mLogs; } }
        public YokiFrameLiveCodeApi LiveCode
        {
            get
            {
                Guard();
                return mLiveCode ?? throw new NotSupportedException("Live code is not installed in this host.");
            }
        }

        public T RequireService<T>(string architecture = null) where T : class, IService
        {
            Guard();
            if (!ArchitectureRegistry.TryResolveLiveService(typeof(T), architecture, out var service, out var reason))
                throw new InvalidOperationException(reason);
            return (T)service;
        }

        public void ConsoleLog(string message)
        {
            Guard();
            if (mLogs.Count >= 256 || (message != null && message.Length > 16384))
                throw new InvalidOperationException("Automation log limit exceeded.");
            mLog(message ?? string.Empty);
            mLogs.Add(message ?? string.Empty);
        }

        public Task WaitFrames(int count, string clock = "gameFrame")
        {
            Guard();
            if (count < 0 || count > 1000000) throw new ArgumentOutOfRangeException(nameof(count));
            long start = ReadClock(clock);
            if (count == 0) return Task.CompletedTask;
            var waiter = new Waiter { Clock = clock, End = start + count };
            mWaiters.Add(waiter);
            return waiter.Completion.Task;
        }

        public Task<string> Capture(string mode, string path, bool autoNumber = false)
        {
            Guard();
            if (mCapture == null) throw new NotSupportedException("Capture is not supported by this host.");
            return mCapture(mode, path, autoNumber, CancellationToken);
        }

        public void AdvanceFrame()
        {
            if (mInvalid) return;
            if (!mAllowed()) { Invalidate(); return; }
            Frames++;
            for (int index = mWaiters.Count - 1; index >= 0; index--)
            {
                Waiter waiter = mWaiters[index];
                if (ReadClock(waiter.Clock, completed: true) < waiter.End) continue;
                mWaiters.RemoveAt(index);
                waiter.Completion.TrySetResult(true);
            }
        }

        public void Invalidate()
        {
            if (mInvalid) return;
            mInvalid = true;
            try { mCancellation.Cancel(); }
            catch (AggregateException exception) { mLog("Cancellation callback failed: " + exception.GetBaseException().Message); }
            foreach (Waiter waiter in mWaiters) waiter.Completion.TrySetCanceled();
            mWaiters.Clear();
        }

        internal void Guard()
        {
            if (Thread.CurrentThread.ManagedThreadId != mThreadId)
                throw new InvalidOperationException("Automation helpers must be called on the host main thread.");
            if (mInvalid || !mAllowed())
            {
                StaleContextCalls++;
                throw new OperationCanceledException("Automation session, target or permission is no longer valid.");
            }
            CancellationToken.ThrowIfCancellationRequested();
        }

        private long ReadClock(string clock, bool completed = false)
        {
            if (clock == "editorTick") return Frames;
            if (clock != "gameFrame") throw new ArgumentException("clock must be gameFrame or editorTick.");
            Func<long> read = completed ? mCompletedGameFrame : mGameFrame;
            long value = read == null ? -1 : read();
            if (value < 0) throw new InvalidOperationException("gameFrame requires an active game.");
            return value;
        }
    }

    public sealed class YokiFrameAutomationAssertionException : Exception
    {
        public YokiFrameAutomationAssertionException(string message) : base(message) { }
    }

    public sealed class YokiFrameAutomationAssertions
    {
        private readonly Action mGuard;
        private readonly List<YokiFrameRunAssertion> mRecords = new List<YokiFrameRunAssertion>();
        internal YokiFrameAutomationAssertions(Action guard) { mGuard = guard; }
        internal IReadOnlyList<YokiFrameRunAssertion> Records { get { return mRecords; } }
        public bool HasFailures { get; private set; }

        public void Equal<T>(T actual, T expected, string message = "")
        {
            mGuard();
            if (mRecords.Count >= 256) throw new InvalidOperationException("Assertion limit exceeded.");
            bool passed = EqualityComparer<T>.Default.Equals(actual, expected);
            string detail = "actual=" + actual + "; expected=" + expected;
            if (detail.Length > 4096) detail = detail.Substring(0, 4096);
            mRecords.Add(new YokiFrameRunAssertion(message, passed, detail));
            if (!passed)
            {
                HasFailures = true;
                throw new YokiFrameAutomationAssertionException(message + ": " + detail);
            }
        }
    }
}
#endif
