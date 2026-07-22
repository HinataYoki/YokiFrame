using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 ToolClass Bindable 与 Collections 公共能力的运行时语义。
    /// </summary>
    public sealed class YokiFrameToolClassRuntimeTests
    {
        /// <summary>
        /// 验证 BindValue 只在值实际变化时通知，并支持静默更新。
        /// </summary>
        [Test]
        public void BindValueNotifiesOnlyForChangedValues()
        {
            var value = new BindValue<int>(10);
            int notificationCount = 0;
            int receivedValue = 0;
            value.Bind(current =>
            {
                notificationCount++;
                receivedValue = current;
            });

            value.Value = 10;
            value.Value = 20;
            value.SetValueWithoutEvent(30);

            Assert.AreEqual(1, notificationCount);
            Assert.AreEqual(20, receivedValue);
            Assert.AreEqual(30, value.Value);
        }

        /// <summary>
        /// 验证 BindWithCallback 注册后立即回放当前值，令牌仍能停止后续通知。
        /// </summary>
        [Test]
        public void BindWithCallbackReplaysCurrentValueAndReturnsToken()
        {
            var value = new BindValue<string>("ready");
            string receivedValue = null;
            LinkUnRegister<string> token = value.BindWithCallback(current => receivedValue = current);

            Assert.AreEqual("ready", receivedValue);
            token.UnRegister();
            value.Value = "changed";

            Assert.AreEqual("ready", receivedValue);
        }

        /// <summary>
        /// 验证快速字典在墓碑复用和扩容后仍保持正确数量与查找结果。
        /// </summary>
        [Test]
        public void FastDictionaryReusesRemovedSlotsAndResizes()
        {
            var dictionary = new FastDictionary<int, int>(4);
            for (var index = 0; index < 100; index++)
            {
                dictionary.Add(index, index * 2);
            }

            Assert.IsTrue(dictionary.Remove(25));
            Assert.IsTrue(dictionary.TryAdd(125, 250));

            Assert.AreEqual(100, dictionary.Count);
            Assert.IsFalse(dictionary.ContainsKey(25));
            Assert.AreEqual(250, dictionary[125]);
            Assert.AreEqual(198, dictionary[99]);
        }

        /// <summary>
        /// 验证快速字典拒绝重复 Add，但 TryAdd 以 false 表达重复键。
        /// </summary>
        [Test]
        public void FastDictionaryRejectsDuplicateKeys()
        {
            var dictionary = new FastDictionary<string, int>();
            dictionary.Add("key", 1);

            Assert.IsFalse(dictionary.TryAdd("key", 2));
            Assert.Throws<ArgumentException>(() => dictionary.Add("key", 2));
            Assert.AreEqual(1, dictionary["key"]);
        }

        /// <summary>
        /// 验证构造容量表示预计元素数量，在达到峰值前不会触发扩容。
        /// </summary>
        [Test]
        public void FastDictionaryCapacityHoldsExpectedElementCount()
        {
            var dictionary = new FastDictionary<int, int>(17);
            int initialCapacity = dictionary.Capacity;

            for (var index = 0; index < 17; index++)
            {
                dictionary.Add(index, index);
            }

            Assert.AreEqual(initialCapacity, dictionary.Capacity);
        }

        /// <summary>
        /// 验证值版 GetOrAdd 在未命中时只计算一次哈希并执行一次探测流程。
        /// </summary>
        [Test]
        public void FastDictionaryValueGetOrAddUsesSingleHashCalculation()
        {
            var comparer = new CountingIntComparer();
            var dictionary = new FastDictionary<int, int>(4, comparer);

            int value = dictionary.GetOrAdd(5, 10);

            Assert.AreEqual(10, value);
            Assert.AreEqual(1, comparer.HashCallCount);
        }

        /// <summary>
        /// 验证 Clear 会释放槽位中的引用，而不是只隐藏有效哈希标记。
        /// </summary>
        [Test]
        public void FastDictionaryClearReleasesStoredReferences()
        {
            var dictionary = new FastDictionary<int, object>();
            WeakReference reference = AddReferenceAndClear(dictionary);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsFalse(reference.IsAlive);
            GC.KeepAlive(dictionary);
        }

        /// <summary>
        /// 验证池化链表预热节点会在移除后复用，而不会创建新的链表节点。
        /// </summary>
        [Test]
        public void PooledLinkedListReusesDetachedNode()
        {
            var list = new PooledLinkedList<int>(1);
            list.Prewarm(1);
            var firstLease = list.AddLast(10);

            Assert.IsTrue(list.Remove(firstLease));
            var secondLease = list.AddLast(20);

            Assert.IsFalse(firstLease.IsValid);
            Assert.AreNotEqual(firstLease, secondLease);
            Assert.IsFalse(list.Remove(firstLease));
            Assert.AreEqual(20, secondLease.Value);
            Assert.AreEqual(1, list.Count);
        }

        /// <summary>
        /// 验证池容量收缩会立即丢弃超出上限的已回收节点。
        /// </summary>
        [Test]
        public void PooledLinkedListTrimsPoolToConfiguredLimit()
        {
            var list = new PooledLinkedList<int>(4);
            list.Prewarm(4);

            list.MaxPoolSize = 2;

            Assert.AreEqual(2, list.PoolSize);
        }

        /// <summary>
        /// 验证反向延迟枚举在链表被修改后立即失败，而不会读取已复用节点。
        /// </summary>
        [Test]
        public void PooledLinkedListReverseDetectsMutation()
        {
            var list = new PooledLinkedList<int>();
            list.AddLast(1);
            list.AddLast(2);
            IEnumerator<int> enumerator = list.Reverse().GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            list.AddLast(3);

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            enumerator.Dispose();
        }

        /// <summary>
        /// 验证 SpanSplitter 按顺序返回片段，并在消费完成后停止。
        /// </summary>
        [Test]
        public void SpanSplitterReturnsSlicesWithoutIntermediateArray()
        {
            ReadOnlySpan<char> source = "one,two,three";
            var splitter = new SpanSplitter(source, ',');

            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> first));
            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> second));
            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> third));
            Assert.IsFalse(splitter.MoveNext(out _));
            Assert.AreEqual("one", first.ToString());
            Assert.AreEqual("two", second.ToString());
            Assert.AreEqual("three", third.ToString());
        }

        /// <summary>
        /// 验证默认分隔策略一致保留开头、中间和末尾空片段。
        /// </summary>
        [Test]
        public void SpanSplitterPreservesAllEmptyEntriesByDefault()
        {
            var splitter = new SpanSplitter(",a,,".AsSpan(), ',');

            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> first));
            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> second));
            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> third));
            Assert.IsTrue(splitter.MoveNext(out ReadOnlySpan<char> fourth));
            Assert.IsFalse(splitter.MoveNext(out _));
            Assert.AreEqual(string.Empty, first.ToString());
            Assert.AreEqual("a", second.ToString());
            Assert.AreEqual(string.Empty, third.ToString());
            Assert.AreEqual(string.Empty, fourth.ToString());
        }

        /// <summary>
        /// 验证 RemoveEmptyEntries 与 ref struct foreach 模式不会产出空片段。
        /// </summary>
        [Test]
        public void SpanSplitterForeachRemovesEmptyEntries()
        {
            var splitter = new SpanSplitter(",one,,two,".AsSpan(), ',', StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            string combined = string.Empty;

            foreach (ReadOnlySpan<char> segment in splitter)
            {
                count++;
                combined += segment.ToString();
            }

            Assert.AreEqual(2, count);
            Assert.AreEqual("onetwo", combined);
        }

        /// <summary>
        /// 把仅由字典持有的对象清空，并返回弱引用供 GC 释放断言使用。
        /// </summary>
        /// <param name="dictionary">保持存活以验证 Clear 行为的字典。</param>
        /// <returns>指向已经从字典清除对象的弱引用。</returns>
        private static WeakReference AddReferenceAndClear(FastDictionary<int, object> dictionary)
        {
            var value = new object();
            var reference = new WeakReference(value);
            dictionary.Add(1, value);
            dictionary.Clear();
            return reference;
        }

        /// <summary>
        /// 记录整数键哈希调用次数，验证快速字典热路径不会重复计算哈希。
        /// </summary>
        private sealed class CountingIntComparer : IEqualityComparer<int>
        {
            /// <summary>
            /// 获取当前累计哈希调用次数。
            /// </summary>
            internal int HashCallCount { get; private set; }

            /// <summary>
            /// 判断两个整数键是否相等。
            /// </summary>
            /// <param name="left">左侧键。</param>
            /// <param name="right">右侧键。</param>
            /// <returns>键相等时返回 true。</returns>
            public bool Equals(int left, int right)
            {
                return left == right;
            }

            /// <summary>
            /// 返回整数键哈希并累计调用次数。
            /// </summary>
            /// <param name="value">需要计算哈希的整数键。</param>
            /// <returns>整数键自身。</returns>
            public int GetHashCode(int value)
            {
                HashCallCount++;
                return value;
            }
        }
    }
}
