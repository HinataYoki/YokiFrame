using System.Reflection;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed class LiveMethodInvokerTests
{
    /// <summary>调用公开方法并保留业务异常。失败后实例状态不变，不调用其他方法。</summary>
    [Fact]
    public void Calls_public_method_and_preserves_business_exception()
    {
        var instance = new Probe();
        var invoker = new LiveMethodInvoker(instance);
        Assert.Equal(7, invoker.Invoke("Hit", new object[] { 3 }));
        Assert.Equal(7, instance.Value);
        Assert.Throws<InvalidOperationException>(() => invoker.Invoke("Fail", Array.Empty<object>()));
        Assert.Equal(7, instance.Value);
    }

    /// <summary>私有、泛型、参数类型不匹配和歧义调用在执行前被拒绝。探针值保持初始值。</summary>
    [Fact]
    public void Rejects_private_generic_wrong_type_and_ambiguous_calls_before_execution()
    {
        var instance = new Probe();
        var invoker = new LiveMethodInvoker(instance);
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Hidden", Array.Empty<object>()));
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Generic", new object[] { 1 }));
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Hit", new object[] { 1.0 }));
        Assert.Throws<AmbiguousMatchException>(() => invoker.Invoke("Ambiguous", new object[] { "text" }));
        Assert.Equal(10, instance.Value);
    }

    public sealed class Probe
    {
        public int Value = 10;

        /// <summary>从当前值减去入参并返回结果。会改写 <see cref="Value"/>。</summary>
        /// <param name="value">要减去的量。</param>
        /// <returns>减法之后的值。</returns>
        public int Hit(int value) => Value -= value;

        /// <summary>抛出业务异常。不修改 <see cref="Value"/>。</summary>
        public void Fail() => throw new InvalidOperationException("business fault");

        /// <summary>私有方法，调用器不得执行。若被执行会把 <see cref="Value"/> 减一。</summary>
        private void Hidden() => Value--;

        /// <summary>泛型方法，调用器不得按普通参数执行。若被执行会把 <see cref="Value"/> 减一。</summary>
        /// <param name="value">未使用的泛型参数。</param>
        public void Generic<T>(T value) => Value--;

        /// <summary>与字符串重载同名，用于制造歧义。若被执行会把 <see cref="Value"/> 减一。</summary>
        /// <param name="value">未使用的对象参数。</param>
        public void Ambiguous(object value) => Value--;

        /// <summary>与对象重载同名，用于制造歧义。若被执行会把 <see cref="Value"/> 减一。</summary>
        /// <param name="value">未使用的字符串参数。</param>
        public void Ambiguous(string value) => Value--;
    }
}
