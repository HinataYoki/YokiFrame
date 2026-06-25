#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class UIKitPanelPrefabCreator
    {
        private static void GenerateCodeForPrefab(GameObject prefab, UIKitPanelCreateRequest request, string scriptFolder)
        {
            if (prefab == default)
                throw new System.ArgumentNullException(nameof(prefab));

            var bindInfo = CollectBindInfo(prefab, request.PanelName);
            var context = new UIKitPanelCodeGenContext(request.PanelName, scriptFolder, request.ScriptNamespace);
            WritePanelScript(request, scriptFolder);
            WritePanelDesignerScript(request, scriptFolder, bindInfo, context);
            WriteBindTypeScripts(bindInfo, context);
        }

        private static BindCodeInfo CollectBindInfo(GameObject prefab, string panelName)
        {
            var bindInfo = new BindCodeInfo
            {
                Type = panelName,
                Name = panelName,
                Self = prefab,
                Bind = BindType.Member
            };
            BindCollector.SearchBinds(prefab.transform, prefab.name, bindInfo);
            return bindInfo;
        }

        private static void WritePanelScript(UIKitPanelCreateRequest request, string scriptFolder)
        {
            var panelPath = GetPanelScriptPath(request, scriptFolder);
            if (File.Exists(panelPath))
                return;

            GenerateCSharpFile(panelPath, request.ScriptNamespace, false, scope =>
            {
                scope.Class(request.PanelName + "Data", "IUIData", false, false, cls => cls.AsSealed());
                scope.EmptyLine();
                scope.Class(request.PanelName, "UIPanel", true, false, cls =>
                {
                    if (IsMinimalCodeTemplate(request.CodeTemplate))
                        AppendMinimalPanelLifecycle(cls, request.PanelName);
                    else
                        AppendDefaultPanelLifecycle(cls, request.PanelName);
                });
            });
        }

        private static void AppendDefaultPanelLifecycle(ICodeScope scope, string panelName)
        {
            AppendPanelInit(scope, panelName);
            scope.EmptyLine();
            scope.ProtectedOverrideVoid("OnOpen", method => method
                .WithParameter("IUIData", "uiData", "null")
                .WithBody(body => body.Custom("mData = uiData as " + panelName + "Data ?? mData;")));
            scope.EmptyLine();
            scope.ProtectedOverrideVoid("OnShow", default);
            scope.EmptyLine();
            scope.ProtectedOverrideVoid("OnHide", default);
            scope.EmptyLine();
            scope.ProtectedOverrideVoid("OnClose", default);
        }

        private static void AppendMinimalPanelLifecycle(ICodeScope scope, string panelName)
        {
            AppendPanelInit(scope, panelName);
            scope.EmptyLine();
            scope.ProtectedOverrideVoid("OnClose", default);
        }

        private static void AppendPanelInit(ICodeScope scope, string panelName)
        {
            scope.ProtectedOverrideVoid("OnInit", method => method
                .WithParameter("IUIData", "uiData", "null")
                .WithBody(body => body.Custom("mData = uiData as " + panelName + "Data ?? new " + panelName + "Data();")));
        }

        private static void WritePanelDesignerScript(UIKitPanelCreateRequest request, string scriptFolder, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var designerPath = GetPanelDesignerPath(request, scriptFolder);
            GenerateCSharpFile(designerPath, request.ScriptNamespace, true, scope =>
            {
                scope.Class(request.PanelName, default, true, false, cls =>
                {
                    AppendBindingFields(cls, bindInfo, context);
                    cls.Field(request.PanelName + "Data", "mData", field => field
                        .WithAccess(AccessModifier.Private)
                        .WithAttribute("SerializeField"));
                    cls.EmptyLine();
                    cls.Property(request.PanelName + "Data", "Data", property => property
                        .WithModifiers(MemberModifier.New)
                        .WithGetter(getter => getter.Custom("return mData;")));
                    cls.EmptyLine();
                    cls.ProtectedOverrideVoid("ClearUIComponents", method => method
                        .WithBody(body =>
                        {
                            AppendClearBindingFields(body, bindInfo);
                            body.Custom("mData = null;");
                        }));
                });
            });
        }

        private static void WriteBindTypeScripts(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var strategy = BindStrategyRegistry.Get(child.Bind);
                if (strategy == default || !strategy.RequiresClassFile)
                    continue;

                WriteBindUserScript(child, context, strategy);
                WriteBindDesignerScript(child, context, strategy);
                WriteBindTypeScripts(child, context);
            }
        }

        private static void WriteBindUserScript(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context, IBindTypeStrategy strategy)
        {
            var scriptPath = strategy.GetScriptPath(bindInfo, context, false);
            if (string.IsNullOrEmpty(scriptPath) || File.Exists(scriptPath))
                return;

            var typeNamespace = strategy.GetNamespace(context);
            var baseClassName = strategy.GetBaseClassName();
            GenerateCSharpFile(scriptPath, typeNamespace, false, scope =>
            {
                scope.Class(bindInfo.Type, baseClassName, true, false, default);
            });
        }

        private static void WriteBindDesignerScript(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context, IBindTypeStrategy strategy)
        {
            var scriptPath = strategy.GetScriptPath(bindInfo, context, true);
            if (string.IsNullOrEmpty(scriptPath))
                return;

            var typeNamespace = strategy.GetNamespace(context);
            GenerateCSharpFile(scriptPath, typeNamespace, true, scope =>
            {
                scope.Class(bindInfo.Type, default, true, false, cls =>
                {
                    AppendBindingFields(cls, bindInfo, context);
                    cls.VoidMethod("Clear", method => method
                        .WithBody(body => AppendClearBindingFields(body, bindInfo)));
                });
            });
        }
    }
}
#endif
