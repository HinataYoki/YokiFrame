using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 输入缓冲系统
    /// </summary>
    public static partial class InputKit
    {
        private static InputBuffer sInputBuffer;
        private static float sBufferWindowMs = 150f;

        #region 缓冲配置

        /// <summary>缓冲窗口时长（毫秒）</summary>
        public static float BufferWindow
        {
            get => sBufferWindowMs;
            set
            {
                sBufferWindowMs = Mathf.Max(0f, value);
                if (sInputBuffer != default)
                {
                    sInputBuffer.WindowMs = sBufferWindowMs;
                }
            }
        }

        /// <summary>
        /// 设置缓冲窗口时长（毫秒）
        /// </summary>
        /// <param name="milliseconds">窗口时长（毫秒）</param>
        public static void SetBufferWindow(float milliseconds)
        {
            BufferWindow = milliseconds;
        }

        #endregion

        #region 类型安全缓冲操作

        /// <summary>
        /// 记录输入到缓冲（内部调用）
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <param name="value">输入值</param>
        internal static void BufferInput(InputAction action, float value = 1f)
        {
            if (action == default) return;
            EnsureBufferInitialized();
            sInputBuffer.Add(action.id.ToString(), value);
        }

        /// <summary>
        /// 查询缓冲中是否有指定输入（类型安全）
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <returns>是否存在有效缓冲</returns>
        public static bool HasBufferedInput(InputAction action)
        {
            if (action == default || sInputBuffer == default) return false;
            return sInputBuffer.Has(action.id.ToString());
        }

        /// <summary>
        /// 消费缓冲中的输入（类型安全）
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <returns>是否成功消费</returns>
        public static bool ConsumeBufferedInput(InputAction action)
        {
            if (action == default || sInputBuffer == default) return false;
            return sInputBuffer.Consume(action.id.ToString());
        }

        /// <summary>
        /// 查看缓冲中的输入（不消费，类型安全）
        /// </summary>
        /// <param name="action">InputAction</param>
        /// <param name="timestamp">输入时间戳</param>
        /// <returns>是否存在有效缓冲</returns>
        public static bool PeekBufferedInput(InputAction action, out float timestamp)
        {
            if (action == default || sInputBuffer == default)
            {
                timestamp = 0f;
                return false;
            }
            return sInputBuffer.Peek(action.id.ToString(), out timestamp, out _);
        }

        /// <summary>
        /// 清空指定 Action 的缓冲（类型安全）
        /// </summary>
        /// <param name="action">InputAction</param>
        public static void ClearBuffer(InputAction action)
        {
            if (action == default) return;
            sInputBuffer?.Clear(action.id.ToString());
        }

        #endregion

        #region 通用缓冲操作

        /// <summary>
        /// 清空所有缓冲
        /// </summary>
        public static void ClearBuffer()
        {
            sInputBuffer?.Clear();
        }

        /// <summary>
        /// 清理过期的缓冲项（可选，在 Update 中调用）
        /// </summary>
        public static void CleanupBuffer()
        {
            sInputBuffer?.Cleanup();
        }

        #endregion

        #region 内部方法

        private static void EnsureBufferInitialized()
        {
            if (sInputBuffer != default) return;

            sInputBuffer = new InputBuffer();
            sInputBuffer.WindowMs = sBufferWindowMs;
        }

        /// <summary>
        /// 重置缓冲系统（内部调用）
        /// </summary>
        internal static void ResetBuffer()
        {
            sInputBuffer?.Clear();
            sInputBuffer = default;
        }

        #endregion
    }
}
