using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrameSettings - LogKit 配置部分
    /// </summary>
    public partial class YokiFrameSettings
    {
        [Header("LogKit")]
        [SerializeField] private LogKitSettings mLogKit = new();

        /// <summary>
        /// LogKit 配置
        /// </summary>
        public LogKitSettings LogKit => mLogKit;

        /// <summary>
        /// LogKit 配置数据
        /// </summary>
        [Serializable]
        public class LogKitSettings
        {
            [Tooltip("编辑器下是否保存日志到文件")]
            public bool SaveLogInEditor;

            [Tooltip("真机下是否保存日志到文件")]
            public bool SaveLogInPlayer = true;

            [Tooltip("真机下是否启用 IMGUI 日志显示")]
            public bool EnableIMGUIInPlayer;

            [Tooltip("是否启用日志加密")]
            public bool EnableEncryption = true;

            [Tooltip("最大队列大小")]
            public int MaxQueueSize = 20000;

            [Tooltip("重复日志阈值")]
            public int MaxSameLogCount = 50;

            [Tooltip("日志保留天数")]
            public int MaxRetentionDays = 15;

            [Tooltip("单文件最大大小 (MB)")]
            public int MaxFileSizeMB = 100;

            /// <summary>
            /// 单文件最大字节数
            /// </summary>
            public long MaxFileBytes => MaxFileSizeMB * 1024L * 1024L;

            /// <summary>
            /// 重置为默认值
            /// </summary>
            public void ResetToDefault()
            {
                SaveLogInEditor = false;
                SaveLogInPlayer = true;
                EnableIMGUIInPlayer = false;
                EnableEncryption = true;
                MaxQueueSize = 20000;
                MaxSameLogCount = 50;
                MaxRetentionDays = 15;
                MaxFileSizeMB = 100;
            }
        }
    }
}
