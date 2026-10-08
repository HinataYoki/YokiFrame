#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using YokiFrame.Unity.Editor;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证脚本图标维护不会在安装包导入或域加载期间改写 MonoImporter。
    /// </summary>
    public sealed class YokiFrameKitScriptIconPostprocessorTests
    {
        /// <summary>
        /// SourceGuard：确认图标维护类型不是 AssetPostprocessor，且类型和方法没有域加载特性。
        /// 只读取特性，不调用私有方法；Unity 没有公开 API 能列出未注册的导入回调。
        /// </summary>
        [Test]
        public void SourceGuard_没有导入和域加载入口_不会自动改写图标()
        {
            Type type = typeof(YokiFrameKitScriptIconPostprocessor);
            Assert.IsFalse(typeof(AssetPostprocessor).IsAssignableFrom(type));
            Assert.AreEqual(0, type.GetCustomAttributes(typeof(InitializeOnLoadAttribute), false).Length);

            const BindingFlags flags = BindingFlags.DeclaredOnly
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            MethodInfo[] methods = type.GetMethods(flags);
            for (int index = 0; index < methods.Length; index++)
            {
                Assert.AreEqual(
                    0,
                    methods[index].GetCustomAttributes(typeof(InitializeOnLoadMethodAttribute), false).Length,
                    methods[index].Name);
            }
        }
    }
}
#endif