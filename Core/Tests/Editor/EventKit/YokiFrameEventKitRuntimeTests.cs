using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 验证 EventKit 在新架构中的运行时 API、Editor 隔离和宿主生命周期注销能力。
    /// </summary>
    public sealed class YokiFrameEventKitRuntimeTests
    {
        /// <summary>
        /// 测试用枚举键，用于覆盖 EnumEvent 的泛型键路由。
        /// </summary>
        private enum SampleEventKey
        {
            TypedPayload
        }

        /// <summary>
        /// 测试用事件负载，避免用基础类型掩盖 TypeEvent 的类型路由行为。
        /// </summary>
        private sealed class SamplePayload
        {
            public int Value;
        }

        /// <summary>
        /// 每个测试前清理全局事件通道和宿主错误处理器，避免其它测试残留监听器。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            EventKit.Clear();
            EventKitErrorHandler.OnError = null;
        }

        /// <summary>
        /// 每个测试后再次清理全局事件通道和宿主错误处理器，避免异常路径污染后续用例。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            EventKit.Clear();
            EventKitErrorHandler.OnError = null;
        }

        /// <summary>
        /// 验证 TypeEvent 会按负载类型发布事件，并且注册返回的令牌可以停止后续回调。
        /// </summary>
        [Test]
        public void Register_发送后注销再发送_只收到注销前的负载()
        {
            SamplePayload payload = new() { Value = 42 };
            int received = 0;
            SamplePayload lastPayload = null;
            IUnRegister token = EventKit.Type.Register<SamplePayload>(value =>
            {
                received++;
                lastPayload = value;
            });

            EventKit.Type.Send(payload);
            token.UnRegister();
            EventKit.Type.Send(new SamplePayload { Value = 7 });

            Assert.AreEqual(1, received);
            Assert.AreSame(payload, lastPayload);
        }

        /// <summary>
        /// 验证 EnumEventKey 按枚举类型和数值判断相等，相同值的哈希一致。
        /// </summary>
        [Test]
        public void EnumEventKey_比较相同与不同的枚举值_只在值相同时相等()
        {
            EnumEventKey sameKey = new(typeof(SampleEventKey), (ulong)SampleEventKey.TypedPayload);
            EnumEventKey equalKey = new(typeof(SampleEventKey), (ulong)SampleEventKey.TypedPayload);
            EnumEventKey differentKey = new(typeof(SampleEventKey), 999UL);

            Assert.IsTrue(sameKey == equalKey);
            Assert.IsFalse(sameKey != equalKey);
            Assert.IsTrue(sameKey.Equals(equalKey));
            Assert.AreEqual(sameKey.GetHashCode(), equalKey.GetHashCode());
            Assert.IsFalse(sameKey == differentKey);
            Assert.IsTrue(sameKey != differentKey);
            Assert.IsFalse(sameKey.Equals(differentKey));
        }

        /// <summary>
        /// 验证 EnumEvent 按枚举键投递负载，并在按键清理后忽略后续发送。
        /// </summary>
        [Test]
        public void Enum_按键注册发送后注销_忽略后续负载()
        {
            int received = 0;
            EventKit.Enum.Register<SampleEventKey, int>(SampleEventKey.TypedPayload, value => received += value);

            EventKit.Enum.Send(SampleEventKey.TypedPayload, 3);
            EventKit.Enum.UnRegister<SampleEventKey>(SampleEventKey.TypedPayload);
            EventKit.Enum.Send(SampleEventKey.TypedPayload, 10);

            Assert.AreEqual(3, received);
        }

        /// <summary>
        /// 验证已过时的 StringEvent 仍按字符串键投递负载，并在按键清理后忽略后续发送。
        /// </summary>
        [Test]
        public void String_按键注册发送后注销_忽略后续负载()
        {
            int received = 0;
#pragma warning disable CS0618
            EventKit.String.Register<int>("sample.typed", value => received += value);
            EventKit.String.Send("sample.typed", 5);
            EventKit.String.UnRegister("sample.typed");
            EventKit.String.Send("sample.typed", 10);
#pragma warning restore CS0618

            Assert.AreEqual(5, received);
        }

        /// <summary>
        /// 验证多线程并发向同一枚举键发送事件时，内部缓存不抛出异常。
        /// </summary>
        [Test]
        public void Send_并发发送同一枚举键_不抛异常()
        {
            System.Collections.Concurrent.ConcurrentBag<Exception> exceptions = new();
            System.Threading.Tasks.Task[] tasks = new System.Threading.Tasks.Task[8];
            for (int taskIndex = 0; taskIndex < tasks.Length; taskIndex++)
            {
                tasks[taskIndex] = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int iteration = 0; iteration < 200; iteration++)
                        {
                            EventKit.Enum.Send(SampleEventKey.TypedPayload, iteration);
                        }
                    }
                    catch (Exception exception)
                    {
                        exceptions.Add(exception);
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.IsEmpty(exceptions);
        }

        /// <summary>
        /// 验证监听器在派发过程中注销自身不会破坏当前派发，也不会阻止后续监听器。
        /// </summary>
        [Test]
        public void Trigger_监听器在派发中注销自身_只执行一次且后续监听器继续()
        {
            EasyEvent easyEvent = new();
            IUnRegister token = null;
            int firstCount = 0;
            int secondCount = 0;
            token = easyEvent.Register(() =>
            {
                firstCount++;
                token.UnRegister();
            });
            easyEvent.Register(() => secondCount++);

            easyEvent.Trigger();
            easyEvent.Trigger();

            Assert.AreEqual(1, firstCount);
            Assert.AreEqual(2, secondCount);
        }

        /// <summary>
        /// 验证前面的宿主错误处理器抛异常时，失败会被记录，且后续处理器仍会收到事件。
        /// </summary>
        [Test]
        public void Trigger_前面的错误处理器抛异常_后续处理器仍收到事件()
        {
            int receivedByLaterHandler = 0;
            Action<string> throwingHandler = _ => throw new InvalidOperationException("handler failed");
            Action<string> laterHandler = _ => receivedByLaterHandler++;
            EventKitErrorHandler.OnError = throwingHandler + laterHandler;
            EasyEvent easyEvent = new();
            easyEvent.Register(() => throw new InvalidOperationException("listener failed"));
            LogAssert.Expect(LogType.Error, "[EventKit] Error handler failed: handler failed");

            Assert.DoesNotThrow(() => easyEvent.Trigger());
            Assert.AreEqual(1, receivedByLaterHandler);
        }

        /// <summary>
        /// 验证 Unity 生命周期扩展会在 GameObject 销毁时注销 EventKit 监听器。
        /// </summary>
        [Test]
        public void UnRegisterWhenGameObjectDestroyed_对象销毁后发送_不再收到事件()
        {
            GameObject gameObject = new("EventKitAutoUnregisterTest");
            try
            {
                int received = 0;
                IUnRegister token = EventKit.Type.Register<SamplePayload>(_ => received++);
                token.UnRegisterWhenGameObjectDestroyed(gameObject);
                Object.DestroyImmediate(gameObject);
                EventKit.Type.Send(new SamplePayload());

                Assert.AreEqual(0, received);
            }
            finally
            {
                if (gameObject != default)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        /// <summary>
        /// 验证 Godot 生命周期适配源文件被整文件 GODOT 宏包裹，避免 Unity 工程误编译 Godot API。
        /// </summary>
        [Test]
        public void SourceGuard_Godot生命周期适配源码_整文件使用GODOT宏()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Godot",
                "Runtime",
                "EventKit",
                "GodotEventKitUnRegisterExtensions.cs");
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot EventKit 生命周期适配源文件。");

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual("#if GODOT", FirstNonEmptyLine(lines), sourcePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), sourcePath);
        }

        /// <summary>
        /// 读取第一条非空源码行，用于整文件宏守卫。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>第一条非空行。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 读取最后一条非空源码行，用于整文件宏守卫。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>最后一条非空行。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
