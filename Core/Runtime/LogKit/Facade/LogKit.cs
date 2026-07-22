using System;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
using System.Threading;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 提供所有 YokiFrame Kit 共用的日志门面，Core Runtime 只负责过滤和宿主后端转发。
    /// Editor 或 Godot Tools 编译时额外提供 Workbench 观察状态。
    /// </summary>
    public static partial class LogKit
    {
        private static readonly object sLock = new object();
#if UNITY_EDITOR || (GODOT && TOOLS)
        private const int MAX_HISTORY = 128;
        private static readonly Queue<LogKitEntry> sHistory = new Queue<LogKitEntry>(MAX_HISTORY);
#endif
        private static Func<IEngineLogger> sDefaultLoggerFactory;
        private static IEngineLogger sLogger;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static IEngineLogger sEffectiveLogger;
        private static Func<IEngineLogger, IEngineLogger> sLoggerAdapter;
#endif
        private static bool sEnabled = true;
        private static LogLevel sMinimumLevel = LogLevel.Debug;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static int sDroppedCount;
        private static long sDiagnosticVersion;

        /// <summary>
        /// 获取 LogKit 诊断状态版本号；历史、过滤配置或后端变化时递增。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }
#endif

        /// <summary>
        /// 获取或设置 LogKit 是否接收并转发日志。
        /// </summary>
        public static bool Enabled
        {
            get
            {
                lock (sLock)
                {
                    return sEnabled;
                }
            }
            set
            {
                SetEnabled(value);
            }
        }

        /// <summary>
        /// 获取或设置当前最小日志等级，低于该等级的日志会被忽略。
        /// </summary>
        public static LogLevel MinimumLevel
        {
            get
            {
                lock (sLock)
                {
                    return sMinimumLevel;
                }
            }
            set
            {
                SetMinimumLevel(value);
            }
        }

        /// <summary>
        /// 获取当前是否已经注入引擎日志后端。
        /// </summary>
        public static bool HasLogger
        {
            get
            {
                lock (sLock)
                {
                    return sLogger != null;
                }
            }
        }

        /// <summary>
        /// 注册当前宿主的默认 logger 工厂；首次写日志时才创建并安装 logger。
        /// 显式调用 <see cref="SetLogger"/> 的后端始终优先。
        /// </summary>
        /// <param name="factory">创建宿主 logger 的工厂。</param>
        public static void RegisterDefaultLoggerFactory(Func<IEngineLogger> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (sLock)
            {
                sDefaultLoggerFactory = factory;
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 获取当前日志后端类型名称；未注入时返回 None。该观察信息不会进入 Player。
        /// </summary>
        public static string LoggerName
        {
            get
            {
                lock (sLock)
                {
                    return sLogger != null ? sLogger.GetType().Name : "None";
                }
            }
        }
#endif

        /// <summary>
        /// 注入宿主日志后端；传入 null 时清空后端。
        /// </summary>
        /// <param name="logger">要注入的宿主日志后端。</param>
        public static void SetLogger(IEngineLogger logger)
        {
            lock (sLock)
            {
                if (ReferenceEquals(sLogger, logger))
                {
                    return;
                }

                sLogger = logger;
#if UNITY_EDITOR || (GODOT && TOOLS)
                sEffectiveLogger = AdaptLoggerLocked(logger);
                BumpDiagnosticVersion();
#endif
            }
        }

        /// <summary>
        /// 获取当前注入的原始宿主日志后端。
        /// </summary>
        /// <returns>当前日志后端；未注入时返回 null。</returns>
        public static IEngineLogger GetLogger()
        {
            lock (sLock)
            {
                return sLogger;
            }
        }

        /// <summary>
        /// 清空当前宿主日志后端。
        /// </summary>
        public static void ClearLogger()
        {
            SetLogger(null);
        }

        /// <summary>
        /// 重置 LogKit 业务状态和日志后端；Editor/Tools 编译时同时清空观察历史。
        /// </summary>
        public static void Reset()
        {
            lock (sLock)
            {
                sLogger = null;
                sDefaultLoggerFactory = null;
#if UNITY_EDITOR || (GODOT && TOOLS)
                sEffectiveLogger = null;
                sLoggerAdapter = null;
#endif
                sEnabled = true;
                sMinimumLevel = LogLevel.Debug;
#if UNITY_EDITOR || (GODOT && TOOLS)
                sDroppedCount = 0;
                sHistory.Clear();
                BumpDiagnosticVersion();
#endif
            }
        }

        /// <summary>
        /// 写入调试级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Debug(object message, object context = null)
        {
            Write(LogLevel.Debug, message, context, null);
        }

        /// <summary>
        /// 写入普通信息级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Log(object message, object context = null)
        {
            Write(LogLevel.Info, message, context, null);
        }

        /// <summary>
        /// 写入普通信息级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Info(object message, object context = null)
        {
            Write(LogLevel.Info, message, context, null);
        }

        /// <summary>
        /// 写入警告级别日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Warning(object message, object context = null)
        {
            Write(LogLevel.Warning, message, context, null);
        }

        /// <summary>
        /// 写入错误级别日志。
        /// </summary>
        /// <param name="message">日志内容；传入异常时会记录异常信息。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Error(object message, object context = null)
        {
            Write(LogLevel.Error, message, context, message as Exception);
        }

        /// <summary>
        /// 写入异常日志；传入 null 时写入明确的错误日志。
        /// </summary>
        /// <param name="exception">要记录的异常。</param>
        /// <param name="context">宿主上下文对象。</param>
        public static void Exception(Exception exception, object context = null)
        {
            Write(LogLevel.Error, exception ?? (object)"null exception", context, exception);
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入调试日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void DebugLog(string message)
        {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS || (GODOT && TOOLS)
            Debug(message);
#endif
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入警告日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void DebugWarning(string message)
        {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS || (GODOT && TOOLS)
            Warning(message);
#endif
        }

        /// <summary>
        /// 仅在编辑器或开发包中写入错误日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void DebugError(string message)
        {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS || (GODOT && TOOLS)
            Error(message);
#endif
        }

        /// <summary>
        /// 惰性创建当前宿主默认 logger，并在首次安装后应用 LogKit Runtime Settings。
        /// </summary>
        private static void EnsureDefaultLogger()
        {
            IEngineLogger logger;
            lock (sLock)
            {
                if (sLogger != null || sDefaultLoggerFactory == null)
                {
                    return;
                }

                logger = sDefaultLoggerFactory();
                if (logger == null)
                {
                    throw new InvalidOperationException("The default LogKit logger factory returned null.");
                }

                sLogger = logger;
#if UNITY_EDITOR || (GODOT && TOOLS)
                sEffectiveLogger = AdaptLoggerLocked(logger);
                BumpDiagnosticVersion();
#endif
            }

            LogKitSettings.ApplyBaseRuntimeSettings();
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 注入 Editor/Tools 日志后端适配器；只用于观察期包装能力。
        /// </summary>
        /// <param name="adapter">适配函数；传入 null 时禁用适配。</param>
        internal static void SetLoggerAdapter(Func<IEngineLogger, IEngineLogger> adapter)
        {
            lock (sLock)
            {
                sLoggerAdapter = adapter;
                sEffectiveLogger = AdaptLoggerLocked(sLogger);
                BumpDiagnosticVersion();
            }
        }
#endif
    }
}
