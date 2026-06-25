using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// Kit 运行时设置的引擎无关入口。Base 只保存 kit/key/value，实际持久化由宿主 Adapter 注入。
    /// </summary>
    public interface IKitSettingsStore
    {
        bool TryGetValue(string kit, string key, out string value);
        void SetValue(string kit, string key, string value);
        void RemoveValue(string kit, string key);
    }

    public static class KitSettings
    {
        private static readonly object sLock = new object();
        private static readonly InMemoryKitSettingsStore sMemoryStore = new InMemoryKitSettingsStore();
        private static IKitSettingsStore sStore = sMemoryStore;

        public static void SetStore(IKitSettingsStore store)
        {
            lock (sLock)
            {
                sStore = store ?? sMemoryStore;
            }
        }

        public static void Reset()
        {
            lock (sLock)
            {
                sMemoryStore.Clear();
                sStore = sMemoryStore;
            }
        }

        public static bool TryGetString(string kit, string key, out string value)
        {
            value = null;
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                return sStore != null && sStore.TryGetValue(kit, key, out value);
            }
        }

        public static string GetString(string kit, string key, string defaultValue)
        {
            string value;
            return TryGetString(kit, key, out value) ? value : defaultValue;
        }

        public static bool GetBool(string kit, string key, bool defaultValue)
        {
            string value;
            if (!TryGetString(kit, key, out value))
                return defaultValue;

            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;

            if (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.Ordinal) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }

        public static int GetInt(string kit, string key, int defaultValue)
        {
            string value;
            if (!TryGetString(kit, key, out value))
                return defaultValue;

            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        public static void SetString(string kit, string key, string value)
        {
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                sStore.SetValue(kit, key, value ?? string.Empty);
            }
        }

        public static void SetBool(string kit, string key, bool value)
        {
            SetString(kit, key, value ? "true" : "false");
        }

        public static void SetInt(string kit, string key, int value)
        {
            SetString(kit, key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Remove(string kit, string key)
        {
            ValidateKey(kit, nameof(kit));
            ValidateKey(key, nameof(key));
            lock (sLock)
            {
                sStore.RemoveValue(kit, key);
            }
        }

        private static void ValidateKey(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128)
                throw new ArgumentException("Kit setting identifiers must be 1-128 safe ASCII characters.", parameterName);

            if (value == "." || value == "..")
                throw new ArgumentException("Kit setting identifiers cannot be dot path segments.", parameterName);

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-';
                if (!valid)
                    throw new ArgumentException("Kit setting identifiers must be safe ASCII characters.", parameterName);
            }
        }

        private sealed class InMemoryKitSettingsStore : IKitSettingsStore
        {
            private readonly Dictionary<string, string> mValues = new(StringComparer.Ordinal);

            public bool TryGetValue(string kit, string key, out string value)
            {
                return mValues.TryGetValue(BuildCompositeKey(kit, key), out value);
            }

            public void SetValue(string kit, string key, string value)
            {
                mValues[BuildCompositeKey(kit, key)] = value ?? string.Empty;
            }

            public void RemoveValue(string kit, string key)
            {
                mValues.Remove(BuildCompositeKey(kit, key));
            }

            public void Clear()
            {
                mValues.Clear();
            }

            private static string BuildCompositeKey(string kit, string key)
            {
                return kit + "/" + key;
            }
        }
    }
}
