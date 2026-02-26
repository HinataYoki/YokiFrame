#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi 数值扩展文档
    /// </summary>
    internal static class FluentApiDocNumeric
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "数值扩展",
                Description = "数值类型扩展方法，支持范围限制、范围判断等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "范围限制",
                        Code = @"// 整数范围限制
int health = damage.Clamp(0, maxHealth);
int level = exp.Clamp(1, 100);

// 浮点数范围限制
float volume = rawVolume.Clamp(0f, 1f);
float speed = inputSpeed.Clamp(-maxSpeed, maxSpeed);",
                        Explanation = "Clamp 将值限制在 [min, max] 范围内，超出范围返回边界值。"
                    },
                    new()
                    {
                        Title = "范围判断",
                        Code = @"// 整数范围判断
if (index.InRange(0, list.Count - 1))
{
    var item = list[index];
}

// 浮点数范围判断
if (progress.InRange(0f, 1f))
{
    UpdateProgress(progress);
}

// 实际应用：输入验证
public void SetVolume(float value)
{
    if (!value.InRange(0f, 1f))
    {
        Debug.LogWarning(""音量值超出范围"");
        return;
    }
    mVolume = value;
}",
                        Explanation = "InRange 判断值是否在 [min, max] 闭区间内，返回 bool。"
                    },
                    new()
                    {
                        Title = "类型转换",
                        Code = @"// 安全类型转换
var result = obj.As<Player>();  // 转换失败返回 null

// 类型判断并转换
if (obj.Is<Enemy>(out var enemy))
{
    enemy.TakeDamage(10);
}

// 等价于
if (obj is Enemy e)
{
    e.TakeDamage(10);
}",
                        Explanation = "As 和 Is 方法提供更流畅的类型转换语法。"
                    }
                }
            };
        }
    }
}
#endif
