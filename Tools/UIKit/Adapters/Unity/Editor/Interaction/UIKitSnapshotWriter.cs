#if UNITY_EDITOR
using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>把 UIKit 只读事实写为确定性且满足 Shared Memory 上限的 JSON。</summary>
    internal static class UIKitSnapshotWriter
    {
        private const int MAX_TYPE_BYTES = 512;
        private const int MAX_NAME_BYTES = 256;
        private const int MAX_LEVEL_BYTES = 128;
        private static readonly UTF8Encoding sUtf8 = new(false);

        /// <summary>创建只含统计与集合总量的轻量只读响应。</summary>
        /// <returns>符合 UIKit state schema 的有界 JSON。</returns>
        internal static string WriteStats()
        {
            UIKitInteractionSnapshot snapshot = UIKitSnapshotBuilder.Create();
            return Write(snapshot, 0, 0);
        }

        /// <summary>创建包含有界面板与栈条目的完整 Workbench state。</summary>
        /// <returns>UTF-8 字节数不超过 Telemetry 默认上限的 JSON。</returns>
        internal static string WriteWorkbench()
        {
            UIKitInteractionSnapshot snapshot = UIKitSnapshotBuilder.Create();
            int panelCount = snapshot.Panels.Count;
            int stackCount = snapshot.Stacks.Count;
            while (true)
            {
                string json = Write(snapshot, panelCount, stackCount);
                if (sUtf8.GetByteCount(json)
                    <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES) return json;
                if (panelCount == 0 && stackCount == 0) return json;
                ReduceLargerCollection(ref panelCount, ref stackCount);
            }
        }

        /// <summary>优先缩减当前返回条目较多的集合，保证降级顺序确定且最终收敛。</summary>
        /// <param name="panelCount">当前面板返回上限。</param>
        /// <param name="stackCount">当前栈返回上限。</param>
        private static void ReduceLargerCollection(ref int panelCount, ref int stackCount)
        {
            if (panelCount >= stackCount && panelCount > 0)
            {
                panelCount = ReduceCount(panelCount);
                return;
            }

            stackCount = ReduceCount(stackCount);
        }

        /// <summary>把正数返回上限至少缩减一项，大集合按二分方式快速收敛。</summary>
        /// <param name="count">当前返回上限。</param>
        /// <returns>严格更小且不小于零的新上限。</returns>
        private static int ReduceCount(int count) => count <= 1 ? 0 : count / 2;

        /// <summary>按指定集合上限写出完整固定 schema。</summary>
        /// <param name="snapshot">一次稳定事实快照。</param>
        /// <param name="panelLimit">最多返回的面板条目数。</param>
        /// <param name="stackLimit">最多返回的栈条目数。</param>
        /// <returns>确定性 JSON。</returns>
        private static string Write(
            UIKitInteractionSnapshot snapshot,
            int panelLimit,
            int stackLimit)
        {
            var builder = new StringBuilder(2048);
            builder.Append("{\"schemaVersion\":1,\"kit\":\"UIKit\"");
            AppendRoot(builder, snapshot.Root);
            AppendStats(builder, snapshot.Stats);
            AppendCache(builder, snapshot.Cache);
            AppendModal(builder, snapshot.Modal);
            AppendPanels(builder, snapshot, panelLimit);
            AppendStacks(builder, snapshot, stackLimit);
            return builder.Append('}').ToString();
        }

        /// <summary>写入 Root 存在性；查询通过现有 loader 完成，不触发单例创建。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="root">Root 快照。</param>
        private static void AppendRoot(StringBuilder builder, UIKitRootSnapshot root)
        {
            builder.Append(",\"root\":{\"exists\":").Append(ToJson(root.Exists)).Append('}');
        }

        /// <summary>写入全局数量和所有生命周期状态桶。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="stats">数量汇总。</param>
        private static void AppendStats(StringBuilder builder, UIKitStatsSnapshot stats)
        {
            builder.Append(",\"stats\":{\"panelCount\":").Append(stats.PanelCount);
            builder.Append(",\"stackCount\":").Append(stats.StackCount);
            builder.Append(",\"stackMembershipCount\":").Append(stats.StackMembershipCount);
            builder.Append(",\"states\":{\"preloaded\":").Append(stats.PreloadedCount);
            builder.Append(",\"opening\":").Append(stats.OpeningCount);
            builder.Append(",\"open\":").Append(stats.OpenCount);
            builder.Append(",\"hiding\":").Append(stats.HidingCount);
            builder.Append(",\"hidden\":").Append(stats.HiddenCount);
            builder.Append(",\"closing\":").Append(stats.ClosingCount);
            builder.Append(",\"cached\":").Append(stats.CachedCount);
            builder.Append(",\"closed\":").Append(stats.ClosedCount).Append("}}");
        }

        /// <summary>写入显式缓存策略数量和 Reusable 容量。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="cache">缓存汇总。</param>
        private static void AppendCache(StringBuilder builder, UIKitCacheSnapshot cache)
        {
            builder.Append(",\"cache\":{\"capacity\":").Append(cache.Capacity);
            builder.Append(",\"transient\":").Append(cache.TransientCount);
            builder.Append(",\"reusable\":").Append(cache.ReusableCount);
            builder.Append(",\"reusableCached\":").Append(cache.ReusableCachedCount);
            builder.Append(",\"persistent\":").Append(cache.PersistentCount).Append('}');
        }

        /// <summary>写入当前模态面板数量与 blocker 是否存在。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="modal">模态汇总。</param>
        private static void AppendModal(StringBuilder builder, UIKitModalSnapshot modal)
        {
            builder.Append(",\"modal\":{\"blockerActive\":").Append(ToJson(modal.BlockerActive));
            builder.Append(",\"panelCount\":").Append(modal.PanelCount).Append('}');
        }

        /// <summary>写入有界面板数组及 total、returned、truncated 元数据。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="snapshot">稳定事实快照。</param>
        /// <param name="limit">返回条目上限。</param>
        private static void AppendPanels(
            StringBuilder builder,
            UIKitInteractionSnapshot snapshot,
            int limit)
        {
            int returned = Math.Min(Math.Max(0, limit), snapshot.Panels.Count);
            builder.Append(",\"panels\":{\"items\":[");
            for (var index = 0; index < returned; index++)
            {
                if (index > 0) builder.Append(',');
                AppendPanel(builder, snapshot.Panels[index]);
            }

            AppendCollectionFooter(builder, snapshot.Panels.Count, returned);
        }

        /// <summary>写入一个面板公开状态，明确不访问 Data 或 Unity 标识。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="panel">面板快照。</param>
        private static void AppendPanel(StringBuilder builder, UIKitPanelSnapshot panel)
        {
            builder.Append("{\"type\":");
            AppendString(builder, panel.Type, MAX_TYPE_BYTES);
            builder.Append(",\"name\":");
            AppendString(builder, panel.Name, MAX_NAME_BYTES);
            builder.Append(",\"state\":");
            AppendString(builder, panel.State, MAX_NAME_BYTES);
            builder.Append(",\"level\":");
            AppendString(builder, panel.Level, MAX_LEVEL_BYTES);
            builder.Append(",\"levelOrder\":").Append(panel.LevelOrder);
            builder.Append(",\"subLevel\":").Append(panel.SubLevel);
            builder.Append(",\"cachePolicy\":");
            AppendString(builder, panel.CachePolicy, MAX_NAME_BYTES);
            builder.Append(",\"modal\":").Append(ToJson(panel.IsModal));
            builder.Append(",\"stack\":");
            AppendNullableString(builder, panel.StackName, MAX_NAME_BYTES);
            builder.Append('}');
        }

        /// <summary>写入有界命名栈数组及 total、returned、truncated 元数据。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="snapshot">稳定事实快照。</param>
        /// <param name="limit">返回条目上限。</param>
        private static void AppendStacks(
            StringBuilder builder,
            UIKitInteractionSnapshot snapshot,
            int limit)
        {
            int returned = Math.Min(Math.Max(0, limit), snapshot.Stacks.Count);
            builder.Append(",\"stacks\":{\"items\":[");
            for (var index = 0; index < returned; index++)
            {
                if (index > 0) builder.Append(',');
                AppendStack(builder, snapshot.Stacks[index]);
            }

            AppendCollectionFooter(builder, snapshot.Stacks.Count, returned);
        }

        /// <summary>写入一个命名栈及其顶部面板的类型和业务名称。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="stack">命名栈快照。</param>
        private static void AppendStack(StringBuilder builder, UIKitStackSnapshot stack)
        {
            builder.Append("{\"name\":");
            AppendString(builder, stack.Name, MAX_NAME_BYTES);
            builder.Append(",\"depth\":").Append(stack.Depth);
            builder.Append(",\"topPanelType\":");
            AppendNullableString(builder, stack.TopPanelType, MAX_TYPE_BYTES);
            builder.Append(",\"topPanelName\":");
            AppendNullableString(builder, stack.TopPanelName, MAX_NAME_BYTES);
            builder.Append('}');
        }

        /// <summary>结束集合对象并写入精确截断元数据。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="total">采集到的条目总数。</param>
        /// <param name="returned">实际写出的条目数。</param>
        private static void AppendCollectionFooter(StringBuilder builder, int total, int returned)
        {
            builder.Append("],\"total\":").Append(total);
            builder.Append(",\"returned\":").Append(returned);
            builder.Append(",\"truncated\":").Append(ToJson(returned < total)).Append('}');
        }

        /// <summary>写入经过 UTF-8 字节上限裁剪的 JSON 字符串。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="value">待写入文本；空值按空字符串处理。</param>
        /// <param name="maxUtf8Bytes">允许的最大 UTF-8 字节数。</param>
        private static void AppendString(StringBuilder builder, string value, int maxUtf8Bytes)
        {
            string normalized = NormalizeText(value ?? string.Empty, maxUtf8Bytes);
            builder.Append('"').Append(JsonHelper.EscapeString(normalized)).Append('"');
        }

        /// <summary>写入可空 JSON 字符串，保持缺失值与空字符串语义不同。</summary>
        /// <param name="builder">目标 JSON 缓冲区。</param>
        /// <param name="value">可空文本。</param>
        /// <param name="maxUtf8Bytes">允许的最大 UTF-8 字节数。</param>
        private static void AppendNullableString(StringBuilder builder, string value, int maxUtf8Bytes)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            AppendString(builder, value, maxUtf8Bytes);
        }

        /// <summary>按 UTF-8 字节数截断文本，并避免把代理项对切开。</summary>
        /// <param name="value">待裁剪文本。</param>
        /// <param name="maxUtf8Bytes">允许的最大 UTF-8 字节数。</param>
        /// <returns>完整文本或安全前缀。</returns>
        private static string NormalizeText(string value, int maxUtf8Bytes)
        {
            if (sUtf8.GetByteCount(value) <= maxUtf8Bytes) return value;
            int length = value.Length;
            while (length > 0 && sUtf8.GetByteCount(value, 0, length) > maxUtf8Bytes) length--;
            if (length > 0 && length < value.Length
                && char.IsHighSurrogate(value[length - 1])
                && char.IsLowSurrogate(value[length])) length--;
            return value.Substring(0, length);
        }

        /// <summary>把布尔值转换为 JSON 小写字面量。</summary>
        /// <param name="value">布尔值。</param>
        /// <returns>JSON true 或 false。</returns>
        private static string ToJson(bool value) => value ? "true" : "false";
    }
}
#endif
