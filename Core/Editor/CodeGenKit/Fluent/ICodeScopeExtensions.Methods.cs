using System;

namespace YokiFrame
{
    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 追加可完整配置的方法声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="returnType">返回类型表达式。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="configure">可选方法配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope Method(
            this ICodeScope scope,
            string returnType,
            string methodName,
            Action<MethodCode> configure)
        {
            MethodCode method = new MethodCode(returnType, methodName);
            configure?.Invoke(method);
            CodeScopeAccess.Add(scope, method);
            return scope;
        }

        /// <summary>
        /// 追加返回类型为 void 的方法声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="configure">可选方法配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope VoidMethod(
            this ICodeScope scope,
            string methodName,
            Action<MethodCode> configure)
        {
            return scope.Method("void", methodName, configure);
        }

        /// <summary>
        /// 追加默认 protected override 的方法声明，配置回调仍可调整访问级别和其它状态。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="returnType">返回类型表达式。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="configure">可选方法配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope OverrideMethod(
            this ICodeScope scope,
            string returnType,
            string methodName,
            Action<MethodCode> configure)
        {
            MethodCode method = new MethodCode(returnType, methodName)
                .WithAccess(AccessModifier.Protected)
                .WithModifiers(MemberModifier.Override);
            configure?.Invoke(method);
            CodeScopeAccess.Add(scope, method);
            return scope;
        }

        /// <summary>
        /// 追加 protected override void 方法声明。
        /// </summary>
        /// <param name="scope">目标作用域。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="configure">可选方法配置回调。</param>
        /// <returns>原作用域。</returns>
        public static ICodeScope ProtectedOverrideVoid(
            this ICodeScope scope,
            string methodName,
            Action<MethodCode> configure)
        {
            return scope.OverrideMethod("void", methodName, configure);
        }
    }
}
