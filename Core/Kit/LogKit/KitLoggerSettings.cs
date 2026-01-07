using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// KitLogger 配置数据
    /// 存储在 ProjectSettings/KitLoggerSettings.json
    /// </summary>
    [Serializable]
    internal class KitLoggerSettings
    {
        private const string SETTINGS_PATH = "ProjectSettings/KitLoggerSettings.json";
        private static KitLoggerSettings sInstance;

        [SerializeField] private bool mSaveLogInEditor;
        [SerializeField] private bool mSaveLogInPlayer = true;
        [SerializeField] private bool mEnableIMGUIInPlayer;
        [SerializeField] private bool mEnableEncryption = true;
        [SerializeField] private int mMaxQueueSize = 20000;
        [SerializeField] private int mMaxSameLogCount = 50;
        [SerializeField] private int mMaxRetentionDays = 15;
        [SerializeField] private int mMaxFileSizeMB = 100;

        public bool SaveLogInEditor
        {
            get => mSaveLogInEditor;
            set => mSaveLogInEditor = value;
        }

        public bool SaveLogInPlayer
        {
            get => mSaveLogInPlayer;
            set => mSaveLogInPlayer = value;
        }

        public bool EnableIMGUIInPlayer
        {
            get => mEnableIMGUIInPlayer;
            set => mEnableIMGUIInPlayer = value;
        }

        public bool EnableEncryption
        {
            get => mEnableEncryption;
            set => mEnableEncryption = value;
        }

        public int MaxQueueSize
        {
            get => mMaxQueueSize;
            set => mMaxQueueSize = value;
        }

        public int MaxSameLogCount
        {
            get => mMaxSameLogCount;
            set => mMaxSameLogCount = value;
        }

        public int MaxRetentionDays
        {
            get => mMaxRetentionDays;
            set => mMaxRetentionDays = value;
        }

        public int MaxFileSizeMB
        {
            get => mMaxFileSizeMB;
            set => mMaxFileSizeMB = value;
        }

        public long MaxFileBytes => mMaxFileSizeMB * 1024L * 1024L;

        /// <summary>
        /// 获取配置实例
        /// </summary>
        internal static KitLoggerSettings Instance
        {
            get
            {
                if (sInstance == null)
                {
                    sInstance = Load();
                }
                return sInstance;
            }
        }

        private static KitLoggerSettings Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    return JsonUtility.FromJson<KitLoggerSettings>(json) ?? new KitLoggerSettings();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[KitLogger] 加载配置失败: {e.Message}");
                }
            }
            return new KitLoggerSettings();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        internal void Save()
        {
#if UNITY_EDITOR
            try
            {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SETTINGS_PATH, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[KitLogger] 保存配置失败: {e.Message}");
            }
#endif
        }
    }
}
