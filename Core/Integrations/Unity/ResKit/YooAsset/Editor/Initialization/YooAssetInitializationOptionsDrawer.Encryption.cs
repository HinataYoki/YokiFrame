#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetInitializationOptionsDrawer
    {
        private const string ENCRYPTION_CARD_KEY = "YooAssetInitialization.Encryption";

        /// <summary>创建旧版 YooAsset 初始化器中的加密配置卡片。</summary>
        private static VisualElement CreateEncryptionCard(SerializedProperty property)
        {
            List<YooAssetEncryptionImplementationPair> pairs =
                YooAssetEncryptionImplementationCatalog.GetAvailablePairs();
            if (pairs.Count == 0)
                return null;

            return InspectorKitUi.CreateCard(
                "加密配置",
                ENCRYPTION_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    SerializedProperty mode = property.FindPropertyRelative(
                        nameof(YooAssetInitializationOptions.EncryptionMode));
                    VisualElement content = new();
                    body.Add(CreateEncryptionModeRow(
                        mode,
                        pairs,
                        () => BuildEncryptionContent(content, property)));
                    body.Add(content);
                    BuildEncryptionContent(content, property);
                });
        }

        /// <summary>创建仅包含已扫描加密解密实现对的方案下拉字段。</summary>
        private static VisualElement CreateEncryptionModeRow(
            SerializedProperty property,
            List<YooAssetEncryptionImplementationPair> pairs,
            Action onChanged)
        {
            List<string> choices = new() { "未启用额外解密" };
            List<int> values = new() { (int)YooAssetEncryptionMode.None };
            for (int index = 0; index < pairs.Count; index++)
            {
                choices.Add(GetEncryptionModeDisplayName(pairs[index].Mode));
                values.Add((int)pairs[index].Mode);
            }

            return CreateMappedDropdown(property, "加密方案", choices, values, onChanged);
        }

        /// <summary>按当前加密方案重建密钥和偏移字段。</summary>
        private static void BuildEncryptionContent(
            VisualElement container,
            SerializedProperty property)
        {
            InspectorKitUi.Refresh(container, target =>
            {
                SerializedProperty mode = property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.EncryptionMode));
                if (mode == null)
                    return;

                YooAssetEncryptionMode selectedMode = (YooAssetEncryptionMode)mode.enumValueIndex;
                if (selectedMode == YooAssetEncryptionMode.None)
                {
                    target.Add(InspectorKitUi.CreateInfoBox(
                        "未启用额外解密",
                        "Bundle 将由 YooAsset 默认文件系统直接读取。",
                        InspectorInfoBoxType.Info));
                    return;
                }

                if (!YooAssetEncryptionImplementationCatalog.TryGetPair(selectedMode, out var pair))
                {
                    target.Add(InspectorKitUi.CreateInfoBox(
                        "缺少实现",
                        "当前方案没有同时扫描到构建加密和运行时解密实现，不能用于初始化或构建。",
                        InspectorInfoBoxType.Error));
                    return;
                }

                target.Add(InspectorKitUi.CreateReadOnlyStringRow(
                    "构建加密实现",
                    pair.EncryptionType.FullName));
                target.Add(InspectorKitUi.CreateReadOnlyStringRow(
                    "运行时解密实现",
                    pair.DecryptionType.FullName));

                switch (selectedMode)
                {
                    case YooAssetEncryptionMode.XorStream:
                        target.Add(InspectorKitUi.CreateInfoBox(
                            "XOR 流式解密",
                            "运行时使用同一密钥种子读取内置和缓存 Bundle。",
                            InspectorInfoBoxType.Info));
                        target.Add(InspectorKitUi.CreateStringRow(
                            property.FindPropertyRelative(nameof(YooAssetInitializationOptions.XorKeySeed)),
                            "密钥种子"));
                        target.Add(InspectorKitUi.CreateButtonRow(
                            InspectorKitUi.CreateActionButton(
                                "恢复默认密钥",
                                () => ResetXorKey(property))));
                        break;
                    case YooAssetEncryptionMode.FileOffset:
                        target.Add(InspectorKitUi.CreateInfoBox(
                            "文件偏移解密",
                            "资源包前置随机字节，运行时通过偏移量加载原始 Bundle。",
                            InspectorInfoBoxType.Info));
                        target.Add(InspectorKitUi.CreateIntegerRow(
                            property.FindPropertyRelative(nameof(YooAssetInitializationOptions.FileOffset)),
                            "文件偏移量"));
                        target.Add(InspectorKitUi.CreateButtonRow(
                            InspectorKitUi.CreateActionButton(
                                "恢复默认偏移量",
                                () => ResetFileOffset(property))));
                        break;
                    case YooAssetEncryptionMode.Aes:
                        target.Add(InspectorKitUi.CreateInfoBox(
                            "AES-CBC 解密",
                            "密码和盐值通过 PBKDF2 派生运行时密钥，构建服务需使用同一参数。",
                            InspectorInfoBoxType.Warning));
                        target.Add(InspectorKitUi.CreateStringRow(
                            property.FindPropertyRelative(nameof(YooAssetInitializationOptions.AesPassword)),
                            "AES 密码"));
                        target.Add(InspectorKitUi.CreateStringRow(
                            property.FindPropertyRelative(nameof(YooAssetInitializationOptions.AesSalt)),
                            "AES 盐值"));
                        target.Add(InspectorKitUi.CreateButtonRow(
                            InspectorKitUi.CreateActionButton(
                                "恢复默认密钥",
                                () => ResetAesKey(property))));
                        break;
                    case YooAssetEncryptionMode.Custom:
                        target.Add(InspectorKitUi.CreateInfoBox(
                            "自定义解密",
                            "请让实现类型标记方案元数据，并在代码中注册 YooAssetEncryptionServices 的自定义工厂。",
                            InspectorInfoBoxType.Warning));
                        break;
                }
            });
        }

        /// <summary>获取用于 Inspector 下拉字段的稳定方案名称。</summary>
        private static string GetEncryptionModeDisplayName(YooAssetEncryptionMode mode)
        {
            switch (mode)
            {
                case YooAssetEncryptionMode.XorStream:
                    return "XOR 流式";
                case YooAssetEncryptionMode.FileOffset:
                    return "文件偏移";
                case YooAssetEncryptionMode.Aes:
                    return "AES-CBC";
                case YooAssetEncryptionMode.Custom:
                    return "自定义";
                default:
                    return "未启用额外解密";
            }
        }

        /// <summary>将 XOR 密钥恢复为 InspectorKit 默认配置。</summary>
        private static void ResetXorKey(SerializedProperty property)
        {
            SerializedProperty key = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.XorKeySeed));
            key.stringValue = YooAssetInitializationOptions.DEFAULT_XOR_KEY_SEED;
            property.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>将文件偏移恢复为 InspectorKit 默认配置。</summary>
        private static void ResetFileOffset(SerializedProperty property)
        {
            SerializedProperty offset = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.FileOffset));
            offset.intValue = YooAssetInitializationOptions.DEFAULT_FILE_OFFSET;
            property.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>将 AES 密码和盐值恢复为 InspectorKit 默认配置。</summary>
        private static void ResetAesKey(SerializedProperty property)
        {
            SerializedProperty password = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.AesPassword));
            SerializedProperty salt = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.AesSalt));
            password.stringValue = YooAssetInitializationOptions.DEFAULT_AES_PASSWORD;
            salt.stringValue = YooAssetInitializationOptions.DEFAULT_AES_SALT;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
