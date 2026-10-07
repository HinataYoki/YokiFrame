using System;
using System.Reflection;
using HarmonyLib;

namespace YokiFrame.RoslynKit.Patching
{
    /// <summary>Only BCL types cross the optional HarmonyX bundle boundary.</summary>
    public static class MethodPatcher
    {
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

        public static void Remove(string owner, MethodInfo original, MethodInfo patch)
        {
            new Harmony(owner).Unpatch(original, patch);
        }
    }
}
