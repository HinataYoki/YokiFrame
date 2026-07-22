#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEditor;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>描述一个可同时用于构建和运行时的 YooAsset 加密方案实现对。</summary>
    internal readonly struct YooAssetEncryptionImplementationPair
    {
        /// <summary>创建已验证的构建加密与运行时解密实现对。</summary>
        /// <param name="mode">两侧实现声明的统一加密方案。</param>
        /// <param name="encryptionType">构建加密实现类型。</param>
        /// <param name="decryptionType">运行时解密实现类型。</param>
        internal YooAssetEncryptionImplementationPair(
            YooAssetEncryptionMode mode,
            Type encryptionType,
            Type decryptionType)
        {
            Mode = mode;
            EncryptionType = encryptionType;
            DecryptionType = decryptionType;
        }

        /// <summary>获取统一加密方案。</summary>
        internal YooAssetEncryptionMode Mode { get; }

        /// <summary>获取构建加密实现类型。</summary>
        internal Type EncryptionType { get; }

        /// <summary>获取运行时解密实现类型。</summary>
        internal Type DecryptionType { get; }
    }

    /// <summary>
    /// 仅在 Unity Editor 中扫描带方案元数据的 YooAsset 实现，避免在 Player 保留类型发现逻辑。
    /// </summary>
    internal static class YooAssetEncryptionImplementationCatalog
    {
        /// <summary>扫描当前编译域中成对存在的 YooAsset 加密和解密实现。</summary>
        /// <returns>按方案枚举顺序排列的可用实现对。</returns>
        internal static List<YooAssetEncryptionImplementationPair> GetAvailablePairs()
        {
            List<Type> types = new(
                TypeCache.GetTypesWithAttribute<YooAssetEncryptionImplementationAttribute>());
            types.Sort(static (left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            return CollectPairs(types);
        }

        /// <summary>从给定类型集合中筛选同一方案的构建加密和运行时解密实现对。</summary>
        /// <param name="types">待检查的带元数据类型集合。</param>
        /// <returns>只有双侧实现都存在时才包含对应方案。</returns>
        internal static List<YooAssetEncryptionImplementationPair> CollectPairs(IEnumerable<Type> types)
        {
            Dictionary<YooAssetEncryptionMode, Type> encryptionTypes = new();
            Dictionary<YooAssetEncryptionMode, Type> decryptionTypes = new();
            if (types != null)
            {
                foreach (Type type in types)
                    CollectImplementation(type, encryptionTypes, decryptionTypes);
            }

            List<YooAssetEncryptionImplementationPair> pairs = new();
            foreach (YooAssetEncryptionMode mode in Enum.GetValues(typeof(YooAssetEncryptionMode)))
            {
                if (mode == YooAssetEncryptionMode.None)
                    continue;
                if (!encryptionTypes.TryGetValue(mode, out Type encryptionType))
                    continue;
                if (!decryptionTypes.TryGetValue(mode, out Type decryptionType))
                    continue;

                pairs.Add(new YooAssetEncryptionImplementationPair(
                    mode,
                    encryptionType,
                    decryptionType));
            }

            return pairs;
        }

        /// <summary>查找指定方案的已扫描实现对。</summary>
        /// <param name="mode">需要查询的加密方案。</param>
        /// <param name="pair">找到时返回构建和运行时实现类型。</param>
        /// <returns>双侧实现均存在时返回 true。</returns>
        internal static bool TryGetPair(
            YooAssetEncryptionMode mode,
            out YooAssetEncryptionImplementationPair pair)
        {
            List<YooAssetEncryptionImplementationPair> pairs = GetAvailablePairs();
            for (int index = 0; index < pairs.Count; index++)
            {
                if (pairs[index].Mode != mode)
                    continue;

                pair = pairs[index];
                return true;
            }

            pair = default;
            return false;
        }

        /// <summary>验证并登记一个具体实现类型；同方案存在多个实现时选择类型全名最小者保持稳定显示。</summary>
        private static void CollectImplementation(
            Type type,
            Dictionary<YooAssetEncryptionMode, Type> encryptionTypes,
            Dictionary<YooAssetEncryptionMode, Type> decryptionTypes)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                return;

            var attribute = Attribute.GetCustomAttribute(
                type,
                typeof(YooAssetEncryptionImplementationAttribute))
                as YooAssetEncryptionImplementationAttribute;
            if (attribute == null || attribute.Mode == YooAssetEncryptionMode.None)
                return;
            if (!IsExpectedImplementation(type, attribute.Role))
                return;

            if (attribute.Role == YooAssetEncryptionImplementationRole.Encryption)
            {
                RegisterCandidate(encryptionTypes, attribute.Mode, type);
                return;
            }

            RegisterCandidate(decryptionTypes, attribute.Mode, type);
        }

        /// <summary>确认带元数据类型确实实现当前 YooAsset 主版本所需的对应接口。</summary>
        private static bool IsExpectedImplementation(
            Type type,
            YooAssetEncryptionImplementationRole role)
        {
#if YOKIFRAME_YOOASSET_3
            return role == YooAssetEncryptionImplementationRole.Encryption
                ? typeof(IBundleEncryptor).IsAssignableFrom(type)
                : typeof(IBundleDecryptor).IsAssignableFrom(type);
#else
            return role == YooAssetEncryptionImplementationRole.Encryption
                ? typeof(IEncryptionServices).IsAssignableFrom(type)
                : typeof(IDecryptionServices).IsAssignableFrom(type);
#endif
        }

        /// <summary>按类型全名稳定登记同一方案的候选实现。</summary>
        private static void RegisterCandidate(
            Dictionary<YooAssetEncryptionMode, Type> candidates,
            YooAssetEncryptionMode mode,
            Type candidate)
        {
            if (!candidates.TryGetValue(mode, out Type current)
                || string.CompareOrdinal(candidate.FullName, current.FullName) < 0)
            {
                candidates[mode] = candidate;
            }
        }
    }
}
#endif
