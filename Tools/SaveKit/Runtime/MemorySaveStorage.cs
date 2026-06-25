using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 基于内存字典的保存槽位存储后端，主要用于测试和临时存档。
    /// </summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<int, byte[]> slots = new();
        private readonly List<int> slotIds = new();

        /// <inheritdoc />
        public bool Exists(int slotId) => slots.ContainsKey(slotId);

        /// <inheritdoc />
        public void Write(int slotId, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (!slots.ContainsKey(slotId))
            {
                slotIds.Add(slotId);
            }

            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            slots[slotId] = copy;
        }

        /// <inheritdoc />
        public byte[] Read(int slotId)
        {
            byte[] bytes;
            if (!slots.TryGetValue(slotId, out bytes))
            {
                return null;
            }

            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }

        /// <inheritdoc />
        public bool Delete(int slotId)
        {
            if (!slots.Remove(slotId))
            {
                return false;
            }

            slotIds.Remove(slotId);
            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> GetSlotIds() => new List<int>(slotIds);

        /// <inheritdoc />
        public void Clear()
        {
            slots.Clear();
            slotIds.Clear();
        }
    }
}
