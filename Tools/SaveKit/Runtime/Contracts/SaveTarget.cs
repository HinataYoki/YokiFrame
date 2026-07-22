using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 的持久化目标。目标类型显式区分槽位和 Global 文档，禁止使用特殊槽位编号表达不同语义。
    /// </summary>
    public readonly struct SaveTarget : IEquatable<SaveTarget>
    {
        private const int MAX_GLOBAL_KEY_LENGTH = 64;
        private readonly SaveTargetKind kind;
        private readonly int slotId;
        private readonly string globalKey;

        private SaveTarget(SaveTargetKind targetKind, int targetSlotId, string targetGlobalKey)
        {
            kind = targetKind;
            slotId = targetSlotId;
            globalKey = targetGlobalKey;
        }

        /// <summary>创建数字槽位目标。</summary>
        /// <param name="slotId">非负槽位编号。</param>
        /// <returns>槽位目标。</returns>
        public static SaveTarget Slot(int slotId)
        {
            if (slotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), "Slot id must be non-negative.");
            }

            return new SaveTarget(SaveTargetKind.Slot, slotId, null);
        }

        /// <summary>创建命名 Global 文档目标。</summary>
        /// <param name="key">用于文件名的安全文档名称。</param>
        /// <returns>Global 文档目标。</returns>
        public static SaveTarget Global(string key)
        {
            ValidateGlobalKey(key);
            return new SaveTarget(SaveTargetKind.Global, 0, key);
        }

        /// <summary>获取目标类型。</summary>
        public SaveTargetKind Kind
        {
            get { return kind; }
        }

        /// <summary>获取槽位编号；Global 目标返回 -1。</summary>
        public int SlotId
        {
            get { return kind == SaveTargetKind.Slot ? slotId : -1; }
        }

        /// <summary>获取 Global 文档名称；槽位目标返回空值。</summary>
        public string GlobalKey
        {
            get { return globalKey; }
        }

        /// <summary>获取用于元数据和存储定位的稳定名称。</summary>
        public string Name
        {
            get { return kind == SaveTargetKind.Slot ? slotId.ToString(CultureInfo.InvariantCulture) : globalKey; }
        }

        /// <summary>判断目标是否为槽位。</summary>
        public bool IsSlot
        {
            get { return kind == SaveTargetKind.Slot; }
        }

        /// <summary>判断目标是否为 Global 文档。</summary>
        public bool IsGlobal
        {
            get { return kind == SaveTargetKind.Global; }
        }

        /// <summary>比较两个目标是否指向同一存档文档。</summary>
        public bool Equals(SaveTarget other)
        {
            return kind == other.kind && slotId == other.slotId &&
                   string.Equals(globalKey, other.globalKey, StringComparison.Ordinal);
        }

        /// <summary>比较对象是否为同一存档目标。</summary>
        public override bool Equals(object obj)
        {
            return obj is SaveTarget && Equals((SaveTarget)obj);
        }

        /// <summary>获取当前目标的进程内哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((int)kind * 397) ^ slotId;
                return globalKey == null ? hash : (hash * 397) ^ StringComparer.Ordinal.GetHashCode(globalKey);
            }
        }

        /// <summary>获取目标的人类可读表示。</summary>
        public override string ToString()
        {
            return kind == SaveTargetKind.Slot ? "Slot(" + slotId + ")" : "Global(" + globalKey + ")";
        }

        /// <summary>比较两个保存目标。</summary>
        public static bool operator ==(SaveTarget left, SaveTarget right)
        {
            return left.Equals(right);
        }

        /// <summary>比较两个保存目标是否不同。</summary>
        public static bool operator !=(SaveTarget left, SaveTarget right)
        {
            return !left.Equals(right);
        }

        /// <summary>验证 Global 文档名称，确保不会逃逸存储根目录。</summary>
        private static void ValidateGlobalKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MAX_GLOBAL_KEY_LENGTH)
            {
                throw new ArgumentException("Global key must contain 1 to 64 characters.", nameof(key));
            }

            for (var i = 0; i < key.Length; i++)
            {
                var character = key[i];
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' || character == '_' || character == '.')
                {
                    continue;
                }

                throw new ArgumentException("Global key contains an unsupported path character.", nameof(key));
            }
        }
    }
}
