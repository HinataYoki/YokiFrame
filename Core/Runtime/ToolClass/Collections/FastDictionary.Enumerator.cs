using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供 FastDictionary 的无分配直接枚举实现。
    /// </summary>
    public partial class FastDictionary<TKey, TValue>
    {
        /// <summary>
        /// 按底层槽位顺序枚举有效键值对，并在枚举期间检测字典修改。
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly FastDictionary<TKey, TValue> mDictionary;
            private readonly int mVersion;
            private int mIndex;
            private KeyValuePair<TKey, TValue> mCurrent;

            /// <summary>
            /// 创建绑定当前字典版本的结构体枚举器。
            /// </summary>
            /// <param name="dictionary">需要枚举的快速字典。</param>
            internal Enumerator(FastDictionary<TKey, TValue> dictionary)
            {
                mDictionary = dictionary;
                mVersion = dictionary.mVersion;
                mIndex = 0;
                mCurrent = default;
            }

            /// <summary>
            /// 获取当前键值对；首次 MoveNext 前或枚举结束后返回默认值。
            /// </summary>
            public KeyValuePair<TKey, TValue> Current
            {
                get { return mCurrent; }
            }

            /// <summary>
            /// 通过非泛型枚举器契约获取当前键值对。
            /// </summary>
            object IEnumerator.Current
            {
                get { return mCurrent; }
            }

            /// <summary>
            /// 移动到下一个有效槽位；字典被修改时抛出异常。
            /// </summary>
            /// <returns>存在下一个键值对时返回 true。</returns>
            public bool MoveNext()
            {
                mDictionary.EnsureVersion(mVersion);
                while (mIndex < mDictionary.mEntries.Length)
                {
                    ref Entry entry = ref mDictionary.mEntries[mIndex];
                    mIndex++;
                    if (entry.HashCode >= 0)
                    {
                        mCurrent = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                        return true;
                    }
                }

                mCurrent = default;
                return false;
            }

            /// <summary>
            /// 把枚举位置重置到首槽位，同时要求字典版本保持不变。
            /// </summary>
            void IEnumerator.Reset()
            {
                mDictionary.EnsureVersion(mVersion);
                mIndex = 0;
                mCurrent = default;
            }

            /// <summary>
            /// 结构体枚举器不持有非托管资源，因此释放时无需执行操作。
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
