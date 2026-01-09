using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 代码生成模板注册表 - 管理所有可用的代码生成模板
    /// </summary>
    public static class UICodeGenTemplateRegistry
    {
        #region 常量

        /// <summary>
        /// 默认模板名称
        /// </summary>
        public const string DEFAULT_TEMPLATE_NAME = "Default";

        #endregion

        #region 静态字段

        private static readonly Dictionary<string, IUICodeGenTemplate> sTemplates = new(4);
        private static string sActiveTemplateName;
        private static bool sInitialized;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前激活的模板名称
        /// </summary>
        public static string ActiveTemplateName
        {
            get
            {
                EnsureInitialized();
                return sActiveTemplateName ?? DEFAULT_TEMPLATE_NAME;
            }
        }

        /// <summary>
        /// 当前激活的模板（永不为 null）
        /// </summary>
        public static IUICodeGenTemplate ActiveTemplate
        {
            get
            {
                EnsureInitialized();

                // 尝试获取指定模板
                if (!string.IsNullOrEmpty(sActiveTemplateName) && sTemplates.TryGetValue(sActiveTemplateName, out var template))
                {
                    return template;
                }

                // 回退到默认模板
                if (sTemplates.TryGetValue(DEFAULT_TEMPLATE_NAME, out var defaultTemplate))
                {
                    return defaultTemplate;
                }

                // 最后保底：创建默认模板
                var fallback = new DefaultUICodeGenTemplate();
                Register(fallback);
                return fallback;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 注册模板
        /// </summary>
        /// <param name="template">模板实例</param>
        public static void Register(IUICodeGenTemplate template)
        {
            if (template == null) return;

            if (sTemplates.ContainsKey(template.TemplateName))
            {
                Debug.LogWarning($"[UICodeGenTemplateRegistry] 模板 '{template.TemplateName}' 已存在，将被覆盖");
            }

            sTemplates[template.TemplateName] = template;
        }

        /// <summary>
        /// 设置激活的模板（同时保存到配置）
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>是否设置成功</returns>
        public static bool SetActiveTemplate(string templateName)
        {
            EnsureInitialized();

            if (!sTemplates.ContainsKey(templateName))
            {
                Debug.LogError($"[UICodeGenTemplateRegistry] 模板 '{templateName}' 不存在");
                return false;
            }

            sActiveTemplateName = templateName;
            
            // 保存到配置
            UIKitCreateConfig.Instance.CodeGenTemplateName = templateName;
            UIKitCreateConfig.Instance.SaveConfig();
            
            return true;
        }

        /// <summary>
        /// 从配置加载激活的模板
        /// </summary>
        public static void LoadActiveTemplateFromConfig()
        {
            EnsureInitialized();
            
            var configTemplateName = UIKitCreateConfig.Instance.CodeGenTemplateName;
            if (!string.IsNullOrEmpty(configTemplateName) && sTemplates.ContainsKey(configTemplateName))
            {
                sActiveTemplateName = configTemplateName;
            }
            else
            {
                sActiveTemplateName = DEFAULT_TEMPLATE_NAME;
            }
        }

        /// <summary>
        /// 获取指定模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>模板实例，未找到返回 null</returns>
        public static IUICodeGenTemplate Get(string templateName)
        {
            EnsureInitialized();
            return sTemplates.TryGetValue(templateName, out var template) ? template : null;
        }

        /// <summary>
        /// 尝试获取指定模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <param name="template">模板实例（输出）</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGet(string templateName, out IUICodeGenTemplate template)
        {
            EnsureInitialized();
            return sTemplates.TryGetValue(templateName, out template);
        }

        /// <summary>
        /// 获取所有已注册的模板名称
        /// </summary>
        public static IEnumerable<string> GetAllTemplateNames()
        {
            EnsureInitialized();
            return sTemplates.Keys;
        }

        /// <summary>
        /// 获取所有已注册的模板
        /// </summary>
        public static IEnumerable<IUICodeGenTemplate> GetAllTemplates()
        {
            EnsureInitialized();
            return sTemplates.Values;
        }

        /// <summary>
        /// 重新扫描并注册模板（用于热重载）
        /// </summary>
        public static void Refresh()
        {
            sTemplates.Clear();
            sInitialized = false;
            EnsureInitialized();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (sInitialized) return;

            // 注册默认模板
            Register(new DefaultUICodeGenTemplate());

            // 自动发现并注册用户自定义模板
            AutoDiscoverTemplates();

            // 从配置加载激活的模板
            var configTemplateName = UIKitCreateConfig.Instance.CodeGenTemplateName;
            if (!string.IsNullOrEmpty(configTemplateName) && sTemplates.ContainsKey(configTemplateName))
            {
                sActiveTemplateName = configTemplateName;
            }
            else
            {
                sActiveTemplateName = DEFAULT_TEMPLATE_NAME;
            }

            sInitialized = true;
        }

        /// <summary>
        /// 自动发现用户自定义模板
        /// </summary>
        private static void AutoDiscoverTemplates()
        {
            // 使用 TypeCache 查找所有实现 IUICodeGenTemplate 的类型
            var templateTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IUICodeGenTemplate>();

            foreach (var type in templateTypes)
            {
                // 跳过抽象类和接口
                if (type.IsAbstract || type.IsInterface) continue;

                // 跳过默认模板（已手动注册）
                if (type == typeof(DefaultUICodeGenTemplate)) continue;

                try
                {
                    var template = (IUICodeGenTemplate)Activator.CreateInstance(type);
                    Register(template);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UICodeGenTemplateRegistry] 无法实例化模板 {type.Name}: {e.Message}");
                }
            }
        }

        #endregion
    }
}
