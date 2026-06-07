using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 绑定器扩展方法
    /// 提供便捷的组件绑定 API
    /// </summary>
    public static class LocalizationBinderExtensions
    {
        #region 文本组件扩展

        /// <summary>
        /// 为 TextMeshProUGUI 创建泛型绑定
        /// </summary>
        public static LocalizedBinder<TextMeshProUGUI> BindLocalizationGeneric(
            this TextMeshProUGUI text,
            int textId)
        {
            return new LocalizedBinder<TextMeshProUGUI>(
                resourceId: textId,
                component: text,
                resourceGetter: LocalizationKit.Get,
                setter: static (tmp, str) => tmp.text = str,
                validityChecker: static tmp => tmp != default
            );
        }

        /// <summary>
        /// 为 Text 创建泛型绑定
        /// </summary>
        public static LocalizedBinder<Text> BindLocalizationGeneric(
            this Text text,
            int textId)
        {
            return new LocalizedBinder<Text>(
                resourceId: textId,
                component: text,
                resourceGetter: LocalizationKit.Get,
                setter: static (t, str) => t.text = str,
                validityChecker: static t => t != default
            );
        }

        #endregion

        #region 自定义绑定辅助方法

        /// <summary>
        /// 创建自定义组件的本地化绑定
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="resourceId">资源ID</param>
        /// <param name="resourceGetter">资源获取函数（从 LocalizationKit 或其他来源获取资源字符串）</param>
        /// <param name="setter">资源设置函数（将资源应用到组件）</param>
        /// <param name="validityChecker">可选的有效性检查（用于 Unity 对象判空）</param>
        /// <returns>绑定器实例</returns>
        /// <example>
        /// <code>
        /// // 示例：绑定自定义文本组件
        /// myCustomText.BindLocalization(
        ///     resourceId: 1001,
        ///     resourceGetter: id => LocalizationKit.Get(id),
        ///     setter: (component, text) => component.SetText(text),
        ///     validityChecker: component => component != default
        /// );
        /// </code>
        /// </example>
        public static LocalizedBinder<TComponent> BindLocalization<TComponent>(
            this TComponent component,
            int resourceId,
            Func<int, string> resourceGetter,
            Action<TComponent, string> setter,
            Func<TComponent, bool> validityChecker = null)
            where TComponent : class
        {
            return new LocalizedBinder<TComponent>(
                resourceId: resourceId,
                component: component,
                resourceGetter: resourceGetter,
                setter: setter,
                validityChecker: validityChecker
            );
        }

        /// <summary>
        /// 创建支持参数的自定义绑定
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <typeparam name="TArgs">参数类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="resourceId">资源ID</param>
        /// <param name="args">参数</param>
        /// <param name="resourceGetter">资源获取函数</param>
        /// <param name="setter">资源设置函数</param>
        /// <param name="validityChecker">可选的有效性检查</param>
        /// <returns>绑定器实例</returns>
        public static LocalizedBinder<TComponent, TArgs> BindLocalization<TComponent, TArgs>(
            this TComponent component,
            int resourceId,
            TArgs args,
            Func<int, TArgs, string> resourceGetter,
            Action<TComponent, string> setter,
            Func<TComponent, bool> validityChecker = null)
            where TComponent : class
        {
            return new LocalizedBinder<TComponent, TArgs>(
                resourceId: resourceId,
                component: component,
                args: args,
                resourceGetter: resourceGetter,
                setter: setter,
                validityChecker: validityChecker
            );
        }

        #endregion

        #region 示例：图片组件扩展（需用户根据项目资源加载方式实现）

        /// <summary>
        /// 为 Image 组件创建本地化图片绑定（示例）
        /// 注意：此方法需要用户根据项目的资源加载方式（如 YooAsset）自行实现 GetLocalizedSprite
        /// </summary>
        /// <param name="image">Image 组件</param>
        /// <param name="spriteId">图片资源ID</param>
        /// <param name="spriteGetter">Sprite 获取函数（输入ID和语言，返回对应 Sprite 路径或资源）</param>
        /// <returns>绑定器实例</returns>
        /// <example>
        /// <code>
        /// // 用户实现示例：
        /// myImage.BindLocalizedSprite(
        ///     spriteId: 2001,
        ///     spriteGetter: id => {
        ///         var lang = LocalizationKit.GetCurrentLanguage();
        ///         var path = $"Localization/{lang}/Sprites/{id}";
        ///         // 使用 YooAsset 或其他资源系统加载
        ///         var sprite = YooAssets.LoadAsset<Sprite>(path);
        ///         return sprite?.name ?? string.Empty; // 返回资源标识
        ///     }
        /// );
        /// </code>
        /// </example>
        public static LocalizedBinder<Image> BindLocalizedSprite(
            this Image image,
            int spriteId,
            Func<int, string> spriteGetter)
        {
            return new LocalizedBinder<Image>(
                resourceId: spriteId,
                component: image,
                resourceGetter: spriteGetter,
                setter: static (img, spritePath) =>
                {
                    // 用户需根据实际情况实现 Sprite 加载逻辑
                    // 这里仅作为示例框架
                    Debug.LogWarning($"[LocalizationKit] BindLocalizedSprite 需要用户自行实现 Sprite 加载逻辑。资源路径: {spritePath}");
                },
                validityChecker: static img => img != default
            );
        }

        #endregion
    }
}
