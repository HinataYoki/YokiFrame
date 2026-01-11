using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// 重绑定选项
    /// </summary>
    public struct RebindOptions
    {
        /// <summary>绑定索引</summary>
        public int BindingIndex;

        /// <summary>取消键路径（默认 Escape）</summary>
        public string CancelKey;

        /// <summary>排除的控件路径</summary>
        public string[] ExcludedControls;

        /// <summary>等待延迟（秒）</summary>
        public float WaitDelay;

        /// <summary>控制方案筛选</summary>
        public string BindingGroup;

        /// <summary>默认选项</summary>
        public static RebindOptions Default => new()
        {
            BindingIndex = 0,
            CancelKey = "<Keyboard>/escape",
            ExcludedControls = new[]
            {
                "<Mouse>/position",
                "<Mouse>/delta",
                "<Pointer>/position",
                "<Pointer>/delta"
            },
            WaitDelay = 0.1f,
            BindingGroup = null
        };
    }

    /// <summary>
    /// InputKit - 运行时重绑定
    /// </summary>
    public static partial class InputKit
    {
        private static InputActionRebindingExtensions.RebindingOperation sRebindOperation;
        private static readonly List<InputAction> sConflictingActions = new();

        /// <summary>当前是否正在重绑定</summary>
        public static bool IsRebinding => sRebindOperation != default;

        /// <summary>绑定冲突事件</summary>
        public static event Action<InputAction, IReadOnlyList<InputAction>> OnBindingConflict;

        #region 交互式重绑定

        /// <summary>
        /// 开始交互式重绑定（一行式 API）
        /// </summary>
        /// <param name="action">目标 InputAction</param>
        /// <param name="bindingIndex">绑定索引（默认 0）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public static UniTask<bool> RebindAsync(
            InputAction action,
            int bindingIndex = 0,
            CancellationToken ct = default)
        {
            var options = RebindOptions.Default;
            options.BindingIndex = bindingIndex;
            return RebindAsync(action, options, ct);
        }

        /// <summary>
        /// 开始交互式重绑定（高级配置）
        /// </summary>
        /// <param name="action">目标 InputAction</param>
        /// <param name="options">重绑定选项</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public static async UniTask<bool> RebindAsync(
            InputAction action,
            RebindOptions options,
            CancellationToken ct = default)
        {
            if (action == default)
            {
                Debug.LogError("[InputKit] Action 不能为空");
                return false;
            }

            // 取消进行中的重绑定
            CancelRebinding();

            action.Disable();

            var tcs = new UniTaskCompletionSource<bool>();

            var operation = action.PerformInteractiveRebinding(options.BindingIndex);

            // 配置取消键
            if (!string.IsNullOrEmpty(options.CancelKey))
            {
                operation.WithCancelingThrough(options.CancelKey);
            }

            // 配置排除控件
            if (options.ExcludedControls != default)
            {
                for (int i = 0; i < options.ExcludedControls.Length; i++)
                {
                    operation.WithControlsExcluding(options.ExcludedControls[i]);
                }
            }

            // 配置等待延迟
            if (options.WaitDelay > 0)
            {
                operation.OnMatchWaitForAnother(options.WaitDelay);
            }

            // 配置控制方案筛选
            if (!string.IsNullOrEmpty(options.BindingGroup))
            {
                operation.WithBindingGroup(options.BindingGroup);
            }

            operation
                .OnComplete(op =>
                {
                    // 检测冲突
                    var conflicts = GetConflictingActions(action, options.BindingIndex);
                    if (conflicts.Count > 0)
                    {
                        OnBindingConflict?.Invoke(action, conflicts);
                    }

                    tcs.TrySetResult(true);
                    RaiseBindingChanged(action, options.BindingIndex);
                })
                .OnCancel(op => tcs.TrySetResult(false));

            sRebindOperation = operation;

            using var registration = ct.Register(static state =>
            {
                if (state is InputActionRebindingExtensions.RebindingOperation op)
                {
                    op.Cancel();
                }
            }, sRebindOperation);

            sRebindOperation.Start();

            var result = await tcs.Task;

            sRebindOperation?.Dispose();
            sRebindOperation = default;

            action.Enable();

            if (result)
            {
                SaveBindings();
            }

            return result;
        }

        /// <summary>
        /// 取消当前重绑定
        /// </summary>
        public static void CancelRebinding()
        {
            if (sRebindOperation == default) return;

            sRebindOperation.Cancel();
            sRebindOperation.Dispose();
            sRebindOperation = default;
        }

        #endregion

        #region 重置绑定

        /// <summary>
        /// 重置单个绑定
        /// </summary>
        /// <param name="action">目标 InputAction</param>
        /// <param name="bindingIndex">绑定索引</param>
        public static void ResetBinding(InputAction action, int bindingIndex = 0)
        {
            if (action == default) return;

            action.RemoveBindingOverride(bindingIndex);
            SaveBindings();
            RaiseBindingChanged(action, bindingIndex);
        }

        /// <summary>
        /// 重置 Action 的所有绑定
        /// </summary>
        /// <param name="action">目标 InputAction</param>
        public static void ResetActionBindings(InputAction action)
        {
            if (action == default) return;

            action.RemoveAllBindingOverrides();
            SaveBindings();
            RaiseBindingChanged(action, -1);
        }

        /// <summary>
        /// 重置所有绑定
        /// </summary>
        public static void ResetAllBindings()
        {
            if (sActionAsset == default) return;

            foreach (var map in sActionAsset.actionMaps)
            {
                map.RemoveAllBindingOverrides();
            }

            SaveBindings();
            RaiseBindingChanged(default, -1);
        }

        #endregion

        #region 绑定冲突检测

        /// <summary>
        /// 检测绑定冲突
        /// </summary>
        /// <param name="action">目标 InputAction</param>
        /// <param name="bindingIndex">绑定索引</param>
        /// <returns>冲突的 Action 列表</returns>
        public static IReadOnlyList<InputAction> GetConflictingActions(InputAction action, int bindingIndex)
        {
            sConflictingActions.Clear();

            if (action == default || sActionAsset == default) return sConflictingActions;

            var binding = action.bindings[bindingIndex];
            var effectivePath = binding.effectivePath;

            if (string.IsNullOrEmpty(effectivePath)) return sConflictingActions;

            foreach (var map in sActionAsset.actionMaps)
            {
                foreach (var otherAction in map.actions)
                {
                    // 跳过自身
                    if (otherAction == action) continue;

                    for (int i = 0; i < otherAction.bindings.Count; i++)
                    {
                        var otherBinding = otherAction.bindings[i];
                        if (otherBinding.effectivePath == effectivePath)
                        {
                            sConflictingActions.Add(otherAction);
                            break;
                        }
                    }
                }
            }

            return sConflictingActions;
        }

        #endregion
    }
}
