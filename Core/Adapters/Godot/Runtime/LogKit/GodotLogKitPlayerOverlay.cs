#if GODOT
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 在 Godot Player 中显示 LogKit 调试日志的运行时覆盖层；使用原生 Control，而不把 Unity IMGUI 概念泄漏到 Godot Adapter。
    /// </summary>
    internal sealed partial class GodotLogKitPlayerOverlay : CanvasLayer
    {
        private const float PANEL_MARGIN = 12f;
        private const float PANEL_WIDTH = 640f;
        private const float PANEL_HEIGHT = 320f;
        private static readonly object sSettingsLock = new();
        private static GodotLogKitPlayerOverlay sInstance;
        private static volatile GodotLogKitPlayerLogBuffer sBuffer;
        private static Node sHost;
        private static volatile int sMainThreadId;
        private static bool sRequestedEnabled;
        private static int sRequestedMaxLogCount = LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT;
        private static int sSettingsDirty;
        private readonly StringBuilder mTextBuilder = new(1024);
        private RichTextLabel mLogText;

        /// <summary>
        /// 接收 Godot Bootstrap 提供的场景树挂载点，并在设置已启用时创建唯一覆盖层节点。
        /// </summary>
        /// <param name="host">当前活跃的 Runtime Bootstrap。</param>
        internal static void Attach(Node host)
        {
            sHost = host;
            sMainThreadId = Thread.CurrentThread.ManagedThreadId;
            ApplyPendingSettings();
        }

        /// <summary>
        /// 按 Runtime Settings 更新覆盖层状态；关闭时释放节点和日志缓冲，开启时等待或使用现有 Bootstrap 挂载。
        /// </summary>
        /// <param name="enabled">是否启用 Player 调试覆盖层。</param>
        /// <param name="maxLogCount">最多保留的日志条数。</param>
        internal static void ApplySettings(bool enabled, int maxLogCount)
        {
            lock (sSettingsLock)
            {
                sRequestedEnabled = enabled;
                sRequestedMaxLogCount = NormalizeMaxLogCount(maxLogCount);
                Volatile.Write(ref sSettingsDirty, 1);
            }

            if (IsCurrentMainThread())
            {
                ApplyPendingSettings();
            }
        }

        /// <summary>
        /// 由 Godot Bootstrap 的主线程帧循环消费后台设置通知；无变更时只进行一次无锁标记读取。
        /// </summary>
        internal static void ProcessPendingSettings()
        {
            if (Volatile.Read(ref sSettingsDirty) == 0 || !IsCurrentMainThread())
            {
                return;
            }

            ApplyPendingSettings();
        }

        /// <summary>
        /// 记录一条已由 LogKit 过滤并格式化的日志；未启用覆盖层时不接触 Godot Node。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">最终输出文本。</param>
        internal static void Record(LogLevel level, string message)
        {
            GodotLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null)
            {
                return;
            }

            buffer.Record(level, message);
        }

        /// <summary>
        /// 清理 Runtime Bootstrap 结束时遗留的静态引用、日志缓冲和覆盖层节点。
        /// </summary>
        internal static void Reset()
        {
            lock (sSettingsLock)
            {
                sRequestedEnabled = false;
                sRequestedMaxLogCount = LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT;
                Volatile.Write(ref sSettingsDirty, 0);
            }

            sBuffer = null;
            sHost = null;
            sMainThreadId = 0;
            RemoveInstance();
        }

        /// <summary>
        /// 在 Godot 主线程把最近一次设置请求转换为实际缓冲和 CanvasLayer 生命周期；后台线程只会留下待处理标记。
        /// </summary>
        private static void ApplyPendingSettings()
        {
            if (!IsCurrentMainThread())
            {
                return;
            }

            bool enabled;
            int maxLogCount;
            lock (sSettingsLock)
            {
                if (Volatile.Read(ref sSettingsDirty) == 0)
                {
                    return;
                }

                enabled = sRequestedEnabled;
                maxLogCount = sRequestedMaxLogCount;
                Volatile.Write(ref sSettingsDirty, 0);
            }

            if (!enabled)
            {
                sBuffer = null;
                RemoveInstance();
                return;
            }

            GodotLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null)
            {
                buffer = new GodotLogKitPlayerLogBuffer(maxLogCount);
                sBuffer = buffer;
            }
            else
            {
                buffer.SetMaxLogCount(maxLogCount);
            }

            EnsureInstance();
        }

        /// <summary>
        /// 判断当前调用是否位于 Bootstrap 已登记的 Godot 主线程，防止日志后台线程直接修改场景树。
        /// </summary>
        /// <returns>当前线程可以安全操作 Godot Node 时返回 true。</returns>
        private static bool IsCurrentMainThread()
        {
            return sMainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == sMainThreadId;
        }

        /// <summary>
        /// 归一化日志容量，防止无效配置导致零容量队列的无限出队循环。
        /// </summary>
        /// <param name="maxLogCount">原始最大条数。</param>
        /// <returns>至少为一的安全容量。</returns>
        private static int NormalizeMaxLogCount(int maxLogCount)
        {
            return maxLogCount > 0 ? maxLogCount : 1;
        }

        /// <summary>
        /// 在可用 Bootstrap 下创建唯一 CanvasLayer；挂载点缺失时保持等待，不创建全局 Godot 对象。
        /// </summary>
        private static void EnsureInstance()
        {
            if (sBuffer == null || sHost == null || !GodotObject.IsInstanceValid(sHost))
            {
                return;
            }

            if (sInstance != null && GodotObject.IsInstanceValid(sInstance))
            {
                return;
            }

            GodotLogKitPlayerOverlay overlay = new();
            sInstance = overlay;
            sHost.AddChild(overlay);
        }

        /// <summary>
        /// 释放当前 CanvasLayer；异步释放完成前先清空静态引用，确保新会话不会复用旧 Node。
        /// </summary>
        private static void RemoveInstance()
        {
            GodotLogKitPlayerOverlay overlay = sInstance;
            sInstance = null;
            if (overlay != null && GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFree();
            }
        }

        /// <summary>
        /// 进入场景树时构建只读调试界面；节点不拦截游戏输入，日志文本在 Process 阶段批量刷新。
        /// </summary>
        public override void _Ready()
        {
            Layer = 100;
            BuildControls();
            SetProcess(true);
        }

        /// <summary>
        /// 每帧至多更新一次已变更的日志文本，避免每条 LogKit 日志直接触发 Godot UI 重排。
        /// </summary>
        /// <param name="delta">Godot 提供的帧间隔；覆盖层不直接使用该值。</param>
        public override void _Process(double _)
        {
            GodotLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null || mLogText == null || !buffer.TryBuildText(mTextBuilder, out string text))
            {
                return;
            }

            mLogText.Text = text;
        }

        /// <summary>
        /// 节点离开场景树时仅清理自身静态引用，不影响新的 Runtime Bootstrap 已创建的覆盖层。
        /// </summary>
        public override void _ExitTree()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        /// <summary>
        /// 使用 CanvasLayer 下的原生 Control 创建不依赖场景资源的调试日志面板。
        /// </summary>
        private void BuildControls()
        {
            PanelContainer panel = new()
            {
                Position = new Vector2(PANEL_MARGIN, PANEL_MARGIN),
                Size = new Vector2(PANEL_WIDTH, PANEL_HEIGHT),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(panel);

            VBoxContainer layout = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.AddChild(layout);

            Label title = new()
            {
                Text = "LogKit Debug",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            layout.AddChild(title);

            mLogText = new RichTextLabel
            {
                BbcodeEnabled = false,
                FitContent = false,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(PANEL_WIDTH, PANEL_HEIGHT - 28f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            layout.AddChild(mLogText);
        }
    }

    /// <summary>
    /// 持有 Godot Player 覆盖层的有界日志缓冲；日志写入线程只会修改此纯 C# 缓冲，不直接操作 Control。
    /// </summary>
    internal sealed class GodotLogKitPlayerLogBuffer
    {
        private readonly object mLock = new();
        private readonly Queue<GodotLogKitPlayerLogEntry> mEntries = new();
        private int mMaxLogCount;
        private bool mDirty = true;

        /// <summary>
        /// 创建使用指定最大条数的空缓冲。
        /// </summary>
        /// <param name="maxLogCount">最多保留的日志条数。</param>
        internal GodotLogKitPlayerLogBuffer(int maxLogCount)
        {
            mMaxLogCount = maxLogCount > 0 ? maxLogCount : 1;
        }

        /// <summary>
        /// 更新保留上限，并立即丢弃最旧的超额日志。
        /// </summary>
        /// <param name="maxLogCount">新的最大条数。</param>
        internal void SetMaxLogCount(int maxLogCount)
        {
            lock (mLock)
            {
                mMaxLogCount = maxLogCount > 0 ? maxLogCount : 1;
                while (mEntries.Count > mMaxLogCount)
                {
                    mEntries.Dequeue();
                }

                mDirty = true;
            }
        }

        /// <summary>
        /// 追加最新日志并在达到容量时丢弃最旧内容；不在日志写入路径拼接 RichText 文本。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">最终输出文本。</param>
        internal void Record(LogLevel level, string message)
        {
            lock (mLock)
            {
                while (mEntries.Count >= mMaxLogCount)
                {
                    mEntries.Dequeue();
                }

                mEntries.Enqueue(new GodotLogKitPlayerLogEntry(level, message ?? string.Empty));
                mDirty = true;
            }
        }

        /// <summary>
        /// 仅在日志发生变化时构建完整显示文本，让 Godot UI 每帧最多写入一次。
        /// </summary>
        /// <param name="builder">复用的文本构建器。</param>
        /// <param name="text">成功时返回完整显示文本。</param>
        /// <returns>日志内容发生变化时返回 true。</returns>
        internal bool TryBuildText(StringBuilder builder, out string text)
        {
            lock (mLock)
            {
                if (!mDirty)
                {
                    text = string.Empty;
                    return false;
                }

                builder.Length = 0;
                foreach (GodotLogKitPlayerLogEntry entry in mEntries)
                {
                    builder.Append('[').Append(entry.Level).Append("] ").AppendLine(entry.Message);
                }

                text = builder.Length == 0 ? "No LogKit messages." : builder.ToString();
                mDirty = false;
                return true;
            }
        }
    }

    /// <summary>
    /// 保存一条 Godot Player 覆盖层日志的等级和最终文本，避免为每条日志创建额外对象。
    /// </summary>
    internal readonly struct GodotLogKitPlayerLogEntry
    {
        /// <summary>
        /// 创建一条覆盖层日志记录。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">最终输出文本。</param>
        internal GodotLogKitPlayerLogEntry(LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        /// <summary>获取日志等级。</summary>
        internal LogLevel Level { get; }

        /// <summary>获取最终日志文本。</summary>
        internal string Message { get; }
    }
}
#endif
