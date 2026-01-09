#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass BindValue 数据绑定文档
    /// </summary>
    internal static class ToolClassDocBindValue
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "BindValue 数据绑定",
                Description = "响应式数据绑定，当值变化时自动通知所有监听者。适合 MVVM 模式的数据层。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本使用",
                        Code = @"public class PlayerData
{
    // 创建可绑定的属性
    public BindValue<int> Health = new(100);
    public BindValue<int> Gold = new(0);
    public BindValue<string> Name = new(""Player"");
}

// 绑定 UI 更新
var data = new PlayerData();

// 绑定回调（值变化时触发）
data.Health.Bind(hp => healthText.text = hp.ToString());
data.Gold.Bind(gold => goldText.text = gold.ToString());

// 绑定并立即触发一次
data.Name.BindWithCallback(name => nameText.text = name);

// 修改值会自动触发回调
data.Health.Value = 80;  // healthText 自动更新
data.Gold.Value += 100;  // goldText 自动更新

// 静默修改（不触发回调）
data.Health.SetValueWithoutEvent(50);

// 解绑
data.Health.UnBind(callback);
data.Health.UnBindAll();"
                    },
                    new()
                    {
                        Title = "隐式转换",
                        Code = @"BindValue<int> health = new(100);

// 隐式转换为值类型
int currentHealth = health;  // 等同于 health.Value

// 比较
if (health > 50) { }  // 自动转换"
                    }
                }
            };
        }
    }
}
#endif
