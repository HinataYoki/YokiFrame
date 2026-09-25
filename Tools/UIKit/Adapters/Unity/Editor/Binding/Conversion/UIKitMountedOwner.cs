#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>解析节点上唯一的已生成 UIElement/UIComponent，避免派生关系导致取到错误身份。</summary>
    internal static class UIKitMountedOwner
    {
        /// <summary>
        /// 读取节点上可解析的唯一生成 owner。
        /// 同一节点同时挂 Element 与 Component 时拒绝猜测。
        /// </summary>
        /// <param name="bind">待生成或转换的绑定。</param>
        /// <param name="owner">唯一可解析的生成脚本；没有时为 null。</param>
        /// <exception cref="InvalidOperationException">节点挂了多份生成脚本，或脚本路径无法解析。</exception>
        internal static void RequireSingle(AbstractBind bind, out UIElement owner)
        {
            owner = null;
            if (bind == default) return;
            UIElement[] mounted = bind.GetComponents<UIElement>();
            List<UIElement> generated = new(mounted.Length);
            for (var index = 0; index < mounted.Length; index++)
            {
                UIElement candidate = mounted[index];
                if (candidate == default || candidate.GetType() == null) continue;
                if (!typeof(UIElement).IsAssignableFrom(candidate.GetType())) continue;
                string path = UIKitGeneratedOwnerCodeService.GetScriptPath(candidate);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("已挂载生成脚本无法解析路径: " + candidate.GetType().FullName);
                generated.Add(candidate);
            }

            if (generated.Count > 1)
                throw new InvalidOperationException(DescribeConflict(generated));
            if (generated.Count == 1) owner = generated[0];
        }

        /// <summary>把冲突的多份挂载身份写成可定位的错误，不挑选其中一份继续。</summary>
        private static string DescribeConflict(List<UIElement> generated)
        {
            List<string> paths = new(generated.Count);
            for (var index = 0; index < generated.Count; index++)
                paths.Add(UIKitGeneratedOwnerCodeService.GetScriptPath(generated[index]));
            return "同一节点挂了多份生成脚本，不能判断它是改名、换位还是重复挂载: " + string.Join("；", paths);
        }
    }
}
#endif
