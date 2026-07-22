using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 内存 SaveKit 存储后端，主要用于测试和临时运行。
    /// </summary>
    public sealed class MemorySaveStorage : ISaveStorage, ISaveMetadataStorage
    {
        private readonly Dictionary<SaveTarget, byte[]> documents = new();

        /// <inheritdoc />
        public bool Exists(SaveTarget target)
        {
            return documents.ContainsKey(target);
        }

        /// <inheritdoc />
        public void Write(SaveTarget target, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            documents[target] = CopyBytes(bytes);
        }

        /// <inheritdoc />
        public byte[] Read(SaveTarget target)
        {
            byte[] bytes;
            return documents.TryGetValue(target, out bytes) ? CopyBytes(bytes) : null;
        }

        /// <inheritdoc />
        public bool TryReadMetadata(SaveTarget target, out SaveMeta meta)
        {
            if (!documents.TryGetValue(target, out byte[] bytes)
                || !SaveMeta.TryDeserializeHeader(bytes, out meta, out _, out _)
                || meta.Target != target)
            {
                meta = default(SaveMeta);
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public bool Delete(SaveTarget target)
        {
            return documents.Remove(target);
        }

        /// <inheritdoc />
        public IReadOnlyList<SaveTarget> GetTargets(SaveTargetKind kind)
        {
            var targets = new List<SaveTarget>();
            foreach (var pair in documents)
            {
                if (pair.Key.Kind == kind)
                {
                    targets.Add(pair.Key);
                }
            }

            targets.Sort(CompareTargets);
            return targets;
        }

        /// <inheritdoc />
        public void Clear(SaveTargetKind kind)
        {
            var targets = GetTargets(kind);
            for (var i = 0; i < targets.Count; i++)
            {
                documents.Remove(targets[i]);
            }
        }

        /// <summary>复制字节，避免调用方修改后端内部状态。</summary>
        private static byte[] CopyBytes(byte[] bytes)
        {
            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }

        /// <summary>按目标类型和名称稳定排序。</summary>
        private static int CompareTargets(SaveTarget left, SaveTarget right)
        {
            var kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : string.CompareOrdinal(left.Name, right.Name);
        }
    }
}
