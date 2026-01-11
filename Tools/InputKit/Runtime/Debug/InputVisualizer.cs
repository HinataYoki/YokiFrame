using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 输入可视化器
    /// 在屏幕上显示当前输入状态，用于调试
    /// </summary>
    public class InputVisualizer : MonoBehaviour
    {
        #region 序列化字段

        [Header("显示配置")]
        [SerializeField] private bool mShowOnScreen = true;
        [SerializeField] private Vector2 mScreenPosition = new(10, 10);
        [SerializeField] private float mEntryHeight = 20f;
        [SerializeField] private float mPanelWidth = 300f;

        [Header("样式")]
        [SerializeField] private Color mBackgroundColor = new(0, 0, 0, 0.7f);
        [SerializeField] private Color mTextColor = Color.white;
        [SerializeField] private Color mActiveColor = Color.green;
        [SerializeField] private int mFontSize = 14;

        [Header("历史记录")]
        [SerializeField] private int mMaxHistoryEntries = 10;
        [SerializeField] private float mHistoryEntryLifetime = 2f;

        #endregion

        #region 私有字段

        private readonly List<InputHistoryEntry> mHistory = new(16);
        private GUIStyle mBackgroundStyle;
        private GUIStyle mLabelStyle;
        private GUIStyle mActiveStyle;
        private bool mStylesInitialized;

        #endregion

        #region 数据结构

        private struct InputHistoryEntry
        {
            public string Text;
            public float Timestamp;
            public bool IsActive;
        }

        #endregion

        #region 属性

        /// <summary>是否显示</summary>
        public bool ShowOnScreen
        {
            get => mShowOnScreen;
            set => mShowOnScreen = value;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 记录输入事件
        /// </summary>
        public void LogInput(string actionName, string value, bool isActive = true)
        {
            mHistory.Add(new InputHistoryEntry
            {
                Text = $"{actionName}: {value}",
                Timestamp = Time.unscaledTime,
                IsActive = isActive
            });

            // 限制历史记录数量
            while (mHistory.Count > mMaxHistoryEntries)
            {
                mHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void ClearHistory()
        {
            mHistory.Clear();
        }

        #endregion

        #region 生命周期

        private void Update()
        {
            // 清理过期条目
            float currentTime = Time.unscaledTime;
            for (int i = mHistory.Count - 1; i >= 0; i--)
            {
                if (currentTime - mHistory[i].Timestamp > mHistoryEntryLifetime)
                {
                    mHistory.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            if (!mShowOnScreen || mHistory.Count == 0) return;

            InitializeStyles();

            float panelHeight = mHistory.Count * mEntryHeight + 10;
            var rect = new Rect(mScreenPosition.x, mScreenPosition.y, mPanelWidth, panelHeight);

            // 背景
            GUI.Box(rect, GUIContent.none, mBackgroundStyle);

            // 条目
            float y = mScreenPosition.y + 5;
            for (int i = 0; i < mHistory.Count; i++)
            {
                var entry = mHistory[i];
                var entryRect = new Rect(mScreenPosition.x + 5, y, mPanelWidth - 10, mEntryHeight);

                float alpha = 1f - (Time.unscaledTime - entry.Timestamp) / mHistoryEntryLifetime;
                var style = entry.IsActive ? mActiveStyle : mLabelStyle;
                var originalColor = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(entryRect, entry.Text, style);
                GUI.color = originalColor;

                y += mEntryHeight;
            }
        }

        #endregion

        #region 内部方法

        private void InitializeStyles()
        {
            if (mStylesInitialized) return;

            mBackgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, mBackgroundColor) }
            };

            mLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = mFontSize,
                normal = { textColor = mTextColor }
            };

            mActiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = mFontSize,
                normal = { textColor = mActiveColor },
                fontStyle = FontStyle.Bold
            };

            mStylesInitialized = true;
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        #endregion
    }
}
