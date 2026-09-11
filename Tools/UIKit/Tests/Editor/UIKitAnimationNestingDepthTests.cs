#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 守护 UIKit 动画配置图的嵌套深度上限。
    /// Unity 默认 Inspector 允许手工拖拽出自引用的 SerializeReference 环，
    /// 若校验与创建路径无深度守卫，会以不可捕获的栈溢出终止 Editor/Player 进程。
    /// </summary>
    public sealed class UIKitAnimationNestingDepthTests
    {
        /// <summary>配置路径允许的最大嵌套层数，与运行时限值一致。</summary>
        private const int EXPECTED_MAX_DEPTH = 32;

        /// <summary>构造深度可控的链式组合配置。</summary>
        /// <param name="depth">链长。</param>
        /// <returns>最外层组合配置。</returns>
        private static CompositeAnimationConfig BuildNested(int depth)
        {
            var root = new CompositeAnimationConfig();
            CompositeAnimationConfig current = root;
            for (var index = 1; index < depth; index++)
            {
                var child = new CompositeAnimationConfig();
                current.Animations.Add(child);
                current = child;
            }

            return root;
        }

        /// <summary>递归统计运行时动画树的最大层数。</summary>
        /// <param name="animation">待测量的动画。</param>
        /// <returns>层数，单节点为 1。</returns>
        private static int MeasureDepth(IUIAnimation animation)
        {
            if (!(animation is CompositeAnimation composite)) return 1;

            FieldInfo field = typeof(CompositeAnimation).GetField(
                "mAnimations",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "未找到 CompositeAnimation.mAnimations");
            var children = (IList)field.GetValue(composite);

            int maxDepth = 1;
            for (var index = 0; index < children.Count; index++)
            {
                int childDepth = 1 + MeasureDepth((IUIAnimation)children[index]);
                if (childDepth > maxDepth) maxDepth = childDepth;
            }

            return maxDepth;
        }

        /// <summary>通过反射调用校验器的私有动画规则，避免依赖面板 GameObject 装配。</summary>
        /// <param name="config">待校验配置。</param>
        /// <returns>校验结果。</returns>
        private static UIPanelValidationResult Validate(UIAnimationConfig config)
        {
            MethodInfo method = null;
            MethodInfo[] candidates = typeof(UIPanelValidator).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic);
            for (var index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Name != "ValidateAnimationConfig") continue;
                if (candidates[index].GetParameters().Length < 4) continue;
                method = candidates[index];
                break;
            }

            Assert.IsNotNull(method, "未找到 UIPanelValidator.ValidateAnimationConfig");
            var result = new UIPanelValidationResult(null);
            object[] arguments = method.GetParameters().Length >= 5
                ? new object[] { config, "动画", null, result, 0 }
                : new object[] { config, "动画", null, result };
            _ = method.Invoke(null, arguments);
            return result;
        }

        /// <summary>核心回归：超过上限的深层配置创建出的动画树必须被截断。</summary>
        [Test]
        public void CreateAnimationStopsAtNestingLimit()
        {
            CompositeAnimationConfig config = BuildNested(100);

            IUIAnimation animation = config.CreateAnimation();

            Assert.LessOrEqual(
                MeasureDepth(animation),
                EXPECTED_MAX_DEPTH,
                "创建路径必须在嵌套上限处停止下钻");
        }

        /// <summary>核心回归：校验器必须对超过上限的深层配置报出错误而不是静默通过。</summary>
        [Test]
        public void ValidatorReportsNestingLimitExceeded()
        {
            UIPanelValidationResult result = Validate(BuildNested(100));

            Assert.IsTrue(
                result.HasErrors,
                "超过嵌套上限的配置必须产生错误级问题，否则用户无法得知配置已失效");
        }

        /// <summary>
        /// 确认场景：自引用组合配置（Unity 默认 Inspector 可手工拖拽产生）必须被截断而不是无界递归。
        /// </summary>
        [Test]
        public void CreateAnimationTerminatesOnSelfReferencingConfig()
        {
            var config = new CompositeAnimationConfig();
            config.Animations.Add(config);

            IUIAnimation animation = config.CreateAnimation();

            Assert.LessOrEqual(
                MeasureDepth(animation),
                EXPECTED_MAX_DEPTH,
                "自引用配置必须在嵌套上限处终止");
        }

        /// <summary>确认场景：校验器遇到自引用配置必须终止并报错，而不是无界递归。</summary>
        [Test]
        public void ValidatorTerminatesOnSelfReferencingConfig()
        {
            var config = new CompositeAnimationConfig();
            config.Animations.Add(config);

            UIPanelValidationResult result = Validate(config);

            Assert.IsTrue(result.HasErrors, "自引用配置必须被报为错误");
        }

        /// <summary>组合动画不得把自身加入自身，否则递归方法会无界递归。</summary>
        [Test]
        public void CompositeAnimationRejectsSelfReference()
        {
            var composite = new CompositeAnimation(CompositeMode.Parallel);

            Assert.Throws<InvalidOperationException>(() => composite.Add(composite));
        }

        /// <summary>组合动画不得形成间接环。</summary>
        [Test]
        public void CompositeAnimationRejectsIndirectCycle()
        {
            var first = new CompositeAnimation(CompositeMode.Parallel);
            var second = new CompositeAnimation(CompositeMode.Sequential);
            first.Add(second);

            Assert.Throws<InvalidOperationException>(() => second.Add(first));
        }

        /// <summary>合法嵌套必须保持原有可添加行为，守卫不得误伤正常配置。</summary>
        [Test]
        public void CompositeAnimationStillAcceptsNonCyclicChildren()
        {
            var outer = new CompositeAnimation(CompositeMode.Parallel);
            var inner = new CompositeAnimation(CompositeMode.Sequential);
            var leaf = new FadeAnimationConfig().CreateAnimation();

            _ = outer.Add(inner).Add(leaf);

            Assert.AreEqual(1, MeasureDepth(inner), "inner 仍应为单节点");
            Assert.AreEqual(2, MeasureDepth(outer), "outer 深度应为 2");
        }
    }
}
#endif
