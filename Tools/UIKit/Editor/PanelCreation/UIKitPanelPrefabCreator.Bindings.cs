#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame
{
    internal static partial class UIKitPanelPrefabCreator
    {
        private static void AppendBindingFields(ICodeScope scope, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var typeName = GetBindFieldType(child, context);
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(fieldName))
                    continue;

                scope.Field(typeName, fieldName, field => field
                    .WithAccess(AccessModifier.Public));
            }

            if (children.Count > 0)
                scope.EmptyLine();
        }

        private static void AppendClearBindingFields(ICodeScope scope, BindCodeInfo bindInfo)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(fieldName))
                    continue;

                scope.Custom(fieldName + " = default;");
            }
        }

        private static string GetBindFieldName(BindCodeInfo bindInfo)
        {
            if (bindInfo == default || bindInfo.RepeatElement || string.IsNullOrEmpty(bindInfo.Name))
                return string.Empty;

            return bindInfo.Name;
        }

        private static string GetBindFieldType(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var strategy = BindStrategyRegistry.Get(bindInfo.Bind);
            return strategy != default ? strategy.GetFullTypeName(bindInfo, context) : bindInfo.Type;
        }

        private static List<BindCodeInfo> GetSortedChildren(BindCodeInfo bindInfo)
        {
            var children = new List<BindCodeInfo>();
            if (bindInfo == default || bindInfo.MemberDic == default)
                return children;

            foreach (var pair in bindInfo.MemberDic)
            {
                if (pair.Value != default)
                    children.Add(pair.Value);
            }

            children.Sort(static (left, right) => left.Order.CompareTo(right.Order));
            return children;
        }

        private static bool TryBindGeneratedPanel(string panelName, string scriptNamespace, string prefabPath, string scriptFolder, string assemblyName)
        {
            return UIKitPrefabBindingProcessor.TryBindGeneratedPanel(
                panelName,
                scriptNamespace,
                prefabPath,
                scriptFolder,
                assemblyName);
        }
    }
}
#endif
