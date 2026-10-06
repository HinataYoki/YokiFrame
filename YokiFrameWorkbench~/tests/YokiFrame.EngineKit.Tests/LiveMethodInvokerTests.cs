using System.Reflection;
using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

public sealed class LiveMethodInvokerTests
{
    [Fact]
    public void Calls_public_method_and_preserves_business_exception()
    {
        var instance = new Probe();
        var invoker = new YokiFrameLiveMethodInvoker(instance);
        Assert.Equal(7, invoker.Invoke("Hit", new object[] { 3 }));
        Assert.Equal(7, instance.Value);
        Assert.Throws<InvalidOperationException>(() => invoker.Invoke("Fail", Array.Empty<object>()));
        Assert.Equal(7, instance.Value);
    }

    [Fact]
    public void Rejects_private_generic_wrong_type_and_ambiguous_calls_before_execution()
    {
        var instance = new Probe();
        var invoker = new YokiFrameLiveMethodInvoker(instance);
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Hidden", Array.Empty<object>()));
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Generic", new object[] { 1 }));
        Assert.Throws<MissingMethodException>(() => invoker.Invoke("Hit", new object[] { 1.0 }));
        Assert.Throws<AmbiguousMatchException>(() => invoker.Invoke("Ambiguous", new object[] { "text" }));
        Assert.Equal(10, instance.Value);
    }

    public sealed class Probe
    {
        public int Value = 10;
        public int Hit(int value) => Value -= value;
        public void Fail() => throw new InvalidOperationException("business fault");
        private void Hidden() => Value--;
        public void Generic<T>(T value) => Value--;
        public void Ambiguous(object value) => Value--;
        public void Ambiguous(string value) => Value--;
    }
}
