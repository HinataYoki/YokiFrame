using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 验证 EventKit 在新架构中的运行时 API、Editor 隔离和宿主生命周期注销能力。
    /// </summary>
    public sealed class YokiFrameEventKitRuntimeTests
    {
        private const string CORE_ASSEMBLY_NAME = "YokiFrame";

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
        /// 每个测试前清理已存在的全局事件通道，避免其它测试残留监听器。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            EventKit.Clear();
        }

        /// <summary>
        /// 每个测试后再次清理全局事件通道，避免 RED 或异常路径污染后续用例。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            EventKit.Clear();
        }

        /// <summary>
        /// 验证 TypeEvent 会按负载类型发布事件，并且注册返回的令牌可以停止后续回调。
        /// </summary>
        [Test]
        public void TypeEventPublishesPayloadAndTokenUnregistersListener()
        {
            object typeBus = RequireStaticBus("YokiFrame.EventKit", "Type");
            var payload = new SamplePayload { Value = 42 };
            int received = 0;
            SamplePayload lastPayload = null;
            object token = InvokeGeneric(typeBus, "Register", new[] { typeof(SamplePayload) },
                new object[] { (Action<SamplePayload>)(value => { received++; lastPayload = value; }) });

            InvokeGeneric(typeBus, "Send", new[] { typeof(SamplePayload) }, new object[] { payload });
            InvokeUnRegister(token);
            InvokeGeneric(typeBus, "Send", new[] { typeof(SamplePayload) }, new object[] { new SamplePayload { Value = 7 } });

            Assert.AreEqual(1, received);
            Assert.AreSame(payload, lastPayload);
        }

        /// <summary>
        /// 验证 EnumEventKey 满足值相等语义，重载运算符与 Equals 行为一致。
        /// </summary>
        [Test]
        public void EnumEventKey_EqualityAndOperatorsUseValueSemantics()
        {
            var key1 = new EnumEventKey(typeof(SampleEventKey), (ulong)SampleEventKey.TypedPayload);
            var key2 = new EnumEventKey(typeof(SampleEventKey), (ulong)SampleEventKey.TypedPayload);
            var key3 = new EnumEventKey(typeof(SampleEventKey), 999UL);

            Assert.IsTrue(key1 == key2);
            Assert.IsFalse(key1 != key2);
            Assert.IsTrue(key1.Equals(key2));
            Assert.AreEqual(key1.GetHashCode(), key2.GetHashCode());

            Assert.IsFalse(key1 == key3);
            Assert.IsTrue(key1 != key3);
            Assert.IsFalse(key1.Equals(key3));
        }

        /// <summary>
        /// 验证 EnumEvent 和 StringEvent 具备旧版同款的带类型负载注册、发送和清理语义。
        /// </summary>
        [Test]
        public void EnumAndStringEventsPublishTypedPayloadsAndClearByKey()
        {
            object enumBus = RequireStaticBus("YokiFrame.EventKit", "Enum");
            object stringBus = RequireStaticBus("YokiFrame.EventKit", "String");
            int enumValue = 0;
            int stringValue = 0;
            InvokeGeneric(enumBus, "Register", new[] { typeof(SampleEventKey), typeof(int) },
                new object[] { SampleEventKey.TypedPayload, (Action<int>)(value => enumValue += value) });
            InvokeGeneric(stringBus, "Register", new[] { typeof(int) },
                new object[] { "sample.typed", (Action<int>)(value => stringValue += value) });

            InvokeGeneric(enumBus, "Send", new[] { typeof(SampleEventKey), typeof(int) },
                new object[] { SampleEventKey.TypedPayload, 3 });
            InvokeGeneric(stringBus, "Send", new[] { typeof(int) }, new object[] { "sample.typed", 5 });
            InvokeGeneric(enumBus, "UnRegister", new[] { typeof(SampleEventKey) }, new object[] { SampleEventKey.TypedPayload });
            InvokeInstance(stringBus, "UnRegister", new object[] { "sample.typed" });
            InvokeGeneric(enumBus, "Send", new[] { typeof(SampleEventKey), typeof(int) },
                new object[] { SampleEventKey.TypedPayload, 10 });
            InvokeGeneric(stringBus, "Send", new[] { typeof(int) }, new object[] { "sample.typed", 10 });

            Assert.AreEqual(3, enumValue);
            Assert.AreEqual(5, stringValue);
        }

        /// <summary>
        /// 验证多线程并发向 EnumEvent 发送事件时，EnumValueCache 内部字典安全不发生数据竞争或异常。
        /// </summary>
        [Test]
        public void EnumEventConcurrentSend_DoesNotCorruptOrThrow()
        {
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new System.Threading.Tasks.Task[8];
            for (var taskIndex = 0; taskIndex < tasks.Length; taskIndex++)
            {
                tasks[taskIndex] = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (var iteration = 0; iteration < 200; iteration++)
                        {
                            EventKit.Enum.Send(SampleEventKey.TypedPayload, iteration);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.IsEmpty(exceptions, "并发调用 EnumEvent.Send 时不应抛出任何异常。");
        }

        /// <summary>验证未使用的 Editor 事件总线不会作为平行公开 API 保留。</summary>
        [Test]
        public void EventKitEditorFacadeHasBeenRemoved()
        {
            Assert.IsNull(Type.GetType("YokiFrame.EventKitEditor, YokiFrame.Editor"));
        }

        /// <summary>
        /// 验证监听器在派发过程中注销自身不会破坏当前派发，也不会影响后续监听器执行。
        /// </summary>
        [Test]
        public void EasyEventAllowsListenerToUnregisterItselfDuringDispatch()
        {
            object easyEvent = Activator.CreateInstance(RequireType("YokiFrame.EasyEvent"));
            object token = null;
            int firstCount = 0;
            int secondCount = 0;
            token = InvokeInstance(easyEvent, "Register", new object[] { (Action)(() => { firstCount++; InvokeUnRegister(token); }) });
            InvokeInstance(easyEvent, "Register", new object[] { (Action)(() => secondCount++) });

            InvokeInstance(easyEvent, "Trigger", Array.Empty<object>());
            InvokeInstance(easyEvent, "Trigger", Array.Empty<object>());

            Assert.AreEqual(1, firstCount);
            Assert.AreEqual(2, secondCount);
        }

        /// <summary>
        /// 验证一个宿主错误处理器抛异常时，后续处理器仍会收到事件且派发不再被打断。
        /// </summary>
        [Test]
        public void ErrorHandlerExceptionsDoNotInterruptLaterHandlers()
        {
            int receivedByLaterHandler = 0;
            Action<string> throwingHandler = _ => throw new InvalidOperationException("handler failed");
            Action<string> laterHandler = _ => receivedByLaterHandler++;
            EventKitErrorHandler.OnError = throwingHandler + laterHandler;
            var easyEvent = new EasyEvent();
            easyEvent.Register(() => throw new InvalidOperationException("listener failed"));

            Assert.DoesNotThrow(() => easyEvent.Trigger());
            Assert.AreEqual(1, receivedByLaterHandler);
        }

        /// <summary>
        /// 验证 Unity 生命周期扩展会把 EventKit 注销令牌绑定到 GameObject 销毁时机。
        /// </summary>
        [Test]
        public void UnityAdapterUnregistersListenerWhenGameObjectDestroyed()
        {
            object typeBus = RequireStaticBus("YokiFrame.EventKit", "Type");
            var gameObject = new GameObject("EventKitAutoUnregisterTest");
            int received = 0;
            object token = InvokeGeneric(typeBus, "Register", new[] { typeof(SamplePayload) },
                new object[] { (Action<SamplePayload>)(_ => received++) });

            InvokeUnityUnregisterExtension("UnRegisterWhenGameObjectDestroyed", token, gameObject);
            Object.DestroyImmediate(gameObject);
            InvokeGeneric(typeBus, "Send", new[] { typeof(SamplePayload) }, new object[] { new SamplePayload() });

            Assert.AreEqual(0, received);
        }

        /// <summary>
        /// 验证 Godot 生命周期适配源文件被 GODOT 宏包裹，避免 Unity 工程误编译 Godot API。
        /// </summary>
        [Test]
        public void GodotAdapterSourceIsGuardedByGodotDefine()
        {
            string sourcePath = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Godot", "Runtime", "EventKit", "GodotEventKitUnRegisterExtensions.cs");
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot EventKit 生命周期适配源文件。");

            string source = File.ReadAllText(sourcePath);
            Assert.IsTrue(source.Contains("#if GODOT"), "Godot EventKit 适配必须由 GODOT 宏包裹。");
            Assert.IsTrue(source.Contains("using Godot;"), "Godot EventKit 适配必须只在 GODOT 宏内引用 Godot API。");
        }

        /// <summary>
        /// 获取指定门面的静态总线字段，缺失时给出清晰的迁移失败信息。
        /// </summary>
        /// <param name="ownerTypeName">EventKit 门面的完整类型名。</param>
        /// <param name="fieldName">静态字段名。</param>
        /// <returns>静态总线实例。</returns>
        private static object RequireStaticBus(string ownerTypeName, string fieldName)
        {
            Type ownerType = Type.GetType(ownerTypeName + ", " + CORE_ASSEMBLY_NAME);
            Assert.IsNotNull(ownerType, "缺少 EventKit 门面类型: " + ownerTypeName);
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, ownerTypeName + " 缺少静态总线字段: " + fieldName);
            object bus = field.GetValue(null);
            Assert.IsNotNull(bus, ownerTypeName + "." + fieldName + " 未初始化。");
            return bus;
        }

        /// <summary>
        /// 从 EventKit 程序集获取指定类型，缺失时给出清晰断言。
        /// </summary>
        /// <param name="typeName">完整类型名。</param>
        /// <returns>已解析类型。</returns>
        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName + ", " + CORE_ASSEMBLY_NAME);
            Assert.IsNotNull(type, "缺少 EventKit 类型: " + typeName);
            return type;
        }

        /// <summary>
        /// 调用实例上的普通方法，并按实参数量与类型选择重载。
        /// </summary>
        /// <param name="target">目标实例。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="arguments">调用参数。</param>
        /// <returns>方法返回值。</returns>
        private static object InvokeInstance(object target, string methodName, object[] arguments)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, 0, Type.EmptyTypes, arguments);
            return method.Invoke(target, BuildInvocationArguments(method, arguments));
        }

        /// <summary>
        /// 调用实例上的泛型方法，并自动补齐可能存在的可选参数。
        /// </summary>
        /// <param name="target">目标实例。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="genericTypes">泛型实参。</param>
        /// <param name="arguments">显式调用参数。</param>
        /// <returns>方法返回值。</returns>
        private static object InvokeGeneric(object target, string methodName, Type[] genericTypes, object[] arguments)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, genericTypes.Length, genericTypes, arguments);
            return method.MakeGenericMethod(genericTypes).Invoke(target, BuildInvocationArguments(method, arguments));
        }

        /// <summary>
        /// 调用 EventKit 注销令牌，缺失接口实现时给出明确失败。
        /// </summary>
        /// <param name="token">Register 返回的注销令牌。</param>
        private static void InvokeUnRegister(object token)
        {
            Assert.IsNotNull(token, "EventKit.Register 必须返回可注销令牌。");
            MethodInfo method = token.GetType().GetMethod("UnRegister", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "注销令牌缺少 UnRegister 方法。");
            method.Invoke(token, Array.Empty<object>());
        }

        /// <summary>
        /// 调用 Unity EventKit 生命周期扩展，把注销令牌绑定到指定 GameObject。
        /// </summary>
        /// <param name="methodName">扩展方法名。</param>
        /// <param name="token">EventKit 注册返回的注销令牌。</param>
        /// <param name="gameObject">用于触发生命周期的 GameObject。</param>
        private static void InvokeUnityUnregisterExtension(string methodName, object token, GameObject gameObject)
        {
            Type extensionType = Type.GetType("YokiFrame.UnityEventKitUnRegisterExtensions, YokiFrame.Unity.Runtime");
            Assert.IsNotNull(extensionType, "缺少 Unity EventKit 生命周期注销扩展。");
            MethodInfo method = FindMethod(extensionType, methodName, 1, new[] { token.GetType() }, new object[] { token, gameObject });
            method.MakeGenericMethod(token.GetType()).Invoke(null, new object[] { token, gameObject });
        }

        /// <summary>
        /// 在指定类型中查找与泛型数量、实参类型和可选参数兼容的方法。
        /// </summary>
        /// <param name="ownerType">要查找的类型。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="genericArgumentCount">泛型参数数量。</param>
        /// <param name="genericTypes">泛型实参，用于匹配泛型委托参数。</param>
        /// <param name="arguments">显式调用参数。</param>
        /// <returns>匹配到的方法。</returns>
        private static MethodInfo FindMethod(Type ownerType, string methodName, int genericArgumentCount, Type[] genericTypes, object[] arguments)
        {
            MethodInfo fallback = null;
            foreach (MethodInfo method in ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!IsCandidate(method, methodName, genericArgumentCount, genericTypes, arguments))
                {
                    continue;
                }

                if (method.GetParameters().Length == arguments.Length)
                {
                    return method;
                }

                fallback = method;
            }

            Assert.IsNotNull(fallback, "未找到兼容方法: " + ownerType.FullName + "." + methodName);
            return fallback;
        }

        /// <summary>
        /// 判断候选方法是否与当前反射调用请求兼容。
        /// </summary>
        /// <param name="method">候选方法。</param>
        /// <param name="methodName">目标方法名。</param>
        /// <param name="genericArgumentCount">泛型参数数量。</param>
        /// <param name="genericTypes">泛型实参。</param>
        /// <param name="arguments">显式调用参数。</param>
        /// <returns>兼容返回 true。</returns>
        private static bool IsCandidate(MethodInfo method, string methodName, int genericArgumentCount, Type[] genericTypes, object[] arguments)
        {
            if (method.Name != methodName || method.GetGenericArguments().Length != genericArgumentCount)
            {
                return false;
            }

            return ParametersMatch(method, genericTypes, arguments);
        }

        /// <summary>
        /// 检查参数数量、可选参数和显式实参类型是否匹配。
        /// </summary>
        /// <param name="method">候选方法。</param>
        /// <param name="genericTypes">泛型实参。</param>
        /// <param name="arguments">显式调用参数。</param>
        /// <returns>匹配返回 true。</returns>
        private static bool ParametersMatch(MethodInfo method, Type[] genericTypes, object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            int requiredCount = CountRequiredParameters(parameters);
            if (arguments.Length < requiredCount || arguments.Length > parameters.Length)
            {
                return false;
            }

            for (var index = 0; index < arguments.Length; index++)
            {
                Type parameterType = ResolveParameterType(parameters[index].ParameterType, method.GetGenericArguments(), genericTypes);
                if (arguments[index] != null && !parameterType.IsInstanceOfType(arguments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 统计非可选参数数量，避免可选参数影响反射调用。
        /// </summary>
        /// <param name="parameters">方法参数列表。</param>
        /// <returns>非可选参数数量。</returns>
        private static int CountRequiredParameters(ParameterInfo[] parameters)
        {
            var count = 0;
            for (var index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOptional)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 把泛型参数替换为当前泛型实参，用于判断委托参数是否兼容。
        /// </summary>
        /// <param name="parameterType">原始参数类型。</param>
        /// <param name="genericDefinitions">方法泛型形参。</param>
        /// <param name="genericTypes">方法泛型实参。</param>
        /// <returns>替换后的参数类型。</returns>
        private static Type ResolveParameterType(Type parameterType, Type[] genericDefinitions, Type[] genericTypes)
        {
            if (parameterType.IsGenericParameter)
            {
                return genericTypes[Array.IndexOf(genericDefinitions, parameterType)];
            }

            if (!parameterType.ContainsGenericParameters)
            {
                return parameterType;
            }

            Type[] originalArguments = parameterType.GetGenericArguments();
            var resolvedArguments = new Type[originalArguments.Length];
            for (var index = 0; index < originalArguments.Length; index++)
            {
                resolvedArguments[index] = ResolveParameterType(originalArguments[index], genericDefinitions, genericTypes);
            }

            return parameterType.GetGenericTypeDefinition().MakeGenericType(resolvedArguments);
        }

        /// <summary>
        /// 为反射调用构造完整参数数组，并用 Type.Missing 填充可选参数。
        /// </summary>
        /// <param name="method">目标方法。</param>
        /// <param name="arguments">显式实参。</param>
        /// <returns>与方法签名长度一致的实参数组。</returns>
        private static object[] BuildInvocationArguments(MethodInfo method, object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var finalArguments = new object[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                finalArguments[index] = index < arguments.Length ? arguments[index] : Type.Missing;
            }

            return finalArguments;
        }
    }
}
