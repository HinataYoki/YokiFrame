#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAsset package 初始化参数。
    /// 该类型只保存可序列化数据，不执行初始化、不持有 package，也不管理 ResKit 生命周期。
    /// </summary>
    [Serializable]
    public sealed partial class YooAssetInitializationOptions
    {
        /// <summary>默认 YooAsset package 名称。</summary>
        public const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

        /// <summary>默认版本和清单请求超时时间，单位为秒。</summary>
        public const int DEFAULT_MANIFEST_TIMEOUT_SECONDS = 60;

        /// <summary>默认 XOR 密钥种子。</summary>
        public const string DEFAULT_XOR_KEY_SEED = "YokiFrame_XOR_Key_Seed_2025!@#$";

        /// <summary>默认 AES 密码。</summary>
        public const string DEFAULT_AES_PASSWORD = "YokiFrame_AES_2025";

        /// <summary>默认 AES 盐值。</summary>
        public const string DEFAULT_AES_SALT = "YokiFram";

        /// <summary>默认文件偏移量。</summary>
        public const int DEFAULT_FILE_OFFSET = 32;

        /// <summary>默认下载并发数。</summary>
        public const int DEFAULT_DOWNLOAD_MAXIMUM_CONCURRENCY = 10;

        /// <summary>默认下载失败重试次数。</summary>
        public const int DEFAULT_DOWNLOAD_RETRY_COUNT = 3;

        /// <summary>默认每帧发起的下载请求数。</summary>
        public const int DEFAULT_DOWNLOAD_MAX_REQUEST_PER_FRAME = 5;

        /// <summary>默认下载无进度超时时间，单位为秒。</summary>
        public const int DEFAULT_DOWNLOAD_NO_PROGRESS_TIMEOUT_SECONDS = 60;

        /// <summary>Unity Editor 中使用的 YooAsset 运行模式。</summary>
        [Tooltip("Unity Editor 中使用的 YooAsset 运行模式")]
        public EPlayMode EditorPlayMode = EPlayMode.EditorSimulateMode;

        /// <summary>Player 中使用的 YooAsset 运行模式。</summary>
        [Tooltip("Player 中使用的 YooAsset 运行模式")]
        public EPlayMode RuntimePlayMode = EPlayMode.OfflinePlayMode;

        /// <summary>需要初始化的 package 快照；Unity Editor 由 YooAsset 收集器自动同步。</summary>
        [Tooltip("由 YooAsset 收集器自动同步；ResKit 按此顺序自动探测，第一项是起点")]
        public List<string> PackageNames = new() { DEFAULT_PACKAGE_NAME };

        /// <summary>package 初始化后是否请求版本并加载 manifest。</summary>
        [Tooltip("package 初始化后请求版本并加载 manifest")]
        public bool LoadManifestAfterInitialization = true;

        /// <summary>控制联网 package 的清单、下载和离线回退流程。</summary>
        [Tooltip("控制联网 package 的清单、下载和离线回退流程")]
        public YooAssetInitializationStrategy InitializationStrategy = YooAssetInitializationStrategy.ManifestOnly;

        /// <summary>Host 模式是否把包体内置清单复制到沙盒，供弱网回退使用。</summary>
        [Tooltip("Host 模式把包体内置清单复制到沙盒，供弱网回退使用")]
        public bool CopyBuiltinPackageManifest = true;

        /// <summary>内置清单复制到自定义沙盒根目录时使用的目标目录；为空时使用 YooAsset 默认目录。</summary>
        [Tooltip("自定义沙盒根目录时填写内置清单复制目标目录；为空使用默认目录")]
        public string CopyBuiltinPackageManifestDestRoot;

        /// <summary>覆盖安装时的清理策略；None 可保留已复制的内置清单。</summary>
        [Tooltip("覆盖安装时的清理策略")]
        public YooAssetInstallCleanupMode InstallCleanupMode = YooAssetInstallCleanupMode.None;

        /// <summary>启动阶段下载资源的最大并发数。</summary>
        [Tooltip("启动阶段下载资源的最大并发数")]
        public int DownloadMaximumConcurrency = DEFAULT_DOWNLOAD_MAXIMUM_CONCURRENCY;

        /// <summary>单个资源下载失败后的重试次数。</summary>
        [Tooltip("单个资源下载失败后的重试次数")]
        public int DownloadRetryCount = DEFAULT_DOWNLOAD_RETRY_COUNT;

        /// <summary>文件系统每帧允许发起的最大下载请求数。</summary>
        [Tooltip("文件系统每帧允许发起的最大下载请求数")]
        public int DownloadMaxRequestPerFrame = DEFAULT_DOWNLOAD_MAX_REQUEST_PER_FRAME;

        /// <summary>下载请求持续无新数据时允许等待的时间，单位为秒。</summary>
        [Tooltip("下载持续无新数据时允许等待的时间，单位为秒")]
        [FormerlySerializedAs("DownloadWatchdogTimeoutSeconds")]
        public int DownloadNoProgressTimeoutSeconds = DEFAULT_DOWNLOAD_NO_PROGRESS_TIMEOUT_SECONDS;

        /// <summary>版本和 manifest 请求超时时间，非正数使用默认值。</summary>
        [Tooltip("版本和 manifest 请求超时时间，单位为秒")]
        public int ManifestTimeoutSeconds = DEFAULT_MANIFEST_TIMEOUT_SECONDS;

        /// <summary>Host/Web 模式主资源服务器地址。</summary>
        [Tooltip("Host/Web 模式主资源服务器地址")]
        public string DefaultHostServer;

        /// <summary>Host/Web 模式备用资源服务器地址；为空时使用主地址。</summary>
        [Tooltip("Host/Web 模式备用资源服务器地址")]
        public string FallbackHostServer;

        /// <summary>资源包构建加密与运行时解密共用的方案；Editor 仅显示扫描到成对实现的方案。</summary>
        [Tooltip("构建加密与运行时解密共用；Inspector 只显示扫描到成对实现的方案")]
        public YooAssetEncryptionMode EncryptionMode;

        /// <summary>XOR 流式解密使用的密钥种子。</summary>
        [Tooltip("XOR 流式解密使用的密钥种子")]
        public string XorKeySeed = DEFAULT_XOR_KEY_SEED;

        /// <summary>文件偏移解密跳过的文件头字节数。</summary>
        [Tooltip("文件偏移解密跳过的文件头字节数")]
        public int FileOffset = DEFAULT_FILE_OFFSET;

        /// <summary>AES 解密使用的密码。</summary>
        [Tooltip("AES 解密使用的密码")]
        public string AesPassword = DEFAULT_AES_PASSWORD;

        /// <summary>AES 解密使用的盐值，长度不足时由 Integration 补齐。</summary>
        [Tooltip("AES 解密使用的盐值，建议至少 8 个 ASCII 字符")]
        public string AesSalt = DEFAULT_AES_SALT;

        /// <summary>获取当前编译目标实际使用的 YooAsset 运行模式。</summary>
        public EPlayMode PlayMode
        {
            get
            {
#if UNITY_EDITOR
                return EditorPlayMode;
#else
                return RuntimePlayMode;
#endif
            }
        }

        /// <summary>获取第一个有效 package 名称；不存在时返回默认名称。</summary>
        public string PrimaryPackageName
        {
            get
            {
                if (PackageNames == null)
                    return DEFAULT_PACKAGE_NAME;

                for (int index = 0; index < PackageNames.Count; index++)
                {
                    string packageName = PackageNames[index];
                    if (!string.IsNullOrWhiteSpace(packageName))
                        return packageName.Trim();
                }

                return DEFAULT_PACKAGE_NAME;
            }
        }

        /// <summary>
        /// 获取有效 manifest 超时时间，避免把非正数交给 YooAsset。
        /// </summary>
        /// <returns>大于零的超时秒数。</returns>
        public int GetManifestTimeoutSeconds()
        {
            return ManifestTimeoutSeconds > 0
                ? ManifestTimeoutSeconds
                : DEFAULT_MANIFEST_TIMEOUT_SECONDS;
        }

        /// <summary>获取有效的启动下载并发数。</summary>
        /// <returns>限制在 YooAsset 支持范围内的并发数。</returns>
        public int GetDownloadMaximumConcurrency()
        {
            return Math.Max(1, Math.Min(32, DownloadMaximumConcurrency));
        }

        /// <summary>获取有效的下载重试次数。</summary>
        /// <returns>不小于零的重试次数。</returns>
        public int GetDownloadRetryCount()
        {
            return Math.Max(0, DownloadRetryCount);
        }

        /// <summary>获取有效的每帧下载请求数。</summary>
        /// <returns>不小于一的每帧请求数。</returns>
        public int GetDownloadMaxRequestPerFrame()
        {
            return Math.Max(1, DownloadMaxRequestPerFrame);
        }

        /// <summary>获取有效的下载无进度超时时间。</summary>
        /// <returns>大于零的超时秒数。</returns>
        public int GetDownloadNoProgressTimeoutSeconds()
        {
            return DownloadNoProgressTimeoutSeconds > 0
                ? DownloadNoProgressTimeoutSeconds
                : DEFAULT_DOWNLOAD_NO_PROGRESS_TIMEOUT_SECONDS;
        }
    }

    /// <summary>定义 YooAsset package 的联网更新和回退策略。</summary>
    public enum YooAssetInitializationStrategy
    {
        /// <summary>只按原有流程初始化并加载清单，不自动下载资源。</summary>
        ManifestOnly,
        /// <summary>远端版本、清单或资源下载失败时直接返回失败。</summary>
        RemoteOnly,
        /// <summary>远端失败后优先恢复上一次完整下载版本，再尝试包体内置版本。</summary>
        RemoteThenCached,
        /// <summary>远端流程失败后销毁联网 package，改用包体内置 OfflinePlayMode。</summary>
        RemoteThenOffline
    }

    /// <summary>定义 Host 模式覆盖安装时的缓存清理策略。</summary>
    public enum YooAssetInstallCleanupMode
    {
        /// <summary>不清理缓存，允许保留复制的内置清单和已有资源。</summary>
        None,
        /// <summary>清理全部缓存文件。</summary>
        ClearAllCacheFiles,
        /// <summary>只清理缓存资源文件。</summary>
        ClearAllBundleFiles,
        /// <summary>只清理缓存清单文件。</summary>
        ClearAllManifestFiles
    }

    /// <summary>YooAsset 资源包运行时解密方案。</summary>
    public enum YooAssetEncryptionMode
    {
        /// <summary>不使用额外解密。</summary>
        None,
        /// <summary>使用 XOR 流式解密。</summary>
        XorStream,
        /// <summary>跳过文件头偏移区域。</summary>
        FileOffset,
        /// <summary>使用 AES-CBC 全量解密。</summary>
        Aes,
        /// <summary>由项目注册自定义解密服务。</summary>
        Custom
    }
}
#endif
