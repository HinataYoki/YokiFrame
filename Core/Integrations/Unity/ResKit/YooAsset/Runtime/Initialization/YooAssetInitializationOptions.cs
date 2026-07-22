#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEngine;
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

        /// <summary>Unity Editor 中使用的 YooAsset 运行模式。</summary>
        [Tooltip("Unity Editor 中使用的 YooAsset 运行模式")]
        public EPlayMode EditorPlayMode = EPlayMode.EditorSimulateMode;

        /// <summary>Player 中使用的 YooAsset 运行模式。</summary>
        [Tooltip("Player 中使用的 YooAsset 运行模式")]
        public EPlayMode RuntimePlayMode = EPlayMode.OfflinePlayMode;

        /// <summary>需要初始化的 package 快照；Unity Editor 由 YooAsset 收集器自动同步。</summary>
        [Tooltip("由 YooAsset 收集器自动同步，第一项作为 ResKit 默认 package")]
        public List<string> PackageNames = new() { DEFAULT_PACKAGE_NAME };

        /// <summary>package 初始化后是否请求版本并加载 manifest。</summary>
        [Tooltip("package 初始化后请求版本并加载 manifest")]
        public bool LoadManifestAfterInitialization = true;

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
