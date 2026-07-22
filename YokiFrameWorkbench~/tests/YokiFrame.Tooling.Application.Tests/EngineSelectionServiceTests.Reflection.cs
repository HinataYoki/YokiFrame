using System.Reflection;
using YokiFrame.Tooling.Application.Engines;

namespace YokiFrame.Tooling.Application.Tests;

public sealed partial class EngineSelectionServiceTests
{
    /// <summary>
    /// 通过反射调用待落地的 Select API，使缺失契约表现为清晰的 RED 断言而不是编译错误。
    /// </summary>
    /// <param name="service">待验证的 engine 选择服务。</param>
    /// <param name="requestedEngineId">调用方请求的 engine 标识。</param>
    /// <param name="nowUtc">稳定 heartbeat 判定使用的当前时间。</param>
    /// <returns>Select 返回的结果对象。</returns>
    private static object InvokeSelect(
        EngineSelectionService service,
        string requestedEngineId,
        DateTimeOffset nowUtc)
    {
        var method = typeof(EngineSelectionService).GetMethod(
            "Select",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(string), typeof(DateTimeOffset) },
            null);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<object>(method.Invoke(service, new object[] { requestedEngineId, nowUtc }));
    }

    /// <summary>
    /// 读取待落地结果模型的公开属性，使缺失属性产生明确的契约失败。
    /// </summary>
    /// <typeparam name="T">预期属性类型。</typeparam>
    /// <param name="target">待读取的结果对象。</param>
    /// <param name="propertyName">公开属性名称。</param>
    /// <returns>断言为预期类型的属性值。</returns>
    private static T ReadProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property.GetValue(target));
    }

    /// <summary>
    /// 读取允许为 null 的公开属性，供成功结果断言未携带错误对象。
    /// </summary>
    /// <param name="target">待读取的结果对象。</param>
    /// <param name="propertyName">公开属性名称。</param>
    /// <returns>属性值。</returns>
    private static object? ReadNullableProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        return property.GetValue(target);
    }
}
