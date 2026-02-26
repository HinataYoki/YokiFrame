#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 特性和注释生成文档
    /// </summary>
    internal static class CodeGenKitDocAttributes
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "特性与注释",
                Description = "CodeGenKit 提供 AttributeCode 和 CommentCode 用于生成 C# 特性和注释。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "AttributeCode - 特性生成",
                        Code = @"// 无参数特性
var attr1 = new AttributeCode(""Serializable"");
// 生成: [Serializable]

// 单参数特性
var attr2 = new AttributeCode(""Header"")
    .WithArgument(""\""基础信息\"""");
// 生成: [Header(""基础信息"")]

// 多参数特性
var attr3 = new AttributeCode(""Range"")
    .WithArgument(""0"")
    .WithArgument(""100"");
// 生成: [Range(0, 100)]

// 命名参数特性
var attr4 = new AttributeCode(""CreateAssetMenu"")
    .WithNamedArgument(""fileName"", ""\""MyConfig\"""")
    .WithNamedArgument(""menuName"", ""\""Config/MyConfig\"""");
// 生成: [CreateAssetMenu(fileName = ""MyConfig"", menuName = ""Config/MyConfig"")]

// 混合参数
var attr5 = new AttributeCode(""MenuItem"")
    .WithArgument(""\""Tools/MyTool\"""")
    .WithArgument(""false"")
    .WithArgument(""100"");
// 生成: [MenuItem(""Tools/MyTool"", false, 100)]",
                        Explanation = "AttributeCode 支持无参、位置参数和命名参数三种形式。"
                    },
                    new()
                    {
                        Title = "在成员上添加特性",
                        Code = @"// 字段特性
cls.Field(""int"", ""mHealth"", field =>
{
    field.WithAttribute(""SerializeField"")
         .WithAttribute(""Range"", ""0, 100"")
         .WithAttribute(""Tooltip"", ""\""玩家生命值\"""");
});
// 生成:
// [SerializeField]
// [Range(0, 100)]
// [Tooltip(""玩家生命值"")]
// private int mHealth;

// 方法特性
cls.Method(""void"", ""OnValidate"", method =>
{
    method.WithAttribute(""ContextMenu"", ""\""验证数据\"""")
          .WithBody(body => body.Custom(""// 验证逻辑""));
});
// 生成:
// [ContextMenu(""验证数据"")]
// public void OnValidate()
// {
//     // 验证逻辑
// }

// 类特性
ns.Class(""GameConfig"", cls =>
{
    cls.WithAttribute(""Serializable"")
       .WithAttribute(""CreateAssetMenu"", 
           ""fileName = \""GameConfig\"", menuName = \""Config/Game\"""");
}, cls =>
{
    // 类成员...
});",
                        Explanation = "特性可以添加到字段、属性、方法和类上。"
                    },
                    new()
                    {
                        Title = "CommentCode - 注释生成",
                        Code = @"// 单行注释
cls.Comment(""这是单行注释"");
// 生成: // 这是单行注释

// XML Summary 注释
cls.Summary(""这是类的摘要说明"");
// 生成:
// /// <summary>
// /// 这是类的摘要说明
// /// </summary>

// XML Param 注释
cls.Param(""damage"", ""伤害值"");
// 生成: /// <param name=""damage"">伤害值</param>

// XML Returns 注释
cls.Returns(""是否成功"");
// 生成: /// <returns>是否成功</returns>",
                        Explanation = "CommentCode 支持单行注释和 XML 文档注释。"
                    },
                    new()
                    {
                        Title = "成员自动注释",
                        Code = @"// 字段注释（使用 WithComment）
cls.Field(""int"", ""mScore"", field =>
{
    field.WithComment(""玩家分数"");
});
// 生成:
// /// <summary>
// /// 玩家分数
// /// </summary>
// private int mScore;

// 属性注释
cls.Property(""bool"", ""IsReady"", prop =>
{
    prop.WithComment(""是否准备就绪"")
        .WithExpressionBody(""mInitialized && mDataLoaded"");
});
// 生成:
// /// <summary>
// /// 是否准备就绪
// /// </summary>
// public bool IsReady => mInitialized && mDataLoaded;

// 方法注释
cls.Method(""void"", ""TakeDamage"", method =>
{
    method.WithComment(""受到伤害"")
          .WithParameter(""int"", ""damage"")
          .WithParameter(""DamageType"", ""type"")
          .WithBody(body => body.Custom(""// 处理伤害""));
});
// 生成:
// /// <summary>
// /// 受到伤害
// /// </summary>
// public void TakeDamage(int damage, DamageType type)
// {
//     // 处理伤害
// }",
                        Explanation = "使用 WithComment 方法可以为成员自动生成 XML 文档注释。"
                    },
                    new()
                    {
                        Title = "Region 块",
                        Code = @"// 使用 Region 组织代码
cls.Region(""字段"", region =>
{
    region.SerializeField(""int"", ""mHealth"", ""生命值"");
    region.SerializeField(""float"", ""mSpeed"", ""速度"");
});

cls.Region(""属性"", region =>
{
    region.AutoProperty(""string"", ""Name"", true);
    region.ReadonlyProperty(""bool"", ""IsAlive"", ""mHealth > 0"");
});

cls.Region(""方法"", region =>
{
    region.VoidMethod(""Initialize"", m => m.WithBody(b => b.Custom(""// 初始化"")));
});

// 生成:
// #region 字段
//
// [SerializeField]
// public int mHealth;
// ...
//
// #endregion
//
// #region 属性
// ...
// #endregion",
                        Explanation = "Region 扩展方法可以将代码组织成可折叠的区块。"
                    },
                    new()
                    {
                        Title = "完整示例",
                        Code = @"var root = new RootCode();
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

root.Namespace(""MyGame"", ns =>
{
    ns.Summary(""敌人配置数据"");
    
    ns.Class(""EnemyConfig"", cls =>
    {
        cls.WithAttribute(""CreateAssetMenu"", 
            ""fileName = \""EnemyConfig\"", menuName = \""Config/Enemy\"""");
    }, cls =>
    {
        cls.Region(""基础属性"", r =>
        {
            r.Field(""string"", ""mEnemyName"", f =>
            {
                f.WithAttribute(""SerializeField"")
                 .WithAttribute(""Header"", ""\""基础信息\"""")
                 .WithComment(""敌人名称"");
            });
            
            r.Field(""int"", ""mMaxHealth"", f =>
            {
                f.WithAttribute(""SerializeField"")
                 .WithAttribute(""Range"", ""1, 1000"")
                 .WithComment(""最大生命值"")
                 .WithDefaultValue(""100"");
            });
        });
        
        cls.EmptyLine();
        
        cls.Region(""属性访问器"", r =>
        {
            r.ReadonlyProperty(""string"", ""EnemyName"", ""mEnemyName"");
            r.ReadonlyProperty(""int"", ""MaxHealth"", ""mMaxHealth"");
        });
    });
});",
                        Explanation = "完整示例展示了特性、注释和 Region 的综合使用。"
                    }
                }
            };
        }
    }
}
#endif
