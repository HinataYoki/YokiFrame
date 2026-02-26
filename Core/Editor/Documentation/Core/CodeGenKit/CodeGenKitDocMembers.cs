#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 成员生成文档
    /// </summary>
    internal static class CodeGenKitDocMembers
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "成员生成",
                Description = "CodeGenKit 提供 FieldCode、PropertyCode、MethodCode 三种成员代码类型，支持 Fluent API 风格的链式调用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "FieldCode - 字段生成",
                        Code = @"// 基础字段生成
cls.Field(""int"", ""mCount"", field =>
{
    field.WithAccess(AccessModifier.Private)
         .WithDefaultValue(""0"");
});
// 生成: private int mCount = 0;

// 带注释和特性的字段
cls.Field(""float"", ""mSpeed"", field =>
{
    field.WithAccess(AccessModifier.Private)
         .WithComment(""移动速度"")
         .WithAttribute(""SerializeField"")
         .WithAttribute(""Range"", ""0, 100"")
         .WithDefaultValue(""5f"");
});
// 生成:
// /// <summary>
// /// 移动速度
// /// </summary>
// [SerializeField]
// [Range(0, 100)]
// private float mSpeed = 5f;

// 静态只读字段
cls.Field(""string"", ""VERSION"", field =>
{
    field.WithAccess(AccessModifier.Public)
         .WithModifiers(MemberModifier.Static | MemberModifier.Readonly)
         .WithDefaultValue(""\""1.0.0\"""");
});
// 生成: public static readonly string VERSION = ""1.0.0"";",
                        Explanation = "FieldCode 支持访问修饰符、成员修饰符、默认值、注释和特性。"
                    },
                    new()
                    {
                        Title = "字段快捷方法",
                        Code = @"// PublicField - 公共字段
cls.PublicField(""int"", ""Score"", ""玩家分数"");
// 生成:
// /// <summary>
// /// 玩家分数
// /// </summary>
// public int Score;

// PrivateField - 私有字段（可带默认值）
cls.PrivateField(""bool"", ""mIsActive"", ""false"");
// 生成: private bool mIsActive = false;

// SerializeField - 带 [SerializeField] 特性的字段
cls.SerializeField(""Button"", ""mStartBtn"", ""开始按钮"");
// 生成:
// /// <summary>
// /// 开始按钮
// /// </summary>
// [SerializeField]
// public Button mStartBtn;",
                        Explanation = "快捷方法简化常见字段的生成，减少重复代码。"
                    },
                    new()
                    {
                        Title = "PropertyCode - 属性生成",
                        Code = @"// 自动属性
cls.Property(""string"", ""Name"", prop =>
{
    prop.AsAutoProperty();
});
// 生成: public string Name { get; set; }

// 只读自动属性
cls.Property(""int"", ""Id"", prop =>
{
    prop.AsReadonly();
});
// 生成: public int Id { get; }

// 表达式体属性
cls.Property(""bool"", ""IsValid"", prop =>
{
    prop.WithExpressionBody(""mCount > 0"");
});
// 生成: public bool IsValid => mCount > 0;

// 完整属性（带 getter/setter 体）
cls.Property(""int"", ""Health"", prop =>
{
    prop.WithComment(""玩家生命值"")
        .WithGetter(getter =>
        {
            getter.Custom(""return mHealth;"");
        })
        .WithSetter(setter =>
        {
            setter.Custom(""mHealth = Mathf.Max(0, value);"");
        });
});
// 生成:
// /// <summary>
// /// 玩家生命值
// /// </summary>
// public int Health
// {
//     get
//     {
//         return mHealth;
//     }
//     set
//     {
//         mHealth = Mathf.Max(0, value);
//     }
// }",
                        Explanation = "PropertyCode 支持自动属性、只读属性、表达式体属性和完整属性。"
                    },
                    new()
                    {
                        Title = "属性快捷方法",
                        Code = @"// AutoProperty - 自动属性
cls.AutoProperty(""string"", ""Name"", true, ""玩家名称"");
// 参数: 类型, 名称, 是否有 setter, 注释
// 生成:
// /// <summary>
// /// 玩家名称
// /// </summary>
// public string Name { get; set; }

// ReadonlyProperty - 只读表达式体属性
cls.ReadonlyProperty(""bool"", ""IsAlive"", ""mHealth > 0"", ""是否存活"");
// 生成:
// /// <summary>
// /// 是否存活
// /// </summary>
// public bool IsAlive => mHealth > 0;",
                        Explanation = "属性快捷方法简化常见属性模式的生成。"
                    },
                    new()
                    {
                        Title = "MethodCode - 方法生成",
                        Code = @"// 基础方法
cls.Method(""void"", ""Initialize"", method =>
{
    method.WithAccess(AccessModifier.Public)
          .WithComment(""初始化方法"")
          .WithBody(body =>
          {
              body.Custom(""mIsInitialized = true;"");
              body.Custom(""OnInitialized();"");
          });
});
// 生成:
// /// <summary>
// /// 初始化方法
// /// </summary>
// public void Initialize()
// {
//     mIsInitialized = true;
//     OnInitialized();
// }

// 带参数的方法
cls.Method(""int"", ""Calculate"", method =>
{
    method.WithAccess(AccessModifier.Public)
          .WithParameter(""int"", ""a"")
          .WithParameter(""int"", ""b"", ""0"")  // 带默认值
          .WithComment(""计算两数之和"")
          .WithBody(body =>
          {
              body.Custom(""return a + b;"");
          });
});
// 生成:
// /// <summary>
// /// 计算两数之和
// /// </summary>
// public int Calculate(int a, int b = 0)
// {
//     return a + b;
// }

// 表达式体方法
cls.Method(""bool"", ""IsValid"", method =>
{
    method.WithExpressionBody(""mData != null && mData.Count > 0"");
});
// 生成: public bool IsValid() => mData != null && mData.Count > 0;",
                        Explanation = "MethodCode 支持参数、默认值、方法体和表达式体。"
                    },
                    new()
                    {
                        Title = "方法快捷方法",
                        Code = @"// VoidMethod - void 返回类型的方法
cls.VoidMethod(""DoSomething"", method =>
{
    method.WithBody(body => body.Custom(""// 实现...""));
});

// OverrideMethod - override 方法
cls.OverrideMethod(""bool"", ""CanClose"", method =>
{
    method.WithBody(body => body.Custom(""return true;""));
});
// 生成: protected override bool CanClose() { return true; }

// ProtectedOverrideVoid - protected override void 方法
cls.ProtectedOverrideVoid(""OnInit"", method =>
{
    method.WithParameter(""IUIData"", ""data"", ""null"")
          .WithBody(body =>
          {
              body.Custom(""base.OnInit(data);"");
              body.Custom(""// 初始化逻辑..."");
          });
});
// 生成:
// protected override void OnInit(IUIData data = null)
// {
//     base.OnInit(data);
//     // 初始化逻辑...
// }",
                        Explanation = "方法快捷方法简化常见方法模式，特别是 Unity 生命周期方法的重写。"
                    },
                    new()
                    {
                        Title = "泛型方法",
                        Code = @"// 带泛型参数的方法
cls.Method(""T"", ""GetComponent"", method =>
{
    method.WithAccess(AccessModifier.Public)
          .WithGenericParameter(""T"", ""Component"")  // 泛型参数和约束
          .WithBody(body =>
          {
              body.Custom(""return gameObject.GetComponent<T>();"");
          });
});
// 生成:
// public T GetComponent<T>() where T : Component
// {
//     return gameObject.GetComponent<T>();
// }

// 多泛型参数
cls.Method(""TResult"", ""Convert"", method =>
{
    method.WithGenericParameter(""TInput"")
          .WithGenericParameter(""TResult"")
          .WithParameter(""TInput"", ""input"")
          .WithParameter(""Func<TInput, TResult>"", ""converter"")
          .WithBody(body =>
          {
              body.Custom(""return converter(input);"");
          });
});",
                        Explanation = "MethodCode 支持泛型参数和泛型约束。"
                    }
                }
            };
        }
    }
}
#endif
