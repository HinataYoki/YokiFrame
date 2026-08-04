#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 在 FastChannel listener 线程和宿主主线程之间传递请求；队列有界、可停止，且不引用任何引擎 API。
    /// </summary>
    public sealed class YokiFrameFastChannelRequestQueue : IDisposable
    {
        private readonly object mGate = new object();
        private readonly Queue<PendingRequest> mPendingRequests = new Queue<PendingRequest>();
        private readonly int mMaxPendingCount;
        private bool mStopped;

        /// <summary>
        /// 创建指定容量的请求队列；容量必须为正数，避免后台 listener 无界积压。
        /// </summary>
        /// <param name="maxPendingCount">允许等待宿主主线程的最大请求数。</param>
        public YokiFrameFastChannelRequestQueue(int maxPendingCount)
        {
            if (maxPendingCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPendingCount));
            }

            mMaxPendingCount = maxPendingCount;
        }

        /// <summary>
        /// 获取当前等待宿主主线程处理的请求数量。
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (mGate)
                {
                    return mPendingRequests.Count;
                }
            }
        }

        /// <summary>
        /// 尝试将 listener 收到的 frame 入队，并返回等待主线程响应的任务。
        /// </summary>
        /// <param name="request">已完成 framing 校验的请求 frame。</param>
        /// <param name="responseTask">主线程处理后完成的 response 任务；拒绝时返回失败任务。</param>
        /// <returns>队列仍运行且未满时返回 true。</returns>
        public bool TryEnqueue(
            YokiFrameFastChannelFrame request,
            out Task<YokiFrameFastChannelFrame> responseTask)
        {
            return TryEnqueue(request, CancellationToken.None, out responseTask);
        }

        /// <summary>
        /// 尝试将 listener 请求入队，并绑定当前连接或 Host 的取消令牌；连接终止后尚未开始处理的请求不会再执行。
        /// </summary>
        /// <param name="request">已完成 framing 校验的请求 frame。</param>
        /// <param name="cancellationToken">当前连接或 Host 生命周期取消令牌。</param>
        /// <param name="responseTask">主线程处理后完成的 response 任务；取消或拒绝时返回已终止任务。</param>
        /// <returns>队列仍运行、未满且请求尚未取消时返回 true。</returns>
        public bool TryEnqueue(
            YokiFrameFastChannelFrame request,
            CancellationToken cancellationToken,
            out Task<YokiFrameFastChannelFrame> responseTask)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                responseTask = Task.FromCanceled<YokiFrameFastChannelFrame>(cancellationToken);
                return false;
            }

            lock (mGate)
            {
                if (mStopped)
                {
                    responseTask = Task.FromCanceled<YokiFrameFastChannelFrame>(new CancellationToken(true));
                    return false;
                }

                if (mPendingRequests.Count >= mMaxPendingCount)
                {
                    responseTask = Task.FromException<YokiFrameFastChannelFrame>(
                        new InvalidOperationException("FastChannel request queue is full."));
                    // 调用方通常只依赖返回值而丢弃该任务；读取 Exception 标记已观察，避免终结时的 UnobservedTaskException。
                    _ = responseTask.Exception;
                    return false;
                }

                PendingRequest pendingRequest = new PendingRequest(request);
                mPendingRequests.Enqueue(pendingRequest);
                pendingRequest.RegisterCancellation(cancellationToken);
                responseTask = pendingRequest.ResponseSource.Task;
                return true;
            }
        }

        /// <summary>
        /// 在宿主主线程依次执行当前已入队请求，并完成对应 listener 等待的 response 任务。
        /// </summary>
        /// <param name="responseFactory">根据单个请求生成终态 response 的主线程回调。</param>
        /// <returns>本次已处理的请求数量。</returns>
        public int ProcessPending(Func<YokiFrameFastChannelFrame, YokiFrameFastChannelFrame> responseFactory)
        {
            if (responseFactory == null)
            {
                throw new ArgumentNullException(nameof(responseFactory));
            }

            var processedCount = 0;
            while (TryDequeue(out var pendingRequest))
            {
                if (CompletePendingRequest(pendingRequest, responseFactory))
                {
                    processedCount++;
                }
            }

            return processedCount;
        }

        /// <summary>
        /// 停止接收新请求并取消全部尚未进入主线程的等待任务。
        /// </summary>
        public void Stop()
        {
            PendingRequest[] pendingRequests;
            lock (mGate)
            {
                if (mStopped)
                {
                    return;
                }

                mStopped = true;
                pendingRequests = mPendingRequests.ToArray();
                mPendingRequests.Clear();
            }

            for (var index = 0; index < pendingRequests.Length; index++)
            {
                pendingRequests[index].Cancel();
                pendingRequests[index].Dispose();
            }
        }

        /// <summary>
        /// 释放队列，语义等同于停止并取消等待中的 listener 请求。
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 从受锁队列取出一个待处理请求；停止后保留已经在 Stop 前取出的请求由当前主线程完成。
        /// </summary>
        /// <param name="pendingRequest">取出的待处理请求；没有请求时为 null。</param>
        /// <returns>存在可处理请求时返回 true。</returns>
        private bool TryDequeue(out PendingRequest pendingRequest)
        {
            lock (mGate)
            {
                if (mPendingRequests.Count == 0)
                {
                    pendingRequest = default!;
                    return false;
                }

                pendingRequest = mPendingRequests.Dequeue();
                return true;
            }
        }

        /// <summary>
        /// 在主线程调用 response factory，并把成功、空返回或异常转换为 listener 可观察的任务终态。
        /// </summary>
        /// <param name="pendingRequest">已经从队列取出的请求。</param>
        /// <param name="responseFactory">宿主提供的主线程 response 生成器。</param>
        private static bool CompletePendingRequest(
            PendingRequest pendingRequest,
            Func<YokiFrameFastChannelFrame, YokiFrameFastChannelFrame> responseFactory)
        {
            if (!pendingRequest.TryBeginProcessing())
            {
                pendingRequest.Dispose();
                return false;
            }

            try
            {
                var response = responseFactory(pendingRequest.Request);
                if (response == null)
                {
                    throw new InvalidOperationException("FastChannel response factory returned null.");
                }

                pendingRequest.ResponseSource.TrySetResult(response);
            }
            catch (Exception exception)
            {
                pendingRequest.ResponseSource.TrySetException(exception);
            }

            pendingRequest.Dispose();
            return true;
        }

        /// <summary>
        /// 保存单个 request frame 与对应 listener 等待的异步 response 源。
        /// </summary>
        private sealed class PendingRequest
        {
            private int mState;
            private CancellationTokenRegistration mCancellationRegistration;

            /// <summary>
            /// 创建单个待处理请求及其异步 response 源。
            /// </summary>
            /// <param name="request">后台 listener 已校验的 request frame。</param>
            public PendingRequest(YokiFrameFastChannelFrame request)
            {
                Request = request;
                ResponseSource = new TaskCompletionSource<YokiFrameFastChannelFrame>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>
            /// 获取需要由主线程处理的 request frame。
            /// </summary>
            public YokiFrameFastChannelFrame Request { get; }

            /// <summary>
            /// 获取 listener 等待的异步 response 源。
            /// </summary>
            public TaskCompletionSource<YokiFrameFastChannelFrame> ResponseSource { get; }

            /// <summary>
            /// 绑定连接取消令牌；回调只取消尚未开始处理的请求，避免主线程执行已经失去消费者的工作。
            /// </summary>
            /// <param name="cancellationToken">连接或 Host 生命周期令牌。</param>
            public void RegisterCancellation(CancellationToken cancellationToken)
            {
                if (cancellationToken.CanBeCanceled)
                {
                    mCancellationRegistration = cancellationToken.Register(
                        static state => ((PendingRequest)state!).Cancel(),
                        this);
                }
            }

            /// <summary>
            /// 尝试将请求从等待状态推进到主线程处理状态。
            /// </summary>
            /// <returns>当前请求仍未被取消且可以处理时返回 true。</returns>
            public bool TryBeginProcessing()
            {
                return Interlocked.CompareExchange(ref mState, 1, 0) == 0;
            }

            /// <summary>
            /// 取消尚未开始处理的请求并完成 listener 等待任务。
            /// </summary>
            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref mState, 2, 0) == 0)
                {
                    ResponseSource.TrySetCanceled();
                }
            }

            /// <summary>
            /// 释放与连接令牌关联的注册，避免长生命周期 Host 保留已完成请求。
            /// </summary>
            public void Dispose()
            {
                mCancellationRegistration.Dispose();
            }
        }
    }
}
#endif
