using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 基于 EasyEvent 的轻量可绑定值，只在比较结果发生变化时通知监听器。
    /// </summary>
    /// <typeparam name="T">绑定值类型。</typeparam>
    public class BindValue<T> : IBindable<T>
    {
        private static Func<T, T, bool> sCompareFunc = EqualityComparer<T>.Default.Equals;
        private readonly EasyEvent<T> mOnValueChanged = new();
        protected T mValue;

        /// <summary>
        /// 创建带可选初始值的绑定对象；构造过程不会发送变化通知。
        /// </summary>
        /// <param name="value">初始值。</param>
        public BindValue(T value = default)
        {
            mValue = value;
        }

        /// <summary>
        /// 获取或设置当前值；新旧值不同时按注册顺序通知监听器。
        /// </summary>
        public virtual T Value
        {
            get { return mValue; }
            set
            {
                if (sCompareFunc(mValue, value))
                {
                    return;
                }

                mValue = value;
                mOnValueChanged.Trigger(mValue);
            }
        }

        /// <summary>
        /// 隐式读取绑定对象当前保存的值。
        /// </summary>
        /// <param name="bindValue">需要读取的绑定对象。</param>
        /// <returns>绑定对象当前值。</returns>
        public static implicit operator T(BindValue<T> bindValue)
        {
            if (bindValue == null)
            {
                return default;
            }

            return bindValue.Value;
        }

        /// <summary>
        /// 注册值变化回调。
        /// </summary>
        /// <param name="callback">值变化后调用的回调。</param>
        /// <returns>用于注销当前回调的令牌。</returns>
        public LinkUnRegister<T> Bind(Action<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return mOnValueChanged.Register(callback);
        }

        /// <summary>
        /// 注销最后一个匹配的值变化回调；空回调和不存在的回调会静默跳过。
        /// </summary>
        /// <param name="callback">需要注销的回调。</param>
        public void UnBind(Action<T> callback)
        {
            if (callback != null)
            {
                mOnValueChanged.UnRegister(callback);
            }
        }

        /// <summary>
        /// 注销当前绑定值上的全部变化回调。
        /// </summary>
        public void UnBindAll()
        {
            mOnValueChanged.UnRegisterAll();
        }

        /// <summary>
        /// 直接更新保存值但不触发任何回调，适合反序列化或批量同步。
        /// </summary>
        /// <param name="value">需要保存的新值。</param>
        public void SetValueWithoutEvent(T value)
        {
            mValue = value;
        }

        /// <summary>
        /// 为当前泛型值类型设置变化比较函数，后续所有 BindValue 实例共享该函数。
        /// </summary>
        /// <param name="compareFunc">返回 true 表示两个值等价的比较函数。</param>
        public static void SetCompareFunc(Func<T, T, bool> compareFunc)
        {
            sCompareFunc = compareFunc ?? throw new ArgumentNullException(nameof(compareFunc));
        }

        /// <summary>
        /// 返回当前值的字符串形式；空引用值返回空文本。
        /// </summary>
        /// <returns>当前值的字符串形式。</returns>
        public override string ToString()
        {
            return mValue?.ToString() ?? string.Empty;
        }
    }
}
