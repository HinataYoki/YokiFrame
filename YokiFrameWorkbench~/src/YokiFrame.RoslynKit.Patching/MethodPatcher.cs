using System;
using System.Reflection;
using HarmonyLib;

namespace YokiFrame.RoslynKit.Patching
{
    /// <summary>Only BCL types cross the optional HarmonyX bundle boundary.</summary>
    public static class MethodPatcher
    {
        /// <summary>按模式把补丁挂到原方法。失败时卸下本次补丁并重新抛出原异常，不吞掉堆栈。</summary>
        /// <param name="owner">Harmony 实例标识。</param>
        /// <param name="original">被补丁的原方法。</param>
        /// <param name="patch">补丁方法。</param>
        /// <param name="mode">prefix、postfix 或 replace。replace 与 prefix 一样作为前缀补丁。</param>
        public static void Apply(string owner, MethodInfo original, MethodInfo patch, string mode)
        {
            var harmony = new Harmony(owner);
            var method = new HarmonyMethod(patch);
            try
            {
                if (mode == "prefix" || mode == "replace")
                    harmony.Patch(original, prefix: method);
                else if (mode == "postfix")
                    harmony.Patch(original, postfix: method);
                else
                    throw new ArgumentException("Expected prefix, postfix or replace.", nameof(mode));
            }
            catch
            {
                harmony.Unpatch(original, patch);
                throw;
            }
        }

        /// <summary>卸下指定 owner 下由该补丁方法挂上的补丁。不保留额外 Harmony 状态。</summary>
        /// <param name="owner">挂载时使用的 Harmony 实例标识。</param>
        /// <param name="original">被补丁的原方法。</param>
        /// <param name="patch">要卸下的补丁方法。</param>
        public static void Remove(string owner, MethodInfo original, MethodInfo patch)
        {
            new Harmony(owner).Unpatch(original, patch);
        }
    }
}
