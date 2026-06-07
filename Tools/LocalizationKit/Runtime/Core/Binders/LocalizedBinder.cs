using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 泛型本地化绑定器
    /// 支持任意组件类型的本地化绑定（文本/图片/音频等）
    /// </summary>
    /// <typeparam name="TComponent">要绑定的组件类型</typeparam>
    public class LocalizedBinder<TComponent> : ILocalizationBinder, IDisposable
        where TComponent : class
    {
        private readonly int mResourceId;
        private readonly TComponent mComponent;
        private readonly Func<int, string> mResourceGetter;
        private readonly Action<TComponent, string> mSetter;
        private readonly Func<TComponent, bool> mValidityChecker;

        private bool mIsDisposed;

        /// <summary>
        /// 绑定的资源ID
        /// </summary>
        public int TextId => mResourceId;

        /// <summary>
        /// 绑定器是否有效
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (mIsDisposed) return false;
                if (mComponent == default) return false;
                return mValidityChecker?.Invoke(mComponent) ?? true;
            }
        }

        /// <summary>
        /// 创建泛型绑定器
        /// </summary>
        /// <param name="resourceId">资源ID（文本ID/资源ID）</param>
        /// <param name="component">要绑定的组件</param>
        /// <param name="resourceGetter">资源获取函数（输入ID，返回资源内容）</param>
        /// <param name="setter">资源设置函数（将资源应用到组件）</param>
        /// <param name="validityChecker">可选的有效性检查函数（用于 Unity 对象判空）</param>
        public LocalizedBinder(
            int resourceId,
            TComponent component,
            Func<int, string> resourceGetter,
            Action<TComponent, string> setter,
            Func<TComponent, bool> validityChecker = null)
        {
            mResourceId = resourceId;
            mComponent = component ?? throw new ArgumentNullException(nameof(component));
            mResourceGetter = resourceGetter ?? throw new ArgumentNullException(nameof(resourceGetter));
            mSetter = setter ?? throw new ArgumentNullException(nameof(setter));
            mValidityChecker = validityChecker;

            LocalizationKit.RegisterBinder(this);
            Refresh();
        }

        /// <summary>
        /// 刷新显示内容
        /// </summary>
        public void Refresh()
        {
            if (!IsValid) return;

            var resource = mResourceGetter(mResourceId);
            mSetter(mComponent, resource);
        }

        /// <summary>
        /// 释放绑定器
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;

            mIsDisposed = true;
            LocalizationKit.UnregisterBinder(this);
        }
    }

    /// <summary>
    /// 支持格式化参数的泛型绑定器
    /// </summary>
    /// <typeparam name="TComponent">要绑定的组件类型</typeparam>
    public class LocalizedBinder<TComponent, TArgs> : ILocalizationBinder, IDisposable
        where TComponent : class
    {
        private readonly int mResourceId;
        private readonly TComponent mComponent;
        private readonly Func<int, TArgs, string> mResourceGetter;
        private readonly Action<TComponent, string> mSetter;
        private readonly Func<TComponent, bool> mValidityChecker;
        private TArgs mArgs;

        private bool mIsDisposed;

        public int TextId => mResourceId;

        public bool IsValid
        {
            get
            {
                if (mIsDisposed) return false;
                if (mComponent == default) return false;
                return mValidityChecker?.Invoke(mComponent) ?? true;
            }
        }

        /// <summary>
        /// 创建支持参数的泛型绑定器
        /// </summary>
        /// <param name="resourceId">资源ID</param>
        /// <param name="component">要绑定的组件</param>
        /// <param name="args">格式化参数</param>
        /// <param name="resourceGetter">资源获取函数（输入ID和参数，返回内容）</param>
        /// <param name="setter">资源设置函数</param>
        /// <param name="validityChecker">可选的有效性检查函数</param>
        public LocalizedBinder(
            int resourceId,
            TComponent component,
            TArgs args,
            Func<int, TArgs, string> resourceGetter,
            Action<TComponent, string> setter,
            Func<TComponent, bool> validityChecker = null)
        {
            mResourceId = resourceId;
            mComponent = component ?? throw new ArgumentNullException(nameof(component));
            mArgs = args;
            mResourceGetter = resourceGetter ?? throw new ArgumentNullException(nameof(resourceGetter));
            mSetter = setter ?? throw new ArgumentNullException(nameof(setter));
            mValidityChecker = validityChecker;

            LocalizationKit.RegisterBinder(this);
            Refresh();
        }

        public void Refresh()
        {
            if (!IsValid) return;

            var resource = mResourceGetter(mResourceId, mArgs);
            mSetter(mComponent, resource);
        }

        /// <summary>
        /// 更新参数并刷新
        /// </summary>
        public void UpdateArgs(TArgs newArgs)
        {
            mArgs = newArgs;
            Refresh();
        }

        public void Dispose()
        {
            if (mIsDisposed) return;

            mIsDisposed = true;
            LocalizationKit.UnregisterBinder(this);
        }
    }
}
