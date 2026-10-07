#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public sealed partial class LiveCodeManager
    {
        /// <summary>按请求顺序写入字段。任一写入失败时逆序回滚已写值，回滚失败并入 AggregateException。写入前先完成全部绑定和类型检查。</summary>
        /// <param name="request">已解析的字段批次，会话和修订必须仍有效。</param>
        /// <param name="guard">每个行为复查时执行的守卫。</param>
        /// <returns>成功写入的字段数。</returns>
        public int SetFields(LiveFieldRequest request, Action guard)
        {
            Guard(guard);
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (mRestoring || mBatchAttaching || mPending.Count != 0)
                throw new InvalidOperationException("Wait for pending live operations before tuning fields.");
            if (!(mHost is ILiveFieldHost fields))
                throw new NotSupportedException("This host has no live field adapter.");
            var current = mState();
            if (request.SessionId != current.SessionId || request.Generation != current.Generation || request.Target != current.ActiveTarget)
                throw new InvalidOperationException("LiveFieldContextExpired: rediscover session/generation/target.");
            var bindings = new List<LiveFieldBinding>();
            var before = new List<object>();
            var after = new List<object>();
            foreach (var update in request.Updates)
            {
                Entry entry = RequireBehaviour(update.Id, guard);
                if (entry.Handle.Revision != update.Revision || !mHost.IsAlive(entry.Attachment))
                    throw new InvalidOperationException("LiveFieldRevisionExpired: rediscover " + update.Id + ".");
                foreach (var change in update.Fields)
                {
                    var binding = fields.BindTunableField(entry.Attachment, change.Name);
                    if (binding.ValueType.FullName != change.TypeName
                        && LiveFieldValues.PersistedTypeName(binding.ValueType) != change.TypeName)
                        throw new ArgumentException("Exact field type required: " + change.Name + " is " + binding.ValueType.FullName + ".");
                    object value = fields.DecodeTunableValue(binding.ValueType, change.Value);
                    if (value == null ? binding.ValueType.IsValueType : !binding.ValueType.IsInstanceOfType(value))
                        throw new ArgumentException("Decoded field value type mismatch.");
                    bindings.Add(binding);
                    before.Add(binding.Read());
                    after.Add(value);
                }
            }
            int applied = 0;
            try
            {
                for (; applied < bindings.Count; applied++) bindings[applied].Write(after[applied]);
            }
            catch (Exception exception)
            {
                var failures = new List<Exception> { exception };
                for (int index = applied - 1; index >= 0; index--)
                    try { bindings[index].Write(before[index]); }
                    catch (Exception rollback) { failures.Add(rollback); }
                throw new AggregateException("Live field batch failed; rollback failures are included.", failures);
            }
            return applied;
        }
    }
}
#endif
