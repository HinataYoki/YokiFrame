#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>发现并管理 Editor-only UIKit 代码模板，模板不拥有任何文件写入。</summary>
    public static class UIKitCodeTemplateRegistry
    {
        /// <summary>默认完整生命周期模板名。</summary>
        public const string DEFAULT_TEMPLATE_NAME = "Default";

        /// <summary>内置精简生命周期模板名。</summary>
        public const string MINIMAL_TEMPLATE_NAME = "Minimal";

        private static readonly Dictionary<string, IUIKitCodeTemplate> sTemplates =
            new(StringComparer.Ordinal);
        private static IReadOnlyList<string> sTemplateNames = Array.AsReadOnly(Array.Empty<string>());
        private static bool sInitialized;

        /// <summary>获取按 Default、Minimal、项目模板排序的模板名。</summary>
        /// <returns>当前不可变名称快照。</returns>
        public static IReadOnlyList<string> GetTemplateNames()
        {
            EnsureInitialized();
            return sTemplateNames;
        }

        /// <summary>注册一个项目模板；名称重复时拒绝覆盖现有所有者。</summary>
        /// <param name="template">具有公开无参构造或项目显式创建的模板。</param>
        public static void Register(IUIKitCodeTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            EnsureInitialized();
            RegisterCore(template, false);
            RebuildNameSnapshot();
        }

        /// <summary>注销一个项目模板；内置 Default/Minimal 不允许注销。</summary>
        /// <param name="templateName">待注销模板名。</param>
        /// <returns>成功移除项目模板时返回 true。</returns>
        public static bool Unregister(string templateName)
        {
            EnsureInitialized();
            if (IsBuiltIn(templateName) || !sTemplates.Remove(templateName)) return false;
            RebuildNameSnapshot();
            return true;
        }

        /// <summary>尝试按区分大小写的稳定名称读取模板。</summary>
        /// <param name="templateName">模板名。</param>
        /// <param name="template">找到的模板。</param>
        /// <returns>当前 Registry 包含模板时返回 true。</returns>
        public static bool TryGet(string templateName, out IUIKitCodeTemplate template)
        {
            EnsureInitialized();
            return sTemplates.TryGetValue(templateName ?? string.Empty, out template);
        }

        /// <summary>清空项目模板并重新执行确定性 TypeCache 发现。</summary>
        public static void Refresh()
        {
            sTemplates.Clear();
            sTemplateNames = Array.AsReadOnly(Array.Empty<string>());
            sInitialized = false;
            EnsureInitialized();
        }

        /// <summary>要求模板存在，用于布局验证和生成流水线。</summary>
        /// <param name="templateName">模板名。</param>
        /// <returns>唯一模板实例。</returns>
        internal static IUIKitCodeTemplate Require(string templateName)
        {
            if (TryGet(templateName, out IUIKitCodeTemplate template)) return template;
            throw new ArgumentException("未知 UIKit 代码模板: " + templateName, nameof(templateName));
        }

        /// <summary>首次使用时安装内置模板并发现项目实现。</summary>
        private static void EnsureInitialized()
        {
            if (sInitialized) return;
            sInitialized = true;
            RegisterCore(new UIKitBuiltInCodeTemplate(
                DEFAULT_TEMPLATE_NAME,
                "生成完整 UIKit 生命周期模板。"), true);
            RegisterCore(new UIKitBuiltInCodeTemplate(
                MINIMAL_TEMPLATE_NAME,
                "只生成必要生命周期入口。"), true);
            DiscoverProjectTemplates();
            RebuildNameSnapshot();
        }

        /// <summary>通过 Unity TypeCache 按类型全名稳定发现项目模板。</summary>
        private static void DiscoverProjectTemplates()
        {
            List<Type> types = new(TypeCache.GetTypesDerivedFrom<IUIKitCodeTemplate>());
            types.Sort(static (left, right) => string.Compare(
                left.FullName,
                right.FullName,
                StringComparison.Ordinal));
            for (var index = 0; index < types.Count; index++)
            {
                Type type = types[index];
                if (type.IsAbstract || type.IsInterface
                    || type == typeof(UIKitBuiltInCodeTemplate)
                    || type.GetConstructor(Type.EmptyTypes) == null) continue;
                TryCreateDiscoveredTemplate(type);
            }
        }

        /// <summary>实例化一个发现类型，失败时记录具体类型但继续其它模板。</summary>
        /// <param name="type">候选模板类型。</param>
        private static void TryCreateDiscoveredTemplate(Type type)
        {
            try
            {
                IUIKitCodeTemplate template = Activator.CreateInstance(type) as IUIKitCodeTemplate;
                if (template == null) return;
                RegisterCore(template, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[UIKit] 无法加载代码模板 " + type.FullName + ": " + exception.Message);
            }
        }

        /// <summary>校验并登记模板；自动发现重复项只记录并跳过。</summary>
        /// <param name="template">待登记模板。</param>
        /// <param name="skipDuplicate">重复时是否跳过而不是抛出。</param>
        private static void RegisterCore(IUIKitCodeTemplate template, bool skipDuplicate)
        {
            string name = RequireTemplateName(template.Name);
            if (sTemplates.ContainsKey(name))
            {
                if (skipDuplicate)
                {
                    Debug.LogWarning("[UIKit] 代码模板名称重复，已保留首个实现: " + name);
                    return;
                }

                throw new ArgumentException("UIKit 代码模板名称已注册: " + name, nameof(template));
            }

            sTemplates.Add(name, template);
        }

        /// <summary>验证模板名可以安全进入命令 payload 与项目设置。</summary>
        /// <param name="name">候选名称。</param>
        /// <returns>裁剪后的安全名称。</returns>
        private static string RequireTemplateName(string name)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            if (normalized.Length == 0
                || normalized.Length > YokiFrameSafeIdContract.MAX_LENGTH
                || !YokiFrameSafeIdContract.IsSafeId(normalized))
            {
                throw new ArgumentException("UIKit 代码模板名必须是安全 ID: " + name, nameof(name));
            }

            return normalized;
        }

        /// <summary>判断模板名是否属于不能被项目注销的内置模板。</summary>
        private static bool IsBuiltIn(string templateName)
        {
            return string.Equals(templateName, DEFAULT_TEMPLATE_NAME, StringComparison.Ordinal)
                || string.Equals(templateName, MINIMAL_TEMPLATE_NAME, StringComparison.Ordinal);
        }

        /// <summary>重建稳定只读名称快照，避免 UI 枚举可变 Dictionary。</summary>
        private static void RebuildNameSnapshot()
        {
            List<string> names = new(sTemplates.Keys);
            names.Sort(CompareTemplateNames);
            sTemplateNames = Array.AsReadOnly(names.ToArray());
        }

        /// <summary>固定内置模板在前，其余项目模板按 ordinal 排序。</summary>
        private static int CompareTemplateNames(string left, string right)
        {
            int leftOrder = GetBuiltInOrder(left);
            int rightOrder = GetBuiltInOrder(right);
            return leftOrder != rightOrder
                ? leftOrder.CompareTo(rightOrder)
                : string.Compare(left, right, StringComparison.Ordinal);
        }

        /// <summary>返回内置模板排序值，项目模板统一排在其后。</summary>
        private static int GetBuiltInOrder(string name)
        {
            if (string.Equals(name, DEFAULT_TEMPLATE_NAME, StringComparison.Ordinal)) return 0;
            if (string.Equals(name, MINIMAL_TEMPLATE_NAME, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
#endif
