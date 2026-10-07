#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// inspect：按「根 + 选择器 + 路径」反射读取**活动对象上的真实值**，纯读、零编译、不落盘。
    /// </summary>
    /// <remarks>
    /// 为什么需要它（而不是 eval）：eval 要靠工程编译，只能在编辑器、且每次读都要写盘 + 编译；
    /// 而"查看运行时状态"是高频只读操作，必须是反射直读。
    /// 通用性来自两点：① 路径求值（成员 / 字典键 / 索引，可嵌套）；
    /// ② **可插拔的根**（<see cref="IInspectRootProvider"/>）——任何框架或游戏都能把自己的寻址方式注册进来，
    /// 核心不需要认识任何具体框架类型。
    /// </remarks>
    public sealed partial class RoslynInspectOperation : IRoslynOperation
    {
        /// <summary>动作名。</summary>
        public const string ACTION = "inspect";

        /// <summary>根名：已注册的服务/模型（由宿主通过 roots 提供）。</summary>
        public const string ROOT_SERVICE = "service";

        /// <summary>根名：类型上的静态成员。</summary>
        public const string ROOT_STATIC = "static";

        /// <summary>成员/集合默认返回上限。</summary>
        public const int DEFAULT_LIMIT = 50;

        /// <summary>成员/集合返回上限的最大值。</summary>
        public const int MAX_LIMIT = 200;

        /// <summary>无路径时展开的默认深度。</summary>
        public const int DEFAULT_DEPTH = 1;

        /// <summary>无路径时展开的最大深度。</summary>
        public const int MAX_DEPTH = 3;

        /// <summary>文本值最大长度。</summary>
        public const int MAX_TEXT_CHARS = 256;

        private readonly InspectRootRegistry mRoots;

        /// <summary>创建 inspect 操作。</summary>
        /// <param name="roots">根的注册表；为空时使用内置根。</param>
        public RoslynInspectOperation(InspectRootRegistry roots = null)
        {
            mRoots = roots ?? InspectRootRegistry.CreateDefault();
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: true,
                isCancellation: false,
                targets: RoslynExecutionTargets.ALL,
                isTargetAgnostic: true);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行 inspect。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!TryParse(request.PayloadJson, out InspectRequest parsed, out string parseError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, parseError);
            }

            if (!TryResolveRoot(parsed, out object instance, out string rootError))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INSPECT_TARGET_NOT_FOUND, rootError);
            }

            var builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("root", parsed.Root)
                .Property("selector", parsed.Selector);
            if (instance == null)
            {
                // 静态根：直接读类型上的静态成员。
                return WriteStatic(parsed, builder);
            }

            builder.Property("instanceType", instance.GetType().FullName);
            if (parsed.Path.Length == 0)
            {
                builder.Property("path", string.Empty).Name("members");
                WriteMemberSummary(builder, instance, parsed.Depth, parsed.Limit);
                return YokiFrameCommandResult.Success(builder.EndObject().ToString());
            }

            if (!TryWalk(instance, parsed.Path, parsed.Limit, out object value, out string memberError, out string resolvedPath))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INSPECT_MEMBER_NOT_FOUND, memberError);
            }

            builder.Property("path", resolvedPath)
                .Property("valueType", value == null ? "null" : value.GetType().FullName)
                .Name("value");
            WriteValue(builder, value, parsed.Limit);
            return YokiFrameCommandResult.Success(builder.EndObject().ToString());
        }

        /// <summary>对已解析的静态类型写成员摘要或路径值。类型不存在时返回目标未找到。</summary>
        /// <param name="parsed">已校验的请求。</param>
        /// <param name="builder">已写入操作名、根和选择器的 JSON。</param>
        /// <returns>成功结果，或类型不存在、成员不存在时的错误结果。</returns>
        private YokiFrameCommandResult WriteStatic(InspectRequest parsed, RoslynJsonBuilder builder)
        {
            Type type = ResolveType(parsed.Selector);
            if (type == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INSPECT_TARGET_NOT_FOUND,
                    "type was not found: " + parsed.Selector);
            }

            builder.Property("instanceType", type.FullName);
            if (parsed.Path.Length == 0)
            {
                builder.Property("path", string.Empty).Name("members");
                WriteMemberSummary(builder, null, parsed.Depth, parsed.Limit, type);
                return YokiFrameCommandResult.Success(builder.EndObject().ToString());
            }

            if (!TryWalk(null, parsed.Path, parsed.Limit, out object value, out string memberError, out string resolvedPath, type))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INSPECT_MEMBER_NOT_FOUND, memberError);
            }

            builder.Property("path", resolvedPath)
                .Property("valueType", value == null ? "null" : value.GetType().FullName)
                .Name("value");
            WriteValue(builder, value, parsed.Limit);
            return YokiFrameCommandResult.Success(builder.EndObject().ToString());
        }

        /// <summary>
        /// 解析根：**完全委托给注册表**。高层只认识「根名 + 选择器 → 实例」这一个契约。
        /// </summary>
        /// <param name="parsed">请求。</param>
        /// <param name="instance">解析到的实例；静态根为 null 且返回 true。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
    }
}
#endif
