#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class UIKitPanelPrefabCreator
    {
        private static bool GenerateCodeForPrefab(GameObject prefab, UIKitPanelCreateRequest request, string scriptFolder)
        {
            if (prefab == default)
                throw new System.ArgumentNullException(nameof(prefab));

            var bindInfo = CollectBindInfo(prefab, request.PanelName);
            var context = new UIKitPanelCodeGenContext(request.PanelName, scriptFolder, request.ScriptNamespace);
            var scriptsChanged = false;
            scriptsChanged |= WritePanelScript(request, scriptFolder);
            scriptsChanged |= WritePanelDesignerScript(request, scriptFolder, bindInfo, context);
            scriptsChanged |= WriteBindTypeScripts(bindInfo, context);
            return scriptsChanged;
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

        private static bool WritePanelScript(UIKitPanelCreateRequest request, string scriptFolder)
        {
            var panelPath = GetPanelScriptPath(request, scriptFolder);
            if (File.Exists(panelPath))
                return UpdateExistingUserScriptNamespace(panelPath, request.PanelName, request.ScriptNamespace);

            return GenerateCSharpFile(panelPath, request.ScriptNamespace, false, scope =>
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

        private static bool WritePanelDesignerScript(UIKitPanelCreateRequest request, string scriptFolder, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var designerPath = GetPanelDesignerPath(request, scriptFolder);
            return GenerateCSharpFile(designerPath, request.ScriptNamespace, true, scope =>
            {
                scope.Class(request.PanelName, default, true, false, cls =>
                {
                    AppendBindingFields(cls, bindInfo, context);
                    cls.Field(request.PanelName + "Data", "mData", field => field
                        .WithAccess(AccessModifier.Private)
                        .WithAttribute("SerializeField"));
                    cls.EmptyLine();
                    cls.Property(request.PanelName + "Data", "Data", property => property
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

        private static bool WriteBindTypeScripts(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var scriptsChanged = false;
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var strategy = BindStrategyRegistry.Get(child.Bind);
                if (strategy == default || !strategy.RequiresClassFile)
                    continue;

                scriptsChanged |= WriteBindUserScript(child, context, strategy);
                scriptsChanged |= WriteBindDesignerScript(child, context, strategy);
                scriptsChanged |= WriteBindTypeScripts(child, context);
            }

            return scriptsChanged;
        }

        private static bool WriteBindUserScript(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context, IBindTypeStrategy strategy)
        {
            var scriptPath = strategy.GetScriptPath(bindInfo, context, false);
            if (string.IsNullOrEmpty(scriptPath))
                return false;

            var typeNamespace = strategy.GetNamespace(context);
            if (File.Exists(scriptPath))
                return UpdateExistingUserScriptNamespace(scriptPath, bindInfo.Type, typeNamespace);

            var baseClassName = strategy.GetBaseClassName();
            return GenerateCSharpFile(scriptPath, typeNamespace, false, scope =>
            {
                scope.Class(bindInfo.Type, baseClassName, true, false, default);
            });
        }

        /// <summary>
        /// 只迁移已有用户脚本的块级命名空间，保留类型内的全部用户代码。
        /// </summary>
        private static bool UpdateExistingUserScriptNamespace(string scriptPath, string typeName, string targetNamespace)
        {
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
                return false;
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("生成类型名不能为空。", nameof(typeName));
            if (string.IsNullOrEmpty(targetNamespace) || !IsValidNamespace(targetNamespace))
                throw new InvalidOperationException("命名空间不合法: " + targetNamespace);

            var source = File.ReadAllText(scriptPath);
            var classMatch = Regex.Match(
                source,
                @"\bpartial\s+class\s+" + Regex.Escape(typeName) + @"\b",
                RegexOptions.CultureInvariant);
            if (!classMatch.Success)
            {
                throw new InvalidOperationException(
                    "已有用户脚本未找到 partial class " + typeName + "，无法安全迁移命名空间: " + scriptPath);
            }

            var namespaceMatches = Regex.Matches(
                source,
                @"(?m)^[ \t]*namespace[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*(?:\r?\n[ \t]*)?\{",
                RegexOptions.CultureInvariant);
            Match namespaceMatch = null;
            for (var i = 0; i < namespaceMatches.Count; i++)
            {
                if (namespaceMatches[i].Index >= classMatch.Index)
                    break;

                namespaceMatch = namespaceMatches[i];
            }

            if (namespaceMatch == null)
            {
                throw new InvalidOperationException(
                    "已有用户脚本未找到包含 " + typeName + " 的块级命名空间，无法安全迁移: " + scriptPath);
            }

            var nameGroup = namespaceMatch.Groups["name"];
            if (string.Equals(nameGroup.Value, targetNamespace, StringComparison.Ordinal))
                return false;

            var migrated = source.Substring(0, nameGroup.Index) + targetNamespace +
                           source.Substring(nameGroup.Index + nameGroup.Length);
            File.WriteAllText(scriptPath, migrated, new System.Text.UTF8Encoding(false));
            return true;
        }

        private static bool WriteBindDesignerScript(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context, IBindTypeStrategy strategy)
        {
            var scriptPath = strategy.GetScriptPath(bindInfo, context, true);
            if (string.IsNullOrEmpty(scriptPath))
                return false;

            var typeNamespace = strategy.GetNamespace(context);
            return GenerateCSharpFile(scriptPath, typeNamespace, true, scope =>
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
