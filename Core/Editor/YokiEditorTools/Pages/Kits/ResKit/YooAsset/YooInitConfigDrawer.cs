#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements;
#endif
using UnityEngine;
#if UNITY_2022_1_OR_NEWER
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;
#endif

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器
    /// Unity 2022.1+ 使用 UIToolkit 实现
    /// Unity 2021.3 使用 IMGUI 实现（见 YooInitConfigDrawer.IMGUI.cs）
    /// </summary>
    [CustomPropertyDrawer(typeof(YooInitConfig))]
    public partial class YooInitConfigDrawer : PropertyDrawer
    {
        #region 静态配置

        /// <summary>
        /// 文件偏移量选项（2 的幂）
        /// </summary>
        private static readonly List<string> sOffsetChoices = new()
        {
            "16", "32", "64", "128", "256", "512", "1024"
        };

        private static readonly int[] sOffsetValues = { 16, 32, 64, 128, 256, 512, 1024 };

        /// <summary>
        /// 构建管线选项
        /// </summary>
        private static readonly List<string> sBuildPipelineChoices = new()
        {
            "ScriptableBuildPipeline",
            "BuiltinBuildPipeline",
            "RawFileBuildPipeline"
        };

        /// <summary>
        /// 压缩选项
        /// </summary>
        private static readonly List<string> sCompressChoices = new()
        {
            "Uncompressed",
            "LZMA",
            "LZ4"
        };

        /// <summary>
        /// 内置文件拷贝选项中文描述
        /// </summary>
        private static readonly List<string> sCopyOptionDisplayNames = new()
        {
            "不拷贝",
            "清空后拷贝全部",
            "清空后按标签拷贝",
            "直接拷贝全部",
            "直接按标签拷贝"
        };

        #endregion

#if UNITY_2022_1_OR_NEWER
        /// <summary>
        /// UIToolkit 实现（Unity 2022.1+ 自动调用）
        /// </summary>
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.marginTop = Spacing.SM;
            root.style.marginBottom = Spacing.SM;

            YokiStyleService.Apply(root, YokiStyleProfile.CoreOnly);

            // 标题
            var header = new Label(property.displayName);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = Spacing.SM;
            root.Add(header);

            // 基础配置卡片
            var (basicCard, basicBody) = CreateConfigCard("基础配置", KitIcons.SETTINGS);
            root.Add(basicCard);

            // 编辑器下初始化模式
            basicBody.Add(CreatePlayModeField(property, "EditorPlayMode", "编辑器下初始化模式", false));
            
            // 真机下初始化模式（过滤掉 EditorSimulateMode）
            basicBody.Add(CreatePlayModeField(property, "RuntimePlayMode", "真机下初始化模式", true));
            
            // 资源包名称列表
            basicBody.Add(CreatePackageListField(property, "PackageNames", "资源包列表"));

            // 加密配置卡片
            var (encryptCard, encryptBody) = CreateConfigCard("加密配置", KitIcons.SETTINGS);
            encryptCard.style.marginTop = Spacing.MD;
            root.Add(encryptCard);

            var encryptionTypeField = CreatePropertyField(property, "EncryptionType", "加密类型");
            encryptBody.Add(encryptionTypeField);

            // 动态内容容器
            var dynamicContainer = new VisualElement();
            dynamicContainer.style.marginTop = Spacing.SM;
            encryptBody.Add(dynamicContainer);

            // 获取加密类型属性
            var encryptionTypeProp = property.FindPropertyRelative("EncryptionType");

            // 初始化动态内容
            UpdateEncryptionUI(dynamicContainer, property, (YooEncryptionType)encryptionTypeProp.enumValueIndex);

            // 监听加密类型变化
            encryptionTypeField.TrackPropertyValue(encryptionTypeProp, prop =>
            {
                UpdateEncryptionUI(dynamicContainer, property, (YooEncryptionType)prop.enumValueIndex);
            });

            // 打包配置卡片
            var buildCard = CreateBuildConfigCard(property);
            buildCard.style.marginTop = Spacing.MD;
            root.Add(buildCard);

            return root;
        }
#endif
    }
}
#endif
