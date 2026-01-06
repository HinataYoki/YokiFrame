using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 本地化文本绑定器
    /// 非 MonoBehaviour 实现，用于绑定 UI 文本组件
    /// </summary>
    public sealed class LocalizedTextBinder : ILocalizationBinder, IDisposable
    {
        private readonly int mTextId;
        private readonly object[] mArgs;
        private readonly IReadOnlyDictionary<string, object> mNamedArgs;

        // 支持的文本组件类型
        private TextMeshProUGUI mTmpText;
        private Text mLegacyText;

        private bool mIsDisposed;

        /// <summary>
        /// 绑定的文本ID
        /// </summary>
        public int TextId => mTextId;

        /// <summary>
        /// 绑定器是否有效
        /// </summary>
        public bool IsValid => !mIsDisposed && (mTmpText != null || mLegacyText != null);

        /// <summary>
        /// 创建绑定器（绑定 TextMeshProUGUI）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="tmpText">TMP 文本组件</param>
        /// <param name="args">格式化参数</param>
        public LocalizedTextBinder(int textId, TextMeshProUGUI tmpText, params object[] args)
        {
            mTextId = textId;
            mTmpText = tmpText ?? throw new ArgumentNullException(nameof(tmpText));
            mArgs = args;
            mNamedArgs = null;

            LocalizationKit.RegisterBinder(this);
            Refresh();
        }

        /// <summary>
        /// 创建绑定器（绑定 Legacy Text）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="legacyText">Legacy 文本组件</param>
        /// <param name="args">格式化参数</param>
        public LocalizedTextBinder(int textId, Text legacyText, params object[] args)
        {
            mTextId = textId;
            mLegacyText = legacyText ?? throw new ArgumentNullException(nameof(legacyText));
            mArgs = args;
            mNamedArgs = null;

            LocalizationKit.RegisterBinder(this);
            Refresh();
        }

        /// <summary>
        /// 创建绑定器（使用命名参数）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="tmpText">TMP 文本组件</param>
        /// <param name="namedArgs">命名参数</param>
        public LocalizedTextBinder(int textId, TextMeshProUGUI tmpText, IReadOnlyDictionary<string, object> namedArgs)
        {
            mTextId = textId;
            mTmpText = tmpText ?? throw new ArgumentNullException(nameof(tmpText));
            mArgs = null;
            mNamedArgs = namedArgs;

            LocalizationKit.RegisterBinder(this);
            Refresh();
        }

        /// <summary>
        /// 刷新显示文本
        /// </summary>
        public void Refresh()
        {
            if (!IsValid) return;

            string text;
            if (mNamedArgs != null)
            {
                text = LocalizationKit.Get(mTextId, mNamedArgs);
            }
            else if (mArgs != null && mArgs.Length > 0)
            {
                text = LocalizationKit.Get(mTextId, mArgs);
            }
            else
            {
                text = LocalizationKit.Get(mTextId);
            }

            if (mTmpText != null)
            {
                mTmpText.text = text;
            }
            else if (mLegacyText != null)
            {
                mLegacyText.text = text;
            }
        }

        /// <summary>
        /// 更新参数并刷新
        /// </summary>
        public void UpdateArgs(params object[] newArgs)
        {
            if (mArgs != null && newArgs != null)
            {
                Array.Copy(newArgs, mArgs, Math.Min(newArgs.Length, mArgs.Length));
                Refresh();
            }
        }

        /// <summary>
        /// 释放绑定器
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;

            mIsDisposed = true;
            LocalizationKit.UnregisterBinder(this);
            mTmpText = null;
            mLegacyText = null;
        }
    }

    /// <summary>
    /// LocalizationKit 扩展方法
    /// </summary>
    public static class LocalizationKitExtensions
    {
        /// <summary>
        /// 为 TextMeshProUGUI 创建本地化绑定
        /// </summary>
        public static LocalizedTextBinder BindLocalization(this TextMeshProUGUI text, int textId, params object[] args)
        {
            return new LocalizedTextBinder(textId, text, args);
        }

        /// <summary>
        /// 为 Legacy Text 创建本地化绑定
        /// </summary>
        public static LocalizedTextBinder BindLocalization(this Text text, int textId, params object[] args)
        {
            return new LocalizedTextBinder(textId, text, args);
        }

        /// <summary>
        /// 为 TextMeshProUGUI 创建本地化绑定（命名参数）
        /// </summary>
        public static LocalizedTextBinder BindLocalization(
            this TextMeshProUGUI text,
            int textId,
            IReadOnlyDictionary<string, object> namedArgs)
        {
            return new LocalizedTextBinder(textId, text, namedArgs);
        }
    }
}
