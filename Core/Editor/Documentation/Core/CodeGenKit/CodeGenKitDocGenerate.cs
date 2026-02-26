#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 生成代码文档
    /// </summary>
    internal static class CodeGenKitDocGenerate
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "代码生成",
                Description = "使用 RootCode 作为根节点，通过 Fluent API 链式调用构建代码结构，最后输出到文件或字符串。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法 - RootCode",
                        Code = @"// RootCode 是代码生成的根节点，管理 using 和顶层代码
var root = new RootCode();

// 添加 using 声明
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

// 添加命名空间
root.Namespace(""MyGame"", ns =>
{
    // 在命名空间内添加类
    ns.Class(""PlayerController"", ""MonoBehaviour"", true, false, cls =>
    {
        // 在类内添加成员
        cls.Codes.Add(new CustomCode(""public float Speed = 5f;""));
    });
});

// 输出到文件
using var writer = new FileCodeWriteKit(""Assets/Scripts/PlayerController.cs"");
root.Gen(writer);",
                        Explanation = "RootCode 是代码生成的入口点，所有代码都添加到它的 Codes 列表中。"
                    },
                    new()
                    {
                        Title = "使用扩展方法简化",
                        Code = @"// ICodeScopeExtensions 提供了大量扩展方法简化代码生成

var root = new RootCode();

// 链式添加 using
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""System.Collections.Generic""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

// 使用扩展方法添加命名空间和类
root.Namespace(""MyGame.Player"", ns =>
{
    // 添加注释
    ns.Summary(""玩家数据类"");
    
    // 添加类
    ns.Class(""PlayerData"", cls =>
    {
        // 配置类
        cls.WithAccess(AccessModifier.Public)
           .WithInterface(""ISerializationCallbackReceiver"");
    }, cls =>
    {
        // 添加字段
        cls.SerializeField(""int"", ""mHealth"", ""玩家生命值"");
        cls.SerializeField(""float"", ""mSpeed"", ""移动速度"");
        cls.PrivateField(""bool"", ""mIsAlive"", ""true"");
        
        cls.EmptyLine();
        
        // 添加属性
        cls.AutoProperty(""string"", ""Name"", true, ""玩家名称"");
        cls.ReadonlyProperty(""bool"", ""IsAlive"", ""mHealth > 0"", ""是否存活"");
    });
});",
                        Explanation = "扩展方法让代码生成更加简洁，支持链式调用。"
                    },
                    new()
                    {
                        Title = "生成的代码示例",
                        Code = @"// 上述代码生成结果：

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Player
{
    /// <summary>
    /// 玩家数据类
    /// </summary>
    public class PlayerData : ISerializationCallbackReceiver
    {
        /// <summary>
        /// 玩家生命值
        /// </summary>
        [SerializeField]
        public int mHealth;

        /// <summary>
        /// 移动速度
        /// </summary>
        [SerializeField]
        public float mSpeed;

        private bool mIsAlive = true;

        /// <summary>
        /// 玩家名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive => mHealth > 0;
    }
}",
                        Explanation = "生成的代码自动处理缩进、换行和格式化。"
                    }
                }
            };
        }
    }
}
#endif
