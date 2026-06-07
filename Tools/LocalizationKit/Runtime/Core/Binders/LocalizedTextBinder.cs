using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 本地化文本绑定器（兼容层）
    /// 基于泛型 LocalizedBinder 实现，保持现有 API 兼容
    /// </summary>
    public sealed class LocalizedTextBinder : ILocalizationBinder, IDisposable
    {
        private readonly ILocalizationBinder mInternalBinder;
        private readonly object[] mArgs;
        private readonly IReadOnlyDictionary<string, object> mNamedArgs;
        private readonly int mTextId;

        /// <summary>
        /// 绑定的文本ID
        /// </summary>
        public int TextId => mTextId;

        /// <summary>
        /// 绑定器是否有效
        /// </summary>
        public bool IsValid => mInternalBinder?.IsValid ?? false;

        /// <summary>
        /// 创建绑定器（绑定 TextMeshProUGUI）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="tmpText">TMP 文本组件</param>
        /// <param name="args">格式化参数</param>
        public LocalizedTextBinder(int textId, TextMeshProUGUI tmpText, params object[] args)
        {
            if (tmpText == default)
                throw new ArgumentNullException(nameof(tmpText));

            mTextId = textId;
            mArgs = args;
            mNamedArgs = null;

            if (args != null && args.Length > 0)
            {
                mInternalBinder = new LocalizedBinder<TextMeshProUGUI, object[]>(
                    resourceId: textId,
                    component: tmpText,
                    args: args,
                    resourceGetter: (id, parameters) => LocalizationKit.Get(id, parameters),
                    setter: static (tmp, text) => tmp.text = text,
                    validityChecker: static tmp => tmp != default
                );
            }
            else
            {
                mInternalBinder = new LocalizedBinder<TextMeshProUGUI>(
                    resourceId: textId,
                    component: tmpText,
                    resourceGetter: LocalizationKit.Get,
                    setter: static (tmp, text) => tmp.text = text,
                    validityChecker: static tmp => tmp != default
                );
            }
        }

        /// <summary>
        /// 创建绑定器（绑定 Legacy Text）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="legacyText">Legacy 文本组件</param>
        /// <param name="args">格式化参数</param>
        public LocalizedTextBinder(int textId, Text legacyText, params object[] args)
        {
            if (legacyText == default)
                throw new ArgumentNullException(nameof(legacyText));

            mTextId = textId;
            mArgs = args;
            mNamedArgs = null;

            if (args != null && args.Length > 0)
            {
                mInternalBinder = new LocalizedBinder<Text, object[]>(
                    resourceId: textId,
                    component: legacyText,
                    args: args,
                    resourceGetter: (id, parameters) => LocalizationKit.Get(id, parameters),
                    setter: static (text, str) => text.text = str,
                    validityChecker: static text => text != default
                );
            }
            else
            {
                mInternalBinder = new LocalizedBinder<Text>(
                    resourceId: textId,
                    component: legacyText,
                    resourceGetter: LocalizationKit.Get,
                    setter: static (text, str) => text.text = str,
                    validityChecker: static text => text != default
                );
            }
        }

        /// <summary>
        /// 创建绑定器（使用命名参数）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="tmpText">TMP 文本组件</param>
        /// <param name="namedArgs">命名参数</param>
        public LocalizedTextBinder(int textId, TextMeshProUGUI tmpText, IReadOnlyDictionary<string, object> namedArgs)
        {
            if (tmpText == default)
                throw new ArgumentNullException(nameof(tmpText));

            mTextId = textId;
            mArgs = null;
            mNamedArgs = namedArgs;

            mInternalBinder = new LocalizedBinder<TextMeshProUGUI, IReadOnlyDictionary<string, object>>(
                resourceId: textId,
                component: tmpText,
                args: namedArgs,
                resourceGetter: (id, parameters) => LocalizationKit.Get(id, parameters),
                setter: static (tmp, text) => tmp.text = text,
                validityChecker: static tmp => tmp != default
            );
        }

        /// <summary>
        /// 刷新显示文本
        /// </summary>
        public void Refresh()
        {
            mInternalBinder?.Refresh();
        }

        /// <summary>
        /// 更新参数并刷新
        /// </summary>
        public void UpdateArgs(params object[] newArgs)
        {
            if (mArgs != null && newArgs != null && mInternalBinder is LocalizedBinder<TextMeshProUGUI, object[]> tmpBinder)
            {
                Array.Copy(newArgs, mArgs, Math.Min(newArgs.Length, mArgs.Length));
                tmpBinder.UpdateArgs(mArgs);
            }
            else if (mArgs != null && newArgs != null && mInternalBinder is LocalizedBinder<Text, object[]> textBinder)
            {
                Array.Copy(newArgs, mArgs, Math.Min(newArgs.Length, mArgs.Length));
                textBinder.UpdateArgs(mArgs);
            }
        }

        /// <summary>
        /// 释放绑定器
        /// </summary>
        public void Dispose()
        {
            (mInternalBinder as IDisposable)?.Dispose();
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
