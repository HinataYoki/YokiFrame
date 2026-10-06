#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 在 Policy 之后实施 Engine Kit 自己的策略：载荷与目标、执行开关、危险来源限制与目标绑定。
    /// </summary>
    /// <remarks>
    /// 判定顺序固定为：① 载荷与目标解析 → ② 执行开关（豁免只跳过这一层）→ ③ Dangerous 来源限制 → ④ 目标绑定。
    /// 该顺序是对外契约，调用方据此判断错误码先后关系。
    /// </remarks>
    public sealed class YokiFrameEngineGate
    {
        /// <summary>默认允许执行 Dangerous 操作的来源。</summary>
        public static readonly string[] DEFAULT_DANGEROUS_SOURCES =
        {
            YokiFrameCommandSourceContract.CLI,
            YokiFrameCommandSourceContract.WORKBENCH
        };

        private const string TARGET_FIELD_NAME = "target";

        private readonly IYokiFrameEngineSettingsSource mSettingsSource;
        private readonly string[] mDangerousSources;

        /// <summary>
        /// 创建 Gate。
        /// </summary>
        /// <param name="settingsSource">执行开关读取端口。</param>
        /// <param name="dangerousSources">允许执行 Dangerous 操作的来源；为空表示不允许任何来源。</param>
        public YokiFrameEngineGate(
            IYokiFrameEngineSettingsSource settingsSource,
            string[] dangerousSources)
        {
            mSettingsSource = settingsSource ?? throw new ArgumentNullException(nameof(settingsSource));
            mDangerousSources = dangerousSources == null ? new string[0] : (string[])dangerousSources.Clone();
        }

        /// <summary>
        /// 使用默认危险来源集合创建 Gate。
        /// </summary>
        /// <param name="settingsSource">执行开关读取端口。</param>
        /// <returns>默认 Gate。</returns>
        public static YokiFrameEngineGate CreateDefault(IYokiFrameEngineSettingsSource settingsSource)
        {
            return new YokiFrameEngineGate(settingsSource, DEFAULT_DANGEROUS_SOURCES);
        }

        /// <summary>
        /// 裁决一条 Engine Kit 命令，按全部宿主目标校验；供不区分宿主的调用方使用。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <param name="operation">命中的操作描述。</param>
        /// <returns>裁决结果。</returns>
        public YokiFrameEngineGateDecision Evaluate(
            YokiFrameCommandRequest request,
            YokiFrameEngineOperationDescriptor operation)
        {
            return Evaluate(request, operation, YokiFrameEngineExecutionTargets.ALL);
        }

        /// <summary>
        /// 裁决一条 Engine Kit 命令；每次调用都会重新读取开关快照。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <param name="operation">命中的操作描述。</param>
        /// <param name="hostTargets">当前宿主可承载的执行目标。</param>
        /// <returns>裁决结果。</returns>
        public YokiFrameEngineGateDecision Evaluate(
            YokiFrameCommandRequest request,
            YokiFrameEngineOperationDescriptor operation,
            YokiFrameEngineExecutionTarget hostTargets)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            YokiFrameEngineSettingsSnapshot settings = mSettingsSource.Read();

            // ① 载荷与目标：豁免也不跳过；payload 必须是合法 JSON 对象，target 必须是单一已知目标。
            if (!TryResolveTarget(request.PayloadJson, out YokiFrameEngineExecutionTarget target, out string targetError))
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    targetError,
                    settings.State);
            }

            // ② 执行开关：缺失与解析失败一律 fail-closed；豁免只跳过这一层。
            if (!operation.ExemptFromExecutionSwitch && settings.BlocksExecution)
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.OPERATION_DISABLED,
                    BuildDisabledMessage(settings, operation),
                    settings.State);
            }

            // ③ Dangerous 操作只允许受信来源。
            if (operation.Kind == YokiFrameCommandKind.Dangerous && !IsDangerousSourceAllowed(request.Source))
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.SOURCE_NOT_PERMITTED,
                    "Source '" + request.Source + "' is not permitted to run Dangerous Engine operations.",
                    settings.State);
            }

            // ④ 目标绑定：与 engine_capabilities 共用同一份判定规则。
            YokiFrameEngineOperationAvailability availability = YokiFrameEngineAvailabilityRules.Resolve(
                operation,
                target,
                settings,
                hostTargets);
            if (availability == YokiFrameEngineOperationAvailability.DisabledBySettings)
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.OPERATION_DISABLED,
                    BuildDisabledMessage(settings, operation),
                    settings.State);
            }

            if (availability == YokiFrameEngineOperationAvailability.UnsupportedByTarget)
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.UNSUPPORTED,
                    "Action '" + operation.Action + "' does not support target '" + YokiFrameEngineExecutionTargets.Format(target) + "'.",
                    settings.State);
            }

            if (availability == YokiFrameEngineOperationAvailability.UnsupportedByHostTarget)
            {
                return YokiFrameEngineGateDecision.Reject(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "Current host cannot serve target '" + YokiFrameEngineExecutionTargets.Format(target) + "'.",
                    settings.State);
            }

            return YokiFrameEngineGateDecision.Allow(settings.State);
        }

        /// <summary>
        /// 解析 payload 的 target 字段；使用 JSON DOM 读取，转义键名（例如 \u0074arget）同样命中。
        /// </summary>
        /// <param name="payloadJson">payload JSON。</param>
        /// <param name="target">解析出的单一目标；缺省为 editor。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>解析成功时返回 true。</returns>
        private static bool TryResolveTarget(
            string payloadJson,
            out YokiFrameEngineExecutionTarget target,
            out string error)
        {
            target = YokiFrameEngineExecutionTarget.Editor;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return true;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "Engine payload is not valid JSON: " + exception.Message;
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "Engine payload must be a JSON object.";
                    return false;
                }

                if (!root.TryGetProperty(TARGET_FIELD_NAME, out JsonElement value)
                    || value.ValueKind == JsonValueKind.Null)
                {
                    return true;
                }

                if (value.ValueKind != JsonValueKind.String)
                {
                    error = "Engine payload target must be a string.";
                    return false;
                }

                if (!YokiFrameEngineExecutionTargets.TryParse(value.GetString(), out YokiFrameEngineExecutionTarget parsed))
                {
                    error = "Engine payload target is not a known execution target.";
                    return false;
                }

                if (!YokiFrameEngineExecutionTargets.IsSingleTarget(parsed))
                {
                    error = "Engine payload target must be a single target; combined values are only valid in capability declarations.";
                    return false;
                }

                target = parsed;
                return true;
            }
        }

        /// <summary>
        /// 判断来源是否允许执行 Dangerous 操作。
        /// </summary>
        /// <param name="source">命令来源。</param>
        /// <returns>允许时返回 true。</returns>
        private bool IsDangerousSourceAllowed(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            for (var index = 0; index < mDangerousSources.Length; index++)
            {
                if (string.Equals(mDangerousSources[index], source, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 构造可诊断的关闭说明，包含开关状态与原因。
        /// </summary>
        /// <param name="settings">开关快照。</param>
        /// <param name="operation">操作描述。</param>
        /// <returns>说明文本。</returns>
        private static string BuildDisabledMessage(
            YokiFrameEngineSettingsSnapshot settings,
            YokiFrameEngineOperationDescriptor operation)
        {
            string message = "Engine operations are disabled (" + settings.State + ").";
            if (!operation.ExecutionPermitted)
                message = "The action's additional execution permission is disabled.";
            if (!string.IsNullOrEmpty(settings.Reason))
            {
                message += " " + settings.Reason;
            }

            return message + " Requested action: " + operation.Action + ".";
        }
    }
}
#endif
