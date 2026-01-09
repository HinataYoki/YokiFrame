#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// LocalizationKit 异步扩展
    /// 使用 UniTask 实现异步加载功能
    /// </summary>
    public static class LocalizationKitAsync
    {
        /// <summary>
        /// 异步加载语言数据
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="progress">加载进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask LoadLanguageAsync(
            LanguageId languageId,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            var provider = LocalizationKit.GetProvider();
            if (provider == null)
            {
                throw new InvalidOperationException("LocalizationKit Provider 未设置");
            }

            // 检查是否支持异步加载
            if (provider is IAsyncLocalizationProvider asyncProvider)
            {
                await asyncProvider.LoadLanguageAsync(languageId, progress, cancellationToken);
            }
            else
            {
                // 同步加载，在线程池执行
                progress?.Report(0f);
                await UniTask.RunOnThreadPool(() =>
                {
                    provider.PreloadLanguage(languageId);
                }, cancellationToken: cancellationToken);
                progress?.Report(1f);
            }

            KitLogger.Log($"[LocalizationKit] 语言异步加载完成: {languageId}");
        }

        /// <summary>
        /// 异步切换语言（包含加载）
        /// </summary>
        /// <param name="languageId">语言标识符</param>
        /// <param name="progress">加载进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功切换</returns>
        public static async UniTask<bool> SetLanguageAsync(
            LanguageId languageId,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            // 先加载语言数据
            await LoadLanguageAsync(languageId, progress, cancellationToken);

            // 切换语言
            return LocalizationKit.SetLanguage(languageId);
        }

        /// <summary>
        /// 异步获取文本（支持延迟加载）
        /// </summary>
        /// <param name="textId">文本ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本地化文本</returns>
        public static async UniTask<string> GetAsync(
            int textId,
            CancellationToken cancellationToken = default)
        {
            var currentLanguage = LocalizationKit.GetCurrentLanguage();

            // 如果语言未加载，先加载
            if (!LocalizationKit.IsLanguageLoaded(currentLanguage))
            {
                await LoadLanguageAsync(currentLanguage, cancellationToken: cancellationToken);
            }

            return LocalizationKit.Get(textId);
        }

        /// <summary>
        /// 异步获取带参数的文本
        /// </summary>
        public static async UniTask<string> GetAsync(
            int textId,
            object[] args,
            CancellationToken cancellationToken = default)
        {
            var currentLanguage = LocalizationKit.GetCurrentLanguage();

            if (!LocalizationKit.IsLanguageLoaded(currentLanguage))
            {
                await LoadLanguageAsync(currentLanguage, cancellationToken: cancellationToken);
            }

            return LocalizationKit.Get(textId, args);
        }

        /// <summary>
        /// 异步卸载语言
        /// </summary>
        public static async UniTask UnloadLanguageAsync(
            LanguageId languageId,
            CancellationToken cancellationToken = default)
        {
            var provider = LocalizationKit.GetProvider();
            if (provider == null) return;

            if (provider is IAsyncLocalizationProvider asyncProvider)
            {
                await asyncProvider.UnloadLanguageAsync(languageId, cancellationToken);
            }
            else
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    LocalizationKit.UnloadLanguage(languageId);
                }, cancellationToken: cancellationToken);
            }

            KitLogger.Log($"[LocalizationKit] 语言异步卸载完成: {languageId}");
        }
    }

    /// <summary>
    /// 异步本地化数据提供者接口
    /// </summary>
    public interface IAsyncLocalizationProvider : ILocalizationProvider
    {
        /// <summary>
        /// 异步加载语言数据
        /// </summary>
        UniTask LoadLanguageAsync(
            LanguageId languageId,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步卸载语言数据
        /// </summary>
        UniTask UnloadLanguageAsync(
            LanguageId languageId,
            CancellationToken cancellationToken = default);
    }
}
#endif
