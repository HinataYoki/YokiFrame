#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAssetInitializationBehaviour 的 Inspector 操作入口。
    /// 字段、提示和按钮均通过 InspectorKit 组合，不维护 YooAsset 私有样式。
    /// </summary>
    [CustomEditor(typeof(YooAssetInitializationBehaviour))]
    public sealed class YooAssetInitializationBehaviourEditor : UnityEditor.Editor
    {
        /// <summary>创建初始化选项、状态提示和常用操作组成的 UI Toolkit Inspector。</summary>
        /// <returns>绑定当前 SerializedObject 的视觉树。</returns>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            root.Add(InspectorKitUi.CreateToggleRow(
                serializedObject.FindProperty("mInitializeOnStart"),
                "Start 时初始化"));

            SerializedProperty options = serializedObject.FindProperty("mOptions");
            PropertyField optionsField = new(options, string.Empty);
            optionsField.BindProperty(options);
            root.Add(optionsField);

            root.Add(InspectorKitUi.CreateInfoBox(
                "ResKit Provider",
                "初始化成功后会自动把第一个资源包安装为 ResKit 的 YooAsset Provider。",
                InspectorInfoBoxType.Info));
            root.Add(CreateActions());
            return root;
        }

        /// <summary>创建 Play Mode 初始化、Provider 安装和资源收集器快捷操作。</summary>
        private VisualElement CreateActions()
        {
            Button initialize = InspectorKitUi.CreateActionButton(
                "立即初始化",
                StartInitialization,
                InspectorActionStyle.Primary,
                "仅在 Play Mode 执行 YooAsset 初始化");
            initialize.SetEnabled(Application.isPlaying);

            Button install = InspectorKitUi.CreateActionButton(
                "安装当前 Provider",
                InstallCurrentProvider);
            Button collector = InspectorKitUi.CreateActionButton(
                "打开资源收集器",
                YooAssetEditorWindows.OpenCollector,
                InspectorActionStyle.Success);
            return InspectorKitUi.CreateButtonRow(initialize, install, collector);
        }

        /// <summary>应用 Inspector 修改并让场景组件启动一次初始化。</summary>
        private void StartInitialization()
        {
            if (!Application.isPlaying)
                return;

            serializedObject.ApplyModifiedProperties();
            YooAssetInitializationBehaviour behaviour =
                target as YooAssetInitializationBehaviour;
            if (behaviour != null)
                behaviour.StartInitialization();
        }

        /// <summary>查找配置中的主 package，并把已就绪 package 安装到 ResKit。</summary>
        private void InstallCurrentProvider()
        {
            serializedObject.ApplyModifiedProperties();
            YooAssetInitializationBehaviour behaviour =
                target as YooAssetInitializationBehaviour;
            if (behaviour == null)
                return;

            try
            {
                ResourcePackage package = ResolveCurrentPackage(behaviour.Options.PrimaryPackageName);
                if (package == null)
                {
                    EditorUtility.DisplayDialog(
                        "YooAsset",
                        "没有找到已创建的资源包，请先初始化 YooAsset。",
                        "确定");
                    return;
                }

                YooAssetInitializer.InstallProvider(
                    package,
                    behaviour.Options.PlayMode == EPlayMode.EditorSimulateMode);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, behaviour);
            }
        }

        /// <summary>优先读取初始化器登记状态，再回退到 YooAsset 全局 package。</summary>
        private static ResourcePackage ResolveCurrentPackage(string packageName)
        {
            ResourcePackage package = YooAssetInitializer.GetPackage(packageName);
            if (package != null)
                return package;

#if YOKIFRAME_YOOASSET_3
            return YooAssets.TryGetPackage(packageName, out package) ? package : null;
#else
            return YooAssets.TryGetPackage(packageName);
#endif
        }
    }
}
#endif
