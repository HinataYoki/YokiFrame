#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 在任意双向 Stream 上读写单个完整 FastChannel frame，处理 partial read 但不管理 Socket、Pipe 或宿主生命周期。
    /// </summary>
    public static class YokiFrameFastChannelFrameStream
    {
        /// <summary>
        /// 从当前 Stream 位置读取一个完整 frame；遇到 EOF 会抛出异常，不会返回半帧。
        /// </summary>
        /// <param name="stream">已建立连接的可读 Stream。</param>
        /// <param name="cancellationToken">读取取消令牌。</param>
        /// <returns>已完成 framing 和 UTF-8 校验的消息。</returns>
        public static async Task<YokiFrameFastChannelFrame> ReadAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var headerBytes = new byte[YokiFrameFastChannelContract.HEADER_SIZE];
            await ReadExactlyAsync(stream, headerBytes, cancellationToken).ConfigureAwait(false);
            var header = YokiFrameFastChannelFrameCodec.ReadValidatedHeader(headerBytes);
            var payloadBytes = new byte[header.PayloadLength];
            await ReadExactlyAsync(stream, payloadBytes, cancellationToken).ConfigureAwait(false);
            return YokiFrameFastChannelFrameCodec.Decode(in header, payloadBytes);
        }

        /// <summary>
        /// 将一个完整 frame 编码、写入并刷新到当前 Stream。
        /// </summary>
        /// <param name="stream">已建立连接的可写 Stream。</param>
        /// <param name="frame">待写入的协议消息。</param>
        /// <param name="cancellationToken">写入取消令牌。</param>
        /// <returns>写入完成后的异步任务。</returns>
        public static async Task WriteAsync(
            Stream stream,
            YokiFrameFastChannelFrame frame,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var frameBytes = YokiFrameFastChannelFrameCodec.Encode(frame);
            await stream.WriteAsync(frameBytes, 0, frameBytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 循环读取直到填满目标缓冲区，避免 Pipe 或 Socket 单次 partial read 造成截断 frame。
        /// </summary>
        /// <param name="stream">已建立连接的可读 Stream。</param>
        /// <param name="buffer">必须读取完整的目标缓冲区。</param>
        /// <param name="cancellationToken">读取取消令牌。</param>
        /// <returns>缓冲区写满后的异步任务。</returns>
        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var readCount = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (readCount == 0)
                {
                    throw new EndOfStreamException("FastChannel stream closed before the current frame was complete.");
                }

                offset += readCount;
            }
        }
    }
}
#endif
