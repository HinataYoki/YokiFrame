#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && !UNITY_2022_1_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - IMGUI 实现（Unity 2021.3 兼容）
    /// 使用纯 EditorGUI 手动布局，避免 EditorGUILayout 在 PropertyDrawer 中的问题
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region IMGUI 状态

        private static bool sBasicFoldout = true;
        private static bool sEncryptFoldout = true;
        private static bool sBuildFoldout = false;
        private static bool sAdvancedFoldout = false;
        private static int sSelectedPackageIndex;
        private static int sSelectedPipelineIndex;

        private const float LINE_HEIGHT = 18f;
        private const float SPACING = 2f;
        private const float INDENT = 15f;
        private const float LABEL_WIDTH = 140f;

        #endregion

        #region IMGUI 主入口

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            float y = position.y;
            float width = position.width;

            // 标题
            var titleRect = new Rect(position.x, y, width, LINE_HEIGHT);
            EditorGUI.LabelField(titleRect, property.displayName, EditorStyles.boldLabel);
            y += LINE_HEIGHT + SPACING * 2;

            // 基础配置
            y = DrawBasicConfigIMGUI(position.x, y, width, property);

            // 加密配置
            y = DrawEncryptionConfigIMGUI(position.x, y, width, property);

            // 打包配置
            y = DrawBuildConfigIMGUI(position.x, y, width, property);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = LINE_HEIGHT + SPACING * 2; // 标题

            // 基础配置
            height += LINE_HEIGHT + SPACING; // foldout
            if (sBasicFoldout)
            {
                height += (LINE_HEIGHT + SPACING) * 2; // 两个模式字段
                var packageNames = property.FindPropertyRelative("PackageNames");
                height += LINE_HEIGHT + SPACING; // 列表标签
                height += (LINE_HEIGHT + SPACING) * packageNames.arraySize; // 列表项
                height += LINE_HEIGHT + SPACING; // 添加按钮
            }
            height += SPACING * 2;

            // 加密配置
            height += LINE_HEIGHT + SPACING; // foldout
            if (sEncryptFoldout)
            {
                height += LINE_HEIGHT + SPACING; // 加密类型
                var encType = (YooEncryptionType)property.FindPropertyRelative("EncryptionType").enumValueIndex;
                height += GetEncryptionContentHeight(encType);
            }
            height += SPACING * 2;

            // 打包配置
            height += LINE_HEIGHT + SPACING; // foldout
            if (sBuildFoldout)
            {
                var packageNames = GetBuildPackageNames();
                if (packageNames.Count == 0)
                    height += LINE_HEIGHT * 3; // 提示 + 按钮
                else
                {
                    height += (LINE_HEIGHT + SPACING) * 5; // 基础选项
                    height += LINE_HEIGHT + SPACING; // 高级选项 foldout
                    if (sAdvancedFoldout)
                        height += (LINE_HEIGHT + SPACING) * 3;
                    height += (LINE_HEIGHT + SPACING) * 3; // 按钮
                }
            }

            return height + 20f;
        }

        private static float GetEncryptionContentHeight(YooEncryptionType type)
        {
            return type switch
            {
                YooEncryptionType.XorStream => (LINE_HEIGHT + SPACING) * 4,
                YooEncryptionType.FileOffset => (LINE_HEIGHT + SPACING) * 3,
                YooEncryptionType.Aes => (LINE_HEIGHT + SPACING) * 5,
                _ => LINE_HEIGHT * 2
            };
        }

        #endregion

        #region 基础配置 IMGUI

        private static float DrawBasicConfigIMGUI(float x, float y, float width, SerializedProperty property)
        {
            // Foldout
            var foldoutRect = new Rect(x, y, width, LINE_HEIGHT);
            sBasicFoldout = EditorGUI.Foldout(foldoutRect, sBasicFoldout, "基础配置", true, EditorStyles.foldoutHeader);
            y += LINE_HEIGHT + SPACING;

            if (!sBasicFoldout) return y + SPACING;

            float contentX = x + INDENT;
            float contentWidth = width - INDENT;

            // 编辑器下初始化模式
            var editorModeProp = property.FindPropertyRelative("EditorPlayMode");
            var editorModeRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            EditorGUI.PropertyField(editorModeRect, editorModeProp, new GUIContent("编辑器下初始化模式"));
            y += LINE_HEIGHT + SPACING;

            // 真机下初始化模式
            var runtimeModeProp = property.FindPropertyRelative("RuntimePlayMode");
            y = DrawFilteredPlayModeIMGUI(contentX, y, contentWidth, runtimeModeProp, "真机下初始化模式");

            // 资源包列表
            var packageNamesProp = property.FindPropertyRelative("PackageNames");
            y = DrawPackageListIMGUI(contentX, y, contentWidth, packageNamesProp);

            return y + SPACING;
        }

        private static float DrawFilteredPlayModeIMGUI(float x, float y, float width, SerializedProperty prop, string label)
        {
            var choices = new[] { "OfflinePlayMode", "HostPlayMode", "WebPlayMode", "CustomPlayMode" };
            var values = new[] { 1, 2, 3, 4 };

            int currentValue = prop.enumValueIndex;
            int currentIndex = Array.IndexOf(values, currentValue);
            if (currentIndex < 0)
            {
                currentIndex = 0;
                prop.enumValueIndex = values[0];
                prop.serializedObject.ApplyModifiedProperties();
            }

            var rect = new Rect(x, y, width, LINE_HEIGHT);
            int newIndex = EditorGUI.Popup(rect, label, currentIndex, choices);
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < values.Length)
            {
                prop.enumValueIndex = values[newIndex];
                prop.serializedObject.ApplyModifiedProperties();
            }

            return y + LINE_HEIGHT + SPACING;
        }

        private static float DrawPackageListIMGUI(float x, float y, float width, SerializedProperty listProp)
        {
            var labelRect = new Rect(x, y, width, LINE_HEIGHT);
            EditorGUI.LabelField(labelRect, "资源包列表");
            y += LINE_HEIGHT + SPACING;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var itemProp = listProp.GetArrayElementAtIndex(i);
                string labelText = i == 0 ? "默认" : $"#{i + 1}";

                float fieldWidth = listProp.arraySize > 1 ? width - 25 : width;
                var fieldRect = new Rect(x, y, fieldWidth, LINE_HEIGHT);
                itemProp.stringValue = EditorGUI.TextField(fieldRect, labelText, itemProp.stringValue);

                if (listProp.arraySize > 1)
                {
                    var btnRect = new Rect(x + fieldWidth + 5, y, 20, LINE_HEIGHT);
                    if (GUI.Button(btnRect, "×"))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        listProp.serializedObject.ApplyModifiedProperties();
                        break;
                    }
                }
                y += LINE_HEIGHT + SPACING;
            }

            var addBtnRect = new Rect(x, y, width, LINE_HEIGHT);
            if (GUI.Button(addBtnRect, "+ 添加包"))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newItem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newItem.stringValue = $"Package{listProp.arraySize}";
                listProp.serializedObject.ApplyModifiedProperties();
            }
            y += LINE_HEIGHT + SPACING;

            return y;
        }

        #endregion

        #region 加密配置 IMGUI

        private static float DrawEncryptionConfigIMGUI(float x, float y, float width, SerializedProperty property)
        {
            var foldoutRect = new Rect(x, y, width, LINE_HEIGHT);
            sEncryptFoldout = EditorGUI.Foldout(foldoutRect, sEncryptFoldout, "加密配置", true, EditorStyles.foldoutHeader);
            y += LINE_HEIGHT + SPACING;

            if (!sEncryptFoldout) return y + SPACING;

            float contentX = x + INDENT;
            float contentWidth = width - INDENT;

            var encryptionTypeProp = property.FindPropertyRelative("EncryptionType");
            var typeRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            EditorGUI.PropertyField(typeRect, encryptionTypeProp, new GUIContent("加密类型"));
            y += LINE_HEIGHT + SPACING;

            var encryptionType = (YooEncryptionType)encryptionTypeProp.enumValueIndex;

            switch (encryptionType)
            {
                case YooEncryptionType.XorStream:
                    y = DrawXorEncryptionIMGUI(contentX, y, contentWidth, property);
                    break;
                case YooEncryptionType.FileOffset:
                    y = DrawFileOffsetIMGUI(contentX, y, contentWidth, property);
                    break;
                case YooEncryptionType.Aes:
                    y = DrawAesEncryptionIMGUI(contentX, y, contentWidth, property);
                    break;
                case YooEncryptionType.None:
                    var noneRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT * 2);
                    EditorGUI.HelpBox(noneRect, "资源将以明文形式存储。", MessageType.Info);
                    y += LINE_HEIGHT * 2 + SPACING;
                    break;
                case YooEncryptionType.Custom:
                    var customRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT * 2);
                    EditorGUI.HelpBox(customRect, "需要在代码中设置自定义加密委托。", MessageType.Info);
                    y += LINE_HEIGHT * 2 + SPACING;
                    break;
            }

            return y + SPACING;
        }

        private static float DrawXorEncryptionIMGUI(float x, float y, float width, SerializedProperty property)
        {
            var helpRect = new Rect(x, y, width, LINE_HEIGHT * 2);
            EditorGUI.HelpBox(helpRect, "XOR 流式加密：性能好，安全性适中。", MessageType.Info);
            y += LINE_HEIGHT * 2 + SPACING;

            var xorSeedProp = property.FindPropertyRelative("XorKeySeed");
            var seedRect = new Rect(x, y, width, LINE_HEIGHT);
            EditorGUI.PropertyField(seedRect, xorSeedProp, new GUIContent("密钥种子"));
            y += LINE_HEIGHT + SPACING;

            var btnRect = new Rect(x, y, width, LINE_HEIGHT);
            if (GUI.Button(btnRect, "重置为默认密钥"))
            {
                xorSeedProp.stringValue = YooInitConfig.DEFAULT_XOR_SEED;
                property.serializedObject.ApplyModifiedProperties();
            }
            y += LINE_HEIGHT + SPACING;

            return y;
        }

        private static float DrawFileOffsetIMGUI(float x, float y, float width, SerializedProperty property)
        {
            var helpRect = new Rect(x, y, width, LINE_HEIGHT * 2);
            EditorGUI.HelpBox(helpRect, "文件偏移：仅防止直接打开，无实际加密。", MessageType.Warning);
            y += LINE_HEIGHT * 2 + SPACING;

            var offsetProp = property.FindPropertyRelative("FileOffset");
            int currentIndex = Array.IndexOf(sOffsetValues, offsetProp.intValue);
            if (currentIndex < 0) currentIndex = 1;

            var popupRect = new Rect(x, y, width, LINE_HEIGHT);
            int newIndex = EditorGUI.Popup(popupRect, "偏移量", currentIndex, sOffsetChoices.ToArray());
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < sOffsetValues.Length)
            {
                offsetProp.intValue = sOffsetValues[newIndex];
                property.serializedObject.ApplyModifiedProperties();
            }
            y += LINE_HEIGHT + SPACING;

            return y;
        }

        private static float DrawAesEncryptionIMGUI(float x, float y, float width, SerializedProperty property)
        {
            var helpRect = new Rect(x, y, width, LINE_HEIGHT * 2);
            EditorGUI.HelpBox(helpRect, "AES 加密：安全性高，但性能开销大。", MessageType.None);
            y += LINE_HEIGHT * 2 + SPACING;

            var aesPasswordProp = property.FindPropertyRelative("AesPassword");
            var pwdRect = new Rect(x, y, width, LINE_HEIGHT);
            EditorGUI.PropertyField(pwdRect, aesPasswordProp, new GUIContent("密码"));
            y += LINE_HEIGHT + SPACING;

            var aesSaltProp = property.FindPropertyRelative("AesSalt");
            var saltRect = new Rect(x, y, width, LINE_HEIGHT);
            EditorGUI.PropertyField(saltRect, aesSaltProp, new GUIContent("盐值"));
            y += LINE_HEIGHT + SPACING;

            var btnRect = new Rect(x, y, width, LINE_HEIGHT);
            if (GUI.Button(btnRect, "重置为默认密钥"))
            {
                aesPasswordProp.stringValue = YooInitConfig.DEFAULT_AES_PASSWORD;
                aesSaltProp.stringValue = "YokiFram";
                property.serializedObject.ApplyModifiedProperties();
            }
            y += LINE_HEIGHT + SPACING;

            return y;
        }

        #endregion

        #region 打包配置 IMGUI

        private static float DrawBuildConfigIMGUI(float x, float y, float width, SerializedProperty property)
        {
            var foldoutRect = new Rect(x, y, width, LINE_HEIGHT);
            sBuildFoldout = EditorGUI.Foldout(foldoutRect, sBuildFoldout, "打包配置", true, EditorStyles.foldoutHeader);
            y += LINE_HEIGHT + SPACING;

            if (!sBuildFoldout) return y + SPACING;

            float contentX = x + INDENT;
            float contentWidth = width - INDENT;

            var packageNames = GetBuildPackageNames();
            if (packageNames.Count == 0)
            {
                var helpRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT * 2);
                EditorGUI.HelpBox(helpRect, "未找到资源包配置，请先配置资源包", MessageType.Warning);
                y += LINE_HEIGHT * 2 + SPACING;

                var btnRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
                if (GUI.Button(btnRect, "打开资源收集器"))
                    EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
                y += LINE_HEIGHT + SPACING;

                return y + SPACING;
            }

            // 资源包选择
            if (sSelectedPackageIndex >= packageNames.Count) sSelectedPackageIndex = 0;
            var pkgRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            sSelectedPackageIndex = EditorGUI.Popup(pkgRect, "资源包", sSelectedPackageIndex, packageNames.ToArray());
            string selectedPackage = packageNames[sSelectedPackageIndex];
            y += LINE_HEIGHT + SPACING;

            // 构建管线
            string currentPipeline = AssetBundleBuilderSetting.GetPackageBuildPipeline(selectedPackage);
            sSelectedPipelineIndex = sBuildPipelineChoices.IndexOf(currentPipeline);
            if (sSelectedPipelineIndex < 0) sSelectedPipelineIndex = 0;

            var pipeRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            int newPipelineIndex = EditorGUI.Popup(pipeRect, "构建管线", sSelectedPipelineIndex, sBuildPipelineChoices.ToArray());
            if (newPipelineIndex != sSelectedPipelineIndex)
            {
                sSelectedPipelineIndex = newPipelineIndex;
                AssetBundleBuilderSetting.SetPackageBuildPipeline(selectedPackage, sBuildPipelineChoices[newPipelineIndex]);
            }
            string selectedPipeline = sBuildPipelineChoices[sSelectedPipelineIndex];
            y += LINE_HEIGHT + SPACING;

            // 压缩方式
            var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(selectedPackage, selectedPipeline);
            var compRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            int newCompressIndex = EditorGUI.Popup(compRect, "压缩方式", (int)compressOption, sCompressChoices.ToArray());
            if (newCompressIndex != (int)compressOption)
                AssetBundleBuilderSetting.SetPackageCompressOption(selectedPackage, selectedPipeline, (ECompressOption)newCompressIndex);
            y += LINE_HEIGHT + SPACING;

            // 首包拷贝
            var copyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(selectedPackage, selectedPipeline);
            var copyRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            int newCopyIndex = EditorGUI.Popup(copyRect, "首包拷贝", (int)copyOption, sCopyOptionDisplayNames.ToArray());
            if (newCopyIndex != (int)copyOption)
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(selectedPackage, selectedPipeline, (EBuildinFileCopyOption)newCopyIndex);
            y += LINE_HEIGHT + SPACING;

            // 拷贝标签
            var copyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(selectedPackage, selectedPipeline);
            var paramsRect = new Rect(contentX, y, contentWidth, LINE_HEIGHT);
            string newCopyParams = EditorGUI.TextField(paramsRect, "拷贝标签", copyParams);
            if (newCopyParams != copyParams)
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(selectedPackage, selectedPipeline, newCopyParams);
            y += LINE_HEIGHT + SPACING;

            // 高级选项
            y = DrawAdvancedBuildOptionsIMGUI(contentX, y, contentWidth, selectedPackage, selectedPipeline);

            // 操作按钮
            y = DrawBuildButtonsIMGUI(contentX, y, contentWidth, selectedPackage, selectedPipeline);

            return y + SPACING;
        }

        private static float DrawAdvancedBuildOptionsIMGUI(float x, float y, float width, string selectedPackage, string selectedPipeline)
        {
            var foldoutRect = new Rect(x, y, width, LINE_HEIGHT);
            sAdvancedFoldout = EditorGUI.Foldout(foldoutRect, sAdvancedFoldout, "高级选项", true);
            y += LINE_HEIGHT + SPACING;

            if (!sAdvancedFoldout) return y;

            float innerX = x + INDENT;
            float innerWidth = width - INDENT;

            // 清空构建缓存
            var clearCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(selectedPackage, selectedPipeline);
            var clearRect = new Rect(innerX, y, innerWidth, LINE_HEIGHT);
            bool newClearCache = EditorGUI.Toggle(clearRect, "清空构建缓存", clearCache);
            if (newClearCache != clearCache)
                AssetBundleBuilderSetting.SetPackageClearBuildCache(selectedPackage, selectedPipeline, newClearCache);
            y += LINE_HEIGHT + SPACING;

            // 使用依赖缓存
            var useDepDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(selectedPackage, selectedPipeline);
            var depRect = new Rect(innerX, y, innerWidth, LINE_HEIGHT);
            bool newUseDepDB = EditorGUI.Toggle(depRect, "使用依赖缓存", useDepDB);
            if (newUseDepDB != useDepDB)
                AssetBundleBuilderSetting.SetPackageUseAssetDependencyDB(selectedPackage, selectedPipeline, newUseDepDB);
            y += LINE_HEIGHT + SPACING;

            // 加密服务
            var encryptClass = AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(selectedPackage, selectedPipeline);
            var encryptionClasses = GetEncryptionServiceClassNames();
            int encryptIndex = encryptionClasses.IndexOf(encryptClass);
            if (encryptIndex < 0) encryptIndex = 0;

            var encryptRect = new Rect(innerX, y, innerWidth, LINE_HEIGHT);
            int newEncryptIndex = EditorGUI.Popup(encryptRect, "加密服务", encryptIndex, encryptionClasses.ToArray());
            if (newEncryptIndex != encryptIndex)
                AssetBundleBuilderSetting.SetPackageEncyptionServicesClassName(selectedPackage, selectedPipeline, encryptionClasses[newEncryptIndex]);
            y += LINE_HEIGHT + SPACING;

            return y;
        }

        private static float DrawBuildButtonsIMGUI(float x, float y, float width, string selectedPackage, string selectedPipeline)
        {
            float halfWidth = (width - 5) / 2;

            var btn1Rect = new Rect(x, y, halfWidth, LINE_HEIGHT);
            if (GUI.Button(btn1Rect, "打开收集器"))
            {
                OpenYooAssetCollector();
            }

            var btn2Rect = new Rect(x + halfWidth + 5, y, halfWidth, LINE_HEIGHT);
            if (GUI.Button(btn2Rect, "打开原始收集器"))
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
            }
            y += LINE_HEIGHT + SPACING;

            var buildRect = new Rect(x, y, width, LINE_HEIGHT + 8);
            if (GUI.Button(buildRect, "构建资源包"))
            {
                if (EditorUtility.DisplayDialog("确认构建",
                    $"确定要构建资源包 [{selectedPackage}] 吗？\n\n" +
                    $"管线: {selectedPipeline}\n" +
                    $"平台: {EditorUserBuildSettings.activeBuildTarget}",
                    "确定", "取消"))
                {
                    ExecuteBuild(selectedPackage, selectedPipeline);
                }
            }
            y += LINE_HEIGHT + 8 + SPACING;

            return y;
        }

        #endregion
    }
}
#endif
